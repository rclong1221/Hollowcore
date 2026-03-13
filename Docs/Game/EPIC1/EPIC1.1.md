# EPIC 1.1: Chassis State & Slot Architecture

**Status**: Planning
**Epic**: EPIC 1 — Chassis & Limb System
**Priority**: Critical — Foundation for all limb mechanics
**Dependencies**: Framework: Items/Components, Traits/CharacterAttributes, Persistence/

---

## Overview

Core data model for the player's modular body. Six equipment slots map to physical body parts. Each slot holds a limb entity with stats, abilities, and district affinity. The chassis aggregates all limb stats and feeds them into the existing EquippedStatsSystem modifier pipeline.

---

## Component Definitions

### ChassisSlot Enum

```csharp
// File: Assets/Scripts/Chassis/Components/ChassisComponents.cs
namespace Hollowcore.Chassis
{
    public enum ChassisSlot : byte
    {
        Head = 0,
        Torso = 1,
        LeftArm = 2,
        RightArm = 3,
        LeftLeg = 4,
        RightLeg = 5
    }
}
```

### ChassisState (IComponentData)

Stored on a **child entity** linked from the player via `ChassisLink` to avoid the 16KB player archetype limit.

```csharp
// File: Assets/Scripts/Chassis/Components/ChassisComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Link from player entity to chassis child entity.
    /// Follows the TargetingModuleLink pattern from the framework.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ChassisLink : IComponentData
    {
        [GhostField]
        public Entity ChassisEntity;
    }

    /// <summary>
    /// The modular body state. Lives on a dedicated child entity (not the player).
    /// Each slot holds an Entity reference to the equipped limb (Entity.Null = empty/destroyed).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ChassisState : IComponentData
    {
        [GhostField] public Entity Head;
        [GhostField] public Entity Torso;
        [GhostField] public Entity LeftArm;
        [GhostField] public Entity RightArm;
        [GhostField] public Entity LeftLeg;
        [GhostField] public Entity RightLeg;

        /// <summary>
        /// Bitmask of which slots are destroyed (not just empty — physically lost).
        /// Bit index matches ChassisSlot enum value.
        /// </summary>
        [GhostField]
        public byte DestroyedSlotsMask;

        public Entity GetSlot(ChassisSlot slot) => slot switch
        {
            ChassisSlot.Head => Head,
            ChassisSlot.Torso => Torso,
            ChassisSlot.LeftArm => LeftArm,
            ChassisSlot.RightArm => RightArm,
            ChassisSlot.LeftLeg => LeftLeg,
            ChassisSlot.RightLeg => RightLeg,
            _ => Entity.Null
        };

        public void SetSlot(ChassisSlot slot, Entity entity)
        {
            switch (slot)
            {
                case ChassisSlot.Head: Head = entity; break;
                case ChassisSlot.Torso: Torso = entity; break;
                case ChassisSlot.LeftArm: LeftArm = entity; break;
                case ChassisSlot.RightArm: RightArm = entity; break;
                case ChassisSlot.LeftLeg: LeftLeg = entity; break;
                case ChassisSlot.RightLeg: RightLeg = entity; break;
            }
        }

        public bool IsSlotDestroyed(ChassisSlot slot) =>
            (DestroyedSlotsMask & (1 << (int)slot)) != 0;

        public void SetSlotDestroyed(ChassisSlot slot) =>
            DestroyedSlotsMask |= (byte)(1 << (int)slot);

        public void ClearSlotDestroyed(ChassisSlot slot) =>
            DestroyedSlotsMask &= (byte)~(1 << (int)slot);

        public int EquippedCount
        {
            get
            {
                int count = 0;
                if (Head != Entity.Null) count++;
                if (Torso != Entity.Null) count++;
                if (LeftArm != Entity.Null) count++;
                if (RightArm != Entity.Null) count++;
                if (LeftLeg != Entity.Null) count++;
                if (RightLeg != Entity.Null) count++;
                return count;
            }
        }
    }
}
```

### LimbInstance (IComponentData)

Each equipped limb is its own entity with runtime state.

