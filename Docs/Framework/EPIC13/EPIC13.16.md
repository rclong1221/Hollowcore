# EPIC 13.16: Health & Damage System Parity

> **Status:** IN PROGRESS  
> **Priority:** HIGH  
> **Dependencies:** None  
> **Reference:** `OPSIVE/.../Runtime/Traits/Health.cs`

## Overview

Bring the DIG health/damage systems to feature parity with Opsive's Health trait.

### Already Implemented in DIG
- **DamageType enum** - Physical, Heat, Radiation, Suffocation, Explosion, Toxic
- **DamageEvent.SourceEntity** - Damage source tracking
- **DamageResistance** - Per-type multipliers (0.0-2.0+)
- **DamageInvulnerabilityWindow** - I-frames with type bitmask
- **DamageCooldown** - Per-type damage cooldowns
- **DamagePolicy** - Global cooldown configuration

### Remaining for Parity (~65% complete)
- Hitbox multipliers
- Shield mechanic
- Heal events
- Death callbacks
- UI integrations

---

## Sub-Tasks

### 13.16.1 Hitbox Component & Multipliers
**Status:** NOT STARTED  
**Priority:** HIGH

Define hitbox regions with damage multipliers (headshot = 2x, leg = 0.5x).

#### Implementation
```csharp
public struct Hitbox : IComponentData
{
    public Entity OwnerEntity;       // Parent character
    public float DamageMultiplier;   // 2.0 for head, 0.5 for legs
}

// On damage:
if (hitCollider has Hitbox) {
    damageAmount *= hitbox.DamageMultiplier;
}
```

#### New Components
```csharp
public struct HitboxElement : IBufferElementData
{
    public Entity ColliderEntity;
    public float DamageMultiplier;
}
```

#### Authoring
```csharp
public class HitboxAuthoring : MonoBehaviour
{
    public float DamageMultiplier = 1.0f;
}
```

#### Acceptance Criteria
- [ ] Headshot does 2x damage
- [ ] Leg shot does 0.5x damage
- [ ] Configurable per-collider multipliers
- [ ] Works with ragdoll bones

---

### 13.16.2 Raycast Fallback for Nested Hitboxes
**Status:** NOT STARTED  
**Priority:** MEDIUM

When main collider is hit, raycast to find hitbox colliders underneath.

#### Implementation
```csharp
// If hit main capsule but no hitbox found:
if (hitCollider == mainCapsule && hitboxMap.Count > 0) {
    // Raycast further to find hitbox
    var hits = Physics.RaycastNonAlloc(hitPoint, damageDirection, hitBuffer, 0.2f);
    foreach (var hit in hits) {
        if (hitboxMap.TryGetValue(hit.collider, out var hitbox)) {
            damageAmount *= hitbox.DamageMultiplier;
            break;
        }
    }
}
```

#### Acceptance Criteria
- [ ] Bullets passing through body still hit correct hitbox
- [ ] Capsule overlap doesn't hide hitboxes

---

### 13.16.3 Shield Attribute
**Status:** NOT STARTED  
**Priority:** MEDIUM

Add regenerating shield that absorbs damage before health. Integrate with existing `DamageApplySystem`.

#### Implementation
```csharp
public struct ShieldComponent : IComponentData
{
    public float Current;
    public float Max;
    public float RegenRate;          // Per second
    public float RegenDelay;         // Seconds after damage
    public float LastDamageTime;
}

// On damage (before Health):
if (shield.Current > 0) {
    float shieldDamage = math.min(damageAmount, shield.Current);
    shield.Current -= shieldDamage;
    damageAmount -= shieldDamage;
    shield.LastDamageTime = currentTime;
}
health.Current -= damageAmount;
```

#### Acceptance Criteria
- [ ] Shield absorbs damage first
- [ ] Shield regenerates after delay
- [ ] Shield has separate visual indicator

---

### 13.16.4 Spawn Invincibility Integration
**Status:** PARTIAL (DamageInvulnerabilityWindow exists)  
**Priority:** LOW

