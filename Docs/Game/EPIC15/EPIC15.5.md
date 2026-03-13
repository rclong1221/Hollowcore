# EPIC 15.5: Expedition Victory & Summary

**Status**: Planning
**Epic**: EPIC 15 — Final Bosses & Endgame
**Dependencies**: EPIC 15.1 (InfluenceMeterState); EPIC 15.2/15.3/15.4 (any final boss); Framework: Roguelite/ (RunLifecycleSystem, MetaBank, RunHistorySaveModule); EPIC 9 (Compendium), EPIC 12 (Scar Map)

---

## Overview

After defeating a final boss, the expedition ends with a victory sequence and comprehensive summary screen. The summary covers the full expedition: statistics, Scar Map review with timeline, Compendium awards, chassis state, and rival encounters. Meta-progression rewards are distributed including meta-currency, Compendium entries, and unlocks for higher ascension tiers. This is the emotional payoff moment where players see the full arc of their run.

---

## Component Definitions

### ExpeditionSummaryState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/ExpeditionSummaryComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss
{
    public enum SummaryPhase : byte
    {
        Inactive = 0,
        Cinematic = 1,       // Post-boss victory cinematic
        Statistics = 2,      // Stats roll-up screen
        ScarMapReview = 3,   // Animated Scar Map timeline
        Rewards = 4,         // Compendium + meta-currency awards
        AscensionUnlock = 5, // New tier unlock presentation
        Complete = 6         // Ready to return to meta screen
    }

    /// <summary>
    /// Tracks the victory summary sequence state.
    /// Created when the final boss is defeated.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ExpeditionSummaryState : IComponentData
    {
        [GhostField] public SummaryPhase Phase;

        /// <summary>Which final boss was defeated.</summary>
        [GhostField] public int FinalBossId;

        /// <summary>Which faction was dominant.</summary>
        [GhostField] public InfluenceFaction DominantFaction;

        /// <summary>Timer for cinematic and animated transitions.</summary>
        [GhostField(Quantization = 100)] public float PhaseTimer;

        /// <summary>Whether the player has acknowledged each phase (skip/advance).</summary>
        [GhostField] public bool PlayerAcknowledged;
    }
}
```

### ExpeditionStatistics (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/ExpeditionSummaryComponents.cs (continued)
namespace Hollowcore.Boss
{
    /// <summary>
    /// Aggregated statistics for the completed expedition.
    /// Populated from RunHistorySaveModule data.
    /// </summary>
    public struct ExpeditionStatistics : IComponentData
    {
        /// <summary>Total districts cleared.</summary>
        public byte DistrictsCleared;

        /// <summary>Total player deaths during expedition.</summary>
        public int DeathCount;

        /// <summary>Total echoes (side missions) completed.</summary>
        public int EchoesCompleted;

        /// <summary>Peak Trace level reached.</summary>
        public byte PeakTraceLevel;

        /// <summary>Expedition wall clock time in seconds.</summary>
        public float TotalTimeSeconds;

        /// <summary>Total enemies killed.</summary>
        public int EnemiesKilled;

        /// <summary>Total bosses killed (district + final).</summary>
        public byte BossesKilled;

        /// <summary>Total limbs equipped over the expedition.</summary>
        public int LimbsEquipped;

        /// <summary>Total currency earned (run + meta).</summary>
        public int TotalCurrencyEarned;

        /// <summary>Compendium entries discovered this run.</summary>
        public int NewCompendiumEntries;

        /// <summary>Number of rival encounters.</summary>
        public byte RivalEncounters;

        /// <summary>Calculated score for leaderboard.</summary>
        public int FinalScore;
    }
}
```

### MetaRewardElement (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/ExpeditionSummaryComponents.cs (continued)
using Unity.Collections;

namespace Hollowcore.Boss
{
    public enum MetaRewardType : byte
    {
        Currency = 0,         // Meta-currency (Roguelite/ MetaBank)
        CompendiumEntry = 1,  // New Compendium unlock (EPIC 9)
        AscensionUnlock = 2,  // Next ascension tier available
        CosmeticUnlock = 3,   // Visual customization reward
        StartingLoadout = 4   // New starting limb option for future runs
    }

