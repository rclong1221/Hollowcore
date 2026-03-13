# EPIC 9.2: Compendium Entries (Permanent Unlocks)

**Status**: Planning
**Epic**: EPIC 9 — Compendium & Meta Progression
**Priority**: Medium — Long-term retention loop
**Dependencies**: Framework: Roguelite/ (MetaUnlockTreeSO, MetaBank), Persistence/ (ISaveModule); EPIC 4 (Districts, extraction triggers)

---

## Overview

Compendium Entries are permanent meta-progression unlocks that survive expedition wipes. Each entry represents a piece of Hollowcore knowledge — a discovered mission type, a new vendor, a traversal shortcut, or a lore fragment with embedded gameplay hints. Entries map directly to the framework's MetaUnlockTreeSO pattern: unlock conditions replace currency costs, and unlock rewards inject new content into the game's district and quest pools. The system checks conditions at extraction and awards new entries, creating the "play more, discover more variety" retention loop.

---

## Component Definitions

### CompendiumEntryCategory Enum

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumEntryComponents.cs
namespace Hollowcore.Compendium
{
    /// <summary>
    /// Categories for permanent compendium entries.
    /// Each category unlocks a different type of content across future expeditions.
    /// </summary>
    public enum CompendiumEntryCategory : byte
    {
        Mission = 0,     // New mission types and events added to quest pools
        Vendor = 1,      // New NPC vendors unlocked in districts
        Traversal = 2,   // Movement abilities or zone shortcuts
        Lore = 3,        // Flavor text with embedded mechanical hints
        Enemy = 4,       // Bestiary entries — damage type weaknesses, attack patterns
        District = 5,    // District-specific knowledge — hazard info, secret areas
        Boss = 6         // Boss dossiers — attack phase breakdowns, counter token hints
    }
}
```

### CompendiumEntryUnlockCondition Enum

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumEntryComponents.cs
namespace Hollowcore.Compendium
{
    /// <summary>
    /// Types of conditions that unlock compendium entries.
    /// Evaluated at extraction by CompendiumEntrySystem.
    /// </summary>
    public enum CompendiumEntryUnlockCondition : byte
    {
        DistrictComplete = 0,    // Complete a specific district
        BossDefeated = 1,        // Kill a specific boss
        EchoComplete = 2,        // Finish a specific echo encounter
        TraceThreshold = 3,      // Reach a Trace level threshold
        HiddenContentFound = 4,  // Discover a specific hidden area or interactable
        QuestComplete = 5,       // Complete a specific quest chain
        EnemyKillCount = 6,      // Kill N of a specific enemy type
        ItemAcquired = 7,        // Acquire a specific item at least once
        MultiCondition = 8       // Compound: all sub-conditions must be met (defined in SO)
    }
}
```

### CompendiumEntryState (IComponentData)

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumEntryComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// Tracks a single unlocked compendium entry on the compendium child entity.
    /// Buffer element — one per unlocked entry.
    /// </summary>
    [InternalBufferCapacity(0)] // Dynamic buffer — entries grow over many expeditions
    public struct CompendiumEntryState : IBufferElementData
    {
        /// <summary>Hash of the CompendiumEntryDefinitionSO.</summary>
        public int EntryDefinitionId;

        /// <summary>Category cached for fast UI filtering.</summary>
        public CompendiumEntryCategory Category;

        /// <summary>Tick when this entry was unlocked (for "new" badge in UI).</summary>
        public uint UnlockedAtTick;
    }
}
```

### CompendiumEntryNewFlag (IBufferElementData)

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumEntryComponents.cs
using Unity.Entities;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// Transient buffer tracking entries unlocked during the current expedition.
    /// Cleared at expedition start. Used by extraction summary UI to highlight new discoveries.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CompendiumEntryNewFlag : IBufferElementData
    {
        public int EntryDefinitionId;
    }
}
```

