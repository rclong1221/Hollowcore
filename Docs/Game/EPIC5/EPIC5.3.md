# EPIC 5.3: Echo Rewards

**Status**: Planning
**Epic**: EPIC 5 — Echo Missions
**Dependencies**: EPIC 5.1, 5.2; Framework: Rewards/, Loot/; EPIC 1 (limbs), EPIC 9 (Compendium)

---

## Overview

Echo rewards are always better than what the original quest offered. The risk of facing wrongness is compensated with guaranteed high-tier loot, exclusive Compendium entries, boss counter tokens, unique limb salvage, and premium revival bodies. Echo rewards are the primary reason players backtrack.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Echo/Components/EchoRewardComponents.cs
using Unity.Entities;

namespace Hollowcore.Echo
{
    public enum EchoRewardType : byte
    {
        EnhancedLoot = 0,       // Rare/Epic guaranteed from reward pool
        CompendiumEntry = 1,    // Unique entry unavailable elsewhere
        BossCounterToken = 2,   // Disables a boss mechanic (EPIC 14)
        UniqueLimb = 3,         // Limb with strong memory bonus (EPIC 1)
        PremiumRevivalBody = 4, // High-quality revival body cached in zone (EPIC 2)
        RunCurrency = 5,        // Large currency dump
        MetaCurrency = 6        // Permanent meta-progression currency
    }

    /// <summary>
    /// Reward manifest for an echo. Multiple rewards per echo.
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct EchoRewardEntry : IBufferElementData
    {
        public EchoRewardType RewardType;
        public int RewardDefinitionId;  // Points to specific reward SO
        public int Quantity;
        public bool IsClaimed;
    }
}
```

---

## ScriptableObject Definitions

```csharp
// File: Assets/Scripts/Echo/Definitions/EchoRewardPoolSO.cs
using UnityEngine;
using System.Collections.Generic;

namespace Hollowcore.Echo.Definitions
{
    [CreateAssetMenu(fileName = "NewEchoRewardPool", menuName = "Hollowcore/Echo/Reward Pool")]
    public class EchoRewardPoolSO : ScriptableObject
    {
        [Header("Guaranteed Rewards")]
        [Tooltip("Always awarded on echo completion")]
        public List<GuaranteedReward> GuaranteedRewards;

        [Header("Bonus Pool")]
        [Tooltip("Additional rewards rolled from weighted pool")]
        public List<WeightedReward> BonusPool;
        public int BonusRollCount = 1;

        [Header("Scaling")]
        [Tooltip("Reward value multiplier per expedition persisted")]
        public float PersistenceScaleMultiplier = 1.25f;
    }

    [System.Serializable]
    public struct GuaranteedReward
    {
        public EchoRewardType Type;
        public int DefinitionId;
        public int BaseQuantity;
    }

    [System.Serializable]
    public struct WeightedReward
    {
        public EchoRewardType Type;
        public int DefinitionId;
        public int Quantity;
        [Range(0f, 1f)] public float Weight;
    }
}
```

---

## Reward Distribution by Echo Type

| Echo Source (Skipped Goal Type) | Primary Reward | Secondary Reward |
|---|---|---|
| Combat side goal | EnhancedLoot (weapon/armor) | RunCurrency |
| Exploration side goal | UniqueLimb | CompendiumEntry |
| Boss prep side goal | BossCounterToken | EnhancedLoot |
| NPC/social side goal | CompendiumEntry | MetaCurrency |
| Containment side goal | PremiumRevivalBody | RunCurrency |

---

## Systems

### EchoRewardGenerationSystem

```csharp
// File: Assets/Scripts/Echo/Systems/EchoRewardGenerationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// On echo generation (EPIC 5.1), pre-compute rewards:
//   1. Load EchoRewardPoolSO for the source quest
//   2. Roll guaranteed rewards
//   3. Roll bonus pool (seed-deterministic)
//   4. Scale quantities by RewardMultiplier and PersistenceScaleMultiplier
//   5. Store as EchoRewardEntry buffer on echo entity
//   6. Reward preview written to district persistence (for Gate Screen display)
```

### EchoRewardDistributionSystem

```csharp
// File: Assets/Scripts/Echo/Systems/EchoRewardDistributionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// On echo completion (EchoCompletionSystem fires):
//   1. Read EchoRewardEntry buffer
//   2. For each unclaimed reward:
//      - EnhancedLoot: spawn loot entity via Loot/ system
//      - CompendiumEntry: unlock via Compendium system (EPIC 9)
//      - BossCounterToken: add to player inventory (Items/)
//      - UniqueLimb: spawn LimbPickup entity (EPIC 1.3)
//      - PremiumRevivalBody: spawn revival body entity (EPIC 2.3)
//      - RunCurrency/MetaCurrency: add to wallet (Economy/)
//   3. Mark rewards as claimed
//   4. Fire reward notification for UI
```

---

## Setup Guide

1. Create EchoRewardPoolSO assets per quest type: `Assets/Data/Echo/Rewards/`
2. Each QuestDefinitionSO needs `EchoRewardPool` reference field
3. Configure guaranteed rewards — echoes should ALWAYS feel worth it
4. Bonus pool provides variety between runs (seed-deterministic)
5. Gate Screen UI (EPIC 6) needs read access to echo reward previews for backtrack gates

---

## Verification

- [ ] Echo completion awards all guaranteed rewards
- [ ] Bonus pool rolls are seed-deterministic
- [ ] RewardMultiplier correctly scales quantities
- [ ] PersistenceScaleMultiplier increases rewards for older echoes
- [ ] Loot drops through framework Loot/ system
- [ ] Compendium entries unlock permanently
- [ ] Boss counter tokens appear in inventory
- [ ] Unique limbs spawn as LimbPickup entities
- [ ] Gate Screen shows echo reward previews for backtrack decisions

---

## Validation

```csharp
// File: Assets/Scripts/Echo/Definitions/EchoRewardPoolSO.cs (OnValidate)
namespace Hollowcore.Echo.Definitions
{
    public partial class EchoRewardPoolSO
    {
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Guaranteed rewards must have valid definitions
            if (GuaranteedRewards == null || GuaranteedRewards.Count == 0)
                Debug.LogWarning($"[EchoRewardPool '{name}'] No guaranteed rewards — echoes should always feel worth it", this);

