# EPIC 3.4: Front Counterplay

**Status**: Planning
**Epic**: EPIC 3 — The Front (District Pressure System)
**Priority**: High — Player agency against the primary pressure mechanic
**Dependencies**: EPIC 3.1 (FrontState, FrontZoneData, FrontPhaseChangedTag); EPIC 3.2 (FrontAdvanceModifier, FrontAdvanceSystem); Framework: Quest/ (quest completion callbacks), AI/ (enemy behavior); Optional: EPIC 5 (echo missions as counterplay content)

---

## Overview

The Front is not an inevitable death march. Players have four counterplay strategies: **Contain** (complete objectives that slow or block the Front), **Divert** (redirect the spread pattern to protect key zones), **Dive** (push into Overrun zones for high-risk rewards), and **Exploit** (lure enemies into Front hazard zones). Each strategy has a dedicated system that modifies FrontState in response to player actions. Counterplay events flow through the FrontCounterplayEvent component so the UI, audio, and progression systems can react.

---

## Component Definitions

### CounterplayType Enum

```csharp
// File: Assets/Scripts/Front/Components/FrontCounterplayComponents.cs
namespace Hollowcore.Front
{
    /// <summary>
    /// The four categories of player counterplay against the Front.
    /// </summary>
    public enum CounterplayType : byte
    {
        Contain = 0,   // Slow/block Front via objective completion
        Divert = 1,    // Redirect spread pattern to protect zones
        Dive = 2,      // Push into Overrun zones for high-risk content
        Exploit = 3    // Lure enemies into Front hazards
    }
}
```

### FrontCounterplayEvent (IComponentData, IEnableableComponent)

```csharp
// File: Assets/Scripts/Front/Components/FrontCounterplayComponents.cs (continued)
using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

namespace Hollowcore.Front
{
    /// <summary>
    /// Enableable event fired for one frame when a counterplay action completes.
    /// On the district entity. Read by UI bridge, audio, progression systems.
    /// Baked disabled.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct FrontCounterplayEvent : IComponentData, IEnableableComponent
    {
        [GhostField] public CounterplayType Type;

        /// <summary>Description key for UI display (e.g., "CONTAIN_GRIEF_LINK").</summary>
        [GhostField] public FixedString64Bytes ActionKey;

        /// <summary>How much the advance rate was modified (-0.5 = 50% slower).</summary>
        [GhostField(Quantization = 100)] public float RateModification;

        /// <summary>Duration in seconds the effect lasts (0 = permanent).</summary>
        [GhostField(Quantization = 100)] public float Duration;

        /// <summary>Zone ID affected (for Divert actions). -1 = district-wide.</summary>
        [GhostField] public int AffectedZoneId;
    }
}
```

### ContainmentObjective (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/FrontCounterplayComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Marks a quest entity as a containment objective. When the associated quest
    /// completes, FrontCounterplaySystem applies a rate modifier to the district's Front.
    /// </summary>
    public struct ContainmentObjective : IComponentData
    {
        /// <summary>District entity this containment affects.</summary>
        public Entity DistrictEntity;

        /// <summary>Rate modifier applied on completion (negative = slower). -0.3 = 30% slower.</summary>
        public float RateModifierOnComplete;

        /// <summary>Duration of the slow effect in seconds (0 = permanent until broken).</summary>
        public float EffectDuration;

        /// <summary>If true, completing this objective sets FrontState.IsContained = true (full block).</summary>
        public bool FullContainment;

        /// <summary>If true, Front advance is paused for PauseDurationSeconds on completion.</summary>
        public bool PausesAdvance;
        public float PauseDurationSeconds;
    }
}
```

### DivertAction (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/FrontCounterplayComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Runtime data for a diversion action. Created as a transient entity when
    /// the player activates a sabotage/relay/barrier.
    /// </summary>
    public struct DivertAction : IComponentData
    {
        /// <summary>District entity whose spread pattern is being modified.</summary>
        public Entity DistrictEntity;

        /// <summary>Zone ID to protect (moved to end of conversion order).</summary>
        public int ProtectedZoneId;

        /// <summary>Zone ID to sacrifice (moved earlier in conversion order).</summary>
        public int SacrificedZoneId;

        /// <summary>Duration the diversion lasts in seconds (0 = permanent for this expedition).</summary>
        public float Duration;
    }
}
```

