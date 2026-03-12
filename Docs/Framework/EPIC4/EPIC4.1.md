# EPIC 4.1: Health, Damage Events, and Death State

**Priority**: HIGH  
**Status**: ✅ **IMPLEMENTED**  
**Goal**: A server-authoritative, NetCode-safe damage pipeline that can be used by hazards, tools, physics impacts, and creatures.  
**Dependencies**: Epic 1.4 (`PlayerState`), Survival hazards (oxygen/radiation/temp), `Player.Components.Health`

## Design Notes (Match EPIC7 Level of Detail)
- **One pipeline**: all harm becomes `DamageEvent` (no feature writes `Health.Current` directly).
- **Server is the only writer**: the server applies damage and sets `DeathState`; clients only present.
- **Prediction-safe**: clients can predict *feedback* (hit indicators), but never predict final health/death outcomes.
- **Ordering**: hazards/tools/collisions enqueue damage → mitigation (Epic 4.2) → apply health → transition death → post-death cleanup.
- **Bounded buffers**: `DamageEvent` must have a cap + compaction rules to prevent runaway memory in extreme cases.
- **Replication**: health/death state needs explicit Ghost replication rules so remote clients see correct outcomes (no hidden server-only state).

## Components

**Health** (IComponentData, on player; `Assets/Scripts/Player/Components/Health.cs`)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Current` | float | Quantization=100 | Current HP |
| `Max` | float | No | Max HP |
| `Normalized` | float (property) | - | Current/Max ratio for UI |
| `IsDepleted` | bool (property) | - | True when Current <= 0 |

**DamageEvent** (IBufferElementData, on entities that can take damage; server-consumed)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Amount` | float | Quantization=100 | HP to subtract (post-mitigation optional) |
| `Type` | DamageType | Yes | Physical / Heat / Radiation / Suffocation / Explosion / Toxic |
| `SourceEntity` | Entity | Yes | Who/what caused it (optional) |
| `HitPosition` | float3 | Quantization=100 | For feedback (optional) |
| `ServerTick` | uint | Yes | Ordering/debugging |

