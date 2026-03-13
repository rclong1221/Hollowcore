# EPIC 4.7: Expedition End Conditions

**Status**: Planning
**Epic**: EPIC 4 — District Graph & Expedition Structure
**Priority**: High — How expeditions conclude
**Dependencies**: EPIC 4.1 (Graph Data Model), EPIC 2 (Death & Revival — Full Wipe), EPIC 15 (Influence & Boss — Victory)

---

## Overview

An expedition ends in one of three ways: Victory (defeat the final boss), Full Wipe (total party kill with no revival options), or Abandonment (player quits). Victory triggers the full expedition summary, Compendium awards, and unlocks. Full Wipe preserves Compendium entries from completed districts but loses run progress. Abandonment can either save state for later resumption or be treated as a wipe. The ExpeditionEndSystem detects end conditions, transitions to the end screen, and hands off to the meta-progression layer.

---

## Component Definitions

### ExpeditionResultType Enum

```csharp
// File: Assets/Scripts/Expedition/Components/ExpeditionEndComponents.cs
namespace Hollowcore.Expedition
{
    public enum ExpeditionResultType : byte
    {
        /// <summary>Default — expedition is still in progress.</summary>
        InProgress = 0,

        /// <summary>Player defeated the final boss.</summary>
        Victory = 1,

        /// <summary>Total party kill with no revival options remaining.</summary>
        FullWipe = 2,

        /// <summary>Player chose to abandon the expedition.</summary>
        Abandoned = 3,

        /// <summary>Expedition state saved for later resumption.</summary>
        Suspended = 4,
    }
}
```

### ExpeditionEndState (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/ExpeditionEndComponents.cs (continued)
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Singleton on the ExpeditionGraphEntity. Tracks whether the expedition has ended
    /// and what triggered the conclusion.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct ExpeditionEndState : IComponentData
    {
        /// <summary>Current result. InProgress until an end condition triggers.</summary>
        public ExpeditionResultType Result;

        /// <summary>Node index where the expedition ended (for summary screen context).</summary>
        public int EndDistrictNodeIndex;

        /// <summary>Total expedition elapsed time at end (copied from ExpeditionGraphState).</summary>
        public float TotalElapsedTime;

        /// <summary>Districts cleared at time of end.</summary>
        public int DistrictsCleared;

        /// <summary>Total player deaths across all districts.</summary>
        public int TotalDeaths;

        /// <summary>If Victory: which boss was defeated (boss definition ID).</summary>
        public int FinalBossId;

        /// <summary>If Suspended: save slot index for resumption.</summary>
        public int SuspendSaveSlot;
    }
}
```

### ExpeditionSummary (Managed — Not ECS)

```csharp
// File: Assets/Scripts/Expedition/UI/ExpeditionSummary.cs
using System;
using System.Collections.Generic;

namespace Hollowcore.Expedition.UI
{
    /// <summary>
    /// Managed data object constructed by EndScreenSystem for the summary UI.
    /// NOT an ECS component — passed to UI layer via bridge.
    /// </summary>
    [Serializable]
    public class ExpeditionSummary
    {
        public ExpeditionResultType Result;
        public float TotalTimeSeconds;
        public int DistrictsCleared;
        public int TotalKills;
        public int TotalDeaths;
        public int EchoesCompleted;

        /// <summary>Per-district summary for the Scar Map review screen.</summary>
        public List<DistrictSummaryEntry> DistrictSummaries = new();

        /// <summary>Compendium entries unlocked during this expedition.</summary>
        public List<int> CompendiumUnlockIds = new();

        /// <summary>Meta-currency earned (survives wipe).</summary>
        public int MetaCurrencyEarned;

        /// <summary>Seed for sharing.</summary>
        public uint ExpeditionSeed;
    }

