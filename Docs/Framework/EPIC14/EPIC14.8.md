# EPIC14.8 - Combat Resolver Abstraction

**Status:** Complete (All Phases)
**Dependencies:** EPIC14.7 (Targeting system)
**Goal:** Abstract combat resolution to support physics-based (DIG) and stat-based (ARPG) damage calculation.

---

## Overview

This EPIC makes combat resolution pluggable so both games share the same weapon infrastructure.

### Theming Integration
Combat results (HitType, DamageType) are fed back into the **EPIC 14.7 Targeting System** via `IndicatorThemeContext` to drive dynamic UI feedback (e.g., crit flashes, elemental colors).

---

## Relationship to Other EPICs

| EPIC | Connection |
|------|------------|
| **14.5** | Resolver config becomes part of `WeaponCategoryDefinition` |
| **14.7** | Uses targeting output for hit detection. `CombatResult` feeds into 14.7 `IndicatorThemeContext`. |
| **14.4** | New weapons can specify which resolver to use |
| **19.2** | Turn-based time affects when resolution happens |

---

## Combat Styles Needed

| Style | Game | Description |
|-------|------|-------------|
| **PhysicsHitbox** | DIG | Raycast/collider hit → apply damage |
| **StatBasedDirect** | ARPG (simple) | Hit always lands, damage = formula |
| **StatBasedRoll** | ARPG (complex) | Roll accuracy, then damage formula |
| **HybridResolver** | Both | Require physics hit, then stat damage calc |

---

## ICombatResolver Interface

| Method | Description |
|--------|-------------|
| `ResolveAttack(context)` | Main entry point, returns `CombatResult` |
| `CalculateHitChance(attacker, target, weapon)` | Returns 0-1 hit probability |
| `RollForHit(hitChance)` | Perform hit roll, returns `HitType` |
| `CalculateDamage(context, hitType)` | Returns final damage value |
| `ApplyDamage(target, damage, damageType)` | Apply to health component |
| `TriggerEffects(target, weapon, hitType)` | Proc on-hit effects |

---

## Data Structures

### CombatContext

| Field | Type | Description |
|-------|------|-------------|
| AttackerEntity | Entity | Who is attacking |
| TargetEntity | Entity | Who is being attacked (may be null) |
| WeaponEntity | Entity | Weapon used |
| HitPoint | float3 | World position of hit |
| HitNormal | float3 | Surface normal at hit |
| HitDistance | float | Distance from attacker |
| WasPhysicsHit | bool | Did physics/raycast confirm contact |
| AttackerStats | StatBlock | Attacker's combat stats |
| TargetStats | StatBlock | Target's combat stats |
| WeaponData | WeaponStats | Weapon stats |

### CombatResult

| Field | Type | Description |
|-------|------|-------------|
| DidHit | bool | Did attack connect |
| HitType | enum | Miss, Graze, Hit, Critical |
| RawDamage | float | Pre-mitigation damage |
| FinalDamage | float | Post-mitigation damage |
| DamageType | enum | Physical, Fire, Ice, Lightning, etc. |
| TargetKilled | bool | Did target die |
| ProcsTriggered | List | On-hit effects that fired |
| CritMultiplier | float | Applied crit multiplier (1.0 if no crit) |

### HitType Enum

| Value | Description |
|-------|-------------|
| Miss | Attack didn't connect |
| Graze | Partial hit (50% damage) |
| Hit | Normal hit (100% damage) |
| Critical | Critical hit (bonus damage) |

---

## Implementations

### PhysicsHitboxResolver

Current DIG behavior. Physics collision determines hit.

| Step | Description |
|------|-------------|
| 1 | Raycast or trigger collider detects contact |
| 2 | If no physics hit, return Miss |
| 3 | Hit confirmed, no accuracy roll |
| 4 | Damage = weapon base damage (no stat scaling) |
| 5 | Apply directly to target health |

**When to use:** Pure action games where skill (aiming) determines hit.

