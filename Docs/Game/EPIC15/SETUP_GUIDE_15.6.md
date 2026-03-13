# EPIC 15.6 Setup Guide: Ascension Tiers, Modifiers & Reward Multipliers

**Status:** Planned
**Requires:** EPIC 15.5 (ExpeditionSummarySystem, AscensionUnlock); Framework: Roguelite/ (AscensionDefinitionSO, RuntimeDifficultyScale, RunLifecycleSystem); EPIC 7 (Strife), EPIC 14 (Boss Variant Clauses)

---

## Overview

Ascension tiers provide 10 levels of escalating difficulty and replayability. Each tier stacks modifiers on the base expedition: faster Fronts, more elite enemies, rotating Strife cards, additional forced boss variant clauses, reduced revivals, stronger enemies, and eventually multiple simultaneous Strife cards. Completing an expedition at a given tier unlocks the next. Higher tiers award exclusive rewards and meta-currency multipliers. A leaderboard tracks performance.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Roguelite/ RunLifecycleSystem | Run start/end events | Loads ascension config at run start |
| EPIC 15.5 | ExpeditionSummarySystem | Awards ascension unlocks on victory |
| EPIC 7 | Strife system | Multi-slot Strife cards and rotation |
| EPIC 14 | BossClauseEvaluationSystem | Forced clause activation |
| EPIC 2 | Death/revival system | Revival limits |
| EPIC 3 | Front system | Front speed scaling |

### New Setup Required

1. Create 11 `AscensionTierSO` assets (Tier 0 through Tier 10).
2. Configure modifiers per tier following the reference table.
3. Wire `AscensionConfigSystem` to RunLifecycleSystem run start.
4. Wire modifier outputs to Front, AI, Strife, Boss, and revival systems.
5. Create ascension selection UI in the meta screen.
6. Create leaderboard UI panel.

---

## 1. Creating Ascension Tier Assets

**Create:** `Assets > Create > Hollowcore/Boss/Ascension Tier`
**Recommended location:** `Assets/Data/Boss/Ascension/`
**Naming convention:** `AscensionTier_[Level]_[Name].asset` (e.g., `AscensionTier_00_Base.asset`)

Create exactly 11 assets: Tier 0 (base game) through Tier 10 (apex difficulty).

### 1.1 Identity Fields

| Field | Description | Notes |
|-------|-------------|-------|
| `TierLevel` | 0-10 | Must be unique, no gaps |
| `TierName` | Display name | "Base", "Awakened", "Hardened", etc. |
| `TierDescription` | What's different at this tier | Shown on meta screen selection |
| `TierIcon` | Tier badge sprite | Shown on HUD, leaderboard |
| `TierColor` | Theme color | For UI badges and overlays |

### 1.2 Difficulty Modifiers

| Field | Description | Default (Tier 0) | Range |
|-------|-------------|-------------------|-------|
| `FrontSpeedMultiplier` | How fast Fronts advance | 1.0 | 1.0 - 3.0 across tiers. Must be monotonically non-decreasing |
| `EliteFrequencyMultiplier` | Elite enemy spawn rate | 1.0 | 1.0 - 2.5 |
| `EnemyDamageMultiplier` | Global enemy damage scale | 1.0 | 1.0 - 2.0 |
| `EnemyHealthMultiplier` | Global enemy health scale | 1.0 | 1.0 - 2.5 |

### 1.3 Strife Configuration

| Field | Description | Default (Tier 0) | Range |
|-------|-------------|-------------------|-------|
| `ActiveStrifeSlots` | Simultaneous Strife cards | 1 | 1-3. Must be in [1,3] |
| `StrifeRotationInterval` | Cards rotate every N districts cleared | 0 (no rotation) | 0 = none. 1-2 at higher tiers |

### 1.4 Boss Configuration

| Field | Description | Default (Tier 0) | Range |
|-------|-------------|-------------------|-------|
| `BonusBossClauseCount` | Additional clauses force-activated per boss | 0 | 0-All. At Tier 10, ALL clauses forced |

### 1.5 Survival Configuration

