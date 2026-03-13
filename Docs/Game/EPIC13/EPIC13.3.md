# EPIC 13.3: Faction & Enemy Pipeline

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: Critical — Enemies are the primary content interaction
**Dependencies**: 13.1 (DistrictDefinitionSO, FactionDefinitionSO), Framework: AI/ (AIBrain, EncounterPoolSO, AbilityProfileSO), Loot/ (LootTableSO, DeathLootSystem), Roguelite/ (SpawnDirectorSystem)

---

## Overview

Each district contains 4 enemy factions assigned to zones. This sub-epic defines the `FactionDefinitionSO` data model, the per-faction enemy prefab pipeline (model + animations + AI behavior + loot table + rippable limbs), Strife mutation overrides that modify factions per-run, and Front phase scaling that reinforces or promotes faction members as the Front advances. The framework's `SpawnDirectorSystem` + `EncounterPoolSO` handle runtime spawning; this epic provides the district-specific content those systems consume.

---

## Component Definitions

### FactionId Enum

```csharp
// File: Assets/Scripts/District/Definitions/FactionEnums.cs
namespace Hollowcore.District
{
    /// <summary>
    /// Faction identity. Each district has 4 factions; IDs are globally unique across all districts.
    /// Range: district * 10 + faction index (e.g., Necrospire factions = 10-13).
    /// </summary>
    public enum FactionId : ushort
    {
        // Necrospire (District 1)
        MourningCollective = 10,
        RecursiveSpecters = 11,
        ArchiveWardens = 12,
        TheInheritors = 13,

        // The Burn (District 6)
        SlagWalkers = 60,
        WasteManagement = 61,
        TheAshborn = 62,
        ScrapHives = 63,

        // The Lattice (District 8)
        TheClimbers = 80,
        CollapseEngineers = 81,
        ApexDwellers = 82,
        TheFoundation = 83,
        // ... remaining districts follow same pattern
    }

    public enum EnemyTier : byte
    {
        Common = 0,
        Elite = 1,
        Special = 2,
        Miniboss = 3,
        Boss = 4
    }

    public enum BehaviorAggression : byte
    {
        Passive = 0,    // Only attacks when attacked
        Defensive = 1,  // Patrols, engages at short range
        Aggressive = 2, // Actively hunts, wide detection
        Berserker = 3   // Charges on sight, no retreat
    }
}
```

### FactionDefinitionSO

```csharp
// File: Assets/Scripts/District/Definitions/FactionDefinitionSO.cs
using System;
using UnityEngine;

namespace Hollowcore.District
{
    /// <summary>
    /// Defines one faction within a district. 4 per district.
    /// Contains enemy prefab references, behavior profile, and zone affinity.
    /// </summary>
    [CreateAssetMenu(fileName = "FactionDefinition", menuName = "Hollowcore/District/Faction Definition")]
    public class FactionDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public FactionId Id;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public Color FactionColor;

        [Header("Enemy Roster")]
        [Tooltip("3-5 enemy types per faction. Ordered by tier (common first).")]
        public FactionEnemyEntry[] EnemyRoster;

        [Header("Behavior")]
        public BehaviorAggression BaseAggression;
        [Tooltip("AI behavior profile overrides applied to all faction members.")]
        public ScriptableObject BehaviorProfileOverride;
        [Tooltip("Patrol pattern: 0=Stationary, 1=Waypoint, 2=Roaming, 3=Swarm")]
        public byte PatrolPattern;
        [Tooltip("Alarm radius: how far faction members call for help.")]
        public float AlarmRadius = 15f;

        [Header("Zone Affinity")]
        [Tooltip("Zone types where this faction spawns. Empty = all zone types.")]
        public DIG.Roguelite.Zones.ZoneType[] PreferredZoneTypes;

        [Header("Front Scaling")]
        [Tooltip("Encounter pool overrides per Front phase (index 0-3). Null = no change.")]
        public DIG.Roguelite.Zones.EncounterPoolSO[] FrontPhaseOverrides = new DIG.Roguelite.Zones.EncounterPoolSO[4];

        [Header("Strife")]
        [Tooltip("Mutation overrides applied when specific Strife cards are active.")]
        public StrifeMutationEntry[] StrifeMutations;
    }

    [Serializable]
    public struct FactionEnemyEntry
    {
        public GameObject EnemyPrefab;
        public string DisplayName;
        public EnemyTier Tier;
        [Tooltip("Spawn weight within faction (higher = more common).")]
        public float SpawnWeight;
        [Tooltip("Spawn cost for the director budget system.")]
        public int SpawnCost;
        [Tooltip("Loot table for this enemy type.")]
        public ScriptableObject LootTable;
        [Tooltip("Can this enemy's limbs be ripped (EPIC 1)?")]
        public bool HasRippableLimbs;
        [Tooltip("Limb definitions available from ripping this enemy.")]
        public ScriptableObject[] RippableLimbDefinitions;
    }

    [Serializable]
    public struct StrifeMutationEntry
    {
        [Tooltip("Strife card ID that triggers this mutation.")]
        public int StrifeCardId;
        [Tooltip("Encounter pool override when this Strife is active.")]
        public DIG.Roguelite.Zones.EncounterPoolSO MutatedPool;
        [Tooltip("Stat multipliers: [0]=Health, [1]=Damage, [2]=Speed")]
        public float[] StatMultipliers;
        [Tooltip("Additional behavior override for this mutation.")]
        public ScriptableObject BehaviorOverride;
    }
}
```

