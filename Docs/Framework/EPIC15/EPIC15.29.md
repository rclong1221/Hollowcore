# EPIC 15.29: Weapon Damage Profile & Modifier System

**Status:** Implemented (Phases 1-8)
**Last Updated:** February 11, 2026
**Priority:** High (Combat Core)
**Dependencies:**
- EPIC 15.28 (Unified Combat Resolution Pipeline — complete)
- EPIC 15.22 (Floating Damage Text System — complete)
- `CombatResolutionSystem` + 4 resolver implementations
- `StatusEffectSystem` + `StatusEffectRequest` pipeline
- `ProjectileBehaviorComponents` compositional pattern

**Feature:** Data-driven weapon damage types and stackable on-hit modifiers. Any weapon frame (melee, hitscan, bow, projectile, channel) can deal any damage type, and unlimited modifiers stack additional effects on top of the base hit — bleed, burn, explosions, knockback, lifesteal, etc.

**Setup Guide:** See `SETUP_GUIDE_15.29.md` for Unity Editor configuration.

---

## Overview

Currently, every hit system (`SweptMeleeHitboxSystem`, `WeaponFireSystem`, `ProjectileSystem`, `ProjectileBehaviorSystem`) hardcodes `DamageType.Physical` at 10+ locations. There is no way to make a fire sword, poison arrows, or an explosive knockback mace without writing new systems for each weapon variant.

This EPIC introduces two new ECS components:

1. **`DamageProfile`** — A single `IComponentData` on every weapon entity that declares its base element (Physical, Fire, Ice, etc.). Hit systems read this instead of hardcoding Physical.

2. **`WeaponModifier`** — An `IBufferElementData` buffer (unlimited capacity) of stackable on-hit effects. Each modifier has a type (Bleed, Burn, Explosion, Lifesteal, etc.), a proc chance, and parameters. Modifiers are processed in `CombatResolutionSystem` after the resolver returns, creating `StatusEffectRequests`, bonus `DamageEvents`, explosion entities, etc.

The design follows the "weapon IS what it IS, modifiers layer ON TOP" principle:
- A knife does physical damage. Adding a Bleed modifier = physical hit + bleed DOT.
- Adding an Explosion modifier = physical hit + explosion at impact point with bonus damage.
- Modifiers track their source (Innate/Enchantment/Ammo) for selective removal on ammo swaps.

---

## Architecture

```
Weapon Entity (ECS)
+-- DamageProfile          (IComponentData) -- base element identity
+-- WeaponModifier[]       (IBufferElementData) -- stackable on-hit effects
+-- MeleeAction / WeaponFireComponent / BowAction / etc. (existing)
+-- ComboData[] (existing)

Hit Detection (SweptMelee / WeaponFire / Projectile / etc.)
|   reads DamageProfile.Element -> fills WeaponStats.DamageType
|   reads DamageProfile.Element -> fills DamageEvent.Type (via DamageTypeConverter)
|   (replaces all hardcoded DamageType.Physical)
v
CombatResolutionSystem
|   resolver.ResolveAttack() -> CombatResult (base damage, existing flow)
|   NEW: iterate WeaponModifier buffer on weapon entity
|     -> roll proc chances per modifier
|     -> set ProcFlags on result
|     -> append StatusEffectRequests to target (DOTs, debuffs)
|     -> create bonus DamageEvents for flat elemental damage
|     -> queue explosion/knockback entities via ECB
|   create CombatResultEvent (existing, now includes ProcFlags)
v
DamageApplicationSystem (unchanged)
v
StatusEffectSystem (existing, processes DOT/debuff requests)
v
UI (damage numbers already themed by DamageType -- just works)
```

---

## New Components

### DamageProfile (IComponentData)

Base element identity for a weapon. One per weapon entity.

| Field | Type | Description |
|-------|------|-------------|
| Element | DamageType (byte) | Physical, Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane |

**File:** `Assets/Scripts/Weapons/Components/DamageProfile.cs`

### WeaponModifier (IBufferElementData)

Stackable on-hit effect. Unlimited per weapon.

| Field | Type | Description |
|-------|------|-------------|
| Type | ModifierType (byte) | Bleed, Burn, Freeze, Shock, Poison, Lifesteal, Stun, Slow, Weaken, Knockback, Explosion, Chain, Cleave, BonusDamage |
| Source | ModifierSource (byte) | Innate, Enchantment, Ammo |
| Element | DamageType (byte) | Element for this modifier's damage |
| BonusDamage | float | Flat damage for BonusDamage type, or center damage for Explosion |
| Chance | float | 0-1 proc probability per hit (1.0 = guaranteed) |
| Duration | float | Seconds for DOTs/debuffs |
| Intensity | float | DPS for DOTs, % for lifesteal, speed mult for Slow |
| Radius | float | AOE radius for Explosion/Chain/Cleave |
| Force | float | Knockback force |

**File:** `Assets/Scripts/Weapons/Components/WeaponModifier.cs`

### DamageTypeConverter (Static Utility)

Maps between `DIG.Targeting.Theming.DamageType` (8 elemental, used in combat resolution) and `Player.Components.DamageType` (6 survival, used in DamageEvent). Centralizes conversion that was previously duplicated in `DamageEventVisualBridgeSystem`.

**File:** `Assets/Scripts/Combat/Utility/DamageTypeConverter.cs`

### ModifierExplosionRequest (IComponentData)

