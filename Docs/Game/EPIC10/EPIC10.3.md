# EPIC 10.3: Reward Distribution & Balancing

**Status**: Planning
**Epic**: EPIC 10 — Reward Economy
**Priority**: High — Tuning layer that makes the economy feel right
**Dependencies**: EPIC 10.1 (RewardCategory), EPIC 10.2 (District Currency); Framework: Loot/ (LootTableSO), Rewards/; EPIC 4 (Districts), EPIC 5 (Echoes), EPIC 14 (Bosses)

---

## Overview

Reward distribution governs where, when, and how much of each reward category the player earns. Six source types feed rewards into the game, each with different reliability, quality, and category focus. Gate reward tags advertise which categories a district emphasizes, letting players make informed routing decisions. The scarcity design ensures the player is always short of at least one category — the engine of the greed-vs-survival tension. A singleton RewardDistributionConfig holds global tuning knobs for designer iteration.

---

## Component Definitions

### RewardSourceType Enum

```csharp
// File: Assets/Scripts/Rewards/Components/RewardDistributionComponents.cs
namespace Hollowcore.Rewards
{
    /// <summary>
    /// Where a reward comes from. Determines base quality, reliability, and category distribution.
    /// </summary>
    public enum RewardSourceType : byte
    {
        Exploration = 0,    // Crates, containers, environmental finds — common, low quality
        SideGoal = 1,       // Quest/objective completion — predictable, high quality, primary source
        Echo = 2,           // Echo encounter completion — best rewards (EPIC 5)
        Boss = 3,           // Boss kill — major reward dump + unique drops
        Event = 4,          // Random events, merchants, discoveries — variable quality
        EnemyDrop = 5       // Enemy kill loot — common, occasional rares from elites
    }
}
```

### RewardDistributionConfig (IComponentData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardDistributionComponents.cs
using Unity.Entities;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Singleton config for global reward distribution tuning.
    /// Baked from RewardDistributionConfigAuthoring. Designers tweak this to balance the economy.
    /// </summary>
    public struct RewardDistributionConfig : IComponentData
    {
        // --- Source quality multipliers (applied to loot table rarity rolls) ---
        public float ExplorationQualityMult;   // Default 0.6 — common, low rarity
        public float SideGoalQualityMult;      // Default 1.2 — above average
        public float EchoQualityMult;          // Default 1.8 — best rewards
        public float BossQualityMult;          // Default 2.0 — guaranteed high rarity
        public float EventQualityMult;         // Default 1.0 — average
        public float EnemyDropQualityMult;     // Default 0.4 — mostly junk

        // --- Source quantity multipliers (applied to drop count) ---
        public float ExplorationQuantityMult;  // Default 1.0
        public float SideGoalQuantityMult;     // Default 1.5
        public float EchoQuantityMult;         // Default 2.0
        public float BossQuantityMult;         // Default 3.0
        public float EventQuantityMult;        // Default 1.0
        public float EnemyDropQuantityMult;    // Default 0.3

        // --- Front phase scaling (multiplied with source multipliers) ---
        public float FrontPhase1Mult;          // Default 1.0 — baseline
        public float FrontPhase2Mult;          // Default 1.3 — better rewards as danger increases
        public float FrontPhase3Mult;          // Default 1.6 — high risk, high reward

        // --- Scarcity knobs ---
        /// <summary>Max reward categories that can appear in a single district (forces gaps).</summary>
        public byte MaxCategoriesPerDistrict;  // Default 4 — always missing 3 categories

        /// <summary>Inventory pressure: total carried items across all categories.</summary>
        public int GlobalCarryLimit;           // Default 30 — forces tough choices

        /// <summary>Currency loss on district exit (percentage of district currency lost).</summary>
        public float DistrictExitCurrencyLoss; // Default 0.25 — lose 25% unspent district currency
    }
}
```

### GateRewardTag (IComponentData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardDistributionComponents.cs
using Unity.Entities;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Attached to gate entities. Declares which reward category this district emphasizes.
    /// Displayed on gate cards to inform player routing decisions.
    /// Not exclusive — just indicates the dominant category.
    /// </summary>
    public struct GateRewardTag : IComponentData
    {
        /// <summary>Primary reward focus advertised on the gate card.</summary>
        public RewardCategory PrimaryFocus;

        /// <summary>Secondary reward category (shown smaller on card). RewardCategory.Face + 128 = none.</summary>
        public RewardCategory SecondaryFocus;

        /// <summary>Abundance level shown as icons (1-3 stars).</summary>
        public byte AbundanceLevel;
    }
}
```

