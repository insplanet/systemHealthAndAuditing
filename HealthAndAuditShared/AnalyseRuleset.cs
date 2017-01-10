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
using System.Linq;
using SystemHealthExternalInterface;
using Newtonsoft.Json;

namespace HealthAndAuditShared
{
    /// <summary>
    /// Abstract base class for RuleSets.
    /// </summary>
    public abstract class AnalyseRuleset
    {
        protected AnalyseRuleset()
        {
            RealType = GetType();
        }

        public Type RealType { get; set; }
        [JsonProperty(PropertyName = "id")]
        public string RuleID { get; internal set; }
        public string ApplicationName { get; set; }
        /// <summary>
        /// Gets or sets the name of the operation to be analysed. Leave null or empty to catch all operations in program.
        /// </summary>
        public string OperationName { get; set; }
        public string RuleName { get; set; }
        public AlarmLevel AlarmLevel { get; set; } = AlarmLevel.Low;
        public string AlarmMessage { get; set; }
        /// <summary>
        /// Gets or sets the time the operation will be used in the analysis. After that time it is dequeued and will no longer affect the analysis.
        /// </summary>
        public TimeSpan KeepOperationInPileTime { get; set; } = new TimeSpan(1, 0, 0);

        /// <summary>
        /// Adds an event and analyses current events.
        /// </summary>
        /// <returns>true if rule is triggered</returns>
        public abstract bool AddAndCheckIfTriggered(SystemEvent opResult);
    }

    /// <summary>
    /// A rule that will trigger if the operation pile builds up to a preset limit within the time limit set in KeepOperationInPileTime.
    /// </summary>
    /// <seealso cref="HealthAndAuditShared.AnalyseRuleset" />
    public class MaxAmountOfFailuresRule : AnalyseRuleset
    {
        public uint MaxTimesFailureAllowed { get; set; }
        private Queue<DateTime> Failures { get; } = new Queue<DateTime>();
        public override bool AddAndCheckIfTriggered(SystemEvent opResult)
        {
            if(opResult.Result != SystemEvent.OperationResult.Failure)
            {
                return false;
            }
            while (Failures.Any() && Failures.Peek() <= DateTime.UtcNow)
            {
                Failures.Dequeue();
            }
            Failures.Enqueue(DateTime.UtcNow + KeepOperationInPileTime);
            if (Failures.Count >= MaxTimesFailureAllowed)
            {
                AlarmMessage = $"{MaxTimesFailureAllowed} failures occured within {KeepOperationInPileTime}.";
                Failures.Clear();
                return true;
            }
            return false;
        }
    }
    /// <summary>
    /// A rule that will trigger if the operations fails at MaxFailurePercent or higher within the time limit set in KeepOperationInPileTime.
    /// </summary>
    /// <seealso cref="HealthAndAuditShared.AnalyseRuleset" />
    public class FailurePercentRule : AnalyseRuleset
    {
        public uint MaxFailurePercent { get; set; } = 0;
        public uint MinimumAmountOfOperationsBeforeRuleCanBeTriggered { get; set; } = 10;
        private Queue<DateTime> Successes { get; } = new Queue<DateTime>();
        private Queue<DateTime> Failures { get; } = new Queue<DateTime>();

        public override bool AddAndCheckIfTriggered(SystemEvent opResult)
        {
            while (Successes.Any() && Successes.Peek() <= DateTime.UtcNow)
            {
                Successes.Dequeue();
            }
            while (Failures.Any() && Failures.Peek() <= DateTime.UtcNow)
            {
                Failures.Dequeue();
            }
            switch (opResult.Result)
            {
                case SystemEvent.OperationResult.Success:
                    Successes.Enqueue(DateTime.UtcNow + KeepOperationInPileTime);
                    break;
                case SystemEvent.OperationResult.Failure:
                    Failures.Enqueue(DateTime.UtcNow + KeepOperationInPileTime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            var totalOperations = Successes.Count + Failures.Count;
            var failRatio = (double)Failures.Count / totalOperations;
            if (totalOperations >= MinimumAmountOfOperationsBeforeRuleCanBeTriggered && (failRatio * 100) >= MaxFailurePercent)
            {
                AlarmMessage = $"{MaxFailurePercent}%  failures occured within {KeepOperationInPileTime}.";
                Failures.Clear();
                Successes.Clear();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Will check if time between operations is over allowed amount. To use with start and end operation variables, leave the <seealso cref="AnalyseRuleset.OperationName"/> null.
    /// With start and end in use the rule will trigger if it takes too long between start and end OR if start is followed by another start OR if end is coming without a start first.
    /// </summary>
    /// <seealso cref="HealthAndAuditShared.AnalyseRuleset" />
    public class TimeBetweenOperations : AnalyseRuleset
    {
        public string StartOperationName { get; set; }
        public string EndOperationName { get; set; }
        private DateTime? LastOperationTime { get; set; }
        public override bool AddAndCheckIfTriggered(SystemEvent opResult)
        {
            throw new NotImplementedException();
            //todo
            //start and end to be used
            if (string.IsNullOrWhiteSpace(OperationName))
            {
                //Due to OperationName being null all operations will pass here, those that do not match start or end will be ignored.
                if(opResult.OperationName != StartOperationName && opResult.OperationName != EndOperationName)
                {
                    return false;
                }
                if (LastOperationTime !=null && StartOperationName == opResult.OperationName)
                {
                    AlarmMessage = $"Start operation was succeeded by another start operation. OperationName: {StartOperationName}";
                    LastOperationTime = null;
                    return true;
                }
                if(LastOperationTime == null)
                {
                    if(opResult.OperationName == EndOperationName)
                    {
                        AlarmMessage = $"End operation recieved before start operation.  OperationName: {EndOperationName}";
                        LastOperationTime = null;
                        return true;
                    }
                }
            }
            if (LastOperationTime == null)
            {
                LastOperationTime = DateTime.UtcNow;
                return false;
            }
            if (LastOperationTime + KeepOperationInPileTime <= DateTime.UtcNow)
            {
                LastOperationTime = null;
                AlarmMessage = $"Time between operations greater than or equal to {KeepOperationInPileTime}. {nameof(OperationName)}:  {OperationName?? string.Empty}. {nameof(StartOperationName)}: {StartOperationName ?? string.Empty}. {nameof(EndOperationName)}: {EndOperationName ?? string.Empty}";
                return true;
            }
            LastOperationTime = DateTime.UtcNow;
            return false;
        }
    }

    public class ScriptRule : AnalyseRuleset
    {
        public override bool AddAndCheckIfTriggered(SystemEvent opResult)
        {
            throw new NotImplementedException();
        }
    }
}