    /// <summary>
    /// Buffer of meta-progression rewards to display and award.
    /// On the summary state entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct MetaRewardElement : IBufferElementData
    {
        public MetaRewardType RewardType;
        public int RewardId;
        public int Quantity;
        public FixedString64Bytes DisplayName;
    }
}
```

---

## Systems

### ExpeditionSummarySystem

```csharp
// File: Assets/Scripts/Boss/Systems/ExpeditionSummarySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Master state machine for the expedition victory sequence.
//
// Triggered when final boss BossPhaseState.EncounterPhase == Victory:
//   1. Create summary entity with ExpeditionSummaryState
//   2. Aggregate ExpeditionStatistics from RunHistorySaveModule
//   3. Calculate FinalScore:
//      Score = (DistrictsCleared * 1000)
//            + (BossesKilled * 2000)
//            + (EchoesCompleted * 500)
//            - (DeathCount * 200)
//            + (AscensionLevel * 1500)
//            + TimeBonus(TotalTimeSeconds)
//   4. Begin Cinematic phase
//
// Phase flow:
//   Cinematic:
//     - Play victory cinematic (boss-specific ending scene)
//     - Auto-advance after timer OR player skip
//   Statistics:
//     - Animated stat roll-up (numbers count up, rank grades appear)
//     - Player advances manually
//   ScarMapReview:
//     - Animated Scar Map showing the expedition timeline
//     - Each district lights up in order cleared
//     - Influence meter changes shown at each step
//     - Player advances manually
//   Rewards:
//     - Meta-currency awarded (MetaBank deposit)
//     - Compendium entries presented one by one
//     - Each reward has a reveal animation
//   AscensionUnlock (if applicable):
//     - New ascension tier unlocked presentation
//     - Shows what changes in the next tier
//   Complete:
//     - Fire RunEndEvent for Roguelite/ RunLifecycleSystem
//     - Transition to meta screen
```

### SummaryScreenBridge

```csharp
// File: Assets/Scripts/Boss/Bridges/SummaryScreenBridge.cs
// Managed MonoBehaviour — bridges ExpeditionSummaryState to the summary UI.
//
// Responsibilities:
//   1. Listen for ExpeditionSummaryState phase changes
//   2. Drive UI panels for each phase:
//      - Cinematic: full-screen video/animation player
//      - Statistics: scrolling stat card with animated counters
//      - Scar Map: interactive replay of the expedition route
//      - Rewards: sequential reveal carousel
//      - Ascension: unlock fanfare with tier description
//   3. Handle player input: skip cinematic, advance panels
//   4. On Complete: trigger screen transition to meta hub
//
// UI Elements:
//   - ExpeditionSummaryPanel (root container)
//   - StatisticsCard (grid of stat entries with counter animations)
//   - ScarMapReplayWidget (miniature Scar Map with timeline scrubber)
//   - RewardRevealCarousel (card-flip animations for each reward)
//   - AscensionUnlockBanner (tier unlock with modifier preview)
//   - FinalScoreDisplay (large score with grade: S/A/B/C/D)
```

### MetaRewardDistributionSystem

```csharp
// File: Assets/Scripts/Boss/Systems/MetaRewardDistributionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ExpeditionSummarySystem
//
// Awards meta-progression rewards during the Rewards phase.
//
// When ExpeditionSummaryState.Phase == Rewards:
//   1. For each MetaRewardElement:
//      a. Currency: deposit into MetaBank (Roguelite/ framework)
//         - Base amount from boss definition
//         - Multipliers: ascension tier, death count, districts cleared
//      b. CompendiumEntry: unlock via Compendium system (EPIC 9)
//         - Final boss entry (unique per boss)
//         - First-time district entries if any new
//      c. AscensionUnlock: mark next tier as available
//         - Persisted via RunHistorySaveModule
//      d. CosmeticUnlock: add to player's cosmetic inventory
//      e. StartingLoadout: add new starting limb option for future runs
//   2. Persist all rewards via save system (EPIC 16.15 pattern)
//   3. Fire RewardAwardedEvent for each item (drives UI reveal animation)
```

---

## Score Grading

| Grade | Score Range | Description |
|---|---|---|
| S | 15000+ | Perfect — minimal deaths, high completion, fast time |
| A | 10000-14999 | Excellent — strong performance |
| B | 6000-9999 | Good — solid run |
| C | 3000-5999 | Average — room for improvement |
| D | 0-2999 | Struggling — but you survived |

---

## Setup Guide

1. Create ExpeditionSummaryComponents.cs in `Assets/Scripts/Boss/Components/`
2. Create summary UI prefabs in `Assets/Prefabs/UI/Summary/`:
   - ExpeditionSummaryPanel
   - StatisticsCard
   - ScarMapReplayWidget
   - RewardRevealCarousel
   - AscensionUnlockBanner
   - FinalScoreDisplay
3. Wire ExpeditionSummarySystem to final boss Victory state
4. Configure meta-currency amounts per final boss in BossDefinitionSO
5. Create victory cinematics (one per final boss) in `Assets/Cinematics/Victory/`
6. Wire MetaRewardDistributionSystem to Roguelite/ MetaBank
7. Configure score formula constants in a ScriptableObject (tunable)
8. Wire ScarMapReplayWidget to EPIC 12 Scar Map data
9. Add Compendium entries for final bosses (EPIC 9)
10. Test full flow: final boss kill → cinematic → stats → map → rewards → meta screen

---

## Verification

- [ ] Victory triggers summary sequence after final boss defeat
- [ ] Cinematic plays (boss-specific) and can be skipped
- [ ] Statistics screen shows accurate expedition data
- [ ] FinalScore calculation matches formula
- [ ] Score grade (S/A/B/C/D) displays correctly
- [ ] Scar Map replay shows districts in order cleared
- [ ] Influence meter changes animate during Scar Map replay
- [ ] Meta-currency deposits into MetaBank
- [ ] Compendium entries unlock and display
- [ ] Ascension unlock presents if applicable
- [ ] All rewards persist to save data
- [ ] Player can advance through each phase manually
- [ ] Complete phase fires RunEndEvent
- [ ] Transition to meta screen works
- [ ] DeathCount correctly penalizes score
- [ ] Time bonus rewards faster completions

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/ExpeditionSummaryValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Validation rules for expedition summary:
    //
    // 1. Score formula constants: verify multipliers produce scores in expected range.
    //    Simulate: 5 districts, 0 deaths, 3 bosses, ascension 0 → score should be in B-A range.
    //    Simulate: 7 districts, 0 deaths, 4 bosses, ascension 5 → score should be S range.
    //    Warning if formula produces negative scores (death penalty too harsh).
    //
    // 2. Grade boundaries: S > A > B > C > D thresholds must be strictly decreasing.
    //    Error if boundaries overlap or are non-monotonic.
    //
    // 3. Meta-currency amounts: per-boss meta-currency must be > 0.
    //    Warning if final boss meta-currency < district boss meta-currency (anti-climactic).
    //
    // 4. Victory cinematics: every final boss must have a corresponding cinematic asset.
    //    Warning if cinematic reference is null.
    //
    // 5. Compendium entries: every final boss must have a Compendium entry definition.
    //    Warning if missing.
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/ExpeditionSummaryLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Summary and meta-reward live tuning:
    //
    //   float MetaCurrencyMultiplierOverride  // -1 = use computed, >0 = override
    //   bool SkipCinematic                    // jump to Statistics phase
    //   bool SkipScarMapReview                // jump to Rewards phase
    //   bool ForceAscensionUnlock             // always show ascension unlock (even if not earned)
    //   int ForceScoreOverride                // -1 = use computed, >=0 = force specific score
    //   bool GrantAllCompendiumEntries        // unlock every possible Compendium entry
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/ExpeditionSummaryDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Summary debug overlay (enabled during summary sequence):
    //
    // [1] Score Breakdown
    //     - Detailed formula: each component shown with its multiplier
    //     - "Districts: 5 * 1000 = 5000"
    //     - "Deaths: 2 * -200 = -400"
    //     - "Ascension: 3 * 1500 = 4500"
    //     - Total and grade
    //
    // [2] Reward Distribution Log
    //     - List of all MetaRewardElement entries as they're processed
    //     - MetaBank balance before and after
    //     - Compendium unlock confirmations
    //
    // [3] Summary Phase State
    //     - Current SummaryPhase badge
    //     - PhaseTimer countdown
    //     - PlayerAcknowledged flag
}
