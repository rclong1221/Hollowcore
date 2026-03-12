# EPIC 23.3: Zone Orchestration & Encounter Direction

**Status:** PLANNED
**Priority:** Critical (core gameplay loop — no zones = no rogue-lite)
**Dependencies:**
- EPIC 23.1 (RunState, RunPhase, RunLifecycleSystem)
- `EncounterState` + `EncounterTriggerDefinition` (existing — `Assets/Scripts/AI/Components/`, EPIC 15.32)
- `EnemySpawner` + `EnemySpawnRequest` (existing — `Assets/Scripts/AI/Components/EnemySpawnerComponents.cs`)
- `LootTableSO` (existing — `Assets/Scripts/Loot/Definitions/LootTableSO.cs`, EPIC 16.6)
- `TeleportSystem` (existing — `Assets/Scripts/Player/Systems/TeleportSystem.cs`)

**Feature:** Game-agnostic zone orchestration framework. Supports any style of rogue-lite: corridor room-clearing (Hades), open-world exploration (Risk of Rain 2), branching paths (Slay the Spire), arena survival (Vampire Survivors), or hybrids. The framework controls the lifecycle — games control the geometry and presentation.

---

## Problem

Zone/level generation is inherently game-specific — a voxel cave generator and a prefab room stitcher share nothing technically. But the orchestration layer (sequencing zones, directing encounters, detecting completion, managing transitions) is identical across all rogue-lite styles. The encounter spawning pattern also varies wildly: some games spawn a fixed wave and wait for kills, others spawn continuously with escalating intensity, others use player-triggered set pieces. The framework must support all of these without prescribing one approach.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        FRAMEWORK (DIG.Roguelite)                        │
│                                                                         │
│  ZoneSequenceSO ──► ZoneSequenceResolver ──► resolved zone list         │
│                                                                         │
│  ZoneTransitionSystem: manages IZoneProvider lifecycle                   │
│       Initialize → poll IsReady → Activate → ... → Deactivate           │
│                                                                         │
│  SpawnDirectorSystem: reads SpawnDirectorConfig, manages spawn budget    │
│       ┌─ Burst mode: one-shot batch (corridor/arena)                    │
│       └─ Continuous mode: credit-per-second escalation (open-world)     │
│       Creates SpawnRequest entities → game's spawner consumes them       │
│                                                                         │
│  ZoneClearDetectionSystem: polls IZoneClearCondition                     │
│       Built-in: AllEnemiesDead, PlayerTriggered, TimerExpired, Composite │
│                                                                         │
│  InteractableDirectorSystem: places interactable nodes from pool         │
│       Games provide IInteractableHandler to spawn actual objects         │
│                                                                         │
│  ZoneUIBridgeSystem → ZoneUIRegistry → game HUD                         │
│                                                                         │
├─────────────────────────────────────────────────────────────────────────┤
│                         GAME LAYER (implements)                          │
│                                                                         │
│  IZoneProvider ─── VoxelCaveProvider, PrefabRoomProvider, SceneProvider  │
│  IInteractableHandler ─── ChestSpawner, ShrineSpawner, TeleporterPlacer │
│  IZoneClearCondition ─── custom win conditions                          │
│  ISpawnPositionProvider ─── navmesh sampling, predefined points, random  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Core Types

### Zone Provider Interface

```csharp
// File: Assets/Scripts/Roguelite/Interfaces/IZoneProvider.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Games implement this for their level technology.
    ///
    /// Examples:
    ///   VoxelCaveProvider     — procedural cave generation
    ///   PrefabRoomProvider    — instantiate and stitch room prefabs
    ///   SceneZoneProvider     — additive scene loading
    ///   HandCraftedProvider   — activate pre-placed zone GameObjects
    ///   OpenWorldProvider     — large terrain with scattered points of interest
    /// </summary>
    public interface IZoneProvider
    {
        /// <summary>Begin loading/generating a zone. May be async (scene loading, generation, etc).</summary>
        void Initialize(uint seed, int zoneIndex, ZoneDefinitionSO definition);

        /// <summary>True when zone geometry, navmesh, lighting, and all assets are ready.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Make zone playable. Called once after IsReady returns true.
        /// Returns activation data that the framework uses for player placement,
        /// spawn point registration, and interactable placement.
        /// </summary>
        ZoneActivationResult Activate();

        /// <summary>Tear down zone. Unload assets, destroy spawned entities, cleanup.</summary>
        void Deactivate();

        /// <summary>Full cleanup on run end or provider swap.</summary>
        void Dispose();
    }

    /// <summary>
    /// Data returned by IZoneProvider.Activate(). The framework reads this
    /// to place the player, register spawn points, and seed interactables.
    /// All arrays are optional — null = not applicable for this zone style.
    /// </summary>
    public struct ZoneActivationResult
    {
        /// <summary>Where to teleport the player (or first player in co-op).</summary>
        public float3 PlayerSpawnPosition;

        /// <summary>Additional spawn positions for co-op players. Null = offset from primary.</summary>
        public float3[] CoopSpawnPositions;

        /// <summary>
        /// Valid positions where enemies can spawn. Null = game uses ISpawnPositionProvider
        /// for dynamic position sampling (e.g., navmesh queries).
        /// For corridor games: predefined spawn markers in each room.
        /// For open-world games: null (use navmesh sampling instead).
        /// </summary>
        public float3[] SpawnPoints;

        /// <summary>
        /// Positions where interactables (chests, shrines, equipment) can appear.
        /// Null = no interactables for this zone, or game handles placement itself.
        /// </summary>
        public float3[] InteractableNodes;

        /// <summary>
        /// Position of the zone exit / teleporter / portal. Null = no fixed exit
        /// (e.g., the game uses a player-activated exit or time-based transition).
        /// </summary>
        public float3? ExitPosition;

        /// <summary>
        /// Axis-aligned bounds of the playable area. Used by spawn director for
        /// distance-from-player checks and off-screen spawning. Zero = unbounded.
        /// </summary>
        public Bounds PlayableArea;
    }
}
```

