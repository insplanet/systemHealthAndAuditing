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

using System.Web.Mvc;

namespace ControlCentre.Controllers
{
    public class ListRulesController : Controller
    {
        // GET: ListRules
        public ActionResult Index()
        {
            var docdb = HelperMethods.GetRuleStorage();
            var all = docdb.GetAllRuleSets();

            return View("ListAll",all);
        }
    }
}