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
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HealthAndAuditShared
{
    public class EventHubSenderAndProcessor : IOperationResultChannel
    {
        public string Send_EventHubConnectionstring { get; set; }
        public string Listen_EventHubConnectionstring { get; set; }
        private string EventHubPath { get; }
        public EventHubSenderAndProcessor(string sendConnectionstring, string listenConnectionstring, string eventHubPath)
        {
            Send_EventHubConnectionstring = sendConnectionstring;
            Listen_EventHubConnectionstring = listenConnectionstring;
            EventHubPath = eventHubPath;
        }
        public void ReportOperationResult(SystemEvent opResult)
        {
            if(string.IsNullOrWhiteSpace(Send_EventHubConnectionstring))
            {
                throw new ArgumentNullException(nameof(Send_EventHubConnectionstring));
            }
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new StringEnumConverter { CamelCaseText = true });
            var eventhubclient = EventHubClient.CreateFromConnectionString(Send_EventHubConnectionstring, EventHubPath);
            var data = JsonConvert.SerializeObject(opResult, settings);
            eventhubclient.Send(new EventData(Encoding.UTF8.GetBytes(data)));
        }
        public Task StartReceiver<T>(string storageConnection) where T: IEventProcessor
        {
            if (string.IsNullOrWhiteSpace(Listen_EventHubConnectionstring))
            {
                throw new ArgumentNullException(nameof(Listen_EventHubConnectionstring));
            }
            var eventhubclient = EventHubClient.CreateFromConnectionString(Listen_EventHubConnectionstring, EventHubPath);
            var defaultGroup = eventhubclient.GetDefaultConsumerGroup();
            var eventproc = new EventProcessorHost(AppDomain.CurrentDomain.FriendlyName, eventhubclient.Path, defaultGroup.GroupName, Listen_EventHubConnectionstring, storageConnection);

            var opt = new EventProcessorOptions
                      {
                          InitialOffsetProvider = partitionId => DateTime.UtcNow.AddHours(-1),
                          PrefetchCount = 300,
                          MaxBatchSize = 300
                      };
            opt.ExceptionReceived += (sender, e) => { Console.WriteLine(e.Exception); };
            return eventproc.RegisterEventProcessorAsync<T>(opt);
        }
    }

}
