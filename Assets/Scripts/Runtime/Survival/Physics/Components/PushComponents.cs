using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Physics
{
    /// <summary>
    /// Tag/Data for an object that can be pushed/pulled by players.
    /// </summary>
    public struct PushableObject : IComponentData
    {
        public float Mass;          // Mass in Kg (visual helpful for tooltips)
        public float Friction;      // Friction coefficient (ground resistance)
        
        // Offset for the 'Grip' point relative to object center?
        // For now, we raycast to find grip point.
    }

    /// <summary>
    /// Component on the Player indicating they are currently pushing an object.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ActivePushConstraint : IComponentData
    {
        public Entity TargetObject;     // The crate entity
        public Entity PhysicsJoint;     // The joint entity (PhysicsConstrainedBodyPair)
        public float3 LocalGripPoint;   // Grip point in object local space
        public float3 LocalPlayerOffset;// Where player should stand relative to object
        
        // State tracking
        public bool IsPushing;
    }

    /// <summary>
    /// Settings for push mechanics (strength, limits).
    /// </summary>
    public struct PushSettings : IComponentData
    {
        public float MaxPushMass;       // Kg limit solo
        public float PushSpeedModifier; // Speed factor when pushing
        public float BreakForce;        // Force limit before joint breaks
        
        public static PushSettings Default => new PushSettings
        {
            MaxPushMass = 100f,
            PushSpeedModifier = 0.5f,
            BreakForce = 2000f
        };
    }
}