### StatBasedDirectResolver

ARPG combat. Attacks always hit, damage from stats.

| Step | Description |
|------|-------------|
| 1 | Check target is in range (from targeting system) |
| 2 | If in range, hit is guaranteed |
| 3 | Calculate damage: `BaseDamage * (1 + AttackPower/100)` |
| 4 | Crit roll: `if (Random < CritChance) damage *= CritMultiplier` |
| 5 | Apply mitigation: `damage = damage * (100 / (100 + TargetDefense))` |
| 6 | Apply to target health |

**When to use:** Fast-paced ARPGs where you want "game feel" over aiming skill.

### StatBasedRollResolver

Full stat-based with accuracy rolls. More tactical feel.

| Step | Description |
|------|-------------|
| 1 | Calculate hit chance: `Accuracy - TargetEvasion` |
| 2 | Roll for hit: `Random < hitChance` |
| 3 | If miss, return (show "MISS" damage number) |
| 4 | Crit roll: `Random < CritChance` |
| 5 | Damage formula with full stat interaction |
| 6 | Apply mitigation |
| 7 | Apply to target |

**When to use:** Tactical ARPGs, turn-based games, when "dice rolling" matters.

### HybridResolver

Combine physics skill with stat damage. Best of both worlds.

| Step | Description |
|------|-------------|
| 1 | Require physics hit (projectile collision, raycast, etc.) |
| 2 | If no physics hit, no damage (skill-based aiming) |
| 3 | On physics hit, run stat-based damage calculation |
| 4 | Crit, mitigation, etc. from StatBasedDirect |

**When to use:** Games where you want skillful aiming but RPG damage depth.

---

## Damage Formula System

For stat-based resolvers, damage formulas are configurable.

### DamageFormula (ScriptableObject)

| Field | Type | Description |
|-------|------|-------------|
| FormulaName | string | Human-readable name |
| BaseDamageExpression | string | `"WeaponDamage * (1 + Strength * 0.02)"` |
| CritChanceExpression | string | `"0.05 + CritRating * 0.001"` |
| CritMultiplierExpression | string | `"1.5 + CritDamage * 0.01"` |
| MitigationExpression | string | `"Damage * (100 / (100 + Defense))"` |
| ElementalModifiers | List | Fire vs Ice, etc. |
| MinDamage | float | Floor for damage (never below this) |
| MaxDamage | float | Cap for damage (never above this) |

### Expression Variables Available

**Attacker Stats:**
- `Strength`, `Dexterity`, `Intelligence`
- `AttackPower`, `SpellPower`
- `CritRating`, `CritDamage`
- `Level`

**Weapon Stats:**
- `WeaponDamage`, `WeaponDamageMin`, `WeaponDamageMax`
- `AttackSpeed`
- `ElementType`

**Target Stats:**
- `Defense`, `Armor`, `Resistance`
- `Evasion`
- `TargetLevel`

**Special:**
- `Random` - 0.0 to 1.0
- `Distance` - Distance to target
- `HealthPercent` - Target current health %

---

## Stat Components

To support stat-based combat, entities need stat components.

### AttackStats (IComponentData)

| Field | Type | Description |
|-------|------|-------------|
| AttackPower | float | Flat damage bonus |
| CritChance | float | 0-1 crit probability |
| CritMultiplier | float | Crit damage multiplier |
| Accuracy | float | Hit chance bonus |

### DefenseStats (IComponentData)

| Field | Type | Description |
|-------|------|-------------|
| Defense | float | Damage reduction stat |
| Evasion | float | Dodge chance |
| Resistances | FixedList | Per-element resistance |

---

## WeaponCategoryDefinition Additions

| Field | Type | Description |
|-------|------|-------------|
| DefaultResolver | ICombatResolver | Category default |
| CanCrit | bool | Whether crits possible |
| DamageFormula | DamageFormula | Override formula (optional) |
| BaseDamageRange | float2 | Min-max base damage |

---

