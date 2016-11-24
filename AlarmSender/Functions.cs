/****************************************************************************************
*	This code originates from the software development department at					*
*	swedish insurance and private loan broker Insplanet AB.								*
*	Full license available in license.txt												*
*	This text block may not be removed or altered.                                  	*
*	The list of contributors may be extended.                                           *
*																						*
*							Mikael Axblom, head of software development, Insplanet AB	*
*																						*
*	Contributors: Mikael Axblom, Fredrik Lindgren										*
*****************************************************************************************/
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthAndAuditShared;
using Microsoft.Azure.WebJobs;

namespace AlarmSender
{
    public class Functions
    {
        public static FloodControl FloodControl { get; set; }
        public static AlarmChannelHolder ChannelHolder { get; set; }
        // This function will get triggered/executed when a new message is written 
        // on an Azure Queue called queue.
        public static void ProcessQueueMessage([ServiceBusTrigger("alarms")] AlarmMessage message, TextWriter log)
        {
            if (FloodControl == null)
            {
                throw new NullReferenceException("FloodControl not initialised.");
            }
            if (ChannelHolder == null)
            {
                throw new NullReferenceException("ChannelHolder not initialised.");
            }

            if (!FloodControl.IsRunning)
            {
                FloodControl.StartFloodControl();
            }

            log.WriteLine(message.Level + message.Message);
            if (FloodControl.IsOriginFlooding(message))
            {
                FloodControl.AddMessage(message);
            }
            else
            {
                ChannelHolder.SendAlarm(message);
                FloodControl.SetLastMessageFromOrigin(message);
            }
        }
    }
    public class FloodControl
    {
        public bool IsRunning { get; private set; }
        private static readonly TimeSpan MAX_FREQUENCY = new TimeSpan(0, 1, 0);
        private AlarmChannelHolder Channels { get; }
        public FloodControl(AlarmChannelHolder channels, bool startOnConstruct = false)
        {
            Channels = channels;
            if (startOnConstruct)
            {
                StartFloodControl();
            }
        }
        public class OriginTimeControl
        {
            public DateTime LastMessageSent { get; set; } = DateTime.UtcNow;
            public bool OkayToSendFrom => LastMessageSent + MAX_FREQUENCY < DateTime.UtcNow;
            public MessageAggregator<AlarmMessage> Aggregator { get; set; } = new MessageAggregator<AlarmMessage>();
        }
        public ConcurrentDictionary<string, OriginTimeControl> PerOriginAggregator { get; } = new ConcurrentDictionary<string, OriginTimeControl>();
        public void AddMessage(AlarmMessage message)
        {
            var timeControl = PerOriginAggregator.GetOrAdd(message.Origin, new OriginTimeControl());
            var msgID = (message.Level + message.Message).GenerateMessageIdentifierFromString();
            var tmsg = new TimeStampedMessage<AlarmMessage>(DateTime.UtcNow, message);
            timeControl.Aggregator.AddMessage(tmsg, msgID);
        }
        public bool IsOriginFlooding(AlarmMessage message)
        {
            OriginTimeControl timeCtrl;
            if (PerOriginAggregator.TryGetValue(message.Origin, out timeCtrl))
            {
                return !timeCtrl.OkayToSendFrom;
            }
            return false;
        }
        public void SetLastMessageFromOrigin(AlarmMessage message)
        {
            var timeControl = PerOriginAggregator.GetOrAdd(message.Origin, new OriginTimeControl());
            timeControl.LastMessageSent = DateTime.UtcNow;
        }
        public void StartFloodControl()
        {
            Task.Run(() =>
                           {
                               try
                               {
                                   IsRunning = true;
                                   while (true)
                                   {
                                       foreach (var control in PerOriginAggregator.Where(control => control.Value.OkayToSendFrom))
                                       {
                                           Channels.SendAlarm(control.Value.Aggregator);
                                           control.Value.LastMessageSent = DateTime.UtcNow;
                                           control.Value.Aggregator.Collection.Clear();
                                       }
                                   }
                               }
                               catch (Exception ex)
                               {
                                   IsRunning = false;
                                   Channels.SendAlarm(new AlarmMessage(AlarmLevel.High, "Alarm " + nameof(FloodControl), "Alarm message flood control went down. Restart will be tried on next alarm.", ex.Message));
                               }
                           });

        }
    }
}