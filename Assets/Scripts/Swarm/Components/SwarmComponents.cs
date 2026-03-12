using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Swarm.Components
{
    // ──────────────────────────────────────────────
    // EPIC 16.2: Swarm Entity Framework — Core Components
    // ──────────────────────────────────────────────

    /// <summary>
    /// Particle-tier entity. Lightweight swarm member following flow field.
    /// No physics, no AI, no health. Just position + velocity + animation.
    /// </summary>
    public struct SwarmParticle : IComponentData
    {
        public float3 Position;
        public float3 Velocity;
        public float Speed;
        public uint ParticleID;
    }

    /// <summary>
    /// Animation state for GPU-instanced swarm rendering.
    /// Shader samples vertex animation texture at (vertexIndex, AnimTime).
    /// </summary>
    public struct SwarmAnimState : IComponentData
    {
        /// <summary>0=Idle, 1=Walk, 2=Run, 3=Attack, 4=Die</summary>
        public byte AnimClipIndex;
        /// <summary>Normalized time within clip (0-1).</summary>
        public float AnimTime;
        /// <summary>Playback speed multiplier.</summary>
        public float AnimSpeed;
    }

    /// <summary>
    /// Aware-tier entity. Has individual flow target and group cohesion.
    /// Still no physics or health — promoted from particle when within AwareRange.
    /// </summary>
    public struct SwarmAgent : IComponentData
    {
        public float3 FlowTarget;
        public float AgentTimer;
        public uint SourceParticleID;
    }

    /// <summary>
    /// Group identifier for coordinated swarm behavior.
    /// </summary>
    public struct SwarmGroupID : IComponentData
    {
        public int GroupIndex;
    }

    /// <summary>
    /// Tag on promoted combat-tier swarm entities.
    /// These entities have full Health, AIBrain(Swarm), single ability, physics capsule.
    /// </summary>
    public struct SwarmCombatTag : IComponentData
    {
        public uint SourceParticleID;
        public float PromotionTime;
    }

    /// <summary>
    /// Transient event: particle promoted to combat entity.
    /// Created by SwarmTierEvaluationSystem, consumed by SwarmPromotionSystem.
    /// </summary>
    public struct SwarmPromotionEvent : IComponentData
    {
        public uint ParticleID;
        public float3 Position;
        public float3 Velocity;
        public byte AnimClipIndex;
        public float AnimTime;
        public Entity SourceEntity;
    }

    /// <summary>
    /// Transient event: combat entity demoted back to particle.
    /// Created by SwarmTierEvaluationSystem, consumed by SwarmDemotionSystem.
    /// </summary>
    public struct SwarmDemotionEvent : IComponentData
    {
        public Entity CombatEntity;
        public float3 Position;
        public float3 Velocity;
    }

    /// <summary>
    /// Area damage event for particle-tier entities.
    /// Kills particles in radius without promoting them. Created by damage systems.
    /// </summary>
    public struct SwarmDamageZone : IComponentData
    {
        public float3 Center;
        public float Radius;
        public float Damage;
        public Entity Source;
    }

    /// <summary>
    /// Request to spawn death VFX at a position. Client-only.
    /// Consumed by SwarmDeathVFXSystem (managed, PresentationSystemGroup).
    /// </summary>
    public struct SwarmDeathVFXRequest : IComponentData
    {
        public float3 Position;
        /// <summary>0=normal, 1=explosion, 2=fire, 3=ice</summary>
        public byte DeathType;
        /// <summary>How many died at this cluster position (scales VFX intensity).</summary>
        public byte Count;
    }

    /// <summary>
    /// Singleton: global swarm configuration. Set via SwarmConfigAuthoring.
    /// </summary>
    public struct SwarmConfig : IComponentData
    {
        public Entity ParticlePrefab;
        public Entity CombatPrefab;

        // Movement
        public float BaseSpeed;
        public float SpeedVariance;

        // Tier thresholds
        public float AwareRange;
        public float CombatRange;
        public float DemoteRange;
        public float AwareHysteresis;

        // Hard caps
        public int MaxCombatEntities;
        public int MaxAwareEntities;

        // Flocking
        public float SeparationRadius;
        public float SeparationWeight;
        public float CohesionWeight;
        public float AlignmentWeight;
        public float FlowFieldWeight;

        // Noise
        public float NoiseScale;
        public float NoiseStrength;

        // Tier evaluation frequency
        public int TierEvalFrameInterval;

        // Combat
        public float CombatMeleeRange;
        public float CombatChaseSpeed;
        public float CombatDamage;
        public float CombatAttackCooldown;
    }

    /// <summary>
    /// Spawner component for batch-creating swarm particles.
    /// Follows the same frame-budgeted pattern as EnemySpawnerSystem.
    /// </summary>
    public struct SwarmSpawner : IComponentData
    {
        public SwarmSpawnMode Mode;
        public int TotalParticles;
        public int BatchSize;
        public float SpawnRadius;
        public int SpawnedCount;
        public bool IsComplete;
        public bool SpawnOnStart;
        public bool IsSpawning;
        public uint Seed;

        // Continuous mode
        /// <summary>Target number of alive particles. System spawns to maintain this count.</summary>
        public int TargetPopulation;
        /// <summary>Max particles spawned per second in continuous mode.</summary>
        public float SpawnRate;
        /// <summary>Accumulator for fractional spawns per frame.</summary>
        public float SpawnAccumulator;

        // Edge mode
        /// <summary>How far inside grid edge to spawn (meters). Prevents spawning at exact boundary.</summary>
        public float EdgeInset;
    }

    /// <summary>
    /// Tag to trigger swarm spawning at runtime.
    /// </summary>
    public struct SwarmSpawnRequest : IComponentData { }

    /// <summary>
    /// Formation configuration for emergent swarm behaviors.
    /// </summary>
    public struct SwarmFormationConfig : IComponentData
    {
        public SwarmFormationType Formation;
        public float DensityTarget;
        public float FunnelWidth;
        public float WallPileHeight;
    }

    /// <summary>
    /// How the spawner places new particles.
    /// </summary>
    public enum SwarmSpawnMode : byte
    {
        /// <summary>Scatter within radius around spawner position. One-time batch.</summary>
        Area = 0,
        /// <summary>Spawn along the flow field grid perimeter. One-time batch.</summary>
        Edge = 1,
        /// <summary>Continuously spawn at grid edges to maintain TargetPopulation.</summary>
        Continuous = 2
    }

    public enum SwarmFormationType : byte
    {
        Flow = 0,
        Funnel = 1,
        Pile = 2,
        Surround = 3,
        Scatter = 4
    }

    /// <summary>
    /// Per-particle emergent state (density, wall proximity, stall detection).
    /// </summary>
    public struct SwarmEmergentState : IComponentData
    {
        public float LocalDensity;
        public byte NearWall;
        public byte Stalled;
        public float StallTimer;
    }
}
