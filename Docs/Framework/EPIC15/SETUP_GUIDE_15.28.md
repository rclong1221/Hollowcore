# SETUP GUIDE 15.28: Unified Combat Resolution Pipeline

**Status**: Implemented (All 8 Phases)
**Last Updated**: February 10, 2026
**Requires**: EPIC 15.22 setup complete (see `SETUP_GUIDE_15.22.md`)

This guide covers what devs and designers need to configure so weapon hits produce rich combat feedback (crits, headshots, backstabs, elemental theming). The pipeline code is fully wired — this guide covers entity and asset configuration only.

---

## What Changed

Before EPIC 15.28, all weapon damage (projectiles, melee, hitscan) produced plain white damage numbers with no context. Now, every weapon hit flows through the combat resolution pipeline, which automatically provides:

- Critical hit rolls based on attacker stats
- Headshot detection from hitbox regions
- Backstab detection from attack direction
- Elemental weakness/resistance indicators
- Hitbox-based damage multipliers (head = 2x, legs = 0.5x)

**No new scene objects, prefabs, or configs are needed.** The pipeline reads existing hitbox and stat components that may already be on your entities.

---

## What's Automatic (No Setup Required)

These features work out of the box once the code is compiled:

| Feature | How It Works |
|---------|-------------|
| Pipeline routing | All three weapon systems (Projectile, Melee, Hitscan) automatically create `PendingCombatHit` entities on the server |
| Duplicate suppression | A hit produces exactly one damage number, not two (dedup between DamageEvent and CombatResultEvent paths) |
| Health pipeline | Unchanged — `DamageEvent` continues handling health subtraction via `DamageApplySystem` |
| Backstab detection | Automatic — compares attack direction to target's forward facing |
| Damage number routing | `CombatUIBridgeSystem` already reads `CombatResultEvent` entities with full HitType/Flags |

---

## 1. Hitbox Setup (Enables Headshots & Limb Damage)

Without hitboxes, all hits default to **Torso** with **1.0x** multiplier. Setting up hitboxes unlocks headshot detection, limb-specific damage, and richer feedback.

> Full hitbox setup instructions are in **SETUP_GUIDE_15.22.md, Section 4**. Summary below.

### 1.1 Root Character

1. Select the **root GameObject** of your character/enemy prefab
2. Add Component: **HitboxOwnerMarker**

### 1.2 Child Hitbox Colliders

For each body region, create a child GameObject with a collider and **HitboxAuthoring**:

| Inspector Field | Description |
|-----------------|-------------|
| **Damage Multiplier** | 0.1 - 5.0 (scaling applied to both health damage and displayed number) |
| **Region** | `Head`, `Torso`, `Arms`, `Legs`, `Hands`, `Feet` |

### 1.3 Recommended Values

| Region | Multiplier | Combat Resolution Effect |
|--------|-----------|--------------------------|
| Head | `2.0` | Headshot flag, +25% crit bonus (Hybrid/StatBasedDirect), guaranteed crit (StatBasedRoll) |
| Torso | `1.0` | Normal damage, standard crit chance |
| Arms | `0.75` | Reduced damage |
| Legs | `0.5` | Half damage |

### 1.4 What Happens at Runtime

When a projectile/melee/hitscan ray hits a child collider with `HitboxAuthoring`:
1. The weapon system reads `Hitbox.DamageMultiplier` and `Hitbox.Region`
2. Damage is multiplied for both health (`DamageEvent`) and combat resolution (`PendingCombatHit`)
3. The resolver checks `HitRegion == Head` to set the `Headshot` flag and apply crit bonuses
4. The UI shows "HEADSHOT" text and uses the Critical damage number prefab

---

## 2. Stat Profile Setup (Enables Crits, Scaling, Resistances)

Without stat components, resolvers use default values: no crit chance, no damage scaling, no elemental resistance. Adding stat profiles unlocks the full combat math.

### 2.1 Create a Stat Profile

1. **Assets** > Right-click > Create > DIG > Combat > **Stat Profile**
2. Save to `Assets/Data/Stats/` (e.g., `Player_Stats.asset`, `SkeletonWarrior_Stats.asset`)

