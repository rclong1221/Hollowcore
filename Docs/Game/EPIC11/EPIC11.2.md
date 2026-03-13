# EPIC 11.2: Rival Trail Markers

**Status**: Planning
**Epic**: EPIC 11 — Rival Operators
**Priority**: High — Primary way players experience rival presence
**Dependencies**: EPIC 11.1 (Rival Definition & Simulation), EPIC 4 (Districts), EPIC 2 (Death/body persistence), EPIC 3 (Front system); Optional: EPIC 5 (Echoes), EPIC 12.1 (Scar Map data)

---

## Overview

Trail markers are the physical evidence rivals leave behind in the world. When the player enters a district, the TrailMarkerSystem checks rival simulation history for that district and stamps markers: bodies with lootable gear, cleared enemy paths, already-looted containers, rival-spawned echoes, and environmental signs of passage. This is the primary mechanism through which players perceive rival activity without direct encounters. Rivals also impact the Front — triggering alarms and advancing threat levels in districts they pass through.

---

## Component Definitions

### TrailMarkerType Enum

```csharp
// File: Assets/Scripts/Rivals/Components/TrailMarkerComponents.cs
namespace Hollowcore.Rivals
{
    public enum TrailMarkerType : byte
    {
        Body = 0,           // Dead rival member — lootable corpse with gear
        ClearedPath = 1,    // Enemies already killed in this zone
        LootedPOI = 2,      // Container already opened, vendor stock reduced
        RivalEcho = 3,      // Their uncompleted objective became an echo
        TrailSign = 4,      // Environmental: spent ammo, campsite, graffiti tag
        AlarmTriggered = 5  // Evidence that Front advanced here due to rival
    }
}
```

### TrailMarkerEntry (IBufferElementData)

Buffer on the district entity recording all rival trail markers for that district.

```csharp
// File: Assets/Scripts/Rivals/Components/TrailMarkerComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Buffer element describing a single trail marker left by a rival team.
    /// Stored on the district entity. Populated by TrailMarkerSystem on district entry.
    /// Read by zone generation, loot spawning, and Scar Map systems.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct TrailMarkerEntry : IBufferElementData
    {
        /// <summary>Which rival team left this marker.</summary>
        public int RivalDefinitionId;

        /// <summary>Zone within the district where the marker is placed.</summary>
        public int ZoneId;

        /// <summary>Type of evidence left behind.</summary>
        public TrailMarkerType MarkerType;

        /// <summary>
        /// Context-dependent metadata:
        /// - Body: equipment tier (loot quality)
        /// - ClearedPath: percentage of enemies removed (0-100)
        /// - LootedPOI: POI type ID that was looted
        /// - RivalEcho: echo definition ID
        /// - TrailSign: sign variant index (visual only)
        /// - AlarmTriggered: Front phases advanced
        /// </summary>
        public int Metadata;

        /// <summary>Unique marker ID for deduplication on re-entry.</summary>
        public int MarkerHash;
    }
}
```

### TrailMarkerConfig (IComponentData)

Singleton tuning data for trail marker generation.

```csharp
// File: Assets/Scripts/Rivals/Components/TrailMarkerComponents.cs
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Global configuration for trail marker generation.
    /// Singleton entity, set via TrailMarkerConfigAuthoring.
    /// </summary>
    public struct TrailMarkerConfig : IComponentData
    {
        /// <summary>Max bodies placed per dead rival member in a district.</summary>
        public int MaxBodiesPerDistrict;

        /// <summary>Percentage of zone enemies removed by ClearedPath (0.0-1.0).</summary>
        public float ClearedPathEnemyReduction;

        /// <summary>Probability that a rival failure generates an echo.</summary>
        public float EchoSpawnChance;

        /// <summary>Max trail sign markers per district (cosmetic cap).</summary>
        public int MaxTrailSignsPerDistrict;

        /// <summary>Front phases advanced per alarm trigger.</summary>
        public int FrontAdvancePerAlarm;
    }
}
```

### RivalBodyLoot (IComponentData)

