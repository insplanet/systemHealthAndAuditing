using HealthAndAuditShared.Observers;

namespace HealthAndAuditShared.Fakes
{
    public class FakeTimeBetweenOperationsObserver : ITimeBetweenOperationsObserver
    {
        public bool IsInvoked { get; set; }
        public void Update(TimeBetweenOperations rule)
        {
            IsInvoked = true;
        }
    }
}
