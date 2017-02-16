using System.Web.Mvc;
using System.Web.Mvc.Filters;
using ControlCentre.Controllers;
using IAuthenticationFilter = System.Web.Mvc.Filters.IAuthenticationFilter;

namespace ControlCentre
{
    public class BasicAutheticationFilter :  IAuthenticationFilter
    {
        public void OnAuthentication(AuthenticationContext filterContext)
        {
         
        }

        public void OnAuthenticationChallenge(AuthenticationChallengeContext filterContext)
        {
            if (filterContext.Controller is LoginController)
            {
                return;
            }
            if(filterContext.HttpContext.Request.Cookies["validLogin"] == null)
            {
                filterContext.Result = new HttpUnauthorizedResult();
            }
        }
    }
}