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
            PartitionKey = GeneratePartitionKey();
        }
        public SystemEvent(OperationResult result)
        {
            Result = result;
            AppInfo = new ApplicationInfo();
            RowKey = Guid.NewGuid().ToString();
            PartitionKey = GeneratePartitionKey();
        }

        public enum OperationResult
        {
            Neutral,
            Failure,
            Success            
        }

        [JsonProperty(PropertyName = "id")]
        public string ID => Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(PartitionKey + ";" + RowKey);

        public static Tuple<string, string> DecodeIDToPartitionAndRowKey(string id)
        {
            id = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(id);
            var splitID = id.Split(';');
            return new Tuple<string, string>(splitID[0], splitID[1]);
        }

        public DateTime TimeStampUtc { get;} = DateTime.UtcNow;
        public OperationResult Result { get; set; }
        public string OperationName { get; set; }
        public string UniqueOperationID { get; set; }
        public Exception CaughtException { get; set; }
        public Dictionary<string, object> OperationParameters { get; private set; } = new Dictionary<string, object>();
        public ApplicationInfo AppInfo { get; private set; }
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


        public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            base.ReadEntity(properties, operationContext);
            CaughtException = JsonConvert.DeserializeObject<Exception>(properties[nameof(CaughtException)].StringValue);
            Result = (OperationResult)Enum.Parse(typeof(OperationResult), properties[nameof(Result)].StringValue);
            OperationParameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(properties[nameof(OperationParameters)].StringValue);
            AppInfo = JsonConvert.DeserializeObject<ApplicationInfo>(properties[nameof(AppInfo)].StringValue);
        }

        private string GeneratePartitionKey()
        {
            return string.IsNullOrWhiteSpace(AppInfo?.ApplicationName) ? "Unkown application" : AppInfo.ApplicationName.Replace("\\","_").Replace("/","_").Replace("#", "_").Replace("?", "_");
        }

    }
}