### CompendiumEntrySaveData

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumEntryComponents.cs
using System;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// Serializable save payload for CompendiumEntrySaveModule.
    /// Persists permanently across all expeditions (meta-progression).
    /// </summary>
    [Serializable]
    public struct CompendiumEntrySaveEntry
    {
        public int EntryDefinitionId;
        public CompendiumEntryCategory Category;
    }
}
```

---

## ScriptableObject Definitions

### CompendiumEntryDefinitionSO

```csharp
// File: Assets/Scripts/Compendium/Definitions/CompendiumEntryDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Compendium.Definitions
{
    [CreateAssetMenu(fileName = "NewEntry", menuName = "Hollowcore/Compendium/Entry Definition")]
    public class CompendiumEntryDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int EntryId;
        public string DisplayName;
        [TextArea(3, 8)] public string Description;
        public Sprite Icon;
        public Sprite LockedIcon;
        public CompendiumEntryCategory Category;

        [Header("Unlock Condition")]
        public CompendiumEntryUnlockCondition ConditionType;
        [Tooltip("Condition parameter: district ID, boss ID, quest ID, enemy type ID, etc.")]
        public int ConditionTargetId;
        [Tooltip("For threshold conditions: required count or level")]
        public int ConditionThreshold = 1;

        [Header("Multi-Condition (only if ConditionType == MultiCondition)")]
        [Tooltip("All sub-conditions must be satisfied for unlock")]
        public CompendiumSubCondition[] SubConditions;

        [Header("Unlock Reward")]
        [Tooltip("What becomes available after unlocking this entry")]
        public CompendiumUnlockReward RewardType;
        [Tooltip("Reward parameter: quest pool ID, vendor ID, traversal ability ID, etc.")]
        public int RewardTargetId;
        [Tooltip("Human-readable reward description for UI")]
        public string RewardDescription;

        [Header("Lore")]
        [TextArea(5, 15)] public string LoreText;
        [Tooltip("Gameplay hint embedded in the lore (shown separately in UI)")]
        [TextArea(1, 3)] public string MechanicalHint;
    }

    [System.Serializable]
    public struct CompendiumSubCondition
    {
        public CompendiumEntryUnlockCondition ConditionType;
        public int TargetId;
        public int Threshold;
    }

    public enum CompendiumUnlockReward : byte
    {
        None = 0,            // Pure lore, no mechanical unlock
        QuestPoolEntry = 1,  // Adds a mission type to the quest pool
        VendorSpawn = 2,     // Enables an NPC vendor in a district
        TraversalAbility = 3,// Unlocks a movement ability or shortcut
        EnemyWeakness = 4,   // Permanently shows weakness icon on enemy health bars
        BossPhaseHint = 5,   // Shows boss phase transitions in dossier
        DistrictSecret = 6   // Marks secret area on district map
    }
}
```

---

## Systems

### CompendiumEntrySystem

```csharp
// File: Assets/Scripts/Compendium/Systems/CompendiumEntrySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: ExtractionEvent (transient, from EPIC 4), expedition run stats,
//        CompendiumEntryState buffer, CompendiumEntryDefinitionSO database
// Writes: CompendiumEntryState buffer, CompendiumEntryNewFlag buffer
//
// Runs at extraction — when ExtractionEvent entities appear:
//   1. Gather expedition stats: districts completed, bosses killed, echoes finished,
//      Trace level, hidden areas found, quests completed, enemy kill counts, items acquired
//   2. Load all CompendiumEntryDefinitionSO from database (BlobAsset or managed lookup)
//   3. For each definition NOT already in CompendiumEntryState buffer:
//      a. Evaluate unlock condition against expedition stats
//      b. For MultiCondition: all sub-conditions must pass
//      c. If condition met: append to CompendiumEntryState + CompendiumEntryNewFlag
//   4. Apply unlock rewards:
//      - QuestPoolEntry → add quest ID to MetaUnlockTreeSO's unlocked quest set
//      - VendorSpawn → add vendor ID to MetaUnlockTreeSO's unlocked NPC set
//      - TraversalAbility → add ability ID to player's available abilities
//      - EnemyWeakness / BossPhaseHint / DistrictSecret → set reveal flags
//   5. Fire CompendiumUnlockEvent per new entry for UI notification
//   6. Mark CompendiumEntrySaveModule dirty for persistence
```

### CompendiumEntryBootstrapSystem

```csharp
// File: Assets/Scripts/Compendium/Systems/CompendiumEntryBootstrapSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// On expedition start:
//   1. Load persisted CompendiumEntryState from CompendiumEntrySaveModule
//   2. Populate CompendiumEntryState buffer on compendium child entity
//   3. Clear CompendiumEntryNewFlag buffer (fresh expedition)
//   4. Apply all previously unlocked rewards (vendor spawns, quest pool entries, etc.)
```

### CompendiumEntrySaveModule

```csharp
// File: Assets/Scripts/Compendium/Persistence/CompendiumEntrySaveModule.cs
// Implements: ISaveModule (TypeId = 24)
// Persistence: Permanent — survives expedition wipes
//
// Serialize:
//   1. Read CompendiumEntryState buffer from compendium child entity
//   2. Write entry count + array of CompendiumEntrySaveEntry structs (EntryDefinitionId, Category)
//   3. Binary format matching ISaveModule conventions (.dig, CRC32)
//
// Deserialize:
//   1. Read entry count + CompendiumEntrySaveEntry array
//   2. CompendiumEntryBootstrapSystem uses this data to populate buffer on expedition start
//
// DirtyFlags: Set by CompendiumEntrySystem when new entries unlocked
```

---

## Framework Integration

| Framework System | Integration |
|---|---|
| Roguelite/ (MetaUnlockTreeSO) | Entries ARE meta unlocks — same tree, conditions replace costs |
| Roguelite/ (MetaBank) | Entry state stored in MetaBank alongside page state |
| Persistence/ (ISaveModule) | CompendiumEntrySaveModule TypeId=24, permanent persistence |
| Quest/ | QuestPoolEntry rewards add quest definitions to available pool |

---

## Setup Guide

1. **Reuse `Assets/Scripts/Compendium/`** folder created in EPIC 9.1; add Persistence/ subfolder
2. Create entry definitions: `Assets/Data/Compendium/Entries/` — organize by category subfolder (Mission/, Vendor/, Traversal/, Lore/, Enemy/, District/, Boss/)
3. Create at least 3 vertical-slice entries: one DistrictComplete, one BossDefeated, one HiddenContentFound
4. Add `CompendiumEntryState` and `CompendiumEntryNewFlag` buffers to CompendiumAuthoring baker (shared child entity with pages)
5. Register `CompendiumEntrySaveModule` (TypeId=24) in the persistence system's module registry
6. Wire ExtractionEvent from EPIC 4 district extraction flow to trigger CompendiumEntrySystem
7. Verify save/load round-trip: unlock entry → save → new expedition → entry still present

---

## Verification

- [ ] CompendiumEntryState buffer exists on compendium child entity
- [ ] CompendiumEntryNewFlag buffer exists and is empty at expedition start
- [ ] CompendiumEntrySystem fires on ExtractionEvent and evaluates all unowned definitions
- [ ] DistrictComplete condition unlocks entry after completing target district
- [ ] BossDefeated condition unlocks entry after killing target boss
- [ ] MultiCondition requires ALL sub-conditions satisfied
- [ ] Duplicate entries never added (already-owned check before append)
- [ ] CompendiumEntryNewFlag tracks entries unlocked this expedition only
- [ ] QuestPoolEntry reward adds quest to available pool for future expeditions
- [ ] VendorSpawn reward enables NPC in target district on next expedition
- [ ] CompendiumEntrySaveModule serializes/deserializes correctly (TypeId=24)
- [ ] Entries persist across expedition wipes — permanent meta-progression
- [ ] No 16KB archetype impact — all buffers on child entity via CompendiumLink

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumEntryBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// Blob representation of CompendiumEntryDefinitionSO for Burst-compatible
    /// unlock condition evaluation in CompendiumEntrySystem.
    /// </summary>
    public struct CompendiumEntryBlob
    {
        public int EntryId;
        public byte Category;  // CompendiumEntryCategory
        public byte ConditionType; // CompendiumEntryUnlockCondition
        public int ConditionTargetId;
        public int ConditionThreshold;
        public byte RewardType; // CompendiumUnlockReward
        public int RewardTargetId;

        // Multi-condition support
        public BlobArray<CompendiumSubConditionBlob> SubConditions;
    }

    public struct CompendiumSubConditionBlob
    {
        public byte ConditionType;
        public int TargetId;
        public int Threshold;
    }

    /// <summary>
    /// All entry definitions for O(1) lookup and Burst iteration.
    /// Singleton blob on the compendium config entity.
    /// </summary>
    public struct CompendiumEntryDatabase
    {
        public BlobArray<CompendiumEntryBlob> Entries;
    }

    public struct CompendiumEntryDatabaseRef : IComponentData
    {
        public BlobAssetReference<CompendiumEntryDatabase> Value;
    }
}
```

