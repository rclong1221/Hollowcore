# EPIC 8.4: Hunter Encounters

**Status**: Planning
**Epic**: EPIC 8 — Trace (Global Pressure Meter)
**Priority**: Medium — Key consequence of high Trace
**Dependencies**: EPIC 8.1 (TraceState, TraceThresholdSystem); Framework: AI/, Combat/, EPIC 4 (District Graph)

---

## Overview

Hunters are elite enemies that appear exclusively at Trace 2+. They are the teeth of the Trace system -- a tangible, dangerous consequence of accumulated heat. Each district has its own hunter variant with unique tracking behavior. Hunters scale with Trace level: solo at Trace 2, pairs at Trace 3, squads at Trace 4+. Defeating hunters is one of the few ways to reduce Trace, dropping items that erase records. Hunters persist within a district visit and actively seek the player across zones.

---

## Component Definitions

### HunterTag (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/HunterComponents.cs
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Marker identifying an enemy entity as a Trace Hunter.
    /// Hunters have special AI behavior and drop Trace-reducing loot.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct HunterTag : IComponentData { }

    /// <summary>
    /// Hunter-specific state beyond standard AI components.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct HunterState : IComponentData
    {
        /// <summary>Trace level when this hunter was spawned (determines scaling tier).</summary>
        [GhostField] public int SpawnTraceLevel;

        /// <summary>District variant ID from HunterDefinitionSO.</summary>
        [GhostField] public int VariantId;

        /// <summary>
        /// Seconds since last player detection.
        /// Hunters have extended memory compared to standard enemies.
        /// </summary>
        [GhostField(Quantization = 10)] public float TimeSincePlayerSeen;

        /// <summary>Hunter tracking persistence (seconds before losing trail). Default 120s.</summary>
        public float TrackingMemoryDuration;

        /// <summary>True if this hunter is part of a squad (Trace 4+ spawns).</summary>
        [GhostField] public bool IsSquadMember;

        /// <summary>Entity reference to squad leader (for coordinated behavior).</summary>
        [GhostField] public Entity SquadLeader;
    }

    /// <summary>
    /// Scaling modifiers applied to a hunter based on Trace level at spawn time.
    /// Layered on top of base HunterDefinitionSO stats.
    /// </summary>
    public struct HunterScaling : IComponentData
    {
        /// <summary>Health multiplier (1.0 at Trace 2, scales up).</summary>
        public float HealthMultiplier;

        /// <summary>Damage multiplier.</summary>
        public float DamageMultiplier;

        /// <summary>Movement speed multiplier.</summary>
        public float SpeedMultiplier;

        /// <summary>Detection range multiplier (hunters have wider vision at high Trace).</summary>
        public float DetectionMultiplier;
    }
}
```

### HunterSpawnRequest (IComponentData)

```csharp
// File: Assets/Scripts/Trace/Components/HunterComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Transient entity requesting hunter spawn. Created by HunterSpawnSystem,
    /// consumed by HunterSpawnExecutionSystem.
    /// </summary>
    public struct HunterSpawnRequest : IComponentData
    {
        /// <summary>District variant to spawn.</summary>
        public int VariantId;

        /// <summary>Number of hunters to spawn (1 at Trace 2, 2 at Trace 3, 3+ at Trace 4).</summary>
        public int Count;

        /// <summary>Current Trace level for scaling calculation.</summary>
        public int TraceLevel;
    }

    public struct HunterSpawnCleanup : ICleanupComponentData { }
}
```

### HunterLootTable (IBufferElementData)

```csharp
// File: Assets/Scripts/Trace/Components/HunterComponents.cs (continued)
namespace Hollowcore.Trace
{
    /// <summary>
    /// Loot entries specific to hunters. Always includes at least one Trace-reducing item.
    /// Buffer on hunter entities, populated from HunterDefinitionSO during spawn.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct HunterLootEntry : IBufferElementData
    {
        /// <summary>Item definition ID to drop.</summary>
        public int ItemId;

        /// <summary>Drop probability (0-1).</summary>
        public float DropChance;

        /// <summary>If true, this item reduces Trace when used/auto-applied.</summary>
        public bool IsTraceReducer;

        /// <summary>Trace reduction amount if IsTraceReducer (usually 1).</summary>
        public int TraceReduction;
    }
}
```

---

## ScriptableObject Definitions

### HunterDefinitionSO

```csharp
// File: Assets/Scripts/Trace/Definitions/HunterDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Trace.Definitions
{
    [CreateAssetMenu(fileName = "NewHunter", menuName = "Hollowcore/Trace/Hunter Definition")]
    public class HunterDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int VariantId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Portrait;

        [Header("District")]
        [Tooltip("Which district this hunter variant spawns in (-1 = universal)")]
        public int DistrictId = -1;

        [Header("Base Stats")]
        public float BaseHealth = 500f;
        public float BaseDamage = 40f;
        public float BaseMoveSpeed = 5f;
        public float BaseDetectionRange = 25f;
        public float TrackingMemoryDuration = 120f;

        [Header("Scaling Per Trace Level Above 2")]
        public float HealthScalePerTrace = 0.25f;   // +25% per level above 2
        public float DamageScalePerTrace = 0.15f;    // +15% per level above 2
        public float SpeedScalePerTrace = 0.05f;     // +5% per level above 2
        public float DetectionScalePerTrace = 0.10f;  // +10% per level above 2

        [Header("Spawn")]
        public GameObject HunterPrefab;

        [Header("Loot")]
        [Tooltip("Guaranteed Trace reduction on kill")]
        public int TraceReductionOnKill = 1;
        public LootEntry[] AdditionalLoot;

        [System.Serializable]
        public struct LootEntry
        {
            public int ItemId;
            [Range(0f, 1f)] public float DropChance;
        }

        [Header("Behavior")]
        [Tooltip("Unique tracking behavior type")]
        public HunterBehaviorType BehaviorType;
    }

    public enum HunterBehaviorType : byte
    {
        /// <summary>Standard pursuit. Tracks last known position, searches area.</summary>
        Pursuer = 0,
        /// <summary>Sets up ambush at predicted player path. Lattice district variant.</summary>
        Ambusher = 1,
        /// <summary>Hunts in coordinated pairs/squads. Industrial district variant.</summary>
        PackHunter = 2,
        /// <summary>Stealth approach, high burst damage. Quarantine district variant.</summary>
        Stalker = 3,
        /// <summary>Relentless, ignores obstacles. Burn district variant.</summary>
        Juggernaut = 4
    }
}
```

---

## Systems

### HunterSpawnSystem

```csharp
// File: Assets/Scripts/Trace/Systems/HunterSpawnSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: TraceThresholdSystem
//
// Reads: TraceState, TraceConfig, HunterSpawnCooldown (singleton)
// Writes: Creates HunterSpawnRequest entities
//
// Each frame:
//   1. If TraceState.CurrentTrace < 2: early out (no hunters below Trace 2)
//   2. Decrement HunterSpawnCooldown timer
//   3. If cooldown expired AND fewer active hunters than Trace-based cap:
//      a. Determine spawn count:
//         - Trace 2: 1 hunter
//         - Trace 3: 2 hunters
//         - Trace 4+: 3 hunters (squad, set IsSquadMember=true)
//      b. Select variant by current district (fallback to universal)
//      c. Create HunterSpawnRequest entity
//      d. Reset cooldown (base / TraceConfig.HunterSpawnRatePerTrace)
//   4. Active hunter cap: Trace * 2 (prevents flooding)
```

### HunterSpawnExecutionSystem

```csharp
// File: Assets/Scripts/Trace/Systems/HunterSpawnExecutionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: HunterSpawnSystem
//
// Reads: HunterSpawnRequest
// Writes: Instantiates hunter prefab entities with HunterTag, HunterState, HunterScaling, HunterLootEntry
//
// For each HunterSpawnRequest:
//   1. Load HunterDefinitionSO by VariantId
//   2. Calculate scaling:
//      HealthMultiplier = 1 + (TraceLevel - 2) * HealthScalePerTrace
//      DamageMultiplier = 1 + (TraceLevel - 2) * DamageScalePerTrace
//      SpeedMultiplier  = 1 + (TraceLevel - 2) * SpeedScalePerTrace
//      DetectionMultiplier = 1 + (TraceLevel - 2) * DetectionScalePerTrace
//   3. For i in 0..Count:
//      a. Instantiate HunterPrefab
//      b. Add HunterTag, HunterState (SpawnTraceLevel, VariantId, TrackingMemoryDuration)
//      c. Add HunterScaling with calculated multipliers
//      d. Apply scaling to Health (BaseHealth * HealthMultiplier), AttackStats, etc.
//      e. Populate HunterLootEntry buffer (guaranteed TraceReducer + additional loot)
//      f. Set spawn position: random valid NavMesh point within detection range of player
//      g. If Count >= 3: set IsSquadMember=true, first spawn is SquadLeader
//   4. Destroy HunterSpawnRequest entities
```

### HunterTrackingSystem

```csharp
// File: Assets/Scripts/Trace/Systems/HunterTrackingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Extends standard AI detection with hunter-specific tracking persistence.
//
// For each entity with HunterTag + HunterState + AIState:
//   1. If player detected this frame: reset TimeSincePlayerSeen to 0
//   2. Else: TimeSincePlayerSeen += deltaTime
//   3. If TimeSincePlayerSeen < TrackingMemoryDuration:
//      - Override AI target to last known player position
//      - AI remains in COMBAT alert (does not de-escalate)
//   4. If TimeSincePlayerSeen >= TrackingMemoryDuration:
//      - Allow normal AI de-escalation
//      - Enter patrol/search pattern around last known position
//   5. BehaviorType specializations:
//      - Ambusher: predict player direction, move to intercept point
//      - PackHunter: maintain formation relative to SquadLeader
//      - Stalker: approach from behind, stay out of direct detection
//      - Juggernaut: direct path, ignore cover
```

### HunterDeathSystem

```csharp
// File: Assets/Scripts/Trace/Systems/HunterDeathSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DeathTransitionSystem (framework)
//
// For each entity with HunterTag + DeathState (newly dead):
//   1. Read HunterLootEntry buffer
//   2. For each entry: roll DropChance, spawn loot if successful
//   3. For guaranteed TraceReducer entries:
//      a. Call TraceSinkAPI.ReduceTrace(TraceReduction, HunterLoot, hunterName)
//      b. OR spawn consumable item that reduces Trace when picked up
//   4. Decrement active hunter count for spawn cap tracking
```

---

## Setup Guide

1. Add HunterComponents to `Assets/Scripts/Trace/Components/`
2. Create `Assets/Scripts/Trace/Definitions/` and add `HunterDefinitionSO.cs`
3. Create hunter definition assets at `Assets/Data/Trace/Hunters/`:
   - `Hunter_Pursuer.asset` (default/universal variant)
   - `Hunter_Ambusher.asset` (Lattice district)
   - `Hunter_PackHunter.asset` (Industrial district)
   - `Hunter_Stalker.asset` (Quarantine district)
   - `Hunter_Juggernaut.asset` (Burn district)
4. Create hunter prefabs at `Assets/Prefabs/Enemies/Hunters/` with:
   - Standard enemy components (Health, DamageableAuthoring, AI, etc.)
   - HunterTag, HunterState, HunterScaling added by HunterSpawnExecutionSystem at runtime
5. Configure per-district variant mapping in a `HunterVariantDatabase` ScriptableObject
6. Set HunterSpawnRatePerTrace in TraceConfig (EPIC 8.1): default 1.0
7. Verify HunterSpawnSystem respects the active hunter cap

---

## Verification

- [ ] No hunters spawn at Trace 0-1
- [ ] Single hunter spawns at Trace 2
- [ ] Two hunters spawn at Trace 3
- [ ] Hunter squad (3+) spawns at Trace 4+, with SquadLeader set
- [ ] Correct district variant selected for current district
- [ ] Universal variant used as fallback for unmapped districts
- [ ] HunterScaling correctly multiplies base stats by Trace level
- [ ] Hunters track player for TrackingMemoryDuration seconds after losing sight
- [ ] Hunters do not despawn on zone transition within a district
- [ ] Defeating a hunter reduces Trace (via loot or direct TraceSinkAPI call)
- [ ] Active hunter cap prevents spawning beyond Trace * 2 limit
- [ ] HunterSpawnCooldown prevents rapid sequential spawns
- [ ] Each behavior type exhibits its specialized tracking pattern

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Trace/Components/HunterDefinitionBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Trace
{
    /// <summary>
    /// Blob representation of HunterDefinitionSO for Burst-compatible reads at spawn time.
    /// Avoids managed SO lookup in HunterSpawnExecutionSystem hot path.
    /// </summary>
    public struct HunterDefinitionBlob
    {
        public int VariantId;
        public float BaseHealth;
        public float BaseDamage;
        public float BaseMoveSpeed;
        public float BaseDetectionRange;
        public float TrackingMemoryDuration;

        // Scaling per Trace level above 2
        public float HealthScalePerTrace;
        public float DamageScalePerTrace;
        public float SpeedScalePerTrace;
        public float DetectionScalePerTrace;

        public byte BehaviorType; // HunterBehaviorType as byte
        public int TraceReductionOnKill;

        // Loot table
        public BlobArray<HunterLootBlobEntry> LootEntries;
    }

    public struct HunterLootBlobEntry
    {
        public int ItemId;
        public float DropChance;
        public bool IsTraceReducer;
        public int TraceReduction;
    }

    /// <summary>
    /// All hunter variants indexed by VariantId for O(1) lookup.
    /// Singleton blob on the TraceConfig entity.
    /// </summary>
    public struct HunterDefinitionDatabase
    {
        public BlobArray<HunterDefinitionBlob> Variants;
    }

    public struct HunterDefinitionDatabaseRef : IComponentData
    {
        public BlobAssetReference<HunterDefinitionDatabase> Value;
    }
}
```

