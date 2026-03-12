# EPIC 4.3: Status Effects (Hypoxia, Radiation Sickness, Burns, Frostbite)

**Priority**: MEDIUM  
**Goal**: A generic status-effect framework that Survival hazards and creature attacks can reuse (apply, stack, expire, and present).  
**Dependencies**: Epic 4.1 (death/damage), Epic 2.1/2.7 (oxygen/radiation/temp)

## Design Notes (Match EPIC7 Level of Detail)
- **Status effects are durable state**: unlike `DamageEvent` (ephemeral), `StatusEffect` persists and should be replicated if it affects gameplay.
- **Bounded complexity**: cap the number of effects and define merge/refresh rules to prevent unbounded buffers.
- **Two outputs**:
  - gameplay consequences (server): emit `DamageEvent`, apply movement modifiers, disable actions
  - presentation mapping (client): icons, screen FX, audio
- **Avoid bespoke UI**: hazard systems set effects; the status system owns UI mapping.

## Components

**StatusEffect** (IBufferElementData, on player; replicated)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Type` | StatusEffectType | Yes | Hypoxia / Radiation / Burn / Cold / Bleed / Concussion |
| `Severity` | float | Quantization=100 | 0..1 |
| `Duration` | float | Quantization=100 | Remaining seconds (-1 = infinite) |
| `SourceEntity` | Entity | Yes | Optional attribution |

**StatusEffectConfig** (IComponentData, singleton; server-only)
| Field | Type | Description |
|---|---|---|
| `TickInterval` | float | How often effects apply |
| `MaxEffects` | int | Hard cap per player |

## Systems

**StatusEffectUpdateSystem** (SimulationSystemGroup, ServerWorld)
- Decrements duration
- Applies periodic consequences:
  - emits `DamageEvent` (hypoxia, burns)
  - modifies movement/input (concussion, frostbite) via modifiers (future)
- Removes expired effects

**StatusEffectPresentationSystem** (PresentationSystemGroup, ClientWorld)
- Maps active effects to UI icons, screen FX, breathing audio, etc.

## Acceptance Criteria
- [x] Multiple effects can coexist, bounded by a configurable cap
- [x] Survival hazards can apply effects without bespoke per-hazard UI logic
- [x] Effects replicate cleanly for remote players (or are local-only when appropriate)

## Sub-Epics / Tasks

### Sub-Epic 4.3.1: Stacking / Refresh Rules
**Goal**: Deterministic effect behavior under repeated application.
**Tasks**:
- [x] Define per-effect policy:
  - [x] stack severity (additive) vs replace (max) vs refresh duration only
  - [x] max stack / max severity
- [x] Implement merge behavior:
  - [x] if same `Type` exists → update existing element instead of adding a new one

### Sub-Epic 4.3.2: Server Tick Application
**Goal**: Apply consequences at a controlled rate.
**Tasks**:
- [x] `TickInterval` governs how often effects apply damage/consequences
- [x] Status tick emits `DamageEvent` (so mitigation applies via Epic 4.2)
- [x] Ensure effect application continues while downed (design choice) but stops while respawning

### Sub-Epic 4.3.3: Client Presentation Map
**Goal**: Consistent HUD/FX across all sources.
**Tasks**:
- [ ] Map each `StatusEffectType` to:
  - [ ] icon
  - [ ] severity-driven UI intensity
  - [ ] looping audio category (breathing/coughing)
- [x] Ensure local-only presentation effects don’t require replication for remote players (unless visible in world)

### Sub-Epic 4.3.4: QA Checklist
**Tasks**:
- [x] Re-applying same effect refreshes/merges correctly (no buffer growth)
- [x] Multiple effects present simultaneously (icons + severity behave)
- [x] Status ticks emit damage at the configured interval (not per frame)

---

## Integration & Usage Guide

### 1. Applying Status Effects
To apply a status effect from any system (Hazards, Weapons, Environment), add a `StatusEffectRequest` to the target entity's buffer. The `StatusEffectSystem` will handle creation, stacking,/refreshing logic.

```csharp
public void ApplyRadiation(Entity player, float severity, float duration)
{
    if (SystemAPI.HasBuffer<StatusEffectRequest>(player))
    {
        var buffer = SystemAPI.GetBuffer<StatusEffectRequest>(player);
        buffer.Add(new StatusEffectRequest
        {
            Type = StatusEffectType.RadiationPoisoning,
            Severity = severity, // 0.0 - 1.0
            Duration = duration, // Seconds
            Additive = true      // True = adds to existing severity, False = updates to max severity
        });
    }
}
```

### 2. Configuring Damage
**CRITICAL**: The `StatusEffectSystem` requires the `StatusEffectConfig` singleton to run. You **MUST** add the `StatusEffectAuthoring` component to a GameObject in your scene (e.g. "GameConfig") and bake it.

Status effects deal damage periodically. Configure this via the `StatusEffectAuthoring` script.

| Field | Default | Description |
|---|---|---|
| **TickInterval** | 1.0s | How often damage triggers |
| **HypoxiaDamage** | 5.0 | Damage per tick at Max Severity (1.0) |
| **RadiationDamage** | 2.0 | Damage per tick at Max Severity (1.0) |
| **BurnDamage** | 5.0 | Damage per tick at Max Severity (1.0) |

*Note: Actual damage = (ConfigDamage * CurrentSeverity). E.g., 50% Hypoxia deals 2.5 damage.*

### 3. Adding New Effects
1. Add entry to `StatusEffectType` enum (`Assets/Scripts/Player/Components/StatusEffect.cs`).
2. Update `StatusEffectConfig` and `StatusEffectAuthoring` to include new damage fields (if applicable).
3. Update `StatusEffectSystem.MapToDamageType` to link the effect to a `DamageType` (e.g., Bleed -> Physical).
4. (Optional) Update `StatusEffectPresentationSystem` for UI.

### 4. Stacking Logic
- **Additive (`Additive = true`)**: Adds `Severity` to existing amount. Good for exposure (Rad + Rad + Rad).
- **Replacement (`Additive = false`)**: Sets `Severity` to `max(current, new)`. Good for debuffs that shouldn't stack infinitely (e.g., Concussion).
- **Duration**: Always refreshes to `max(remaining, new_duration)`.

### 5. Testing Instructions (Debug)
A `StatusEffectDebugSystem` is included for testing in Host Mode (Editor).
1. **Apply Hypoxia**: Press **[** (Left Bracket). Adds 0.2 severity (5s). Repeated presses increase severity.
2. **Apply Radiation**: Press **]** (Right Bracket). Sets severity to 0.5 (10s). Repeated presses refresh duration but don't stack above 0.5 (unless logic changes).
3. **Apply Burn**: Press **\** (Backslash). Adds 1.0 severity (3s). High damage.

**Validation**:
- Open **Entity Debugger**.
- Select Player Entity.
- Observe `StatusEffect` buffer:
  - New elements appear.
  - `Severity` and `TimeRemaining` update.
  - `TickTimer` increments.
- Observe `Health.Current` decreasing ticks.


