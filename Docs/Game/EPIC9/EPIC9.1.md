# EPIC 9.1: Compendium Pages (Run Consumables)

**Status**: Planning
**Epic**: EPIC 9 — Compendium & Meta Progression
**Priority**: Medium — Tactical depth within a single expedition
**Dependencies**: Framework: Roguelite/ (RunModifierStack), Items/; EPIC 4 (Districts), EPIC 6 (Gate Screen)

---

## Overview

Compendium Pages are run-level consumable modifiers the player earns during exploration and spends for immediate tactical advantage. Three page types (Scout, Suppression, Insight) each address a different information or threat axis. Pages slot into a limited inventory (4-6 active slots) and are consumed on use. Their effects are implemented as temporary entries in the framework's RunModifierStack, making them single-use run modifiers with curated Hollowcore-specific effects.

---

## Component Definitions

### CompendiumPageType Enum

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumPageComponents.cs
namespace Hollowcore.Compendium
{
    /// <summary>
    /// The three page families, each addressing a different tactical axis.
    /// Scout = information. Suppression = threat mitigation. Insight = weakness reveal.
    /// </summary>
    public enum CompendiumPageType : byte
    {
        Scout = 0,        // Reveal gates, Front patterns, hidden POIs, echo locations
        Suppression = 1,  // Slow Front advance, weaken threat factions, reduce hazard intensity
        Insight = 2       // Reveal boss weaknesses, echo mutation types, reward contents
    }
}
```

### CompendiumPageState (IBufferElementData)

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumPageComponents.cs
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// One page held in the player's active page inventory.
    /// Buffer lives on a dedicated child entity (CompendiumLink) to avoid the 16KB player archetype limit.
    /// InternalBufferCapacity kept low — max 6 pages, no ghost replication pressure.
    /// </summary>
    [InternalBufferCapacity(6)]
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CompendiumPageState : IBufferElementData
    {
        /// <summary>Hash of the CompendiumPageDefinitionSO. Resolves to SO at runtime.</summary>
        [GhostField] public int PageDefinitionId;

        /// <summary>Page type cached for fast filtering in UI and systems.</summary>
        [GhostField] public CompendiumPageType PageType;

        /// <summary>Slot index (0-5). -1 = unslotted (overflow / recently acquired).</summary>
        [GhostField] public sbyte SlotIndex;

        /// <summary>Display name for UI tooltip.</summary>
        [GhostField] public FixedString64Bytes DisplayName;
    }
}
```

### CompendiumLink (IComponentData)

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumPageComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// Link from the player entity to the compendium child entity.
    /// Follows the TargetingModuleLink / ChassisLink child-entity pattern.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CompendiumLink : IComponentData
    {
        [GhostField] public Entity CompendiumEntity;
    }
}
```

### PageActivationRequest (IComponentData)

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumPageComponents.cs
using Unity.Entities;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// Transient component placed on an RPC entity to request page activation.
    /// Created by the client UI, consumed by PageActivationSystem on the server.
    /// </summary>
    public struct PageActivationRequest : IComponentData
    {
        /// <summary>The PageDefinitionId to consume.</summary>
        public int PageDefinitionId;

        /// <summary>Slot index the page was used from.</summary>
        public sbyte SlotIndex;
    }
}
```

