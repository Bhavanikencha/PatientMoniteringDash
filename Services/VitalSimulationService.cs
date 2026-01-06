using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using PatinetMo.Data;
using PatinetMo.Hubs;
using PatinetMo.Models;
using PatinetMo.Services;

namespace PatientMo.Services
{
    public class VitalSimulationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<VitalsHub> _hub;
        private readonly AlertService _alertService;

        public VitalSimulationService(
            IServiceScopeFactory scopeFactory,
            IHubContext<VitalsHub> hub,
            AlertService alertService)
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
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var patients = db.Patients.ToList();

                foreach (var patient in patients)
                {
                    int heartRate = random.Next(60, 140);
                    int oxygen = random.Next(88, 100);
                    float temperature = (float)(36 + random.NextDouble() * 2.5);

                    // Save in database
                    var vital = new VitalSigns
                    {
                        PatientId = patient.PatientId,
                        HeartRate = heartRate,
                        Oxygen = oxygen,
                        Temperature = temperature,
                        UpdatedAt = DateTime.Now
                    };
                    db.VitalSigns.Add(vital);

                    // Send data to UI
                    var payload = new
                    {
                        PatientId = patient.PatientId,
                        PatientName = patient.Name,
                        ECG = heartRate,
                        RESP = random.Next(12, 20),
                        SpO2 = oxygen,
                        CO2 = random.Next(35, 45),
                        IBP = "120/80",
                        NIBP = "118/78"
                    };

                    await _hub.Clients.All.SendAsync("ReceiveVitals", payload);



                    db.SaveChanges();
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
    }
}
