# EPIC 15.15: Combat State System

**Status:** Complete ✅
**Dependencies:** `DamageApplySystem.cs`, `SimpleDamageApplySystem.cs`, `CombatStateComponents.cs`
**Feature:** Persistent Combat State Logic

## Overview
Currently, the codebase handles "Combat" only as instantaneous events (Hits). There is no concept of "Being in Combat". This EPIC introduces a persistent state system to track when an entity is engaged in combat, allowing for behaviors like:
- Preventing health regeneration while fighting.
- Playing battle music.
- Keeping AI in an "Alert" posture.
- Triggering "OnEnterCombat" and "OnExitCombat" effects.
- **Health bar visibility modes** (WhenInCombat, WhenInCombatWithTimeout)

## Implemented Components

### 1. `CombatState` Component ✅
**File:** `Assets/Scripts/Combat/Components/CombatStateComponents.cs`

```csharp
public struct CombatState : IComponentData
{
    public bool IsInCombat;
    public float TimeSinceLastCombatAction;
    public float CombatDropTime; // How long without a hit before dropping combat
    public float CombatExitTime; // When combat was exited
}
```

> **Note:** This is `DIG.Combat.Components.CombatState` - distinct from `Player.Components.CombatState` which tracks kill attribution (LastAttacker, LastAttackTime).

Also includes:
- `EnteredCombatTag` - Enableable tag for reactive systems
- `ExitedCombatTag` - Enableable tag for reactive systems
- `CombatStateSettings` - Per-entity configuration

### 2. `CombatStateAuthoring` ✅
**File:** `Assets/Scripts/Combat/Authoring/CombatStateAuthoring.cs`

MonoBehaviour for baking combat state onto entities:
- `CombatDropTime` - Configurable timeout (default 5s)
- `CanEnterCombat` - Whether entity can enter combat
- `StartInCombat` - For testing/spawned-in-combat scenarios

### 3. `CombatStateSystem` ✅
**File:** `Assets/Scripts/Combat/Systems/CombatStateSystem.cs`

Updates combat state timers and handles exit transitions:
1. Increments `TimeSinceLastCombatAction` each frame
2. Exits combat when timer exceeds `CombatDropTime`
3. Sets `ExitedCombatTag` on state change
4. Clears event tags from previous frame

### 4. Combat Entry via Damage Systems ✅
**Files:**
- `Assets/Scripts/Combat/Systems/SimpleDamageApplySystem.cs`
- `Assets/Scripts/Combat/Systems/CombatStateFromDamageSystem.cs`

Combat state is set directly when damage is applied:
1. **Target enters combat** when receiving damage
2. **Attacker enters combat** when dealing damage
3. Burst-compiled IJobEntity for performance
4. Works for both player→enemy and enemy→player damage

### 5. Health Bar Visibility Integration ✅
**Files Updated:**
- `Assets/Scripts/Combat/Bridges/EnemyHealthBarBridgeSystem.cs`
- `Assets/Scripts/Combat/UI/WorldSpace/EnemyHealthBarPool.cs`

Bridge system reads `CombatState` from ServerWorld and passes `IsInCombat` and `TimeSinceCombatEnded` to the health bar pool, enabling:
- `WhenInCombat` visibility mode
- `WhenInCombatWithTimeout` visibility mode

## Combat State Flow

```
Player attacks enemy (SweptMeleeHitboxSystem / ProjectileSystem)
    ↓
DamageEvent added to target's buffer
    ↓
SimpleDamageApplySystem:
    - Applies damage to Health
    - Sets target.CombatState.IsInCombat = true
    - Sets attacker.CombatState.IsInCombat = true
    ↓
CombatStateSystem:
    - Increments TimeSinceLastCombatAction each frame
    - Exits combat after CombatDropTime (default 5s)
    ↓
EnemyHealthBarBridgeSystem:
    - Reads CombatState from ServerWorld
    - Passes IsInCombat to health bar pool
```

## Integration Points
- **Health Regen:** `AttributeRegenSystem.cs` should check `!CombatState.IsInCombat` before applying regeneration.
- **UI:** Health bar visibility now supports `WhenInCombat` mode ✅
- **Music:** `MusicManager` can listen for `EnteredCombatTag` / `ExitedCombatTag` changes.
- **AI:** AI systems can check `IsInCombat` for alert behavior.

## Verification Plan
1. Add `CombatStateAuthoring` to enemy prefabs ✅ (BoxingJoe has it)
2. Hit an enemy with an attack
3. Observe console log: "Entity X ENTERED COMBAT" (debug builds only)
4. Wait 5 seconds (default `CombatDropTime`)
5. Observe combat state exit in Entity Debugger
6. Test health bar `WhenInCombat` visibility mode

## Files Created/Modified
- ✅ `Assets/Scripts/Combat/Components/CombatStateComponents.cs` (NEW)
- ✅ `Assets/Scripts/Combat/Authoring/CombatStateAuthoring.cs` (NEW)
- ✅ `Assets/Scripts/Combat/Systems/CombatStateSystem.cs` (NEW)
- ✅ `Assets/Scripts/Combat/Systems/SimpleDamageApplySystem.cs` (UPDATED - combat entry on damage)
- ✅ `Assets/Scripts/Combat/Systems/CombatStateFromDamageSystem.cs` (NEW - enemy→player attacks)
- ✅ `Assets/Scripts/Combat/Bridges/EnemyHealthBarBridgeSystem.cs` (UPDATED)
- ✅ `Assets/Scripts/Combat/UI/WorldSpace/EnemyHealthBarPool.cs` (UPDATED)

## Performance Notes
- `SimpleDamageApplySystem` - Burst-compiled `IJobEntity`, single-threaded scheduling
- `CombatStateFromDamageSystem` - Burst-compiled `IJobEntity`
- `CombatStateSystem` - Burst-compiled parallel job
- No Debug.Log calls in production builds