### 2.2 Offensive Stats (On Attackers)

These determine how hard an entity hits. Populated into `AttackStats` ECS component at runtime.

| Field | Range | Effect on Combat Resolution |
|-------|-------|----------------------------|
| Base Attack Power | 0+ | Scales weapon damage: `damage * (1 + AttackPower/100)` |
| Base Spell Power | 0+ | Scales spell/elemental damage |
| Base Crit Chance | 0.0 - 1.0 | Probability of critical hit (0.05 = 5%) |
| Base Crit Multiplier | 1.0+ | Damage multiplier on crit (2.0 = double damage) |
| Base Accuracy | 0+ | Hit chance vs target evasion (StatBasedRoll resolver only) |

### 2.3 Defensive Stats (On Targets)

These reduce incoming damage. Populated into `DefenseStats` ECS component at runtime.

| Field | Range | Effect on Combat Resolution |
|-------|-------|----------------------------|
| Base Defense | 0+ | Flat damage reduction |
| Base Armor | 0+ | Percentage damage reduction |
| Base Evasion | 0.0 - 1.0 | Dodge chance (StatBasedRoll resolver only) |

### 2.4 Attributes (On Both)

Strength, Dexterity, Intelligence, Vitality — used for stat scaling formulas.

| Attribute | Offensive Effect | Defensive Effect |
|-----------|-----------------|------------------|
| Strength | +2% damage per point | — |
| Dexterity | Affects crit calculation | Affects evasion |
| Intelligence | Spell power scaling | — |
| Vitality | — | Health pool |

### 2.5 Elemental Resistances (On Targets)

Each element has a resistance value (0.0 - 1.0). These affect both damage math and UI feedback.

| Resistance | Damage Effect | UI Effect |
|-----------|---------------|-----------|
| > 0.25 | Significant reduction | Down-arrow indicator, "RESIST" styling |
| 0.0 - 0.25 | Minor/no reduction | Normal damage number |
| < -0.1 (weakness) | Increased damage | Up-arrow indicator, pulsing text |

Configure per element: Physical, Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane.

### 2.6 Per-Level Scaling

| Field | Description |
|-------|-------------|
| Attack Power Per Level | Added to base per character level |
| Spell Power Per Level | Added to base per character level |
| Defense Per Level | Added to base per character level |

---

## 3. Weapon Configuration

Weapons are configured via the **WeaponAuthoring** component on weapon prefabs. The fields relevant to combat resolution are:

### 3.1 Key Fields on WeaponAuthoring

| Field | Section | Effect on Combat Resolution |
|-------|---------|----------------------------|
| **Damage** | Shootable / Melee | Base damage passed to resolver as `WeaponStats.BaseDamage` |
| **Range** | Shootable / Melee | Used for hitscan ray length |
| **UseHitscan** | Shootable | Enables server-side raycast hit detection (EPIC 15.28) |

### 3.2 Resolver Type

Currently, all weapon hits use the **Hybrid** resolver (`CombatResolverType.Hybrid`). This means:
- Physics hit is required (projectile/melee/hitscan collision)
- Stat-based damage scaling is applied after hit confirmation
- Crit rolls use attacker's `CritChance` stat
- Headshots provide +25% bonus crit chance

The resolver type is set in code per weapon system, not in the Inspector. To change which resolver a weapon type uses, a programmer must modify the `ResolverType` field in the `PendingCombatHit` creation code.

### 3.3 Understanding the Four Resolver Types

| Resolver | Hit Detection | Damage Scaling | Best For |
|----------|--------------|----------------|----------|
| **PhysicsHitbox** | Physics collision | No stat scaling (weapon damage only) | Pure action games (aiming = skill) |
| **StatBasedDirect** | In range = hit | Full stat scaling, crit rolls | Fast ARPGs (Diablo-like) |
| **StatBasedRoll** | Accuracy vs Evasion roll | Full scaling, miss/graze possible | Tactical RPGs (dice-roll feel) |
| **Hybrid** | Physics collision + stat scaling | Full scaling after hit confirmed | Action-RPGs (aim + stats, current default) |

### 3.4 Damage Type

