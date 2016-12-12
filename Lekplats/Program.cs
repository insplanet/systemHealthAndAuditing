using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HealthAndAuditShared;
using static System.Console;

namespace Lekplats
{
    class Program
    {
        static void Main(string[] args)
        {
            
            var docdb = new DocumentDBRuleStorage(ConfigurationManager.AppSettings["EndPointUrl"], ConfigurationManager.AppSettings["AuthorizationKey"],ConfigurationManager.AppSettings["RuleDatabaseId"],ConfigurationManager.AppSettings["RuleCollectionId"]);

            var ruleset = new MaxAmountOfFailuresRule();
            ruleset.ApplicationName = "test";
            ruleset.MaxTimesFailureAllowed = 30;
            ruleset.OperationName = "test33";

            docdb.UpsertRuleSetAsync(ruleset).Wait();


            var result = docdb.GetAllRuleSets();

            var f = 99;

        }
}

}
