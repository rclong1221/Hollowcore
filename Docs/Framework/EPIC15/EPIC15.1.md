# EPIC 15.1 - Ability System Foundation

**Status:** Planned
**Dependencies:** EPIC 14.7 (Targeting System)
**Goal:** Create a data-driven ability system supporting cooldowns, targeting, and effects.

---

## Overview

The Ability System provides a framework for skills, spells, and special abilities beyond basic weapon attacks. Abilities use the targeting system from EPIC 14.7 and integrate with the combat, animation, and UI systems.

---

## Core Components

### AbilityDefinition (ScriptableObject)

| Field | Type | Description |
|-------|------|-------------|
| AbilityID | int | Unique identifier |
| DisplayName | string | UI display name |
| Icon | Sprite | UI icon |
| Cooldown | float | Seconds between uses |
| ManaCost | float | Resource cost |
| CastTime | float | Time before effect triggers (0 = instant) |
| TargetingMode | TargetingMode | From EPIC 14.7 |
| Range | float | Max ability range |
| AreaRadius | float | AoE radius (0 = single target) |
| Effects | AbilityEffect[] | What happens when triggered |

### AbilityEffect (Polymorphic)

Base class for ability effects:
- `DamageEffect` - Deal damage
- `HealEffect` - Restore health
- `BuffEffect` - Apply status effect
- `SpawnEffect` - Spawn projectile/entity
- `TeleportEffect` - Move character

---

## ECS Components

| Component | Type | Purpose |
|-----------|------|---------|
| `AbilitySlot` | IBufferElementData | Equipped abilities per character |
| `AbilityCooldown` | IComponentData | Current cooldown timers |
| `AbilityRequest` | IComponentData | Input → ability activation |
| `AbilityCasting` | IComponentData | Currently casting ability |

---

## Systems

| System | Purpose |
|--------|---------|
| `AbilityInputSystem` | Map input → AbilityRequest |
| `AbilityCooldownSystem` | Tick cooldown timers |
| `AbilityCastSystem` | Process cast times, validate targeting |
| `AbilityExecuteSystem` | Trigger effects on cast complete |

---

## Targeting Integration (from 14.7)

Abilities read `TargetData` from player entity:

```
AbilityCastSystem:
  1. Check AbilityRequest.SlotIndex
  2. Get AbilityDefinition from AbilitySlot
  3. Read TargetData from player entity
  4. Validate target (range, LOS, valid target type)
  5. If valid → start cast
```

**Targeting modes per ability:**
- `CameraRaycast` — Aim spells (fireballs)
- `CursorAim` — Ground-target spells (meteor)
- `AutoTarget` — Auto-lock skills (dash attack)
- `ClickSelect` — Target-first abilities (heals)

---

## Tasks

### Phase 1: Data Structures
- [ ] Create `AbilityDefinition` ScriptableObject
- [ ] Create `AbilityEffect` base class + implementations
- [ ] Create ECS components (AbilitySlot, AbilityCooldown, etc.)
- [ ] Create `AbilityAuthoring` + Baker

### Phase 2: Core Systems
- [ ] Create `AbilityInputSystem`
- [ ] Create `AbilityCooldownSystem`
- [ ] Create `AbilityCastSystem` (integrate TargetData)
- [ ] Create `AbilityExecuteSystem`

### Phase 3: Effects
- [ ] Implement `DamageEffect`
- [ ] Implement `HealEffect`
- [ ] Implement `BuffEffect`
- [ ] Implement `SpawnEffect`

### Phase 4: UI
- [ ] Ability bar UI
- [ ] Cooldown visualization
- [ ] Cast bar
- [ ] Target info panel (health, name) — from EPIC 14.7 future work

---

## Verification Checklist

- [ ] Ability with cooldown works correctly
- [ ] Ground-target ability uses CursorAim targeting
- [ ] Auto-target ability uses nearest enemy
- [ ] Cast time prevents instant activation
- [ ] Effects apply to correct targets
