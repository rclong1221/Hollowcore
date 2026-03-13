# EPIC 15.6: Ascension Loop

**Status**: Planning
**Epic**: EPIC 15 — Final Bosses & Endgame
**Dependencies**: EPIC 15.5 (ExpeditionSummarySystem, AscensionUnlock); Framework: Roguelite/ (AscensionDefinitionSO, RuntimeDifficultyScale, RunLifecycleSystem); EPIC 7 (Strife), EPIC 14 (Boss Variant Clauses)

---

## Overview

Ascension tiers provide escalating difficulty and replayability across 10 levels. Each tier stacks modifiers on top of the base expedition: faster Fronts, more elites, rotating Strife cards, additional boss variant clauses, reduced revival options, and eventually multiple simultaneous Strife cards. Completing an expedition at a given tier unlocks the next. Exclusive rewards at higher tiers incentivize pushing further. A leaderboard tracks performance across seed, ascension level, completion time, deaths, and score.

---

## Component Definitions

### AscensionState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/AscensionComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss
{
    /// <summary>
    /// Current ascension configuration for the active expedition.
    /// Stored on the run state entity (Roguelite/ RunLifecycleSystem).
    /// Populated from AscensionDefinitionSO at run start.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct AscensionState : IComponentData
    {
        /// <summary>Current ascension tier (0 = base game, 1-10 = ascension tiers).</summary>
        [GhostField] public byte AscensionLevel;

        /// <summary>Front speed multiplier (1.0 = base, higher = faster Front advance).</summary>
        [GhostField(Quantization = 100)] public float FrontSpeedMultiplier;

        /// <summary>Elite enemy spawn frequency multiplier.</summary>
        [GhostField(Quantization = 100)] public float EliteFrequencyMultiplier;

        /// <summary>Number of simultaneous Strife cards active (1 = base, up to 3).</summary>
        [GhostField] public byte ActiveStrifeSlots;

        /// <summary>How often Strife cards rotate (in districts cleared). 0 = no rotation.</summary>
        [GhostField] public byte StrifeRotationInterval;

        /// <summary>Number of additional boss variant clauses force-activated.</summary>
        [GhostField] public byte BonusBossClauseCount;

        /// <summary>Maximum revival count per expedition (-1 = unlimited).</summary>
        [GhostField] public sbyte MaxRevivals;

        /// <summary>Global damage multiplier for enemies.</summary>
        [GhostField(Quantization = 100)] public float EnemyDamageMultiplier;

        /// <summary>Global health multiplier for enemies.</summary>
        [GhostField(Quantization = 100)] public float EnemyHealthMultiplier;

        /// <summary>Whether new enemy variants are enabled at this tier.</summary>
        [GhostField] public bool NewVariantsEnabled;
    }
}
```

### AscensionProgressState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/AscensionComponents.cs (continued)
namespace Hollowcore.Boss
{
    /// <summary>
    /// Persistent meta-progression tracking for ascension tier unlocks.
    /// Stored in save data (RunHistorySaveModule), loaded at meta screen.
    /// </summary>
    public struct AscensionProgressState : IComponentData
    {
        /// <summary>Highest ascension tier unlocked (0 = base only, 10 = all unlocked).</summary>
        public byte HighestUnlockedTier;

        /// <summary>Highest tier completed (may be lower than unlocked if player unlocked but hasn't cleared).</summary>
        public byte HighestCompletedTier;

        /// <summary>Total expeditions completed across all tiers.</summary>
        public int TotalExpeditionsCompleted;

        /// <summary>Best score achieved at each tier (fixed array, index 0-10).</summary>
        public int BestScoreTier0;
        public int BestScoreTier1;
        public int BestScoreTier2;
        public int BestScoreTier3;
        public int BestScoreTier4;
        public int BestScoreTier5;
        public int BestScoreTier6;
        public int BestScoreTier7;
        public int BestScoreTier8;
        public int BestScoreTier9;
        public int BestScoreTier10;

        public int GetBestScore(int tier) => tier switch
        {
            0 => BestScoreTier0, 1 => BestScoreTier1, 2 => BestScoreTier2,
            3 => BestScoreTier3, 4 => BestScoreTier4, 5 => BestScoreTier5,
            6 => BestScoreTier6, 7 => BestScoreTier7, 8 => BestScoreTier8,
            9 => BestScoreTier9, 10 => BestScoreTier10, _ => 0
        };
    }
}
```

