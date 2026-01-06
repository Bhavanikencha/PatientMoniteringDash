using Microsoft.AspNetCore.Mvc;

namespace PatinetMo.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