```csharp
// File: Assets/Scripts/Trace/Definitions/HunterDefinitionSO.cs (append BakeToBlob method)
namespace Hollowcore.Trace.Definitions
{
    // Add to existing HunterDefinitionSO class:
    //
    // public static BlobAssetReference<HunterDefinitionDatabase> BakeToBlob(
    //     HunterDefinitionSO[] definitions, IBaker baker)
    // {
    //     var builder = new BlobBuilder(Allocator.Temp);
    //     ref var root = ref builder.ConstructRoot<HunterDefinitionDatabase>();
    //     var variants = builder.Allocate(ref root.Variants, definitions.Length);
    //     for (int i = 0; i < definitions.Length; i++)
    //     {
    //         var def = definitions[i];
    //         variants[i] = new HunterDefinitionBlob
    //         {
    //             VariantId = def.VariantId,
    //             BaseHealth = def.BaseHealth,
    //             BaseDamage = def.BaseDamage,
    //             BaseMoveSpeed = def.BaseMoveSpeed,
    //             BaseDetectionRange = def.BaseDetectionRange,
    //             TrackingMemoryDuration = def.TrackingMemoryDuration,
    //             HealthScalePerTrace = def.HealthScalePerTrace,
    //             DamageScalePerTrace = def.DamageScalePerTrace,
    //             SpeedScalePerTrace = def.SpeedScalePerTrace,
    //             DetectionScalePerTrace = def.DetectionScalePerTrace,
    //             BehaviorType = (byte)def.BehaviorType,
    //             TraceReductionOnKill = def.TraceReductionOnKill,
    //         };
    //         // Loot entries
    //         var lootCount = 1 + (def.AdditionalLoot?.Length ?? 0);
    //         var loot = builder.Allocate(ref variants[i].LootEntries, lootCount);
    //         loot[0] = new HunterLootBlobEntry
    //         {
    //             ItemId = -1, // Trace reducer (no item, direct API call)
    //             DropChance = 1f,
    //             IsTraceReducer = true,
    //             TraceReduction = def.TraceReductionOnKill,
    //         };
    //         for (int j = 0; j < (def.AdditionalLoot?.Length ?? 0); j++)
    //         {
    //             loot[j + 1] = new HunterLootBlobEntry
    //             {
    //                 ItemId = def.AdditionalLoot[j].ItemId,
    //                 DropChance = def.AdditionalLoot[j].DropChance,
    //                 IsTraceReducer = false,
    //                 TraceReduction = 0,
    //             };
    //         }
    //     }
    //     var result = builder.CreateBlobAssetReference<HunterDefinitionDatabase>(Allocator.Persistent);
    //     builder.Dispose();
    //     return result;
    // }
}
```

