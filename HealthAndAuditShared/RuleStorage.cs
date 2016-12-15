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
using System.Threading.Tasks;

namespace HealthAndAuditShared
{
    public interface IRuleStorage
    {
        List<AnalyseRuleset> GetAllRuleSets();
        List<AnalyseRuleset> GetRuleSetsForApplication(string applicationName);
        Task UpsertRuleSetAsync(AnalyseRuleset ruleset);
        void DeleteRuleSet(AnalyseRuleset ruleset);
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

            var rs = new FailurePercentRule();
            rs.ApplicationName = "Eventpump.vshost.exe";
            rs.RuleName = "TestRule002";
            rs.KeepOperationInPileTime = new TimeSpan(0, 30, 0);
            rs.MaxFailurePercent = 80;
            list.Add(rs);

            return list;
        }

        public Task UpsertRuleSetAsync(AnalyseRuleset ruleset)
        {
            throw new NotImplementedException();
        }

        public void DeleteRuleSet(AnalyseRuleset ruleset)
        {
            throw new NotImplementedException();
        }
    }



}

