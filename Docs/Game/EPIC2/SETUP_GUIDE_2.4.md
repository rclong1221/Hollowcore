# EPIC 2.4 Setup Guide: Body Reanimation

**Status:** Planned
**Requires:** EPIC 2.2 (DeadBodyState, DeadBodyInventoryEntry, DeadBodyLimbEntry), Framework AI/ (enemy prefab pipeline, AIBrain), Framework Combat/ (DeathTransitionSystem, damage pipeline), Framework CorpseLifecycle, EPIC 3 (FrontState -- optional for Front-accelerated timing)

---

## Overview

When a player dies, the district claims the body and converts it into a hostile enemy. Each of the 15 districts has a unique reanimation type rooted in its narrative identity -- Necrospire raises recursive specters, Old Growth assimilates augments into root networks, Mirrortown steals your face. The reanimated enemy uses the player's equipped weapons and limb stats (scaled by difficulty) and is classified as a mini-boss. Defeating it recovers your gear plus district-specific bonus loot. In co-op, teammates fight "you."

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Dead body entities | `DeadBodyState` (EPIC 2.2) | Bodies to reanimate |
| Dead body entities | `DeadBodyInventoryEntry` buffer | Loadout for reanimated enemy |
| Dead body entities | `DeadBodyLimbEntry` buffer | Chassis config for reanimated enemy |
| Framework | AI/ pipeline (AIBrain, enemy prefab) | Base enemy spawning |
| Framework | Combat/ (damage, death) | Defeat detection |
| Framework | Loot/ pipeline | Bonus loot drops |
| EPIC 3 | `FrontState` (optional) | Front proximity acceleration |

### New Setup Required

1. Create 15 `ReanimationDefinitionSO` assets (one per district)
2. Create reanimated enemy prefab variants per district
3. Add `ReanimationInProgress` (baked disabled) to dead body entity baker
4. Create VFX prefabs for Claiming and Transforming phases
5. Create reanimation material overrides per district
6. Configure mini-boss AI profile for reanimated enemies
7. Create bonus loot tables per district
8. Set up co-op callout notification

---

## 1. Reanimation Definition Assets

**Create:** `Assets > Create > Hollowcore/Revival/Reanimation Definition`
**Recommended location:** `Assets/Data/Revival/Reanimation/`
**Naming convention:** `Reanimate_[DistrictName].asset` -- e.g., `Reanimate_Necrospire.asset`

### 1.1 Identity

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **ReanimationTypeId** | Unique ID (1-15, one per district) | (required) | 1-15 |
| **ReanimationName** | Display name for UI | (required) | Max 32 chars |
| **DistrictId** | Which district uses this reanimation | (required) | Valid district ID |

### 1.2 Timing

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **ClaimDelay** | Seconds before district begins claiming body | 30 | 10-120 |
| **TransformDuration** | Seconds for transformation phase | 15 | 5-60 |
| **FrontAccelerated** | Front proximity doubles reanimation rate | true | bool |

### 1.3 Enemy Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **EnemyPrefab** | Base enemy prefab for reanimated variant | (required) | Must have AIBrain, Health |
| **DifficultyScale** | Stat multiplier on player's loadout | 1.0 | 0.5-3.0 |
| **UsesPlayerWeapons** | Enemy wields the player's equipped weapons | true | bool |
| **UsesPlayerLimbs** | Enemy uses player's limb stats for chassis | true | bool |

### 1.4 Loot

| Field | Description | Default |
|-------|-------------|---------|
| **BonusLootTable** | Additional loot beyond recovered gear | (required) |

### 1.5 Visuals

| Field | Description | Default |
|-------|-------------|---------|
| **ClaimingVFX** | VFX during Claiming phase (tendrils, energy approaching body) | (required) |
| **TransformingVFX** | VFX during Transforming phase (body changing) | (required) |
| **ReanimationMaterial** | Material override on the reanimated body mesh | (required) |
| **FlavorText** | Narrative description shown in UI | (required) |

**Tuning tip:** ClaimDelay of 30s gives players a reasonable window to return and interrupt. In early districts, set higher (45-60s) so new players have time to understand the mechanic. In late districts, lower (15-20s) for more pressure. FrontAccelerated=true at Phase 3+ can halve the effective claim delay.

---

## 2. Reanimated Enemy Prefab Variants

**Create:** One per district
**Recommended location:** `Assets/Prefabs/Revival/Reanimated/`
**Naming convention:** `Reanimated_[DistrictName].prefab`

### 2.1 Base Prefab Requirements

