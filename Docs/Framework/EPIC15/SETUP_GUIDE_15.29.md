# SETUP GUIDE 15.29: Weapon Damage Profile & Modifier System

**Status**: Implemented (Phases 1-8)
**Last Updated**: February 11, 2026
**Requires**: EPIC 15.28 setup complete (see `SETUP_GUIDE_15.28.md`)

This guide covers what devs and designers need to configure to give weapons elemental damage types and stackable on-hit effects (bleed, burn, explosions, etc.). The system code is fully wired — this guide covers entity and asset configuration only.

---

## What Changed

Before EPIC 15.29, all weapons dealt `Physical` damage with no way to change the element or add on-hit effects without writing new systems. Now, any weapon can:

- Deal any damage element (Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane, Physical)
- Stack unlimited on-hit modifiers (DOTs, explosions, knockback, bonus damage, etc.)
- Carry modifiers from different sources (Innate, Enchantment, Ammo) for selective removal

**All configuration is done in the Inspector on weapon prefabs.** No code changes needed to create new weapon variants.

---

## What's Automatic (No Setup Required)

These features work out of the box once code is compiled:

| Feature | How It Works |
|---------|-------------|
| Default Physical element | All existing weapons default to `Physical` if no `DamageElement` is set |
| Element propagation | Hit systems read `DamageProfile.Element` automatically — no per-weapon code needed |
| Modifier processing | `CombatResolutionSystem` processes all modifiers on every combat hit |
| Explosion AOE | `ModifierExplosionSystem` handles physics overlap, distance falloff, and damage application |
| Projectile inheritance | Throwable weapons automatically stamp their element + modifiers onto spawned projectiles |
| Damage number theming | UI damage numbers are already colored by element type (from EPIC 15.22) |
| Type conversion | `DamageTypeConverter` maps between elemental types (8) and survival types (6) automatically |

---

## 1. Setting a Weapon's Damage Element

### 1.1 Select the Weapon Prefab

1. Open the weapon prefab in the Project window (e.g., `Assets/Prefabs/Items/Converted/`)
2. Select the root GameObject with the **WeaponAuthoring** component

### 1.2 Set the Damage Element

In the **WeaponAuthoring** Inspector, find the **Damage Profile** section:

| Inspector Field | Description | Default |
|-----------------|-------------|---------|
| **Damage Element** | Base elemental type this weapon deals | Physical |

Available elements:

| Element | DamageEvent Type | Visual Color |
|---------|-----------------|-------------|
| Physical | Physical | White |
| Fire | Heat | Orange/Red |
| Ice | Physical | Blue |
| Lightning | Physical | Yellow |
| Poison | Toxic | Green |
| Holy | Physical | Gold |
| Shadow | Physical | Purple |
| Arcane | Physical | Magenta |

> **Note:** Ice, Lightning, Holy, Shadow, and Arcane map to `Physical` in the survival damage system since they have no survival equivalent. They still display with their themed colors in damage numbers.

### 1.3 What Happens at Runtime

When a weapon with `DamageElement: Fire` hits a target:
1. The `DamageEvent` is created with `Type = Heat` (converted automatically)
2. The `PendingCombatHit` carries `WeaponStats.DamageType = Fire`
3. Combat resolution produces a `CombatResultEvent` with `DamageType = Fire`
4. The damage number appears with fire-themed color and style

---

## 2. Adding Weapon Modifiers

Modifiers are stackable on-hit effects that layer on top of the base damage. A weapon can have unlimited modifiers.

### 2.1 Open the Modifier List

In the **WeaponAuthoring** Inspector, find the **Weapon Modifiers** section. Click **+** to add a new modifier entry.

### 2.2 Modifier Fields

Each modifier entry has these fields:

| Field | Type | Description |
|-------|------|-------------|
| **Type** | Dropdown | The effect type (see table below) |
| **Source** | Dropdown | Origin: `Innate` (permanent), `Enchantment` (removable), `Ammo` (swappable) |
| **Element** | Dropdown | Element for this modifier's damage (independent of weapon's base element) |
| **Bonus Damage** | Float | Flat damage amount (for BonusDamage type), or center damage (for Explosion) |
| **Chance** | Float (0-1) | Proc probability per hit. `1.0` = guaranteed, `0.5` = 50% chance |
| **Duration** | Float | Seconds for DOTs, debuffs, stun |
| **Intensity** | Float | DPS for DOTs, heal % for Lifesteal, speed multiplier for Slow |
| **Radius** | Float | AOE radius in meters (for Explosion, Chain, Cleave). `0` = single target |
| **Force** | Float | Knockback force |

### 2.3 Modifier Types

