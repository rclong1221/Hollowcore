using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Player.Components
{
    /// <summary>
    /// Tackle state component for Epic 7.4.2.
    /// Tracks state of an active tackle (intentional knockdown attempt).
    /// 
    /// Tackle is a high-risk, high-reward action:
    /// - Tackler commits to forward direction (can't change mid-tackle)
    /// - On hit: target gets knockdown, tackler gets brief stagger
    /// - On miss: tackler gets longer stagger (punishment for whiffing)
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct TackleState : IComponentData
    {
        /// <summary>
        /// Time remaining in active tackle phase.
        /// Tackle is active while this > 0.
        /// </summary>
        [GhostField] public float TackleTimeRemaining;
        
        /// <summary>
        /// Direction tackler committed to (normalized, horizontal plane only).
        /// Direction is locked at tackle initiation and cannot be changed.
        /// </summary>
        [GhostField] public float3 TackleDirection;
        
        /// <summary>
        /// Cooldown before tackle can be used again.
        /// Decremented each frame, tackle input ignored while > 0.
        /// </summary>
        [GhostField] public float TackleCooldown;
        
        /// <summary>
        /// Did tackler hit a target during this tackle?
        /// Used for animation branching (hit reaction vs stumble/whiff).
        /// Reset on new tackle.
        /// </summary>
        [GhostField] public bool DidHitTarget;
        
        /// <summary>
        /// Speed at tackle initiation (for impact calculation).
        /// Stored to calculate knockback force on hit.
        /// </summary>
        [GhostField] public float TackleSpeed;
        
        /// <summary>
        /// Whether tackle has already processed a hit this activation.
        /// Prevents multi-hitting same target in one tackle.
        /// Reset on new tackle.
        /// </summary>
        [GhostField] public bool HasProcessedHit;
    }
}