**DeathState** (IComponentData, on player; replicated)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Phase` | DeathPhase | Yes | Alive / Downed / Dead / Respawning |
| `StateStartTime` | float | Quantization=100 | For timing |
| `RespawnDelay` | float | No | Seconds (default: 5s) |
| `InvulnerabilityDuration` | float | No | Post-respawn invuln (default: 3s) |
| `InvulnerabilityEndTime` | float | Quantization=100 | When invuln expires |

**DeathPresentationState** (IComponentData, local client-only)
| Field | Type | Description |
|---|---|---|
| `CurrentHealthPercent` | float | 0-1 for UI |
| `LowHealthIntensity` | float | 0 = healthy, 1 = critical |
| `IsDead` | bool | Combined Dead/Downed check |
| `TriggerHitEffect` | bool | One-frame hit marker trigger |
| `TriggerDeathEffect` | bool | One-frame death screen trigger |

## Systems

**DamageSystemGroup** (SimulationSystemGroup, ServerWorld)
- System group for damage pipeline ordering
- Ensures: Bridge systems → DamageApplySystem → DeathTransitionSystem → DeathPlayerStateSyncSystem

**DamageApplySystem** (DamageSystemGroup, ServerWorld)
- Consumes `DamageEvent` buffers (max 16 events/tick)
- Validates damage (rejects NaN/Inf/negative)
- Respects invulnerability (`DeathState.InvulnerabilityEndTime`, dead state)
- Applies to `Health.Current` (clamp 0..Max)
- Clears buffer after processing

**DamageApplyWithDodgeInvulnSystem** (DamageSystemGroup, ServerWorld)
- Clears damage for entities with `DodgeRollInvuln` component
- Backwards compatibility with existing dodge roll invulnerability

**DeathTransitionSystem** (DamageSystemGroup, ServerWorld)
- When `Health.Current <= 0`, transitions `DeathState.Phase`:
  - Alive → Dead (MVP, skips Downed)
- Sets `StateStartTime` for respawn timing
- Prevents double-death by checking current phase

**DeathPlayerStateSyncSystem** (DamageSystemGroup, ServerWorld)
- Syncs `PlayerState.Mode` with `DeathState.Phase`
- Sets `PlayerMode.Dead` when dead/downed
- Resets to `PlayerMode.EVA` when alive (after respawn)

**DeathPresentationSystem** (PresentationSystemGroup, ClientWorld)
- Tracks local player health changes
- Computes `LowHealthIntensity` (full at <30% HP)
- Triggers hit/death effects for UI systems

## Bridge Systems

**ExplosionDamageBridgeSystem** (SimulationSystemGroup, ServerWorld)
- Converts `ExplosionDamageEvent` → `DamageEvent`
- Sets `DamageType.Explosion`

**SurvivalDamageBridgeSystem** (SimulationSystemGroup, any world)
- Converts `SurvivalDamageEvent` → `DamageEvent`
- Only map damage sources: Suffocation, Radiation, Heat (Hypo/Hyperthermia), Toxic

### Survival & Environmental Hazards
The damage pipeline integrates with survival systems via `SurvivalDamageBridgeSystem`.
*   **Oxygen**: `OxygenDepletionSystem` reduces tank level. When empty, `OxygenSuffocationSystem` generates `SurvivalDamageEvent` (Suffocation).
*   **Radiation**: `RadiationSystem` accumulates exposure. Exceeding threshold triggers `SurvivalDamageEvent` (Radiation).
*   **Zone Detection**: Relies on `EnvironmentZoneDetectionSystem` (Physics) and `ShipLocalSpaceZoneSystem` (Ship State). *Critical Requirement: `ShipLocalSpaceZoneSystem` must run on both Client (for prediction) and Server to correctly detect vacuum inside powered-down ships.*

## Sub-Epics / Tasks

### Sub-Epic 4.1.1: Component Replication Rules (NetCode)
**Goal**: Remote clients see consistent health/death outcomes.
**Tasks**:
- [x] Replicate `Health.Current` with `[GhostField(Quantization = 100)]`
- [x] Add `[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]` to Health
- [x] Add `[InternalBufferCapacity(8)]` to `DamageEvent`
- [x] Add `[GhostComponent]` to `DamageEvent` buffer for replication

### Sub-Epic 4.1.2: Damage Buffer Invariants
**Goal**: No runaway buffers, no per-frame spam.
**Tasks**:
- [x] Validate damage amounts (reject NaN/Inf/negative)
- [x] Cap events processed per tick (MaxEventsPerTick = 16)
- [ ] Optional merge rule (not yet implemented: same Type + SourceEntity → merge)

### Sub-Epic 4.1.3: System Ordering (Server)
**Goal**: Deterministic server behavior.
**Tasks**:
- [x] Bridge systems run before DamageSystemGroup
- [x] DamageApplySystem runs in DamageSystemGroup
- [x] DeathTransitionSystem runs after DamageApplySystem
- [x] DeathPlayerStateSyncSystem runs after DeathTransitionSystem
- [ ] `DamageMitigationSystem` (Epic 4.2) - not yet implemented
- [ ] `RespawnSystem` (Epic 4.5) - not yet implemented

### Sub-Epic 4.1.4: Presentation Hooks (Client)
**Goal**: Rich feedback without extra replication.
**Tasks**:
- [x] `DeathPresentationState` component with hit/death triggers
- [x] `LowHealthIntensity` computed (0-1 scale)
- [x] `TriggerHitEffect` / `TriggerDeathEffect` one-frame flags
- [ ] Actual UI/VFX implementation (depends on UI framework)

### Sub-Epic 4.1.5: QA Checklist
**Tasks**:
- [x] Suffocation damage routes through DamageEvent → Health
- [x] Explosive damage routes through DamageEvent → Health
- [x] No "double-death" (phase check prevents repeat transitions)
- [x] Invulnerability respected (time-based and dodge roll)
- [ ] Network latency testing (requires multiplayer test environment)

## File Locations

```
Assets/Scripts/Player/
├── Components/
│   ├── Health.cs              # Health component with GhostField replication
│   ├── DamageEvent.cs         # DamageEvent buffer with damage types
│   └── DeathState.cs          # Death phase state machine
├── Systems/
│   ├── DamageApplySystem.cs           # Server damage application + DamageSystemGroup
│   ├── DeathTransitionSystem.cs       # Death transition + PlayerState sync
│   └── DeathPresentationSystem.cs     # Client-side presentation state
├── Bridges/
│   └── Damage/
│       └── DamageBridgeSystems.cs     # Bridge systems for explosion/survival damage
├── UI/
│   ├── HealthHUD.cs                   # Runtime HUD reading ECS health data
│   └── HealthHUDBuilder.cs            # Auto-creates HUD at runtime
└── Authoring/
    └── PlayerAuthoring.cs             # Updated to bake all new components