### DiveRewardMultiplier (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/FrontCounterplayComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Singleton config controlling risk/reward scaling for Overrun zone exploration.
    /// </summary>
    public struct DiveRewardMultiplier : IComponentData
    {
        /// <summary>Loot rarity multiplier in Overrun zones (e.g., 2.5 = 2.5x rarer drops).</summary>
        public float OverrunLootMultiplier;

        /// <summary>XP multiplier for kills in Overrun zones.</summary>
        public float OverrunXPMultiplier;

        /// <summary>Currency multiplier for pickups in Overrun zones.</summary>
        public float OverrunCurrencyMultiplier;

        /// <summary>Chance (0-1) to find echo mission entries in Overrun zones.</summary>
        public float EchoMissionChance;

        /// <summary>Chance (0-1) to find premium revival bodies in Overrun zones.</summary>
        public float PremiumBodyChance;
    }
}
```

### ExploitKillTracker (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/FrontCounterplayComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Tracks enemy kills caused by Front hazard damage for Exploit counterplay.
    /// On district entity. Reset per zone transition.
    /// </summary>
    public struct ExploitKillTracker : IComponentData
    {
        /// <summary>Number of enemies killed by Front hazards in current session.</summary>
        public int HazardKillCount;

        /// <summary>Threshold for exploit bonus (e.g., 5 kills = advance rate reduction).</summary>
        public int BonusThreshold;

        /// <summary>Rate reduction per threshold reached (e.g., -0.1 per 5 kills).</summary>
        public float RateReductionPerThreshold;
    }
}
```

---

## Systems

### FrontCounterplaySystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontCounterplaySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: FrontAdvanceSystem (EPIC 3.2)
//
// Processes containment objective completions and modifies FrontState.
//
// For each ContainmentObjective where associated quest is complete (QuestState.Completed):
//   1. Resolve DistrictEntity → FrontState
//   2. If FullContainment:
//      a. Set FrontState.IsContained = true
//      b. SpreadProgress freezes (handled by FrontAdvanceSystem checking IsContained)
//   3. If PausesAdvance:
//      a. Set FrontState.PausedUntilTick = currentTick + (PauseDurationSeconds * tickRate)
//   4. Add FrontAdvanceModifier to district entity:
//      - RateModifier = RateModifierOnComplete (negative)
//      - Source = AdvanceModifierSource.Containment
//      - RemainingDuration = EffectDuration
//   5. Enable FrontCounterplayEvent on district entity:
//      - Type = CounterplayType.Contain
//      - ActionKey from quest definition
//      - RateModification, Duration
//   6. Destroy ContainmentObjective entity (one-shot)
```

### FrontDivertSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontDivertSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Processes DivertAction entities and modifies zone conversion order at runtime.
//
// For each DivertAction entity:
//   1. Resolve DistrictEntity → FrontZoneData buffer
//   2. Find ProtectedZoneId in buffer → move its ConversionOrder to last
//   3. Find SacrificedZoneId in buffer → move its ConversionOrder earlier
//   4. Re-sort buffer by ConversionOrder
//   5. Enable FrontCounterplayEvent on district:
//      - Type = CounterplayType.Divert
//      - AffectedZoneId = ProtectedZoneId
//   6. If Duration > 0: schedule revert (store original order, create timed revert entity)
//   7. Destroy DivertAction entity
//
// NOTE: Diversion does NOT reverse already-converted zones. It only changes
// the order of future conversions. A zone already Hostile stays Hostile.
```

### DiveRewardSystem

