using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Tuning data for dodge dive (forward dive that ends in prone), authored per-prefab
    /// </summary>
    public struct DodgeDiveComponent : IComponentData
    {
        public float Duration;          // seconds - total dive duration
        public float Distance;          // meters - horizontal distance traveled
        public float InvulnWindowStart; // seconds from start
        public float InvulnWindowEnd;   // seconds from start
        public float StaminaCost;       // stamina consumed
        public float Cooldown;          // seconds before another dive is allowed
        public byte EndInProne;         // 1 = transition to prone at end, 0 = just land
        
        // Collision filtering
        public uint CollisionLayerMask; // Layers to check for collision
        public float MinFloorNormalY;   // Minimum Y component of normal to consider floor
        public float MaxFloorHeight;    // Maximum height above ground to consider floor hit

        public static DodgeDiveComponent Default => new DodgeDiveComponent
        {
            Duration = 0.8f,
            Distance = 4.0f,
            InvulnWindowStart = 0.1f,
            InvulnWindowEnd = 0.6f,
            StaminaCost = 25f,
            Cooldown = 1.5f,
            EndInProne = 1,
            CollisionLayerMask = 0,
            MinFloorNormalY = 0.7f,
            MaxFloorHeight = 0.2f
        };
    }

    /// <summary>
    /// Runtime state for an active dodge dive
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DodgeDiveState : IComponentData
    {
        [GhostField] public float Elapsed;           // seconds since dive started
        [GhostField] public float Duration;
        [GhostField] public float DistanceRemaining;
        [GhostField] public float InvulnStart;
        [GhostField] public float InvulnEnd;
        [GhostField] public byte IsActive;           // 0/1
        [GhostField] public uint StartFrame;         // network input frame when dive started
        [GhostField] public float CooldownRemaining; // seconds until next dive allowed
        [GhostField] public byte WillEndInProne;     // 1 if should transition to prone at end
        
        // Prediction reconciliation fields
        [GhostField] public float ServerElapsed;
        [GhostField] public float ReconcileSmoothing;
        [GhostField] public byte IsReconciling;
    }

    /// <summary>
    /// Marker component for predicted dive (similar to dodge roll)
    /// </summary>
    public struct PredictedDodgeDive : IComponentData
    {
        public uint FrameCount;
        public float PredictedStartTime;
    }
}
