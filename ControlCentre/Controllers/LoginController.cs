using System.Configuration;
using System.Web;
using System.Web.Mvc;

namespace ControlCentre.Controllers
{
    public class LoginController : Controller
    {
        // GET: Login
        public ActionResult Index()
        {
            return View("login");
        }

        public ActionResult Login(string username, string password)
        {
            if(username == ConfigurationManager.AppSettings["username"] && password == ConfigurationManager.AppSettings["password"])
            {
                var cookie = new HttpCookie("validLogin");
                HttpContext.Response.Cookies.Add(cookie);
                Redirect(Url.Content("~/"));
            }
            
            return View("login");
            
        }
    }
}