# EPIC 5.3 Setup Guide: Echo Rewards

**Status:** Planned
**Requires:** EPIC 5.1 (Echo Generation), EPIC 5.2 (Echo Encounters), Framework Rewards/, Loot/

---

## Overview

Echo rewards are the primary reason players backtrack. They are always better than the original quest's rewards: guaranteed high-tier loot, exclusive Compendium entries, boss counter tokens, unique limb salvage, and premium revival bodies. Rewards are pre-computed at echo generation time (seed-deterministic) and displayed on the Gate Screen so players can make informed backtrack decisions.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 5.1 | EchoMissionEntry (with RewardMultiplier) | Echo data triggering reward generation |
| EPIC 5.2 | EchoCompletionSystem | Fires reward distribution on echo completion |
| Framework Rewards/ | RewardPoolSO pattern | Base pattern for weighted reward pools |
| Framework Loot/ | Loot spawn system | Spawns EnhancedLoot items |
| Framework Items/ | Inventory system | Receives BossCounterTokens, currency |
| EPIC 1 (Limbs) | LimbPickup entity | Spawns unique limb salvage |
| EPIC 2 (Bodies) | Revival body entity | Spawns premium revival bodies |
| EPIC 9 (Compendium) | Compendium unlock system | Unlocks exclusive entries |

### New Setup Required
1. Create EchoRewardPoolSO assets per quest source type
2. Link each QuestDefinitionSO to its EchoRewardPool
3. Configure guaranteed rewards (echoes must ALWAYS feel worth it)
4. Configure bonus pool for run variety
5. Wire reward display to Gate Screen (EPIC 6)

---

## 1. Creating an EchoRewardPoolSO

**Create:** `Assets > Create > Hollowcore/Echo/Reward Pool`
**Recommended location:** `Assets/Data/Echo/Rewards/`

Naming convention: `EchoRewards_{QuestSourceType}.asset`
Example: `EchoRewards_Combat.asset`, `EchoRewards_Exploration.asset`

### 1.1 Guaranteed Rewards
These are always awarded on echo completion. Echoes should NEVER feel like a waste.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `Type` | EchoRewardType enum | — | See reward types |
| `DefinitionId` | Points to specific reward asset (item ID, compendium entry ID, etc.) | — | Must be valid |
| `BaseQuantity` | Amount at persistence tier 0 | — | 1+ |

### 1.2 Bonus Pool
Additional rewards rolled from a weighted pool. Adds variety between runs.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `Type` | EchoRewardType enum | — | See reward types |
| `DefinitionId` | Specific reward reference | — | Must be valid |
| `Quantity` | Amount per roll | — | 1+ |
| `Weight` | Relative probability (higher = more likely) | — | 0.0-1.0 |

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `BonusRollCount` | How many times to roll the bonus pool per echo completion | 1 | 1-5 |
| `PersistenceScaleMultiplier` | Quantity multiplier per expedition persisted (EPIC 5.4) | 1.25 | 1.0-2.0 |

---

## 2. EchoRewardType Enum

| Value | Description | Distribution System |
|-------|-------------|-------------------|
| `EnhancedLoot` (0) | Rare/Epic guaranteed from reward pool | Framework Loot/ spawn |
| `CompendiumEntry` (1) | Unique entry unavailable elsewhere | EPIC 9 Compendium unlock |
| `BossCounterToken` (2) | Disables a boss mechanic in future encounters | Framework Items/ inventory |
| `UniqueLimb` (3) | Limb with strong memory bonus | EPIC 1 LimbPickup spawn |
| `PremiumRevivalBody` (4) | High-quality revival body cached in zone | EPIC 2 body entity spawn |
| `RunCurrency` (5) | Large currency dump (in-run spending) | Framework Economy/ wallet |
| `MetaCurrency` (6) | Permanent meta-progression currency | Framework Economy/ wallet |

---

