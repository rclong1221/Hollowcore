using UnityEngine;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Static registry for quest UI providers.
    /// Follows CombatUIRegistry pattern — MonoBehaviour adapters register on Awake/Enable.
    /// </summary>
    public static class QuestUIRegistry
    {
        private static IQuestLogProvider _questLog;
        private static IObjectiveTrackerProvider _objectiveTracker;
        private static IQuestNotificationProvider _notifications;

        // --- Properties ---
        public static IQuestLogProvider QuestLog => _questLog;
        public static IObjectiveTrackerProvider ObjectiveTracker => _objectiveTracker;
        public static IQuestNotificationProvider Notifications => _notifications;

        // --- Has checks ---
        public static bool HasQuestLog => _questLog != null;
        public static bool HasObjectiveTracker => _objectiveTracker != null;
        public static bool HasNotifications => _notifications != null;

        // --- Registration ---
        public static void RegisterQuestLog(IQuestLogProvider provider)
        {
            if (_questLog != null && provider != null)
                Debug.LogWarning("[QuestUIRegistry] Replacing existing quest log provider.");
            _questLog = provider;
        }

        public static void RegisterObjectiveTracker(IObjectiveTrackerProvider provider)
        {
            if (_objectiveTracker != null && provider != null)
                Debug.LogWarning("[QuestUIRegistry] Replacing existing objective tracker provider.");
            _objectiveTracker = provider;
        }

        public static void RegisterNotifications(IQuestNotificationProvider provider)
        {
            if (_notifications != null && provider != null)
                Debug.LogWarning("[QuestUIRegistry] Replacing existing notification provider.");
            _notifications = provider;
        }

        // --- Unregistration ---
        public static void UnregisterQuestLog(IQuestLogProvider provider)
        {
            if (_questLog == provider) _questLog = null;
        }

        public static void UnregisterObjectiveTracker(IObjectiveTrackerProvider provider)
        {
            if (_objectiveTracker == provider) _objectiveTracker = null;
        }

        public static void UnregisterNotifications(IQuestNotificationProvider provider)
        {
            if (_notifications == provider) _notifications = null;
        }

        public static void UnregisterAll()
        {
            _questLog = null;
            _objectiveTracker = null;
            _notifications = null;
            QuestEventQueue.Clear();
        }
    }
}
