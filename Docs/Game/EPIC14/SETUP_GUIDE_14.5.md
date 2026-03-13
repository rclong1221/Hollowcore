# EPIC 14.5 Setup Guide: Boss Loot Table, Unique Drops & Limb Rewards

**Status:** Planned
**Requires:** EPIC 14.2 (BossEncounterSystem Victory state); Framework: Loot/, Items/, Roguelite/ (MetaBank); EPIC 1 (Boss Limbs), EPIC 4 (District Graph), EPIC 9 (Compendium)

---

## Overview

After defeating a district boss, players receive guaranteed high-quality rewards in a safe loot room: a unique boss limb (Legendary rarity, strongest memory bonuses), counter tokens for other bosses, Compendium entries, and a scaled currency dump. Reward amounts scale with fight performance (death count, active variant clauses, difficulty). The extraction sequence transitions players through the loot room back to the Gate Selection screen while applying district completion effects.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| BossDefinitionSO (14.1) | BossLootTable reference, BaseHealth | Loot table lookup, currency base |
| BossEncounterSystem (14.2) | Victory state | Triggers reward spawn |
| Loot/ framework | LootTableSO | Standard loot roll system |
| Items/ framework | Item entities | Spawned rewards are items |
| EPIC 1 | LimbDefinitionSO | Boss-unique limb definitions |

### New Setup Required

1. Create `LootTableSO` for each boss in `Assets/Data/Boss/LootTables/`.
2. Create boss-unique `LimbDefinitionSO` in `Assets/Data/Chassis/Limbs/Boss/`.
3. Build loot room prefab with reward spawn points and extraction gate.
4. Configure currency amounts and scaling in the boss definition.
5. Wire extraction gate to district completion effects.

---

## 1. Boss Loot Table Configuration

**Create:** Standard `LootTableSO` via the Loot/ framework.
**Recommended location:** `Assets/Data/Boss/LootTables/`
**Naming convention:** `LootTable_Boss_[BossName].asset`

### 1.1 Guaranteed Rewards

Every boss kill guarantees:

| Reward | Source | Quantity | Notes |
|--------|--------|----------|-------|
| Boss Limb | Boss-specific LimbDefinitionSO | 1 | Legendary rarity, Permanent durability |
| Counter Tokens | Cross-district token pool | 1-2 | Random from tokens targeting OTHER bosses |
| Compendium Entry | Boss entry in EPIC 9 | 1 | First kill only |
| Meta-currency | Roguelite/ MetaBank | Large dump | Scaled by performance |
| Run currency | Run economy | Medium dump | Scaled by difficulty |

### 1.2 Random Rolls

| Reward | Roll Count | Source | Notes |
|--------|-----------|--------|-------|
| Bonus Materials | 0-3 items | LootTableSO standard rolls | RNG from configured loot table |

---

## 2. Boss Limb Definition

**Create:** `LimbDefinitionSO` via EPIC 1 framework.
**Location:** `Assets/Data/Chassis/Limbs/Boss/`
**Naming convention:** `Limb_Boss_[BossName]_[LimbType].asset`

### 2.1 Boss Limb Properties

| Field | Value | Notes |
|-------|-------|-------|
| Rarity | Legendary (always) | Boss limbs are the highest tier |
| Durability | Permanent | Never degrades |
| Memory Bonuses | Strongest available | Boss limbs carry the best memory modifiers in the game |
| BossId | Matching boss | Links limb to specific boss for Compendium tracking |

**Tuning tip:** Boss limbs should be noticeably stronger than anything found in the district. They are the primary tangible reward for boss completion and should feel worth the effort.

---

## 3. Currency Scaling

### 3.1 Base Currency

Configure on the BossDefinitionSO (or in a separate reward config):

| Field | Description | Range |
|-------|-------------|-------|
| Base meta-currency | Amount before multipliers | 500-2000 typical |
| Base run currency | Amount before multipliers | 1000-5000 typical |

### 3.2 Performance Multipliers

`BossRewardScalingSystem` calculates multipliers:

| Factor | Formula | Example |
|--------|---------|---------|
| Death bonus | 0 deaths = 1.5x, 1 death = 1.25x, 2+ deaths = 1.0x | Flawless kill = 50% more currency |
| Clause bonus | 1.0 + (ActiveClauseCount * 0.1) | 5 active clauses = 1.5x |
| Difficulty bonus | DifficultyHealthCurve(scale) | Higher difficulty = more reward |
| Ascension bonus | AscensionTierSO.MetaCurrencyMultiplier | Higher tier = more reward |

**Final currency** = Base * DeathBonus * ClauseBonus * DifficultyBonus * AscensionBonus

---

## 4. Loot Room Setup

### 4.1 Loot Room Prefab

**Location:** `Assets/Prefabs/Boss/LootRoom/`

| Element | Purpose | Count |
|---------|---------|-------|
| Reward spawn points | Positions where loot entities appear | 3-5 minimum |
| Extraction gate | Exit trigger with `ExtractionTrigger` component | 1 |
| Ambient lighting | Safe area feel | -- |
| Inventory management area | Space for player to sort items | -- |

### 4.2 Extraction Gate

| Field | Description | Notes |
|-------|-------------|-------|
| `LinkedDistrictId` | Which district this extraction is for | Set per-arena |
| `EncounterEntity` | Link to boss encounter entity | Set at runtime |

The extraction gate uses the framework Interaction/ system. Player interacts to trigger extraction.

---

## 5. District Completion Effects

When the player uses the extraction gate, `ExtractionTriggerSystem` fires:

| Effect | System | Notes |
|--------|--------|-------|
| Forward gates unlock | EPIC 4 District Graph | Districts connected forward become available |
| District Front paused | EPIC 3 Front system | Front timer frozen (not reversed) |
| Power-vacuum events seeded | GDD SS4.3 | New side quests, NPC spawns in cleared districts |
| Scar Map updated | EPIC 12 | Boss completion marker added |
| Screen transition | Roguelite/ RunLifecycleSystem | Transition to Gate Selection screen |

---

## Scene & Subscene Checklist

- [ ] LootTableSO created for each boss in `Assets/Data/Boss/LootTables/`
- [ ] Boss-unique LimbDefinitionSO created (Legendary, Permanent)
- [ ] Loot room prefab with 3-5 reward spawn points
- [ ] Extraction gate entity with ExtractionTrigger component
- [ ] Currency amounts configured (base + scaling)
- [ ] BossDefinitionSO.BossLootTable references a valid LootTableSO
- [ ] Reward summary UI created
- [ ] Extraction gate interaction registered in Interaction/ framework

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| BossLootTable.LootTableId references invalid LootTableSO | Validator error, no loot drops | Create and assign a valid LootTableSO |
| No boss-specific LimbDefinitionSO | Validator warning, missing unique reward | Create limb in `Assets/Data/Chassis/Limbs/Boss/` |
| Currency base amount = 0 | Validator warning, no currency reward | Set base > 0 |
| Boss can drop its own counter token | Token received after it would be useful | Verify cross-district pool excludes self-targeting tokens |
| Arena subscene has no ExtractionTrigger | Validator error, player cannot extract | Add ExtractionTrigger entity to loot room |
| Fewer spawn points than expected rewards | Loot stacks on single point | Add spawn points >= maximum reward count |
| Not wiring to EPIC 4 graph unlocking | Forward gates stay locked after boss kill | Wire ExtractionTriggerSystem to district graph |

---

## Verification

- [ ] Boss victory transitions to RewardDump phase
- [ ] Guaranteed boss limb spawns (Legendary, Permanent)
- [ ] Counter tokens spawn from cross-district pool
- [ ] Compendium entry awarded on first boss kill
- [ ] Currency scales with death count (0 deaths = +50%)
- [ ] Currency scales with active variant clause count
- [ ] Loot room is safe (no enemies, no timer)
- [ ] Extraction gate interaction triggers district completion
- [ ] Forward gates unlock in expedition graph
- [ ] District Front pauses (frozen, not reversed)
- [ ] Power-vacuum events seed in cleared districts
- [ ] Scar Map updates with boss completion marker
- [ ] Screen transitions to Gate Selection after extraction
- [ ] Reward scaling respects RuntimeDifficultyScale and AscensionTierSO
- [ ] Multiple boss kills in same expedition award tokens correctly