### Zone Clear Condition Interface

```csharp
// File: Assets/Scripts/Roguelite/Interfaces/IZoneClearCondition.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Pluggable: when is a zone "cleared"?
    /// Framework provides built-in implementations. Games can implement custom conditions.
    /// Multiple conditions can be composed via CompositeClearCondition.
    /// </summary>
    public interface IZoneClearCondition
    {
        /// <summary>Called once when the zone becomes Active. Reset internal state.</summary>
        void OnZoneActivated(SystemBase system, ZoneDefinitionSO definition);

        /// <summary>Polled each frame during Active/BossEncounter phases.</summary>
        bool IsCleared(SystemBase system);
    }

    /// <summary>All enemies spawned and all dead. Classic corridor roguelite.</summary>
    public class AllEnemiesDeadCondition : IZoneClearCondition { ... }

    /// <summary>
    /// Player interacts with an exit object (teleporter, portal, door).
    /// Used for open-world zones where the player chooses when to leave.
    /// Reads ZoneExitActivated enableable component on RunState entity.
    /// </summary>
    public class PlayerTriggeredCondition : IZoneClearCondition { ... }

    /// <summary>Survive for N seconds. Timer survival zones.</summary>
    public class TimerExpiredCondition : IZoneClearCondition { ... }

    /// <summary>
    /// Player-triggered boss kill: player activates exit → boss spawns →
    /// boss dies → zone clears. Two-phase (Risk of Rain 2 teleporter pattern).
    /// Phase 1: ZoneExitActivated → spawns boss encounter.
    /// Phase 2: Boss EncounterState completes → zone cleared.
    /// </summary>
    public class TriggerThenBossCondition : IZoneClearCondition { ... }

    /// <summary>
    /// Combine multiple conditions with AND/OR logic.
    /// Example: (AllEnemiesDead OR TimerExpired) AND PlayerAtExit
    /// </summary>
    public class CompositeClearCondition : IZoneClearCondition
    {
        public enum Logic { And, Or }
        public Logic Mode;
        public IZoneClearCondition[] Conditions;
        ...
    }
}
```

### Spawn Position Provider Interface

```csharp
// File: Assets/Scripts/Roguelite/Interfaces/ISpawnPositionProvider.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Games implement this to resolve WHERE enemies spawn in their zone geometry.
    /// The spawn director calls this when it needs a position for a new spawn request.
    ///
    /// Examples:
    ///   NavMeshSpawnProvider     — samples random navmesh points at distance from player
    ///   MarkerSpawnProvider      — picks from ZoneActivationResult.SpawnPoints
    ///   OffScreenSpawnProvider   — spawns just outside camera view (top-down games)
    ///   NestSpawnProvider        — spawns near pre-placed nest/hive objects
    /// </summary>
    public interface ISpawnPositionProvider
    {
        /// <summary>
        /// Get a valid spawn position for an enemy.
        /// Returns false if no valid position found this frame (all points occupied, etc).
        /// </summary>
        bool TryGetSpawnPosition(
            float3 playerPosition,
            float minDistance,
            float maxDistance,
            ref Random rng,
            out float3 position);
    }
}
```

### Interactable Handler Interface

```csharp
// File: Assets/Scripts/Roguelite/Interfaces/IInteractableHandler.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Games implement this to spawn actual interactable objects from placement nodes.
    /// The framework's InteractableDirectorSystem calls this with positions and budgets;
    /// the game decides what to place (chests, shrines, equipment barrels, etc).
    ///
    /// Games that don't want framework-managed interactables simply don't register a handler.
    /// </summary>
    public interface IInteractableHandler
    {
        /// <summary>
        /// Place interactables in the zone. Called once after zone activation.
        /// </summary>
        void PlaceInteractables(
            float3[] nodes,
            uint seed,
            float difficulty,
            ZoneDefinitionSO zoneDefinition);

        /// <summary>Cleanup all placed interactables on zone deactivation.</summary>
        void ClearInteractables();
    }
}
```