```csharp
// File: Assets/Scripts/Chassis/Components/LimbComponents.cs
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis
{
    public enum LimbRarity : byte
    {
        Junk = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5  // Boss limbs only
    }

    public enum LimbDurability : byte
    {
        Temporary = 0,    // 30-60s, from common enemy rips
        DistrictLife = 1, // Lasts rest of district, from elite enemy rips
        Permanent = 2     // Standard salvage, boss rips, quest rewards
    }

    /// <summary>
    /// Runtime state for an equipped or world-placed limb entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct LimbInstance : IComponentData
    {
        /// <summary>Reference to the ScriptableObject definition (baked as blob or hash).</summary>
        [GhostField] public int LimbDefinitionId;

        /// <summary>Which chassis slot this limb fits.</summary>
        [GhostField] public ChassisSlot SlotType;

        /// <summary>Current integrity (0 = destroyed).</summary>
        [GhostField] public float CurrentIntegrity;

        /// <summary>Max integrity from definition.</summary>
        [GhostField] public float MaxIntegrity;

        [GhostField] public LimbRarity Rarity;
        [GhostField] public LimbDurability DurabilityType;

        /// <summary>For Temporary limbs: elapsed time since equip. Destroyed when >= ExpirationTime.</summary>
        [GhostField(Quantization = 100)] public float ElapsedTime;
        [GhostField(Quantization = 100)] public float ExpirationTime;

        /// <summary>District affinity ID for memory bonus (-1 = no affinity).</summary>
        [GhostField] public int DistrictAffinityId;

        /// <summary>Display name for UI.</summary>
        [GhostField] public FixedString64Bytes DisplayName;
    }
}
```

### LimbStatBlock (IComponentData)

Stats contributed by a single limb to the chassis aggregate.

```csharp
// File: Assets/Scripts/Chassis/Components/LimbComponents.cs (continued)
namespace Hollowcore.Chassis
{
    /// <summary>
    /// Stat contributions from a single limb. Read by ChassisStatAggregatorSystem.
    /// </summary>
    public struct LimbStatBlock : IComponentData
    {
        public float BonusDamage;
        public float BonusArmor;
        public float BonusMoveSpeed;    // Additive modifier (can be negative for heavy limbs)
        public float BonusMaxHealth;
        public float BonusAttackSpeed;
        public float BonusStamina;
        public float HeatResistance;    // Burn district affinity
        public float ToxinResistance;   // Quarantine district affinity
        public float FallDamageReduction; // Lattice district affinity
    }
}
```

---

## ScriptableObject Definitions

### LimbDefinitionSO

```csharp
// File: Assets/Scripts/Chassis/Definitions/LimbDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Chassis.Definitions
{
    [CreateAssetMenu(fileName = "NewLimb", menuName = "Hollowcore/Chassis/Limb Definition")]
    public class LimbDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int LimbId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public ChassisSlot SlotType;
        public LimbRarity Rarity;

        [Header("Stats")]
        public float MaxIntegrity = 100f;
        public float BonusDamage;
        public float BonusArmor;
        public float BonusMoveSpeed;
        public float BonusMaxHealth;
        public float BonusAttackSpeed;
        public float BonusStamina;
        public float HeatResistance;
        public float ToxinResistance;
        public float FallDamageReduction;

        [Header("District Memory")]
        [Tooltip("District this limb has affinity for (-1 = none)")]
        public int DistrictAffinityId = -1;
        [Tooltip("Bonus multiplier when in affinity district (0.05 = 5%)")]
        public float AffinityBonusMultiplier = 0.05f;

        [Header("Special")]
        [Tooltip("Ability unlocked while this limb is equipped (0 = none)")]
        public int SpecialAbilityId;
        [Tooltip("Visual prefab for the limb mesh")]
        public GameObject VisualPrefab;
        [Tooltip("Visual prefab for the destroyed stump")]
        public GameObject StumpPrefab;

        [Header("Rip Settings")]
        [Tooltip("If from an enemy rip: how long temporary limbs last")]
        public float TemporaryDuration = 45f;
        [Tooltip("Whether this limb can carry curses/instabilities")]
        public bool CanBeCursed;
    }
}
```

---

## Systems

### ChassisStatAggregatorSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/ChassisStatAggregatorSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: EquippedStatsSystem (framework)
//
// Reads: ChassisState (on chassis entity), LimbStatBlock (on each limb entity)
// Writes: Modifier entries into EquippedStatsSystem pipeline
//
// For each player with ChassisLink:
//   1. Resolve ChassisLink → ChassisState
//   2. For each non-null slot, read LimbStatBlock from limb entity
//   3. Sum all limb stats into aggregate
//   4. Check district affinity: if current district matches limb's DistrictAffinityId,
//      multiply that limb's contribution by (1 + AffinityBonusMultiplier)
//   5. Push aggregate as modifier into EquippedStatsSystem
```

### ChassisBootstrapSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/ChassisBootstrapSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// On player entity spawn:
//   1. Create chassis child entity with ChassisState (all slots Entity.Null)
//   2. Set ChassisLink on player entity → chassis child
//   3. If starting loadout defined, spawn starting limb entities and populate slots
```

### LimbExpirationSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/LimbExpirationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// For each LimbInstance with DurabilityType == Temporary:
//   1. Increment ElapsedTime by deltaTime
//   2. If ElapsedTime >= ExpirationTime:
//      a. Set corresponding ChassisState slot to Entity.Null
//      b. Destroy limb entity
//      c. Fire LimbLostEvent for UI notification
```

---

## Authoring

### ChassisAuthoring

```csharp
// File: Assets/Scripts/Chassis/Authoring/ChassisAuthoring.cs
// Added to player prefab. Creates child entity with ChassisState during baking.
// Baker:
//   1. CreateAdditionalEntity(TransformUsageFlags.None) → chassisEntity
//   2. AddComponent<ChassisState>(chassisEntity) with all slots Entity.Null
//   3. AddComponent<ChassisLink>(playerEntity) with ChassisEntity = chassisEntity
//   4. If startingLimbs configured, bake LimbDefinitionSO refs for spawn at runtime
```

---

## Setup Guide

1. **Create `Assets/Scripts/Chassis/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/, Bridges/
2. **Create assembly definition** `Hollowcore.Chassis.asmdef` referencing `DIG.Shared`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`
3. Add `ChassisAuthoring` to the player prefab (Warrok_Server or equivalent)
4. Create starting limb definitions: `Assets/Data/Chassis/Limbs/` with basic set (CommonArm, CommonLeg, CommonTorso, CommonHead)
5. Reimport subscene after adding authoring to prefab
6. Verify: player entity has `ChassisLink`, child entity has `ChassisState`, all slots populated with starting limbs

---

## Verification

- [ ] ChassisState child entity created on player spawn
- [ ] ChassisLink correctly references child entity
- [ ] Starting limbs spawn as entities with LimbInstance + LimbStatBlock
- [ ] ChassisStatAggregatorSystem sums limb stats and feeds EquippedStatsSystem
- [ ] Slot get/set works for all 6 slots
- [ ] DestroyedSlotsMask correctly tracks destroyed vs empty
- [ ] Temporary limbs expire after duration
- [ ] 16KB archetype limit not exceeded on player entity (ChassisLink is only 8 bytes)

---

## BlobAsset Pipeline

`LimbDefinitionSO` is read by multiple runtime systems (stat aggregation, memory, equip). Bake it to a BlobAsset for Burst-compatible access.

### Blob Struct

```csharp
// File: Assets/Scripts/Chassis/Definitions/LimbDefinitionBlob.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Chassis.Definitions
{
    public struct LimbDefinitionBlob
    {
        public int LimbId;
        public BlobString DisplayName;
        public ChassisSlot SlotType;
        public LimbRarity Rarity;

        // Stats
        public float MaxIntegrity;
        public float BonusDamage;
        public float BonusArmor;
        public float BonusMoveSpeed;
        public float BonusMaxHealth;
        public float BonusAttackSpeed;
        public float BonusStamina;
        public float HeatResistance;
        public float ToxinResistance;
        public float FallDamageReduction;

        // District Memory
        public int DistrictAffinityId;
        public float AffinityBonusMultiplier;

        // Special
        public int SpecialAbilityId;
        public float TemporaryDuration;
        public bool CanBeCursed;

        // Memory entries (variable-length)
        public BlobArray<LimbMemoryEntryBlob> MemoryEntries;
    }

    public struct LimbMemoryEntryBlob
    {
        public int DistrictId;
        public byte BonusType; // MemoryBonusType
        public float BonusValue;
    }

    /// <summary>
    /// Database blob containing all limb definitions. Singleton entity.
    /// </summary>
    public struct LimbDefinitionDatabase
    {
        public BlobArray<LimbDefinitionBlob> Definitions;
    }

    /// <summary>
    /// Singleton component referencing the baked database.
    /// </summary>
    public struct LimbDatabaseReference : IComponentData
    {
        public BlobAssetReference<LimbDefinitionDatabase> Value;
    }
}
```

### BakeToBlob Method

```csharp
// File: Assets/Scripts/Chassis/Definitions/LimbDefinitionSO.cs (extension)
// Add to LimbDefinitionSO class:
public static BlobAssetReference<LimbDefinitionDatabase> BakeDatabaseToBlob(
    LimbDefinitionSO[] allDefinitions, BlobAssetStore blobAssetStore)
{
    var builder = new BlobBuilder(Allocator.Temp);
    ref var db = ref builder.ConstructRoot<LimbDefinitionDatabase>();
    var defArray = builder.Allocate(ref db.Definitions, allDefinitions.Length);

    for (int i = 0; i < allDefinitions.Length; i++)
    {
        var so = allDefinitions[i];
        defArray[i].LimbId = so.LimbId;
        builder.AllocateString(ref defArray[i].DisplayName, so.DisplayName);
        defArray[i].SlotType = so.SlotType;
        defArray[i].Rarity = so.Rarity;
        defArray[i].MaxIntegrity = so.MaxIntegrity;
        defArray[i].BonusDamage = so.BonusDamage;
        defArray[i].BonusArmor = so.BonusArmor;
        defArray[i].BonusMoveSpeed = so.BonusMoveSpeed;
        defArray[i].BonusMaxHealth = so.BonusMaxHealth;
        defArray[i].BonusAttackSpeed = so.BonusAttackSpeed;
        defArray[i].BonusStamina = so.BonusStamina;
        defArray[i].HeatResistance = so.HeatResistance;
        defArray[i].ToxinResistance = so.ToxinResistance;
        defArray[i].FallDamageReduction = so.FallDamageReduction;
        defArray[i].DistrictAffinityId = so.DistrictAffinityId;
        defArray[i].AffinityBonusMultiplier = so.AffinityBonusMultiplier;
        defArray[i].SpecialAbilityId = so.SpecialAbilityId;
        defArray[i].TemporaryDuration = so.TemporaryDuration;
        defArray[i].CanBeCursed = so.CanBeCursed;
        // Memory entries baked from SO.MemoryEntries list
    }

    var result = builder.CreateBlobAssetReference<LimbDefinitionDatabase>(Allocator.Persistent);
    builder.Dispose();
    return result;
}
```

### Bootstrap

```csharp
// File: Assets/Scripts/Chassis/Systems/LimbDatabaseBootstrapSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup (runs once)
//
// 1. Load all LimbDefinitionSO from Resources/ or Addressables
// 2. Call BakeDatabaseToBlob() → BlobAssetReference<LimbDefinitionDatabase>
// 3. Create singleton entity with LimbDatabaseReference
// 4. All runtime systems query LimbDatabaseReference singleton for Burst-safe lookups
```

---

## Validation

### LimbDefinitionSO.OnValidate

```csharp
// File: Assets/Scripts/Chassis/Definitions/LimbDefinitionSO.cs (add to class)
#if UNITY_EDITOR
private void OnValidate()
{
    if (LimbId < 0)
        Debug.LogError($"[LimbDefinitionSO] '{name}': LimbId must be >= 0", this);

    if (MaxIntegrity <= 0f)
        Debug.LogError($"[LimbDefinitionSO] '{name}': MaxIntegrity must be > 0", this);

    if (string.IsNullOrWhiteSpace(DisplayName))
        Debug.LogWarning($"[LimbDefinitionSO] '{name}': DisplayName is empty", this);

    if (DistrictAffinityId >= 0 && AffinityBonusMultiplier <= 0f)
        Debug.LogWarning($"[LimbDefinitionSO] '{name}': Has district affinity but AffinityBonusMultiplier is 0", this);

    if (TemporaryDuration < 0f)
        Debug.LogError($"[LimbDefinitionSO] '{name}': TemporaryDuration cannot be negative", this);

    if (SlotType == ChassisSlot.Head && VisualPrefab == null)
        Debug.LogWarning($"[LimbDefinitionSO] '{name}': Head limb should have a VisualPrefab", this);
}
#endif
```

### Build-Time Validation

```csharp
// File: Assets/Editor/Chassis/LimbDefinitionValidator.cs
// [InitializeOnLoad] editor class that runs on build:
// 1. Find all LimbDefinitionSO via AssetDatabase
// 2. Check for duplicate LimbId values → error
// 3. Check every ChassisSlot enum value has at least one definition → warning
// 4. Check all VisualPrefab references are non-null for non-Junk rarity → warning
// 5. Check StumpPrefab assigned for each SlotType that has any definition → warning
// 6. Validate MemoryEntries: DistrictId >= 0, BonusValue > 0
```

---

## Editor Tooling

### Custom Inspector: LimbDefinitionSO

```
// File: Assets/Editor/Chassis/LimbDefinitionSOEditor.cs
// Custom Editor for LimbDefinitionSO:
// - Stat radar chart (BonusDamage, BonusArmor, BonusMoveSpeed, etc.) drawn as preview
// - Slot type icon badge (arm/leg/torso/head) next to name
// - Color-coded rarity border (Junk=gray, Common=white, Uncommon=green, Rare=blue, Epic=purple, Legendary=gold)
// - Memory entry list with district name lookup (not just raw ID)
// - "Compare With..." button: side-by-side stat diff with another LimbDefinitionSO
// - VisualPrefab inline preview (3D mesh thumbnail)
```

### Chassis Workstation (EditorWindow)

```
// File: Assets/Editor/ChassisWorkstation/ChassisWorkstationWindow.cs
// Follows DIG workstation pattern: sidebar tabs, IWorkstationModule interface.
//
// Modules:
// 1. **Limb Browser** — searchable/filterable grid of all LimbDefinitionSO assets
//    - Filter by: SlotType, Rarity, DistrictAffinity, has curse
//    - Sort by: any stat, name, LimbId
//    - Drag-drop to chassis loadout preview
//
// 2. **Loadout Previewer** — 6-slot body diagram
//    - Drag limbs into slots, see aggregated stats in real-time
//    - Shows: total stat block, memory bonuses per district, penalty states
//    - "Export Loadout" button → saves as starting loadout preset
//
// 3. **Balance Matrix** — spreadsheet of all limbs with sortable stat columns
//    - Highlights outliers (> 2σ from mean) in red
//    - Per-rarity stat budget validation (Legendary should have higher total stat budget than Common)
//
// 4. **ID Audit** — lists all LimbId values, flags duplicates/gaps
```

---

## Live Tuning

### Runtime Config Singleton

```csharp
// File: Assets/Scripts/Chassis/Components/ChassisRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Singleton for runtime-tunable chassis parameters.
    /// Initialized from ChassisConfigSO, modifiable in play mode.
    /// </summary>
    public struct ChassisRuntimeConfig : IComponentData
    {
        /// <summary>Base limb integrity regen rate per second (out of combat).</summary>
        public float LimbRegenRate;

        /// <summary>Temporary limb default duration multiplier (1.0 = as authored).</summary>
        public float TemporaryDurationMultiplier;

        /// <summary>Global limb damage multiplier (for difficulty scaling).</summary>
        public float LimbDamageMultiplier;

        /// <summary>Memory bonus global multiplier (1.0 = as authored).</summary>
        public float MemoryBonusMultiplier;
    }

    /// <summary>
    /// Dirty flag. Enable to force systems to re-read config on next frame.
    /// </summary>
    public struct ChassisConfigDirty : IComponentData, IEnableableComponent { }
}
```

**Tunable parameters**: `LimbRegenRate`, `TemporaryDurationMultiplier`, `LimbDamageMultiplier`, `MemoryBonusMultiplier`.

**Propagation**: Systems check `ChassisConfigDirty` (IEnableableComponent). When a designer changes a value at runtime, enable the dirty flag. Systems re-read on next tick and disable the flag.

---

## Debug Visualization

### In-Game Chassis Debug Overlay

```
// Toggle: console command `chassis.debug` or key binding (default: F7)
//
// Draws:
// 1. Per-slot integrity bars above player head (6 small bars, color = green→yellow→red)
// 2. Slot labels (H, T, LA, RA, LL, RL) with entity ID
// 3. Destroyed slots shown as X with red background
// 4. Active memory bonuses listed with district name + bonus %
// 5. Penalty state text: "CRAWLING", "LIMPING", "ONE-ARM", "UNARMED"
//
// Implementation: ChassisDebugOverlaySystem (ClientSimulation | LocalSimulation, PresentationSystemGroup)
// Uses DIG.Debug.DebugOverlay API for screen-space text/bars
```

### Gizmo Drawing

```
// File: Assets/Editor/Chassis/ChassisGizmoDrawer.cs
// In Scene view when player entity selected:
// - Draw wireframe socket positions for each ChassisSlot
// - Color: green = occupied, red = destroyed, gray = empty
// - Draw line from player to each limb entity position (should be co-located)
// - Label each socket with limb name + integrity %
```