```csharp
// File: Assets/Scripts/Front/Systems/DiveRewardSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Applies reward multipliers when player is in an Overrun zone.
//
// For each player entity with PlayerFrontExposure:
//   1. If CurrentZoneState != Overrun: clear dive bonuses, early out
//   2. Read DiveRewardMultiplier singleton
//   3. Push temporary modifiers into loot/XP/currency systems:
//      a. Loot system: multiply drop rarity rolls by OverrunLootMultiplier
//      b. XP system: multiply kill XP by OverrunXPMultiplier
//      c. Currency: multiply pickup value by OverrunCurrencyMultiplier
//   4. On enemy kill in Overrun zone:
//      a. Roll EchoMissionChance — if hit, place echo mission marker
//      b. Roll PremiumBodyChance — if hit, mark dropped body as premium
//   5. Enable FrontCounterplayEvent:
//      - Type = CounterplayType.Dive
```

### ExploitTrackingSystem

```csharp
// File: Assets/Scripts/Front/Systems/ExploitTrackingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: FrontHazardDamageSystem (EPIC 3.3)
//
// Tracks enemy kills caused by Front hazard damage and applies exploit bonuses.
//
// For each district entity with ExploitKillTracker:
//   1. Query DeathState entities that died this frame with DamageEvent source == Environmental
//   2. If AI entity (not player) and in a restricted zone:
//      a. Increment ExploitKillTracker.HazardKillCount
//   3. If HazardKillCount >= BonusThreshold:
//      a. Add FrontAdvanceModifier:
//         - RateModifier = RateReductionPerThreshold (negative)
//         - Source = AdvanceModifierSource.Counterplay
//         - RemainingDuration = 60 seconds (configurable)
//      b. Reset HazardKillCount (or subtract threshold for rolling bonus)
//      c. Enable FrontCounterplayEvent:
//         - Type = CounterplayType.Exploit
//   4. NOTE: Thematically immune factions do not count for exploit kills
```

### FrontCounterplayEventCleanupSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontCounterplayEventCleanupSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup, OrderLast
//
// Disables FrontCounterplayEvent at end of frame (one-frame event pattern).
//
// For each entity with enabled FrontCounterplayEvent:
//   1. SetComponentEnabled<FrontCounterplayEvent>(entity, false)
```

---

## Setup Guide

1. **Create FrontCounterplayComponents.cs** in `Assets/Scripts/Front/Components/`
2. **Create all five systems** in `Assets/Scripts/Front/Systems/`:
   - FrontCounterplaySystem
   - FrontDivertSystem
   - DiveRewardSystem
   - ExploitTrackingSystem
   - FrontCounterplayEventCleanupSystem
3. **Add FrontCounterplayEvent** (baked disabled) to district entity in FrontAuthoring baker
4. **Add ExploitKillTracker** to district entity in FrontAuthoring baker with default threshold = 5
5. **Create DiveRewardMultiplier singleton** via FrontConfigAuthoring (EPIC 3.2):
   - OverrunLootMultiplier = 2.5
   - OverrunXPMultiplier = 2.0
   - OverrunCurrencyMultiplier = 1.5
   - EchoMissionChance = 0.15
   - PremiumBodyChance = 0.08
6. **Tag containment quests** with ContainmentObjective component:
   - Per district, create 2-3 quests with ContainmentObjective referencing the district entity
   - Example: Necrospire — "Sever the Grief-Link" (RateModifier -0.3, Duration 0 permanent)
   - Example: The Burn — "Cool the Core" (PausesAdvance true, PauseDuration 120s)
7. **Create DivertAction spawn points** in district scenes — interactable objects that create DivertAction entities when activated
8. **Verify Quest/ integration**: quest completion callback must query for ContainmentObjective on the quest entity

---

## Verification

- [ ] Completing a containment quest adds negative FrontAdvanceModifier to district
- [ ] FullContainment quest sets FrontState.IsContained = true and freezes advance
- [ ] PausesAdvance quest sets PausedUntilTick correctly
- [ ] FrontCounterplayEvent fires for one frame on each counterplay action
- [ ] FrontCounterplayEvent.Type correctly identifies Contain/Divert/Dive/Exploit
- [ ] DivertAction modifies FrontZoneData.ConversionOrder at runtime
- [ ] Protected zone moves to end of conversion order (converted last)
- [ ] Sacrificed zone moves earlier in conversion order (converted sooner)
- [ ] Diversion does not reverse already-converted zones
- [ ] Timed diversions revert after Duration expires
- [ ] Player in Overrun zone receives loot/XP/currency multipliers from DiveRewardMultiplier
- [ ] Echo mission and premium body rolls occur on kills in Overrun zones
- [ ] Enemy kills by Front hazards increment ExploitKillTracker.HazardKillCount
- [ ] Reaching exploit threshold applies temporary advance rate reduction
- [ ] Thematically immune factions do not count for exploit kills
- [ ] FrontCounterplayEventCleanupSystem disables event at end of frame
- [ ] Multiple counterplay modifiers stack correctly on FrontAdvanceModifier buffer

---

## Validation

Validation rules for `ContainmentObjective`:
- `DistrictEntity` must reference a valid district entity (not Entity.Null)
- `RateModifierOnComplete` must be negative (containment slows, never accelerates)
- `EffectDuration` must be >= 0 (0 = permanent)
- If `FullContainment == true`: `RateModifierOnComplete` is ignored (full block)
- If `PausesAdvance == true`: `PauseDurationSeconds` must be > 0

Validation rules for `DiveRewardMultiplier` singleton:
- All multipliers must be >= 1.0 (Overrun rewards should never be worse than normal)
- `EchoMissionChance` and `PremiumBodyChance` must be in [0.0, 1.0]

Validation rules for `ExploitKillTracker`:
- `BonusThreshold` must be > 0
- `RateReductionPerThreshold` must be negative

---

## Live Tuning

Live-tunable parameters:
- `DiveRewardMultiplier` singleton: all fields tunable at runtime via `FrontConfigAuthoring` inspector
- `ExploitKillTracker.BonusThreshold` and `RateReductionPerThreshold`: tunable per-district
- `ContainmentObjective.RateModifierOnComplete` and `EffectDuration`: tunable per quest (but only before quest completion)

Change propagation: `DiveRewardMultiplier` is a singleton IComponentData read every frame — direct writes take effect immediately. Exploit tracker fields are per-entity and require manual `SetComponent` calls via a tuning bridge.

---

## Debug Visualization

**Counterplay Status Overlay** (toggle via debug menu):
- HUD panel showing:
  - Active containment modifiers with source quest name, rate modification, remaining duration
  - Diversion status: list of protected/sacrificed zone pairs with remaining duration
  - Dive reward status: current multipliers if player is in Overrun zone
  - Exploit kill counter: current count / threshold, with progress bar
- FrontAdvanceModifier breakdown: visual stack showing all modifier sources and their net effect on AdvanceRate

**Activation**: Debug menu toggle `Front/Counterplay/ShowStatus`

---

## Simulation & Testing

**Counterplay Effectiveness Analysis**:
- Automated test: run expedition with no counterplay vs all containment quests completed
  - No counterplay: record time-to-Phase4
  - Full containment: verify IsContained blocks advance entirely
  - Partial containment (1 of 3 quests): verify rate reduction matches expected modifier sum

**Exploit Economy Simulation**:
- Monte Carlo (N=1000): simulate random combat encounters in restricted zones, track hazard kills
  - Balance target: average time to reach exploit threshold (5 kills) should be 60-120s of active combat in Hostile+ zones
  - Rate reduction per threshold should extend time-to-next-phase by 10-15%

**Dive Risk/Reward Balance**:
- Simulate 100 Overrun zone exploration sessions with random loot drops
  - Track: player survival rate, average loot value vs baseline, echo mission discovery rate
  - Balance target: Overrun zone loot should be 2-3x baseline value, survival rate 40-60% without gear bypass
