using Microsoft.AspNetCore.Mvc;

namespace MedCare_Pharmacy.Controllers
{
    public class PharmacistController : Controller
    {
        // GET: /Pharmacist hoặc /Pharmacist/Index (Ánh xạ từ pharmacist/index.html)
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Pharmacist/Pos (Ánh xạ từ pharmacist/pos.html)
        public IActionResult Pos()
        {
            return View();
        }

        // GET: /Pharmacist/OnlineOrders (Ánh xạ từ pharmacist/online-orders.html)
        public IActionResult OnlineOrders()
        {
            return View();
        }

        // GET: /Pharmacist/InventoryAlert (Ánh xạ từ pharmacist/inventory-alert.html)
        public IActionResult InventoryAlert()
        {
            return View();
        }

        // GET: /Pharmacist/Consultation (Ánh xạ từ pharmacist/consultation.html)
        public IActionResult Consultation()
        {
            return View();
        }
    }
}
