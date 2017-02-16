using HealthAndAuditShared.Observers;

namespace HealthAndAuditShared.Fakes
{
    public class FakeTimeBetweenOperationsObserver : ITimeBetweenOperationsObserver
    {
        public bool IsInvoked { get; set; }
        public void RuleTriggeredByTimeout(TimeBetweenOperations rule)
        {
            IsInvoked = true;
        }
    }
}
