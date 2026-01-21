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
    public class PatientState
    {
        public int HeartRate { get; set; } = 75;
        public int Oxygen { get; set; } = 98;
        public float Temperature { get; set; } = 36.5f;
        public int Counter { get; set; } = 0;

        // Tracking for Alert Throttling
        public string LastStatus { get; set; } = "Normal";
        public DateTime LastAlertSentTime { get; set; } = DateTime.MinValue;
    }

    public class VitalSimulationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<VitalsHub> _hub;
        private readonly AlertService _alertService;

        // Cache Lists
        private List<AlertHistory> _alertCache = new List<AlertHistory>();
        private List<VitalSigns> _vitalCache = new List<VitalSigns>();

        private DateTime _lastDbSaveTime = DateTime.Now;
        private readonly TimeSpan _saveInterval = TimeSpan.FromMinutes(1); // Set to 1 min for your review

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
                    var patients = db.Patients.ToList();

                    foreach (var patient in patients)
                    {
                        if (!_patientStates.ContainsKey(patient.PatientId))
                            _patientStates[patient.PatientId] = new PatientState();

                        var state = _patientStates[patient.PatientId];
                        state.Counter++;

                        // --- 1. SIMULATION (Mother) ---
                        bool forceAbnormal = state.Counter % 30 == 0;

                        if (forceAbnormal)
                        {
                            state.HeartRate = random.Next(120, 150);
                        }
                        else if (state.LastStatus != "Normal")
                        {
                            if (random.NextDouble() > 0.2) state.HeartRate = random.Next(120, 150);
                            else state.HeartRate = 75;
                        }
                        else
                        {
                            state.HeartRate = Math.Clamp(state.HeartRate + random.Next(-2, 3), 60, 100);
                        }

                        state.Oxygen = (state.HeartRate > 110) ? random.Next(90, 95) : random.Next(96, 100);

                        // --- 2. SIMULATION (Fetus - Only if Pregnant) ---
                        int? fetalHr = null;
                        if (patient.IsPregnant)
                        {
                            // Fetal HR is typically 110-160
                            fetalHr = 140 + random.Next(-5, 15);

                            // Occasional dip simulation
                            if (random.Next(0, 100) > 98) fetalHr = 100;
                        }

                        // --- 3. GET STATUS ---
                        string currentStatus = _alertService.GetStatus(state.HeartRate, state.Oxygen, state.Temperature);

                        // --- 4. STORAGE LOGIC ---
                        if (currentStatus != "Normal")
                        {
                            _vitalCache.Add(new VitalSigns
                            {
                                PatientId = patient.PatientId,
                                HeartRate = state.HeartRate,
                                Oxygen = state.Oxygen,
                                Temperature = state.Temperature,
                                FetalHeartRate = fetalHr, // Store Fetal Data
                                UpdatedAt = DateTime.Now
                            });
                        }

                        // --- 5. ALERT LOGIC ---
                        if (currentStatus != "Normal")
                        {
                            bool isStatusChange = (currentStatus != state.LastStatus);
                            bool isTimeExpired = (DateTime.Now - state.LastAlertSentTime).TotalMinutes >= 1;

                            if (isStatusChange || isTimeExpired)
                            {
                                var alert = new AlertHistory
                                {
                                    PatientId = patient.PatientId,
                                    Severity = currentStatus,
                                    Message = $"{currentStatus} Vitals: HR {state.HeartRate}, SpO2 {state.Oxygen}%",
                                    Timestamp = DateTime.Now
                                };
                                _alertCache.Add(alert);
                                state.LastAlertSentTime = DateTime.Now;
                            }
                        }

                        state.LastStatus = currentStatus;

                        // --- 6. SEND TO UI ---
                        var payload = new
                        {
                            PatientId = patient.PatientId,
                            ECG = state.HeartRate,
                            SpO2 = state.Oxygen,
                            Temp = state.Temperature,
                            Status = currentStatus,

                            // NEW: Send Pregnancy Data
                            IsPregnant = patient.IsPregnant,
                            FetalHR = fetalHr
                        };
                        await _hub.Clients.All.SendAsync("ReceiveVitals", payload, stoppingToken);
                    }

                    // --- 7. DATABASE BATCH SAVE ---
                    if (DateTime.Now - _lastDbSaveTime > _saveInterval)
                    {
                        if (_alertCache.Any() || _vitalCache.Any())
                        {
                            db.AlertHistory.AddRange(_alertCache);
                            db.VitalSigns.AddRange(_vitalCache);
                            await db.SaveChangesAsync(stoppingToken);
                            _alertCache.Clear();
                            _vitalCache.Clear();
                        }
                        _lastDbSaveTime = DateTime.Now;
                    }
                }
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}

