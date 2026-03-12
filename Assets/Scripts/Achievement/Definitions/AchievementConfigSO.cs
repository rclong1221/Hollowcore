using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Global achievement system configuration.
    /// Load from Resources/AchievementConfig by AchievementBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Achievement/Config")]
    public class AchievementConfigSO : ScriptableObject
    {
        [Tooltip("Max achievements tracked in progress buffer")]
        [Min(1)]
        public int MaxTrackedAchievements = 128;

        [Tooltip("Seconds to show unlock toast notification")]
        [Min(0.5f)]
        public float ToastDisplayDuration = 5.0f;

        [Tooltip("Max queued toasts before dropping oldest")]
        [Min(1)]
        public int ToastQueueMaxSize = 5;

        [Tooltip("Auto-trigger save on achievement unlock")]
        public bool SaveOnUnlock = true;

        [Tooltip("Global toggle for hidden achievement system")]
        public bool EnableHiddenAchievements = true;

        [Tooltip("Frames between tracking updates (1 = every frame)")]
        [Min(1)]
        public int ProgressUpdateInterval = 1;

        [Tooltip("Reset kill streak counter on player death")]
        public bool KillStreakResetOnDeath = true;

        [Tooltip("Global toggle for toast popup notifications")]
        public bool EnableToastNotifications = true;
    }
}
