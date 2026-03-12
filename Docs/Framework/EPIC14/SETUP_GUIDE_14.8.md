# EPIC 14.8 - Combat Resolver Setup Guide

## Overview

The Combat Resolver system abstracts damage calculation to support both physics-based (DIG action combat) and stat-based (ARPG) damage resolution. This allows weapons to use different combat styles via configuration.

---

## Phase Status

| Phase | Content | Status |
|-------|---------|--------|
| Phase 1 | Interface & Data | ✅ Complete |
| Phase 2 | Stat Components | ✅ Complete |
| Phase 3 | Resolver Implementations | ✅ Complete |
| Phase 4 | Expression Parser | ✅ Complete |
| Phase 5 | Weapon System Integration | ✅ Complete |
| Phase 6 | UI & Feedback | ✅ Complete |

---

## Files Created

### Phase 1: Interface & Data
| File | Purpose |
|------|---------|
| `Assets/Scripts/Combat/Resolvers/ICombatResolver.cs` | Interface for combat resolution implementations |
| `Assets/Scripts/Combat/Resolvers/CombatContext.cs` | Input data struct (attacker, target, weapon, stats) |
| `Assets/Scripts/Combat/Resolvers/CombatResult.cs` | Output data struct (damage, hit type, procs) |
| `Assets/Scripts/Combat/Resolvers/CombatResolverType.cs` | Enum for resolver types |
| `Assets/Scripts/Combat/Definitions/DamageFormula.cs` | ScriptableObject for configurable damage formulas |

### Phase 2: Stat Components
| File | Purpose |
|------|---------|
| `Assets/Scripts/Combat/Components/CombatStatComponents.cs` | ECS components (AttackStats, DefenseStats, ElementalResistances, CharacterAttributes) |
| `Assets/Scripts/Combat/Authoring/CombatStatsAuthoring.cs` | Authoring components for prefab setup |
| `Assets/Scripts/Combat/Definitions/CombatStatProfile.cs` | ScriptableObject for class/enemy stat templates |

### Phase 3: Resolver Implementations
| File | Purpose |
|------|---------|
| `Assets/Scripts/Combat/Resolvers/Implementations/PhysicsHitboxResolver.cs` | Physics collision = hit, no stat scaling |
| `Assets/Scripts/Combat/Resolvers/Implementations/StatBasedDirectResolver.cs` | Always hit in range, stat damage, crits |
| `Assets/Scripts/Combat/Resolvers/Implementations/StatBasedRollResolver.cs` | Accuracy vs evasion rolls, graze, crits |
| `Assets/Scripts/Combat/Resolvers/Implementations/HybridResolver.cs` | Physics hit + stat damage + distance falloff |
| `Assets/Scripts/Combat/Resolvers/CombatResolverFactory.cs` | Factory for resolver access |

### Phase 4: Formula System
| File | Purpose |
|------|---------|
| `Assets/Scripts/Combat/Formulas/FormulaParser.cs` | Math expression parser with variable substitution |
| `Assets/Scripts/Combat/Formulas/FormulaEvaluator.cs` | Bridges CombatContext to formula expressions |
| `Assets/Scripts/Combat/Formulas/DefaultFormulas.cs` | Static factory for preset formula configurations |
| `Assets/Scripts/Combat/Editor/DamageFormulaEditor.cs` | Custom inspector with validation and inline testing |
| `Assets/Scripts/Combat/Editor/FormulaTestingWindow.cs` | Editor window for interactive formula testing |

### Phase 5: Weapon System Integration
| File | Purpose |
|------|---------|
| `Assets/Scripts/Items/Definitions/WeaponCategoryDefinition.cs` | Added ResolverType, DamageFormula, CanCrit, BaseDamageRange fields |
| `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs` | Processes PendingCombatHit → CombatResultEvent |
| `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs` | Applies damage to targets, handles death events |
| `Assets/Scripts/Combat/Systems/CombatEventCleanupSystem.cs` | Cleans up unconsumed combat events |
| `Assets/Scripts/Combat/Systems/CombatHitFactory.cs` | Static factory for creating pending hits from weapon code |
| `Assets/Scripts/Combat/Authoring/HealthAuthoring.cs` | Authoring component for damageable entities |