```

## Prefab Setup

### Ghost Prefab (Server) - `Warrok_Server.prefab`

⚠️ **Action Required**: Add `SurvivalAuthoring` component to enable oxygen/survival damage.

Components baked by `PlayerAuthoring`:
| Component | Default Values |
|-----------|----------------|
| `Health` | Current=100, Max=100 |
| `DeathState` | Phase=Alive, RespawnDelay=5s, InvulnerabilityDuration=3s |
| `DamageEvent` (buffer) | InternalBufferCapacity=8 |
| `DeathPresentationState` | LowHealthIntensity=0, IsDead=false |

Components baked by `SurvivalAuthoring` (**MUST ADD TO PREFAB**):
| Component | Purpose |
|-----------|---------|
| `SurvivalDamageEvent` | Bridge for survival damage → DamageEvent |
| `OxygenTank` | Stores player oxygen level |
| `OxygenConsumer` | Tag for oxygen depletion |
| `CurrentEnvironmentZone` | Tracks which zone player is in |
| `EnvironmentSensitive` | Tag for zone detection |

**How to add SurvivalAuthoring:**
1. Open `Warrok_Server.prefab` in the Prefab Editor
2. Select the root GameObject
3. Click **Add Component** → search for `SurvivalAuthoring`
4. Configure oxygen settings (defaults are fine for testing)
5. Save the prefab

#### Configurable Fields in SurvivalAuthoring
| Field | Default | Description |
|---|---|---|
| **OxygenDepletionRate** | 1.0 | Units consumed per second (1.0 = 100s duration) |
| **MaxOxygen** | 100 | Total tank capacity |
| **SuffocationDamage** | 10 | HP damage per second when O2 is empty |
| **OxygenWarningThreshold** | 0.25 | Trigger warning UI at 25% |
| **OxygenCriticalThreshold** | 0.10 | Trigger critical warning at 10% |
| **RadiationSusceptible** | True | Enables radiation tracking |
| **RadiationDamageThreshold** | 100 | Accumulated rads before damage starts |


### Client Prefab - `Warrok_Client.prefab`

✅ **No Changes Required** - Components are baked automatically.

### UI Setup - `HealthHUDBuilder`

✅ **Auto-Created at Runtime** - Similar to `PowerHUDBuilder`, the `HealthHUDBuilder` creates all UI at runtime.

**To enable the Health HUD:**

1. Add `HealthHUDBuilder` component to a persistent scene GameObject (e.g., GameManager, Bootstrap, etc.)
2. The HUD will auto-create when the game starts

**What gets created automatically:**

| UI Element | Description |
|------------|-------------|
| **Health Bar** | Bottom-center, shows current/max HP with color coding (green → yellow → red) |
| **Low-Health Vignette** | Full-screen red overlay, intensity increases as HP drops below 30% |
| **Hit Indicator** | Red screen flash when damage is taken (0.3s duration) |
| **Death Screen** | "YOU DIED" overlay with dark background when player dies |

**Events exposed for audio/FX integration:**

```csharp
// On the HealthHUD component:
OnDamageTaken   // Fires each time health decreases
OnDeath         // Fires when transitioning to Dead state
OnRespawn       // Fires when transitioning back to Alive state
```

**Manual alternative** (if you want custom UI):

Instead of using `HealthHUDBuilder`, create your own UI and add `HealthHUD` component, then wire up the references manually in the Inspector.

### Adding Damage to Custom Entity Types

To make any entity damageable (NPCs, destructibles, etc.):

1. Create an authoring component:
```csharp
public class DamageableAuthoring : MonoBehaviour
{
    public float MaxHealth = 100f;
    