| Field | Description | Default (Tier 0) | Range |
|-------|-------------|-------------------|-------|
| `MaxRevivals` | Max revival count per expedition | -1 (unlimited) | -1 = unlimited, 0-5 typical. Tier 10 = 0 (permadeath) |

### 1.6 Enemy Variants

| Field | Description | Default (Tier 0) | Notes |
|-------|-------------|-------------------|-------|
| `EnableNewVariants` | Whether new enemy types appear | false | Enabled from Tier 4+ |
| `NewVariants` | List of EnemyVariantReference | empty | Enemy IDs + variant names for this tier |

### 1.7 Rewards

| Field | Description | Default (Tier 0) | Range |
|-------|-------------|-------------------|-------|
| `MetaCurrencyMultiplier` | Scale on all meta-currency earned | 1.0 | Must be >= 1.0 at all tiers (higher = more reward) |
| `TierRewards` | Exclusive rewards for completing this tier | empty | FirstCompletionOnly flag for one-time awards |

---

## 2. Tier Reference Table

| Tier | Front | Elite | Strife Slots | Rotation | Boss Clauses | Revivals | Variants | Enemy Dmg | Enemy HP | Currency Mult |
|------|-------|-------|-------------|----------|-------------|----------|----------|-----------|---------|---------------|
| 0 | 1.0x | 1.0x | 1 | None | 0 | Unlimited | No | 1.0x | 1.0x | 1.0x |
| 1 | 1.15x | 1.1x | 1 | None | 0 | Unlimited | No | 1.05x | 1.1x | 1.1x |
| 2 | 1.3x | 1.2x | 1 | None | 0 | 5 | No | 1.1x | 1.2x | 1.2x |
| 3 | 1.5x | 1.35x | 1 | None | 1 | 4 | No | 1.15x | 1.3x | 1.35x |
| 4 | 1.5x | 1.35x | 1 | 2 dist | 1 | 3 | Yes | 1.2x | 1.4x | 1.5x |
| 5 | 1.7x | 1.5x | 1 | 2 dist | 2 | 3 | Yes | 1.3x | 1.5x | 1.7x |
| 6 | 1.7x | 1.5x | 2 | 2 dist | 2 | 2 | Yes | 1.4x | 1.6x | 2.0x |
| 7 | 2.0x | 1.75x | 2 | 1 dist | 3 | 2 | Yes | 1.5x | 1.75x | 2.3x |
| 8 | 2.0x | 2.0x | 2 | 1 dist | 3 | 1 | Yes | 1.6x | 2.0x | 2.7x |
| 9 | 2.5x | 2.0x | 3 | 1 dist | 4 | 1 | Yes | 1.75x | 2.25x | 3.0x |
| 10 | 3.0x | 2.5x | 3 | 1 dist | All | 0 | Yes | 2.0x | 2.5x | 4.0x |

---

## 3. Validation Rules

**Tier 0 must be identity** (all multipliers = 1.0, no modifiers). This is the base game.

**Monotonic difficulty increase:** Each field must be non-decreasing across tiers:
- FrontSpeedMultiplier, EliteFrequencyMultiplier, EnemyDamageMultiplier, EnemyHealthMultiplier must all be >= the previous tier.
- MaxRevivals must be decreasing (or equal) from unlimited toward 0.

**Tier coverage:** Exactly 11 AscensionTierSO assets must exist (0-10). No duplicates, no gaps.

---

## 4. System Integration

### 4.1 Where Modifiers Apply

| Modifier | Target System | How Applied |
|----------|--------------|-------------|
| FrontSpeedMultiplier | EPIC 3 Front system | Scales Front advance timer |
| EliteFrequencyMultiplier | AI/ spawning system | Scales elite spawn probability |
| EnemyDamageMultiplier | AI/ damage calculation | Global scale on enemy attack damage |
| EnemyHealthMultiplier | AI/ spawn health | Scale on enemy MaxHealth at spawn |
| ActiveStrifeSlots | EPIC 7 Strife system | Multiple card slots active simultaneously |
| StrifeRotationInterval | AscensionStrifeRotationSystem | Card swap on DistrictCompletionEvent |
| BonusBossClauseCount | AscensionBossClauseSystem | Force-activates clauses at boss fight start |
| MaxRevivals | EPIC 2 death/revival | Caps revival count for expedition |
| NewVariantsEnabled | AI/ spawning | Registers new enemy prefab variants |
| MetaCurrencyMultiplier | MetaRewardDistributionSystem | Scales all meta-currency awards |