### Phase 6: UI & Feedback (Abstracted)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Combat/UI/ICombatUIProviders.cs` | Interfaces: IDamageNumberProvider, ICombatFeedbackProvider, ICombatLogProvider |
| `Assets/Scripts/Combat/UI/CombatUIRegistry.cs` | Static registry for UI provider registration |
| `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs` | ECS system that forwards events to registered UI providers |
| `Assets/Scripts/Combat/UI/Adapters/DamageNumberAdapterBase.cs` | Base class for Asset Store damage number integration |
| `Assets/Scripts/Combat/UI/Adapters/DamageNumbersProAdapter.cs` | Example adapter for "Damage Numbers Pro" asset |
| `Assets/Scripts/Combat/UI/SimpleCombatFeedback.cs` | Built-in feedback: hit stop, camera shake, damage flash |

---

## Quick Setup

### 1. Create a Damage Formula

1. **Right-click** in Project → `Create > DIG > Combat > Damage Formula`
2. Save to `Assets/Data/Combat/` folder (create if needed)
3. Configure formula settings:

| Field | Description |
|-------|-------------|
| `Formula Name` | Display name for identification |
| `Base Damage Expression` | Formula string (e.g., `WeaponDamage * (1 + Strength * 0.02)`) |
| `Crit Chance Expression` | Crit probability formula |
| `Crit Multiplier Expression` | Crit damage bonus formula |
| `Mitigation Expression` | Defense reduction formula |
| `Min/Max Damage` | Damage floor and cap |

### 2. Create Stat Profiles (Optional)

1. **Right-click** in Project → `Create > DIG > Combat > Stat Profile`
2. Save to `Assets/Data/Combat/Profiles/` folder
3. Configure base stats and per-level scaling

| Field | Description |
|-------|-------------|
| `Profile ID` | Unique identifier (e.g., "Warrior", "Goblin") |
| `Base Attack Power` | Starting attack power |
| `Base Defense` | Starting defense |
| `Attack Power Per Level` | Scaling per level |
| `Base Resistances` | Elemental resistance values |

### 3. Add Combat Stats to Player/Enemy Prefabs

For **stat-based combat** (ARPG), add these authoring components:

1. Open your **Player Ghost Prefab** (e.g., `Atlas_Server.prefab`)
2. Add Component → search for `AttackStatsAuthoring`
3. Configure offensive stats (AttackPower, CritChance, etc.)
4. Add Component → `DefenseStatsAuthoring`
5. Configure defensive stats (Defense, Evasion)
6. *(Optional)* Add `ElementalResistancesAuthoring` for elemental mitigation
7. *(Optional)* Add `CharacterAttributesAuthoring` for RPG attributes

> [!NOTE]
> For **DIG physics combat**, stat components are optional — the PhysicsHitboxResolver ignores stats.

### 4. Recommended Damage Formula Presets

Create these `DamageFormula` assets for common use cases:

**DIG_Simple.asset** (Physics Action):
- `BaseDamageExpression`: `WeaponDamage`
- `CritChanceExpression`: `0`
- No mitigation (skill-based)

**ARPG_Standard.asset** (Stat-Based):
- `BaseDamageExpression`: `WeaponDamage * (1 + AttackPower / 100)`
- `CritChanceExpression`: `CritChance`
- `MitigationExpression`: `Damage * (100 / (100 + Defense))`

**ARPG_Tactical.asset** (With Accuracy Rolls):
- `HitChanceExpression`: `0.9 + Accuracy * 0.01 - TargetEvasion * 0.01`
- `EnableGraze`: ✓
- `GrazeDamageMultiplier`: `0.5`

---

## Combat Resolver Types

| Type | Description | Best For |
|------|-------------|----------|
| **PhysicsHitbox** | Raycast/collider hit required, no stat scaling | DIG (action combat) |
| **StatBasedDirect** | In-range = always hit, damage from stats | Fast ARPGs |
| **StatBasedRoll** | Accuracy vs Evasion roll, tactical feel | Tactical ARPGs |
| **Hybrid** | Requires physics hit, then stat damage | Skill + RPG depth |

### Resolver Behavior Comparison

