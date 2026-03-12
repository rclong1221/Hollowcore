# EPIC14.7 - Targeting System Abstraction

**Status:** Planned
**Dependencies:** EPIC14.5 (Universal architecture)
**Goal:** Abstract targeting to support camera-based (DIG), cursor-aim (ARPG), and lock-on styles.

---

## Overview

DIG uses camera-raycast targeting ("shoot where you aim at crosshair"). An ARPG roguelite like Ravenswatch uses cursor-aim ("fire toward mouse position") or auto-target nearest enemy. This EPIC abstracts the targeting decision so both games share the same combat infrastructure.

---

## Architecture

### Hybrid Pattern
- **MonoBehaviour** (`ITargetingSystem`) handles targeting logic (raycasts, mode switching).
- **ECS Component** (`TargetData`) stores results (target entity, aim direction, target point).
- **ECS Systems** (`WeaponUseSystem`, `ProjectileSpawnSystem`) read `TargetData` only.

### Modularity
```
ITargetingSystem (interface)
├── CameraRaycastTargeting
├── CursorAimTargeting
├── AutoTargetTargeting
├── LockOnTargeting
└── [Future modes...]
```

**Adding new mode:**
1. Create MonoBehaviour implementing `ITargetingSystem`.
2. Add enum value to `TargetingMode`.
3. No changes to ECS systems.

### EPIC 19 Compatibility
`ITargetingSystem` is one of the pluggable modules in EPIC 19.6's `GameProfile` system.

---

## Relationship to Other EPICs

| EPIC | Connection |
|------|------------|
| **14.5** | Targeting config becomes a ScriptableObject |
| **14.8** | Combat resolver uses targeting output |
| **14.9** | Isometric camera needs input transformation for targeting |
| **19.5** | Click-to-move may use targeting for attack destinations |

---

## Target Styles Needed

| Style | Game | Description |
|-------|------|-------------|
| **CameraRaycast** | DIG (TPS) | Fire toward screen center / crosshair |
| **CursorAim** | ARPG (Isometric) | Fire toward mouse cursor position in world |
| **AutoTarget** | ARPG (optional) | Auto-lock to nearest enemy in range |
| **LockOnTarget** | Souls-like | Tab/trigger to lock to specific enemy |
| **ClickSelectTarget** | Diablo-style | Click enemy, then use ability |

---

## ITargetingSystem Interface

| Method | Description |
|--------|-------------|
| `GetPrimaryTarget()` | Returns currently targeted entity (or null) |
| `GetAimDirection()` | Returns aim direction vector (for projectiles) |
| `GetTargetPoint()` | Returns world position for ground-target abilities |
| `GetTargetsInArea(center, radius)` | Returns entities in AoE |
| `SetTarget(entity)` | Manually set target (for click-select) |
| `ClearTarget()` | Remove current target |
| `CycleTarget(direction)` | Tab-target to next/previous enemy |
| `HasValidTarget()` | Check if current target is still valid |
| `UpdateTargeting()` | Called each frame to refresh state |

---

## TargetingConfig (ScriptableObject)

| Field | Type | Description |
|-------|------|-------------|
| TargetingMode | enum | CameraRaycast, CursorAim, AutoTarget, LockOn, ClickSelect |
| MaxTargetRange | float | How far to detect targets |
| RequireLineOfSight | bool | Can target through walls? |
| AutoTargetOnUse | bool | Auto-acquire target when attacking |
| StickyTargeting | bool | Keep target after use action ends |
| ValidTargetLayers | LayerMask | What can be targeted |
| TargetPriority | enum | Nearest, LowestHealth, HighestThreat, CursorProximity |
| AimAssistStrength | float | How much to nudge aim toward targets |
| AimAssistRadius | float | Detection radius for aim assist |

---

## TargetingModifiers (Runtime Bonuses)

Skills, items, and buffs can modify targeting stats at runtime via a separate ECS component.

| Field | Type | Description |
|-------|------|-------------|
| RangeModifier | float | Added to MaxTargetRange |
| AimAssistModifier | float | Added to AimAssistStrength |
| IgnoreLineOfSight | bool | Bypass line-of-sight check (e.g., wall-hack skill) |
| PriorityOverride | TargetPriority? | Temporarily change target priority |

**Usage:**
- Stat system writes to `TargetingModifiers` when buffs/items change.
- Targeting system reads: `EffectiveRange = Config.MaxTargetRange + Modifiers.RangeModifier`
- Config remains immutable (design intent preserved).

