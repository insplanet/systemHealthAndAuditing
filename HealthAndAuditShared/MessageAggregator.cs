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
using System.Security.Cryptography;
using System.Text;

namespace HealthAndAuditShared
{
    public class MessageAggregator<T>
    {
        public  Dictionary<string, AggregatedMessage<T>> Collection { get; } = new Dictionary<string, AggregatedMessage<T>>();

        public void AddMessage(TimeStampedMessage<T> message, string messageIdentifier)
        {
            AggregatedMessage<T> value;
            if (Collection.TryGetValue(messageIdentifier, out value))
            {
                value.AmountCounter++;
                if (message.TimeStamp < value.FirstOccurrence)
                {
                    value.FirstOccurrence = message.TimeStamp;
                }
                if (message.TimeStamp > value.LastOccurrence)
                {
                    value.LastOccurrence = message.TimeStamp;
                }
            }
            else
            {
                var newTracker = new AggregatedMessage<T>();
                newTracker.AmountCounter = 1;
                newTracker.Message = message.Message;
                newTracker.FirstOccurrence = newTracker.LastOccurrence = message.TimeStamp;
                Collection.Add(messageIdentifier, newTracker);
            }
        }
    }

    public class AggregatedMessage<T>
    {
        public T Message { get; internal set; }
        public uint AmountCounter { get; internal set; }
        public DateTime FirstOccurrence { get; internal set; }
        public DateTime LastOccurrence { get; internal set; }
    }
    public static class AggregatedMessageExtensions
    {
        public static string GenerateMessageIdentifierFromString(this string message)
        {
            using (var cryptoTransformSHA1 = new SHA1CryptoServiceProvider())
            {
                return BitConverter.ToString(cryptoTransformSHA1.ComputeHash(Encoding.UTF8.GetBytes(message)));
            }
        }
    }
}