### DistrictRewardProfile (IComponentData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardDistributionComponents.cs
using Unity.Entities;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Per-district reward configuration. Baked from DistrictRewardProfileSO.
    /// Controls which categories appear and at what weights in this district's loot tables.
    /// </summary>
    public struct DistrictRewardProfile : IComponentData
    {
        public int DistrictId;

        // Category weights (0.0 = never drops, 1.0 = standard, 2.0 = double rate)
        public float FaceWeight;
        public float MemoryWeight;
        public float AugmentWeight;
        public float CompendiumPageWeight;
        public float CurrencyWeight;
        public float BossCounterTokenWeight;
        public float LimbSalvageWeight;
    }
}
```

### RewardGrantEvent (IComponentData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardDistributionComponents.cs
using Unity.Entities;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Transient entity created when a reward is actually granted to the player.
    /// Consumed by RewardPresentationBridge (EPIC 10.4) and inventory systems.
    /// </summary>
    public struct RewardGrantEvent : IComponentData
    {
        public Entity TargetPlayer;
        public RewardCategory Category;
        public RewardRarity Rarity;
        public int ItemDefinitionId;      // Specific item/page/token/currency granted
        public int Amount;                 // Quantity (for currency/stackable)
        public RewardSourceType Source;
    }
}
```

---

## ScriptableObject Definitions

### DistrictRewardProfileSO

```csharp
// File: Assets/Scripts/Rewards/Definitions/DistrictRewardProfileSO.cs
using UnityEngine;

namespace Hollowcore.Rewards.Definitions
{
    [CreateAssetMenu(fileName = "NewDistrictRewardProfile", menuName = "Hollowcore/Rewards/District Reward Profile")]
    public class DistrictRewardProfileSO : ScriptableObject
    {
        [Header("District")]
        public int DistrictId;
        public string DistrictName;

        [Header("Category Weights")]
        [Tooltip("0 = never, 1 = standard, 2 = double rate")]
        [Range(0f, 3f)] public float FaceWeight = 0.5f;
        [Range(0f, 3f)] public float MemoryWeight = 0.5f;
        [Range(0f, 3f)] public float AugmentWeight = 0.5f;
        [Range(0f, 3f)] public float CompendiumPageWeight = 0.3f;
        [Range(0f, 3f)] public float CurrencyWeight = 1.0f;
        [Range(0f, 3f)] public float BossCounterTokenWeight = 0f;
        [Range(0f, 3f)] public float LimbSalvageWeight = 0.5f;

        [Header("Gate Card")]
        public Rewards.RewardCategory PrimaryFocus;
        public Rewards.RewardCategory SecondaryFocus;
        [Range(1, 3)] public int AbundanceLevel = 2;

        [Header("Boss Counter Tokens")]
        [Tooltip("Which boss counter token can be found here (from side goals only)")]
        public int BossCounterTokenId = -1;

        [Header("Loot Tables")]
        [Tooltip("Framework LootTableSO used for exploration containers")]
        public Object ExplorationLootTable;
        [Tooltip("Framework LootTableSO used for enemy drops")]
        public Object EnemyDropLootTable;
        [Tooltip("Framework LootTableSO used for side goal rewards")]
        public Object SideGoalLootTable;
    }
}
```

---

## Systems

### RewardDistributionSystem