Use existing `DamageInvulnerabilityWindow` after respawn.

#### Implementation
```csharp
// In RespawnSystem:
invulnWindow.SetImmunity(spawnInvincibilityDuration, currentTime, -1); // All types
```

#### Acceptance Criteria
- [x] DamageInvulnerabilityWindow component exists
- [ ] RespawnSystem sets invulnerability
- [ ] Visual feedback (flashing)

---

### 13.16.5 Damage Popup Integration
**Status:** NOT STARTED  
**Priority:** LOW

Show floating damage numbers.

#### Implementation
```csharp
// On damage:
if (DamagePopupManager != null) {
    DamagePopupManager.ShowPopup(hitPosition, damageAmount, isCrit);
}
```

#### Acceptance Criteria
- [ ] Damage numbers appear at hit location
- [ ] Different color for crits
- [ ] Configurable per-character

---

### 13.16.6 Death Layer Change
**Status:** NOT STARTED  
**Priority:** LOW

Change GameObject layer on death (for collision filtering).

#### Implementation
```csharp
public struct DeathSettings : IComponentData
{
    public int AliveLayer;
    public int DeathLayer;
}

// On death:
gameObject.layer = DeathLayer;
```

#### Acceptance Criteria
- [ ] Dead bodies on different layer
- [ ] Live players don't collide with corpses (optional)

---

### 13.16.7 Spawn Objects on Death
**Status:** NOT STARTED  
**Priority:** LOW

Spawn explosions, loot, etc. when character dies.

#### Implementation
```csharp
public struct DeathSpawnElement : IBufferElementData
{
    public Entity Prefab;
    public bool ApplyForce; // Inherit death force
}

// On death:
foreach (var spawn in deathSpawns) {
    Instantiate(spawn.Prefab, position, rotation);
    if (spawn.ApplyForce) {
        rigidbody.AddForce(deathForce);
    }
}
```

#### Acceptance Criteria
- [ ] Loot drops on death
- [ ] Explosion spawns (for barrels)
- [ ] Force applied to ragdoll pieces

---

### 13.16.8 Attribute Regen Cancellation on Death
**Status:** NOT STARTED  
**Priority:** LOW

Stop health/shield regeneration when dead.

#### Implementation
```csharp
// On death:
shieldRegenTimer = 0;
// ShieldRegenSystem checks DeathState.Phase
```

#### Acceptance Criteria
- [ ] No regen while dead
- [ ] Regen resumes on respawn

---

### 13.16.9 Heal Events
**Status:** NOT STARTED  
**Priority:** MEDIUM

Add healing support using the same pattern as DamageEvent.

#### Implementation
```csharp
public struct HealEvent : IBufferElementData
{
    public float Amount;
    public Entity SourceEntity;
    public float3 Position;
    public uint ServerTick;
}

// In HealApplySystem:
health.Current = math.min(health.Max, health.Current + healAmount);
```

#### Acceptance Criteria
- [ ] HealEvent buffer exists
- [ ] HealApplySystem processes heals
- [ ] Overheal capped at MaxHealth
- [ ] Heal source tracked

---

### 13.16.10 Death Event Callbacks
**Status:** NOT STARTED  
**Priority:** MEDIUM

Add OnWillDie (cancellable) and OnDeath callbacks for systems to hook into.

#### Implementation
```csharp
// In DeathTransitionSystem:
// 1. OnWillDie phase - allow cancellation (god mode, last stand)
if (health.Current <= 0 && !deathState.DeathPrevented) {
    // Broadcast WillDie event
    ecb.AddComponent<WillDieEvent>(entity);
}

// 2. OnDeath phase - post-death cleanup
if (deathState.Phase == DeathPhase.Dying) {
    // Broadcast DiedEvent
    ecb.AddComponent<DiedEvent>(entity);
}
```

#### Acceptance Criteria
- [ ] WillDieEvent allows cancellation
- [ ] DiedEvent triggers post-death logic
- [ ] Systems can react to both events

---

### 13.16.11 Health Changed Events
**Status:** NOT STARTED  
**Priority:** LOW