| Component | Notes |
|-----------|-------|
| `AIBrain` | Set to mini-boss profile: extended aggro range, no leash distance |
| `Health` | Scaled by DifficultyScale at spawn time |
| `DamageableAuthoring` | Standard damage pipeline |
| `ReanimatedEnemy` | Baked with OriginalSoulId=0 (set at runtime) |
| `ReanimatedLoadoutEntry` buffer | Empty (populated from dead body at spawn) |
| `ReanimationBonusLoot` buffer | Empty (populated from definition at spawn) |

### 2.2 Mini-Boss AI Profile

| Setting | Value | Notes |
|---------|-------|-------|
| **AggroRange** | 30m (2x normal) | Always engages within large area |
| **LeashDistance** | Infinite | Never retreats to spawn point |
| **BossHealthBar** | true | Shows boss-style health bar in UI |
| **SpecialAbilities** | District-specific | See section 2.3 |

### 2.3 District-Specific Special Abilities (Examples)

| District | Reanimation Type | Special Ability |
|----------|-----------------|-----------------|
| Necrospire | Recursive Specter | Phases through walls briefly, immune to physical during phase |
| Old Growth | Root Runner | Regenerates near vegetation, leaves slowing root trail |
| The Nursery | Pattern Learner | Adapts to repeated attack patterns, increasing dodge chance |
| The Quarantine | Plague Mutant | Melee applies infection DOT, explodes plague cloud on death |
| Chrome Cathedral | Ascended Faithful | Periodic holy shield, summons 1-2 Seraphim adds |
| Mirrortown | Hollow One | Appears friendly on minimap until within 15m |
| Glitch Quarter | Loop Echo | Spawns 2-3 copies at 50% stats, real one has full stats |
| The Hollows | Void Shell | Invisible beyond 10m, one-shot ambush from stealth |

**Tuning tip:** District special abilities should create memorable encounters, not just harder enemies. The Mirrortown Hollow One's minimap deception is more impactful than raw stat increases. Prioritize unique mechanics over damage/health inflation.

---

## 3. Reanimation Phase Configuration

### 3.1 Phase Timeline

| Phase | Progress Range | Duration | Player Action |
|-------|---------------|----------|---------------|
| **Unclaimed** | N/A | ClaimDelay seconds after death | Normal dead body, fully lootable |
| **Claiming** | 0.0 - 0.3 | ~30% of TransformDuration | Interruptible: approach + "Reclaim Body" |
| **Transforming** | 0.3 - 1.0 | ~70% of TransformDuration | Damage stagger (30% of projected HP, once only) |
| **Complete** | 1.0 | Instant | Enemy spawns, body consumed |

### 3.2 Interruptible Window (Claiming Phase)

| Parameter | Value | Notes |
|-----------|-------|-------|
| **Interrupt interaction** | "Reclaim Body" | Standard interact prompt |
| **Effect of interrupt** | Cancels reanimation, returns to normal dead body | Body becomes lootable again |
| **Cost** | Free | Just requires reaching the body in time |

### 3.3 Damage Stagger (Transforming Phase)

| Parameter | Value | Notes |
|-----------|-------|-------|
| **Damage threshold** | 30% of projected reanimated enemy max HP | Must deal this much damage to the body |
| **Effect of stagger** | Resets Progress by 0.15 | Buys more time |
| **Max staggers** | 1 per transformation | Cannot stagger twice |

---

## 4. VFX & Material Setup

**Recommended location:** `Assets/Prefabs/VFX/Revival/`

### 4.1 Per-District VFX

| VFX | Phase | Example (Necrospire) |
|-----|-------|---------------------|
| **Claiming VFX** | Claiming (0.0-0.3) | Purple spectral tendrils reaching toward body |
| **Transforming VFX** | Transforming (0.3-1.0) | Body distortion, mesh warping, material shift |
| **Spawn VFX** | Complete | Burst effect as enemy emerges |

### 4.2 Per-District Material Override

| Material | Description |
|----------|-------------|
| **ReanimationMaterial** | Shader/texture applied to the body during transformation |
| Example (Necrospire) | Ghostly translucent shader with purple emission |
| Example (Old Growth) | Organic vine overlay with green tint |
| Example (Glitch Quarter) | Digital glitch shader with scan lines |

---

## 5. Bonus Loot Tables

**Create:** Per-district loot table referenced by ReanimationDefinitionSO
**Recommended location:** `Assets/Data/Loot/Reanimation/`

| Field | Description | Default |
|-------|-------------|---------|
| **Table entries** | District-themed bonus items | 2-4 entries |
| **Drop chance** | Probability per entry (these are BONUS, on top of recovered gear) | 50-100% |

