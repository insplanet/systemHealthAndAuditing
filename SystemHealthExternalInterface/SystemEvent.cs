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
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace SystemHealthExternalInterface
{
    
    /// <summary>
    /// Used to report a system event to the health and audit system
    /// </summary>
    [Serializable]
    public class SystemEvent : TableEntity
    {
        public SystemEvent()
        {
            Result = OperationResult.Neutral;
            AppInfo = new ApplicationInfo();
            RowKey = Guid.NewGuid().ToString();
            PartitionKey = string.IsNullOrWhiteSpace(AppInfo?.ApplicationName) ? "Unkown application" : AppInfo.ApplicationName;
        }
        public SystemEvent(OperationResult result)
        {
            Result = result;
            AppInfo = new ApplicationInfo();
            RowKey = Guid.NewGuid().ToString();
            PartitionKey = string.IsNullOrWhiteSpace(AppInfo?.ApplicationName) ? "Unkown application" : AppInfo.ApplicationName;
        }

        public enum OperationResult
        {
            Neutral,
            Failure,
            Success            
        }

        public string ID
        {
            get
            {
                var plainTextBytes = Encoding.UTF8.GetBytes(PartitionKey + ";" + RowKey);
                return Convert.ToBase64String(plainTextBytes);
            }
        }

        public static Tuple<string, string> DecodeIDToPartitionAndRowKey(string id)
        {
            var base64EncodedBytes = Convert.FromBase64String(id);
            id = Encoding.UTF8.GetString(base64EncodedBytes);
            var splitID = id.Split(';');
            return new Tuple<string, string>(splitID[0], splitID[1]);
        }

        public DateTime TimeStampUtc { get;} = DateTime.UtcNow;
        public OperationResult Result { get; set; }
        public string OperationName { get; set; }
        public Exception CaughtException { get; set; }
        public Dictionary<string, object> OperationParameters { get; } = new Dictionary<string, object>();
        public ApplicationInfo AppInfo { get;}
        public string OtherInfo { get; set; }


        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var results = base.WriteEntity(operationContext);
            results.Add(nameof(CaughtException), new EntityProperty(JsonConvert.SerializeObject(CaughtException)));
            results.Add(nameof(Result), new EntityProperty(Result.ToString()));
            results.Add(nameof(OperationParameters), new EntityProperty(JsonConvert.SerializeObject(OperationParameters)));
            results.Add(nameof(AppInfo), new EntityProperty(JsonConvert.SerializeObject(AppInfo)));
            return results;
        }

    }
}