### Zone Definition

```csharp
// File: Assets/Scripts/Roguelite/Definitions/ZoneDefinitionSO.cs
namespace DIG.Roguelite.Zones
{
    public enum ZoneType : byte
    {
        Combat = 0,       // Standard enemy zone
        Elite = 1,        // Harder enemies, better rewards
        Boss = 2,         // Boss encounter zone
        Shop = 3,         // Spend run currency
        Event = 4,        // Narrative/risk-reward choice
        Rest = 5,         // Heal, upgrade, prepare
        Treasure = 6,     // Bonus loot zone
        Exploration = 7,  // Open-world: explore, find interactables, player-triggered exit
        Arena = 8,        // Survival waves (Vampire Survivors style)
        Secret = 9        // Hidden zone, unlocked by conditions
    }

    [CreateAssetMenu(menuName = "DIG/Roguelite/Zone Definition")]
    public class ZoneDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int ZoneId;
        public string DisplayName;
        public ZoneType Type;
        public Sprite Icon;
        [TextArea(2, 4)]
        public string Description;

        [Header("Difficulty")]
        [Tooltip("Multiplier applied on top of the run's base difficulty for this zone.")]
        public float DifficultyMultiplier = 1f;
        [Tooltip("Earliest zone index where this can appear. 0 = any.")]
        public int MinZoneIndex;
        [Tooltip("Latest zone index where this can appear. 0 = any.")]
        public int MaxZoneIndex;

        [Header("Encounters")]
        [Tooltip("Pool of enemies for this zone. Null = no framework-spawned enemies.")]
        public EncounterPoolSO EncounterPool;
        [Tooltip("How the spawn director operates in this zone.")]
        public SpawnDirectorConfigSO SpawnDirectorConfig;

        [Header("Rewards")]
        [Tooltip("Reward choices on zone clear. Null = no framework reward screen.")]
        public RewardPoolSO ClearRewardPool;  // from 23.5
        [Tooltip("Loot table for bonus drops. Uses existing loot pipeline.")]
        public LootTableSO BonusLootTable;

        [Header("Interactables")]
        [Tooltip("How many interactable nodes to populate. 0 = none / game handles it.")]
        public int InteractableBudget;
        [Tooltip("Interactable pool (chests, shrines, etc). Null = game handles placement.")]
        public InteractablePoolSO InteractablePool;

        [Header("Clear Condition")]
        [Tooltip("How this zone is considered cleared. Configurable per zone type.")]
        public ZoneClearMode ClearMode = ZoneClearMode.AllEnemiesDead;
        [Tooltip("Timer duration for TimerSurvival clear mode, in seconds.")]
        public float SurvivalTimer;

        [Header("Environment")]
        [Tooltip("Size hint for spawn density and interactable count scaling.")]
        public ZoneSizeHint SizeHint = ZoneSizeHint.Medium;
        [Tooltip("Environmental hazard profile. Null = no framework-managed hazards.")]
        public ScriptableObject HazardProfile;

        [Header("Extension")]
        [Tooltip("Game-specific data. Cast to your own types in IZoneProvider.")]
        public ScriptableObject[] ExtensionData;
    }

    public enum ZoneClearMode : byte
    {
        AllEnemiesDead = 0,       // Kill everything. Hades/Dead Cells.
        PlayerTriggered = 1,      // Player activates exit. Risk of Rain 2.
        TimerSurvival = 2,        // Survive N seconds. Vampire Survivors.
        BossKill = 3,             // Kill the boss entity. Boss zones.
        TriggerThenBoss = 4,      // Activate exit → boss spawns → kill boss. RoR2 teleporter.
        Objective = 5,            // Game-specific objective (via custom IZoneClearCondition).
        Manual = 6                // Framework doesn't check — game calls ZoneClearAPI directly.
    }

    public enum ZoneSizeHint : byte
    {
        Tiny = 0,     // Single room / small arena
        Small = 1,    // A few connected rooms
        Medium = 2,   // Standard zone
        Large = 3,    // Large open area
        Massive = 4   // Exploration-scale (RoR2 stages)
    }
}
```

### Zone Sequence

