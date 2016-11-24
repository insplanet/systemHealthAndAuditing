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

namespace HealthAndAuditShared
{
    public class TimeStampedMessage<T>
    {
        public TimeStampedMessage(DateTime timestamp, T message)
        {
            TimeStamp = timestamp;
            Message = message;
        }
        public DateTime TimeStamp { get;}
        public T Message { get; set; }
    }
}
