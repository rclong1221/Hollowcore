# EPIC 4.2: Damage Types, Mitigation, and “Rules of Harm”

**Priority**: HIGH  
**Goal**: Define consistent rules for damage types and mitigation so content systems can plug in without bespoke logic.  
**Dependencies**: Epic 4.1 (damage pipeline), Epic 2.7 (hazards), Epic 7.x (collision events as a damage source)

## Design Notes (Match EPIC7 Level of Detail)
- **Centralize tuning**: resistances, cooldowns, and invulnerability rules live in one place so you can rebalance without touching hazard/tool code.
- **Determinism**: mitigation is applied server-side only, using stable inputs (no client-only data).
- **Spam control**: mitigation layer is where “micro tick” sources (e.g., overlapping hazard triggers) are rate-limited.
- **Extensibility**: new damage types should require only enum + config + (optional) presentation mapping.

## Design Rules
- Every damage source emits a `DamageEvent` with a `DamageType`.
- Mitigation is applied in one place (server), before subtracting from `Health`.
- Avoid per-feature “if health then …” logic; everything goes through the event pipeline.

## Components

**DamageResistance** (IComponentData, on player; replicated)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `PhysicalMult` | float | Quantization=100 | 1.0 = normal, 0.8 = 20% resist |
| `HeatMult` | float | Quantization=100 | |
| `RadiationMult` | float | Quantization=100 | |
| `SuffocationMult` | float | Quantization=100 | |
| `ExplosionMult` | float | Quantization=100 | |

**DamageCooldown** (IComponentData, optional; replicated)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `NextAllowedTime` | float | Quantization=100 | Prevent micro-tick spam |

## Systems

**DamageMitigationSystem** (SimulationSystemGroup, ServerWorld)
- For each `DamageEvent`:
  - apply resistance multiplier
  - apply cooldown throttling rules (optional)
  - output a final amount to be applied by `DamageApplySystem`

## Sub-Epics / Tasks

### Sub-Epic 4.2.1: Define `DamageType` and Policy Table
**Goal**: A single source of truth for “how each damage type behaves”.
**Tasks**:
- [x] Define `DamageType` enum (if not already defined in code)
- [x] Define per-type policy/config (singleton or ScriptableObject for hybrid):
  - [x] resistance multiplier mapping
  - [x] per-type cooldown window
  - [ ] stack/merge rule (merge within tick vs keep separate)
  - [ ] presentation hints (icon/sfx category)

### Sub-Epic 4.2.2: Cooldowns + Caps
**Goal**: Prevent degenerate cases without hiding real damage.
**Tasks**:
- [ ] Per-player global damage cap per tick (optional safety)
- [x] Per-type cooldown (e.g., radiation tick at most once per second)
- [ ] Per-source cap (same `SourceEntity` cannot apply more than N events per tick)

### Sub-Epic 4.2.3: “I-Frames” and Temporary Immunity
**Goal**: Support dodge/evade windows and spawn protection without special-casing damage sources.
**Tasks**:
- [x] Add optional `DamageImmunity`/`InvulnerabilityWindow` component on player
- [x] Mitigation checks immunity window and zeros damage for covered types
- [x] Ensure immunity replicates if it affects gameplay (ghost field)

### Sub-Epic 4.2.4: QA Checklist
**Tasks**:
- [x] Adjust resistance values; hazards/tools reflect new numbers without code changes
- [x] Cooldown prevents 60hz radiation micro-ticks from deleting HP instantly
- [x] Immunity windows block the intended types only (no accidental godmode)

## Acceptance Criteria
- [x] Changing mitigation numbers requires no edits in hazard/tool systems
- [x] Radiation/temperature/oxygen damage can be tuned independently via resist multipliers
- [x] Damage spam sources can be rate-limited without breaking gameplay

---

## Integration & Usage Guide

### 1. Configuring Damage Resistance
Resistances are configured on the Player prefab via the `PlayerAuthoring` component.
- **Values**:
  - `1.0`: Normal damage (Default)
  - `0.5`: 50% damage reduction
  - `0.0`: Full immunity
  - `> 1.0`: Vulnerability (extra damage)
- **Fields**: defined for Physical, Heat, Radiation, Suffocation, Explosion, Toxic.

### 2. Configuring Global Cooldowns
Cooldowns prevent damage spam (e.g., preventing 60Hz damage ticks from radiation zones).
1. Create a GameObject in the scene (e.g., "GameConfig").
2. Add `DamagePolicyAuthoring` component.
3. specific cooldown durations (in seconds) for each damage type.
   - **Recommended**:
     - `Physical`: 0s (Apply every hit)
     - `Radiation`: 1.0s (Tick once per second)
     - `Heat`: 0.5s
     - `Suffocation`: 0s (Usually handled by streaming depletion, but can be throttled)

### 3. Applying Temporary Immunity (I-Frames)
To give a player temporary invulnerability (e.g., during a dodge roll or spawn protection), set the `DamageInvulnerabilityWindow` component.

```csharp
// In a System:
public void OnUpdate(ref SystemState state)
{
    var networkTime = SystemAPI.GetSingleton<NetworkTime>();
    float currentTime = (float)SystemAPI.Time.ElapsedTime;
    
    foreach (var (immunity, entity) in SystemAPI.Query<RefRW<DamageInvulnerabilityWindow>>().WithEntityAccess())
    {
        // Example: Grant 0.5s immunity to everything
        immunity.ValueRW.SetImmunity(0.5f, currentTime, -1); // -1 mask = Block All
        
        // Example: Grant 2s immunity to Radiation only
        int radiationMask = 1 << (int)DamageType.Radiation;
        immunity.ValueRW.SetImmunity(2.0f, currentTime, radiationMask);
    }
}
```

### 4. Adding a New Damage Type
If you need a new damage source (e.g., "Electric"):
1. Add `Electric = 6` to `DamageType` enum in `DamageEvent.cs`.
2. Update `DamageResistance` struct to include `ElectricMult`.
3. Update `DamageCooldown` struct to include `NextElectricTime`.
4. Update `DamagePolicy` and `DamagePolicyAuthoring` to support `ElectricCooldown`.
5. Update `DamageMitigationSystem` to handle the new case in `GetMultiplier` / `IsCooldownActive`.
6. Update `PlayerAuthoring` to bake the new resistance field.
