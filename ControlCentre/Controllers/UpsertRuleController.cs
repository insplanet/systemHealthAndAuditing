/****************************************************************************************
*	This code originates from the software development department at					*
*	swedish insurance and private loan broker Insplanet AB.								*
*	Full license available in license.txt												*
*	This text block may not be removed or altered.                                  	*
*	The list of contributors may be extended.                                           *
*																						*
*							Mikael Axblom, head of software development, Insplanet AB	*
*																						*
*	Contributors: Mikael Axblom															*
*****************************************************************************************/
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HealthAndAuditShared;

namespace ControlCentre.Controllers
{
    public class UpsertRuleController : Controller
    {
        // GET: UpsertRule
        [HttpGet]
        public ActionResult Index()
        {
            return View("ruleview");
        }

        [HttpPost]
        [ActionName("create")]
        public  ActionResult Create(MaxAmountOfFailuresRule ruleset)
        {
            var t = ruleset;
            return View("ruleview");
        }


    }
}