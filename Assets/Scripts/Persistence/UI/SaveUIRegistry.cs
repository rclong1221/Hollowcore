using UnityEngine;

namespace DIG.Persistence.UI
{
    /// <summary>
    /// EPIC 16.15: Central registry for persistence UI providers.
    /// Follows CombatUIRegistry pattern — static singleton, MonoBehaviours register on Awake.
    /// </summary>
    public static class SaveUIRegistry
    {
        private static ISaveNotificationProvider _notifications;
        private static ISaveSlotProvider _slots;
        private static ISaveProgressProvider _progress;

        // ==================== PROPERTIES ====================

        public static ISaveNotificationProvider Notifications => _notifications;
        public static ISaveSlotProvider Slots => _slots;
        public static ISaveProgressProvider Progress => _progress;

        public static bool HasNotifications => _notifications != null;
        public static bool HasSlots => _slots != null;
        public static bool HasProgress => _progress != null;

        // ==================== REGISTRATION ====================

        public static void RegisterNotifications(ISaveNotificationProvider provider)
        {
            if (_notifications != null && provider != null)
                Debug.LogWarning("[SaveUIRegistry] Replacing existing notification provider.");
            _notifications = provider;
        }

        public static void RegisterSlots(ISaveSlotProvider provider)
        {
            if (_slots != null && provider != null)
                Debug.LogWarning("[SaveUIRegistry] Replacing existing slot provider.");
            _slots = provider;
        }

        public static void RegisterProgress(ISaveProgressProvider provider)
        {
            if (_progress != null && provider != null)
                Debug.LogWarning("[SaveUIRegistry] Replacing existing progress provider.");
            _progress = provider;
        }

        // ==================== UNREGISTRATION ====================

        public static void UnregisterAll()
        {
            _notifications = null;
            _slots = null;
            _progress = null;
        }

        public static void UnregisterNotifications(ISaveNotificationProvider provider)
        {
            if (_notifications == provider) _notifications = null;
        }

        public static void UnregisterSlots(ISaveSlotProvider provider)
        {
            if (_slots == provider) _slots = null;
        }

        public static void UnregisterProgress(ISaveProgressProvider provider)
        {
            if (_progress == provider) _progress = null;
        }
    }
}
