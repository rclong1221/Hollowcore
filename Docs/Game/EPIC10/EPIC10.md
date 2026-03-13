# EPIC 10: Reward Economy

**Status**: Planning
**Priority**: High — Core motivation loop
**Dependencies**: Framework: Rewards/, Loot/, Economy/, Items/; EPIC 1 (Chassis), EPIC 9 (Compendium)
**GDD Sections**: 12.1 Core Reward Categories

---

## Problem

Hollowcore's reward economy is more complex than standard roguelite loot. Six distinct reward categories serve different strategic purposes, and rewards are tightly coupled to the district system (district-specific currencies, limb salvage with district memory, boss counter tokens). The framework has generic Rewards/ and Loot/ systems — this epic specializes them for Hollowcore's economy.

---

## Overview

Six reward categories, each serving a different gameplay need. Gate cards declare which category a district focuses on, letting players make informed decisions about where to go based on what they need. The economy is designed so players are always short of something — creating the greed-vs-survival tension the GDD emphasizes.

---

## Sub-Epics

### 10.1: Reward Category Definitions
The six reward types and their gameplay roles.

- **RewardCategory enum**: Face, Memory, Augment, CompendiumPage, Currency, BossCounterToken, LimbSalvage
- **Per-category definitions** (GDD §12.1):
  - **Face** (infiltration/social): toll skips, disguise checks, identity gate passes
    - Useful in Mirrortown, Chrome Cathedral, The Auction — social/identity districts
  - **Memory** (tempo/combat): reflex loops, dream hacks, borrowed skills
    - Temporary combat buffs, skill unlocks for current district
  - **Augment** (movement/weapon): grapples, sonar lungs, heat shielding, analog kit
    - Persistent equipment upgrades that change traversal options
  - **Compendium Pages**: Scout, Suppression, Insight (EPIC 9)
    - Run-level tactical resources
  - **Currency** (district-specific): secrets, memories, contracts, organs
    - Each district has its own currency — used at local vendors
    - Universal meta-currency for cross-district purchases
  - **Boss Counter Tokens**: "Warden Override Key," "Hymn Scrambler," etc.
    - Specific items that disable boss mechanics (EPIC 14)
    - Found as side goal rewards in thematically linked districts
  - **Limb Salvage**: district-specific prosthetics with memory bonuses (EPIC 1)
    - The most Hollowcore-specific reward type

### 10.2: District Currency System
Per-district economies.

- **DistrictCurrencyDefinitionSO**: CurrencyId, DisplayName, Icon, DistrictId
  - Each district has 1 primary currency (e.g., Necrospire = "Memory Fragments", Wetmarket = "Bio-Tokens")
  - Universal expedition currency (e.g., "Creds") accepted everywhere at worse rates
- **DistrictVendorSystem**: vendors in each district accept local currency
  - Vendor inventory scaled to Front phase (more dangerous = better stock)
  - Some vendors only appear after specific events (boss kill, echo completion)
- **Currency conversion**: local ↔ universal at gate screen vendors (poor exchange rate = incentive to spend locally)
- **Maps to**: existing Economy/ CurrencyTransactionSystem — extend with district-specific currency types

### 10.3: Reward Distribution & Balancing
Where rewards come from and how they're tuned.

- **Reward sources per district**:
  - Zone exploration (crates, containers, environmental finds)
  - Side goal completion (primary reward source — predictable, high quality)
  - Echo completion (best rewards — EPIC 5)
  - Boss kill (major reward dump + unique drops)
  - Random events (merchants, events, discoveries)
  - Enemy drops (common loot, occasional rares)
- **Gate reward tags**: forward gates declare "Reward Focus: [Category]" (GDD §7.1)
  - Not exclusive — just indicates which category is most abundant
  - Influences player gate selection decisions
- **Scarcity design**: players always need more than they can carry
  - Inventory limits force choices (take the limb or the augment?)
  - Currency doesn't carry perfectly between districts (exchange rate loss)
  - Boss counter tokens are district-specific (can't stock up generically)

### 10.4: Reward Presentation
How rewards are surfaced to the player.

- **Reward chest / container UI**: shows contents before taking (informed choice)
- **Side goal reward preview**: quest tracker shows expected reward category
- **Post-district summary**: total rewards earned during district visit
- **Rarity visual language**: consistent color/VFX across all reward types
  - Common (white) → Uncommon (green) → Rare (blue) → Epic (purple) → Legendary (gold)
- **Limb-specific presentation**: salvage preview shows stats, memory bonuses, slot compatibility

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Rewards/ | RewardDefinitionSO extended with Hollowcore categories |
| Loot/ (LootTableSO) | District loot tables reference Hollowcore reward types |
| Economy/ (CurrencyTransactionSystem) | District currencies are currency types in existing system |
| Items/ | Augments, boss counter tokens flow through item pipeline |
| Trading/ | Vendor purchases use existing trade system |
| EPIC 1 (Chassis) | Limb salvage rewards create/equip limb entities |
| EPIC 9 (Compendium) | Compendium Page rewards use page system |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 10.1 (Categories) | None — definitions | — |
| 10.2 (District Currency) | 10.1, EPIC 4 (districts) | — |
| 10.3 (Distribution) | 10.1, 10.2 | EPIC 5 (echo rewards), EPIC 14 (boss rewards) |
| 10.4 (Presentation) | 10.1 | — |

---

## Vertical Slice Scope

- 10.1 (categories) at least Currency, Augment, LimbSalvage, BossCounterToken
- 10.2 (district currency) for vertical slice districts
- 10.3 (distribution) basic tuning pass
- 10.4 (presentation) functional UI, not polished

---

## Tooling & Quality

| Sub-Epic | BlobAsset Pipeline | Validation | Editor Tooling | Live Tuning | Debug Visualization | Simulation & Testing |
|---|---|---|---|---|---|---|
| 10.1 (Categories) | RewardCategoryBlob | SO OnValidate, build-time duplicate/weight checks | Economy Dashboard (sink/source flow) | Drop weights, carry limits | Reward flow overlay | Monte Carlo economy sim |
| 10.2 (District Currency) | DistrictCurrencyBlob | CurrencyId uniqueness, MaxCarry > 0 | Currency flow diagram per district | Conversion rates, MaxCarry | Currency balance bars | Sink-source equilibrium |
| 10.3 (Distribution) | DistrictRewardProfileBlob | Weight sum > 0, max categories check | Accumulation curve projection | Quality/quantity mults, scarcity knobs | Source attribution overlay | Expected value per district per category |
| 10.4 (Presentation) | RarityVisualBlob | Rarity entry completeness | Rarity visual preview | — | — | — |

### CRITICAL: RewardCategory Enum Collision

> **EPIC 10** defines `RewardCategory` in `Hollowcore.Rewards` (Face, Memory, Augment, CompendiumPage, Currency, BossCounterToken, LimbSalvage).
> **EPIC 13** defines `RewardCategory` in `Hollowcore.District` (Limbs, Currency, Weapons, Chassis, Recipes, Augments, Intel).
>
> To resolve this collision, EPIC 10's enum is renamed to `RewardCategoryType` in the `Hollowcore.Economy` namespace. EPIC 13's enum is unchanged (handled by another team). All EPIC 10 sub-epics reference `RewardCategoryType` for the economy-layer reward classification. The two enums serve different purposes: EPIC 10's categorizes the **player-facing reward economy** (what the player collects and spends), while EPIC 13's categorizes **district-level reward focus** (what a district emphasizes as loot theme).
