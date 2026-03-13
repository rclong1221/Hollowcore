# EPIC 15.1: Influence Meter System

**Status**: Planning
**Epic**: EPIC 15 — Final Bosses & Endgame
**Dependencies**: EPIC 4 (District Graph, district completion tracking); EPIC 14 (Boss System); EPIC 12 (Scar Map)

---

## Overview

Three influence meters -- Infrastructure, Transmission, and Market -- track which power faction the player has most disrupted throughout the expedition. Each district cleared contributes to one or two meters. The highest meter at expedition end determines which of three final bosses the player faces. Players can see their influence balance on the Scar Map and Gate Screen, enabling strategic district selection to target a preferred final boss.

---

## Component Definitions

### InfluenceFaction Enum

```csharp
// File: Assets/Scripts/Boss/Components/InfluenceComponents.cs
namespace Hollowcore.Boss
{
    /// <summary>
    /// The three power factions controlling the city.
    /// Each maps to a final boss.
    /// </summary>
    public enum InfluenceFaction : byte
    {
        Infrastructure = 0, // The Architect
        Transmission = 1,   // The Signal
        Market = 2          // The Board
    }
}
```

### InfluenceMeterState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/InfluenceComponents.cs (continued)
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss
{
    /// <summary>
    /// Tracks the three faction influence meters for the current expedition.
    /// Stored on the run state entity (Roguelite/ RunLifecycleSystem).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InfluenceMeterState : IComponentData
    {
        /// <summary>Infrastructure influence (The Architect).</summary>
        [GhostField(Quantization = 10)] public float Infrastructure;

        /// <summary>Transmission influence (The Signal).</summary>
        [GhostField(Quantization = 10)] public float Transmission;

        /// <summary>Market influence (The Board).</summary>
        [GhostField(Quantization = 10)] public float Market;

        /// <summary>ID of the most recently cleared district (for tie-breaking).</summary>
        [GhostField] public int LastClearedDistrictId;

        /// <summary>Total districts cleared this expedition.</summary>
        [GhostField] public byte DistrictsClearedCount;

        public float GetFaction(InfluenceFaction faction) => faction switch
        {
            InfluenceFaction.Infrastructure => Infrastructure,
            InfluenceFaction.Transmission => Transmission,
            InfluenceFaction.Market => Market,
            _ => 0f
        };

        public void AddFaction(InfluenceFaction faction, float amount)
        {
            switch (faction)
            {
                case InfluenceFaction.Infrastructure: Infrastructure += amount; break;
                case InfluenceFaction.Transmission: Transmission += amount; break;
                case InfluenceFaction.Market: Market += amount; break;
            }
        }

        /// <summary>
        /// Returns the dominant faction. Tie-break uses LastClearedDistrictId.
        /// </summary>
        public InfluenceFaction GetDominantFaction(DistrictInfluenceMapSO districtMap)
        {
            float max = Infrastructure;
            InfluenceFaction dominant = InfluenceFaction.Infrastructure;

            if (Transmission > max)
            {
                max = Transmission;
                dominant = InfluenceFaction.Transmission;
            }
            if (Market > max)
            {
                dominant = InfluenceFaction.Market;
            }
            else if (Market == max && max == Transmission)
            {
                // Three-way tie or two-way tie: use last district's primary faction
                dominant = districtMap.GetPrimaryFaction(LastClearedDistrictId);
            }

            return dominant;
        }
    }
}
```

### InfluenceContribution (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/InfluenceComponents.cs (continued)
namespace Hollowcore.Boss
{
    /// <summary>
    /// History buffer tracking each district's influence contribution.
    /// On the run state entity. Used by summary screen to show timeline.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct InfluenceContribution : IBufferElementData
    {
        public int DistrictId;
        public InfluenceFaction PrimaryFaction;
        public float PrimaryAmount;
        public InfluenceFaction SecondaryFaction; // Some districts contribute to two factions
        public float SecondaryAmount;             // 0 = no secondary contribution
    }
}
```

---

## ScriptableObject Definitions

### DistrictInfluenceMapSO

```csharp
// File: Assets/Scripts/Boss/Definitions/DistrictInfluenceMapSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Boss.Definitions
{
    [CreateAssetMenu(fileName = "DistrictInfluenceMap", menuName = "Hollowcore/Boss/District Influence Map")]
    public class DistrictInfluenceMapSO : ScriptableObject
    {
        [Tooltip("Configurable mapping from district to influence contributions")]
        public List<DistrictInfluenceEntry> Entries = new();

        [Header("Final Boss Thresholds")]
        [Tooltip("Minimum districts cleared before final boss is available")]
        public int MinDistrictsForFinalBoss = 5;
        public int MaxDistrictsForFinalBoss = 7;

        public InfluenceFaction GetPrimaryFaction(int districtId)
        {
            foreach (var entry in Entries)
                if (entry.DistrictId == districtId)
                    return entry.PrimaryFaction;
            return InfluenceFaction.Infrastructure; // fallback
        }
    }

    [System.Serializable]
    public class DistrictInfluenceEntry
    {
        public int DistrictId;
        public string DistrictName;
        public InfluenceFaction PrimaryFaction;
        public float PrimaryAmount = 1f;
        [Tooltip("Optional secondary faction contribution (0 = none)")]
        public InfluenceFaction SecondaryFaction;
        public float SecondaryAmount;
    }
}
```