Placed on spawned rival body entities for loot integration.

```csharp
// File: Assets/Scripts/Rivals/Components/TrailMarkerComponents.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Marks a world entity as a lootable rival body.
    /// Loot system reads RivalDefinitionId + EquipmentTier to generate drops.
    /// Follows EPIC 2 body persistence pattern.
    /// </summary>
    public struct RivalBodyLoot : IComponentData
    {
        public int RivalDefinitionId;
        public int EquipmentTier;
        public FixedString64Bytes RivalTeamName;

        /// <summary>Zone where body was placed (for Scar Map marker).</summary>
        public int ZoneId;

        /// <summary>Whether the body has been looted by the player.</summary>
        public bool IsLooted;
    }
}
```

---

## Systems

### TrailMarkerSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/TrailMarkerSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DistrictEntrySystem (EPIC 4)
//
// Triggers on player district entry:
//   1. Query RivalOutcomeEntry buffers for all rivals that visited this district
//   2. For each rival outcome in the district:
//      a. Skip if markers already stamped (check MarkerHash deduplication)
//      b. Based on outcome type, generate TrailMarkerEntry entries:
//         - TeamWiped/LostMember → Body markers at EventZoneId
//         - ClearedEnemies → ClearedPath markers at traversed zones
//         - LootedPOIs → LootedPOI markers at POI zone positions
//         - TriggeredAlarm → AlarmTriggered marker + advance Front via FrontSystem
//         - Failed objectives → RivalEcho markers (probability roll)
//      c. Add TrailSign markers along the rival's path through district
//   3. Append all markers to district entity's TrailMarkerEntry buffer
//   4. Fire TrailMarkersStampedEvent for zone generation integration
```

### TrailMarkerSpawnSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/TrailMarkerSpawnSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: TrailMarkerSystem
//
// Converts trail marker data into world entities:
//   1. For each new Body marker:
//      a. Spawn rival body entity at zone position (deterministic from seed + zoneId)
//      b. Add RivalBodyLoot component with tier and definition reference
//      c. Add loot table reference from RivalOperatorSO.EquipmentTier
//      d. Add interactable component for player pickup (Loot/ framework)
//   2. For each new ClearedPath marker:
//      a. Flag zone's enemy spawn list for reduction (EnemySpawnReduction component)
//      b. Spawn environmental evidence (dead enemy bodies, opened doors)
//   3. For each new LootedPOI marker:
//      a. Set container entity to already-opened state
//      b. Reduce vendor stock if applicable
//   4. For each new TrailSign marker:
//      a. Spawn cosmetic prop entity (spent ammo casings, campsite, graffiti)
//      b. Variant selected from seed + marker position
```

### TrailMarkerFrontImpactSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/TrailMarkerFrontImpactSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: TrailMarkerSystem
// UpdateBefore: FrontAdvanceSystem (EPIC 3)
//
// Applies Front advancement from rival alarm triggers:
//   1. For each AlarmTriggered trail marker in the current district:
//      a. Read FrontAdvancePerAlarm from TrailMarkerConfig
//      b. Call into FrontSystem to advance district Front phase
//      c. If Front crosses a phase threshold, trigger bleed to adjacent districts
//   2. This means entering a rival-alarmed district feels MORE dangerous
//      — the player discovers the Front is worse than expected
```

---

## Setup Guide

1. **Add TrailMarkerComponents.cs** to `Assets/Scripts/Rivals/Components/`
2. **Add TrailMarkerConfigAuthoring** MonoBehaviour to expedition manager prefab with tuning values:
   - MaxBodiesPerDistrict: 3
   - ClearedPathEnemyReduction: 0.3 (30% fewer enemies)
   - EchoSpawnChance: 0.25
   - MaxTrailSignsPerDistrict: 5
   - FrontAdvancePerAlarm: 1
3. **Create rival body prefab**: `Assets/Prefabs/Rivals/RivalBody.prefab` with interactable + loot source components
4. **Create trail sign prefab variants**: 3-4 cosmetic props (ammo casings, campsite, graffiti)
5. **Wire TrailMarkerSystem** to fire after DistrictEntrySystem detects player entering a new district
6. **Wire TrailMarkerFrontImpactSystem** before FrontAdvanceSystem to ensure alarm impacts apply before player sees Front state
7. **Add assembly reference** to `Hollowcore.Front` (EPIC 3) and `Hollowcore.Loot` (framework)

---

## Verification

- [ ] TrailMarkerEntry buffer populated on district entity when rival has visited
- [ ] Body markers spawn at correct zone positions (seed-deterministic)
- [ ] Rival body entities have RivalBodyLoot with correct tier and definition
- [ ] Bodies are interactable and produce loot matching EquipmentTier
- [ ] ClearedPath zones have reduced enemy count (30% default)
- [ ] LootedPOI containers show as already-opened
- [ ] Trail signs spawn as cosmetic props along rival traversal path
- [ ] Alarm-triggered districts show advanced Front phase on entry
- [ ] Front bleed propagates correctly from rival-alarmed districts
- [ ] Markers are deduplicated on district re-entry (MarkerHash check)
- [ ] RivalEcho markers only spawn with configured probability
- [ ] No markers generated for districts with no rival history
- [ ] Scar Map receives RivalMarker events from trail marker generation (12.1 integration)

---

## BlobAsset Pipeline

TrailMarkerConfig is a singleton baked from authoring. Already an IComponentData — no blob needed. However, trail sign variant data (cosmetic prop prefab indices, placement rules) benefits from blob storage.

```csharp
// File: Assets/Scripts/Rivals/Blobs/TrailMarkerConfigBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Blob for trail sign variant data. Maps variant index to placement rules.
    /// TrailMarkerSpawnSystem reads this for cosmetic prop selection.
    /// </summary>
    public struct TrailSignVariantBlob
    {
        public int VariantIndex;
        public BlobString PropName;       // "SpentAmmo", "Campsite", "GraffitiTag", "BloodTrail"
        public float SpawnWeight;          // Weighted random selection
        public float MinDistFromPrevious;  // Minimum distance between same-variant signs in a zone
    }

    public struct TrailSignDatabase
    {
        public BlobArray<TrailSignVariantBlob> Variants;
    }

    public struct TrailSignDatabaseRef : IComponentData
    {
        public BlobAssetReference<TrailSignDatabase> Value;
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Rivals/Components/TrailMarkerComponents.cs (append validation note)
// TrailMarkerConfigAuthoring OnValidate:

#if UNITY_EDITOR
    // Add to TrailMarkerConfigAuthoring MonoBehaviour:
    private void OnValidate()
    {
        if (MaxBodiesPerDistrict < 0)
            Debug.LogError($"[TrailMarkerConfig] MaxBodiesPerDistrict cannot be negative.", this);
        if (ClearedPathEnemyReduction < 0f || ClearedPathEnemyReduction > 1f)
            Debug.LogError($"[TrailMarkerConfig] ClearedPathEnemyReduction must be 0-1, got {ClearedPathEnemyReduction}.", this);
        if (EchoSpawnChance < 0f || EchoSpawnChance > 1f)
            Debug.LogError($"[TrailMarkerConfig] EchoSpawnChance must be 0-1.", this);
        if (MaxTrailSignsPerDistrict < 0)
            Debug.LogError($"[TrailMarkerConfig] MaxTrailSignsPerDistrict cannot be negative.", this);
        if (FrontAdvancePerAlarm < 0 || FrontAdvancePerAlarm > 3)
            Debug.LogWarning($"[TrailMarkerConfig] FrontAdvancePerAlarm={FrontAdvancePerAlarm} — values > 2 may feel punishing.", this);
    }
#endif
```

```csharp
// File: Assets/Editor/Rivals/TrailMarkerBuildValidator.cs
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hollowcore.Rivals.Editor
{
    public class TrailMarkerBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 11;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Validate trail sign prefab variants exist
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Rivals" });
            if (prefabGuids.Length == 0)
                Debug.LogWarning("[TrailMarkerBuildValidation] No rival prefabs found in Assets/Prefabs/Rivals/");

            // Validate rival body prefab has interactable + loot components
            var bodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rivals/RivalBody.prefab");
            if (bodyPrefab == null)
                Debug.LogError("[TrailMarkerBuildValidation] Missing RivalBody.prefab");
        }
    }
}
```

---

## Editor Tooling — Trail Marker Placement Tool

```csharp
// File: Assets/Editor/RivalWorkstation/TrailMarkerPlacementTool.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Rivals.Editor
{
    /// <summary>
    /// Trail Marker Placement Tool — scene view overlay for district maps.
    /// Features:
    ///   - Zone map visualization: shows district zones with rival traversal paths
    ///   - Marker placement preview: given a RivalOperatorSO + outcome sequence,
    ///     preview where bodies, cleared paths, and trail signs would spawn
    ///   - Density heatmap: color-code zones by marker density (red = cluttered, blue = sparse)
    ///   - Position bounds validation: warn if markers would spawn outside zone geometry
    ///   - "Simulate & Preview" button: run rival sim for one district, show all markers
    ///
    /// Integrated into Rival Designer as a sub-tab.
    /// </summary>
    public class TrailMarkerPlacementTool : EditorWindow
    {
        [MenuItem("Hollowcore/Trail Marker Tool")]
        public static void Open() => GetWindow<TrailMarkerPlacementTool>("Trail Markers");
    }
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Rivals/Debug/TrailMarkerLiveTuning.cs
namespace Hollowcore.Rivals.Debug
{
    /// <summary>
    /// Runtime-tunable trail marker parameters:
    ///   - TrailMarkerConfig.MaxBodiesPerDistrict (int)
    ///   - TrailMarkerConfig.ClearedPathEnemyReduction (float, 0-1)
    ///   - TrailMarkerConfig.EchoSpawnChance (float, 0-1)
    ///   - TrailMarkerConfig.MaxTrailSignsPerDistrict (int)
    ///   - TrailMarkerConfig.FrontAdvancePerAlarm (int)
    ///
    /// Pattern: TrailMarkerLiveTuningSystem writes to TrailMarkerConfig singleton.
    /// Changes apply on next district entry (markers re-stamped from rival history).
    /// Exposed via console: /tune trail.bodies 5, /tune trail.reduction 0.5
    /// </summary>
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Rivals/Debug/TrailMarkerDebugOverlay.cs
namespace Hollowcore.Rivals.Debug
{
    /// <summary>
    /// Debug overlay for trail markers (development builds):
    /// - Zone map: marker type icons at spawn positions
    ///   - Skull = Body, Broom = ClearedPath, Chest = LootedPOI,
    ///     Spiral = RivalEcho, Footprint = TrailSign, Alarm = AlarmTriggered
    /// - Color-coded by rival team (matches team icon color)
    /// - Cleared path zones shaded with enemy reduction percentage
    /// - Alarm districts highlighted with Front advance amount
    /// - Marker count per zone (density number overlay)
    /// - Toggle: /debug rivals markers
    /// </summary>
}
```

---

## Simulation & Testing

Trail marker coverage is analyzed as part of the Rival Simulation Tester (EPIC 11.1):
- **Marker density per district**: average markers of each type across 1000 expeditions. Validates that marker distribution feels organic (not clustered or sparse)
- **Body placement distribution**: which zones accumulate bodies most often. Ensures lootable bodies appear in traversal-adjacent areas (findable, not hidden)
- **Cleared path impact**: average enemy reduction experienced by players entering rival-visited districts. Target: 10-30% reduction feels meaningful without trivializing combat
- **Alarm cascade analysis**: how many total Front phases are advanced by rival alarms per expedition. Target: 0-2 phases on average (noticeable but not catastrophic)
- **Echo spawn rate validation**: percentage of rival failures that generate echoes. At 25% spawn chance, expect ~0.5 rival echoes per expedition
