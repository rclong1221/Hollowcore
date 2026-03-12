using UnityEngine;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Static gate that provides a clean protocol for overriding gameplay cameras.
    /// CameraManager and CinemachineCameraController check IsOverridden in their LateUpdate
    /// and yield control when true. Priority-based acquire/release semantics.
    /// </summary>
    public static class CameraAuthorityGate
    {
        /// <summary>Whether any system currently overrides gameplay cameras.</summary>
        public static bool IsOverridden { get; private set; }

        /// <summary>Name of the current override owner (for debugging).</summary>
        public static string CurrentOwner { get; private set; }

        /// <summary>Priority of the current override (higher = harder to preempt).</summary>
        public static int CurrentPriority { get; private set; }

        /// <summary>
        /// Acquire camera authority. Succeeds if no current owner or priority >= current.
        /// </summary>
        /// <param name="owner">Identifier for the acquiring system.</param>
        /// <param name="priority">Priority level (0=gameplay, 10=death, 20=cutscene, 30=editor).</param>
        /// <returns>True if authority was acquired.</returns>
        public static bool Acquire(string owner, int priority)
        {
            if (IsOverridden && priority < CurrentPriority)
            {
                DCamLog.LogWarning($"[CameraAuthorityGate] {owner} (priority {priority}) cannot preempt {CurrentOwner} (priority {CurrentPriority}).");
                return false;
            }

            IsOverridden = true;
            CurrentOwner = owner;
            CurrentPriority = priority;
            DCamLog.Log($"[DCam] AuthorityGate ACQUIRED by '{owner}' (priority {priority})");
            return true;
        }

        /// <summary>
        /// Release camera authority. Only succeeds if caller matches current owner.
        /// </summary>
        public static void Release(string owner)
        {
            if (!IsOverridden) return;

            if (CurrentOwner != owner)
            {
                DCamLog.LogWarning($"[CameraAuthorityGate] {owner} tried to release, but {CurrentOwner} holds authority.");
                return;
            }

            DCamLog.Log($"[DCam] AuthorityGate RELEASED by '{owner}'");
            IsOverridden = false;
            CurrentOwner = null;
            CurrentPriority = 0;
        }

        /// <summary>
        /// Unconditional release — for editor reset and error recovery.
        /// </summary>
        public static void ForceRelease()
        {
            IsOverridden = false;
            CurrentOwner = null;
            CurrentPriority = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsOverridden = false;
            CurrentOwner = null;
            CurrentPriority = 0;
        }
    }
}