Event entity created by modifier processing for AOE explosion effects.

| Field | Type | Description |
|-------|------|-------------|
| Position | float3 | Explosion center |
| SourceEntity | Entity | Who caused it |
| Damage | float | Center damage |
| Radius | float | Explosion radius |
| Element | DamageType | Damage element |
| KnockbackForce | float | Push force |

**File:** `Assets/Scripts/Combat/Components/ModifierExplosionRequest.cs`

---

## Implementation Phases

### Phase 1: New Component Files (DONE)
Created `DamageProfile.cs`, `WeaponModifier.cs`, `DamageTypeConverter.cs`.

### Phase 2: Extend StatusEffectType (DONE)
Added combat-relevant types to `StatusEffectType` enum: Shock, PoisonDOT, Stun, Slow, Weaken. Updated `StatusEffectConfig` and `StatusEffectSystem.MapToDamageType()`.

### Phase 3: Authoring & Baking (DONE)
Added `DamageElement` dropdown and `weaponModifiers` list to `WeaponAuthoring`. Updated `WeaponBaker` to bake `DamageProfile` + `WeaponModifier` buffer.

### Phase 4: Hit Systems Read DamageProfile (DONE)
Replaced all hardcoded `DamageType.Physical` in `SweptMeleeHitboxSystem`, `WeaponFireSystem`, `ProjectileSystem`. Refactored `DamageEventVisualBridgeSystem.MapDamageType()` to use `DamageTypeConverter`.

### Phase 5: Modifier Processing (DONE)
Added `BufferLookup<WeaponModifier>` to `CombatResolutionSystem`. After resolver returns `CombatResult`, iterates modifiers — rolls procs, creates `StatusEffectRequests`, bonus `DamageEvents`, explosion/knockback event entities. BonusDamage is passive (always applies, no proc roll).

### Phase 6: ModifierExplosionSystem (DONE)
Created system that queries `ModifierExplosionRequest` entities, does physics overlap sphere, applies area damage with quadratic distance falloff, destroys request entities. Server-only.

### Phase 7: Projectile Stamping (DONE)
In `ThrowableActionSystem`, stamps `DamageProfile` and `WeaponModifier` buffer onto projectile at spawn time. Server-only. Same pattern for `BowActionSystem` when arrow spawning is implemented.

### Phase 8: Prefab Validation (DONE)
Existing weapons default to `DamageElement: Physical`. Default path works unchanged.

### Remaining Work
- Lifesteal modifier: needs healing system
- Knockback modifier: needs knockback event entity
- Chain/Cleave modifiers: needs multi-target hit logic
- BowActionSystem stamping: when arrow spawning is implemented

---

## Example Weapon Configurations

| Weapon | Base Element | Modifiers |
|--------|-------------|-----------|
| Iron Sword | Physical | (none) |
| Fire Sword | Physical | Burn: 80% chance, 5 dps, 3s |
| Explosive Mace | Physical | Explosion: 30 dmg, 3m radius, 15 knockback |
| Lifesteal Katana | Shadow | Lifesteal: 100%, 10% heal |
| Poison Bow | Physical | Poison: 60% chance, 3 dps, 6s |
| Frost Rifle | Physical | Freeze: 40% chance, 2s, 0.5 slow + BonusDamage: 15 Ice |
| Incendiary Grenade | Physical | Burn: 100%, 8 dps, 4s + Explosion: 50 dmg, 4m |

---

## File Change Summary

| File | Action | Phase |
|------|--------|-------|
| `Assets/Scripts/Weapons/Components/DamageProfile.cs` | CREATE | 1 |
| `Assets/Scripts/Weapons/Components/WeaponModifier.cs` | CREATE | 1 |
| `Assets/Scripts/Combat/Utility/DamageTypeConverter.cs` | CREATE | 1 |
| `Assets/Scripts/Player/Components/StatusEffect.cs` | MODIFY | 2 |
| `Assets/Scripts/Player/Components/StatusEffectConfig.cs` | MODIFY | 2 |
| `Assets/Scripts/Player/Systems/StatusEffectSystem.cs` | MODIFY | 2 |
| `Assets/Scripts/Weapons/Authoring/WeaponAuthoring.cs` | MODIFY | 3 |
| `Assets/Scripts/Weapons/Authoring/WeaponBaker.cs` | MODIFY | 3 |
| `Assets/Scripts/Weapons/Systems/SweptMeleeHitboxSystem.cs` | MODIFY | 4 |
| `Assets/Scripts/Weapons/Systems/WeaponFireSystem.cs` | MODIFY | 4 |
| `Assets/Scripts/Weapons/Systems/ProjectileSystem.cs` | MODIFY | 4 |
| `Assets/Scripts/Weapons/Systems/ProjectileBehaviorSystem.cs` | MODIFY | 4 |
| `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs` | MODIFY | 4 |
| `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs` | MODIFY | 5 |
| `Assets/Scripts/Combat/Components/ModifierExplosionRequest.cs` | CREATE | 6 |
| `Assets/Scripts/Combat/Systems/ModifierExplosionSystem.cs` | CREATE | 6 |
| `Assets/Scripts/Weapons/Systems/ThrowableActionSystem.cs` | MODIFY | 7 |
| `Assets/Prefabs/Items/Converted/KnifeWeapon_ECS.prefab` | MODIFY | 8 |

**Total: 5 new files, 13 modified files**
