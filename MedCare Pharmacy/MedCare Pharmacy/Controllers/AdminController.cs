using Microsoft.AspNetCore.Mvc;

namespace MedCare_Pharmacy.Controllers
{
    public class AdminController : Controller
    {
        // GET: /Admin hoặc /Admin/Index (Ánh xạ từ admin/index.html)
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Admin/Medicines (Ánh xạ từ admin/medicines.html)
        public IActionResult Medicines()
        {
            return View();
        }

        // GET: /Admin/Categories (Ánh xạ từ admin/categories.html)
        public IActionResult Categories()
        {
            return View();
        }

        // GET: /Admin/Suppliers (Ánh xạ từ admin/suppliers.html)
        public IActionResult Suppliers()
        {
            return View();
        }

        // GET: /Admin/NhapKho (Ánh xạ từ admin/nhap-kho.html)
        public IActionResult NhapKho()
        {
            return View();
        }

        // GET: /Admin/Users (Ánh xạ từ admin/users.html)
        public IActionResult Users()
        {
            return View();
        }

        // GET: /Admin/Reports (Ánh xạ từ admin/reports.html)
        public IActionResult Reports()
        {
            return View();
        }
    }
}
