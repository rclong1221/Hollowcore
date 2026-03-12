using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.19 + 15.33: Tracks the alert level of an AI entity.
    /// Alert AI have improved detection capabilities via AlertStateMultiplier.
    ///
    /// 5-level alert model:
    /// IDLE(0) → CURIOUS(1) → SUSPICIOUS(2) → SEARCHING(3) → COMBAT(4)
    /// Escalation is immediate; de-escalation steps down one level at a time with timers.
    /// </summary>
    public struct AlertState : IComponentData
    {
        /// <summary>Current alert level (use constants below).</summary>
        public int AlertLevel;

        /// <summary>Time remaining in current alert state before decaying one level.</summary>
        public float AlertTimer;

        /// <summary>Entity that caused the alert (for investigation behavior).</summary>
        public Entity AlertSource;

        /// <summary>Last known position of alert source (for investigation).</summary>
        public float3 AlertPosition;

        /// <summary>EPIC 15.33: Position to investigate (set on SEARCHING entry).</summary>
        public float3 InvestigatePosition;

        /// <summary>EPIC 15.33: How long to search at investigate point before giving up.</summary>
        public float SearchDuration;

        /// <summary>EPIC 15.33: Time spent in SEARCHING state (for AI behavior).</summary>
        public float SearchTimer;

        /// <summary>EPIC 15.33: Whether the AI has reached the investigate point.</summary>
        public bool HasInvestigated;

        // Alert level constants
        public const int IDLE = 0;
        public const int CURIOUS = 1;
        public const int SUSPICIOUS = 2;
        public const int SEARCHING = 3;
        public const int COMBAT = 4;

        public static AlertState Default => new AlertState
        {
            AlertLevel = IDLE,
            AlertTimer = 0f,
            AlertSource = Entity.Null,
            AlertPosition = float3.zero,
            InvestigatePosition = float3.zero,
            SearchDuration = 10f,
            SearchTimer = 0f,
            HasInvestigated = false
        };
    }
}
