### Epic 1.8: Falling & Landing System
**Priority**: MEDIUM  
**Goal**: Realistic fall damage and recovery animations
**Status**: ✅ COMPLETE

**Tasks**:
- [X] Define `FallState` component:
  - [X] `IsFalling`, `FallStartHeight`, `FallDistance`, `FallSpeed`, `IsInFreeFall`
- [X] Define `FallDamageSettings` component:
  - [X] `SafeFallHeight` (3m), `MaxSafeFallHeight` (6m), `LethalFallHeight` (15m)
  - [X] `DamagePerMeter` (10 HP/m above safe height)
- [X] Define `LandingState` component:
  - [X] `IsRecovering`, `RecoveryDuration`, `RecoveryProgress`
- [X] Create `Player/Systems/FallDetectionSystem.cs`
- [X] Track when player becomes airborne and record start height
- [X] Calculate fall distance continuously
- [X] Create `Player/Systems/FallDamageSystem.cs`
- [X] Calculate damage on landing based on fall distance
- [X] Apply damage to player health
- [X] Trigger landing stun (0.5-2 seconds based on fall height)
- [X] Create `Player/Systems/LandingRecoverySystem.cs`
- [X] Lock player input during recovery
- [X] Play landing animation/camera shake
- [X] Gradually restore control
- [X] Implement soft landing (crouch before landing = 50% damage reduction)
- [X] Implement central adapter system to consume ApplyDamage events and apply them to health/buffers, centralizing damage application.
- [X] Test fall damage feels fair and impactful

- [X] Add `LandingAnimatorBridge` MonoBehaviour to the player prefab:
  - [X] Drive landing-specific animator triggers and optional root-motion-to-MoveRequest translation.
  - [X] Receive `OnLanding` animation events (via `AnimatorEventBridge`) to trigger VFX/audio and notify DOTS systems when precise-frame landing logic is required.
  - [X] Expose designer-mapped parameter names for landing blends and recovery timing.

---

## Implementation Summary

### Files Created/Modified

**Components (ECS)**:
- [FallState.cs](../Assets/Scripts/Player/Components/FallState.cs) - Tracks falling state (`IsFalling`, `FallStartHeight`, `FallDistance`)
- [FallDamageSettings.cs](../Assets/Scripts/Player/Components/FallDamageSettings.cs) - Configurable damage thresholds and camera shake params
- [LandingState.cs](../Assets/Scripts/Player/Components/LandingState.cs) - Recovery state (`IsRecovering`, `RecoveryTimer`)
- [LandingFlag.cs](../Assets/Scripts/Player/Components/LandingFlag.cs) - Non-structural flag for MonoBehaviour adapters
- [LandingEvent.cs](../Assets/Scripts/Player/Events/LandingEvent.cs) - One-shot event emitted on landing

**Systems (ECS/DOTS)**:
- [FallDetectionSystem.cs](../Assets/Scripts/Player/Systems/FallDetectionSystem.cs) - Detects falls, calculates damage, emits `LandingEvent`
- [LandingRecoverySystem.cs](../Assets/Scripts/Player/Systems/LandingRecoverySystem.cs) - Manages recovery timer, locks input
- [LandingEventSystem.cs](../Assets/Scripts/Player/Systems/LandingEventSystem.cs) - Converts `LandingEvent` to `LandingFlag`
- [LandingFlagDecaySystem.cs](../Assets/Scripts/Player/Systems/LandingFlagDecaySystem.cs) - Decays and removes `LandingFlag` over time

**Authoring**:
- [FallDamageAuthoring.cs](../Assets/Scripts/Player/Authoring/FallDamageAuthoring.cs) - Baker for `FallDamageSettings`

**Bridges (MonoBehaviour Presentation)**:
- [LandingAnimatorBridge.cs](../Assets/Scripts/Player/Bridges/LandingAnimatorBridge.cs) - Full-featured landing animation bridge with:
  - Landing intensity parameters
  - Recovery timing and progress tracking
  - Root motion support
  - Animation event receivers
  - UnityEvents for audio/VFX hookup
- [LandingAnimationAdapter.cs](../Assets/Scripts/Player/Adapters/LandingAnimationAdapter.cs) - ECS-to-MonoBehaviour bridge that reads `LandingFlag`

---

## Hookup / User Instructions

### 1. Player Prefab Setup

**Architecture Note**: This project uses a hybrid Ghost/UI prefab pattern:
- **Ghost Prefab** = CharacterController + ECS components (networked, DOTS authoritative)
- **UI Prefab** = Animator + Animation Bridges (client-side presentation)

Add these components to your **UI Prefab** (the GameObject with the Animator):

```
UI Prefab (GameObject with Animator)
├── Animator (required)
├── LandingAnimationAdapter (required - bridges ECS to MonoBehaviour)
├── LandingAnimatorBridge (recommended - full animation control)
└── [Other animation bridges: SlideAnimatorBridge, DodgeRollAnimatorBridge, etc.]

Ghost Prefab (separate - networked entity)
├── CharacterController
├── FallDamageAuthoring (bakes to ECS entity)
└── [Other ECS authoring components]
```

**Important**: Do NOT put animation bridges on the Ghost Prefab. They belong on the UI Prefab where the Animator lives.

### 2. Component Configuration

#### LandingAnimatorBridge (Inspector)

| Field | Description | Default |
|-------|-------------|---------|
| **Animator** | Reference to Animator component | Auto-found |
| **ParamLandingTrigger** | Trigger parameter name for landing | `LandTrigger` |
| **ParamLandingIntensity** | Float (0-1) for landing intensity | `LandIntensity` |
| **ParamIsRecovering** | Bool during recovery | `IsRecovering` |
| **ParamRecoveryProgress** | Float (0-1) recovery progress | `RecoveryProgress` |
| **ApplyRootMotion** | Use animator root motion (see note below) | `false` |
| **RootMotionScale** | Scale factor for root motion | `1.0` |

