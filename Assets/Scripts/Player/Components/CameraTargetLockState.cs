using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Targeting.Core;

namespace Player.Components
{
    /// <summary>
    /// Lock-on state for camera and movement.
    /// All fields are predicted (no GhostField) - client sets values, prediction keeps them stable.
    /// Server doesn't need lock-on data since it's a client visual preference.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CameraTargetLockState : IComponentData
    {
        // Client-only lock state - NOT synced to prevent server overwrite
        public bool IsLocked;
        public float3 LastTargetPosition;
        public Entity TargetEntity;
        
        /// <summary>
        /// EPIC 15.16: Lock Phase State Machine.
        /// Unlocked → Locking → Locked → Unlocked
        /// Camera signals arrival for Locking → Locked transition.
        /// </summary>
        public LockPhase Phase;
        
        // Helper for input de-bouncing
        public bool WasGrabPressed;
        
        // EPIC 15.16: Soft lock break cooldown (per-entity for multiplayer support)
        public float SoftLockBreakCooldown;
        
        // Cross-system coordination: true for 1 frame after unlock to prevent flicker
        public bool JustUnlocked;
    }
}
