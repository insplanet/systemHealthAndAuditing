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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace HealthAndAuditShared
{
    /// <summary>
    /// The main engine. Holds a collection of <see cref="ProgramAnalyser"/>s that runs the actual analyses.
    /// </summary>
    public sealed class AnalyserEngine
    {
        /// <summary>
        /// Starts the engine. Reads <see cref="AnalyseRuleset"/>s from storage and builds a collection of <see cref="ProgramAnalyser"/>s to hold them.
        /// </summary>
        /// <param name="ruleStorage">The <see cref="AnalyseRuleset"/> storage.</param>
        /// <param name="alarmMessageManager">The alarm manager.</param>
        public void StartEngine(IRuleStorage ruleStorage, AlarmMessageManager alarmMessageManager)
        {
            AddMessage($"Starting {nameof(AnalyserEngine)}");
            AlarmMessageManager = alarmMessageManager;
            Analysers.Clear();
            var allRules = ruleStorage.GetAllRuleSets();
            if (allRules.Count == 0)
            {
                AddMessage("Starting with no rules.");
            }
            foreach (var ruleset in allRules)
            {
                var analyser = Analysers.GetOrAdd(ruleset.ApplicationName, new ProgramAnalyser(AlarmMessageManager));
                analyser.AddOrReplaceRuleSet(ruleset);
                analyser.StartAnalyserTask();
                AddMessage($"Ruleset {ruleset.RuleName} added to {nameof(ProgramAnalyser)} for {ruleset.ApplicationName}. Ruleset applies to operation: {(string.IsNullOrEmpty(ruleset.OperationName) ? "All operations" : ruleset.OperationName)}.");
            }

            StartEngineTask();
        }
        /// <summary>
        /// Messages from the engine. eg: if it has started, events recivied.
        /// </summary>
        /// <value>
        /// The engine messages.
        /// </value>
        public ConcurrentQueue<TimeStampedMessage<string>> EngineMessages { get; } = new ConcurrentQueue<TimeStampedMessage<string>>();
        private AlarmMessageManager AlarmMessageManager { get; set; }
        /// <summary>
        /// Gets a value indicating whether the engines main task is running.
        /// </summary>
        /// <value>
        ///   <c>true</c> if engine is running; otherwise, <c>false</c>.
        /// </value>
        public bool EngineIsRunning { get; private set; }
        /// <summary>
        /// Adds a message to EngineMessageCollection.
        /// </summary>
        /// <param name="message">The message.</param>
        private void AddMessage(string message)
        {
            EngineMessages.Enqueue(new TimeStampedMessage<string>(DateTime.UtcNow, message));
        }
        private ConcurrentQueue<SystemEvent> MainEventQueue { get; } = new ConcurrentQueue<SystemEvent>();
        private ConcurrentDictionary<string, ProgramAnalyser> Analysers { get; } = new ConcurrentDictionary<string, ProgramAnalyser>();

        /// <summary>
        /// Adds a list of <see cref="SystemEvent"/>s to main queue of the engine.
        /// </summary>
        /// <param name="results">The results.</param>
        public async Task AddToMainQueue(List<SystemEvent> results)
        {
            await Task.Run(() =>
                           {
                               foreach (var operationResult in results)
                               {
                                   MainEventQueue.Enqueue(operationResult);
                               }
                           });

        }

        /// <summary>
        /// Starts the engine task.
        /// </summary>
        /// <returns></returns>
        private void StartEngineTask()
        {
            Task.Run(() =>
                          {
                              try
                              {
                                  EngineIsRunning = true;
                                  AddMessage("Main engine Task started.");
                                  while (true)
                                  {
                                      SystemEvent fromQ;
                                      if (MainEventQueue.TryDequeue(out fromQ))
                                      {
                                          if (Analysers.ContainsKey(fromQ.AppInfo.ApplicationName))
                                          {
                                              ProgramAnalyser analyser;
                                              var tryAmount = 0;
                                              const int tryUntil = 1000000;
                                              while (!Analysers.TryGetValue(fromQ.AppInfo.ApplicationName, out analyser))
                                              {
                                                   //We don't want to get stuck here, so only try a limited large amount of times.
                                                  if (tryAmount++ > tryUntil)
                                                  {
                                                      break;
                                                  }
                                              }

                                              if (analyser == null)
                                              {
                                                  AddMessage($"{nameof(analyser)} is null. Tried to get from {nameof(Analysers)} {tryUntil} times.");
                                              }
                                              else
                                              {
                                                  if (!analyser.AnalyserIsRunning)
                                                  {
                                                      AddMessage($"{analyser.ProgramName} analyser not running. Starting.");
                                                      analyser.StartAnalyserTask();
                                                  }
                                                  analyser.AddEvent(fromQ);
                                                  AddMessage($"Event added from {fromQ.AppInfo.ApplicationName} to {nameof(Analysers)}.");
                                              }
                                          }
                                          else
                                          {
                                              AddMessage($"No analyser for {fromQ.AppInfo.ApplicationName} in {nameof(Analysers)}. Trying to add a blank one with no rulesets.");
                                              var analyser = new ProgramAnalyser(AlarmMessageManager) { ProgramName = fromQ.AppInfo.ApplicationName };
                                              if (Analysers.TryAdd(analyser.ProgramName, analyser))
                                              {
                                                  AddMessage($"Added blank analyser for {fromQ.AppInfo.ApplicationName} in {nameof(Analysers)}.");
                                                  analyser.StartAnalyserTask();
                                                  analyser.AddEvent(fromQ);
                                              }
                                              else
                                              {
                                                  AddMessage($"Failed to add blank analyser for {fromQ.AppInfo.ApplicationName} in {nameof(Analysers)}.");
                                              }
                                          }
                                      }

                                  }
                              }
                              catch (Exception ex)
                              {
                                  EngineIsRunning = false;
                                  var msg = new AlarmMessage(AlarmLevel.Medium, AppDomain.CurrentDomain.FriendlyName, $"Exception in {nameof(AnalyserEngine)}.{nameof(StartEngineTask)}. Engine is down. Engine will try to restart.", ex.Message);
                                  AlarmMessageManager.RaiseAlarmAsync(msg).Wait();
                              }
                          });


        }

        /// <summary>
        /// Holds the <see cref="AnalyseRuleset"/>s to analyse one program. Starts its own task to run analyses.
        /// </summary>
        private class ProgramAnalyser
        {
            public ProgramAnalyser(AlarmMessageManager alarmMessageManager)
            {
                AlarmMessageManager = alarmMessageManager;
            }
            public string ProgramName { get; set; }
            private AlarmMessageManager AlarmMessageManager { get; }
            public bool AnalyserIsRunning { get; private set; }
            private ConcurrentDictionary<string, AnalyseRuleset> RuleSets { get; } = new ConcurrentDictionary<string, AnalyseRuleset>();
            private ConcurrentQueue<SystemEvent> EventQueue { get; } = new ConcurrentQueue<SystemEvent>();
            public void AddEvent(SystemEvent result)
            {
                EventQueue.Enqueue(result);
            }

            public void StartAnalyserTask()
            {
                Task.Run(() =>
                               {
                                   try
                                   {
                                       AnalyserIsRunning = true;
                                       while (true)
                                       {
                                           SystemEvent fromQ;
                                           if (EventQueue.TryDequeue(out fromQ))
                                           {
                                               Parallel.ForEach(RuleSets.Where(r => string.IsNullOrEmpty(r.Value.OperationName) || r.Value.OperationName.Equals(fromQ.OperationName)), ruleSet =>
                                               {
                                                   if (ruleSet.Value.AddAndCheckIfTriggered(fromQ))
                                                   {
                                                       var msg = new AlarmMessage(ruleSet.Value.AlarmLevel, fromQ.AppInfo.ApplicationName, $"Rule {ruleSet.Value.RuleName} triggered. Message: {ruleSet.Value.AlarmMessage}", fromQ.CaughtException.Message, fromQ.ID);
                                                       AlarmMessageManager.RaiseAlarm(msg);
#if DEBUG
                                                       Debug.WriteLine($"ALARM! {ruleSet.Value.AlarmLevel} level. From {fromQ.AppInfo.ApplicationName}. Message: {ruleSet.Value.AlarmMessage}");
#endif
                                                   }
                                               });
                                           }
                                       }
                                   }
                                   catch (Exception ex)
                                   {
                                       AnalyserIsRunning = false;
                                       var msg = new AlarmMessage(AlarmLevel.Medium, AppDomain.CurrentDomain.FriendlyName, $"Exception in {nameof(ProgramAnalyser)}.{nameof(StartAnalyserTask)} for {ProgramName}.",ex.InnerException?.Message ?? ex.Message);
                                       AlarmMessageManager.RaiseAlarm(msg);
                                   }
                               }
                    );
            }

            public void AddOrReplaceRuleSet(AnalyseRuleset ruleset)
            {
                if (string.IsNullOrWhiteSpace(ProgramName))
                {
                    ProgramName = ruleset.ApplicationName;
                }
                if (ProgramName != ruleset.ApplicationName)
                {
                    throw new ArgumentException($"This instance of {nameof(ProgramAnalyser)} is analysing {ProgramName}. Can not add ruleset for {ruleset.ApplicationName}.");
                }
                RuleSets.AddOrUpdate(ruleset.RuleName, ruleset, (key, oldValue) => ruleset);
            }
        }
    }
}
