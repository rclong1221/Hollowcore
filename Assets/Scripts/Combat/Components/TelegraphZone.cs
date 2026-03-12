using Unity.Entities;
using Unity.Mathematics;
using DIG.AI.Components;
using DIG.Targeting.Theming;
using DIG.Combat.Resolvers;
using DIG.Weapons;

namespace DIG.Combat.Components
{
    /// <summary>
    /// EPIC 15.32: AOE telegraph zone entity.
    /// Spawned by AbilityExecutionSystem, ticked by TelegraphDamageSystem.
    /// NOT ghost-replicated — server-side only. TelegraphVisualBridge reads from ServerWorld.
    /// </summary>
    public struct TelegraphZone : IComponentData
    {
        public TelegraphShape Shape;
        public float3 Position;
        public quaternion Rotation;
        public float Radius;
        public float InnerRadius;      // For Ring shape (0 = solid)
        public float Angle;            // Cone angle (degrees)
        public float Length;           // Line length
        public float Width;            // Line width
        public float WarningDuration;  // Time before damage (visual warning)
        public float DamageDelay;      // Time from spawn to first damage tick
        public float LingerDuration;   // How long zone persists after first damage (0 = one-shot)
        public float TickInterval;     // Damage repeat interval (0 = single hit)
        public float Timer;            // Elapsed time since spawn
        public float LastTickTime;     // Time of last damage tick
        public float DamageBase;
        public float DamageVariance;
        public DamageType DamageType;
        public Entity OwnerEntity;     // Who spawned this (for stat lookups)
        public int MaxTargets;
        public CombatResolverType ResolverType;
        public bool IsSafeZone;        // Inverted: damage OUTSIDE zone
        public bool HasDealtDamage;    // One-shot flag (for non-lingering zones)

        // Status effect applied by zone (from ability modifier)
        public ModifierType Modifier0Type;
        public float Modifier0Chance;
        public float Modifier0Duration;
        public float Modifier0Intensity;
    }
}
