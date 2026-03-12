using System;

namespace DIG.Notifications.Config
{
    /// <summary>
    /// EPIC 18.3: Per-channel configuration for notification display behavior.
    /// </summary>
    [Serializable]
    public class NotificationChannelConfig
    {
        /// <summary>Max notifications visible simultaneously in this channel.</summary>
        public int MaxVisible = 3;

        /// <summary>Max queued notifications waiting to display. Oldest dropped when exceeded.</summary>
        public int MaxQueueSize = 10;

        /// <summary>Screen position for this channel's container.</summary>
        public NotificationPosition Position = NotificationPosition.TopRight;

        /// <summary>Direction new notifications stack.</summary>
        public StackDirection StackDirection = StackDirection.Down;

        /// <summary>Default display duration in seconds (used if style and data don't specify).</summary>
        public float DefaultDuration = 4f;
    }

    public enum NotificationPosition : byte
    {
        TopLeft = 0,
        TopCenter = 1,
        TopRight = 2,
        BottomLeft = 3,
        BottomCenter = 4,
        BottomRight = 5,
        Center = 6,
    }

    public enum StackDirection : byte
    {
        Down = 0,
        Up = 1,
    }
}