### FactionAssignment (IComponentData)

```csharp
// File: Assets/Scripts/District/Components/FactionComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.District
{
    /// <summary>
    /// Stamped onto every spawned enemy entity. Identifies which faction and district
    /// this enemy belongs to. Read by aggro social systems, loot, and Front scaling.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FactionAssignment : IComponentData
    {
        [GhostField] public ushort FactionId;
        [GhostField] public byte DistrictId;
        [GhostField] public byte EnemyTier;
    }

    /// <summary>
    /// Applied to enemy entities when Front phase advances and their stats are scaled.
    /// Prevents double-application of phase scaling.
    /// </summary>
    public struct FrontPhaseScaled : IComponentData
    {
        public byte AppliedPhase;
    }
}
```

---

## Systems

### FactionEncounterPoolResolverSystem

```csharp
// File: Assets/Scripts/District/Systems/FactionEncounterPoolResolverSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: SpawnDirectorSystem (framework)
//
// Reads: DistrictState, ZoneState, FactionDefinitionSO (resolved from registry)
// Writes: Active EncounterPoolSO on the framework SpawnDirectorSystem
//
// Each zone transition:
//   1. Read ZoneState.ZoneIndex → ZoneGraphBufferEntry.PrimaryFactionIndex
//   2. Resolve FactionDefinitionSO from DistrictDefinitionSO.Factions[factionIndex]
//   3. Check DistrictState.FrontPhase → use FrontPhaseOverrides[phase] if non-null
//   4. Check active Strife cards → apply StrifeMutation override if matching
//   5. Push resolved EncounterPoolSO to framework spawn director
```

### FactionSpawnStampSystem

```csharp
// File: Assets/Scripts/District/Systems/FactionSpawnStampSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: AddSpawnSystem (framework AI/)
//
// Reads: Newly spawned enemy entities (SpawnRequest consumed), DistrictState, ZoneState
// Writes: FactionAssignment on each new enemy entity
//
// For each newly spawned enemy:
//   1. Resolve faction from current zone's PrimaryFactionIndex
//   2. Determine EnemyTier from the EncounterPoolEntry that spawned it
//   3. Stamp FactionAssignment { FactionId, DistrictId, EnemyTier }
```

### FrontPhaseScalingSystem

```csharp
// File: Assets/Scripts/District/Systems/FrontPhaseScalingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: DistrictState.FrontPhase, FactionAssignment, FrontPhaseScaled
// Writes: Health, AttackStats, DefenseStats (stat scaling), FrontPhaseScaled
//
// When DistrictState.FrontPhase changes:
//   1. Query all alive enemies with FactionAssignment
//   2. Skip if FrontPhaseScaled.AppliedPhase >= current phase
//   3. Apply phase scaling multipliers from FactionDefinitionSO
//   4. Elite promotion: common enemies in Front-converted zones become elites
//   5. Mark FrontPhaseScaled.AppliedPhase = current phase
```

---

## Enemy Prefab Pipeline

Each enemy prefab requires:

| Layer | Components | Source |
|---|---|---|
| Model | MeshRenderer, Animator | Art pipeline |
| Animation | AnimatorController, AnimationClips | Art pipeline |
| AI | AIBrainAuthoring, AbilityProfileAuthoring | AI/ framework |
| Combat | DamageableAuthoring, HitboxOwnerMarker | Combat framework |
| Loot | LootTableAuthoring | Loot/ framework |
| Separation | EnemySeparationConfigAuthoring | AI/ framework |
| Physics | PhysicsShapeAuthoring (BelongsTo=Creature) | Physics framework |
| Faction | FactionAssignment (stamped at runtime) | District/ |
| Rippable Limbs | LimbRipAuthoring (EPIC 1) | Chassis/ |