```csharp
// File: Assets/Scripts/Rewards/Systems/RewardDistributionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: RewardRollRequest (transient entities), DistrictRewardProfile, RewardDistributionConfig,
//        current Front phase, framework LootTableSO
// Writes: Creates RewardGrantEvent entities
//
// Flow:
//   1. Query RewardRollRequest entities (from loot pickup, quest complete, boss kill, etc.)
//   2. For each request:
//      a. Look up DistrictRewardProfile for current district
//      b. Determine source type → apply quality and quantity multipliers from config
//      c. Apply Front phase multiplier
//      d. Roll category from weighted distribution (district profile weights)
//      e. Roll rarity from quality-adjusted rarity table
//      f. Roll specific item from framework LootTableSO for that category + rarity
//      g. Create RewardGrantEvent entity with all resolved data
//   3. For BossCounterToken: never random — only granted by specific side goal completion
//   4. For boss kills: use dedicated boss loot table (guaranteed high rarity + unique drops)
//   5. Destroy request entities via ECB
```

### RewardScarcitySystem

```csharp
// File: Assets/Scripts/Rewards/Systems/RewardScarcitySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: RewardDistributionSystem
//
// Enforces scarcity rules on granted rewards.
//
// Flow:
//   1. Query RewardGrantEvent entities before they're consumed by inventory systems
//   2. Check GlobalCarryLimit: if player's total carried items >= limit, downgrade to currency
//   3. Check per-category CarryLimit: if at cap, convert to currency equivalent
//   4. On district exit: apply DistrictExitCurrencyLoss to unspent district currency
//   5. Fire InventoryFullEvent when items are downgraded (UI: "Inventory full — converted to Creds")
```

### GateRewardTagSystem

```csharp
// File: Assets/Scripts/Rewards/Systems/GateRewardTagSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: DistrictRewardProfileSO database, gate entities
// Writes: GateRewardTag on gate entities
//
// Flow:
//   1. When gates are generated (EPIC 4 district transition):
//      a. Look up DistrictRewardProfileSO for each gate's destination district
//      b. Set PrimaryFocus = highest-weighted category
//      c. Set SecondaryFocus = second-highest (or none if weight < threshold)
//      d. Set AbundanceLevel from profile
//   2. Gate card UI reads GateRewardTag to display "Reward Focus: [Category]" + abundance stars
```

---

## Reward Source Details

### Exploration (Crates, Containers)
- **Reliability**: guaranteed presence, random contents
- **Quality**: low — mostly Common/Uncommon
- **Volume**: high — many containers per district
- **Categories**: weighted by district profile, Currency dominant

### Side Goals (Quests, Objectives)
- **Reliability**: predictable — known reward category shown on quest tracker
- **Quality**: high — Uncommon/Rare guaranteed
- **Volume**: medium — 3-5 per district
- **Categories**: curated per quest, often the district's primary focus
- **Special**: only source of BossCounterTokens (in linked districts)

### Echo Completion
- **Reliability**: guaranteed, but echoes are optional and dangerous
- **Quality**: highest — Rare/Epic guaranteed
- **Volume**: low — 1-2 echoes per district
- **Categories**: weighted toward Augment and LimbSalvage

### Boss Kill
- **Reliability**: guaranteed on kill
- **Quality**: guaranteed Epic+, chance of Legendary
- **Volume**: 1 per district (the district boss)
- **Categories**: major currency dump + unique category item + LimbSalvage (Legendary limb)

### Random Events
- **Reliability**: unpredictable — events may or may not spawn
- **Quality**: variable — Common to Rare
- **Volume**: 0-3 per district
- **Categories**: any, weighted by district profile

### Enemy Drops
- **Reliability**: guaranteed from elites, random chance from normals
- **Quality**: low — mostly Common junk, elites drop Uncommon
- **Volume**: high — many enemies per district
- **Categories**: Currency dominant, occasional Memory or LimbSalvage (temporary)

---

## Setup Guide

1. **Reuse `Assets/Scripts/Rewards/`** folder from EPIC 10.1; add Systems/ subfolder
2. Create `RewardDistributionConfigAuthoring` singleton in subscene with default multipliers:
   - Exploration: quality=0.6, quantity=1.0
   - SideGoal: quality=1.2, quantity=1.5
   - Echo: quality=1.8, quantity=2.0
   - Boss: quality=2.0, quantity=3.0
   - MaxCategoriesPerDistrict=4, GlobalCarryLimit=30, DistrictExitCurrencyLoss=0.25