```csharp
// File: Assets/Scripts/Roguelite/Definitions/ZoneSequenceSO.cs
namespace DIG.Roguelite.Zones
{
    public enum ZoneSelectionMode : byte
    {
        Fixed = 0,            // Always use first entry
        WeightedRandom = 1,   // Seed-deterministic weighted pick
        PlayerChoice = 2,     // Present N options, player picks
        Conditional = 3       // First entry whose condition is met
    }

    [CreateAssetMenu(menuName = "DIG/Roguelite/Zone Sequence")]
    public class ZoneSequenceSO : ScriptableObject
    {
        public string SequenceName;

        [Tooltip("Ordered layers. Each layer = one zone slot in the run.")]
        public List<ZoneSequenceLayer> Layers;

        [Tooltip("If true, after the last layer the sequence loops back to LoopStartIndex with increased difficulty.")]
        public bool EnableLooping;

        [Tooltip("Layer index to loop back to (0-based). Only used if EnableLooping is true.")]
        public int LoopStartIndex;

        [Tooltip("Difficulty multiplier applied per loop iteration.")]
        public float LoopDifficultyMultiplier = 1.5f;
    }

    [Serializable]
    public class ZoneSequenceLayer
    {
        public string LayerName;
        public ZoneSelectionMode Mode;

        [Tooltip("For PlayerChoice mode: how many options to present.")]
        public int ChoiceCount = 2;

        public List<ZoneSequenceEntry> Entries;
    }

    [Serializable]
    public struct ZoneSequenceEntry
    {
        public ZoneDefinitionSO Zone;
        public float Weight;

        [Tooltip("For Conditional mode: minimum ascension level required. 0 = always available.")]
        public byte MinAscensionLevel;

        [Tooltip("For Conditional mode: minimum loop count required. 0 = first pass.")]
        public byte MinLoopCount;
    }
}
```

### Encounter Pool

```csharp
// File: Assets/Scripts/Roguelite/Definitions/EncounterPoolSO.cs
namespace DIG.Roguelite.Zones
{
    [CreateAssetMenu(menuName = "DIG/Roguelite/Encounter Pool")]
    public class EncounterPoolSO : ScriptableObject
    {
        public string PoolName;
        public List<EncounterPoolEntry> Entries;

        [Tooltip("Boss encounter profile for Boss/TriggerThenBoss zones. Null = use Entries.")]
        public EncounterProfileSO BossProfile;
    }

    [Serializable]
    public struct EncounterPoolEntry
    {
        [Tooltip("Enemy ghost prefab.")]
        public GameObject EnemyPrefab;

        [Tooltip("Display name for editor tooling and simulation.")]
        public string DisplayName;

        [Tooltip("Selection weight. Higher = more likely to be picked by the director.")]
        public float Weight;

        [Tooltip("Spawn credit cost. Director 'buys' spawns with its budget. " +
                 "Higher = spawned less often but represents a bigger threat.")]
        public int SpawnCost;

        [Tooltip("Minimum effective difficulty before this entry appears in the pool. " +
                 "0 = always available. Lets you gate strong enemies to later zones.")]
        public float MinDifficulty;

        [Tooltip("Maximum effective difficulty before this entry is removed from the pool. " +
                 "0 = no upper limit. Lets weaker enemies phase out.")]
        public float MaxDifficulty;

        [Tooltip("Can this entry spawn as an Elite variant?")]
        public bool CanBeElite;

        [Tooltip("Maximum concurrent alive count for this entry type. 0 = unlimited.")]
        public int MaxAlive;
    }
}
```

### Spawn Director Configuration

```csharp
// File: Assets/Scripts/Roguelite/Definitions/SpawnDirectorConfigSO.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Configures HOW the spawn director operates in a zone.
    /// Designers create different configs for different zone styles:
    ///
    ///   "Corridor Burst"   — high initial budget, zero regen. Spawns everything at once.
    ///   "Open World"       — low initial budget, steady regen. Escalates over time.
    ///   "Arena Survival"   — zero initial, fast regen. Starts slow, overwhelms eventually.
    ///   "Boss Room"        — zero budget (boss is spawned by EncounterState, not director).
    ///   "Rest Zone"        — zero budget, zero regen. No enemies.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Spawn Director Config")]
    public class SpawnDirectorConfigSO : ScriptableObject
    {
        [Header("Budget")]
        [Tooltip("Starting spawn credits when zone activates. " +
                 "High value = burst spawn on entry (corridor style). " +
                 "Zero = nothing spawns until credits accrue.")]
        public float InitialBudget = 100f;

        [Tooltip("Credits earned per second during Active phase. " +
                 "Zero = one-shot mode (everything spawns from InitialBudget). " +
                 "Positive = continuous spawning that escalates over time.")]
        public float CreditsPerSecond = 0f;

        [Tooltip("Multiplier applied to CreditsPerSecond based on time in zone. " +
                 "1.0 = linear. >1.0 = accelerating. Evaluated as: " +
                 "effectiveRate = CreditsPerSecond * (1 + timeInZone * Acceleration).")]
        public float Acceleration = 0f;

        [Tooltip("Maximum accumulated unspent credits. Prevents stockpiling. 0 = unlimited.")]
        public float MaxBudget = 500f;

        [Header("Spawn Rules")]
        [Tooltip("Minimum seconds between spawn attempts.")]
        public float MinSpawnInterval = 0.5f;

        [Tooltip("Maximum concurrent alive enemies from this director. 0 = unlimited. " +
                 "Director pauses spending when at cap (credits still accrue up to MaxBudget).")]
        public int MaxAliveEnemies = 40;

        [Tooltip("Minimum distance from any player to spawn. 0 = no minimum.")]
        public float MinSpawnDistance = 15f;

        [Tooltip("Maximum distance from nearest player to spawn. 0 = no maximum.")]
        public float MaxSpawnDistance = 80f;

        [Tooltip("Don't spawn enemies within this distance of player (prevents pop-in).")]
        public float NoSpawnRadius = 10f;

        [Header("Elite Spawning")]
        [Tooltip("Chance (0-1) that a spawn attempt produces an elite variant.")]
        [Range(0f, 1f)]
        public float EliteChance = 0.05f;

        [Tooltip("Credit cost multiplier for elite variants.")]
        public float EliteCostMultiplier = 3f;

        [Tooltip("Minimum effective difficulty before elites can spawn.")]
        public float EliteMinDifficulty = 2f;

        [Header("Difficulty Scaling")]
        [Tooltip("If true, difficulty increases CreditsPerSecond. " +
                 "False = difficulty only affects which pool entries are available.")]
        public bool DifficultyAffectsRate = true;

        [Tooltip("Multiplier for CreditsPerSecond per point of effective difficulty.")]
        public float DifficultyRateMultiplier = 0.5f;
    }
}
```

