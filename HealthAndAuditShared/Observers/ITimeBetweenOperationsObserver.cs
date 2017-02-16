namespace HealthAndAuditShared.Observers
{
    public interface ITimeBetweenOperationsObserver
    {
        void RuleTriggeredByTimeout(TimeBetweenOperations rule);
    }
}
