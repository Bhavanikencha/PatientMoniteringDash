
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatinetMo.Data;
using PatinetMo.Hubs;
using PatinetMo.Models;
using PatinetMo.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PatientMo.Services
{
    // Internal state tracking
    public class PatientState
    {
        public int HeartRate { get; set; } = 75;
        public int Oxygen { get; set; } = 98;
        public float Temperature { get; set; } = 36.5f;
        public int? FetalHeartRate { get; set; } = null;

        public int Counter { get; set; } = 0;
        public string LastStatus { get; set; } = "Normal";

        // Timers
        public DateTime LastAlertDbTime { get; set; } = DateTime.MinValue;
        public DateTime LastBroadcastTime { get; set; } = DateTime.MinValue;
        public DateTime LastSimulationTime { get; set; } = DateTime.MinValue;
    }

    public class VitalSimulationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<VitalsHub> _hub;
        private readonly AlertService _alertService;

        // Buffers for DB Batching
        private List<AlertHistory> _alertCache = new List<AlertHistory>();
        private List<VitalSigns> _vitalCache = new List<VitalSigns>();
        private DateTime _lastDbBatchSaveTime = DateTime.Now;
        private readonly TimeSpan _dbSaveInterval = TimeSpan.FromMinutes(1);

        private readonly ConcurrentDictionary<int, PatientState> _patientStates = new();

        public VitalSimulationService(IServiceScopeFactory scopeFactory, IHubContext<VitalsHub> hub, AlertService alertService)
        {
            _scopeFactory = scopeFactory;
            _hub = hub;
            _alertService = alertService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var random = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    // Fetch list of patients (using AsNoTracking for speed since we only read IDs)
                    var patients = db.Patients.Select(p => new { p.PatientId, p.IsPregnant }).ToList();

                    foreach (var patient in patients)
                    {
                        if (!_patientStates.ContainsKey(patient.PatientId))
                            _patientStates[patient.PatientId] = new PatientState();

                        var state = _patientStates[patient.PatientId];

                        // --- 1. SIMULATION MATH (Runs every 200ms) ---
                        // Update physics logic if 1 second has passed (or keep it fast for smooth waves)
                        // Here we update slightly faster for the monitor visuals
                        if ((DateTime.Now - state.LastSimulationTime).TotalMilliseconds >= 1500)
                        {
                            state.Counter++;
                            state.LastSimulationTime = DateTime.Now;

                            // A. Force Abnormality (Every 60 ticks)
                            bool forceAbnormal = state.Counter % 60 == 0;

                            if (forceAbnormal)
                            {
                                state.HeartRate = random.Next(130, 160); // Critical High
                            }
                            else if (state.LastStatus != "Normal")
                            {
                                // Recovery chance
                                if (random.NextDouble() > 0.3) state.HeartRate = 75;
                            }
                            else
                            {
                                // CHANGE 2: Natural Drift. Only change by -1, 0, or +1.
                                // Humans don't jump from 70 to 75 instantly.
                                int drift = random.Next(-1, 2);
                                state.HeartRate = Math.Clamp(state.HeartRate + drift, 60, 100);
                            }

                            // B. Calculate O2 and Temp (Keep correlated with HR)
                            // O2 fluctuates very slowly
                            state.Oxygen = (state.HeartRate > 110) ? random.Next(85, 94) : Math.Clamp(state.Oxygen + random.Next(-1, 2), 95, 100);

                            // Temp fluctuates very slowly
                            state.Temperature = (state.HeartRate > 110) ? 38.5f : 36.5f + (float)(random.NextDouble() * 0.2);

                            // C. Fetal Heart Rate
                            if (patient.IsPregnant)
                            {
                                state.FetalHeartRate = (state.FetalHeartRate ?? 140) + random.Next(-1, 2);
                            }
                        }



                        // Determine Status
                        string currentStatus = _alertService.GetStatus(state.HeartRate, state.Oxygen, state.Temperature);

                        // --- 2. HEAVY PAYLOAD (Targeted Group Broadcast) ---
                        // Send precise waveform data FAST (every 200ms) 
                        // BUT ONLY to the specific SignalR Group "Patient_{ID}"
                        var monitorData = new
                        {
                            PatientId = patient.PatientId,
                            ECG = state.HeartRate,
                            SpO2 = state.Oxygen,
                            Temp = Math.Round(state.Temperature,1),
                            FetalHR = state.FetalHeartRate,
                            Status = currentStatus
                        };

                        // This uses the Join/Leave logic we added to VitalsHub.cs
                        await _hub.Clients.Group($"Patient_{patient.PatientId}")
                                .SendAsync("ReceiveWaveform", monitorData, stoppingToken);


                        // --- 3. LIGHTWEIGHT PAYLOAD (Global Broadcast) ---
                        // Send summary status to EVERYONE (the main grid)
                        // Throttle this to once per second (unless Critical)
                        bool timeToBroadcast = (DateTime.Now - state.LastBroadcastTime).TotalSeconds >= 1;
                        bool isCritical = (currentStatus == "Critical" || currentStatus == "Warning");
                        bool statusChanged = (currentStatus != state.LastStatus);

                        if (timeToBroadcast || isCritical)
                        {
                            var dashboardData = new
                            {
                                PatientId = patient.PatientId,
                                Status = currentStatus,
                                // FIXED: Using "ECG" to match the frontend expectation
                                ECG = state.HeartRate,
                                SpO2 = state.Oxygen,
                                Temp = Math.Round(state.Temperature,1)// <--- Added Temp
                            };

                            await _hub.Clients.All.SendAsync("ReceiveDashboardUpdate", dashboardData, stoppingToken);
                            state.LastBroadcastTime = DateTime.Now;
                        }


                        // --- 4. DATA STORAGE (Alerts & Vitals) ---
                        bool alertTimeout = (DateTime.Now - state.LastAlertDbTime).TotalMinutes >= 1;

                        if (isCritical && (statusChanged || alertTimeout))
                        {
                            _alertCache.Add(new AlertHistory
                            {
                                PatientId = patient.PatientId,
                                Severity = currentStatus,
                                Message = $"{currentStatus} Vitals: HR {state.HeartRate}, SpO2 {state.Oxygen}%",
                                Timestamp = DateTime.Now
                            });

                            _vitalCache.Add(new VitalSigns
                            {
                                PatientId = patient.PatientId,
                                HeartRate = state.HeartRate,
                                Oxygen = state.Oxygen,
                                Temperature = state.Temperature,
                                FetalHeartRate = state.FetalHeartRate,
                                UpdatedAt = DateTime.Now
                            });

                            state.LastAlertDbTime = DateTime.Now;
                        }

                        state.LastStatus = currentStatus;
                    }

                    // --- 5. DATABASE BATCH SAVE ---
                    if ((DateTime.Now - _lastDbBatchSaveTime) > _dbSaveInterval)
                    {
                        if (_alertCache.Any() || _vitalCache.Any())
                        {
                            using (var saveScope = _scopeFactory.CreateScope())
                            {
                                var saveDb = saveScope.ServiceProvider.GetRequiredService<AppDbContext>();
                                if (_alertCache.Any()) await saveDb.AlertHistory.AddRangeAsync(_alertCache, stoppingToken);
                                if (_vitalCache.Any()) await saveDb.VitalSigns.AddRangeAsync(_vitalCache, stoppingToken);
                                await saveDb.SaveChangesAsync(stoppingToken);
                                _alertCache.Clear();
                                _vitalCache.Clear();
                            }
                        }
                        _lastDbBatchSaveTime = DateTime.Now;
                    }
                }

                // Loop Speed: 200ms
                await Task.Delay(200, stoppingToken);
            }
        }
    }
}
