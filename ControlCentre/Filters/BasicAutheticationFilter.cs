
using System;
using System.Collections.Generic;
using System.Configuration;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http.Filters;
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