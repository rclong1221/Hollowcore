using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Player.Abilities
{
    /// <summary>
    /// Configuration for abilities that need to detect objects (ladders, ledges, etc).
    /// </summary>
    public struct DetectObjectAbility : IComponentData
    {
        public int AbilityIndex;
        public float DetectionRadius;
        public float3 DetectionOffset;
        public uint DetectionLayerMask; // Physics layers
        
        // Runtime state
        public bool TargetDetected;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float3 TargetNormal;
    }

    /// <summary>
    /// Configuration for abilities that check ground conditions (slide, etc).
    /// </summary>
    public struct DetectGroundAbility : IComponentData
    {
        public int AbilityIndex;
        public float MaxGroundAngle;
        public float MinGroundAngle;
        
        // Runtime state
        public bool IsValidGround;
        public float CurrentGroundAngle;
        public float3 CurrentGroundNormal;
    }
}
