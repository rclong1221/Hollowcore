# Epic 15.15 Setup Guide: Combat State System

This guide covers the Unity Editor setup for the **Combat State System** - a persistent state tracker that determines when entities are "in combat".

---

## Overview

The combat state system tracks whether entities are currently engaged in combat:
- **In Combat** - Entity has recently dealt or received damage
- **Out of Combat** - No combat activity for `CombatDropTime` seconds (default 5s)

This enables features like:
- Health regeneration blocking while fighting
- Battle music triggers
- AI alert posture
- Health bar visibility modes (`WhenInCombat`, `WhenInCombatWithTimeout`)

### Two CombatState Types

The project has two different `CombatState` components:

| Component | Namespace | Purpose |
|-----------|-----------|---------|
| `CombatState` | `Player.Components` | Kill attribution - tracks `LastAttacker`, `LastAttackTime` for kill credit |
| `CombatState` | `DIG.Combat.Components` | **Combat state tracking** - `IsInCombat`, timers, etc. (this EPIC) |

---

## Quick Start

### 1. Add CombatStateAuthoring to Enemy Prefabs

1. Open your enemy prefab (e.g., `BoxingJoe.prefab`)
2. Add Component → **Combat** → `CombatStateAuthoring`
3. Configure settings:

| Field | Default | Description |
|-------|---------|-------------|
| Combat Drop Time | 5 | Seconds without combat before exiting combat state |
| Can Enter Combat | ✅ | Whether this entity can enter combat (disable for non-combatants) |
| Start In Combat | ☐ | Start already in combat (for testing or spawned-in-combat scenarios) |

> **Note:** `BoxingJoe.prefab` already has `CombatStateAuthoring` configured.

### 2. Verify DamageableAuthoring is Present

Combat state is triggered when damage is dealt. Entities need:

1. `DamageableAuthoring` component (for Health, DamageEvent buffer)
2. `CombatStateAuthoring` component (for combat state tracking)

Both are required for full combat state functionality.

### 3. Add to Player Prefab (Optional)

If you want the player to track combat state (for regeneration blocking, etc.):

1. Open player prefab
2. Add Component → **Combat** → `CombatStateAuthoring`
3. Configure as above

---

## How Combat State Works

### Automatic Entry
Combat state is set **automatically** when:
- Entity **receives damage** via `DamageEvent`
- Entity **deals damage** to another entity

No manual scripting required - the damage systems handle it.

### Automatic Exit
Combat exits when:
- No damage dealt or received for `CombatDropTime` seconds
- Timer resets on each damage event

---

## Testing Combat State

### Method 1: Using HealthBarVisibilityTester

1. Add `HealthBarVisibilityTester` component to any GameObject in scene
2. In Inspector, enable **Use Direct Override**
3. Set **Direct Mode** to `WhenInCombat`
4. Play the scene and hit an enemy
5. Observe:
   - **"Entities In Combat: 1"** in tester overlay
   - Health bar appears on damaged enemy
   - Health bar hides after 5 seconds of no combat

### Method 2: Using Entity Debugger

1. Play the scene
2. Open **Window → Entities → Debugger**
3. Select **ServerWorld** in world dropdown
4. Find an enemy entity (e.g., BoxingJoe)
5. Look for `CombatState` component:

| Field | Expected Value |
|-------|----------------|
| IsInCombat | `true` after hit, `false` after timeout |
| TimeSinceLastCombatAction | Counts up each frame after last hit |
| CombatDropTime | 5 (default) |
| CombatExitTime | Set when combat exits |

### Method 3: Console Logs (Debug Builds)

When hitting an enemy, you may see:
```
[SimpleDamageApply] Entity 8697 ENTERED COMBAT (took 1.0 damage)
[SimpleDamageApply] Attacker 18580 ENTERED COMBAT (dealt damage)
```

> **Note:** Logs are disabled in optimized/Burst-compiled builds.

---

## Health Bar Visibility Integration

The combat state integrates with the Health Bar Visibility System (EPIC 15.14).

### Setup WhenInCombat Mode

1. Create or select a `HealthBarVisibilityConfig` asset
2. Set **Primary Mode** to `WhenInCombat`
3. Assign to `EnemyHealthBarPool.cachedConfig`

Or use the tester:
1. Add `HealthBarVisibilityTester` to scene
2. Enable **Use Direct Override**
3. Set **Direct Mode** to `WhenInCombat` or `WhenInCombatWithTimeout`

### Visibility Modes Using Combat State

| Mode | Behavior |
|------|----------|
| `WhenInCombat` | Show only while entity is in combat |
| `WhenInCombatWithTimeout` | Show while in combat + fade out after `hideAfterSeconds` |

---

## Configuration Reference

### CombatStateAuthoring Inspector Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Combat Drop Time | float | 5 | Seconds without combat before exiting |
| Can Enter Combat | bool | true | Whether entity can enter combat |
| Start In Combat | bool | false | Initialize in combat state |

### Runtime CombatState Component

| Field | Type | Description |
|-------|------|-------------|
| IsInCombat | bool | Whether currently in combat |
| TimeSinceLastCombatAction | float | Seconds since last damage |
| CombatDropTime | float | Per-entity timeout threshold |
| CombatExitTime | float | Game time when combat exited |

---

## Troubleshooting

### "Entities In Combat: 0" after hitting enemies

1. **Check CombatStateAuthoring** - Verify enemy prefab has `CombatStateAuthoring` component
2. **Check DamageableAuthoring** - Entity needs damage system components
3. **Check prefab variant** - If using prefab variants (e.g., `BoxingJoe_ECS`), ensure base prefab has components
4. **Check ServerWorld** - Combat state runs on server; ensure you're in Host mode

### Combat state not showing in Entity Debugger

1. Select **ServerWorld** in world dropdown (not ClientWorld)
2. Search for entity by index number
3. Expand component list to find `CombatState`

### Health bars not responding to combat state

1. Verify `EnemyHealthBarPool` has **Use Visibility System** enabled
2. Verify visibility mode is set to `WhenInCombat` or `WhenInCombatWithTimeout`
3. Check that bridge system can read from ServerWorld (host mode only)

### Enemies stay "in combat" forever

1. Check `CombatDropTime` value (should be ~5 seconds)
2. Verify `CombatStateSystem` is running (check Systems window)
3. Check for continuous damage sources resetting the timer

---

## System Execution Order

```
DamageSystemGroup:
├── DamageApplyWithDodgeInvulnSystem (clears damage for dodging)
├── DamageApplySystem (player damage)
├── CombatStateFromDamageSystem (puts attackers in combat)
└── SimpleDamageApplySystem (enemy damage + combat state)

SimulationSystemGroup:
└── CombatStateSystem (timer updates, exit transitions)

Presentation (MonoBehaviour):
└── EnemyHealthBarBridgeSystem (reads combat state for UI)
```

---

## Related Documentation

- [EPIC 15.15 - Combat State System](Docs/EPIC15/EPIC15.15.md) - Technical specification
- [EPIC 15.14 - Health Bar Visibility System](Docs/EPIC15/EPIC15.14.md) - Visibility modes
- [SETUP_GUIDE_15.7.md](SETUP_GUIDE_15.7.md) - Opsive Parity Analysis
