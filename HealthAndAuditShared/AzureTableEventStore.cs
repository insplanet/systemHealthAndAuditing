using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage.Table;
using SystemHealthExternalInterface;

namespace HealthAndAuditShared
{
    public class AzureTableEventStore : IEventStore
    {
        private static string AzureStorageConnectionString { get; set; }
        private static string OperationStorageTableName { get; set; }

        public AzureTableEventStore(FileLogger logger, string azureStorageConnectionString, string operationStorageTableName)
        {
            Logger = logger;
            AzureStorageConnectionString = azureStorageConnectionString;
            OperationStorageTableName = operationStorageTableName;
        }
        public FileLogger Logger { get; set; }

        public void StoreEvent(SystemEvent @event)
        {
            throw new NotImplementedException();
        }

        public async Task<string> StoreEventsAsync(List<SystemEvent> events)
        {
            var retVal = "";
            try
            {
                var storageMan = new AzureStorageManager(AzureStorageConnectionString);
                CloudTable table = storageMan.GetTableReference(OperationStorageTableName);

                var batches = new Dictionary<string, TableBatchOperation>();
                var batchNames = new Dictionary<string, string>();
                const int maxOps = Microsoft.WindowsAzure.Storage.Table.Protocol.TableConstants.TableServiceBatchMaximumOperations;
                foreach (var operationResult in events)
                {
                    if (!batchNames.TryGetValue(operationResult.PartitionKey, out var batchName))
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
                retVal = "Running batches";
                foreach (var batch in batches)
                {
                    await table.ExecuteBatchAsync(batch.Value);
                }
            }
            catch (Exception ex) when (ex.Message.Contains("The specified entity already exists"))
            {
                Logger.AddRow("Duplicate entry tried to be added to table storage.");
            }
            catch (Exception ex) when (ex.Message.Contains("lease for the blob has expired"))
            {
                Logger.AddRow("Lease for the blob has expired");                
            }
            catch (LeaseLostException)
            {
                Logger.AddRow("LeaseLostException");                
            }
            return retVal;
        }

        private string GetNewBatchName(string orginalName, Dictionary<string, string> names)
        {
            const string addon = "X";
            if (names.TryGetValue(orginalName, out var currentAlias))
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
    }
}
