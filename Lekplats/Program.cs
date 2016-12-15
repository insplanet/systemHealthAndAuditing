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
            ruleset.MaxTimesFailureAllowed = 4;
            ruleset.OperationName = "test33";
            ruleset.RuleName = "amount4";

            var rs = new FailurePercentRule();
            rs.ApplicationName = "test";
            rs.MaxFailurePercent = 22;
            rs.OperationName = "test33";
            rs.RuleName = "percent22";


            docdb.UpsertRuleSetAsync(ruleset).Wait();
            docdb.UpsertRuleSetAsync(rs).Wait();

            var result = docdb.GetAllRuleSets();

            var f = 99;

        }
}

}
