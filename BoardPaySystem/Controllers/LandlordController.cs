using Microsoft.AspNetCore.Mvc;

namespace BoardPaySystem.Controllers
{
    public class LandlordController : Controller
    {
        public IActionResult Overview()
        {
            return View();
        }
        public IActionResult ManageBuildings()
        {
            return View();
        }
        public IActionResult Billing()
        {
            return View();
        }
        public IActionResult AddTenant()
        {
            return View();
        }
        public IActionResult ManageTenants()
        {
            return View();
        }
        public IActionResult MeterReadings()
        {
            return View();
        }
        public IActionResult Reports()
        {
            return View();
        }
    }
}
