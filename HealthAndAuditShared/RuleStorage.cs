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
        List<AnalyzeRule> GetAllRules();
        List<AnalyzeRule> GetRulesForApplication(string applicationName);
        void UpsertRuleSet(AnalyzeRule ruleset);
        void DeleteRuleSet(AnalyzeRule ruleset);
    }

  

    public class TestRuleStorage : IRuleStorage
    {
        public List<AnalyzeRule> GetAllRules()
        {
            return GetRulesForApplication("");
        }

        public List<AnalyzeRule> GetRulesForApplication(string applicationName)
        {
            var rules = new MaxAmountOfFailuresRule();
            rules.ProgramName = "Eventpump.vshost.exe";
            rules.RuleName = "TestRule001";
            rules.KeepOperationInPileTime = new TimeSpan(0, 30, 0);
            rules.MaxTimesFailureAllowed = 10;
            var list = new List<AnalyzeRule>();
            list.Add(rules);

            var rs = new FailurePercentRule();
            rs.ProgramName = "Eventpump.vshost.exe";
            rs.RuleName = "TestRule002";
            rs.KeepOperationInPileTime = new TimeSpan(0, 30, 0);
            rs.MaxFailurePercent = 80;
            rs.MinimumAmountOfOperationsBeforeRuleCanBeTriggered = 20;
            list.Add(rs);

            return list;
        }

        public void UpsertRuleSet(AnalyzeRule ruleset)
        {
            throw new NotImplementedException();
        }

        public void DeleteRuleSet(AnalyzeRule ruleset)
        {
            throw new NotImplementedException();
        }
    }



}