## Integration Points

| System | How It Uses Resolver |
|--------|---------------------|
| `WeaponUseSystem` | Calls resolver when attack lands |
| `ProjectileHitSystem` | Passes physics hit to resolver |
| `MeleeHitSystem` | Passes collider trigger to resolver |
| `DamageNumbersUI` | Displays result from resolver |
| `CombatLogSystem` | Records combat events |

---

## Tasks

### Phase 1: Interface & Data
- [x] Create `ICombatResolver` interface
- [x] Create `CombatContext` struct
- [x] Create `CombatResult` struct
- [x] Create `HitType` enum (reused from EPIC 14.7 Theming)
- [x] Create `DamageFormula` ScriptableObject

### Phase 2: Stat Components
- [x] Create `AttackStats` component
- [x] Create `DefenseStats` component
- [x] Add stats to player and enemy prefabs (via authoring components)
- [x] Create stat initialization system (`CombatStatProfile` ScriptableObject)

### Phase 3: Implementations
- [x] Create `PhysicsHitboxResolver` (physics collision = hit, no stats)
- [x] Create `StatBasedDirectResolver` (always hit, stat scaling, crits)
- [x] Create `StatBasedRollResolver` (accuracy vs evasion, graze, crits)
- [x] Create `HybridResolver` (physics + stat damage)
- [x] Create `CombatResolverFactory` for resolver access

### Phase 4: Formula System
- [x] Implement expression parser (`FormulaParser.cs`, `FormulaEvaluator.cs`)
- [x] Create default formulas (DIG simple, ARPG complex) - `DefaultFormulas.cs`
- [x] Add formula validation in editor (`DamageFormulaEditor.cs`)
- [x] Create formula testing tool (`FormulaTestingWindow.cs`)

### Phase 5: Integration
- [x] Add resolver field to `WeaponCategoryDefinition` (ResolverType, DamageFormula, CanCrit, BaseDamageRange)
- [x] Create `CombatResolutionSystem` (processes PendingCombatHit → CombatResultEvent)
- [x] Create `DamageApplicationSystem` (applies damage, handles death)
- [x] Create `CombatHitFactory` for projectile/melee integration
- [x] Add `HealthAuthoring` component for damage targets

### Phase 6: UI & Feedback
- [x] Create UI provider interfaces (`IDamageNumberProvider`, `ICombatFeedbackProvider`, `ICombatLogProvider`)
- [x] Create `CombatUIRegistry` for provider registration
- [x] Create `CombatUIBridgeSystem` (ECS → UI bridge)
- [x] Create `DamageNumberAdapterBase` for Asset Store integration
- [x] Create `SimpleCombatFeedback` (built-in hit stop, camera shake, damage flash)
- [x] **Theming Integration (with 14.7)**: HitType and DamageType passed to UI providers for styling

---

## Verification Checklist

### PhysicsHitbox (DIG)
- [ ] Raycast damage works as before
- [ ] Collider triggers apply damage
- [ ] No stat interaction (pure skill)

### StatBasedDirect (ARPG)
- [ ] In-range attacks always hit
- [ ] Damage scales with AttackPower
- [ ] Crits apply multiplier
- [ ] Defense reduces damage

### StatBasedRoll
- [ ] Accuracy vs Evasion produces misses
- [ ] "MISS" damage number appears
- [ ] Stats affect hit chance

### Hybrid
- [ ] Requires physics hit to trigger
- [ ] Then applies stat-based damage
- [ ] Best for skill+stats games

### Damage Numbers
- [ ] Numbers appear at hit location
- [ ] Crit numbers are bigger/different color
- [ ] Miss text displays

---

## Success Criteria

- [ ] Combat resolver swappable via config
- [ ] All four resolvers functional
- [ ] DIG physics combat unchanged
- [ ] ARPG stat combat feels "numbery"
- [ ] Formulas evaluate correctly
- [ ] Damage numbers provide feedback
- [ ] No code changes to add new formula