Weapons currently default to `DamageType.Physical`. To assign elemental damage types to weapons, a programmer must set `WeaponStats.DamageType` in the `PendingCombatHit` creation code in the relevant weapon system.

> **Future work:** Add a `DamageType` field to `WeaponAuthoring` so designers can set elemental types per weapon in the Inspector.

---

## 4. Dual Pipeline Overview

Understanding the two damage pathways helps with debugging and design decisions.

### 4.1 Weapon Hits (Enriched Path — EPIC 15.28)

```
Weapon System (Projectile/Melee/Hitscan)
  |
  +-- DamageEvent (health subtraction, predicted)
  |     -> DamageApplySystem (Burst, server)
  |
  +-- PendingCombatHit (server-only)
        -> CombatResolutionSystem (crit roll, headshot, backstab, flags)
           -> CombatResultEvent
              -> CombatUIBridgeSystem (rich damage number, hitmarker, combo, etc.)
```

**Result:** Rich damage numbers with crits, headshots, elemental colors.

### 4.2 Environmental Damage (Simple Path — Unchanged)

```
Grenade/Hazard/AOE/Fall Damage
  |
  +-- DamageEvent (health subtraction, predicted)
        -> DamageApplySystem (Burst, server)
        -> DamageEventVisualBridgeSystem (enqueues to DamageVisualQueue)
           -> CombatUIBridgeSystem (simple white damage number)
```

**Result:** Simple white damage numbers. No crit/headshot/elemental feedback.

### 4.3 Deduplication

When a weapon creates both a `DamageEvent` and a `PendingCombatHit` for the same target:
- `CombatResolutionSystem` registers the target in `CombatResolvedTargets`
- `DamageEventVisualBridgeSystem` checks this set and skips already-resolved targets
- The set is cleared each frame
- **Result:** Exactly one damage number per hit, never two

---

## 5. Adding a New Enemy (Checklist)

To get full combat feedback on a new enemy:

1. **Root prefab:** Add **HitboxOwnerMarker** component
2. **Child colliders:** Add **HitboxAuthoring** to each body region collider
   - Head collider: Region = `Head`, Multiplier = `2.0`
   - Torso collider: Region = `Torso`, Multiplier = `1.0`
   - *(Optional)* Legs: Region = `Legs`, Multiplier = `0.5`
3. **Stat profile:** Create a **CombatStatProfile** asset with defensive stats and elemental resistances
4. **Health:** Ensure the entity has a `HealthComponent` (for death events and kill feed)

> Steps 1-2 enable headshots and limb damage. Step 3 enables crit interaction and elemental feedback. Step 4 enables kill tracking. All are optional — the pipeline produces basic damage numbers even without them.

---

## 6. Adding a New Weapon (Checklist)

For weapons to produce rich combat feedback:

1. **WeaponAuthoring:** Set **Damage**, **Range**, and weapon type fields as normal
2. **Hitscan weapons:** Enable **UseHitscan** on the WeaponAuthoring component — EPIC 15.28 added full server-side hitscan hit detection
3. **Attacker stats:** Ensure the wielding character has `AttackStats` (via stat profile) for crit chance and damage scaling
4. **No additional setup needed** — the weapon systems automatically create `PendingCombatHit` entities

> Projectile and melee weapons work automatically. Hitscan weapons require `UseHitscan = true` on WeaponAuthoring.

---

## 7. Verification Checklist

Test each feature after setup:

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Host | Click Host, enter game | No console errors or host-time errors |
| 3 | Projectile hit | Shoot enemy with projectile weapon | Rich damage number (colored, scaled by hit type) |
| 4 | Headshot | Shoot enemy's Head hitbox | Larger yellow number + "HEADSHOT" tag |
| 5 | Crit | Hit enemies repeatedly (need CritChance > 0) | Occasional larger golden numbers |
| 6 | Backstab | Attack enemy from behind | "BACKSTAB" tag on damage number |
| 7 | Melee hit | Melee attack an enemy | Rich damage number with hit type |
| 8 | Hitscan hit | Fire hitscan weapon at enemy | Rich damage number (requires UseHitscan enabled) |
| 9 | No double numbers | Any weapon hit | Exactly ONE damage number per hit |
| 10 | Grenade/AOE | Throw grenade at enemy | Simple white damage number (DamageEvent path) |
| 11 | Health consistency | Hit with known damage, check health bar | Displayed number matches actual health lost |
| 12 | Elemental weakness | Hit fire-weak enemy with fire weapon | Up-arrow on damage number |
| 13 | No hitbox fallback | Hit enemy without HitboxAuthoring | Normal damage number (Torso default, no headshot) |
| 14 | No stats fallback | Hit enemy without CombatStatProfile | Normal damage number (no crits, no scaling) |

