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

namespace HealthAndAuditShared
{
    public interface IRuleStorage
    {
        List<AnalyseRuleset> GetAllRuleSets();
        List<AnalyseRuleset> GetRuleSetsForApplication(string applicationName);
    }

    public class DocStorageAzure : IRuleStorage
    {
        public List<AnalyseRuleset> GetAllRuleSets()
        {
            throw new NotImplementedException();
        }

        public List<AnalyseRuleset> GetRuleSetsForApplication(string applicationName)
        {
            throw new NotImplementedException();
        }
    }


    public class TestRuleStorage : IRuleStorage
    {
        public List<AnalyseRuleset> GetAllRuleSets()
        {
            return GetRuleSetsForApplication("");
        }

        public List<AnalyseRuleset> GetRuleSetsForApplication(string applicationName)
        {
            var rules = new MaxAmountOfFailuresRule();
            rules.ApplicationName = "Eventpump.vshost.exe";
            rules.RuleName = "TestRule001";
            rules.KeepOperationInPileTime = new TimeSpan(0, 30, 0);
            rules.MaxTimesFailureAllowed = 10;
            var list = new List<AnalyseRuleset>();
            list.Add(rules);
            return list;
        }

        
    }



}