**Root Motion Note**: For hybrid Ghost/UI setups, leave `ApplyRootMotion = false`. The Ghost prefab (with CharacterController) handles authoritative movement via DOTS. Enabling root motion on the UI prefab would only move the visual representation, causing desync with the ghost.
| **SoftLandingDuration** | Recovery time for light landings | `0.3s` |
| **HardLandingDuration** | Recovery time for hard landings | `1.5s` |
| **HardLandingThreshold** | Intensity threshold (0-1) | `0.5` |

#### FallDamageAuthoring (Inspector)

| Field | Description | Default |
|-------|-------------|---------|
| **SafeFallHeight** | Fall distance with no damage | `3m` |
| **MaxSafeFallHeight** | Fall distance before serious damage | `6m` |
| **LethalFallHeight** | Instant death fall height | `15m` |
| **DamagePerMeter** | HP damage per meter above safe | `10` |
| **ShakeAmplitude** | Camera shake intensity | `0.5` |
| **ShakeFrequency** | Camera shake speed | `20` |
| **ShakeDecay** | Camera shake decay rate | `5` |
| **LandingFlagDuration** | How long flag persists for adapters | `0.5s` |

### 3. Animator Controller Setup

Create these parameters in your **Animator Controller**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `LandTrigger` | Trigger | Fires when landing occurs |
| `LandIntensity` | Float | 0-1 intensity for animation blend |
| `IsRecovering` | Bool | True during landing recovery |
| `RecoveryProgress` | Float | 0-1 progress through recovery |

**Animation State Machine Example**:
```
[Locomotion] --LandTrigger--> [Land_Soft] --RecoveryProgress=1--> [Locomotion]
                         \
                          \--(LandIntensity>0.5)--> [Land_Hard] --> [GetUp] --> [Locomotion]
```

### 4. Animation Events (Optional)

Add these events to your landing animation clips for precise audio/VFX timing:

| Event Name | When to Place | Purpose |
|------------|---------------|---------|
| `OnLandingImpact` | Frame where feet hit ground | Play impact sound/dust VFX |
| `OnRecoveryStep` | Each foot plant during get-up | Play footstep sounds |

### 5. Audio/VFX Hookup via UnityEvents

In **LandingAnimatorBridge** Inspector, wire up these events:

- **OnLanding(float intensity)** → `AudioManager.PlayLandingSound(intensity)`
- **OnRecoveryStart** → `AudioManager.PlayGruntSound()`
- **OnRecoveryComplete** → `AudioManager.PlayReadySound()`
- **OnLandingImpactEvent** → `VFXManager.SpawnDustCloud()`
- **OnRecoveryStepEvent** → `AudioManager.PlayFootstep()`

### 6. Soft Landing (Damage Reduction)

Players who **crouch before landing** receive 50% reduced fall damage:
- Hold crouch input while falling
- System detects `PlayerStance.Crouching` on impact
- Damage automatically halved

### 7. Testing Checklist

- [ ] Fall from 3m → No damage, brief landing animation
- [ ] Fall from 5m → Light damage, soft landing animation
- [ ] Fall from 8m → Medium damage, hard landing animation, 1s+ recovery
- [ ] Fall from 15m+ → Lethal damage (death)
- [ ] Crouch + Fall from 8m → Half damage vs uncrouch
- [ ] Camera shake scales with damage
- [ ] Landing animation triggers correctly
- [ ] Recovery locks movement input
- [ ] Audio/VFX events fire at correct frames

### 8. Code API Reference

```csharp
// Trigger landing manually (e.g., from custom fall system)
var bridge = GetComponent<LandingAnimatorBridge>();
bridge.TriggerLandingWithIntensity(0.7f);

// Check if player is in recovery
if (bridge.IsRecovering)
{
    float progress = bridge.GetRecoveryProgress(); // 0-1
}

// Cancel recovery early (e.g., player takes urgent action)
bridge.CancelRecovery();
```

### 9. Network Considerations

- **FallDetectionSystem** runs in `PredictedFixedStepSimulationSystemGroup` for client prediction
- **LandingState** is authoritative on server; client recovery is cosmetic
- **LandingFlag** is a non-structural component for safe MonoBehaviour reads
- Animation is client-side only; DOTS state is authoritative

---

## Data Flow Diagram

```
[Player Falling]
      │
      ▼
[FallDetectionSystem] ─── detects landing ───▶ [LandingEvent] (one-shot)
      │                                              │
      │                                              ▼
      │                                     [LandingEventSystem]
      │                                              │
      │                                              ▼
      │                                     [LandingFlag] (persists for duration)
      │                                              │
      │                                              ▼
      │                          [LandingAnimationAdapter] (MonoBehaviour Update)
      │                                              │
      │                                              ▼
      │                          [LandingAnimatorBridge.TriggerLandingWithIntensity()]
      │                                              │
      ▼                                              ▼
[LandingState]                              [Animator Parameters]
[LandingRecoverySystem]                     [Recovery Timer]
      │                                     [UnityEvents → Audio/VFX]
      │                                              │
      ▼                                              ▼
[Input Locked]                              [Animation Plays]
[Recovery Timer Ticks]                      [RecoveryProgress Updates]
      │                                              │
      ▼                                              ▼
[Recovery Complete]                         [OnRecoveryComplete Event]
[Input Restored]                            [Animator Returns to Locomotion]
```