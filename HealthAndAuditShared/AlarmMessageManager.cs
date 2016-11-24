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
    public enum AlarmLevel
    {
        High = 0,
        Medium = 10,
        Low = 20
    }

    [Serializable]
    public class AlarmMessage
    {
        public AlarmMessage() { }
        public AlarmMessage(AlarmLevel level, string origin, string message, string exceptionMessage = null, string strorageID = null)
        {
            Level = level;
            Origin = origin;
            Message = message;
            ExceptionMessage = exceptionMessage;
            StorageID = strorageID;
        }
        public AlarmLevel Level { get; set; }
        public string Origin { get; set; }
        public string Message { get; set; }
        public string ExceptionMessage { get; set; }
        public string StorageID { get; set; }

    }

    public class AlarmMessageManager
    {
        private IMessageQueue<AlarmMessage> Queue { get; }

        public AlarmMessageManager(IMessageQueue<AlarmMessage> queue)
        {
            Queue = queue;
        }
        public void RaiseAlarm(AlarmMessage message)
        {
            Queue.SendMessage(message);
        }

        public async Task RaiseAlarmAsync(AlarmMessage message)
        {
            await Queue.SendMessageAsync(message);
        }
        public async Task<AlarmMessage> CheckQueueForAlarmAsync()
        {
            return await Queue.ReceiveMessageAsync();
        }
    }

    public interface IMessageQueue<T>
    {
        void SendMessage(T message);
        Task SendMessageAsync(T message);
        T ReceiveMessage();
        Task<T> ReceiveMessageAsync();
    }


    public class ServiceBusConnection<T> : IMessageQueue<T>
    {
        private MessageSender MessageSender { get; }
        private MessageReceiver MessageReceiver { get; }
        public ServiceBusConnection(string connectionString, string queueName)
        {
            var factory = MessagingFactory.CreateFromConnectionString(connectionString);
            MessageSender = factory.CreateMessageSender(queueName);
            MessageReceiver = factory.CreateMessageReceiver(queueName);
        }
        //todo varna om meddelande för stort
        public void SendMessage(T message)
        {
            MessageSender.Send(new BrokeredMessage(message));
        }

        public async Task SendMessageAsync(T message)
        {
            await MessageSender.SendAsync(new BrokeredMessage(message));
        }

        public T ReceiveMessage()
        {
            var bmsg = MessageReceiver.Receive();
            return bmsg.GetBody<T>();
        }

        public async Task<T> ReceiveMessageAsync()
        {
            return await Task.Run(() => MessageReceiver.ReceiveAsync().Result.GetBody<T>());

        }
    }
}