    [Serializable]
    public class DistrictSummaryEntry
    {
        public int DistrictDefinitionId;
        public string DistrictName;
        public bool MainChainComplete;
        public int ZonesExplored;
        public int ZonesTotal;
        public byte FinalFrontPhase;
        public int KillCount;
        public int DeathCount;
        public float TimeSpentSeconds;
    }
}
```

### ExpeditionEndRequest (IComponentData, IEnableableComponent)

```csharp
// File: Assets/Scripts/Expedition/Components/ExpeditionEndComponents.cs (continued)
namespace Hollowcore.Expedition
{
    /// <summary>
    /// Ephemeral event that triggers expedition end processing.
    /// Baked disabled on ExpeditionGraphEntity. Enabled by victory/wipe/abandon detection.
    /// </summary>
    public struct ExpeditionEndRequest : IComponentData, IEnableableComponent
    {
        public ExpeditionResultType RequestedResult;

        /// <summary>If Victory: boss definition ID.</summary>
        public int BossId;
    }
}
```

---

## Systems

### ExpeditionEndDetectionSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ExpeditionEndDetectionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Monitors end conditions each frame. Creates ExpeditionEndRequest when triggered.
//
// 1. Victory check:
//    a. Query for final boss entity with DeathState.IsDead == true
//    b. Boss must be in the designated boss district (BossSlotIndex)
//    c. If found: enable ExpeditionEndRequest with Result=Victory, BossId
//
// 2. Full Wipe check:
//    a. Query all player entities
//    b. If ALL players have DeathState.IsDead == true:
//       - Check revival system (EPIC 2): any revival options remaining?
//       - Revival options: nearby player bodies with compatible limbs, echo revival tokens
//       - If no revival options: enable ExpeditionEndRequest with Result=FullWipe
//    c. Grace period: 5 seconds after last player death before confirming wipe
//       (allows echo revival or body interaction in final moments)
//
// 3. Abandonment check:
//    a. Listen for player abandon input (menu → "Abandon Expedition")
//    b. If confirmed: enable ExpeditionEndRequest with Result=Abandoned
//
// 4. Suspension check:
//    a. Listen for player suspend input (menu → "Save & Quit")
//    b. If confirmed: enable ExpeditionEndRequest with Result=Suspended
```

### ExpeditionEndProcessingSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ExpeditionEndProcessingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ExpeditionEndDetectionSystem
//
// Processes the end request and transitions to the end screen.
//
// When ExpeditionEndRequest is enabled:
//
// 1. Populate ExpeditionEndState:
//    Result = RequestedResult
//    EndDistrictNodeIndex = CurrentNodeIndex
//    TotalElapsedTime = ExpeditionGraphState.ElapsedTime
//    DistrictsCleared = ExpeditionGraphState.DistrictsCleared
//    TotalDeaths = sum of all GraphNodeState.DeathCount
//    FinalBossId = BossId (if Victory)
//
// 2. Compute Compendium awards:
//    a. For each district with IsMainChainComplete:
//       - Add district Compendium entry to unlock list
//    b. For each completed echo mission across all districts:
//       - Add echo Compendium entry
//    c. If Victory: add boss Compendium entry + expedition completion entry
//    d. Compendium entries survive Full Wipe (persist to meta save)
//
// 3. Compute meta-currency:
//    a. Base amount per district cleared
//    b. Bonus for Victory
//    c. Bonus for low death count
//    d. Bonus for speed (under target time thresholds)
//    e. Full Wipe: 50% of earned currency (not zero — incentivize pushing further)
//
// 4. Build ExpeditionSummary (managed) from all GraphNodeState entries + computed data
//
// 5. Branch by result:
//    a. Victory / FullWipe / Abandoned:
//       - Signal EndScreenSystem to show summary UI
//       - Clear DistrictPersistenceModule (all district saved states)
//       - Write Compendium unlocks + meta-currency to meta save (framework Persistence/)
//    b. Suspended:
//       - Save full expedition state via Persistence/ (ExpeditionGraphState + all district saves)
//       - Write SuspendSaveSlot
//       - Return to main menu
//
// 6. Disable ExpeditionEndRequest
```

### EndScreenSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/EndScreenSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Managed SystemBase — bridges ECS end state to the UI layer.
//
// 1. When ExpeditionEndState.Result != InProgress:
//    a. Read ExpeditionSummary from ExpeditionEndProcessingSystem (static handoff or singleton)
//    b. Signal EndScreenUIController (MonoBehaviour) to display:
//       - Result banner: "EXPEDITION COMPLETE" / "TOTAL WIPE" / "ABANDONED"
//       - Scar Map review: interactive map showing district completion states
//       - Stats grid: time, kills, deaths, districts cleared, echoes completed
//       - Compendium unlocks: list of newly unlocked entries with previews
//       - Meta-currency earned: amount with breakdown
//       - Seed display: expedition seed for sharing
//    c. On player confirmation ("Continue" button):
//       - Transition to hub / main menu
//       - Clean up all expedition ECS entities (graph, districts, zones)
//       - Reset RunLifecycleSystem phase to PreRun
```

### ExpeditionResumeSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/ExpeditionResumeSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Handles loading a suspended expedition from a save slot.
//
// 1. On resume request (from main menu → "Continue Expedition"):
//    a. Read save slot from player selection
//    b. Deserialize ExpeditionGraphState + ExpeditionSeedState + all DistrictSaveStates
//    c. Recreate ExpeditionGraphEntity with full state
//    d. Populate GraphNodeState + GraphEdgeState buffers from saved data
//    e. Populate DistrictSeedData buffer
//    f. Load the district the player was in (CurrentNodeIndex)
//    g. Apply saved delta via DistrictDeltaApplySystem
//    h. Spawn player at last known position (or district entry point as fallback)
//    i. Delete the suspend save slot (one-use — no save scumming)
```

---

## Setup Guide

1. **Add ExpeditionEndState + ExpeditionEndRequest** to the ExpeditionGraphEntity prefab
2. **Bake ExpeditionEndRequest** as IEnableableComponent (disabled by default)
3. **Create EndScreenUIController** MonoBehaviour on a persistent UI canvas:
   - Summary panel with result banner, stats grid, scar map, compendium list, seed display
   - "Continue" button wired to clean up and return to hub
4. **Configure meta-currency formula** in an `ExpeditionRewardConfig` ScriptableObject:
   - BasePerDistrict=100, VictoryBonus=500, LowDeathBonus=200, SpeedBonusThreshold=3600s
   - WipeMultiplier=0.5
5. **Wire Compendium persistence**: on end, call `CompendiumSaveModule.UnlockEntries(ids)` (or equivalent)
6. **Wire suspend save**: create `ExpeditionSaveModule` (ISaveModule TypeId=23) that serializes full expedition state
7. **Wire resume flow**: main menu "Continue" button checks for suspend save, creates resume request

---

## Verification

- [ ] Victory triggers when final boss dies in boss district
- [ ] Full Wipe triggers when all players dead AND no revival options AND 5s grace period elapsed
- [ ] Abandonment triggers from menu confirmation
- [ ] Suspension saves full expedition state and returns to main menu
- [ ] ExpeditionEndState populates all fields correctly
- [ ] Compendium entries survive Full Wipe (persisted to meta save)
- [ ] Meta-currency formula applies correctly: base + bonuses, 50% on wipe
- [ ] End screen displays correct stats, scar map, compendium unlocks, and seed
- [ ] "Continue" from end screen cleans up all expedition entities and returns to hub
- [ ] Resume from suspend save restores full expedition graph + district states
- [ ] Suspend save is deleted after resume (no save scumming)
- [ ] Full Wipe grace period prevents premature wipe during revival window
- [ ] Expedition seed displayed on end screen is correct and copyable
- [ ] Multiple concurrent end conditions (e.g., boss dies same frame as player) resolve correctly (Victory wins)

---

## Validation

```csharp
// File: Assets/Scripts/Expedition/Definitions/ExpeditionRewardConfigSO.cs (OnValidate)
namespace Hollowcore.Expedition.Definitions
{
    public partial class ExpeditionRewardConfigSO
    {
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (BasePerDistrict <= 0) Debug.LogError("[ExpeditionRewardConfig] BasePerDistrict must be positive", this);
            if (VictoryBonus < 0) Debug.LogError("[ExpeditionRewardConfig] VictoryBonus cannot be negative", this);
            if (WipeMultiplier < 0f || WipeMultiplier > 1f)
                Debug.LogWarning($"[ExpeditionRewardConfig] WipeMultiplier={WipeMultiplier} — expected [0,1]", this);
            if (SpeedBonusThresholdSeconds <= 0)
                Debug.LogError("[ExpeditionRewardConfig] SpeedBonusThreshold must be positive", this);
        }
#endif
    }
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/ExpeditionWorkstation/EndConditionDebugPanel.cs
// Custom editor panel for expedition end state debugging:
//
// Play-mode features:
//   - Current result display: InProgress / Victory / FullWipe / Abandoned / Suspended
//   - "Force Victory" button: creates ExpeditionEndRequest with Result=Victory
//   - "Force Wipe" button: creates ExpeditionEndRequest with Result=FullWipe
//   - "Force Suspend" button: saves and quits to main menu
//   - Grace period countdown: shows remaining seconds before Full Wipe confirms
//   - Expedition summary preview: shows what the end screen would display RIGHT NOW
//     (districts cleared, kills, deaths, meta-currency estimate, compendium entries pending)
//   - Resume save slot inspector: shows saved expedition data if suspended
//
// Reward formula preview:
//   - Editable fields: districts cleared, deaths, time, victory toggle
//   - Live calculation: shows meta-currency breakdown
//   - "What if" scenarios for reward tuning
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Expedition/Components/EndConditionRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Runtime-tunable expedition end condition parameters.
    /// </summary>
    public struct EndConditionRuntimeConfig : IComponentData
    {
        /// <summary>Grace period in seconds before Full Wipe confirms. Default 5.0.</summary>
        public float WipeGracePeriod;

        /// <summary>Meta-currency multiplier (scales all earned currency). Default 1.0.</summary>
        public float MetaCurrencyMultiplier;

        /// <summary>If true, abandonment preserves 25% meta-currency instead of 0. Default false.</summary>
        public bool AbandonPartialReward;

        /// <summary>If true, disable one-use suspend save deletion (debug). Default false.</summary>
        public bool DebugPersistSuspendSave;

        public static EndConditionRuntimeConfig Default => new()
        {
            WipeGracePeriod = 5.0f,
            MetaCurrencyMultiplier = 1.0f,
            AbandonPartialReward = false,
            DebugPersistSuspendSave = false,
        };
    }
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Expedition/Debug/EndConditionDebugOverlay.cs
// In-game HUD overlay for end condition monitoring:
//
//   - Status bar: "EXPEDITION: IN PROGRESS" / "WIPE GRACE: 3.2s" / "VICTORY!" etc.
//   - Revival status: "Revival options: [body nearby] [echo token: 1]" or "NO REVIVAL OPTIONS"
//   - Player death tally: alive/dead count for all players
//   - Boss health bar (if in boss district): percentage + kill detection status
//   - Meta-currency running estimate: "~350 meta-currency earned so far"
//   - Compendium running count: "4 entries unlocked this expedition"
//   - Toggle: always visible in debug builds, hidden in release
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/ExpeditionWorkstation/EndConditionSimulator.cs
// IExpeditionWorkstationModule — "End Conditions" tab.
//
// "Reward Monte Carlo" (1000 expeditions):
//   For each simulated expedition:
//     - Random districts cleared (1-7), random deaths (0-20), random time (15-120 min)
//     - Random result: 60% Victory, 30% FullWipe, 10% Abandoned
//     - Compute meta-currency per ExpeditionRewardConfig formula
//   Output:
//     - Histogram: meta-currency distribution per result type
//     - Average/min/max currency for Victory vs Wipe
//     - Speed bonus frequency: % of expeditions that qualify
//     - "Is the economy balanced?" summary: expected currency/hour
//
// "Priority Resolution" test:
//   Simulate simultaneous end conditions:
//     - Boss dies + all players dead: verify Victory wins
//     - Abandon request + boss dies same frame: verify Victory wins
//     - Suspend + wipe grace period active: verify suspend takes priority
//   Verify: only one ExpeditionEndRequest can be active at a time
//
// "Resume Fidelity" test:
//   Simulate: full expedition → suspend → resume → verify all state intact
//   Check: graph topology, district save states, current node, elapsed time, seed
```
