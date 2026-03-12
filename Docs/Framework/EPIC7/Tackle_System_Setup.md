# Tackle System Setup Guide (Epic 7.4.2)

## Overview
The Tackle system allows players to intentionally tackle other players with a dedicated input. This is a high-risk/high-reward mechanic where the tackler commits to an animation and both players are affected.

## Input Controls

### How to Trigger a Tackle
**Press the G key** while meeting these conditions:
- You must be **sprinting** (hold Shift + move)
- Your speed must be ≥ **5.0 m/s** (minimum tackle speed)
- You must have **≥35 stamina** (tackle cost)
- You must be **grounded** (can't tackle in air)
- **Cooldown must be ready** (3 second cooldown between tackles)

**You do NOT run into another character** - tackle is an active input that commits you to a forward lunge. The collision detection happens automatically in front of you during the tackle animation.

### Input Binding Location
- **Key**: G
- **Defined in**: `PlayerInputSystem.cs` (both new and legacy input systems)
- **Component field**: `input.Tackle` in `PlayerInput` struct

## Tackle Behavior

### What Happens When You Tackle:

1. **Initiation** (TackleSystem):
   - 35 stamina consumed
   - Direction locked to your current velocity
   - Speed boosted by 1.3x multiplier
   - Movement state set to `Tackling`
   - 0.5 second tackle duration begins
   - 3 second cooldown starts

2. **Hit Detection** (TackleCollisionSystem):
   - **Cone-based detection** checks in front of tackler
   - **Hit radius**: 0.6m
   - **Hit distance**: 1.5m forward
   - **Hit angle**: 45° cone

3. **On Successful Hit**:
   - **Target**: Knockdown for **1.5 seconds** (falls down, can't move)
   - **Tackler**: Brief stagger for **0.3 seconds** (recovery time)
   - Animation plays: `TackleHitTrigger`

4. **On Miss** (no target hit):
   - **Tackler**: Longer stagger for **0.6 seconds** (vulnerable punishment)
   - Animation plays: `TackleMissTrigger`

### Power Dynamics
- Tackling has the **highest collision power multiplier** in the game: **2.0**
- Sprinting = 1.5, Running = 1.0, Walking = 0.8, Idle = 0.6
- When a tackler hits another player, the massive power difference almost always causes knockdown

## Animator Setup

### Required Animator Parameters
Add these to your Player Animator Controller:

| Parameter Name | Type | Purpose |
|----------------|------|---------|
| `TackleTrigger` | Trigger | Starts tackle lunge animation |
| `TackleHitTrigger` | Trigger | Successful hit reaction (tackler staggers briefly) |
| `TackleMissTrigger` | Trigger | Whiff/stumble reaction (tackler staggers longer) |
| `IsTackling` | Bool | True during tackle (for blending/transitions) |
| `TackleSpeed` | Float | Tackle speed/intensity 0-1 (for animation speed) |

### Recommended Animator State Setup

```
Locomotion (any state)
    ↓ (TackleTrigger = true)
TackleState
    ├─→ (TackleHitTrigger = true) → TackleHitState → Return to Locomotion
    └─→ (TackleMissTrigger = true) → TackleMissState → Return to Locomotion
```

#### State Details:

1. **TackleState** (Tackle Lunge)
   - Animation: Forward diving lunge (arms extended, body horizontal)
   - Duration: ~0.5 seconds
   - Entry: `TackleTrigger` fires
   - Exit: `TackleHitTrigger` OR `TackleMissTrigger` fires
   - Loop: No
   - `IsTackling` = true during this state

2. **TackleHitState** (Hit Recovery)
   - Animation: Brief recovery/impact reaction (stumble forward slightly)
   - Duration: ~0.3 seconds
   - Entry: `TackleHitTrigger` fires from TackleState
   - Exit: Animation complete → return to Locomotion
   - Loop: No
   - Shows tackler successfully hit target, brief stagger

3. **TackleMissState** (Miss Recovery/Stumble)
   - Animation: Longer stumble/off-balance recovery
   - Duration: ~0.6 seconds
   - Entry: `TackleMissTrigger` fires from TackleState
   - Exit: Animation complete → return to Locomotion
   - Loop: No
   - Shows tackler whiffed and is vulnerable

### Transitions

```csharp
// From Any State → TackleState
Conditions: TackleTrigger = true
Exit Time: false
Transition Duration: 0.1s
Interruption Source: None (can't cancel tackle initiation)

// From TackleState → TackleHitState
Conditions: TackleHitTrigger = true
Exit Time: false
Transition Duration: 0.1s

// From TackleState → TackleMissState
Conditions: TackleMissTrigger = true
Exit Time: false
Transition Duration: 0.1s

// From TackleHitState → Locomotion
Conditions: Exit Time = true (animation complete)
Exit Time: true (1.0)
Transition Duration: 0.2s

// From TackleMissState → Locomotion
Conditions: Exit Time = true (animation complete)
Exit Time: true (1.0)
Transition Duration: 0.2s
```

### Animation Speed Scaling
The `TackleSpeed` parameter (0-1) can be used to:
- Scale animation playback speed based on actual tackle speed
- Add variation to tackle intensity
- Calculated as: `tackleSpeed / 10.0` (clamped 0-1)

### Example Setup in Unity

1. **Open Player Animator Controller** (`Player Animator Controller.controller`)

2. **Add Parameters** (right-click Parameters tab):
   - Add `TackleTrigger` (Trigger)
   - Add `TackleHitTrigger` (Trigger)
   - Add `TackleMissTrigger` (Trigger)
   - Add `IsTackling` (Bool)
   - Add `TackleSpeed` (Float, default 0)

3. **Create States**:
   - Right-click state machine → Create State → TackleState
   - Right-click state machine → Create State → TackleHitState
   - Right-click state machine → Create State → TackleMissState

4. **Assign Animations**:
   - TackleState: Your tackle lunge animation
   - TackleHitState: Brief recovery animation
   - TackleMissState: Longer stumble animation

5. **Create Transitions**:
   - Any State → TackleState (condition: TackleTrigger)
   - TackleState → TackleHitState (condition: TackleHitTrigger)
   - TackleState → TackleMissState (condition: TackleMissTrigger)
   - TackleHitState → Locomotion (exit time: 1.0)
   - TackleMissState → Locomotion (exit time: 1.0)

6. **Configure Transition Settings**:
   - Disable "Can Transition To Self" on all tackle states
   - Set "Interruption Source" to None on Any State → TackleState
   - Enable "Exit Time" only on return transitions to Locomotion

## System Architecture

### ECS Systems (Server + Client)
- **TackleSystem.cs**: Handles input, initiation, direction commitment, timeout
  - Updates in `PredictedFixedStepSimulationSystemGroup`
  - Checks conditions, applies speed boost, manages state
  
- **TackleCollisionSystem.cs**: Hit detection, applies knockdown/stagger
  - Updates after TackleSystem
  - Cone-based collision detection
  - Applies effects to target and tackler

- **TackleSettingsInitSystem.cs**: Bootstraps settings singleton
  - Runs once at world creation

### Animation Systems (Client Only)
- **LocalPlayerTackleAnimationSystem.cs**: Drives local player animations
- **RemotePlayerTackleAnimationSystem.cs**: Drives remote player animations
- **TackleAnimatorBridge.cs**: MonoBehaviour bridge to Animator

### Components
- **TackleState.cs**: Per-player tackle state
  - `TackleTimeRemaining`, `TackleDirection`, `TackleCooldown`
  - `DidHitTarget`, `TackleSpeed`, `HasProcessedHit`
  - All fields replicated via `[GhostField]`

- **TackleSettings.cs**: Singleton configuration
  - All tunable values (speed, duration, cooldown, costs, etc.)

## Tuning Values (TackleSettings)

| Setting | Default | Description |
|---------|---------|-------------|
| `TackleMinSpeed` | 5.0 m/s | Minimum speed to initiate tackle |
| `TackleDuration` | 0.5s | How long tackle lasts |
| `TackleSpeedMultiplier` | 1.3 | Speed boost during tackle |
| `TackleStaminaCost` | 35 | Stamina consumed on tackle |
| `TackleCooldownDuration` | 3.0s | Cooldown between tackles |
| `TackleHitRadius` | 0.6m | Detection radius |
| `TackleHitDistance` | 1.5m | Detection distance forward |
| `TackleHitAngle` | 45° | Detection cone angle |
| `TackleKnockdownDuration` | 1.5s | Target knockdown time |
| `TacklerHitRecoveryDuration` | 0.3s | Tackler stagger on hit |
| `TacklerMissRecoveryDuration` | 0.6s | Tackler stagger on miss |

## Testing Checklist

- [ ] Add Animator parameters to Player Animator Controller
- [ ] Create tackle animation states and transitions
- [ ] Test: Press G while sprinting → tackle initiates
- [ ] Test: Tackle while too slow → nothing happens
- [ ] Test: Tackle without stamina → nothing happens
- [ ] Test: Hit another player → target knocked down 1.5s, you stagger 0.3s
- [ ] Test: Miss → you stagger 0.6s (vulnerable)
- [ ] Test: Tackle during cooldown → nothing happens
- [ ] Test: Tackler has highest collision power (2.0) vs other states
- [ ] Test: Animation triggers fire correctly (TackleHitTrigger vs TackleMissTrigger)
- [ ] Test: Network replication works for remote players

## Troubleshooting

**Tackle not initiating:**
- Check speed ≥ 5.0 m/s (must be sprinting)
- Check stamina ≥ 35
- Check grounded (not in air)
- Check cooldown ready (3s between tackles)

**Animations not playing:**
- Verify Animator parameters exist and match exact names
- Check TackleAnimatorBridge component attached to player
- Ensure LocalPlayerTackleAnimationSystem and RemotePlayerTackleAnimationSystem are running
- Check Unity console for TackleAnimatorBridge debug logs

**Hit detection not working:**
- Check target is within 1.5m forward
- Check target is within 45° cone angle
- Check target is within 0.6m radius
- Verify TackleCollisionSystem is running after TackleSystem

**Network issues:**
- Verify all TackleState fields have `[GhostField]` attribute
- Check TackleSettings singleton exists on both client and server
- Ensure GhostComponent attribute on TackleState

## Files Modified/Created

**Created:**
- `TackleState.cs` - Component
- `TackleSettings.cs` - Singleton
- `TackleSystem.cs` - ECS System
- `TackleCollisionSystem.cs` - ECS System
- `TackleSettingsInitSystem.cs` - Bootstrap
- `TackleAnimatorBridge.cs` - Animation bridge
- `LocalPlayerTackleAnimationSystem.cs` - Animation system
- `RemotePlayerTackleAnimationSystem.cs` - Animation system

**Modified:**
- `PlayerInput_Global.cs` - Added Tackle InputEvent
- `PlayerInputSystem.cs` - Added G key binding
- `PlayerStateSystem.cs` - Added Tackling to non-override states
- `CollisionPowerUtility.cs` - Added Tackling power multiplier
- `PlayerProximityCollisionSystem.cs` - Added Tackling power multiplier
- `PlayerAuthoring.cs` - Added TackleState component
- `DIG_EPICS.md` - Marked Epic 7.4.2 complete
