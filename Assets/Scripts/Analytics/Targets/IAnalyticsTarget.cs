namespace DIG.Analytics
{
    public interface IAnalyticsTarget
    {
        string TargetName { get; }
        void SendBatch(AnalyticsEvent[] events);
        void Initialize(DispatchTargetConfig config);
        void Shutdown();
    }
}
