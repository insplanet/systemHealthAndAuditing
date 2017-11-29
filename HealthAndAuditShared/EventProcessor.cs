﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using SystemHealthExternalInterface;

namespace HealthAndAuditShared
{
    public class EventProcessor : IEventProcessor
    {
        public delegate void NewInfo(ConcurrentDictionary<string, string> info);
        public static event NewInfo OnUpdatedInfo;
        private static ConcurrentDictionary<string, string> EventProcInfo { get;  } = new ConcurrentDictionary<string, string>();
        private static FileLogger Logger { get; set; }
        private static AnalyzerEngine Engine { get; set; }
        private static string AzureStorageConnectionString { get; set; }
        private static string OperationStorageTableName { get; set; } 
        private static string TimeStampThis(string input)
        {
            return $"\t{DateTime.UtcNow}UTC\t{input}";
        }

        private string id;

        private void AddNewInfo(string id, string info)
        {
            EventProcInfo[id] = TimeStampThis(info);
            OnUpdatedInfo?.Invoke(EventProcInfo);
        }

        private static bool IsInitialized { get; set; }

        public static void Init(AnalyzerEngine engine, FileLogger logger, string azureStorageConnectionString, string operationStorageTableName)
        {
            Engine = engine;
            Logger = logger;
            AzureStorageConnectionString = azureStorageConnectionString;
            OperationStorageTableName = operationStorageTableName;
            IsInitialized = true;
        }


        public Task OpenAsync(PartitionContext context)
        {
            id = Guid.NewGuid().ToString();
            AddNewInfo(id, "Open");
            return Task.FromResult<object>(null);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {

            if(!IsInitialized)
            {
                Logger.AddRow("Processor not initialized. Throwing exception.");
                throw new Exception("Processor not initialized. Run Init() before starting");
            }

            AddNewInfo(id, "Processing");
            var parsedData = messages.Select(eventData => Encoding.UTF8.GetString(eventData.GetBytes())).Select(JsonConvert.DeserializeObject<SystemEvent>).ToList();
            if (Engine.EngineIsRunning)
            {
                await Engine.AddToMainQueue(parsedData);
                AddNewInfo(id, parsedData.Count + " events added to engine");
            }
            else
            {
                AddNewInfo(id, "Engine is not running. Cannot add events. Aborting.");
                return;
            }
            try
            {
                var storageMan = new AzureStorageManager(AzureStorageConnectionString);
                CloudTable Table = storageMan.GetTableReference(OperationStorageTableName);

                var batches = new Dictionary<string, TableBatchOperation>();
                var batchNames = new Dictionary<string, string>();
                const int maxOps = Microsoft.WindowsAzure.Storage.Table.Protocol.TableConstants.TableServiceBatchMaximumOperations;
                foreach (var operationResult in parsedData)
                {
                    string batchName;
                    if (!batchNames.TryGetValue(operationResult.PartitionKey, out batchName))
                    {
                        batchName = operationResult.PartitionKey;
                    }
                    TableBatchOperation batchOperation;
                    if (!batches.ContainsKey(batchName))
                    {
                        batchOperation = new TableBatchOperation();
                        batches.Add(batchName, batchOperation);
                    }
                    else
                    {
                        batches.TryGetValue(batchName, out batchOperation);
                    }
                    Debug.Assert(batchOperation != null, "Could not find batchOperation in Dictionary.");

                    if (batchOperation.Count == maxOps)
                    {
                        batchOperation = new TableBatchOperation();
                        batches.Add(GetNewBatchName(operationResult.PartitionKey, batchNames), batchOperation);
                    }
                    batchOperation.Insert(operationResult);
                }
                AddNewInfo(id, "Running batches");
                foreach (var batch in batches)
                {
                    await Table.ExecuteBatchAsync(batch.Value);
                }
            }
            catch (Exception ex) when (ex.Message.Contains("The specified entity already exists"))
            {
                Logger.AddRow("Duplicate entry tried to be added to table storage.");
            }
            catch (Exception ex)
            {
                AddNewInfo(id, "!ERROR! " + ex.Message);
                Logger.AddRow("!ERROR! In event processor");
                Logger.AddRow(ex.ToString());
            }
            finally
            {
                AddNewInfo(id, "Setting Checkpoint");
                await context.CheckpointAsync();
                AddNewInfo(id, "Checkpoint set");
            }
        }

        public string GetNewBatchName(string orginalName, Dictionary<string, string> names)
        {
            const string addon = "X";
            string currentAlias;
            if (names.TryGetValue(orginalName, out currentAlias))
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
            AddNewInfo(id, "Closing");
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
                AddNewInfo(id, "Closing, checkpoint set.");
            }
        }
    }
}
