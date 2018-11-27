using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

using Newtonsoft.Json;
using SystemHealthExternalInterface;

namespace HealthAndAuditShared
{
    public class EventProcessor : IEventProcessor
    {
        public delegate void NewInfo(ConcurrentDictionary<string, string> info);
        public static event NewInfo OnUpdatedInfo;
        private static ConcurrentDictionary<string, string> EventProcInfo { get; } = new ConcurrentDictionary<string, string>();
        private static FileLogger Logger { get; set; }
        private static FileLogger ErrorLogger { get; set; }
        private static AnalyzerEngine Engine { get; set; }
        
        private static IEventStore EventStore { get; set; }
        private static string TimeStampThis(string input)
        {
            return $"\t{DateTime.UtcNow}UTC\t{input}";
        }

        private string _id;

        private static void AddNewInfo(string id, string info)
        {
            EventProcInfo[id] = TimeStampThis(info);
            OnUpdatedInfo?.Invoke(EventProcInfo);
        }

        private void RemoveInfo(string id)
        {
            EventProcInfo.TryRemove(id, out _);
            OnUpdatedInfo?.Invoke(EventProcInfo);
        }

        private static bool IsInitialized { get; set; }

        public static void Init(AnalyzerEngine engine, FileLogger logger, FileLogger errorLogger, IEventStore eventStore)
        {
            Engine = engine;
            Logger = logger;
            ErrorLogger = errorLogger;
            EventStore = eventStore;
            IsInitialized = true;
        }

        public Task OpenAsync(PartitionContext context)
        {
            _id = Guid.NewGuid().ToString();
            AddNewInfo(_id, "Open");
            return Task.FromResult<object>(null);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {

            if (!IsInitialized)
            {
                Logger.AddRow("Processor not initialized. Throwing exception.");
                throw new Exception("Processor not initialized. Run Init() before starting");
            }

            AddNewInfo(_id, "Processing");
            var parsedData = messages.Select(eventData => Encoding.UTF8.GetString(eventData.GetBytes())).Select(JsonConvert.DeserializeObject<SystemEvent>).ToList();
            if (Engine.EngineIsRunning)
            {
                await Engine.AddToMainQueue(parsedData);
                AddNewInfo(_id, parsedData.Count + " events added to engine");
            }
            else
            {
                AddNewInfo(_id, "Engine is not running. Cannot add events. Aborting.");
                return;
            }
            try
            {
                var workInfo = await EventStore.StoreEventsAsync(parsedData);
                if (string.IsNullOrEmpty(workInfo))
                {
                    RemoveInfo(_id);
                }
                else
                {
                    AddNewInfo(_id, workInfo);
                }
            }
            catch (Exception ex)
            {
                AddNewInfo(_id, "!ERROR! " + ex.Message);
                ErrorLogger.AddRow("!ERROR! In event processor");
                ErrorLogger.AddRow(ex.ToString());
            }
            finally
            {
                AddNewInfo(_id, "Setting Checkpoint");
                try
                {
                    await context.CheckpointAsync();
                    AddNewInfo(_id, "Checkpoint set");
                }
                catch (Exception ex) when (ex.Message.Contains("lease for the blob has expired"))
                {
                    Logger.AddRow("Lease for the blob has expired");
                    RemoveInfo(_id);
                }
                catch (Exception ex) when (ex.Message.Contains("System.TimeoutException"))
                {
                    Logger.AddRow("System.TimeoutException");
                    RemoveInfo(_id);
                }
                catch (LeaseLostException)
                {
                    Logger.AddRow("LeaseLostException");
                    RemoveInfo(_id);
                }
            }
        }

     

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            AddNewInfo(_id, "Closing");
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
                AddNewInfo(_id, "Closing, checkpoint set.");
            }
        }
    }
}
