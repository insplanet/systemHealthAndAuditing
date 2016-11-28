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
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SystemHealthExternalInterface;
using HealthAndAuditShared;
using Microsoft.Azure;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using static System.Console;

namespace QuickAnalyzer
{
    internal class Program
    {
        public static AnalyserEngine Engine { get; set; }

        private static void Main(string[] args)
        {
#if DEBUG
            SetWindowSize(200, 40);
#endif

            var storageConnection = ConfigurationManager.AppSettings["AzureStorageConnectionString"];
            var eventhubConnS = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Listen"];
            var eventhubpath = ConfigurationManager.AppSettings["EventHubPath"];
            var alarmQueueConnS = ConfigurationManager.AppSettings["ServiceBus.Queue.Connectionstring"];
            var alarmQueueName = ConfigurationManager.AppSettings["ServiceBus.Queue.Name"];

            WriteLine("Starting analyzer for hub: " + eventhubpath);

            var alarmQueue = new ServiceBusConnection<AlarmMessage>(alarmQueueConnS, alarmQueueName);
            var alarmManger = new AlarmMessageManager(alarmQueue);
            Engine = new AnalyserEngine();
            ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(eventhubConnS);
            builder.TransportType = TransportType.Amqp;
            var connection = new EventHubProcessor(builder.ToString(), eventhubpath);
            WriteLine("Starting event receiver.");
            var recTask = connection.StartReceiver<EventProc>(storageConnection);
            recTask.Wait();
            WriteLine("Receiver waiting.");
            var engineStartCounter = 0;
            var maxEngineRestarts = 10;
            try
            {
                while(true)
                {
                    if(!Engine.EngineIsRunning)
                    {
                        Engine.StartEngine(new TestRuleStorage(), alarmManger);
                        if(maxEngineRestarts <= engineStartCounter++)
                        {
                            var alarm = new AlarmMessage(AlarmLevel.High, AppDomain.CurrentDomain.FriendlyName, $"AnalyserEngine main task has been restared {engineStartCounter -1} times. Engine is down and can not recover! Resetting start counter.");
                            alarmManger.RaiseAlarm(alarm);
                            engineStartCounter = 0;
                        }
                    }
                    Thread.Sleep(1000);
                    var messageAggregator = new MessageAggregator<string>();
                    for(var i = 0; i < 500; ++i)
                    {
                        TimeStampedMessage<string> msg;
                        if(Engine.EngineMessages.TryDequeue(out msg))
                        {
                            messageAggregator.AddMessage(msg, msg.Message.GenerateMessageIdentifierFromString());
                        }
                    }
                    foreach(var messageTracker in messageAggregator.Collection)
                    {
                        WriteLine($"{messageTracker.Value.Message} | {messageTracker.Value.AmountCounter} times from {messageTracker.Value.FirstOccurrence} to {messageTracker.Value.LastOccurrence}");
                    }
                }
            }
            catch(Exception ex)
            {
                var alarm = new AlarmMessage(AlarmLevel.High, AppDomain.CurrentDomain.FriendlyName, $"Exception in main loop.",ex.Message);
                alarmManger.RaiseAlarm(alarm);
            }

            //todo read msg queue, act on
        }

        public class EventProc : IEventProcessor
        {
            private CloudTable Table { get; set; }

            public Task OpenAsync(PartitionContext context)
            {
                var storageMan = new AzureStorageManager(CloudConfigurationManager.GetSetting("AzureStorageConnectionString"));
                Table = storageMan.GetTableReference(ConfigurationManager.AppSettings["OperationStorageTable"]);
                return Task.FromResult<object>(null);
            }

            public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
            {
                var parsedData = messages.Select(eventData => Encoding.UTF8.GetString(eventData.GetBytes())).Select(JsonConvert.DeserializeObject<SystemEvent>).ToList();
                await Engine.AddToMainQueue(parsedData);
                var batches = new Dictionary<string, TableBatchOperation>();
                var batchNames = new Dictionary<string, string>();
                const int maxOps = Microsoft.WindowsAzure.Storage.Table.Protocol.TableConstants.TableServiceBatchMaximumOperations;

                foreach(var operationResult in parsedData)
                {
                    string batchName;
                    if(!batchNames.TryGetValue(operationResult.PartitionKey, out batchName))
                    {
                        batchName = operationResult.PartitionKey;
                    }
                    TableBatchOperation batchOperation;
                    if(!batches.ContainsKey(batchName))
                    {
                        batchOperation = new TableBatchOperation();
                        batches.Add(batchName, batchOperation);
                    }
                    else
                    {
                        batches.TryGetValue(batchName, out batchOperation);
                    }
                    Debug.Assert(batchOperation != null, "Could not find batchOperation in Dictionary.");

                    if(batchOperation.Count == maxOps)
                    {
                        batchOperation = new TableBatchOperation();
                        batches.Add(GetNewBatchName(operationResult.PartitionKey, batchNames), batchOperation);
                    }
                    batchOperation.Insert(operationResult);
                }
                foreach(var batch in batches)
                {
                    await Table.ExecuteBatchAsync(batch.Value);
                }
                await context.CheckpointAsync();
            }

            public string GetNewBatchName(string orginalName, Dictionary<string, string> names)
            {
                const string addon = "X";
                string currentAlias;
                if(names.TryGetValue(orginalName, out currentAlias))
                {
                    names[orginalName] = currentAlias + addon;
                }
                else
                {
                    currentAlias = orginalName + addon;
                    names.Add(orginalName, currentAlias);
                }
                return currentAlias;
            }

            public async Task CloseAsync(PartitionContext context, CloseReason reason)
            {
                if(reason == CloseReason.Shutdown)
                {
                    await context.CheckpointAsync();
                }
            }
        }
    }
}