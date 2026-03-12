namespace DIG.Notifications
{
    /// <summary>
    /// EPIC 18.3: History entry wrapping a shown notification.
    /// Stored in the ring buffer for history queries.
    /// </summary>
    public class NotificationRecord
    {
        public NotificationData Data;
        public float Timestamp;
        public bool WasSeen;
        public bool WasActioned;
        public NotificationHandle Handle;
    }
}