            foreach (var gr in GuaranteedRewards)
            {
                if (gr.BaseQuantity <= 0)
                    Debug.LogError($"[EchoRewardPool '{name}'] Guaranteed reward {gr.Type} has quantity={gr.BaseQuantity}", this);
                if (gr.DefinitionId <= 0)
                    Debug.LogError($"[EchoRewardPool '{name}'] Guaranteed reward {gr.Type} has invalid DefinitionId", this);
            }

            // Bonus pool weights must sum > 0 if pool is non-empty
            if (BonusPool != null && BonusPool.Count > 0)
            {
                float weightSum = 0f;
                foreach (var wr in BonusPool) weightSum += wr.Weight;
                if (weightSum <= 0f)
                    Debug.LogError($"[EchoRewardPool '{name}'] Bonus pool weights sum to {weightSum} — must be > 0", this);

                // No zero-weight entries (they waste pool space)
                foreach (var wr in BonusPool)
                    if (wr.Weight <= 0f)
                        Debug.LogWarning($"[EchoRewardPool '{name}'] Bonus entry {wr.Type} has weight={wr.Weight}", this);
            }

            // BonusRollCount must be positive if pool exists
            if (BonusPool != null && BonusPool.Count > 0 && BonusRollCount <= 0)
                Debug.LogError($"[EchoRewardPool '{name}'] BonusRollCount={BonusRollCount} but pool has entries", this);

            // PersistenceScaleMultiplier should be >= 1.0 (rewards should grow, not shrink)
            if (PersistenceScaleMultiplier < 1.0f)
                Debug.LogWarning($"[EchoRewardPool '{name}'] PersistenceScaleMultiplier={PersistenceScaleMultiplier} < 1.0 — rewards shrink over time", this);
        }
#endif
    }
}
```

```csharp
// File: Assets/Editor/Validation/EchoRewardBuildValidator.cs
#if UNITY_EDITOR
namespace Hollowcore.Editor.Validation
{
    public class EchoRewardBuildValidator : UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder => 2;

        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            // Verify every QuestDefinitionSO with isEchoEligible has a linked EchoRewardPoolSO
            // Verify all DefinitionIds in reward pools reference valid assets
            // Verify reward type coverage: at least one pool per quest source type
        }
    }
}
#endif
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/EchoWorkstation/EchoRewardPanel.cs
// Echo reward configuration and preview panel:
//
// Features:
//   - EchoRewardPoolSO selector with live editing
//   - Reward distribution pie chart: shows weighted probability of each bonus reward type
//   - Guaranteed reward list with quantity preview at different persistence tiers:
//     "Tier 0: 2x Run Currency | Tier 1: 2.5x | Tier 2: 3.1x | Tier 3: 3.9x"
//   - "Roll Rewards" button: simulates N reward rolls with current config
//     Shows: frequency distribution, min/max/avg total value per roll
//   - Reward value estimator: converts all reward types to approximate "run value" units
//     for cross-comparison (e.g., 1 BossCounterToken ≈ 500 RunCurrency value)
//   - Coverage matrix: rows = quest source types, columns = reward types
//     Ensures every quest type has appropriate reward types configured

// File: Assets/Editor/EchoWorkstation/EchoRewardComparisonTool.cs
// Side-by-side comparison of echo rewards vs original quest rewards:
//   - Original quest reward pool (from Quest/ framework)
//   - Echo reward pool
//   - "Reward ratio" calculation: how much better is the echo reward?
//   - Target: 2-3x value improvement for echoes
//   - Highlights: pools where ratio is < 1.5x (not worth it) or > 5x (too generous)
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/EchoWorkstation/EchoRewardSimulator.cs
// IExpeditionWorkstationModule — "Echo Rewards" tab.
//
// "Reward Monte Carlo" (10000 completions):
//   Input: EchoRewardPoolSO, persistence tier distribution
//   For each simulated completion:
//     - Roll guaranteed + bonus rewards
//     - Scale by persistence tier
//     - Convert to "value units" for aggregation
//   Output:
//     - Reward type frequency histogram
//     - Value distribution curve (min/median/max/stddev)
//     - Per-tier breakdown: how much does persistence scaling matter?
//     - "Expected value at Tier 0: 450 units, Tier 3: 1125 units"
//
// "Economy Balance Check":
//   Simulate full expedition: 6 districts, 3 echoes per district (avg)
//   Compare: total echo rewards vs total normal quest rewards
//   Target: echoes provide 20-30% of total run rewards (significant but not dominant)
//   Flag if echoes < 10% (underwhelming) or > 50% (overshadowing normal content)
```
