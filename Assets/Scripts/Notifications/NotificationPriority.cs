namespace DIG.Notifications
{
    /// <summary>
    /// EPIC 18.3: Notification priority for queue ordering.
    /// Higher priority shows first when multiple are queued.
    /// </summary>
    public enum NotificationPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3,
    }
}
