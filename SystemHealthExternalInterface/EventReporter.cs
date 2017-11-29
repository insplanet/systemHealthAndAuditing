﻿/****************************************************************************************
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SystemHealthExternalInterface
{
    public sealed class EventReporter
    {
        #region Singleton stuff
        private static readonly Lazy<EventReporter> LAZY = new Lazy<EventReporter>(() => new EventReporter());
       
        public static EventReporter Instance => LAZY.Value;
        private EventReporter()
        {

        }
        #endregion

        public string OverrideApplicationNameWith { get; set; }
        private string SendEventHubConnectionstring { get; set; }
        private string EventHubPath { get; set; }
        private EventHubClient Client { get; set; }

        /// <summary>
        /// Create a new reporter and manually set the connection
        /// </summary>
        /// <param name="sendConnectionstring"></param>
        /// <param name="eventHubPath"></param>
        public void Init(string sendConnectionstring, string eventHubPath)
        {
            SendEventHubConnectionstring = sendConnectionstring;
            EventHubPath = eventHubPath;
            Client = EventHubClient.CreateFromConnectionString(SendEventHubConnectionstring, EventHubPath);
        }

        /// <summary>
        /// Create a new reporter and read connection info from app.config ("Microsoft.ServiceBus.ConnectionString.Send", "EventHubPath")
        /// </summary>
        public void Init()
        {
            SendEventHubConnectionstring = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString.Send"];
            EventHubPath = ConfigurationManager.AppSettings["EventHubPath"];
            Client = EventHubClient.CreateFromConnectionString(SendEventHubConnectionstring, EventHubPath);
        }


        //WARNING!!! Swallows exceptions unless an Action to handle it is supplied
        public void SafeReportEvent(SystemEvent @event, Action<Exception> handleException = null)
        {
            try
            {
                ReportEvent(@event);
            }
            catch (Exception e)
            {
                handleException?.Invoke(e);
            }
        }

        public void ReportEvent(SystemEvent @event)
        {
            Task.Run(async () => { await ReportEventAsync(@event); }).Wait();
        }

        public async Task ReportEventAsync(SystemEvent @event)
        {
            if (!string.IsNullOrWhiteSpace(OverrideApplicationNameWith))
            {
                @event.PartitionKey  = @event.AppInfo.ApplicationName = OverrideApplicationNameWith;
            }
            await Client.SendAsync(new EventData(Encoding.UTF8.GetBytes(Serialise(@event))));
        }

        public async Task ReportEventBatchAsync(List<SystemEvent> events)
        {
            await Client.SendBatchAsync(events.Select(@event => new EventData(Encoding.UTF8.GetBytes(Serialise(@event)))).ToList());
        }

        private static string Serialise(SystemEvent @event)
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new StringEnumConverter { CamelCaseText = true });
            return JsonConvert.SerializeObject(@event, settings);
        }

    }
}