| Feature | PhysicsHitbox | StatBasedDirect | StatBasedRoll | Hybrid |
|---------|---------------|-----------------|---------------|--------|
| Requires physics hit | ✓ | ✗ | ✗ | ✓ |
| Accuracy rolls | ✗ | ✗ | ✓ | ✗ |
| Critical hits | ✗ | ✓ | ✓ | ✓ |
| Graze hits | ✗ | ✗ | ✓ | ✗ |
| Stat scaling | ✗ | ✓ | ✓ | ✓ |
| Defense mitigation | ✗ | ✓ | ✓ | ✓ |
| Elemental resistance | ✗ | ✗ | ✓ | ✓ |
| Distance falloff | ✗ | ✗ | ✗ | ✓ |

### Using Resolvers (Available Now)

Get resolvers via the factory:

| Method | Returns |
|--------|---------|
| `CombatResolverFactory.GetDIGDefault()` | PhysicsHitboxResolver |
| `CombatResolverFactory.GetARPGDefault()` | StatBasedDirectResolver |
| `CombatResolverFactory.GetTacticalDefault()` | StatBasedRollResolver |
| `CombatResolverFactory.GetHybridDefault()` | HybridResolver |
| `CombatResolverFactory.GetResolver(CombatResolverType)` | Resolver by enum |
| `CombatResolverFactory.GetResolver("ResolverID")` | Resolver by string ID |

---

## Stat Component Reference

### AttackStatsAuthoring

| Field | Type | Description |
|-------|------|-------------|
| Attack Power | float | Flat damage bonus |
| Spell Power | float | Magic damage bonus |
| Crit Chance | float (0-1) | Critical hit probability |
| Crit Multiplier | float | Crit damage multiplier (1.5 = 150%) |
| Accuracy | float | Hit bonus for roll-based combat |

### DefenseStatsAuthoring

| Field | Type | Description |
|-------|------|-------------|
| Defense | float | Primary damage reduction |
| Armor | float | Alternative reduction stat |
| Evasion | float (0-1) | Dodge chance |

### ElementalResistancesAuthoring

All fields are float (0-1): Physical, Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane

### CharacterAttributesAuthoring

| Field | Type | Description |
|-------|------|-------------|
| Strength | int | Physical power attribute |
| Dexterity | int | Agility/precision attribute |
| Intelligence | int | Magic power attribute |
| Vitality | int | Health attribute |
| Level | int | Character level |

---

## Combat System Integration

### WeaponCategoryDefinition Fields

New combat-related fields added to weapon categories:

| Field | Type | Description |
|-------|------|-------------|
| Resolver Type | `CombatResolverType` | How this category resolves combat |
| Damage Formula | `DamageFormula` | Optional formula override |
| Can Crit | bool | Whether this weapon can critically hit |
| Base Damage Range | Vector2 | Min-max base damage |

### Creating Combat Hits

From projectile/melee hit detection, use `CombatHitFactory`:

| Method | Use Case |
|--------|----------|
| `CreatePhysicsHit()` | Projectile collision, raycast hit |
| `CreateTargetedHit()` | ARPG targeted attack (no physics) |
| `CreateFromCategory()` | Auto-extract settings from WeaponCategoryDefinition |

### Combat System Flow

```
Hit Detection → PendingCombatHit → CombatResolutionSystem → CombatResultEvent → DamageApplicationSystem
```

| System | Order | Purpose |
|--------|-------|---------|
| `CombatResolutionSystem` | First | Resolves hits using appropriate resolver |
| `DamageApplicationSystem` | Second | Applies damage to HealthComponent |
| `CombatEventCleanupSystem` | Late | Cleans up unconsumed events |

### Health Setup

Add `HealthAuthoring` to any entity that can take damage:

| Field | Description |
|-------|-------------|
| Max Health | Maximum health points |
| Starting Health | Initial health (defaults to max if 0) |

---

## UI Provider Integration

The combat system is fully decoupled from UI. Use any damage numbers, feedback system, or combat log.

### Key Abstraction Points

| Layer | Purpose |
|-------|---------|
| **Interfaces** | `IDamageNumberProvider`, `ICombatFeedbackProvider`, `ICombatLogProvider` |
| **Registry** | `CombatUIRegistry` - static registry for swappable providers |
| **Bridge** | `CombatUIBridgeSystem` - converts ECS events to interface calls |
| **Adapters** | `DamageNumberAdapterBase` - base class for Asset Store integration |