| Type | Category | Key Fields | Effect |
|------|----------|-----------|--------|
| **BonusDamage** | Passive | BonusDamage, Element | Always applies (ignores Chance). Adds flat elemental damage as a separate `DamageEvent` |
| **Bleed** | Status DOT | Chance, Duration, Intensity | Applies Bleed status effect (Intensity = DPS) |
| **Burn** | Status DOT | Chance, Duration, Intensity | Applies Burn status effect |
| **Freeze** | Status DOT | Chance, Duration, Intensity | Applies Frostbite status effect + slow |
| **Shock** | Status DOT | Chance, Duration, Intensity | Applies Shock status effect |
| **Poison** | Status DOT | Chance, Duration, Intensity | Applies PoisonDOT status effect |
| **Stun** | Debuff | Chance, Duration | Stuns target for Duration seconds |
| **Slow** | Debuff | Chance, Duration, Intensity | Slows target (Intensity = speed multiplier, e.g., `0.5` = half speed) |
| **Weaken** | Debuff | Chance, Duration, Intensity | Reduces target damage output |
| **Explosion** | AOE | Chance, BonusDamage, Radius, Element, Force | Creates AOE explosion at hit point. Quadratic distance falloff |
| **Knockback** | Utility | Chance, Force | Pushes target away (not yet implemented) |
| **Lifesteal** | Utility | Chance, Intensity | Heals attacker for % of damage (not yet implemented) |
| **Chain** | AOE | Chance, Radius | Chains damage to nearby targets (not yet implemented) |
| **Cleave** | AOE | Chance, Radius | Hits all targets in radius (not yet implemented) |

### 2.4 Modifier Source

The `Source` field tracks where a modifier came from, enabling selective removal:

| Source | Use Case | Removal |
|--------|----------|---------|
| **Innate** | Built into the weapon | Never removed |
| **Enchantment** | Applied by rune/enchant system | Removed when enchantment is removed |
| **Ammo** | From ammunition type | Swapped when ammo type changes |

---

## 3. Example Weapon Configurations

### 3.1 Iron Sword (No Modifiers)

| Field | Value |
|-------|-------|
| Damage Element | Physical |
| Modifiers | (none) |

Result: Standard physical damage, white damage numbers.

### 3.2 Fire Sword

| Field | Value |
|-------|-------|
| Damage Element | Physical |
| **Modifier 1** | |
| Type | Burn |
| Source | Innate |
| Chance | 0.8 |
| Duration | 3.0 |
| Intensity | 5.0 |

Result: Physical base hit + 80% chance to apply 5 DPS burn for 3 seconds.

### 3.3 Explosive Mace

| Field | Value |
|-------|-------|
| Damage Element | Physical |
| **Modifier 1** | |
| Type | Explosion |
| Source | Innate |
| Element | Physical |
| Bonus Damage | 30 |
| Chance | 1.0 |
| Radius | 3.0 |
| Force | 15.0 |

Result: Every hit creates a 3m explosion dealing up to 30 damage (quadratic falloff) with knockback.

### 3.4 Frost Rifle (Multiple Modifiers)

| Field | Value |
|-------|-------|
| Damage Element | Physical |
| **Modifier 1** | |
| Type | Freeze |
| Source | Innate |
| Chance | 0.4 |
| Duration | 2.0 |
| Intensity | 0.5 |
| **Modifier 2** | |
| Type | BonusDamage |
| Source | Innate |
| Element | Ice |
| Bonus Damage | 15 |

Result: Every hit adds 15 flat Ice damage. 40% chance to apply a 2-second slow (half speed).

### 3.5 Incendiary Grenade

| Field | Value |
|-------|-------|
| Damage Element | Physical |
| **Modifier 1** | |
| Type | Burn |
| Source | Innate |
| Chance | 1.0 |
| Duration | 4.0 |
| Intensity | 8.0 |
| **Modifier 2** | |
| Type | Explosion |
| Source | Innate |
| Element | Fire |
| Bonus Damage | 50 |
| Chance | 1.0 |
| Radius | 4.0 |

Result: Guaranteed burn (8 DPS for 4s) on direct hit, plus a 4m fire explosion dealing up to 50 damage.

> **Note:** For throwable weapons (grenades), the `ThrowableActionSystem` automatically copies the weapon's `DamageProfile` and all `WeaponModifier` entries onto the spawned projectile entity. No extra setup needed.

---

## 4. Explosion Modifier Details

The Explosion modifier deserves special attention because it involves AOE physics:

### 4.1 How Explosion AOE Works

1. On hit, `CombatResolutionSystem` creates a `ModifierExplosionRequest` entity
2. `ModifierExplosionSystem` processes it (server-only):
   - Physics overlap sphere at hit point with the configured `Radius`
   - Resolves hitbox entities to their root owner
   - Applies quadratic distance falloff: `damage = BonusDamage * (1 - normalizedDistance)^2`
   - Creates `DamageEvent` on each target in range
   - Skips the source entity (attacker) to avoid self-damage
