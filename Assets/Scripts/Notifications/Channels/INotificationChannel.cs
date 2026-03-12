using System;
using DIG.Notifications.Config;
using UnityEngine.UIElements;

namespace DIG.Notifications.Channels
{
    /// <summary>
    /// EPIC 18.3: Interface for a notification display channel.
    /// Each channel owns a VisualElement container and manages its own element pool.
    /// </summary>
    public interface INotificationChannel
    {
        /// <summary>Show a notification. Returns the assigned handle ID.</summary>
        int Show(int handleId, NotificationData data, NotificationStyleSO style);

        /// <summary>Dismiss a specific notification by handle ID.</summary>
        void Dismiss(int handleId);

        /// <summary>Update an existing notification's data in-place.</summary>
        void Update(int handleId, NotificationData data);

        /// <summary>Dismiss all active notifications in this channel.</summary>
        void Clear();

        /// <summary>Number of currently visible notifications.</summary>
        int ActiveCount { get; }

        /// <summary>Max visible capacity from config.</summary>
        int MaxVisible { get; }

        /// <summary>True if this channel can accept another notification right now.</summary>
        bool HasCapacity { get; }

        /// <summary>Called each frame to update timers and progress bars.</summary>
        void Tick(float deltaTime);

        /// <summary>Fired when a notification finishes its exit animation and is fully removed.</summary>
        event Action<int> OnDismissComplete;

        /// <summary>Inject the container into a parent layer element.</summary>
        void AttachTo(VisualElement parentLayer);
    }
}