---

## District-to-Influence Mapping

| District | Primary Faction | Primary Amt | Secondary Faction | Secondary Amt |
|---|---|---|---|---|
| Lattice | Infrastructure | 1.0 | — | — |
| Burn | Infrastructure | 1.0 | — | — |
| Auction | Infrastructure | 0.7 | Market | 0.5 |
| Chrome Cathedral | Transmission | 1.0 | — | — |
| Nursery | Transmission | 1.0 | — | — |
| Synapse Row | Transmission | 1.0 | — | — |
| Wetmarket | Market | 1.0 | — | — |
| Mirrortown | Market | 1.0 | — | — |
| Necrospire | Transmission | 0.5 | Infrastructure | 0.5 |
| Glitch Quarter | Transmission | 0.7 | Market | 0.3 |
| Shoals | Market | 0.7 | Infrastructure | 0.3 |
| Quarantine | Infrastructure | 0.5 | Transmission | 0.5 |
| Old Growth | Infrastructure | 0.7 | Market | 0.3 |
| Deadwave | Transmission | 0.5 | Market | 0.5 |
| Skyfall | Infrastructure | 1.0 | — | — |

---

## Systems

### InfluenceUpdateSystem

```csharp
// File: Assets/Scripts/Boss/Systems/InfluenceUpdateSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Updates influence meters when a district is completed.
//
// When DistrictCompletionEvent fires (from EPIC 14.5):
//   1. Look up district ID in DistrictInfluenceMapSO
//   2. Add primary faction contribution to InfluenceMeterState
//   3. If secondary contribution > 0: add secondary faction contribution
//   4. Append InfluenceContribution to history buffer
//   5. Update LastClearedDistrictId and DistrictsClearedCount
//   6. Fire InfluenceMeterChangedEvent for UI update
```

### InfluenceMeterSystem

```csharp
// File: Assets/Scripts/Boss/Systems/InfluenceMeterSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: InfluenceUpdateSystem
//
// Determines final boss selection and manages meter display state.
//
// Each frame (lightweight — early out if no change):
//   1. Read InfluenceMeterState
//   2. If DistrictsClearedCount >= MinDistrictsForFinalBoss:
//      a. Calculate dominant faction
//      b. Store selected final boss ID on run state entity
//      c. Fire FinalBossSelectedEvent if changed
//   3. Calculate normalized meter values (0-1) for UI display
//   4. Determine "trending toward" faction for Gate Screen hints
```

### InfluenceMeterUIBridge

```csharp
// File: Assets/Scripts/Boss/Bridges/InfluenceMeterUIBridge.cs
// Managed MonoBehaviour — bridges InfluenceMeterState to UI displays.
//
// Scar Map display:
//   1. Three meter bars (Infrastructure, Transmission, Market)
//   2. Color-coded: Infrastructure=orange, Transmission=blue, Market=green
//   3. Dominant faction highlighted
//   4. Per-district nodes show their contribution breakdown
//
// Gate Screen display:
//   1. Small meter preview showing current balance
//   2. For each available gate: show projected influence change if that district is chosen
//   3. "Trending toward: [Boss Name]" hint text
//
// Tooltip: hovering a meter shows which districts contributed and by how much
```

---

## Setup Guide

1. Create InfluenceComponents.cs in `Assets/Scripts/Boss/Components/`
2. Create DistrictInfluenceMapSO in `Assets/Data/Boss/InfluenceMap.asset`
   - Configure all 15 districts with primary/secondary contributions
3. Add InfluenceMeterState component to run state entity (Roguelite/ RunLifecycleSystem init)
4. Add InfluenceContribution buffer to run state entity
5. Wire InfluenceUpdateSystem to consume DistrictCompletionEvent from EPIC 14.5
6. Create influence meter UI elements:
   - Scar Map overlay: three meter bars + per-node contribution markers
   - Gate Screen widget: preview meters + projected changes
7. Configure MinDistrictsForFinalBoss (default 5) in DistrictInfluenceMapSO
8. Wire final boss selection to EPIC 15.2/15.3/15.4 boss encounter triggers

---

## Verification