```csharp
// File: Assets/Scripts/Compendium/Authoring/CompendiumEntryDatabaseAuthoring.cs
namespace Hollowcore.Compendium.Authoring
{
    // Baker:
    //   1. Gather all CompendiumEntryDefinitionSO from project
    //   2. Build BlobBuilder with CompendiumEntryDatabase root
    //   3. For each definition: populate CompendiumEntryBlob fields
    //   4. For MultiCondition entries: allocate SubConditions BlobArray
    //   5. AddBlobAsset, add CompendiumEntryDatabaseRef to config entity
    //   6. CompendiumEntrySystem reads blob for condition evaluation (Burst-safe)
}
```

---

## Validation

```csharp
// File: Assets/Editor/CompendiumWorkstation/CompendiumEntryValidation.cs
namespace Hollowcore.Compendium.Editor
{
    /// <summary>
    /// Validates CompendiumEntryDefinitionSO assets for uniqueness, completeness, and cross-references.
    /// </summary>
    // Rules:
    //   1. EntryId uniqueness: no two CompendiumEntryDefinitionSO may share an EntryId
    //   2. DisplayName must be non-empty
    //   3. Category must be a valid CompendiumEntryCategory enum value
    //   4. ConditionType must be a valid CompendiumEntryUnlockCondition enum value
    //   5. Cross-reference validation:
    //      a. DistrictComplete: ConditionTargetId must reference a valid district in the district graph
    //      b. BossDefeated: ConditionTargetId must reference a valid boss definition
    //      c. QuestComplete: ConditionTargetId must reference a valid quest chain ID
    //      d. EnemyKillCount: ConditionTargetId must reference a valid enemy type
    //   6. MultiCondition entries must have at least 2 SubConditions (otherwise use single condition)
    //   7. Each SubCondition must independently pass cross-reference validation
    //   8. RewardType must be valid; RewardTargetId must reference valid content for reward type
    //   9. Page completeness: for each CompendiumEntryCategory, at least one entry must exist
    //  10. Lore entries: LoreText must be non-empty, MechanicalHint recommended but optional
    //
    // Report: CompendiumValidationReport with errors (blocking) and warnings (advisory)
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Compendium/Debug/CompendiumEntryLiveTuning.cs
namespace Hollowcore.Compendium.Debug
{
    /// <summary>
    /// Runtime-editable entry unlock conditions for testing meta-progression.
    /// </summary>
    // Tunable fields:
    //   - UnlockAllEntries (bool) — immediately unlock every entry for the local player
    //   - UnlockEntryById (int) — unlock a specific entry by ID
    //   - LockAllEntries (bool) — reset all entries to locked (clears CompendiumEntryState buffer)
    //   - BypassConditions (bool) — all condition checks return true (test unlock rewards)
    //   - SimulateExtraction (bool) — trigger CompendiumEntrySystem as if extraction occurred
    //   - ForceNewFlagCount (int) — set N random entries as "new this expedition" for UI testing
    //
    // Pattern: static CompendiumEntryLiveTuningOverrides, checked by CompendiumEntrySystem.
    // WARNING: LockAllEntries also wipes CompendiumEntrySaveModule — use only in dev builds.
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/CompendiumWorkstation/Modules/CompendiumSimulationModule.cs
namespace Hollowcore.Compendium.Editor.Modules
{
    /// <summary>
    /// Simulates compendium completion rate across multiple expeditions.
    /// Runs in-editor without Play mode.
    /// </summary>
    // Simulation parameters:
    //   - ExpeditionCount: 50 (default, simulates a player's progression arc)
    //   - DistrictsPerExpedition: 4-8 (configurable range)
    //   - BossDefeatProbability: 0.6
    //   - HiddenContentDiscoveryRate: 0.15 per district
    //   - EchoCompletionRate: 0.4
    //   - QuestCompletionRate: 0.5
    //   - TraceLevelDistribution: from EPIC 8 simulation output (or manual override)
    //
    // Output metrics:
    //   - Completion curve: % of all entries unlocked vs expedition count (line graph)
    //   - Per-category completion: separate curves for Mission, Vendor, Traversal, Lore, etc.
    //   - First-unlock distribution: which expedition each entry is typically first discovered
    //   - Stall detection: flag entries that < 5% of simulated players unlock by expedition 50
    //   - Content unlock rate: new entries per expedition (should decline but never hit zero)
    //
    // Visualization: EditorGUILayout line graphs, summary statistics table, CSV export
}
```