Generic health change events for UI binding and effects.

#### Implementation
```csharp
public struct HealthChangedEvent : IComponentData, IEnableableComponent
{
    public float OldValue;
    public float NewValue;
    public float Delta; // Negative = damage, Positive = heal
    public Entity Source;
}
```

#### Acceptance Criteria
- [ ] Event fired on any health change
- [ ] UI can bind to event
- [ ] Delta correctly signed

---

### 13.16.12 Kill Attribution System
**Status:** NOT STARTED  
**Priority:** HIGH

Track kills and assists using existing DamageEvent.SourceEntity.

#### Implementation
```csharp
// On death:
Entity killer = GetLastDamageSource(damageBuffer);
ecb.AddComponent(killer, new KillCredited { Target = entity, Position = position });

// Track assists (entities that dealt damage in last X seconds)
foreach (var assist in recentDamageSources) {
    if (assist != killer) {
        ecb.AddComponent(assist, new AssistCredited { Target = entity });
    }
}
```

#### Acceptance Criteria
- [ ] Last damager gets kill credit
- [ ] Recent damagers get assist credit
- [ ] Kill feed integration

---

## Files to Modify

| File | Changes |
|------|---------|
| `Hitbox.cs` | New component (13.16.1) |
| `HitboxAuthoring.cs` | New authoring (13.16.1) |
| `ShieldComponent.cs` | New component (13.16.3) |
| `ShieldRegenSystem.cs` | New system (13.16.3) |
| `HealEvent.cs` | New buffer element (13.16.9) |
| `HealApplySystem.cs` | New system (13.16.9) |
| `WillDieEvent.cs` | New event component (13.16.10) |
| `DiedEvent.cs` | New event component (13.16.10) |
| `HealthChangedEvent.cs` | New component (13.16.11) |
| `KillAttributionSystem.cs` | New system (13.16.12) |
| `DamageApplySystem.cs` | Hitbox multipliers, shield logic, events |
| `DeathTransitionSystem.cs` | Layer change, spawn objects, death events |
| `RespawnSystem.cs` | Reset shield, invulnerability, restart regen |

## Verification Plan

### Core Damage Tests
1. Shoot head → 2x damage (13.16.1)
2. Shoot leg → 0.5x damage (13.16.1)
3. Nested hitbox raycast works (13.16.2)

### Shield Tests
4. Damage with shield → shield drains first (13.16.3)
5. Wait after damage → shield regenerates (13.16.3)
6. Shield displays correctly (T2)

### Spawn/Death Tests
7. Respawn → briefly invincible (13.16.4)
8. Kill enemy → loot drops (13.16.7)
9. Dead body on different layer (13.16.6)

### Event System Tests
10. Heal station restores HP (13.16.9)
11. WillDie event can be cancelled (13.16.10)
12. DiedEvent triggers (13.16.10)
13. Health changed events fire (13.16.11)

### Attribution Tests
14. Last damager gets kill credit (13.16.12)
15. Assists tracked for recent damage (13.16.12)

---

## Test Environment Tasks

Create the following test objects under: `GameObject > DIG - Test Objects > Combat > Damage Tests`

### 13.16.T1 Hitbox Target Dummy
**Status:** IMPLEMENTED (TestEnvironmentComponents.cs)

Target dummy with visible hitbox regions.

#### Specifications
- Full humanoid dummy with labeled hitbox regions
- Head (2x multiplier, red)
- Torso (1x multiplier, yellow)
- Arms (0.75x multiplier, green)
- Legs (0.5x multiplier, blue)
- Damage display showing multiplier applied
- Respawning after destruction

#### Hierarchy
```
Damage Tests/
  Hitbox Dummy/
    Dummy_Model
    Hitbox_Head (2x)
    Hitbox_Torso (1x)
    Hitbox_Arms (0.75x)
    Hitbox_Legs (0.5x)
    Damage Display (UI)
```

---

### 13.16.T2 Shield Test Arena
**Status:** IMPLEMENTED (TestEnvironmentComponents.cs / TestEnvironmentSystems.cs)