### LeaderboardEntry (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/AscensionComponents.cs (continued)
using Unity.Collections;

namespace Hollowcore.Boss
{
    /// <summary>
    /// Local leaderboard entry. Buffer on a persistent leaderboard entity.
    /// </summary>
    [InternalBufferCapacity(10)]
    public struct LeaderboardEntry : IBufferElementData
    {
        public FixedString64Bytes PlayerName;
        public int Seed;
        public byte AscensionLevel;
        public float CompletionTimeSeconds;
        public int DeathCount;
        public int FinalScore;
        public byte DistrictsCleared;
        public InfluenceFaction FinalBossFaction;
    }
}
```

---

## ScriptableObject Definitions

### AscensionTierSO

```csharp
// File: Assets/Scripts/Boss/Definitions/AscensionTierSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Boss.Definitions
{
    [CreateAssetMenu(fileName = "AscensionTier", menuName = "Hollowcore/Boss/Ascension Tier")]
    public class AscensionTierSO : ScriptableObject
    {
        [Header("Tier")]
        public int TierLevel;
        public string TierName;
        [TextArea] public string TierDescription;
        public Sprite TierIcon;
        public Color TierColor = Color.white;

        [Header("Difficulty Modifiers")]
        public float FrontSpeedMultiplier = 1f;
        public float EliteFrequencyMultiplier = 1f;
        public float EnemyDamageMultiplier = 1f;
        public float EnemyHealthMultiplier = 1f;

        [Header("Strife")]
        [Tooltip("Number of simultaneous Strife card slots")]
        public int ActiveStrifeSlots = 1;
        [Tooltip("Strife cards rotate every N districts (0 = no rotation)")]
        public int StrifeRotationInterval;

        [Header("Boss")]
        [Tooltip("Additional boss variant clauses force-activated")]
        public int BonusBossClauseCount;

        [Header("Survival")]
        [Tooltip("Max revivals per expedition (-1 = unlimited)")]
        public int MaxRevivals = -1;

        [Header("Enemies")]
        public bool EnableNewVariants;
        [Tooltip("New enemy variant definitions available at this tier")]
        public List<EnemyVariantReference> NewVariants = new();

        [Header("Rewards")]
        [Tooltip("Exclusive rewards for completing this tier")]
        public List<AscensionReward> TierRewards = new();

        [Header("Meta-Currency Multiplier")]
        [Tooltip("Multiplier applied to all meta-currency earned at this tier")]
        public float MetaCurrencyMultiplier = 1f;
    }

    [System.Serializable]
    public struct EnemyVariantReference
    {
        public int EnemyId;
        public string VariantName;
    }

    [System.Serializable]
    public class AscensionReward
    {
        public MetaRewardType RewardType;
        public int RewardId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        [Tooltip("Awarded on first completion only")]
        public bool FirstCompletionOnly;
    }
}
```

---

## Ascension Tier Reference

| Tier | Front Speed | Elite Freq | Strife Slots | Strife Rotation | Boss Clauses | Revivals | New Variants | Enemy Dmg | Enemy HP |
|---|---|---|---|---|---|---|---|---|---|
| 0 (Base) | 1.0x | 1.0x | 1 | None | 0 | Unlimited | No | 1.0x | 1.0x |
| 1 | 1.15x | 1.1x | 1 | None | 0 | Unlimited | No | 1.05x | 1.1x |
| 2 | 1.3x | 1.2x | 1 | None | 0 | 5 | No | 1.1x | 1.2x |
| 3 | 1.5x | 1.35x | 1 | None | 1 | 4 | No | 1.15x | 1.3x |
| 4 | 1.5x | 1.35x | 1 | 2 districts | 1 | 3 | Yes | 1.2x | 1.4x |
| 5 | 1.7x | 1.5x | 1 | 2 districts | 2 | 3 | Yes | 1.3x | 1.5x |
| 6 | 1.7x | 1.5x | 2 | 2 districts | 2 | 2 | Yes | 1.4x | 1.6x |
| 7 | 2.0x | 1.75x | 2 | 1 district | 3 | 2 | Yes | 1.5x | 1.75x |
| 8 | 2.0x | 2.0x | 2 | 1 district | 3 | 1 | Yes | 1.6x | 2.0x |
| 9 | 2.5x | 2.0x | 3 | 1 district | 4 | 1 | Yes | 1.75x | 2.25x |
| 10 | 3.0x | 2.5x | 3 | 1 district | All | 0 | Yes | 2.0x | 2.5x |

---

## Systems

### AscensionConfigSystem

```csharp
// File: Assets/Scripts/Boss/Systems/AscensionConfigSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Loads ascension tier configuration at expedition start.
//
// On run start (Roguelite/ RunLifecycleSystem run begin event):
//   1. Read selected ascension level from meta screen / run config
//   2. Load AscensionTierSO for the selected tier
//   3. Populate AscensionState on the run state entity:
//      - FrontSpeedMultiplier, EliteFrequencyMultiplier
//      - ActiveStrifeSlots, StrifeRotationInterval
//      - BonusBossClauseCount, MaxRevivals
//      - EnemyDamageMultiplier, EnemyHealthMultiplier
//      - NewVariantsEnabled
//   4. Apply RuntimeDifficultyScale modifiers from AscensionTierSO
//   5. If NewVariantsEnabled: register new enemy variant prefabs with AI/ spawning system
//   6. If ActiveStrifeSlots > 1: initialize multiple Strife card slots (EPIC 7)
```

### AscensionStrifeRotationSystem

```csharp
// File: Assets/Scripts/Boss/Systems/AscensionStrifeRotationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Rotates Strife cards at ascension-defined intervals.
//
// When DistrictCompletionEvent fires:
//   1. Read AscensionState.StrifeRotationInterval
//   2. If interval > 0 and (DistrictsClearedCount % interval == 0):
//      a. For each active Strife slot:
//         - Remove current Strife card
//         - Draw new Strife card from available pool (EPIC 7)
//         - Fire StrifeCardChangedEvent for UI
//      b. Avoid duplicates across simultaneous slots
//   3. Log rotation for expedition summary
```

### AscensionBossClauseSystem

```csharp
// File: Assets/Scripts/Boss/Systems/AscensionBossClauseSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: BossClauseEvaluationSystem
//
// Force-activates additional boss variant clauses based on ascension tier.
//
// When BossClauseEvaluationSystem begins evaluation:
//   1. Read AscensionState.BonusBossClauseCount
//   2. If > 0:
//      a. Get list of boss variant clauses that are NOT already active
//      b. Force-activate N additional clauses (prioritizing highest-impact)
//      c. These clauses CANNOT be disabled by counter tokens at Tier 7+
//   3. At Tier 10: ALL clauses are force-activated regardless of side goals/tokens
```

### LeaderboardSystem

```csharp
// File: Assets/Scripts/Boss/Systems/LeaderboardSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Records expedition results to the local leaderboard.
//
// When ExpeditionSummaryState.Phase == Complete:
//   1. Create LeaderboardEntry from ExpeditionStatistics:
//      - Seed, AscensionLevel, CompletionTimeSeconds
//      - DeathCount, FinalScore, DistrictsCleared
//      - FinalBossFaction
//   2. Insert into LeaderboardEntry buffer (sorted by FinalScore descending)
//   3. Cap buffer at 100 entries (trim lowest)
//   4. Update AscensionProgressState:
//      - If FinalScore > BestScore for this tier: update
//      - If AscensionLevel == HighestCompletedTier + 1: increment HighestCompletedTier
//      - If HighestCompletedTier >= HighestUnlockedTier: unlock next tier
//      - Increment TotalExpeditionsCompleted
//   5. Persist via RunHistorySaveModule
```

### AscensionSelectionBridge

```csharp
// File: Assets/Scripts/Boss/Bridges/AscensionSelectionBridge.cs
// Managed MonoBehaviour — bridges ascension state to meta screen UI.
//
// Meta Screen — Ascension Selection:
//   1. Display all unlocked tiers (0 through HighestUnlockedTier)
//   2. For each tier: show name, icon, color, modifier summary
//   3. Show best score and completion status per tier
//   4. Locked tiers grayed out with "Complete Tier N to unlock" text
//   5. Selected tier shows full modifier breakdown:
//      - Front speed, elite frequency, Strife slots, revival limit
//      - Enemy damage/health multipliers
//      - New variant warnings
//      - Exclusive rewards available
//   6. Leaderboard view: sortable by score, time, deaths, ascension
//   7. On confirm: pass selected tier to RunLifecycleSystem for run start
```

---

## Setup Guide

1. Create AscensionComponents.cs in `Assets/Scripts/Boss/Components/`
2. Create AscensionTierSO assets in `Assets/Data/Boss/Ascension/`:
   - `Tier0_Base.asset` through `Tier10_Apex.asset`
   - Configure modifiers per the tier reference table
3. Wire AscensionConfigSystem to Roguelite/ RunLifecycleSystem run start
4. Wire AscensionState modifiers to:
   - EPIC 3 Front system (FrontSpeedMultiplier)
   - AI/ spawning (EliteFrequencyMultiplier, EnemyDamageMultiplier, EnemyHealthMultiplier)
   - EPIC 7 Strife system (ActiveStrifeSlots, StrifeRotationInterval)
   - EPIC 14 BossClauseEvaluationSystem (BonusBossClauseCount)
   - EPIC 2 death/revival system (MaxRevivals)
5. Create ascension selection UI in meta screen
6. Create leaderboard UI panel (sortable table)
7. Configure exclusive rewards per tier in AscensionTierSO
8. Wire LeaderboardSystem to RunHistorySaveModule persistence
9. Create new enemy variant prefabs for Tier 4+ (at least 3-5 variants)
10. Test full loop: complete expedition at Tier 0 → unlock Tier 1 → start Tier 1 → verify modifiers

---

## Verification

- [ ] AscensionState populates correctly from AscensionTierSO at run start
- [ ] Front speed scales with FrontSpeedMultiplier
- [ ] Elite enemy frequency scales with EliteFrequencyMultiplier
- [ ] Enemy damage and health scale with multipliers
- [ ] Multiple Strife slots work at Tier 6+ (two cards active simultaneously)
- [ ] Strife rotation fires at correct district intervals (Tier 4+)
- [ ] Bonus boss clauses force-activate at correct tier
- [ ] Tier 10: all boss clauses force-activated
- [ ] MaxRevivals correctly limits revival count
- [ ] MaxRevivals == 0 at Tier 10 (permadeath)
- [ ] New enemy variants spawn at Tier 4+
- [ ] Completing a tier unlocks the next tier
- [ ] AscensionProgressState persists across sessions
- [ ] Leaderboard records entries sorted by score
- [ ] Meta screen shows correct unlock state for all tiers
- [ ] Exclusive tier rewards award on first completion
- [ ] Meta-currency multiplier applies at higher tiers
- [ ] Score formula accounts for ascension level bonus

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Boss/Authoring/AscensionBlobBaker.cs
namespace Hollowcore.Boss.Authoring
{
    // AscensionTierSO collection → AscensionBlob (singleton entity)
    //
    // AscensionBlob:
    //   BlobArray<AscensionTierBlob> Tiers (indices 0-10)
    //     - byte TierLevel
    //     - float FrontSpeedMultiplier
    //     - float EliteFrequencyMultiplier
    //     - float EnemyDamageMultiplier
    //     - float EnemyHealthMultiplier
    //     - byte ActiveStrifeSlots
    //     - byte StrifeRotationInterval
    //     - byte BonusBossClauseCount
    //     - sbyte MaxRevivals
    //     - bool EnableNewVariants
    //     - float MetaCurrencyMultiplier
    //     - BlobArray<int> NewVariantEnemyIds (for EnableNewVariants tiers)
    //
    // Baked by AscensionConfigAuthoring on run state singleton prefab.
    // AscensionConfigSystem reads blob instead of loading SOs at runtime.
    // All tier data Burst-accessible without managed allocations.
}
```

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/AscensionTierValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Validation rules for AscensionTierSO collection:
    //
    // 1. Monotonic difficulty increase:
    //    For each field across tiers 0-10:
    //    FrontSpeedMultiplier[i] <= FrontSpeedMultiplier[i+1]
    //    EliteFrequencyMultiplier[i] <= EliteFrequencyMultiplier[i+1]
    //    EnemyDamageMultiplier[i] <= EnemyDamageMultiplier[i+1]
    //    EnemyHealthMultiplier[i] <= EnemyHealthMultiplier[i+1]
    //    Error if any tier is easier than the previous tier.
    //
    // 2. MaxRevivals decreasing (or equal) across tiers:
    //    Tier 0 unlimited (-1), progressively fewer, Tier 10 = 0.
    //    Warning if revivals increase at a higher tier.
    //
    // 3. ActiveStrifeSlots must be in [1, 3]. Error if outside range.
    //    StrifeRotationInterval must be >= 0. Error if negative.
    //
    // 4. BonusBossClauseCount at tier 10 must trigger "all clauses active".
    //    Warning if tier 10 BonusBossClauseCount < maximum clause count of any boss.
    //
    // 5. MetaCurrencyMultiplier must be >= 1.0 at all tiers (higher tiers = more reward).
    //    Warning if any tier has multiplier < 1.0.
    //
    // 6. Tier coverage: exactly 11 AscensionTierSO assets (0-10) must exist.
    //    Error if any tier level missing or duplicated.
    //
    // 7. New enemy variants: if EnableNewVariants == true, NewVariants list must be non-empty.
    //    Warning if enabled but no variants configured.
    //
    // 8. Tier 0 must be identity:
    //    All multipliers == 1.0, ActiveStrifeSlots == 1, StrifeRotationInterval == 0,
    //    BonusBossClauseCount == 0, MaxRevivals == -1, EnableNewVariants == false.
    //    Error if tier 0 deviates from base game.
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/BossWorkstation/AscensionTierBuilderModule.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // AscensionTierBuilderModule : IWorkstationModule — tab in BossDesignerWorkstation
    //
    // [1] Visual Tier Ladder
    //     - Vertical ladder: tier 0 at bottom, tier 10 at top
    //     - Each rung shows: tier name, icon, color, key modifiers summary
    //     - Active modifiers shown as small icons (skull = damage up, shield = HP up, etc.)
    //     - Click a tier to expand full modifier detail panel
    //     - Locked/unlocked status indicator
    //
    // [2] Forced Modifier Overlay
    //     - Per tier: list of forced modifiers (Front speed, elite freq, Strife slots, etc.)
    //     - Visual diff from previous tier highlighted in yellow
    //     - "New at this tier" badge for first-time modifiers
    //
    // [3] Cumulative Difficulty Graph
    //     - X-axis: tiers 0-10
    //     - Y-axis: composite difficulty score (weighted sum of all multipliers)
    //     - Line chart showing difficulty curve
    //     - Ideal: smooth exponential. Warning if any tier is a flat plateau or sharp spike
    //     - Overlay lines for individual modifiers (toggle each)
    //
    // [4] Reward Multiplier Curve
    //     - X-axis: tiers 0-10
    //     - Y-axis: MetaCurrencyMultiplier
    //     - Overlay: expected meta-currency per expedition at each tier
    //     - "Risk/reward ratio" indicator: difficulty increase vs reward increase
    //
    // [5] Economy Projection
    //     - Given: N expeditions per tier, average completion rate
    //     - Output: meta-currency accumulation curve over total play time
    //     - Overlay: unlock costs for all meta-progression items
    //     - "Time to unlock all" estimate
    //     - Warning if accumulation is too slow (> 100 hours) or too fast (< 10 hours)
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/AscensionLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Ascension live tuning:
    //
    //   int ForceAscensionLevel            // -1 = use selected, 0-10 = force tier
    //   float FrontSpeedMultiplierOverride  // -1 = use tier, >0 = override
    //   float EliteFreqMultiplierOverride   // -1 = use tier, >0 = override
    //   float EnemyDamageMultiplierOverride // -1 = use tier, >0 = override
    //   float EnemyHealthMultiplierOverride // -1 = use tier, >0 = override
    //   int ActiveStrifeSlotsOverride       // -1 = use tier, 1-3 = override
    //   int MaxRevivalsOverride             // -99 = use tier, -1 = unlimited, 0+ = override
    //   bool UnlockAllTiers                 // make all tiers available regardless of progress
    //   float MetaCurrencyMultiplierOverride // -1 = use tier, >0 = override
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/AscensionDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Ascension debug overlay (enabled via `ascension_debug 1`):
    //
    // [1] Ascension Modifier Stack Display
    //     - Screen-corner panel listing all active ascension modifiers
    //     - Each modifier: name, value, source tier
    //     - Color-coded by category: red = enemy buffs, blue = player nerfs, purple = mechanic changes
    //
    // [2] Tier Badge
    //     - Top-right badge: "Ascension 5" with tier icon and color
    //     - Tooltip: full modifier summary
    //
    // [3] Revival Counter
    //     - If MaxRevivals != -1: "Revivals: 2/3 remaining"
    //     - Flashes red at 0 remaining
    //
    // [4] Strife Slot Display
    //     - Shows all active Strife card slots (1-3)
    //     - Rotation timer: "Next rotation in 1 district"
    //     - Current cards with icons
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/BossWorkstation/AscensionDifficultyAnalyzer.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Ascension difficulty curve analysis tool.
    //
    // [1] Difficulty Curve Analysis
    //     - For each tier (0-10), compute composite difficulty score:
    //       score = FrontSpeed * EliteFreq * EnemyDmg * EnemyHP * (1 + BonusClauses * 0.2)
    //              * (1 + ActiveStrifeSlots * 0.15) / (MaxRevivals > 0 ? 1 : 2)
    //     - Plot difficulty curve: should be smooth exponential
    //     - Flag tiers where difficulty jumps > 50% from previous (spike warning)
    //     - Flag tiers where difficulty increases < 5% (plateau warning)
    //
    // [2] Player Power vs Enemy Power Ratio
    //     - Input: expected player DPS and HP at each tier (from progression curves)
    //     - Output: power ratio at each tier (player effective power / enemy effective power)
    //     - Target: ratio decreases smoothly from ~2.0 (tier 0) to ~0.8 (tier 10)
    //     - Warning if ratio drops below 0.5 at any tier (likely unbeatable)
    //     - "At tier 10, expected player power vs enemy power ratio: X"
    //
    // [3] Endgame Economy Projection
    //     - Input: average expedition time per tier, completion rate per tier
    //     - Meta-currency accumulation rate = (currencyPerExpedition * MetaCurrencyMultiplier) / expeditionTime
    //     - Plot: cumulative meta-currency over play hours
    //     - Overlay: unlock costs for all meta-progression items (from MetaBank registry)
    //     - Output:
    //       - Hours to unlock 50%, 75%, 100% of meta-progression
    //       - "Currency surplus" or "currency deficit" at endgame
    //       - Optimal tier for farming (highest currency/hour considering completion rate)
    //     - Warning if 100% unlock takes > 200 hours or < 20 hours
    //
    // [4] Revival Impact Analysis
    //     - Simulate expeditions at each tier with revival limit
    //     - Output: completion rate vs revival count
    //     - "At tier 8 (1 revival), expected completion rate: 35%"
    //     - Flag tiers where completion rate drops below 10% (frustration threshold)
}