---

## 8. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| All weapon hits show plain white numbers | EPIC 15.28 code not compiled | Check Unity Console for compilation errors |
| Headshots never trigger | No Head hitbox on enemy | Add child collider with `HitboxAuthoring` (Region: Head) |
| Crits never happen | Attacker has no `AttackStats` or CritChance = 0 | Create a `CombatStatProfile` with CritChance > 0 and assign to attacker |
| Double damage numbers on weapon hits | Dedup system not working | Check that `DamageEventVisualBridgeSystem` is running (auto-created by ECS) |
| Grenade damage shows rich numbers | Grenades shouldn't go through combat resolution | This is a bug — grenades use DamageEvent path only, verify no PendingCombatHit is being created for AOE |
| Backstab never triggers | Target has no `LocalTransform` | Ensure enemy entity has transform components (standard for all entities) |
| Hitscan weapon doesn't hit | `UseHitscan` not enabled, or raycast hits nothing | Enable **UseHitscan** on WeaponAuthoring; verify CollisionFilter settings |
| Damage number doesn't match health lost | Hitbox multiplier mismatch | Both `DamageEvent` and `PendingCombatHit` use the same multiplier — if they differ, check weapon system code |
| "No DamageNumbers provider registered!" | UI adapter not set up | See **SETUP_GUIDE_15.22.md** for `DamageNumbersProAdapter` scene setup |
| Console errors about `CombatResolverType` | Missing assembly reference | Ensure `DIG.Combat.Resolvers` assembly is referenced by weapon system assemblies |

---

## 9. Designer Reference: What Controls What

| What You Want | What to Configure | Where |
|---------------|-------------------|-------|
| Headshot damage multiplier | `HitboxAuthoring.DamageMultiplier` | Child collider on enemy prefab |
| Crit chance | `CombatStatProfile.BaseCritChance` | ScriptableObject on attacker |
| Crit damage multiplier | `CombatStatProfile.BaseCritMultiplier` | ScriptableObject on attacker |
| Elemental resistance | `CombatStatProfile.BaseResistances` | ScriptableObject on target |
| Weapon base damage | `WeaponAuthoring.Damage` | Weapon prefab Inspector |
| Weapon range | `WeaponAuthoring.Range` | Weapon prefab Inspector |
| Hitscan vs projectile | `WeaponAuthoring.UseHitscan` | Weapon prefab Inspector |
| Damage number visuals | `DamageFeedbackProfile` | ScriptableObject (see SETUP_GUIDE_15.22) |
| Damage number prefabs | `DamageNumberConfig` | ScriptableObject (see SETUP_GUIDE_15.22) |

---

## 10. Relationship to EPIC 15.22

| Concern | Guide |
|---------|-------|
| Damage number prefabs, colors, scales | SETUP_GUIDE_15.22 Section 2-3 |
| CombatUIBootstrap scene setup | SETUP_GUIDE_15.22 Section 1 |
| DamageNumbersProAdapter configuration | SETUP_GUIDE_15.22 Section 1.2 |
| Hitmarker, combo, kill feed configs | SETUP_GUIDE_15.22 Section 6 |
| Hitbox collider setup (detailed) | SETUP_GUIDE_15.22 Section 4 |
| Stat profile fields (detailed) | SETUP_GUIDE_15.22 Section 5 |
| **Pipeline routing & weapon config** | **This guide (15.28)** |
| **Resolver types & combat math** | **This guide (15.28)** |
| **Dual pipeline architecture** | **This guide (15.28)** |
| **Weapon & enemy setup checklists** | **This guide (15.28)** |