## 3. Reward Distribution by Quest Source Type

Use this table as a starting point for which reward types to assign per quest source:

| Echo Source (Skipped Goal Type) | Primary Reward | Secondary Reward | Suggested Pool |
|---|---|---|---|
| Combat side goal | EnhancedLoot (weapon/armor) | RunCurrency | `EchoRewards_Combat.asset` |
| Exploration side goal | UniqueLimb | CompendiumEntry | `EchoRewards_Exploration.asset` |
| Boss prep side goal | BossCounterToken | EnhancedLoot | `EchoRewards_BossPrep.asset` |
| NPC/social side goal | CompendiumEntry | MetaCurrency | `EchoRewards_Social.asset` |
| Containment side goal | PremiumRevivalBody | RunCurrency | `EchoRewards_Containment.asset` |

**Tuning tip:** Combat echoes should lean toward immediate power (loot, currency). Exploration echoes should provide lasting value (limbs, compendium). Boss prep echoes provide strategic advantage (counter tokens). This creates distinct motivations for backtracking to different echo types.

---

## 4. Linking Rewards to Quests

Each `QuestDefinitionSO` needs:

| Field | Description |
|-------|-------------|
| `EchoRewardPool` | Reference to the EchoRewardPoolSO for this quest's echo rewards |

The build validator (`EchoRewardBuildValidator`, callbackOrder=2) checks:
- Every QuestDefinitionSO with `IsEchoEligible=true` has a linked EchoRewardPoolSO
- All DefinitionIds in reward pools reference valid assets
- Reward type coverage: at least one pool per quest source type

---

## 5. Reward Scaling

### 5.1 RewardMultiplier (from EchoMissionEntry)
Applied at echo generation time to BaseQuantity:
```
FinalQuantity = BaseQuantity * RewardMultiplier
```

Where `RewardMultiplier = BaseRewardMultiplier + (RewardPerPersistence * ExpeditionsPersisted)`

| Persistence Tier | RewardMultiplier (default) | Example: BaseQuantity=2 |
|---|---|---|
| Tier 0 (same run) | 2.0x | 4 |
| Tier 1 (1 expedition) | 2.5x | 5 |
| Tier 2 (2 expeditions) | 3.0x | 6 |
| Tier 3 (Legendary) | 3.5x | 7 |
| Tier 4 (Mythic) | 4.0x | 8 |

### 5.2 PersistenceScaleMultiplier (Bonus Pool)
Bonus pool quantities additionally scale by `PersistenceScaleMultiplier^ExpeditionsPersisted`:
```
BonusQuantity = BaseQuantity * PersistenceScaleMultiplier^N
```

At default 1.25: Tier 0=1x, Tier 1=1.25x, Tier 2=1.56x, Tier 3=1.95x.

**Tuning tip:** The combined scaling (RewardMultiplier + PersistenceScaleMultiplier) means Legendary echoes give roughly 3-4x normal echo rewards. This should feel exceptional but not game-breaking. Monitor via the Economy Balance Check in the Reward Simulator.

---

## 6. Gate Screen Reward Preview

For the Gate Screen (EPIC 6) to show echo reward previews on backtrack gates:

1. EchoRewardGenerationSystem pre-computes rewards at echo generation time
2. Reward previews are written to district persistence (DistrictSaveState)
3. Gate Screen reads persisted echo data for each backtrack-accessible district
4. Display format: icon + name for guaranteed rewards, "+" for bonus pool indicator

Example Gate Screen display:
```
District: Necrospire (Phase 2)
  2 Active Echoes
  Rewards: [Rare Limb] [Boss Counter Token] +bonus
  Difficulty: [skull][skull][skull]
```

---

## 7. Editor Tooling

### Reward Panel
**File:** `Assets/Editor/EchoWorkstation/EchoRewardPanel.cs`