### Interactable Pool

```csharp
// File: Assets/Scripts/Roguelite/Definitions/InteractablePoolSO.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Weighted pool of interactable types for zone placement.
    /// The InteractableDirectorSystem picks from this pool; the game's
    /// IInteractableHandler does the actual spawning.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Interactable Pool")]
    public class InteractablePoolSO : ScriptableObject
    {
        public string PoolName;
        public List<InteractablePoolEntry> Entries;
    }

    [Serializable]
    public struct InteractablePoolEntry
    {
        [Tooltip("Identifier passed to IInteractableHandler. Game maps this to prefabs.")]
        public int InteractableTypeId;
        public string DisplayName;
        public float Weight;

        [Tooltip("Base run-currency cost. 0 = free. Negative = gives currency.")]
        public int BaseCost;

        [Tooltip("Cost scales with difficulty. Final = BaseCost * (1 + difficulty * CostScale).")]
        public float CostScale;

        [Tooltip("Maximum of this type per zone. 0 = unlimited.")]
        public int MaxPerZone;

        [Tooltip("Minimum effective difficulty for this to appear. 0 = always.")]
        public float MinDifficulty;
    }
}
```

### Zone State Component

```csharp
// File: Assets/Scripts/Roguelite/Components/ZoneState.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Runtime state for the current zone. Stored on the RunState entity.
    /// Read by SpawnDirectorSystem, ZoneClearDetectionSystem, ZoneUIBridgeSystem.
    /// </summary>
    public struct ZoneState : IComponentData
    {
        public int ZoneIndex;
        public int ZoneId;                    // ZoneDefinitionSO.ZoneId
        public ZoneType Type;
        public ZoneClearMode ClearMode;
        public float TimeInZone;              // Seconds since zone activated
        public float EffectiveDifficulty;     // Base curve * zone multiplier * ascension
        public int EnemiesSpawned;
        public int EnemiesAlive;
        public int EnemiesKilled;
        public float SpawnBudget;             // Current accumulated spawn credits
        public float SpawnTimer;              // Countdown to next spawn attempt
        public bool BossSpawned;              // For TriggerThenBoss: has boss been spawned?
        public bool ExitActivated;            // Player has interacted with zone exit
        public byte LoopCount;                // How many times the sequence has looped
        public bool IsCleared;
    }

    /// <summary>
    /// Enableable component on RunState entity. Game code enables this
    /// when the player activates the zone exit (teleporter, door, etc).
    /// Read by PlayerTriggeredCondition and TriggerThenBossCondition.
    /// </summary>
    public struct ZoneExitActivated : IComponentData, IEnableableComponent { }
}
```

### Spawn Request & Zone Clear API