### 4.2 Forced Boss Clause Logic

At Tier 3+, `AscensionBossClauseSystem` runs before `BossClauseEvaluationSystem`:
1. Reads `BonusBossClauseCount`.
2. Gets clauses NOT already active (from side goals, Strife, etc.).
3. Force-activates N additional clauses (highest-impact first).
4. At Tier 7+: forced clauses CANNOT be disabled by counter tokens.
5. At Tier 10: ALL clauses force-activated regardless of everything.

---

## 5. Leaderboard

### 5.1 Entry Fields

| Field | Source | Purpose |
|-------|--------|---------|
| PlayerName | Meta profile | Display |
| Seed | Run config | Reproducibility |
| AscensionLevel | Selected tier | Difficulty context |
| CompletionTimeSeconds | RunHistorySaveModule | Speed metric |
| DeathCount | ExpeditionStatistics | Survival metric |
| FinalScore | Score formula (EPIC 15.5) | Ranking metric |
| DistrictsCleared | ExpeditionStatistics | Completion metric |
| FinalBossFaction | InfluenceMeterState | Which ending |

### 5.2 Leaderboard Configuration

- Maximum 100 entries (oldest/lowest trimmed).
- Sortable by: FinalScore (default), CompletionTime, DeathCount, AscensionLevel.
- Persisted via RunHistorySaveModule.

---

## 6. Meta Screen: Ascension Selection

| Element | Purpose |
|---------|---------|
| Tier ladder | Vertical list, Tier 0 at bottom, 10 at top. Locked tiers grayed out |
| Modifier summary | Per-tier badges showing key modifiers |
| Best score | Personal best per tier with grade (S/A/B/C/D) |
| Full breakdown | Selected tier shows all modifier values |
| Exclusive rewards | Tier-specific rewards listed |
| Confirm button | Starts expedition at selected tier |

---

## Scene & Subscene Checklist

- [ ] 11 AscensionTierSO assets created (Tier 0-10)
- [ ] Tier 0 is identity (all defaults)
- [ ] All fields monotonically non-decreasing across tiers
- [ ] MaxRevivals monotonically non-increasing across tiers
- [ ] MetaCurrencyMultiplier >= 1.0 at all tiers
- [ ] AscensionConfigAuthoring on run state singleton prefab
- [ ] AscensionConfigSystem wired to RunLifecycleSystem
- [ ] All modifier outputs wired (Front, AI, Strife, Boss, revival)
- [ ] Ascension selection UI in meta screen
- [ ] Leaderboard UI panel with sorting
- [ ] New enemy variant prefabs created for Tier 4+ (minimum 3-5)
- [ ] Exclusive tier rewards configured

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Tier 0 has non-identity values | Base game is harder than intended | Reset all Tier 0 fields to defaults (1.0, unlimited, etc.) |
| Non-monotonic difficulty | Tier 5 is easier than Tier 4 | Validator error; fix offending field |
| Missing tier SO (gap in 0-10) | Validator error, crash on tier selection | Create missing AscensionTierSO |
| MetaCurrencyMultiplier < 1.0 | Higher tiers give LESS reward | Validator warning; set >= 1.0 |
| Tier 10 BonusBossClauseCount too low | Not all clauses forced at max difficulty | Set high enough or use "All" flag |
| EnableNewVariants = true but NewVariants empty | Flag set but no variants configured | Add variant references or set flag to false |
| ActiveStrifeSlots > 3 | Out of range | Validator error; set 1-3 |
| MaxRevivals increases at higher tier | Easier survival at harder difficulty | Validator warning; ensure decreasing |

---

## Verification

- [ ] AscensionState populates correctly from AscensionTierSO at run start
- [ ] Front speed scales with FrontSpeedMultiplier
- [ ] Elite enemy frequency scales with EliteFrequencyMultiplier
- [ ] Enemy damage and health scale with multipliers
- [ ] Multiple Strife slots work at Tier 6+ (two cards active)
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