- [ ] InfluenceMeterState initializes to zero on expedition start
- [ ] District completion adds correct primary influence
- [ ] District completion adds correct secondary influence (e.g., Auction adds both Infrastructure and Market)
- [ ] InfluenceContribution history buffer records each district
- [ ] GetDominantFaction returns correct faction for clear winner
- [ ] Tie-breaking uses LastClearedDistrictId primary faction
- [ ] Scar Map displays three meter bars with correct values
- [ ] Gate Screen shows projected influence changes for available districts
- [ ] "Trending toward" hint updates as meters change
- [ ] Final boss selection triggers at MinDistrictsForFinalBoss threshold
- [ ] Final boss selection matches dominant faction
- [ ] InfluenceMeterChangedEvent fires only on actual change
- [ ] Normalized meter values work for UI (0-1 range)

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Boss/Authoring/InfluenceMeterBlobBaker.cs
namespace Hollowcore.Boss.Authoring
{
    // DistrictInfluenceMapSO → InfluenceMapBlob (singleton entity)
    //   BlobArray<InfluenceEntryBlob> Entries
    //     - int DistrictId
    //     - InfluenceFaction PrimaryFaction
    //     - float PrimaryAmount
    //     - InfluenceFaction SecondaryFaction
    //     - float SecondaryAmount
    //   int MinDistrictsForFinalBoss
    //   int MaxDistrictsForFinalBoss
    //
    // InfluenceMeterSO → InfluenceBlob (if separate per-meter tuning SO exists)
    //   BlobArray<float2> ThresholdCurve  // (districtsClearedNormalized, meterValue) for UI display
    //   float NormalizationMax            // maximum expected meter value for UI scaling
    //
    // Baked by InfluenceMapAuthoring on a singleton prefab in the run state subscene.
    // InfluenceUpdateSystem reads blob at runtime instead of loading SO.
}
```

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/InfluenceMeterValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Validation rules for DistrictInfluenceMapSO:
    //
    // 1. Influence meter threshold bounds:
    //    PrimaryAmount for each entry must be in (0, 5.0].
    //    SecondaryAmount must be in [0, 5.0].
    //    Error if negative. Warning if > 3.0 (single district dominates faction).
    //
    // 2. All 15 districts must be represented in Entries.
    //    Warning if any DistrictId missing (district has no influence contribution).
    //
    // 3. Final boss prerequisite chain — all 3 influence meters must be reachable:
    //    Simulate all possible 5-7 district expedition paths.
    //    Verify that for each InfluenceFaction, there exists at least one valid path
    //    of MinDistrictsForFinalBoss districts where that faction is dominant.
    //    Error if any faction is unreachable (players can never face that final boss).
    //
    // 4. Balance check:
    //    Sum total influence per faction across all districts.
    //    Warning if any faction's total is < 50% or > 200% of another's
    //    (significant imbalance makes one boss much more/less likely).
    //
    // 5. Tie-breaking coverage:
    //    For every district, GetPrimaryFaction must return a valid faction.
    //    Error if any district has no Entries match.
    //
    // 6. MinDistrictsForFinalBoss must be >= 3 and <= 10.
    //    MaxDistrictsForFinalBoss must be >= MinDistrictsForFinalBoss.
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/BossWorkstation/InfluenceMeterDesignerModule.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // InfluenceMeterDesignerModule : IWorkstationModule — tab in BossDesignerWorkstation
    //
    // [1] Influence Map Overview
    //     - Table: rows = districts, columns = Infrastructure / Transmission / Market
    //     - Cells show contribution amounts, color intensity = magnitude
    //     - Row total shows which faction each district primarily feeds
    //     - Column total shows overall faction reachability
    //
    // [2] Threshold Visualization
    //     - Three horizontal bars (one per faction) showing cumulative meter range
    //     - Overlay: vertical markers at MinDistrictsForFinalBoss and MaxDistrictsForFinalBoss
    //     - Shaded region shows "final boss trigger zone" on each meter
    //
    // [3] Path Simulator
    //     - Dropdown: select 5-7 districts as a test expedition path
    //     - Live preview: three meter bars fill as districts are "cleared" in sequence
    //     - Shows which final boss would be selected at each step
    //     - "Trending toward" indicator updates live
    //     - "Random path" button: simulate 1000 random paths, show faction distribution pie chart
    //
    // [4] Balance Dashboard
    //     - Faction reachability percentage (from 1000 random path simulations)
    //     - Target: each faction reachable in 25-40% of paths
    //     - Red/yellow/green indicators per faction
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/InfluenceLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Influence meter live tuning:
    //
    //   float InfrastructureOverride  // -1 = use computed, >=0 = force meter value
    //   float TransmissionOverride
    //   float MarketOverride
    //   InfluenceFaction ForceFinalBoss  // 255 = auto, 0/1/2 = force specific faction
    //   int ForceDistrictsClearedCount   // -1 = use actual, >=0 = override threshold check
    //   bool ShowMeterDebugUI            // always show meter values on screen
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/InfluenceMeterDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Influence meter debug overlay (enabled via `influence_debug 1`):
    //
    // [1] Meter Bars
    //     - Three vertical bars in screen corner: Infrastructure (orange), Transmission (blue), Market (green)
    //     - Numeric values displayed
    //     - Dominant faction highlighted with crown icon
    //
    // [2] Contribution History
    //     - Scrollable list of InfluenceContribution buffer entries
    //     - Each entry: district name, faction, amount, running total
    //
    // [3] Final Boss Prediction
    //     - "Trending: The Architect" text with faction icon
    //     - Distance to second-place faction: "Lead: +0.5 over Transmission"
    //     - "Districts until final boss: 2" countdown
}
