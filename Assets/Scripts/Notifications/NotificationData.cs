using System;
using UnityEngine;

namespace DIG.Notifications
{
    /// <summary>
    /// EPIC 18.3: Data payload for a notification request.
    /// </summary>
    public struct NotificationData
    {
        /// <summary>Style asset name (loaded from Resources). Null uses channel default.</summary>
        public string StyleId;

        /// <summary>Which channel to display on.</summary>
        public NotificationChannel Channel;

        /// <summary>Title text (bold header line).</summary>
        public string Title;

        /// <summary>Body text (description).</summary>
        public string Body;

        /// <summary>Optional icon texture.</summary>
        public Texture2D Icon;

        /// <summary>Queue priority. Higher shows first.</summary>
        public NotificationPriority Priority;

        /// <summary>Display duration in seconds. 0 = use channel/style default.</summary>
        public float Duration;

        /// <summary>If set, duplicate Show() calls with same key update in-place instead of creating new.</summary>
        public string DeduplicationKey;

        /// <summary>Optional action button label. Null = no action button.</summary>
        public string ActionButtonLabel;

        /// <summary>Callback when action button is clicked.</summary>
        public Action OnAction;

        /// <summary>Callback when notification is dismissed (timeout or manual).</summary>
        public Action OnDismiss;

        /// <summary>Optional sound override. Null = use style default.</summary>
        public AudioClip Sound;
    }
}
