namespace DIG.Notifications
{
    /// <summary>
    /// EPIC 18.3: Opaque handle returned by NotificationService.Show().
    /// Used to dismiss or update a specific notification.
    /// </summary>
    public readonly struct NotificationHandle
    {
        public readonly int Id;
        public readonly NotificationChannel Channel;

        public bool IsValid => Id > 0;

        public NotificationHandle(int id, NotificationChannel channel)
        {
            Id = id;
            Channel = channel;
        }

        public static readonly NotificationHandle Invalid = default;
    }
}
