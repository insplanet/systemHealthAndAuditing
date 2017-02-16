using System;
using System.Configuration;
using HealthAndAuditShared;

namespace Lekplats
{
    class Program
    {
        static void Main(string[] args)
        {
            
            var docdb = new DocumentDBRuleStorage(ConfigurationManager.AppSettings["DocDBEndPointUrl"], ConfigurationManager.AppSettings["AuthorizationKey"],ConfigurationManager.AppSettings["RuleDatabaseId"],ConfigurationManager.AppSettings["RuleCollectionId"]);

            var rules = new MaxAmountOfFailuresRule();
            rules.ApplicationName = "Eventpump.vshost.exe";
            rules.RuleName = "TestRule001";
            rules.KeepOperationInPileTime = new TimeSpan(0, 30, 0);
            rules.MaxTimesFailureAllowed = 10;
          

            var rs = new FailurePercentRule();
            rs.ApplicationName = "Eventpump.vshost.exe";
            rs.RuleName = "TestRule002";
            rs.KeepOperationInPileTime = new TimeSpan(0, 30, 0);
            rs.MaxFailurePercent = 80;
            rs.MinimumAmountOfOperationsBeforeRuleCanBeTriggered = 20;

            

            

            var f = 99;

        }
}

}
