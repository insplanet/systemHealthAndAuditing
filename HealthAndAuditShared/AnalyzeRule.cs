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
using System.Timers;
using SystemHealthExternalInterface;
using HealthAndAuditShared.Observers;
using Newtonsoft.Json;

namespace HealthAndAuditShared
{
    /// <summary>
    /// Abstract base class for RuleSets.
    /// </summary>
    public abstract class AnalyzeRule
    {
        protected AnalyzeRule()
        {
            RealType = GetType();
        }

        public Type RealType { get; set; }
        [JsonProperty(PropertyName = "id")]
        public string RuleID { get; internal set; }
        public string ProgramName { get; set; }
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
    /// <seealso cref="HealthAndAuditShared.AnalyzeRule" />
    public class MaxAmountOfFailuresRule : AnalyzeRule
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
    /// <seealso cref="HealthAndAuditShared.AnalyzeRule" />
    public class FailurePercentRule : AnalyzeRule
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
    /// Will check if time between operations is taking too long. To use with start and end operation variables, leave the <seealso cref="AnalyzeRule.OperationName"/> null.
    /// With start and end in use the rule will trigger if it takes too long between start and end OR if start is followed by another start OR if end is coming without a start first.
    /// NOTE: an observer must be attached to this rule <see cref="ITimeBetweenOperationsObserver"/>
    /// </summary>
    /// <seealso cref="HealthAndAuditShared.AnalyzeRule" />
    public class TimeBetweenOperations : AnalyzeRule
    {
        private Timer _timer;
        private List<ITimeBetweenOperationsObserver> _observers;

        public string StartOperationName { get; set; }
        public string EndOperationName { get; set; }
        private bool StartOperationReceived { get; set; }

        private List<ITimeBetweenOperationsObserver> Observers
        {
            get { return _observers = _observers ?? new List<ITimeBetweenOperationsObserver>(); }
        }
        public void AttachObserver(ITimeBetweenOperationsObserver observer) => Observers.Add(observer);

        public void NotifyObservers()
        {
            foreach(var observer in Observers)
            {
                observer.RuleTriggeredByTimeout(this);
            }
        }

        public override bool AddAndCheckIfTriggered(SystemEvent opResult)
        {
            if(Observers.Count == 0)
            {
                AlarmMessage = "No observers attached to rule. Rule can not let know when triggered through timeout.";
                return true;
            }
            if(OperationName != null && opResult.OperationName == OperationName)
            {
                AddSingleOperation();
                return false;
            }
            if(opResult.OperationName == StartOperationName)
            {
                return AddAndCheckIfTriggeredForStartOperation();
            }
            if(opResult.OperationName == EndOperationName)
            {
                return  AddAndCheckIfTriggeredForEndOperation();
            }
            return false;
        }

        private void AddSingleOperation()
        {
            StopTimer();
            StartTimer();
        }

        private bool AddAndCheckIfTriggeredForStartOperation()
        {
            if (StartOperationReceived)
            {
                AlarmMessage = $"Start operation was succeeded by another start operation. OperationName: {StartOperationName}";
                StopTimer();
                return true;
            }
            StartOperationReceived = true;
            StartTimer();
            return false;
        }

        private bool AddAndCheckIfTriggeredForEndOperation()
        {
            var retVal = false;
            if (!StartOperationReceived)
            {
                AlarmMessage = $"End operation recieved before start operation. OperationName: {EndOperationName}";
                retVal = true;
            }
            StartOperationReceived = false;
            StopTimer();
            return retVal;
        }

        private void StartTimer()
        {
            _timer = new Timer(KeepOperationInPileTime.TotalMilliseconds);
            _timer.Enabled = true;
            _timer.Elapsed += OnTimeout;
        }

        private void StopTimer()
        {
            _timer?.Stop();
        }

        private void OnTimeout(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            AlarmMessage = $"Time between operations greater than or equal to {KeepOperationInPileTime}. {nameof(OperationName)}:  {OperationName ?? string.Empty}. {nameof(StartOperationName)}: {StartOperationName ?? string.Empty}. {nameof(EndOperationName)}: {EndOperationName ?? string.Empty}";
            StopTimer();
            NotifyObservers();
        }
    }

    public class ScriptRule : AnalyzeRule
    {
        public override bool AddAndCheckIfTriggered(SystemEvent opResult)
        {
            throw new NotImplementedException();
        }
    }
}