    class Baker : Baker<DamageableAuthoring>
    {
        public override void Bake(DamageableAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Required components
            AddComponent(entity, new Health 
            { 
                Current = authoring.MaxHealth, 
                Max = authoring.MaxHealth 
            });
            AddComponent(entity, DeathState.Default);
            AddBuffer<DamageEvent>(entity);
            
            // Optional: Client effects
            AddComponent(entity, DeathPresentationState.Default);
        }
    }
}
```

2. Add to prefab:
   - Add the `DamageableAuthoring` component to your prefab
   - Set `MaxHealth` as desired
   - Ensure the prefab has `GhostAuthoringComponent` for replication

## Acceptance Criteria

- [x] Hazards/tools can enqueue `DamageEvent` without directly mutating health
- [x] Server applies damage deterministically and replicates resulting `Health`/`DeathState`
- [x] Damage buffer is bounded (rate-limited or compacted) to avoid unbounded growth

## Testing Instructions

### Manual Testing

1. **Create Test Environment**:
   - Use `GameObject > DIG - Test Objects > Ships > Complete Test Ship`
   - Add `HealthHUDBuilder` component to a persistent GameObject (or the Main Camera)
   - Enter the ship (use airlock)

2. **Test Suffocation Damage**:
   - Toggle power off (Press **O**) to disable life support
   - Interior becomes Vacuum, oxygen depletes
   - When oxygen reaches 0, suffocation damage begins
   - Watch health decrease in Entity Debugger
   - Verify death triggers when health reaches 0

3. **Test Explosion Damage**:
   - Equip an **Explosive** (C4)
   - Place and detonate near player
   - Verify health decreases via `DamageEvent` pipeline
   - Multiple explosions should stack damage correctly

4. **Test Invulnerability**:
   - Perform dodge roll while taking damage
   - Verify `DodgeRollInvuln` prevents damage during roll

5. **Verify Death State**:
   - Reduce health to 0 via any damage source
   - Check `DeathState.Phase` in Entity Debugger
   - Should transition from `Alive` to `Dead`
   - `PlayerState.Mode` should sync to `Dead`

### Debugging

- Use **Entity Debugger** to view:
  - `Health.Current` - Current HP
  - `DeathState.Phase` - Alive/Dead status
  - `DamageEvent` buffer - Queued damage events
- Use **P** key for power debug output (life support status)

## Known Limitations

1. **No Respawn**: Players stay dead forever (Epic 4.5 will add respawn)
2. **No Damage Mitigation**: All damage applies at full value (Epic 4.2 will add armor/resistance)
3. **No Death Animation**: Player model doesn't animate death (requires animator integration)
4. **Basic Vignette**: Low-health vignette is solid red, not a proper gradient/texture
5. **Downed Phase Skipped**: MVP goes directly Alive → Dead (no Downed/revive mechanic)
6. **No Audio**: Hit/death sounds not implemented (hook into `HealthHUD.OnDamageTaken`/`OnDeath` events)

## Future Improvements

1. **Epic 4.2**: Damage mitigation (armor, resistance by damage type)
2. **Epic 4.5**: Respawn system with respawn timer UI
3. **Death Animation**: Play ragdoll or death animation
4. **Audio Integration**: Wire `HealthHUD` events to audio clips
5. **Damage Numbers**: Floating damage text on hit
6. **Kill Feed**: Who killed whom notification
7. **Improved Vignette**: Use proper vignette shader/texture

---

## Integration Guide for Designers/Developers

### Adding Damage to an Entity

To damage a player or NPC, add a `DamageEvent` to their buffer. **Never modify `Health.Current` directly.**

```csharp
using Player.Components;