3. Create DistrictRewardProfileSO for each vertical slice district in `Assets/Data/Rewards/Districts/`
4. Configure category weights per district: emphasize primary focus (weight 2.0+), zero out absent categories
5. Wire quest completion, boss kill, and loot pickup systems to create RewardRollRequest entities
6. Hook GateRewardTagSystem to gate generation in EPIC 4
7. Playtest: verify player cannot satisfy all needs in a single district run

---

## Verification

- [ ] RewardDistributionConfig singleton baked with all tuning values
- [ ] RewardDistributionSystem rolls category from district profile weights
- [ ] Quality multipliers affect rarity distribution (boss drops = mostly Epic+, exploration = mostly Common)
- [ ] Quantity multipliers affect drop count (boss = 3x, enemies = 0.3x)
- [ ] Front phase scaling increases reward quality in later phases
- [ ] BossCounterToken never appears from random rolls — only side goal grants
- [ ] GateRewardTag correctly reflects district's highest-weighted category
- [ ] Gate card UI displays reward focus and abundance level
- [ ] RewardScarcitySystem enforces GlobalCarryLimit — excess converted to currency
- [ ] Per-category carry limits enforced
- [ ] DistrictExitCurrencyLoss applies 25% loss to unspent district currency
- [ ] InventoryFullEvent fires when items downgraded for UI feedback
- [ ] MaxCategoriesPerDistrict ensures at least 3 categories absent from any district
- [ ] RewardGrantEvent entities created with correct source, category, rarity, and item data

---

## BlobAsset Pipeline

DistrictRewardProfileSO is read per reward roll. Convert to blob for Burst-compiled distribution lookups.

```csharp
// File: Assets/Scripts/Economy/Blobs/DistrictRewardProfileBlob.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Economy
{
    public struct DistrictRewardProfileBlob
    {
        public int DistrictId;
        /// <summary>7 floats indexed by (int)RewardCategoryType.</summary>
        public BlobArray<float> CategoryWeights;
        public RewardCategoryType PrimaryFocus;
        public RewardCategoryType SecondaryFocus;
        public byte AbundanceLevel;
        public int BossCounterTokenId;
    }

    public struct DistrictRewardProfileDatabase
    {
        /// <summary>Indexed by DistrictId (0-14). Length = 15.</summary>
        public BlobArray<DistrictRewardProfileBlob> Profiles;
    }

    public struct DistrictRewardProfileDatabaseRef : IComponentData
    {
        public BlobAssetReference<DistrictRewardProfileDatabase> Value;
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Rewards/Definitions/DistrictRewardProfileSO.cs (append to class)

#if UNITY_EDITOR
    private void OnValidate()
    {
        float sum = FaceWeight + MemoryWeight + AugmentWeight + CompendiumPageWeight
                  + CurrencyWeight + BossCounterTokenWeight + LimbSalvageWeight;
        if (sum <= 0f)
            Debug.LogError($"[DistrictRewardProfile] {name}: Total category weight sum is 0 — no rewards can drop.", this);

        // Count non-zero categories
        int nonZero = 0;
        if (FaceWeight > 0) nonZero++;
        if (MemoryWeight > 0) nonZero++;
        if (AugmentWeight > 0) nonZero++;
        if (CompendiumPageWeight > 0) nonZero++;
        if (CurrencyWeight > 0) nonZero++;
        if (BossCounterTokenWeight > 0) nonZero++;
        if (LimbSalvageWeight > 0) nonZero++;

        if (nonZero > 4)
            Debug.LogWarning($"[DistrictRewardProfile] {name}: {nonZero} non-zero categories — MaxCategoriesPerDistrict is 4. " +
                "Some categories will be artificially abundant, reducing scarcity tension.", this);

        if (BossCounterTokenWeight > 0 && BossCounterTokenId < 0)
            Debug.LogError($"[DistrictRewardProfile] {name}: BossCounterTokenWeight > 0 but no BossCounterTokenId set.", this);

        if (AbundanceLevel < 1 || AbundanceLevel > 3)
            Debug.LogError($"[DistrictRewardProfile] {name}: AbundanceLevel must be 1-3.", this);
    }
#endif
```

