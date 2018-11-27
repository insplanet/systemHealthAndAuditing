using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemHealthExternalInterface;

namespace HealthAndAuditShared
{
    public interface IEventStore
    {
        FileLogger Logger { get; set; }
        Task<string>  StoreEventsAsync(List<SystemEvent> events);
        void StoreEvent(SystemEvent @event);

    }
}