Arena to test shield mechanics.

#### Specifications
- Enemy with 100 health + 50 shield
- Health/Shield bars visible above enemy
- Timer showing shield regen progress
- Damage source (turret or trigger)

#### Hierarchy
```
Damage Tests/
  Shield Arena/
    Enemy_Shielded
    Health Bar (UI)
    Shield Bar (UI)
    Regen Timer (UI)
    Damage Turret
```

---

### 13.16.T3 Spawn Invincibility Test
**Status:** IMPLEMENTED (RespawnSystem + TestComponents)

Spawner with damage field to test invincibility.

#### Specifications
- Spawn point in damage-dealing zone
- Timer showing invincibility remaining
- Visual effect (flashing) during invincibility
- Damage source constant

#### Hierarchy
```
Damage Tests/
  Invincibility Test/
    Spawn Point
    Damage Zone
    Invincibility Timer (UI)
    Visual Effect
```

---

### 13.16.T4 Death Loot Spawner
**Status:** IMPLEMENTED (DeathSpawnSystem + TestComponents)

Enemies that drop loot on death.

#### Specifications
- Enemy that drops configurable items
- Explosive barrel that spawns explosion
- Loot pickup zone
- Death force demonstration (ragdoll)

#### Hierarchy
```
Damage Tests/
  Loot Test/
    Enemy_LootDropper
    Explosive_Barrel
    Loot Pickup Zone
    Ragdoll Target
```

---

### 13.16.T5 Damage Popup Test
**Status:** IMPLEMENTED (DamagePopupSystem + TestComponents)

Test area for damage number display.

#### Specifications
- Multiple targets at various distances
- Normal damage (white numbers)
- Critical damage (yellow/red numbers)
- Heal pickup (green numbers)

#### Hierarchy
```
Damage Tests/
  Popup Test/
    Target_Near
    Target_Mid
    Target_Far
    Heal Pickup
```

---

### 13.16.T6 Heal Station Test
**Status:** IMPLEMENTED (HealStationSystem + TestComponents)

Test area for heal events (13.16.9).

#### Specifications
- Heal station that applies HealEvent
- Health bar showing current/max
- Green heal numbers floating
- Heal source tracking display

#### Hierarchy
```
Damage Tests/
  Heal Station/
    Player_Damaged (50/100 HP)
    Health Bar (UI)
    Heal_Station (trigger)
    Heal Numbers Display
```

---

### 13.16.T7 Death Callback Test
**Status:** IMPLEMENTED (GodModeSystem + TestComponents)

Test area for death events (13.16.10).

#### Specifications
- Enemy with WillDie cancellation (god mode toggle)
- Enemy with DiedEvent listener (spawns effect)
- Kill counter display
- Death prevention toggle button

#### Hierarchy
```
Damage Tests/
  Death Callback/
    Enemy_Cancelable (god mode)
    Enemy_SpawnsOnDeath
    Kill Counter (UI)
    God Mode Toggle
```

---

### 13.16.T8 Kill Attribution Test
**Status:** IMPLEMENTED (KillFeedSystem + Turret Code)

Test area for kill/assist tracking (13.16.12).

#### Specifications
- Multiple damage sources (turrets)
- Enemy that tracks damage sources
- Kill feed display
- Assist credit display

#### Hierarchy
```
Damage Tests/
  Kill Attribution/
    Turret_A
    Turret_B
    Target_Enemy (100 HP)
    Kill Feed (UI)
    Assist Display (UI)
```

---

### 13.16.T9 Resistance Test Arena
**Status:** IMPLEMENTED (Supported by DamageResistance Logic)

Test existing DamageResistance component.

#### Specifications
- Target with configurable resistances
- Multiple damage type triggers (fire, explosion, etc.)
- Damage taken display per type
- Resistance value sliders

#### Hierarchy
```
Damage Tests/
  Resistance Arena/
    Target_Resistant
    Fire Trigger
    Explosion Trigger
    Radiation Trigger
    Damage Display (UI)
    Resistance Sliders (UI)
```