---

## Setup Guide

1. **Create `Assets/Scripts/District/Definitions/FactionDefinitionSO.cs`** and `FactionEnums.cs`
2. **Create `Assets/Data/Districts/Necrospire/Factions/`** folder
3. **Author 4 FactionDefinitionSOs** for Necrospire: MourningCollective, RecursiveSpecters, ArchiveWardens, TheInheritors
4. **Create enemy prefabs** per faction (minimum 3 per faction for vertical slice = 12 prefabs):
   - Add AIBrainAuthoring with appropriate behavior profile
   - Add DamageableAuthoring + HitboxOwnerMarker
   - Set PhysicsShapeAuthoring BelongsTo = Creature (bit 8)
   - Add LootTableAuthoring with faction-appropriate loot table
5. **Create EncounterPoolSOs** per faction: `Assets/Data/Districts/Necrospire/Encounters/`
6. **Wire enemy prefabs** into FactionEnemyEntry array on each FactionDefinitionSO
7. **Wire FactionDefinitionSOs** into DistrictDefinitionSO.Factions[0-3]

---

## Verification

- [ ] Each FactionDefinitionSO has 3-5 enemy entries with valid prefabs
- [ ] Enemy prefabs have AIBrainAuthoring, DamageableAuthoring, PhysicsShapeAuthoring
- [ ] PhysicsShapeAuthoring.BelongsTo = Creature (not Everything)
- [ ] FactionSpawnStampSystem stamps FactionAssignment on every spawned enemy
- [ ] FactionEncounterPoolResolverSystem switches encounter pool on zone transition
- [ ] Front phase 2+ applies stat scaling to existing enemies
- [ ] Elite promotion occurs in Front-converted zones
- [ ] Strife mutation override replaces encounter pool when matching card is active
- [ ] Loot tables produce faction-appropriate drops
- [ ] Rippable limb definitions present on enemies with HasRippableLimbs = true
- [ ] No FactionId collisions across districts (each district uses unique range)

---

## Validation

```csharp
// File: Assets/Editor/District/FactionValidator.cs
// Build-time scan of all FactionDefinitionSO assets:
//
//   1. Enemy prefab completeness:
//      [ERROR] EnemyPrefab is null
//      [ERROR] Missing AIBrainAuthoring on prefab
//      [ERROR] Missing DamageableAuthoring on prefab
//      [ERROR] Missing PhysicsShapeAuthoring on prefab
//      [ERROR] PhysicsShapeAuthoring.BelongsTo includes Everything (must be Creature)
//   2. FactionId uniqueness:
//      [ERROR] Two FactionDefinitionSOs share the same FactionId
//   3. Roster sanity:
//      [WARNING] Fewer than 3 enemy types
//      [ERROR] SpawnWeight <= 0 on any entry
//      [ERROR] SpawnCost <= 0 on any entry
//   4. LootTable reference:
//      [WARNING] LootTable is null on any FactionEnemyEntry
//   5. Rippable limb consistency:
//      [ERROR] HasRippableLimbs=true but RippableLimbDefinitions is empty
```

---

## Live Tuning

| Parameter | Source | Effect |
|-----------|--------|--------|
| SpawnWeight | FactionEnemyEntry | Enemy type frequency within faction |
| SpawnCost | FactionEnemyEntry | Director budget consumed per spawn |
| AlarmRadius | FactionDefinitionSO | How far faction-wide alerts propagate |
| BaseAggression | FactionDefinitionSO | Default aggression level (Passive→Berserker) |
| FrontPhaseOverrides[0–3] | FactionDefinitionSO | Encounter pool swap at each Front phase |
| StatMultipliers | StrifeMutationEntry | Health/Damage/Speed scaling per Strife card |

---

## Debug Visualization

```csharp
// File: Assets/Scripts/District/Debug/FactionDebugOverlay.cs
// Development builds, toggle: Ctrl+F8
//   - Per-enemy floating label: "[Faction] [Tier] HP:X/Y"
//   - Faction color ring around each enemy (matches FactionDefinitionSO.FactionColor)
//   - Aggression state icon: shield=Passive, eye=Defensive, sword=Aggressive, skull=Berserker
//   - Alarm radius: wireframe sphere around enemies when alert triggers
//   - FrontPhaseScaled indicator: yellow border if phase-scaled, with applied phase number
```
