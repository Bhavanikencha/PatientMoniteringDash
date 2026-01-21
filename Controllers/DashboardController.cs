using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatinetMo.Data;
using PatinetMo.Models.ViewModels;

namespace PatinetMo.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }


        public IActionResult Index()
        {
            var viewModel = new DashboardViewModel
            {
                Patients = _context.Patients
                    .Include(p => p.Doctor)
                    .Include(p => p.Medications) // Load Meds
                    .Include(p => p.Surgeries)   // Load Surgeries
                    .Include(p => p.Conditions)  // Load Conditions
                    .ToList(),

                RecentAlerts = _context.AlertHistory.Include(a => a.Patient).ToList()
            };
            return View(viewModel);
        }

    }
}