**Tuning tip:** Bonus loot should make defeating a reanimated enemy feel rewarding beyond just recovering your gear. District-specific crafting materials, rare consumables, or unique weapon mods work well. The value should scale with the district's difficulty.

---

## 6. Dead Body Entity Additions

`DamageableAuthoring` baker (or a supplemental `ReanimationAuthoring`) should add:

| Component | Baked State | Notes |
|-----------|-------------|-------|
| `ReanimationInProgress` | Disabled | IEnableableComponent, enabled when claiming begins |

This blocks body interaction during the Transforming phase.

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Dead Body Entity Baker | `ReanimationInProgress` (baked disabled) | Blocks interaction during transformation |
| Global Config Subscene | Reanimation database blob singleton | Baked from all 15 ReanimationDefinitionSO assets |
| Ghost Prefab Registry | 15 reanimated enemy prefabs | One per district |
| VFX Assets | 15 x Claiming VFX + 15 x Transforming VFX | Per-district variants |
| Material Assets | 15 x ReanimationMaterial | Per-district visual override |
| Loot Data | 15 x bonus loot tables | Per-district bonus rewards |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Missing ReanimationDefinitionSO for a district | Bodies in that district never reanimate | Ensure exactly 15 assets, one per DistrictId |
| EnemyPrefab missing AIBrain or Health | Reanimated enemy spawns but cannot fight | Validate prefab has all required AI/combat components |
| DifficultyScale set too high (>2.0) for early districts | Reanimated enemy is unkillable for early-game players | Scale 0.8-1.0 for districts 1-5, up to 1.5 for districts 10-15 |
| ClaimDelay too short (<15s) | Players never have time to interrupt, feels unfair | Minimum 20s for early districts, lower in late game |
| ReanimationInProgress not baked disabled | Bodies cannot be looted from spawn (always in reanimation) | Verify authoring bakes the enableable component as disabled |
| Bonus loot table is null | Defeating reanimated enemy only drops recovered gear, feels underwhelming | Always assign a non-empty bonus table |
| Looted bodies not reanimating | District reanimation never triggers on already-looted bodies | Verify `ReanimationTimerSystem` processes IsLooted bodies (frame is still usable) |
| Front acceleration not connected | FrontAccelerated=true but timer never speeds up | Verify ReanimationTimerSystem reads FrontState.Phase for proximity check |
| Co-op: reanimated enemy not mini-boss flagged | No boss health bar, leashes like normal enemy | Verify AIBrain config: AggroRange=30, LeashDistance=infinite, BossBar=true |

---

## Verification

1. **Claim Timer Start** -- Kill player, wait ClaimDelay seconds. Console:
   ```
   [ReanimationTimerSystem] Body (SoulId=12345) entering CLAIMING phase in district Necrospire
   ```

2. **Claiming VFX** -- District-appropriate VFX should appear on the dead body during Claiming phase.

3. **Interrupt During Claiming** -- Approach body during Claiming phase. Interact with "Reclaim Body". Reanimation should cancel, body returns to normal lootable state.

4. **Transforming Phase** -- Let Claiming complete. Body enters Transforming with visual distortion and material override.

5. **Damage Stagger** -- Deal 30% of projected HP in damage to the transforming body. Progress should reset by 0.15.

6. **Reanimation Complete** -- Let transformation finish. Reanimated enemy spawns at body location:
   ```
   [ReanimationSpawnSystem] Reanimated enemy spawned for SoulId=12345: Recursive Specter (DifficultyScale=1.0)
   ```

7. **Enemy Uses Player Gear** -- Reanimated enemy should wield the player's equipped weapons and display the player's limb configuration.

8. **Mini-Boss Behavior** -- Reanimated enemy shows boss health bar, does not leash, has extended aggro range.

9. **Defeat & Loot** -- Kill the reanimated enemy. Original gear drops + bonus loot:
   ```
   [ReanimationDefeatSystem] Reanimated enemy defeated. Dropping 3 weapons, 5 limbs, 500 currency + 2 bonus items
   ```

10. **XP Bonus** -- Verify XP award for killing a reanimated mini-boss is higher than a standard enemy.

11. **Front Acceleration** -- Advance Front to Phase 3. Reanimation timer should progress at 2x speed.

12. **Scar Map Update** -- Check map: skull icon should change from orange (reanimating) to red (reanimated) to defeated state.

13. **Co-op Callout** -- In multiplayer, all party members should see:
    ```
    "Warning: [PlayerName]'s body is being claimed by Necrospire"
    ```

14. **District-Specific Ability** -- Test at least one district's unique ability (e.g., Mirrortown Hollow One appears friendly on minimap until 15m).