```csharp
// File: Assets/Editor/Economy/RewardDistributionBuildValidator.cs
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;

namespace Hollowcore.Economy.Editor
{
    public class RewardDistributionBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 2;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:DistrictRewardProfileSO");
            var profiles = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<DistrictRewardProfileSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(so => so != null).ToList();

            foreach (var p in profiles)
            {
                float sum = p.FaceWeight + p.MemoryWeight + p.AugmentWeight +
                            p.CompendiumPageWeight + p.CurrencyWeight +
                            p.BossCounterTokenWeight + p.LimbSalvageWeight;
                if (sum <= 0f)
                    Debug.LogError($"[DistributionBuildValidation] {p.name}: weight sum is 0.");
            }

            // Economy balance warning: check total sources vs sinks per category
            // Sources = sum of (category weight * source quantity multiplier) across all districts
            // Sinks = carry limit caps + district exit loss + conversion loss
            // If sources >> sinks for any category: inflation warning
            // If sinks >> sources: deflation warning (players can never accumulate enough)
            Debug.Log($"[DistributionBuildValidation] Validated {profiles.Count} district reward profiles.");
        }
    }
}
```

---

## Editor Tooling

Distribution tuning is the core of the Economy Dashboard (EPIC 10.1). The **Accumulation Curve Module** specifically serves 10.3:
- Per-category accumulation curve over N districts (configurable district sequence)
- Overlay of all 7 categories simultaneously for cross-category comparison
- Expected value table: mean reward count per district per category
- Front phase impact visualization: how phase 1/2/3 shift the distribution curves
- "Scarcity gap" indicator: highlights which categories are most starved per district sequence

---

## Live Tuning

```csharp
// File: Assets/Scripts/Economy/Debug/DistributionLiveTuning.cs
namespace Hollowcore.Economy.Debug
{
    /// <summary>
    /// Runtime-tunable distribution parameters:
    ///   - RewardDistributionConfig.ExplorationQualityMult through EnemyDropQuantityMult (12 floats)
    ///   - RewardDistributionConfig.FrontPhase1/2/3Mult (3 floats)
    ///   - RewardDistributionConfig.MaxCategoriesPerDistrict (byte)
    ///   - RewardDistributionConfig.GlobalCarryLimit (int)
    ///   - RewardDistributionConfig.DistrictExitCurrencyLoss (float)
    ///   - Per-district category weight overrides
    ///
    /// Pattern: DistributionLiveTuningSystem writes to RewardDistributionConfig singleton
    /// when overrides are dirty. Reads from static DistributionTuningOverrides.
    /// Exposed via Economy Dashboard "Live Tuning" tab.
    /// </summary>
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Economy/Debug/DistributionDebugOverlay.cs
namespace Hollowcore.Economy.Debug
{
    /// <summary>
    /// Debug overlay for reward distribution (development builds):
    /// - Source attribution: each RewardGrantEvent tagged with source icon
    ///   (pickaxe=exploration, flag=sidegoal, skull=boss, spiral=echo, sword=enemy, dice=event)
    /// - Category balance bars: per-category running total vs carry limit
    /// - Scarcity indicator: categories at 0 inventory highlighted in red
    /// - District summary popup on district exit (debug version with raw numbers)
    /// - Toggle: /debug economy distribution
    /// </summary>
}
```

---

## Simulation & Testing

The Monte Carlo simulation (EPIC 10.1 Economy Dashboard) provides the critical testing for distribution:

- **Expected value per district per category**: average rewards a player earns in each district, broken down by category. Validates that the "always short of something" design intent holds
- **Breakpoint analysis**: at what district count do 50%/90% of simulated players hit carry limits per category. If breakpoints are too early, players feel capped; too late, scarcity pressure is insufficient
- **Source contribution analysis**: pie chart of which source types contribute most to each category. Validates that side goals remain the "primary source" as intended
- **Front phase sensitivity**: how much do phase 2/3 multipliers shift accumulation curves. Ensures "high risk, high reward" delivers meaningfully better outcomes
- **Scarcity gap validation**: across 1000 expeditions, what percentage of players are starved (0 inventory) in at least 3 categories at any given point. Target: >80% to maintain tension