### CompendiumPageConfig (IComponentData)

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumPageComponents.cs
using Unity.Entities;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// Singleton config for page inventory limits. Baked from CompendiumPageConfigAuthoring.
    /// </summary>
    public struct CompendiumPageConfig : IComponentData
    {
        /// <summary>Maximum active page slots (default 6).</summary>
        public byte MaxActiveSlots;

        /// <summary>Maximum total pages carried including overflow (default 10).</summary>
        public byte MaxTotalPages;
    }
}
```

---

## ScriptableObject Definitions

### CompendiumPageDefinitionSO

```csharp
// File: Assets/Scripts/Compendium/Definitions/CompendiumPageDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Compendium.Definitions
{
    [CreateAssetMenu(fileName = "NewPage", menuName = "Hollowcore/Compendium/Page Definition")]
    public class CompendiumPageDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int PageId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public CompendiumPageType PageType;

        [Header("Effect")]
        [Tooltip("RunModifierStack key this page injects when consumed")]
        public string ModifierKey;
        [Tooltip("Modifier value (duration in seconds for Suppression, radius for Scout, etc.)")]
        public float ModifierValue;
        [Tooltip("Duration the modifier stays active. 0 = instant / permanent for district")]
        public float EffectDuration;

        [Header("Scout-Specific")]
        [Tooltip("Whether this page reveals forward gate destinations")]
        public bool RevealGates;
        [Tooltip("Whether this page reveals hidden POIs in current district")]
        public bool RevealHiddenPOIs;
        [Tooltip("Whether this page reveals the Front's current pattern")]
        public bool RevealFrontPattern;
        [Tooltip("Whether this page reveals echo locations")]
        public bool RevealEchoLocations;

        [Header("Suppression-Specific")]
        [Tooltip("Multiplier applied to Front advance speed (0.5 = half speed)")]
        public float FrontSlowMultiplier = 1f;
        [Tooltip("Faction ID to weaken (-1 = all factions)")]
        public int WeakenFactionId = -1;
        [Tooltip("Damage reduction applied to weakened faction (0.0-1.0)")]
        public float FactionDamageReduction;

        [Header("Insight-Specific")]
        [Tooltip("Whether this page reveals the next boss's weakness")]
        public bool RevealBossWeakness;
        [Tooltip("Whether this page reveals echo mutation types before entering")]
        public bool RevealEchoMutations;
        [Tooltip("Whether this page reveals reward chest contents before opening")]
        public bool RevealRewardContents;

        [Header("Source")]
        [Tooltip("Rarity tier for loot table weighting")]
        public PageRarity Rarity = PageRarity.Common;
    }

    public enum PageRarity : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2
    }
}
```

---

## Systems

### PageActivationSystem

```csharp
// File: Assets/Scripts/Compendium/Systems/PageActivationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: PageActivationRequest (transient entities), CompendiumLink, CompendiumPageState buffer
// Writes: CompendiumPageState buffer (remove consumed page), RunModifierStack (add effect)
//
// Flow:
//   1. Query all PageActivationRequest entities (manual EntityQuery, not SystemAPI.Query)
//   2. For each request, resolve player's CompendiumLink → compendium child entity
//   3. Find matching CompendiumPageState element by PageDefinitionId + SlotIndex
//   4. Validate page exists in inventory (reject stale / duplicate activations)
//   5. Look up CompendiumPageDefinitionSO via PageDefinitionId → extract modifier data
//   6. Push modifier into RunModifierStack with duration and effect key
//   7. For Scout pages: set reveal flags on DistrictRevealState (EPIC 4)
//   8. For Suppression pages: inject FrontSlowModifier + FactionWeakenModifier
//   9. For Insight pages: set reveal flags on BossInsightState / EchoInsightState
//  10. Remove the CompendiumPageState element from buffer (RemoveAt)
//  11. Destroy the request entity via ECB
//  12. Fire PageConsumedEvent for UI notification
```

### PageConsumeSystem

```csharp
// File: Assets/Scripts/Compendium/Systems/PageConsumeSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: PageActivationSystem
//
// Handles post-consumption cleanup and expiration of active page effects.
//
// Flow:
//   1. Track active page effects with remaining duration (PageActiveEffect buffer)
//   2. Decrement remaining duration by deltaTime each frame
//   3. When duration reaches 0, remove the corresponding RunModifierStack entry
//   4. Clear any reveal flags that were time-limited (Suppression Front slow)
//   5. Permanent effects (Scout reveals) are not tracked here — they persist for the district
```

### PageAcquisitionSystem

```csharp
// File: Assets/Scripts/Compendium/Systems/PageAcquisitionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: PageGrantEvent (transient entities from reward / loot systems)
// Writes: CompendiumPageState buffer on compendium child entity
//
// Flow:
//   1. Query PageGrantEvent entities
//   2. Resolve target player's CompendiumLink → compendium child entity
//   3. Check CompendiumPageState buffer length against CompendiumPageConfig.MaxTotalPages
//   4. If at capacity: reject (fire PageOverflowEvent for UI "inventory full" message)
//   5. Otherwise: append new CompendiumPageState element
//   6. Auto-assign SlotIndex if fewer than MaxActiveSlots are slotted, else SlotIndex = -1
//   7. Destroy event entity
//   8. Fire PageAcquiredEvent for UI notification
```

---

## Authoring

### CompendiumAuthoring

```csharp
// File: Assets/Scripts/Compendium/Authoring/CompendiumAuthoring.cs
// Added to the player prefab. Creates child entity with CompendiumPageState buffer.
// Baker:
//   1. CreateAdditionalEntity(TransformUsageFlags.None) → compendiumEntity
//   2. AddBuffer<CompendiumPageState>(compendiumEntity)
//   3. AddComponent<CompendiumLink>(playerEntity) with CompendiumEntity = compendiumEntity
//   4. If startingPages configured, bake PageDefinitionSO refs for runtime spawn
```

### CompendiumPageConfigAuthoring

```csharp
// File: Assets/Scripts/Compendium/Authoring/CompendiumPageConfigAuthoring.cs
// Placed on a singleton GameObject in the subscene.
// Baker:
//   1. AddComponent<CompendiumPageConfig> with MaxActiveSlots and MaxTotalPages from inspector
```

---

## Setup Guide

1. **Create `Assets/Scripts/Compendium/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/, Bridges/
2. **Create assembly definition** `Hollowcore.Compendium.asmdef` referencing `DIG.Shared`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`
3. Create page definitions: `Assets/Data/Compendium/Pages/` — at minimum one of each type (ScoutBasic, SuppressionBasic, InsightBasic)
4. Add `CompendiumAuthoring` to the player prefab
5. Add `CompendiumPageConfigAuthoring` singleton to the subscene with MaxActiveSlots=6, MaxTotalPages=10
6. Reimport subscene after adding authoring components
7. Verify child entity creation and buffer initialization in the Entity Inspector

---

## Verification

- [ ] CompendiumLink on player entity references a valid child entity
- [ ] Child entity has CompendiumPageState buffer with capacity 6
- [ ] CompendiumPageConfig singleton baked with correct values
- [ ] PageAcquisitionSystem adds pages to buffer from PageGrantEvent
- [ ] Page inventory respects MaxTotalPages cap; overflow rejected with event
- [ ] Auto-slot assignment fills empty active slots before overflow
- [ ] PageActivationSystem consumes page from buffer and injects RunModifierStack entry
- [ ] Scout page reveals forward gate info (RevealGates flag set on DistrictRevealState)
- [ ] Suppression page slows Front advance speed for configured duration
- [ ] Insight page reveals boss weakness flags
- [ ] PageConsumeSystem expires time-limited effects and removes RunModifierStack entry
- [ ] 16KB archetype limit not exceeded on player entity (CompendiumLink is only 8 bytes)
- [ ] No ghost replication errors — buffer lives on child entity, not player ghost

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Compendium/Components/CompendiumPageBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Compendium
{
    /// <summary>
    /// Blob representation of CompendiumPageDefinitionSO for Burst-compatible reads
    /// in PageActivationSystem. Avoids managed SO lookup on the hot path.
    /// </summary>
    public struct CompendiumPageBlob
    {
        public int PageId;
        public byte PageType; // CompendiumPageType as byte
        public FixedString64Bytes DisplayName;

        // Effect
        public BlobString ModifierKey;
        public float ModifierValue;
        public float EffectDuration;

        // Scout flags
        public bool RevealGates;
        public bool RevealHiddenPOIs;
        public bool RevealFrontPattern;
        public bool RevealEchoLocations;

        // Suppression
        public float FrontSlowMultiplier;
        public int WeakenFactionId;
        public float FactionDamageReduction;

        // Insight
        public bool RevealBossWeakness;
        public bool RevealEchoMutations;
        public bool RevealRewardContents;

        public byte Rarity; // PageRarity as byte
    }

    /// <summary>
    /// All page definitions indexed by PageId for O(1) lookup.
    /// Singleton blob on the CompendiumPageConfig entity.
    /// </summary>
    public struct CompendiumPageDatabase
    {
        public BlobArray<CompendiumPageBlob> Pages;
    }

    public struct CompendiumPageDatabaseRef : IComponentData
    {
        public BlobAssetReference<CompendiumPageDatabase> Value;
    }
}
```

