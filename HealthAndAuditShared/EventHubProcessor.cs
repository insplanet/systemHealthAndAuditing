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
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace HealthAndAuditShared
{
    public class EventHubProcessor 
    {
        public string Listen_EventHubConnectionstring { get; set; }
        private string EventHubPath { get; }
        public EventHubProcessor( string listenConnectionstring, string eventHubPath)
        {
            Listen_EventHubConnectionstring = listenConnectionstring;
            EventHubPath = eventHubPath;
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
