using Microsoft.AspNetCore.Mvc;

namespace Login.WebApi.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
