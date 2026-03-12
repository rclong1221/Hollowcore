using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    // Tuning data for dodge roll, authored per-prefab
    public struct DodgeRollComponent : IComponentData
    {
        public float Duration; // seconds
        public float Distance; // meters
        public float InvulnWindowStart; // seconds from start
        public float InvulnWindowEnd;   // seconds from start
        public float StaminaCost;       // stamina consumed
        public float Cooldown;          // seconds before another roll is allowed
        
        // Collision filtering for more robust detection
        public uint CollisionLayerMask; // Layers to check for collision (0 = all layers)
        public float MinFloorNormalY;   // Minimum Y component of normal to consider floor (default 0.7 = ~45 degrees)
        public float MaxFloorHeight;    // Maximum height above ground to still consider floor hit

        public static DodgeRollComponent Default => new DodgeRollComponent
        {
            Duration = 0.6f,
            Distance = 3.0f,
            InvulnWindowStart = 0.05f,
            InvulnWindowEnd = 0.45f,
            StaminaCost = 20f,
            Cooldown = 1.0f,
            CollisionLayerMask = 0, // 0 means check all layers (backward compatible)
            MinFloorNormalY = 0.7f, // ~45 degree slopes
            MaxFloorHeight = 0.2f   // 20cm tolerance for floor detection
        };
    }

    // Runtime state for an active dodge roll
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DodgeRollState : IComponentData
    {
        [GhostField] public float Elapsed; // seconds since roll started
        [GhostField] public float Duration;
        [GhostField] public float DistanceRemaining;
        [GhostField] public float InvulnStart;
        [GhostField] public float InvulnEnd;
        [GhostField] public byte IsActive; // 0/1
        [GhostField] public uint StartFrame; // network input frame when roll started (0 if local/hybrid)
        [GhostField] public float CooldownRemaining; // seconds until next roll allowed
        
        // Prediction reconciliation fields
        [GhostField] public float ServerElapsed;      // Authoritative server elapsed time (for reconciliation)
        [GhostField] public float ReconcileSmoothing; // Smoothing factor for elapsed time corrections (0-1)
        [GhostField] public byte IsReconciling;       // 1 if currently smoothing a correction
    }
}