```csharp
// File: Assets/Scripts/Compendium/Authoring/CompendiumPageConfigAuthoring.cs (BakeToBlob addition)
namespace Hollowcore.Compendium.Authoring
{
    // Add to existing CompendiumPageConfigAuthoring baker:
    //
    // 1. Gather all CompendiumPageDefinitionSO from project (Resources.LoadAll or SerializedField list)
    // 2. Build BlobBuilder:
    //    var builder = new BlobBuilder(Allocator.Temp);
    //    ref var root = ref builder.ConstructRoot<CompendiumPageDatabase>();
    //    var pages = builder.Allocate(ref root.Pages, definitions.Length);
    //    for each definition: populate CompendiumPageBlob fields
    //    builder.AllocateString(ref pages[i].ModifierKey, def.ModifierKey);
    // 3. AddBlobAsset to baker, add CompendiumPageDatabaseRef to config entity
    // 4. PageActivationSystem reads CompendiumPageDatabaseRef instead of managed SO lookup
}
```

---

## Validation

```csharp
// File: Assets/Editor/CompendiumWorkstation/CompendiumPageValidation.cs
namespace Hollowcore.Compendium.Editor
{
    /// <summary>
    /// Validates CompendiumPageDefinitionSO assets.
    /// </summary>
    // Rules:
    //   1. PageId must be unique across all CompendiumPageDefinitionSO assets
    //   2. DisplayName must be non-empty
    //   3. ModifierKey must be non-empty (needed for RunModifierStack injection)
    //   4. For Scout pages: at least one reveal flag must be true
    //   5. For Suppression pages: FrontSlowMultiplier must be in (0, 1] (0 = stops Front entirely, disallowed)
    //   6. For Suppression pages: FactionDamageReduction must be in [0, 1]
    //   7. For Insight pages: at least one reveal flag must be true
    //   8. EffectDuration must be >= 0 (0 = permanent for district)
    //   9. ModifierValue must be > 0
    //  10. Icon must not be null
    //
    // Invocation: OnPostprocessAllAssets + CompendiumWorkstation validation tab
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Compendium/Debug/CompendiumPageLiveTuning.cs
namespace Hollowcore.Compendium.Debug
{
    /// <summary>
    /// Runtime-editable page config for playtesting.
    /// </summary>
    // Tunable fields:
    //   - MaxActiveSlots (byte, min 1, max 10) — override slot count for testing
    //   - MaxTotalPages (byte, min 1, max 20) — override carry limit
    //   - EffectDurationMultiplier (float, min 0.1, max 10.0) — scale all page durations
    //   - GrantPageById (int) — immediately grant a page by ID to the local player
    //   - GrantAllPages (bool) — fill inventory with one of each page type
    //   - ClearAllPages (bool) — empty page inventory
    //
    // Pattern: static CompendiumPageLiveTuningOverrides, checked by PageAcquisitionSystem.
}
```
