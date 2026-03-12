using Unity.Entities;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.33: Runtime state for social aggro tracking.
    /// Paired with SocialAggroConfig on social enemy entities.
    /// </summary>
    public struct SocialAggroState : IComponentData
    {
        /// <summary>Countdown until next call-for-help is allowed.</summary>
        public float CallForHelpTimer;

        /// <summary>Number of allies that have died since encounter start.</summary>
        public int AllyDeathCount;

        /// <summary>Entity of the most recently killed ally.</summary>
        public Entity LastDeadAlly;

        /// <summary>Time remaining for rage multiplier (from ally death).</summary>
        public float RageTimer;

        public static SocialAggroState Default => new SocialAggroState
        {
            CallForHelpTimer = 0f,
            AllyDeathCount = 0,
            LastDeadAlly = Entity.Null,
            RageTimer = 0f
        };
    }
}