---

## Validation

```csharp
// File: Assets/Editor/TraceWorkstation/HunterValidation.cs
namespace Hollowcore.Trace.Editor
{
    /// <summary>
    /// Validates HunterDefinitionSO assets and spawn configuration.
    /// </summary>
    // Rules:
    //   1. Each HunterDefinitionSO.VariantId must be unique across all hunter assets
    //   2. DistrictId must reference a valid district in the district graph (-1 = universal is valid)
    //   3. BaseHealth, BaseDamage, BaseMoveSpeed, BaseDetectionRange must all be > 0
    //   4. TrackingMemoryDuration must be > 0 (recommend >= 30s)
    //   5. Scale factors (HealthScalePerTrace etc.) must be >= 0 (0 = no scaling, acceptable)
    //   6. HunterPrefab must not be null
    //   7. TraceReductionOnKill must be >= 1 (hunters must provide meaningful sink)
    //   8. At least one universal variant (DistrictId=-1) must exist as fallback
    //   9. Spawn threshold validation: hunter spawn Trace threshold (2) must be < MaxTrace
    //  10. Each BehaviorType should have at least one variant assigned
    //
    // Cross-validation: hunter active cap (Trace * 2) must be achievable
    //   given HunterSpawnRatePerTrace and spawn cooldown
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Trace/Debug/HunterLiveTuning.cs
namespace Hollowcore.Trace.Debug
{
    /// <summary>
    /// Runtime-editable hunter spawn parameters for playtesting.
    /// </summary>
    // Tunable fields:
    //   - HunterSpawnThreshold (int, min 1, max 6) — Trace level hunters start spawning
    //   - HunterSpawnRatePerTrace (float, min 0.1, max 5.0) — spawn cooldown divisor
    //   - HunterActiveCapMultiplier (float, min 0.5, max 4.0) — multiplier on Trace * 2 cap
    //   - HunterScalingOverride (float4: health/damage/speed/detection multipliers)
    //   - ForceSpawnVariantId (int, -1 = normal) — force a specific variant for testing
    //   - ForceSpawnCount (int, 0 = normal) — override spawn count regardless of Trace
    //
    // Pattern: static HunterLiveTuningOverrides, checked by HunterSpawnSystem each frame.
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Trace/Debug/HunterDebugOverlay.cs
namespace Hollowcore.Trace.Debug
{
    /// <summary>
    /// In-game debug overlay for hunter encounters.
    /// </summary>
    // Overlay elements:
    //   1. Hunter proximity indicators: directional arrows pointing toward active hunters
    //      - Color: red (pursuing), yellow (searching), blue (patrolling)
    //      - Distance label in meters
    //   2. Active hunter roster: list showing each living hunter
    //      - VariantId, BehaviorType, SpawnTraceLevel, current health %, TimeSincePlayerSeen
    //      - Squad grouping indicated with bracket
    //   3. Spawn state: cooldown timer, active count / cap, next spawn variant
    //   4. World-space gizmos (Scene view): detection radius sphere, tracking memory cone,
    //      last-known-position marker, predicted intercept point (Ambusher)
    //
    // Implementation: MonoBehaviour overlay + TraceDebugBridgeSystem reads HunterTag entities
}
```
