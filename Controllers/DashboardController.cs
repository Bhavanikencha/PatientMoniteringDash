
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatinetMo.Data;
using System.Linq;

namespace PatinetMo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("patients")]
        public IActionResult GetPatients()
        {
            var patients = _context.Patients
                .AsNoTracking()
                .Include(p => p.Doctor)
                .Include(p => p.Medications)
                .Include(p => p.Conditions)
                .Include(p => p.Surgeries) // <--- Added
                .Select(p => new {
                    p.PatientId,
                    p.Name,
                    p.IsPregnant,
                    p.AdmissionDate,
                    p.DateOfBirth,         // <--- Added
                    p.Mobile,              // <--- Added
                    p.Address,             // <--- Added
                    p.BloodType,           // <--- Added
                    p.FamilyHistory,       // <--- Added
                    DoctorName = p.Doctor != null ? p.Doctor.Name : "Unassigned",

                    // Lists
                    Medications = p.Medications.Select(m => new { m.DrugName, m.Dosage }).ToList(),
                    Conditions = p.Conditions.Select(c => new { c.Diagnosis }).ToList(),
                    Surgeries = p.Surgeries.Select(s => new { s.ProcedureName, s.Year }).ToList() // <--- Added
                })
                .ToList();

            return Ok(patients);
        }



        [HttpGet("alerts")]
        public IActionResult GetAlerts()
        {
            // 1. Fetch raw data (Get more than 20 to ensure we capture full episodes)
            var rawAlerts = _context.AlertHistory
                .AsNoTracking()
                .Include(a => a.Patient)
                .OrderByDescending(a => a.Timestamp)
                .Take(100) // Fetch last 100 to group them effectively
                .ToList();

            // 2. SMART GROUPING LOGIC
            var groupedAlerts = new List<object>();

            if (rawAlerts.Any())
            {
                // Start with the most recent alert
                var currentGroup = rawAlerts.First();
                var endTime = currentGroup.Timestamp;
                var startTime = currentGroup.Timestamp;

                for (int i = 1; i < rawAlerts.Count; i++)
                {
                    var nextAlert = rawAlerts[i];

                    // Check if this alert belongs to the SAME "Episode" as the previous one
                    // Conditions: Same Patient + Same Severity + Time difference is less than 2 minutes
                    bool isSameEpisode =
                        nextAlert.PatientId == currentGroup.PatientId &&
                        nextAlert.Severity == currentGroup.Severity &&
                        (startTime - nextAlert.Timestamp).TotalMinutes < 1.5; // Allow small gaps

                    if (isSameEpisode)
                    {
                        // Extend the start time backwards
                        startTime = nextAlert.Timestamp;
                    }
                    else
                    {
                        // Finalize the previous group and add to list
                        groupedAlerts.Add(new
                        {
                            Severity = currentGroup.Severity,
                            Message = currentGroup.Message,
                            PatientName = currentGroup.Patient.Name,
                            // Format: "9:00 - 9:05" or just "9:00" if singular
                            Time = (startTime == endTime)
                                ? startTime.ToString("HH:mm:ss")
                                : $"{startTime:HH:mm} - {endTime:HH:mm} ({Math.Round((endTime - startTime).TotalMinutes + 1)} min)"
                        });

                        // Start a new group
                        currentGroup = nextAlert;
                        endTime = nextAlert.Timestamp;
                        startTime = nextAlert.Timestamp;
                    }
                }

                // Add the final group
                groupedAlerts.Add(new
                {
                    Severity = currentGroup.Severity,
                    Message = currentGroup.Message,
                    PatientName = currentGroup.Patient.Name,
                    Time = (startTime == endTime)
                        ? startTime.ToString("HH:mm:ss")
                        : $"{startTime:HH:mm} - {endTime:HH:mm} ({Math.Round((endTime - startTime).TotalMinutes + 1)} min)"
                });
            }

            return Ok(groupedAlerts);
        }


    }
}


