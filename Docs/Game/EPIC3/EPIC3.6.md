# EPIC 3.6: District Bleed

**Status**: Planning
**Epic**: EPIC 3 — The Front (District Pressure System)
**Priority**: High — Uncontained Fronts spread to neighbors
**Dependencies**: EPIC 3.1 (FrontState, FrontPhase, FrontZoneData); EPIC 3.2 (FrontAdvanceSystem, FrontOffScreenSimulationSystem); EPIC 4.1 (ExpeditionGraphState, DistrictNode, GateConnection — adjacency graph)
**Optional**: EPIC 3.4 (containment stops bleed)

---

## Overview

When a district's Front reaches Phase 3 or higher, it begins bleeding into adjacent districts through the expedition graph's gate connections. On each gate transition, BleedSystem increments a BleedCounter on qualifying districts. When the counter crosses a threshold, Front hazards appear in the neighbor district's border zones — smaller and weaker than the source, but unexpected and potentially route-cutting. Bleed is thematic: Quarantine leaks plague spores, Old Growth creeps tendrils, Deadwave eats tech, The Burn radiates heat. Containment in the source district stops bleed, bleed zones fade over time, and some side goals in the affected district can cleanse border contamination directly. Compound bleed (3+ Phase 4 districts) accelerates expedition graph deterioration.

---

## Component Definitions

### BleedZoneState (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/BleedComponents.cs
using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

namespace Hollowcore.Front
{
    /// <summary>
    /// Applied to individual zone entries in a district's FrontZoneData buffer
    /// when bleed from an adjacent district contaminates border zones.
    /// Stored as a separate buffer to avoid modifying FrontZoneData semantics.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BleedZoneEntry : IBufferElementData
    {
        /// <summary>Zone ID in this (target) district that is contaminated.</summary>
        public int ZoneId;

        /// <summary>Entity of the source district causing the bleed.</summary>
        public Entity SourceDistrictEntity;

        /// <summary>Thematic restriction type inherited from source district's Front.</summary>
        public FrontRestriction BleedRestriction;

        /// <summary>Intensity of bleed contamination (0.0 = clean, 1.0 = full bleed).</summary>
        public float Intensity;

        /// <summary>DPS applied to entities in this bleed zone (fraction of source Front's hazard DPS).</summary>
        public float BleedDPS;

        /// <summary>True if this bleed zone is currently fading (source contained or cleansed).</summary>
        public bool IsFading;

        /// <summary>Fade progress (0.0 = full bleed, 1.0 = fully cleansed). Only advances when IsFading.</summary>
        public float FadeProgress;
    }
}
```

### BleedSourceLink (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/BleedComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Tracks which source districts are actively bleeding into this district.
    /// Buffer on target district entity. Used for compound bleed detection.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct BleedSourceLink : IBufferElementData
    {
        /// <summary>Entity of the source district.</summary>
        public Entity SourceEntity;

        /// <summary>Source district's Front definition ID (for thematic mapping).</summary>
        public int SourceFrontDefinitionId;

        /// <summary>Current bleed counter for this source.</summary>
        public int BleedCounter;

        /// <summary>Threshold at which bleed zones appear.</summary>
        public int BleedThreshold;

        /// <summary>True if source is contained (bleed should begin fading).</summary>
        public bool SourceContained;
    }
}
```