```csharp
// File: Assets/Scripts/Roguelite/Components/SpawnRequest.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Transient entity created by SpawnDirectorSystem.
    /// Game's spawner system consumes these and instantiates actual enemies.
    /// Bridges framework orchestration → game instantiation.
    /// </summary>
    public struct SpawnRequest : IComponentData
    {
        public Entity Prefab;
        public float3 Position;
        public uint Seed;              // Per-spawn seed for deterministic variation
        public float Difficulty;       // Effective difficulty at spawn time
        public bool IsElite;
        public int PoolEntryIndex;     // Index into EncounterPoolSO.Entries (for tracking)
    }

    public struct SpawnRequestConsumed : ICleanupComponentData { }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Utility/ZoneClearAPI.cs
namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Static helper for game code to interact with zone clear state.
    /// Use when ZoneClearMode = Manual, or for game-specific triggers.
    /// </summary>
    public static class ZoneClearAPI
    {
        /// <summary>Signal that the player has activated the zone exit.</summary>
        public static void ActivateExit(EntityManager em, Entity runEntity)
            => em.SetComponentEnabled<ZoneExitActivated>(runEntity, true);

        /// <summary>Manually mark the zone as cleared (for Manual clear mode).</summary>
        public static void ForceZoneClear(EntityManager em, Entity runEntity)
        {
            var state = em.GetComponentData<ZoneState>(runEntity);
            state.IsCleared = true;
            em.SetComponentData(runEntity, state);
        }
    }
}
```

---

## Systems

| System | Group | World Filter | Burst | Purpose |
|--------|-------|--------------|-------|---------|
| `ZoneSequenceResolverSystem` | SimulationSystemGroup | Server\|Local | No | On `Preparation`: reads `ZoneSequenceSO`, uses seed to resolve zone order. Handles looping, conditional entries, weighted random. Stores resolved list in managed singleton |
| `ZoneTransitionSystem` | SimulationSystemGroup, UpdateAfter(RunLifecycleSystem) | Server\|Local | No | Manages `IZoneProvider` lifecycle (Initialize/IsReady/Activate/Deactivate). Creates `ZoneState` component. Teleports player. Calls `IInteractableHandler.PlaceInteractables()` on activation |
| `SpawnDirectorSystem` | SimulationSystemGroup, UpdateAfter(ZoneTransitionSystem) | Server\|Local | Partial | Reads `SpawnDirectorConfigSO` + `ZoneState`. Accrues credits per second. Selects enemies from `EncounterPoolSO` by weight (filtered by difficulty range). Creates `SpawnRequest` entities with position from `ISpawnPositionProvider`. Tracks alive count via EntityQuery. Burst-compiled budget math, managed spawn request creation |
| `DefaultSpawnRequestConsumerSystem` | SimulationSystemGroup, UpdateAfter(SpawnDirectorSystem) | Server\|Local | No | Converts `SpawnRequest` entities → `EnemySpawner` entities using existing spawner pipeline. Games with custom spawning can disable this and consume SpawnRequests directly |
| `ZoneClearDetectionSystem` | SimulationSystemGroup, OrderLast | Server\|Local | Partial | Instantiates `IZoneClearCondition` from `ZoneClearMode`. Polls each frame. On clear: sets `ZoneState.IsCleared`, awards RunCurrency, triggers reward phase, sets `RunPhase = ZoneTransition` |
| `InteractableDirectorSystem` | SimulationSystemGroup, UpdateAfter(ZoneTransitionSystem) | Server\|Local | No | On zone activation: reads `InteractablePoolSO`, selects items by weight within budget, calls `IInteractableHandler.PlaceInteractables()` with resolved positions and types |
| `ZoneUIBridgeSystem` | PresentationSystemGroup | Client\|Local | No | Reads `ZoneState`, pushes to `ZoneUIRegistry`: zone name, type, time, enemies alive/killed, clear progress, exit status |

### Spawn Director Flow (per frame)

```
1. Read ZoneState.TimeInZone, SpawnDirectorConfigSO
2. Calculate effective credits/sec:
     rate = CreditsPerSecond * (1 + TimeInZone * Acceleration)
     if DifficultyAffectsRate:
         rate *= (1 + EffectiveDifficulty * DifficultyRateMultiplier)
3. Accrue: SpawnBudget += rate * deltaTime (clamped to MaxBudget)
4. If SpawnTimer > 0: decrement, skip spawning
5. Query alive enemy count. If >= MaxAliveEnemies: skip (budget still accrues)
6. Filter EncounterPoolSO.Entries by:
     - MinDifficulty <= EffectiveDifficulty
     - MaxDifficulty == 0 || MaxDifficulty >= EffectiveDifficulty
     - MaxAlive == 0 || current alive of this type < MaxAlive
7. Weighted random selection (seeded). Check SpawnCost <= SpawnBudget
8. Roll elite chance. If elite: cost *= EliteCostMultiplier
9. Request position from ISpawnPositionProvider(playerPos, MinSpawnDistance, MaxSpawnDistance)
10. If position valid: create SpawnRequest entity, subtract cost, reset SpawnTimer
11. Repeat from 6 while budget remains and alive < cap
```