---

## Implementations

### CameraRaycastTargeting

Current DIG behavior. Raycast from camera through crosshair.

| Property | Value |
|----------|-------|
| Target Selection | Automatic (whatever's in crosshair) |
| Aim Direction | Camera forward |
| Persistence | None (constantly updates) |
| UI Indicator | Crosshair changes on valid target |

### CursorAimTargeting

ARPG behavior. Aim toward mouse cursor in world space.

| Property | Value |
|----------|-------|
| Target Selection | Raycast at cursor, or direction toward cursor |
| Aim Direction | Character → cursor position |
| Persistence | None (constantly updates per cursor) |
| UI Indicator | Optional targeting reticle at cursor |
| World Projection | Raycast cursor position to ground plane |

**Input Transformation:**
- Isometric view: cursor position → ground intersection
- Top-down view: cursor position → XZ plane intersection
- Calculate direction from character to that point

### AutoTargetTargeting

Simplified targeting for fast-paced action. Auto-locks nearest enemy.

| Property | Value |
|----------|-------|
| Target Selection | Nearest valid entity in range |
| Aim Direction | Character → target entity |
| Persistence | Until target dies or out of range |
| UI Indicator | Target highlight on locked enemy |
| Fallback | If no target, use cursor aim direction |

### LockOnTargeting

Souls-like. Manual lock with cycling.

| Property | Value |
|----------|-------|
| Target Selection | Press button to lock nearest |
| Aim Direction | Character → locked target |
| Persistence | Until target dies, out of range, or manual break |
| UI Indicator | Lock-on reticle on target |
| Cycle | Tab or shoulder buttons to switch |

### ClickSelectTargeting

Diablo/MMORPG-style. Click enemy to select.

| Property | Value |
|----------|-------|
| Target Selection | Click on enemy |
| Aim Direction | Character → selected target |
| Persistence | Until click elsewhere or target dies |
| UI Indicator | Selection circle under target |
| Ground Target | Click ground for AoE placement |

---

## Integration Points

| System | How It Uses Targeting |
|--------|----------------------|
| `WeaponUseSystem` | Gets aim direction before firing |
| `ProjectileSystem` | Uses target for homing projectiles |
| `MeleeHitSystem` | Uses target for aim assist nudge |
| `AbilitySystem` | Uses target or target point |
| `UI/HUD` | Shows current target info |
| `CameraSystem` | Lock-on mode affects camera orbit |

---

## Tasks

### Phase 1: Interface & Data ✅
- [x] Create `ITargetingSystem` interface
  - **Note**: MonoBehaviour-based (hybrid). Not ECS. Allows polymorphism for mode switching.
- [x] Create `TargetingConfig` ScriptableObject
  - **Note**: Data-driven config. Read at entity spawn → baked into ECS component.
- [x] Create `TargetData` IComponentData (entity, point, direction, validity)
  - **Note**: blittable struct, Burst-compatible.
- [x] Create `TargetingMode` enum
  - **Note**: byte-backed enum for serialization efficiency.
- [x] Create `TargetingModifiers` IComponentData (runtime buffs)
  - **Note**: Skills/items modify range, aim assist, LOS bypass at runtime.
- [x] Create `TargetDataAuthoring` + Baker
  - **Note**: Bakes both `TargetData` and `TargetingModifiers` onto entity.

### Phase 2: Implementations ✅
- [x] Create `CameraRaycastTargeting` (extract from current code)
  - **Note**: Hybrid MonoBehaviour. Raycast uses Physics.Raycast (main thread). Consider `RaycastCommand` + Jobs for batched queries if many entities.
- [x] Create `CursorAimTargeting` (new for ARPG)
  - **Note**: Screen-to-world raycast (main thread). Direction calculation is math-only, Burst-compatible.
- [x] Create `AutoTargetTargeting` (new for ARPG)
  - **Note**: Requires spatial query (find nearest enemy). Use `OverlapSphereCommand` + `IJobParallelFor` for many entities. For single-player, simple `Physics.OverlapSphere` is fine.
- [x] Create `LockOnTargeting`
  - **Note**: Tab-cycling logic. State machine. Main thread.
- [x] Create `ClickSelectTargeting`
  - **Note**: Input event → raycast. Main thread.

> [!IMPORTANT]
> All implementations must read `TargetingModifiers` and apply to effective values:
> `EffectiveRange = Config.MaxTargetRange + Modifiers.RangeModifier`

### Phase 3: Integration ✅
- [x] Refactor `WeaponFireSystem` to use targeting
  - **Note**: Reads `TargetData.AimDirection` from owner entity with fallback chain.
- [x] ProjectileSystem uses spawn direction (no change needed)
- [x] MeleeActionSystem uses hitbox overlap (aim assist = future)

> [!NOTE]
> Ability system targeting integration → See [EPIC 15.1](../EPIC15/EPIC15.1.md)

### Phase 4: UI & Feedback ✅
- [x] Add `TargetHighlightUI` — follows target on screen
- [x] Add `LockOnReticleUI` — Souls-like rotating reticle
- [x] Add `CursorAimIndicator` — ground indicator for ARPG
- [ ] Add target info panel → See [EPIC 15.1 Phase 4](../EPIC15/EPIC15.1.md)

### Phase 5: Configuration ✅
- [x] Add static presets: `CreateDIGPreset()`, `CreateARPGPreset()`
- [x] Documented in SETUP_GUIDE_14.7.md

### Phase 6: Visual Indicator Variants ✅
- [x] `ITargetIndicator` — Abstract interface for swappable visuals
- [x] `VFXTargetHighlight` — VFX Graph-based target indicator
- [x] `VFXCursorIndicator` — VFX Graph ground circle with particles
- [x] `DecalCursorIndicator` — URP Decal Projector ground circle

### Phase 7: Conditional UI Theming
Indicators should adapt based on many conditions:

| Trigger Category | Examples |
|------------------|----------|
| **Target-Based** | Faction (enemy/ally), Type (normal/elite/boss), Race, Health % |
| **Combat-Based** | Damage type (fire/ice), Hit type (crit/miss), Threat level |
| **Player-Based** | Class, Race, Equipped weapon category |
| **Game State** | PvP mode, Boss fight, Stealth, Friendly fire enabled |
| **Accessibility** | High contrast, Colorblind mode, Size scaling |

#### Hybrid Architecture
- **Editor:** `ThemeProfile` ScriptableObject for designer-friendly config
- **Runtime:** Bake to `IndicatorThemeData` (ECS IComponentData) for Burst access
- **Immutable:** Never mutate SOs at runtime, always copy

> [!IMPORTANT]
> ScriptableObjects hold prefab references via Addressables paths to reduce memory.

**Tasks:**
- [x] Create `IndicatorThemeContext` struct with all trigger data
- [x] Create `ThemeProfile` ScriptableObject (editor config)
- [x] Create `IndicatorThemeData` IComponentData (runtime baked)
- [x] Create `ThemeProfileAuthoring` + Baker
- [x] Create `DefaultThemeResolver` (evaluates rules at runtime)
- [ ] Integrate with EPIC 14.8 combat results (damage type, hit type)

### Phase 8: Third-Party UI Adapter ✅
Decouple targeting logic from UI so any asset can be integrated.

**Architecture:**
```
ITargetingSystem → TargetData (ECS) ← IThemedTargetIndicator ← Your Adapter → 3rd Party Asset
```

**Tasks:**
- [x] Create `IThemedTargetIndicator` (extends ITargetIndicator with context)
- [x] Create `TargetIndicatorBridge` (reads TargetData, builds context, forwards to indicators)
- [x] Example: `ThirdPartyUIAdapter` showing how to wrap a 3rd-party UI

---

## Verification Checklist

### CameraRaycast (DIG)
- [ ] Shooting at crosshair works as before
- [ ] Targets detected through raycast
- [ ] Crosshair feedback on valid target

### CursorAim (ARPG)
- [ ] Projectiles fire toward cursor
- [ ] Ground position correctly calculated
- [ ] Works with isometric camera (14.9)

### AutoTarget (ARPG)
- [ ] Nearest enemy auto-locked
- [ ] Attack aims at locked enemy
- [ ] Switches when target dies

### LockOn
- [ ] Button press locks to enemy
- [ ] Cycling works
- [ ] Camera adjusts to face target

---

## Success Criteria

- [ ] Targeting mode swappable via config only
- [ ] All five modes functional
- [ ] DIG combat unchanged using CameraRaycast
- [ ] ARPG uses CursorAim or AutoTarget effectively
- [ ] Lock-on feels responsive like Souls games
- [ ] No code changes to add new targeting mode