| Feature | Description |
|---------|-------------|
| Pool editor | Live editing of EchoRewardPoolSO with pie chart |
| Tier preview | Reward quantities at each persistence tier |
| "Roll Rewards" button | Simulates N rolls with current config |
| Value estimator | Converts all types to "run value units" for comparison |
| Coverage matrix | Quest source types vs reward types, gap highlighting |

### Comparison Tool
**File:** `Assets/Editor/EchoWorkstation/EchoRewardComparisonTool.cs`

| Feature | Description |
|---------|-------------|
| Original vs Echo | Side-by-side reward pools |
| Reward ratio | "Echo is 2.7x more valuable than original" |
| Balance flags | < 1.5x (not worth it), > 5x (too generous) |

### Reward Simulator
**Open:** Expedition Workstation > "Echo Rewards" tab
**File:** `Assets/Editor/EchoWorkstation/EchoRewardSimulator.cs`

| Test | Description |
|------|-------------|
| Reward Monte Carlo (10000) | Frequency histogram, value distribution, per-tier breakdown |
| Economy Balance Check | Full expedition sim: echoes should be 20-30% of total run rewards |
| Balance flags | < 10% (underwhelming), > 50% (overshadowing normal content) |

---

## 8. Scene & Subscene Checklist

- [ ] EchoRewardPoolSO assets at `Assets/Data/Echo/Rewards/` (one per quest source type)
- [ ] Every echo-eligible QuestDefinitionSO has EchoRewardPool reference assigned
- [ ] All reward DefinitionIds reference valid assets (items, compendium entries, etc.)
- [ ] Guaranteed rewards configured for every pool (echoes must always feel worth it)
- [ ] Bonus pool weights sum > 0 for all non-empty pools
- [ ] Gate Screen UI can read echo reward previews from district persistence
- [ ] EchoRewardGenerationSystem runs during echo generation (EPIC 5.1 flow)
- [ ] EchoRewardDistributionSystem runs on echo completion (EPIC 5.2 flow)

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| No guaranteed rewards in pool | Echoes sometimes give nothing (empty bonus rolls) | Always include at least 1 GuaranteedReward |
| GuaranteedReward.BaseQuantity <= 0 | Validation error, zero-quantity reward | Set BaseQuantity to at least 1 |
| DefinitionId = 0 or invalid | Reward fails to distribute, error in console | Verify DefinitionId references a real asset |
| Bonus pool weights sum to 0 | "Weights sum to 0" error, bonus rolls fail | Set at least one weight > 0 |
| BonusRollCount = 0 with non-empty pool | Bonus pool exists but never rolled | Set BonusRollCount >= 1 |
| PersistenceScaleMultiplier < 1.0 | Rewards shrink over time (anti-pattern) | Set >= 1.0; rewards should grow with persistence |
| Missing EchoRewardPool on quest | Build warning, echo generates with no rewards | Assign EchoRewardPoolSO reference |
| Reward ratio < 1.5x vs original quest | Players don't bother with echoes | Increase guaranteed quantities or bonus pool |
| Reward ratio > 5x vs original | Echoes overshadow all other content | Reduce quantities, check Economy Balance Check |

---

## Verification

- [ ] Echo completion awards all guaranteed rewards (check inventory/compendium)
- [ ] Bonus pool rolls are seed-deterministic (same seed = same bonus rewards)
- [ ] RewardMultiplier correctly scales guaranteed quantities
- [ ] PersistenceScaleMultiplier increases bonus quantities for older echoes
- [ ] EnhancedLoot drops through framework Loot/ system
- [ ] CompendiumEntry unlocks permanently via EPIC 9
- [ ] BossCounterToken appears in player inventory
- [ ] UniqueLimb spawns as LimbPickup entity in world
- [ ] Gate Screen shows echo reward previews on backtrack gates
- [ ] Economy Balance Check: echoes = 20-30% of total run rewards
- [ ] Reward Monte Carlo: distribution matches configured weights