This flow supports all game styles:
- **Corridor**: InitialBudget=200, CreditsPerSecond=0 → spawns everything immediately, done
- **Open world**: InitialBudget=50, CreditsPerSecond=10, Acceleration=0.1 → starts light, escalates
- **Arena survival**: InitialBudget=0, CreditsPerSecond=20, Acceleration=0.3 → starts empty, overwhelms
- **Boss room**: InitialBudget=0, CreditsPerSecond=0 → director does nothing, boss from EncounterState
- **Rest/Shop**: InitialBudget=0, CreditsPerSecond=0 → no enemies

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Credit-based spawn director, not fixed waves | Supports corridor (burst budget), open-world (continuous accrual), arena (accelerating), and passive (zero budget) with ONE system and different SpawnDirectorConfigSO presets |
| ISpawnPositionProvider separate from IZoneProvider | Zone geometry provider shouldn't know about spawn rules. A navmesh-sampling provider works with any zone geometry. Games can mix: hand-placed markers for corridors, navmesh sampling for open worlds |
| ZoneClearMode enum + IZoneClearCondition interface | Enum covers 95% of cases with zero code. Interface handles the remaining 5%. CompositeClearCondition lets designers combine built-ins |
| ZoneActivationResult struct, not single float3 | Big 3D zones need spawn points, interactable nodes, exit position, playable bounds. Corridor games just fill PlayerSpawnPosition and leave the rest null |
| SpawnRequest transient entities, not direct instantiation | Framework doesn't instantiate enemies (doesn't know the game's prefab/pooling/setup pipeline). Creates data-only requests. Game consumes them however it wants |
| ExtensionData is ScriptableObject[] array | Games often need multiple extension types (visual theme + gameplay params + audio profile). Single slot is too restrictive |
| ZoneSizeHint enum on ZoneDefinitionSO | Lets the spawn director and interactable director scale density without knowing actual geometry |
| Looping support in ZoneSequenceSO | Risk of Rain 2 loops stages at increasing difficulty. Slay the Spire doesn't loop. Framework supports both — designer toggle |
| ZoneExitActivated as IEnableableComponent | Player-triggered exit is cross-system. Enableable component is the cheapest signal mechanism. Follows PermadeathSignal pattern from 23.1 |
| DefaultSpawnRequestConsumerSystem as opt-out | Simple games get working spawning out of the box. Complex games disable it and consume SpawnRequests directly for pooling, custom setup, etc |

---

## Integration With Existing Systems

| Existing System | How 23.3 Uses It |
|-----------------|------------------|
| `EnemySpawner` + `EnemySpawnRequest` | `DefaultSpawnRequestConsumerSystem` converts `SpawnRequest` → `EnemySpawner` entities. Games can bypass this and consume `SpawnRequest` directly |
| `EncounterState` + `EncounterTriggerDefinition` | Boss zones (`BossKill` / `TriggerThenBoss`) create `EncounterState` entities from existing `EncounterProfileSO`. Zero new boss code |
| `LootTableSO` | `ZoneDefinitionSO.BonusLootTable` resolves through existing loot pipeline on zone clear |
| `TeleportSystem` | Player teleported to `ZoneActivationResult.PlayerSpawnPosition` |
| `RuntimeDifficultyScale` (23.4) | `SpawnDirectorSystem` reads this for effective difficulty. Modifiers affect spawn rates, elite chance, and pool entry filtering |
| `RunCurrency` (23.1) | `ZoneClearDetectionSystem` awards `RunCurrencyPerZoneClear` from `RunConfigSO` |
| `RewardPoolSO` (23.5) | `ZoneClearDetectionSystem` triggers reward generation from `ZoneDefinitionSO.ClearRewardPool` |

---

## Example Configurations

### Corridor Room-Clear (Hades / Dead Cells)
```
SpawnDirectorConfigSO "Corridor Burst":
  InitialBudget = 200, CreditsPerSecond = 0, MaxAliveEnemies = 0

ZoneDefinitionSO:
  Type = Combat, ClearMode = AllEnemiesDead, SizeHint = Small, InteractableBudget = 0

ZoneSequenceSO: Fixed layers, no looping
```

### Open-World Exploration (Risk of Rain 2)
```
SpawnDirectorConfigSO "Open World Escalation":
  InitialBudget = 50, CreditsPerSecond = 12, Acceleration = 0.08
  MaxBudget = 400, MaxAliveEnemies = 40
  MinSpawnDistance = 20, MaxSpawnDistance = 80
  EliteChance = 0.05, DifficultyAffectsRate = true

ZoneDefinitionSO:
  Type = Exploration, ClearMode = TriggerThenBoss, SizeHint = Massive
  InteractableBudget = 15, InteractablePool = chests/shrines/equipment

ZoneSequenceSO: WeightedRandom layers, looping enabled, LoopDifficultyMultiplier = 1.5
```

### Arena Survival (Vampire Survivors)
```
SpawnDirectorConfigSO "Arena Swarm":
  InitialBudget = 0, CreditsPerSecond = 25, Acceleration = 0.15
  MaxBudget = 800, MaxAliveEnemies = 100

ZoneDefinitionSO:
  Type = Arena, ClearMode = TimerSurvival, SurvivalTimer = 300, SizeHint = Large

ZoneSequenceSO: Single layer, no looping
```