// In your system (server-side):
void ApplyDamageToTarget(Entity target, float damage, DamageType type, Entity source)
{
    if (SystemAPI.HasBuffer<DamageEvent>(target))
    {
        var buffer = SystemAPI.GetBuffer<DamageEvent>(target);
        buffer.Add(new DamageEvent
        {
            Amount = damage,
            Type = type,
            SourceEntity = source,
            HitPosition = float3.zero, // Optional: use hit location for effects
            ServerTick = networkTime.ServerTick.TickIndexForValidTick
        });
    }
}
```

### Damage Types

| Type | Use Case |
|---|---|
| `DamageType.Physical` | Collisions, melee, fall damage |
| `DamageType.Heat` | Fire, temperature extremes |
| `DamageType.Radiation` | Radiation zones |
| `DamageType.Suffocation` | Oxygen depletion |
| `DamageType.Explosion` | Explosive blasts |
| `DamageType.Toxic` | Poison, chemical hazards |

### Checking Death State

```csharp
// Check if player is dead
if (SystemAPI.HasComponent<DeathState>(playerEntity))
{
    var deathState = SystemAPI.GetComponent<DeathState>(playerEntity);
    if (deathState.Phase == DeathPhase.Dead)
    {
        // Player is dead
    }
}

// Or use Health.IsDepleted for quick check
var health = SystemAPI.GetComponent<Health>(playerEntity);
if (health.IsDepleted)
{
    // Health is 0 or below
}
```

### Client-Side Effects

Read `DeathPresentationState` to trigger UI/VFX:

```csharp
// In a client-side MonoBehaviour or system
var presentation = SystemAPI.GetComponent<DeathPresentationState>(localPlayerEntity);

// Low health warning (0 = healthy, 1 = critical)
SetVignetteIntensity(presentation.LowHealthIntensity);

// Hit marker
if (presentation.TriggerHitEffect)
{
    PlayHitSound();
    ShowHitMarker(presentation.LastDamageAmount);
}

// Death screen
if (presentation.TriggerDeathEffect)
{
    ShowDeathScreen();
}
```

### System Ordering

If you create new damage-related systems:

1. **Damage producers** (hazards, weapons) - Run in `SimulationSystemGroup`, before `DamageSystemGroup`
2. **Damage mitigation** (Epic 4.2) - Run in `DamageSystemGroup`, before `DamageApplySystem`
3. **Post-death logic** - Run after `DeathPlayerStateSyncSystem`

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(DamageSystemGroup))]
public partial struct MyDamageProducerSystem : ISystem
{
    // Add damage events here
}
```

### Adding New Damageable Entity Types

To make any entity support damage:

1. Add `Health` component
2. Add `DeathState` component  
3. Add `DamageEvent` buffer
4. (Optional) Add `DeathPresentationState` for client effects

In an authoring component:
```csharp
public override void Bake(MyEntityAuthoring authoring)
{
    var entity = GetEntity(TransformUsageFlags.Dynamic);
    
    AddComponent(entity, Health.Default);
    AddComponent(entity, DeathState.Default);
    AddBuffer<DamageEvent>(entity);
    AddComponent(entity, DeathPresentationState.Default);
}
```

