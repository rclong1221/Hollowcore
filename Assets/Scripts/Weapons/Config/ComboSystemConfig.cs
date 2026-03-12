using UnityEngine;

namespace DIG.Weapons.Config
{
    /// <summary>
    /// Global configuration for the combo system.
    /// Create via: Create > DIG > Combat > Combo System Config
    /// </summary>
    [CreateAssetMenu(fileName = "ComboSystemConfig", menuName = "DIG/Combat/Combo System Config")]
    public class ComboSystemConfig : ScriptableObject
    {
        [Header("Input Mode")]
        [Tooltip("How combo input is processed globally.")]
        public ComboInputMode InputMode = ComboInputMode.InputPerSwing;

        [Header("Queue Settings")]
        [Tooltip("Maximum number of attacks that can be queued. 0 = no queue, 1 = buffer one ahead, -1 = unlimited.")]
        [Range(-1, 5)]
        public int QueueDepth = 1;

        [Header("Cancel Settings")]
        [Tooltip("When attacks can be canceled by other actions.")]
        public ComboCancelPolicy CancelPolicy = ComboCancelPolicy.RecoveryOnly;

        [Tooltip("Which actions can cancel attacks.")]
        public ComboCancelPriority CancelPriority = ComboCancelPriority.Dodge;

        [Tooltip("When the attack queue should be cleared.")]
        public ComboQueueClearPolicy QueueClearPolicy = ComboQueueClearPolicy.Standard;

        [Header("Rhythm Mode Settings")]
        [Tooltip("For RhythmBased mode: normalized time window start for valid input.")]
        [Range(0f, 1f)]
        public float RhythmWindowStart = 0.6f;

        [Tooltip("For RhythmBased mode: normalized time window end for valid input.")]
        [Range(0f, 1f)]
        public float RhythmWindowEnd = 0.9f;

        [Tooltip("For RhythmBased mode: bonus damage multiplier for perfect timing.")]
        public float RhythmPerfectBonus = 1.25f;

        [Header("Per-Weapon Override")]
        [Tooltip("Allow individual weapons to override these global settings.")]
        public bool AllowPerWeaponOverride = true;

        // ============================================================
        // Preset Factory Methods
        // ============================================================

        /// <summary>
        /// Apply Souls-like preset (Dark Souls, Elden Ring, Monster Hunter).
        /// </summary>
        public void ApplySoulsLikePreset()
        {
            InputMode = ComboInputMode.InputPerSwing;
            QueueDepth = 1;
            CancelPolicy = ComboCancelPolicy.RecoveryOnly;
            CancelPriority = ComboCancelPriority.Dodge;
            QueueClearPolicy = ComboQueueClearPolicy.Standard;
        }

        /// <summary>
        /// Apply Character Action preset (Devil May Cry, Bayonetta).
        /// </summary>
        public void ApplyCharacterActionPreset()
        {
            InputMode = ComboInputMode.HoldToCombo;
            QueueDepth = 3;
            CancelPolicy = ComboCancelPolicy.Anytime;
            CancelPriority = ComboCancelPriority.Dodge | ComboCancelPriority.Jump | ComboCancelPriority.Ability;
            QueueClearPolicy = ComboQueueClearPolicy.OnCancel;
        }

        /// <summary>
        /// Apply Brawler preset (Batman Arkham, Spider-Man).
        /// </summary>
        public void ApplyBrawlerPreset()
        {
            InputMode = ComboInputMode.RhythmBased;
            QueueDepth = 2;
            CancelPolicy = ComboCancelPolicy.Anytime;
            CancelPriority = ComboCancelPriority.Dodge | ComboCancelPriority.Block | ComboCancelPriority.Movement;
            QueueClearPolicy = ComboQueueClearPolicy.Never;
            RhythmWindowStart = 0.6f;
            RhythmWindowEnd = 0.85f;
            RhythmPerfectBonus = 1.3f;
        }
    }
}