### Boss Arena
```
SpawnDirectorConfigSO "Boss Room":
  InitialBudget = 0, CreditsPerSecond = 0

ZoneDefinitionSO:
  Type = Boss, ClearMode = BossKill, EncounterPool.BossProfile = existing profile
```

---

## Performance

- `SpawnDirectorSystem`: Burst-compiled budget math. Managed `ISpawnPositionProvider` call only when actually spawning. One `EntityQuery.CalculateEntityCount()` per frame for alive count. Early-exit when budget < cheapest entry cost
- `ZoneClearDetectionSystem`: One managed `IZoneClearCondition.IsCleared()` call per frame. `AllEnemiesDeadCondition` reuses the director's alive count. Early-exit via `ZoneState.IsCleared` flag
- `ZoneTransitionSystem`: Runs once per zone transition. `IsReady` polling is one managed call per frame during loading only
- `InteractableDirectorSystem`: Runs once on zone activation. Not per-frame
- Spawn position sampling: `ISpawnPositionProvider.TryGetSpawnPosition()` called only when budget available AND alive < cap. Typically 0-3 calls per frame in steady state
- `MinSpawnInterval` prevents spawning more than 1/interval enemies per second

---

## File Manifest

| File | Type | Status |
|------|------|--------|
| `Assets/Scripts/Roguelite/Interfaces/IZoneProvider.cs` | Interface | **NEW** |
| `Assets/Scripts/Roguelite/Interfaces/IZoneClearCondition.cs` | Interface + built-in impls | **NEW** |
| `Assets/Scripts/Roguelite/Interfaces/ISpawnPositionProvider.cs` | Interface | **NEW** |
| `Assets/Scripts/Roguelite/Interfaces/IInteractableHandler.cs` | Interface | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/ZoneDefinitionSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/ZoneSequenceSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/EncounterPoolSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/SpawnDirectorConfigSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/InteractablePoolSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Components/ZoneState.cs` | IComponentData | **NEW** |
| `Assets/Scripts/Roguelite/Components/SpawnRequest.cs` | IComponentData (transient) | **NEW** |
| `Assets/Scripts/Roguelite/Utility/ZoneClearAPI.cs` | Static helper | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ZoneSequenceResolverSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ZoneTransitionSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/SpawnDirectorSystem.cs` | SystemBase (partial Burst) | **NEW** |
| `Assets/Scripts/Roguelite/Systems/DefaultSpawnRequestConsumerSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ZoneClearDetectionSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/InteractableDirectorSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ZoneUIBridgeSystem.cs` | SystemBase | **NEW** |

---

## Verification

1. **IZoneProvider contract**: Mock provider receives correct seed, index, definition. IsReady polling works. All ZoneActivationResult fields passed correctly
2. **Zone sequence**: WeightedRandom deterministic. PlayerChoice presents correct options. Conditional entries gated by ascension/loop. Looping increments difficulty
3. **Spawn director — burst mode**: InitialBudget=200, CreditsPerSecond=0 → spawns all immediately, stops. Same seed = same spawns
4. **Spawn director — continuous mode**: CreditsPerSecond=10 → enemies spawn steadily. Acceleration increases rate. MaxAliveEnemies cap respected. Same seed = same sequence
5. **Spawn director — difficulty filtering**: Entry with MinDifficulty=3.0 absent at difficulty 2.0. Present above 3.0
6. **Elite spawning**: EliteChance respected. Cost multiplier applied. Gated by EliteMinDifficulty. Deterministic from seed
7. **Zone clear — AllEnemiesDead**: Detects zero alive after all spawning. Awards currency. Transitions phase
8. **Zone clear — PlayerTriggered**: ZoneExitActivated triggers clear. Enemies still alive
9. **Zone clear — TriggerThenBoss**: Exit activation spawns boss. Boss death clears zone. Two-phase confirmed
10. **Zone clear — TimerSurvival**: Clears on timer expiry regardless of enemies
11. **Zone clear — Manual**: Game calls ZoneClearAPI.ForceZoneClear() when ready
12. **Interactables**: InteractableDirectorSystem calls handler with correct nodes, seed, difficulty. MaxPerZone enforced
13. **Spawn positions**: ISpawnPositionProvider called with correct distance constraints. False = spawn deferred
14. **Extension data**: Game-specific SOs attached via ExtensionData[], castable in providers
15. **Boss zones**: BossProfile creates EncounterState. Director produces zero spawns
16. **DefaultSpawnRequestConsumer**: Converts SpawnRequest → EnemySpawner entities. Disabling it doesn't break other systems
17. **Performance**: 40+ alive enemies with continuous spawning holds 60fps. No per-frame allocations in steady state
