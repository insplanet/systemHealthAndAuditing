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
using System.Collections.Concurrent;
using System.Linq;

namespace HealthAndAuditShared
{
    public class AlarmChannelHolder
    {
        private ConcurrentBag<AlarmLevelChannelConnection>  Collection { get; } = new ConcurrentBag<AlarmLevelChannelConnection>();
        private class AlarmLevelChannelConnection
        {
            public AlarmLevel Level { get; set; }
            public IAlarmChannel Channel { get; set; }
        }
     
        public void AddChannel(AlarmLevel level, IAlarmChannel channel)
        {
            var conn =  new AlarmLevelChannelConnection();
            conn.Level = level;
            conn.Channel = channel;
            Collection.Add(conn);
        }
        public void SendAlarm(AlarmMessage message)
        {
            var con = Collection.Where(c => c.Level == message.Level);
            foreach(var connection in con)
            {
                connection.Channel.SendAlarm(message);
            }
        }

        public void SendAlarm(MessageAggregator<AlarmMessage> messages)
        {
            foreach(var message in messages.Collection)
            {
                var con = Collection.Where(c => c.Level == message.Value.Message.Level);
                foreach (var connection in con)
                {
                    connection.Channel.SendAggregatedAlarm(message.Value);
                }
            }
        }
    }
    public interface IAlarmChannel
    {
        void SendAlarm(AlarmMessage message);
        void SendAggregatedAlarm(AggregatedMessage<AlarmMessage> aggregated);   
    }
}
