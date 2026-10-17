using Microsoft.AspNetCore.Mvc;

namespace MedCare_Pharmacy.Controllers
{
    public class CustomerController : Controller
    {
        // GET: /Customer/Login
        public IActionResult Login()
        {
            return View();
        }

        // GET: /Customer/Register
        public IActionResult Register()
        {
            return View();
        }

        // GET: /Customer/Products
        public IActionResult Products()
        {
            return View();
        }

        // GET: /Customer/ProductDetail
        public IActionResult ProductDetail()
        {
            return View();
        }

        // GET: /Customer/Cart
        public IActionResult Cart()
        {
            return View();
        }

        // GET: /Customer/Checkout
        public IActionResult Checkout()
        {
            return View();
        }

        // GET: /Customer/Orders
        public IActionResult Orders()
        {
            return View();
        }

        // GET: /Customer/Prescription
        public IActionResult Prescription()
        {
            return View();
        }

        // GET: /Customer/Consultation
        public IActionResult Consultation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(); // Sẽ render file ForgotPassword.cshtml trong Views/Customer/
        }

        [HttpGet]
        public IActionResult Profile()
        {
            return View();
        }
    }
}