### BleedConfig (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/BleedComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Singleton config for bleed mechanics. Created by FrontConfigAuthoring.
    /// </summary>
    public struct BleedConfig : IComponentData
    {
        /// <summary>BleedCounter increment per gate transition for Phase 3 districts.</summary>
        public int Phase3IncrementPerTransition;

        /// <summary>BleedCounter increment per gate transition for Phase 4 districts.</summary>
        public int Phase4IncrementPerTransition;

        /// <summary>Counter threshold to trigger bleed zone appearance.</summary>
        public int DefaultBleedThreshold;

        /// <summary>Number of border zones contaminated per threshold crossing.</summary>
        public int ZonesPerBleedEvent;

        /// <summary>DPS in bleed zones as fraction of source district's Hostile DPS.</summary>
        public float BleedDPSFraction;

        /// <summary>Time in seconds for a bleed zone to fully fade once source is contained.</summary>
        public float BleedFadeDuration;

        /// <summary>Number of Phase 4 districts required for compound bleed escalation.</summary>
        public int CompoundBleedThreshold;

        /// <summary>Rate multiplier applied to ALL district Fronts during compound bleed.</summary>
        public float CompoundBleedRateMultiplier;
    }
}
```

### CompoundBleedActive (IComponentData, IEnableableComponent)

```csharp
// File: Assets/Scripts/Front/Components/BleedComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Singleton enableable flag set when compound bleed conditions are met
    /// (3+ Phase 4 districts). Triggers expedition-wide acceleration.
    /// Baked disabled on expedition graph entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CompoundBleedActive : IComponentData, IEnableableComponent
    {
        /// <summary>Number of Phase 4 districts currently contributing.</summary>
        [GhostField] public int Phase4DistrictCount;
    }
}
```

---

## Thematic Bleed Mapping

Each district's Front produces a unique bleed flavor in adjacent districts:

| Source District | Front Name | Bleed Flavor | BleedRestriction | Visual Description |
|---|---|---|---|---|
| The Quarantine | Outbreak Surge | Plague spores | BiohazardFog | Greenish fog, coughing NPCs |
| Old Growth | Bloom Propagation | Creeping tendrils | CollapseDebris | Organic roots blocking paths |
| Deadwave | Silence Expansion | Tech death zone | EMPZone | Flickering lights, dead screens |
| The Burn | Overheat Cascade | Heat radiation | RadStorm | Heat shimmer, scorched ground |
| Necrospire | Corruption Bloom | Data corruption | CognitiveStatic | Glitched geometry, false signals |
| Wetmarket | Waterline Rise | Seeping flood | FloodedZone | Shallow water, rusted surfaces |
| Glitch Quarter | Reality Desync | Physics glitches | CognitiveStatic | Floating debris, distorted space |
| Chrome Cathedral | Choir Crescendo | Sound pressure | CognitiveStatic | Ear-ringing, distorted audio |
| The Shoals | Tide Swell | Encroaching water | FloodedZone | Rising water, strong currents |
| Mirrortown | Identity Drift | Identity confusion | CognitiveStatic | NPC face-swaps, wrong names |
| The Lattice | Structural Failure | Debris collapse | CollapseDebris | Falling rubble, blocked routes |
| Synapse Row | Cognitive Overload | Memetic pressure | CognitiveStatic | Visual noise, false UI elements |
| The Auction | Market Volatility | Price inflation | None | Economic penalties, not physical |
| The Nursery | Consciousness Cascade | Rogue AI | EMPZone | Hostile turrets, locked doors |
| Skyfall Ruins | Systems Reboot | Security drones | HunterPacks | Patrol drones at borders |

---

## Systems

### BleedSystem

```csharp
// File: Assets/Scripts/Front/Systems/BleedSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: FrontOffScreenSimulationSystem (EPIC 3.2)
//
// Increments bleed counters and creates bleed zones on gate transitions.
// Triggered by district change (same event as FrontOffScreenSimulationSystem).
//
// On gate transition:
//   1. Read BleedConfig singleton
//   2. For each district entity with FrontState where Phase >= Phase3_Crisis:
//      a. Find all adjacent districts via ExpeditionGraphState edge list
//      b. For each adjacent target district:
//         i.  Find or create BleedSourceLink entry in target's buffer
//         ii. Increment BleedCounter by Phase3IncrementPerTransition (Phase 3)
//             or Phase4IncrementPerTransition (Phase 4)
//         iii. If BleedCounter >= BleedThreshold:
//              - Select border zones in target district (zones adjacent to gate)
//              - For each selected zone (up to ZonesPerBleedEvent):
//                * Add BleedZoneEntry with:
//                  - BleedRestriction from thematic mapping
//                  - BleedDPS = source Hostile DPS * BleedDPSFraction
//                  - Intensity = 1.0
//                  - IsFading = false
//              - Reset BleedCounter (or subtract threshold for escalating bleed)
//   3. Check containment:
//      a. For each BleedSourceLink where SourceEntity's FrontState.IsContained == true:
//         - Set SourceContained = true
//         - Mark all matching BleedZoneEntry entries as IsFading = true
```

### BleedFadeSystem

```csharp
// File: Assets/Scripts/Front/Systems/BleedFadeSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BleedSystem
//
// Fades bleed zones once their source is contained or cleansed.
//
// For each district entity with BleedZoneEntry buffer:
//   1. For each BleedZoneEntry where IsFading == true:
//      a. FadeProgress += deltaTime / BleedConfig.BleedFadeDuration
//      b. Intensity = 1.0 - FadeProgress
//      c. BleedDPS = baseDPS * Intensity
//      d. If FadeProgress >= 1.0:
//         - Remove BleedZoneEntry (zone is clean)
//   2. If all BleedZoneEntry entries for a given SourceEntity are removed:
//      a. Remove corresponding BleedSourceLink entry
```

### BleedHazardDamageSystem

```csharp
// File: Assets/Scripts/Front/Systems/BleedHazardDamageSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: DamageSystemGroup (PredictedFixedStepSimulation)
//
// Applies bleed zone DPS to entities (players and AI) within bleed-contaminated zones.
//
// For each entity with Health in a zone that has a BleedZoneEntry:
//   1. Look up BleedZoneEntry for the entity's current zone
//   2. If entry exists and Intensity > 0:
//      a. If entity has PlayerFrontExposure:
//         - Check bypass conditions against BleedRestriction (same as ZoneRestrictionSystem)
//         - If bypassed: apply 25% BleedDPS
//         - If not: apply full BleedDPS
//      b. If AI entity: apply BleedDPS (faction immunity from EPIC 3.3 applies)
//      c. Create DamageEvent: amount = BleedDPS * fixedDeltaTime, type = Environmental
```

### CompoundBleedSystem

```csharp
// File: Assets/Scripts/Front/Systems/CompoundBleedSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BleedSystem
//
// Detects compound bleed condition (3+ Phase 4 districts) and escalates.
//
// Every frame (lightweight query):
//   1. Count district entities where FrontState.Phase == Phase4_Overrun
//   2. Read BleedConfig.CompoundBleedThreshold
//   3. If count >= threshold:
//      a. Enable CompoundBleedActive singleton
//      b. Set Phase4DistrictCount = count
//      c. For each district entity with FrontState:
//         - Add FrontAdvanceModifier (if not already present):
//           * RateModifier = CompoundBleedRateMultiplier - 1.0 (positive = accelerate)
//           * Source = AdvanceModifierSource.RunModifier
//           * RemainingDuration = 0 (permanent until compound condition clears)
//   4. If count < threshold and CompoundBleedActive is enabled:
//      a. Disable CompoundBleedActive
//      b. Remove compound bleed FrontAdvanceModifier from all districts
```

### BleedCleanseSystem

```csharp
// File: Assets/Scripts/Front/Systems/BleedCleanseSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Processes side-goal cleanse actions that directly remove bleed from border zones.
// Listens for quest completion on quests tagged with BleedCleanseObjective.
//
// For each completed BleedCleanseObjective quest:
//   1. Resolve target district and zone IDs
//   2. For matching BleedZoneEntry entries:
//      a. Set IsFading = true (begin fade)
//      b. Optionally: set FadeProgress = 0.5 (accelerated cleanse)
//   3. Fire FrontCounterplayEvent (Type = Contain, ActionKey = cleanse quest key)
```

---

## Authoring

### BleedAuthoring

```csharp
// File: Assets/Scripts/Front/Authoring/BleedAuthoring.cs
// Added to district prefab alongside FrontAuthoring. Adds bleed buffers.
//
// Baker:
//   1. AddBuffer<BleedZoneEntry> (initially empty — populated at runtime)
//   2. AddBuffer<BleedSourceLink> (initially empty)
//
// On expedition graph entity:
//   3. AddComponent<CompoundBleedActive> and SetComponentEnabled(false)
```

### BleedConfigAuthoring

```csharp
// File: Assets/Scripts/Front/Authoring/BleedConfigAuthoring.cs
// Added to FrontConfigAuthoring in persistent scene. Creates BleedConfig singleton.
//
// Inspector fields:
//   - Phase3IncrementPerTransition (default 1)
//   - Phase4IncrementPerTransition (default 2)
//   - DefaultBleedThreshold (default 3)
//   - ZonesPerBleedEvent (default 2)
//   - BleedDPSFraction (default 0.3 — 30% of source Hostile DPS)
//   - BleedFadeDuration (default 60 seconds)
//   - CompoundBleedThreshold (default 3)
//   - CompoundBleedRateMultiplier (default 1.5 — 50% faster all Fronts)
```

---

## Setup Guide

1. **Create BleedComponents.cs** in `Assets/Scripts/Front/Components/`
2. **Create all five systems** in `Assets/Scripts/Front/Systems/`:
   - BleedSystem
   - BleedFadeSystem
   - BleedHazardDamageSystem
   - CompoundBleedSystem
   - BleedCleanseSystem
3. **Add BleedAuthoring** to each district prefab (adds BleedZoneEntry + BleedSourceLink buffers)
4. **Add CompoundBleedActive** (baked disabled) to expedition graph entity authoring
5. **Add BleedConfig** to FrontConfigAuthoring singleton with default values:
   - Phase3IncrementPerTransition = 1
   - Phase4IncrementPerTransition = 2
   - DefaultBleedThreshold = 3
   - ZonesPerBleedEvent = 2
   - BleedDPSFraction = 0.3
   - BleedFadeDuration = 60
   - CompoundBleedThreshold = 3
   - CompoundBleedRateMultiplier = 1.5
6. **Define border zones** per district: tag zones adjacent to gates in FrontDefinitionSO (add `int[] BorderZoneIds` field)
7. **Create bleed VFX prefabs** per thematic type in `Assets/Prefabs/Front/Bleed/`:
   - Spore particles (Quarantine), tendril meshes (Old Growth), heat distortion (Burn), etc.
8. **Create 1-2 BleedCleanseObjective quests** per district in `Assets/Data/Quests/Front/`
9. **Test with 2 districts**: advance District A to Phase 3, perform gate transition, verify BleedCounter increments on District B, cross threshold, verify BleedZoneEntry appears on B's border zones

---

## Verification

- [ ] BleedSystem only processes districts at Phase 3 or higher
- [ ] BleedCounter increments by correct amount per gate transition (Phase 3 vs Phase 4)
- [ ] BleedZoneEntry entries appear in target district when counter crosses threshold
- [ ] Bleed zones use correct thematic restriction type from source district
- [ ] BleedDPS is BleedDPSFraction of source district's Hostile hazard DPS
- [ ] Bleed zones do not affect zones already converted by the target district's own Front
- [ ] BleedFadeSystem decrements Intensity and removes fully faded entries
- [ ] Containment in source district triggers IsFading on all matching BleedZoneEntry entries
- [ ] Removed BleedZoneEntry entries also remove corresponding BleedSourceLink when all gone
- [ ] BleedHazardDamageSystem applies DPS to players and AI in bleed zones
- [ ] Bypass conditions reduce bleed DPS to 25%
- [ ] Faction immunity applies to AI in bleed zones
- [ ] CompoundBleedSystem enables CompoundBleedActive when 3+ districts at Phase 4
- [ ] Compound bleed adds FrontAdvanceModifier to ALL districts
- [ ] CompoundBleedActive disables and modifiers are removed when count drops below threshold
- [ ] BleedCleanseSystem processes cleanse quest completions and begins fade on target zones
- [ ] Multiple bleed sources on the same district stack correctly (independent entries)
- [ ] Bleed zones from different sources have independent fade timers

---

## Validation

Validation rules for `BleedConfig` singleton:
- `Phase3IncrementPerTransition` must be > 0
- `Phase4IncrementPerTransition` must be >= `Phase3IncrementPerTransition` (Phase 4 bleeds faster)
- `DefaultBleedThreshold` must be > 0
- `ZonesPerBleedEvent` must be > 0
- `BleedDPSFraction` must be in (0.0, 1.0] (fraction of source DPS)
- `BleedFadeDuration` must be > 0 (0 = instant fade, invalid)
- `CompoundBleedThreshold` must be >= 2 (at least 2 districts for compound)
- `CompoundBleedRateMultiplier` must be > 1.0 (compound accelerates, doesn't slow)

Validation rules for `BleedZoneEntry` buffer:
- `ZoneId` must be a valid zone in the target district
- `SourceDistrictEntity` must not be Entity.Null
- `Intensity` must be in [0.0, 1.0]
- `BleedDPS` must be >= 0
- `FadeProgress` must be in [0.0, 1.0]
- If `IsFading == false`: `FadeProgress` must be 0

---

## Live Tuning

Live-tunable parameters via `BleedConfig` singleton:
- `Phase3IncrementPerTransition` / `Phase4IncrementPerTransition`: control bleed escalation speed
- `DefaultBleedThreshold`: how many gate transitions before bleed appears
- `BleedDPSFraction`: bleed zone danger level
- `BleedFadeDuration`: how quickly bleed recedes after containment
- `CompoundBleedThreshold` / `CompoundBleedRateMultiplier`: compound bleed tuning

Change propagation: `BleedConfig` is a singleton IComponentData read by `BleedSystem` on each gate transition and by `BleedFadeSystem` every frame. Direct `SetSingleton` calls take effect immediately.

---

## Debug Visualization

**Bleed Zone Overlay** (toggle via debug menu):
- Per-zone border zones highlighted with source district's thematic color
- Bleed intensity gradient: full color at Intensity 1.0, fading to transparent at 0.0
- Source district labels at bleed zone boundaries showing district name + Front phase
- Fade progress bars on fading bleed zones

**Compound Bleed Indicator**:
- HUD warning when CompoundBleedActive is enabled: "COMPOUND BLEED: [N] Phase 4 Districts"
- All district AdvanceRate values shown as a bar chart to visualize expedition-wide acceleration

**Bleed Network Graph** (editor scene view):
- Lines between adjacent district nodes colored by bleed status:
  - Grey = no bleed, yellow = Phase 3 bleed possible, red = active bleed, pulsing red = Phase 4 bleed

**Activation**: Debug menu toggle `Front/Bleed/ShowOverlay`

---

## Simulation & Testing

**Bleed Propagation Test**:
- Automated test: set District A to Phase 3, perform N gate transitions
  - Verify: BleedCounter increments by `Phase3IncrementPerTransition` each transition
  - At N = `DefaultBleedThreshold`: verify BleedZoneEntry appears on adjacent District B
  - Verify: BleedDPS = source Hostile DPS * `BleedDPSFraction`

**Compound Bleed Threshold Test**:
- Set 3 districts to Phase 4, verify CompoundBleedActive enables
- Set 1 district back to Phase 3 (via containment), verify CompoundBleedActive disables
- Verify all compound FrontAdvanceModifiers are added/removed correctly

**Bleed Fade Timing**:
- Monte Carlo (N=100): contain source district at various bleed intensities, measure actual fade duration
- Expected: fade completes within `BleedFadeDuration` +/- 1 frame tolerance

**Cross-District Bleed Distribution** (balance):
- Given 1000 random expedition graphs with 5-8 districts:
  - Track: percentage of districts affected by bleed at expedition midpoint
  - Balance target: 20-40% of districts should have bleed zones (enough pressure without overwhelming)