3. Request entity is destroyed after processing

### 4.2 Explosion Parameters

| Parameter | Effect |
|-----------|--------|
| Radius = 2m | Small blast, shotgun-like |
| Radius = 4m | Medium grenade |
| Radius = 8m | Large area denial |
| BonusDamage = 30 | Light splash (enemies at edge take ~7.5) |
| BonusDamage = 100 | Heavy blast (enemies at edge take ~25) |
| Force = 0 | No knockback |
| Force = 15 | Moderate push |

---

## 5. Subscene Reimport

After modifying weapon prefabs that live in subscenes, you must **reimport the subscene** for changes to take effect:

1. Select the subscene asset in the Project window
2. Right-click > **Reimport**
3. Wait for the bake to complete

> **Tip:** Changes to weapon prefabs that are spawned at runtime (not baked into subscenes) take effect immediately on next play.

---

## 6. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Default weapon | Attack with unmodified weapon | Physical damage, white numbers |
| 3 | Element change | Set DamageElement to Fire, attack | Fire-themed damage number color |
| 4 | Burn modifier | Add Burn modifier (Chance=1.0), attack | Target receives Burn status DOT |
| 5 | Explosion modifier | Add Explosion modifier, attack | AOE damage around hit point |
| 6 | BonusDamage | Add BonusDamage modifier (Element=Ice), attack | Extra Ice damage event on target |
| 7 | Proc chance | Set Chance=0.5, attack 10+ times | Effect triggers ~50% of hits |
| 8 | Multiple modifiers | Add 2+ modifiers, attack | Both effects can proc independently |
| 9 | Grenade inheritance | Add modifiers to throwable weapon, throw | Projectile carries weapon's modifiers |
| 10 | No modifiers | Remove all modifiers | Weapon works normally with just base damage |

---

## 7. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| Weapon still does Physical | `DamageElement` not set on prefab | Select weapon prefab, set DamageElement in WeaponAuthoring |
| Modifier never procs | `Chance` set to 0 | Set Chance between 0.01 and 1.0 |
| BonusDamage not applying | Target has no `DamageEvent` buffer | Ensure target has `DamageableAuthoring` component |
| Explosion does no damage | `BonusDamage` is 0, or `Radius` is 0 | Set both to positive values |
| Grenade doesn't inherit modifiers | Grenade spawned on client, not server | Stamping is server-only — verify listen server mode |
| Status effect not visible | `StatusEffectSystem` not processing | Check that target entity has `StatusEffectRequest` buffer |
| Damage numbers wrong color | Element → survival type mapping | Some elements (Ice, Lightning, etc.) map to Physical in survival system; color comes from the theme DamageType on CombatResultEvent |
| Changes not taking effect | Stale subscene bake | Reimport the subscene (right-click > Reimport) |

---

## 8. Designer Reference: What Controls What

| What You Want | What to Configure | Where |
|---------------|-------------------|-------|
| Weapon damage element | `WeaponAuthoring.DamageElement` | Weapon prefab Inspector |
| On-hit bleed/burn/freeze | Add WeaponModifier (Type = Bleed/Burn/Freeze) | Weapon prefab Inspector |
| On-hit explosion | Add WeaponModifier (Type = Explosion) | Weapon prefab Inspector |
| Bonus flat damage | Add WeaponModifier (Type = BonusDamage) | Weapon prefab Inspector |
| Proc chance | `WeaponModifier.Chance` | Weapon prefab Inspector |
| DOT duration/DPS | `WeaponModifier.Duration` / `Intensity` | Weapon prefab Inspector |
| Explosion radius | `WeaponModifier.Radius` | Weapon prefab Inspector |
| Modifier source tracking | `WeaponModifier.Source` | Weapon prefab Inspector |
| Damage number visuals | `DamageFeedbackProfile` | ScriptableObject (see SETUP_GUIDE_15.22) |
| Hitbox multipliers | `HitboxAuthoring.DamageMultiplier` | Enemy prefab child colliders (see SETUP_GUIDE_15.28) |

---

## 9. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Damage number prefabs, colors, scales | SETUP_GUIDE_15.22 |
| Pipeline routing, resolver types | SETUP_GUIDE_15.28 |
| Hitbox setup, stat profiles | SETUP_GUIDE_15.28 Section 1-2 |
| Grenade/explosive voxel destruction | SETUP_GUIDE_15.10 |
| **Weapon damage elements** | **This guide (15.29)** |
| **On-hit modifiers (DOTs, explosions, etc.)** | **This guide (15.29)** |
| **Modifier explosion AOE** | **This guide (15.29)** |
