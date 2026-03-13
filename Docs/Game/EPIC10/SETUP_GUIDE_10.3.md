# EPIC 10.3 Setup Guide: Reward Distribution & Balancing

**Status:** Planned
**Requires:** EPIC 10.1 (RewardCategoryType), EPIC 10.2 (District Currency), EPIC 4 (Districts), Framework Loot/ (LootTableSO), Rewards/

---

## Overview

Reward distribution controls where, when, and how much of each reward category the player earns. Six source types feed rewards into the game with different reliability and quality profiles. Per-district reward profiles weight which categories are abundant or absent. Global scarcity knobs ensure the player is always short of at least one category. Gate reward tags advertise district focus to inform player routing decisions.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 10.1 | RewardCategoryDefinitionSO (x7) | Category metadata and carry limits |
| EPIC 10.2 | DistrictCurrencyDefinitionSO (x16) | Currency type definitions |
| EPIC 4 | District system | DistrictId values, gate entities |
| Framework `Loot/` | LootTableSO | Item selection within categories |

### New Setup Required
- 1 `RewardDistributionConfigAuthoring` singleton in subscene
- 15 `DistrictRewardProfileSO` assets (one per district)
- 1 `DistrictRewardProfileDatabaseAuthoring` singleton in subscene
- Wire reward roll requests from loot, quest, boss, echo, and enemy systems

---

## 1. Create the RewardDistributionConfig Singleton
**Create:** Add `RewardDistributionConfigAuthoring` MonoBehaviour to a subscene GameObject.

### 1.1 Source Quality Multipliers
Applied to loot table rarity rolls. Higher = better rarity distribution.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| ExplorationQualityMult | Crates, containers | 0.6 | 0.1-3.0 |
| SideGoalQualityMult | Quest/objective rewards | 1.2 | 0.1-3.0 |
| EchoQualityMult | Echo completion (EPIC 5) | 1.8 | 0.1-3.0 |
| BossQualityMult | Boss kill rewards | 2.0 | 0.1-3.0 |
| EventQualityMult | Random events | 1.0 | 0.1-3.0 |
| EnemyDropQualityMult | Enemy kill loot | 0.4 | 0.1-3.0 |

### 1.2 Source Quantity Multipliers
Applied to drop count. Higher = more items per reward event.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| ExplorationQuantityMult | Crates, containers | 1.0 | 0.1-5.0 |
| SideGoalQuantityMult | Quest/objective | 1.5 | 0.1-5.0 |
| EchoQuantityMult | Echo completion | 2.0 | 0.1-5.0 |
| BossQuantityMult | Boss kill | 3.0 | 0.1-5.0 |
| EventQuantityMult | Random events | 1.0 | 0.1-5.0 |
| EnemyDropQuantityMult | Enemy kill | 0.3 | 0.1-5.0 |

### 1.3 Front Phase Scaling
Multiplied with source multipliers. Higher Front = better rewards for riskier play.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| FrontPhase1Mult | Baseline | 1.0 | 0.5-2.0 |
| FrontPhase2Mult | Medium danger | 1.3 | 0.5-3.0 |
| FrontPhase3Mult | High danger | 1.6 | 0.5-3.0 |

### 1.4 Scarcity Knobs
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| MaxCategoriesPerDistrict | Cap on non-zero categories per district | 4 | 2-7 |
| GlobalCarryLimit | Total items across all categories | 30 | 10-100 |
| DistrictExitCurrencyLoss | Percentage of district currency lost on exit | 0.25 | 0.0-1.0 |

**Tuning tip:** MaxCategoriesPerDistrict=4 means at least 3 categories are always absent from any given district. This is the core driver of the "always short of something" feeling. Reducing it to 3 increases tension; raising to 5 makes the game more generous.

---

## 2. Create DistrictRewardProfileSO Assets
**Create:** `Assets > Create > Hollowcore/Rewards/District Reward Profile`
**Recommended location:** `Assets/Data/Rewards/Districts/`

Create one per district (15 total).

### 2.1 Per-Profile Configuration
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| DistrictId | Matching district | -- | 1-15 |
| DistrictName | Display name | -- | string |
| FaceWeight | Face category drop rate | 0.5 | 0.0-3.0 |
| MemoryWeight | Memory category drop rate | 0.5 | 0.0-3.0 |
| AugmentWeight | Augment category drop rate | 0.5 | 0.0-3.0 |
| CompendiumPageWeight | Page drop rate | 0.3 | 0.0-3.0 |
| CurrencyWeight | Currency drop rate | 1.0 | 0.0-3.0 |
| BossCounterTokenWeight | Counter token rate | 0.0 | 0.0-3.0 |
| LimbSalvageWeight | Limb drop rate | 0.5 | 0.0-3.0 |
| PrimaryFocus | Highest-weight category | -- | RewardCategory |
| SecondaryFocus | Second-highest category | -- | RewardCategory |
| AbundanceLevel | Gate card star rating | 2 | 1-3 |
| BossCounterTokenId | Which token is available here | -1 | -1 or valid ID |
| ExplorationLootTable | Framework LootTableSO for containers | -- | Object ref |
| EnemyDropLootTable | Framework LootTableSO for enemy drops | -- | Object ref |
| SideGoalLootTable | Framework LootTableSO for quest rewards | -- | Object ref |