### Using Built-in Feedback

1. Add `SimpleCombatFeedback` component to a GameObject in your scene
2. Assign camera reference and optional damage flash overlay
3. It auto-registers with `CombatUIRegistry`

### Integrating Asset Store Packages

1. Import the package (e.g., Damage Numbers Pro, Floating Text, etc.)
2. Create a new class extending `DamageNumberAdapterBase`
3. Override `ShowDamageNumber()`, `ShowMiss()`, `ShowHealNumber()`
4. Call your Asset Store package's API inside each method
5. Add your adapter component to a scene GameObject - it auto-registers

**Example flow:**
```
ECS Combat Event → CombatUIBridgeSystem → CombatUIRegistry → Your Adapter → Asset Store Package
```

### Available Interfaces

| Interface | Purpose |
|-----------|---------|
| `IDamageNumberProvider` | Display damage/heal numbers at world positions |
| `ICombatFeedbackProvider` | Hit stop, camera shake, screen flash |
| `ICombatLogProvider` | Combat history/log display |

### Registration

Providers auto-register on `OnEnable()` and unregister on `OnDisable()`:
- `CombatUIRegistry.RegisterDamageNumbers(provider)`
- `CombatUIRegistry.RegisterFeedback(provider)`
- `CombatUIRegistry.RegisterCombatLog(provider)`

---

## Theming Integration (EPIC 14.7)

Combat results feed into the Targeting System's theming:

| CombatResult Field | IndicatorThemeContext Field |
|--------------------|----------------------------|
| `HitType` | `LastHitType` (Miss, Graze, Hit, Critical) |
| `DamageType` | `LastDamageType` (Fire, Ice, etc.) |

This enables dynamic UI feedback:
- Crit hits → flashy indicator
- Fire damage → orange tint
- Miss → faded indicator

---

## Expression Variables (For Formulas)

Available variables in `DamageFormula` expression strings:

**Attacker Stats:**
`Strength`, `Dexterity`, `Intelligence`, `AttackPower`, `SpellPower`, `CritChance`, `CritMultiplier`, `Accuracy`, `Level`

**Weapon Stats:**
`WeaponDamage`, `WeaponDamageMin`, `WeaponDamageMax`, `AttackSpeed`

**Target Stats:**
`Defense`, `Armor`, `Evasion`, `TargetLevel`, `HealthPercent`

**Special:**
`Random` (0.0-1.0), `Distance`, `Damage` (for mitigation pass)

---

## Formula Testing Tools

### DamageFormula Inspector

When you select a `DamageFormula` asset, the custom inspector shows:

| Section | Feature |
|---------|---------|
| **Validation** | Displays any expression parsing errors |
| **Formula Tester** | Inline test with adjustable stat values |

Click **Calculate Damage** to output results to the Console.

### Formula Testing Window

Open via menu: **DIG > Combat > Formula Testing Window**

| Feature | Description |
|---------|-------------|
| Formula selection | Drag in any `DamageFormula` asset |
| Quick Create | Buttons to generate preset formulas |
| Attacker/Weapon/Target stats | Adjustable sliders for all variables |
| Results | Shows base damage, hit/crit chances, expected DPS |

### Default Formula Presets

| Preset | Description |
|--------|-------------|
| `CreateDIGSimple()` | WeaponDamage only, no stats |
| `CreateARPGStandard()` | AttackPower scaling, crits, defense mitigation |
| `CreateARPGTactical()` | Accuracy rolls, graze hits, evasion |
| `CreateHybrid()` | Physics + stat damage with distance falloff |
| `CreateSpellcaster()` | SpellPower + Intelligence scaling |
| `CreateExecute()` | Bonus damage on low health targets |

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `DamageFormula` not in Create menu | Ensure scripts compiled (check Console for errors) |
| `Stat Profile` not in Create menu | Ensure scripts compiled (check Console for errors) |
| `AttackStatsAuthoring` not found | Search for "Attack" in Add Component search |
| Expression parsing fails | Check variable spelling matches exactly (case-sensitive) |
| No damage applied | Phase 3+ required for resolver implementations |
| Stats not syncing in multiplayer | Ensure authoring is on Ghost prefab with `GhostAuthoringComponent` |