### 2.2 Example District Profiles

| District | Primary | Secondary | High Weights | Zero Weights |
|----------|---------|-----------|--------------|--------------|
| Mirrortown | Face | Currency | Face=2.5, Currency=1.5 | BossCounterToken=0 |
| Necrospire | Memory | LimbSalvage | Memory=2.0, LimbSalvage=1.5 | Face=0, Augment=0 |
| Lattice | Augment | CompendiumPage | Augment=2.5, Page=1.0 | Face=0, Memory=0 |
| Wetmarket | LimbSalvage | Augment | Limb=2.5, Augment=1.5 | BossCounterToken=0 |

**Tuning tip:** Aim for 3-4 non-zero categories per district. Set the primary focus to 2.0+ weight, secondary to 1.0-1.5, and keep 2-3 categories at 0. Currency should almost always be present (it is the universal lubricant).

### 2.3 BossCounterToken Special Rules
- BossCounterTokenWeight should be 0 for most districts
- Only districts thematically linked to a boss should have non-zero weight
- Even then, tokens only come from side goal completion, never random drops
- Set BossCounterTokenId to the specific token type available here

---

## 3. Create the DistrictRewardProfileDatabase Singleton
**Create:** Add `DistrictRewardProfileDatabaseAuthoring` to a subscene GameObject.

### 3.1 Inspector Setup
| Field | Value |
|-------|-------|
| Profiles | Drag all 15 DistrictRewardProfileSO assets |

Baker produces `DistrictRewardProfileDatabaseRef` with a `BlobArray` of 15 profiles indexed by DistrictId.

---

## 4. Wire Reward Roll Requests

Systems that trigger rewards must create `RewardRollRequest` transient entities:

| Trigger | RewardSourceType | System |
|---------|------------------|--------|
| Player opens container | Exploration | Loot pickup system |
| Quest completed | SideGoal | Quest completion system |
| Echo completed | Echo | Echo reward system (EPIC 5) |
| Boss killed | Boss | Boss loot system (EPIC 14) |
| Random event reward | Event | Event resolution system |
| Enemy killed | EnemyDrop | Enemy death loot system |

The `RewardDistributionSystem` consumes these requests and produces `RewardGrantEvent` entities with resolved category, rarity, and item data.

---

## 5. Gate Reward Tags

`GateRewardTagSystem` automatically reads `DistrictRewardProfileSO` data to set `GateRewardTag` on gate entities:

| Field | Source |
|-------|--------|
| PrimaryFocus | Highest-weighted category in the profile |
| SecondaryFocus | Second-highest (or none if below threshold) |
| AbundanceLevel | From profile AbundanceLevel field |

Gate card UI reads this to display: **"Reward Focus: Augments (3 stars)"**

---

## 6. Scene & Subscene Checklist

- [ ] `RewardDistributionConfigAuthoring` singleton in subscene with all multiplier defaults
- [ ] `DistrictRewardProfileDatabaseAuthoring` singleton in subscene with all 15 profiles
- [ ] No duplicate config or database authorings across subscenes
- [ ] Loot tables referenced by each profile SO actually exist

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| All 7 category weights non-zero in a district | Violates MaxCategoriesPerDistrict=4; scarcity tension breaks | Zero out at least 3 categories per district |
| Total weight sum is 0 | No rewards can drop in this district; OnValidate error | Ensure at least one category has positive weight |
| BossCounterTokenWeight > 0 but BossCounterTokenId = -1 | Token weight active but no token defined; validator error | Either set both or neither |
| Missing loot table references on profile SO | RewardDistributionSystem cannot resolve specific items | Assign ExplorationLootTable, EnemyDropLootTable, SideGoalLootTable |
| Front phase multipliers all 1.0 | No reward incentive for taking on higher danger | Use escalating values (1.0 / 1.3 / 1.6) |
| GlobalCarryLimit too high | No inventory pressure; players hoard everything | Keep at 30 or lower for meaningful choices |
| DistrictExitCurrencyLoss = 0 | No incentive to spend before leaving a district | Default 0.25 (25% loss) creates spending pressure |

---

## Verification

- [ ] `RewardDistributionConfig` singleton baked with all 18+ tuning values
- [ ] RewardDistributionSystem rolls category from district profile weights
- [ ] Boss drops are mostly Epic+ rarity (quality mult 2.0)
- [ ] Exploration drops are mostly Common (quality mult 0.6)
- [ ] Boss kills produce 3x drop count vs baseline
- [ ] Enemy drops produce 0.3x count (mostly single junk items)
- [ ] Front phase 3 yields measurably better rewards than phase 1
- [ ] BossCounterToken never appears from random distribution rolls
- [ ] GateRewardTag matches district profile highest-weighted category
- [ ] Gate card UI displays reward focus and abundance stars
- [ ] RewardScarcitySystem caps total carried items at GlobalCarryLimit
- [ ] Excess items converted to currency with InventoryFullEvent
- [ ] District exit applies 25% currency loss to unspent district currency
- [ ] At least 3 categories absent from every district (MaxCategoriesPerDistrict=4)
- [ ] Build validator confirms all 15 profiles have valid weight sums
- [ ] Monte Carlo sim (Economy Dashboard) shows "always short" pattern across 1000 runs
