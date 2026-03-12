# EPIC 15.25: Procedural Motion Layer

**Status:** Planning
**Last Updated:** 2026-02-14
**Priority:** High (Game Feel / Core Engine)
**Dependencies:**
- [x] `DIG.Weapons` — WeaponFireState, WeaponRecoilSystem, WeaponAimState
- [x] `DIG.CameraSystem` — CameraSpringState, CameraSpringSolverSystem, CameraShakeEffect
- [x] `DIG.Core.Input` — InputParadigm, ParadigmSettings (EPIC 15.20)
- [x] `DIG.Player.IK` — FootIKSystem, HandIKSystem, LookAtIKSystem
- [x] `DIG.Animation` — WeaponAnimatorBridge, RootMotionSystem

**Feature:** A universal procedural animation layer that makes held items and camera feel physically grounded across all game genres. Replaces static animation with dynamic 6DOF spring physics that auto-adapts based on the active InputParadigm — full weapon motion for FPS/TPS, disabled for ARPG/MOBA, and scaled for everything in between.

---

## Problem Statement

### Current State

| System | Status | Issue |
|--------|--------|-------|
| Camera recoil | BUILT (`WeaponRecoilSystem`) | Works, but uses Opsive-derived solver that is frame-rate dependent |
| Camera spring | BUILT (`CameraSpringState`) | Stiffness/Damping parameterization is non-intuitive for designers |
| Camera shake | BUILT (`CameraShakeEffect`) | Feature-complete, no changes needed |
| Weapon sway | MISSING | No first-person weapon drag on mouse input |
| Weapon bob | MISSING | No weapon oscillation during movement |
| Weapon inertia | MISSING | No weapon lag on sudden acceleration/deceleration |
| Landing impact | MISSING | No camera/weapon dip on ground contact |
| Idle breathing | MISSING | Weapon is perfectly still when idle (feels like a screenshot) |
| Wall interaction | MISSING | Weapon clips through walls, no tuck/push-back |
| ADS transition | MISSING | No spring-driven transition to aim-down-sights position |
| Motion states | MISSING | No per-state spring tuning (sprint vs crouch vs slide) |
| Paradigm adaptation | MISSING | No automatic force scaling per game genre |
| Visual recoil | MISSING | No weapon-specific kick separate from camera recoil |
| Hit reactions | MISSING | No directional flinch from damage |
| HUD sway | STUB (`HudSwaySystem`) | Lerps to zero, no actual input-based motion |

### Desired State

Every player interaction produces physically-grounded visual feedback through procedural springs. The system is:
- **Genre-aware**: Automatically scales forces per InputParadigm (FPS=full, ARPG=zero weapon motion, MMO=subtle)
- **Designer-friendly**: ScriptableObject profiles with Hz/DampingRatio parameterization and weapon-class presets
- **Performant**: <0.2ms total budget, all force computation in a single Burst pass
- **Frame-rate independent**: Analytical spring solver, no `dt*60` approximations

---

## Architecture Decisions

### Decision 1: Dual Spring Model

| Approach | Pros | Cons |
|----------|------|------|
| **Single spring (camera only)** | Simple, existing | Weapon and camera move identically — no relative motion |
| **Dual spring (camera + weapon)** | Weapon can sway/lag independently of camera | Two springs to solve |
| **N-spring per bone** | Full procedural skeleton | Massive complexity, fights Animator |

**Decision:** Dual spring. Camera spring (`CameraSpringState`, existing) handles camera-level effects (recoil, landing, hit reaction). New `WeaponSpringState` (client-only) handles weapon-level effects (sway, bob, visual recoil, wall tuck). The weapon spring is NOT ghost-replicated — it is purely cosmetic presentation. This respects the MEMORY.md constraint: "NEVER create new IBufferElementData on ghost-replicated entities."

### Decision 2: Analytical Second-Order Spring Solver

| Approach | Pros | Cons |
|----------|------|------|
| **Semi-implicit Euler (current)** | Simple, Opsive-compatible | Frame-rate dependent, `dt*60` hack, can explode at high stiffness |
| **Analytical (exact) solver** | Frame-rate independent by construction, unconditionally stable | More math (exp, sin, cos) |
| **RK4 integrator** | Very accurate | 4x evaluation cost, overkill for springs |

**Decision:** Analytical solver with Frequency (Hz) + DampingRatio (zeta) parameterization. Designers think in "how fast does it settle?" (frequency) and "does it overshoot?" (damping ratio). The solver produces identical results at 30fps and 240fps. Backward-compatible: existing `CameraSpringState` with `PositionFrequency == 0` uses the old Opsive solver.

### Decision 3: Single-Pass Force Computation

| Approach | Pros | Cons |
|----------|------|------|
| **One system per force** | Clean separation | 8+ system scheduling passes, cache thrashing |
| **Single system, all forces** | One pass, data stays hot | Larger function |
| **Job per force, single system** | Parallel force evaluation | Forces write to same spring — can't parallelize |

**Decision:** Single Burst ISystem (`ProceduralWeaponForceSystem`) evaluates all forces in one pass. With only 1 local player entity, parallelism gains nothing — sequential evaluation in one function is fastest. The function early-outs entirely when `FPMotionWeight < 0.001` (ARPG/MOBA).

### Decision 4: ProceduralMotionProfile to BlobAsset

| Approach | Pros | Cons |
|----------|------|------|
| **IComponentData with all fields** | Simple, no blob | Huge component (~400 bytes), not designer-editable |
| **ScriptableObject at runtime** | Designer-friendly | Not Burst-compatible, managed reference |
| **SO baked to BlobAsset** | Designer-friendly + Burst-safe | Blob lifecycle management |

**Decision:** `ProceduralMotionProfile` ScriptableObject for editor/design, baked to `BlobAssetReference<ProceduralMotionBlob>` during subscene baking for Burst-safe runtime access. Follows the existing `SurfaceDatabaseBlob` pattern from EPIC 15.24.

---

## Architecture Overview

### Data Flow

```
                              CAMERA PIPELINE (Predicted, Networked)
                        ┌──────────────────────────────────────────────────────────┐
                        │         PredictedFixedStepSimulationSystemGroup          │
                        │                                                          │
  [WeaponFireState] ──► │ WeaponRecoilSystem ──► CameraSpringState.RotVelocity     │
                        │     (existing)          (camera recoil impulse)           │
                        │                                                          │
  [PlayerState     ] ──►│ ProceduralCameraForceSystem ──► CameraSpringState        │
  [DamageDirection ]    │     (NEW)                        .PosVelocity (landing)  │
                        │                                  .RotVelocity (hit)      │
                        │                                                          │
                        │ CameraSpringSolverSystem ──► CameraSpringState.Value      │
                        │     (MODIFY: +analytical)    (solved offset)              │
                        │                                                          │
                        │ PlayerCameraControlSystem ──► CameraTarget               │
                        │     (existing)                (applies offset to camera)  │
                        └──────────────────────────────────────────────────────────┘

                              WEAPON PIPELINE (Client-Only, Visual)
                        ┌──────────────────────────────────────────────────────────┐
                        │                   PresentationSystemGroup                │
                        │                                                          │
  [PlayerState     ] ──►│ ProceduralMotionStateSystem ──► ProceduralMotionState    │
  [WeaponAimState  ]    │     (NEW: state machine)         .CurrentState           │
  [ParadigmSettings]    │                                  .ParadigmWeights        │
                        │                                                          │
  [LookDelta       ] ──►│ ProceduralWeaponForceSystem ──► WeaponSpringState        │
  [Velocity        ]    │     (NEW: all forces)            .PosVelocity            │
  [MotionState     ]    │     Sway + Bob + Inertia +       .RotVelocity            │
  [ProfileBlob     ]    │     Landing + Noise + Wall +                             │
                        │     VisualRecoil + HitReact                              │
                        │                                                          │
                        │ WeaponSpringSolverSystem ──► WeaponSpringState.Value      │
                        │     (NEW: analytical)         (solved offset)             │
                        │                                                          │
                        │ WeaponMotionApplySystem ──► Weapon GameObject             │
                        │     (NEW: managed bridge)    (local pos + rot offset)     │
                        │                                                          │
                        │ ProceduralSoundBridgeSystem ──► Audio Events             │
                        │     (NEW: velocity to foley)                             │
                        └──────────────────────────────────────────────────────────┘
```

### One-Frame Execution (Shooter Paradigm, Walking, Fires Weapon)

1. **WeaponRecoilSystem** (PredictedFixedStep): Detects fire event, applies pitch+yaw impulse to `CameraSpringState.RotationVelocity`
2. **ProceduralCameraForceSystem** (PredictedFixedStep): No landing this frame. Checks for recent damage, applies directional camera flinch (scaled by paradigm weight 1.0)
3. **CameraSpringSolverSystem** (PredictedFixedStep): Solves camera spring (analytical if frequency>0, else Opsive). Outputs position/rotation values
4. **PlayerCameraControlSystem** (PredictedFixedStep): Reads spring values, applies as additive offset to camera
5. **ProceduralMotionStateSystem** (Presentation): Maps `Walking` to `MotionState.Walk`. Caches paradigm weights (Shooter: FPMotionWeight=1.0)
6. **ProceduralWeaponForceSystem** (Presentation): Single pass: Sway (mouse drag), Bob (Lissajous), Visual Recoil (fire kick), Inertia (near-zero, steady walk)
7. **WeaponSpringSolverSystem** (Presentation): Analytical solve. Outputs weapon position/rotation offsets
8. **WeaponMotionApplySystem** (Presentation): Writes offsets to weapon model root transform
9. **ProceduralSoundBridgeSystem** (Presentation): Spring velocity > rattle threshold, enqueue weapon rattle audio

---

## Component Architecture

### New Components

#### `MotionState` Enum

**File:** `Assets/Scripts/ProceduralMotion/Components/MotionState.cs`

| Value | Name | Description |
|-------|------|-------------|
| 0 | Idle | Standing still, weapon at rest |
| 1 | Walk | Normal movement speed |
| 2 | Sprint | Fast movement, exaggerated bob |
| 3 | ADS | Aim-down-sights, reduced sway, tight spring |
| 4 | Slide | Slide, weapon tilted with body |
| 5 | Vault | Mantle/vault, springs frozen |
| 6 | Swim | Underwater, sluggish damping |
| 7 | Airborne | Jumping/falling, no bob |
| 8 | Crouch | Crouched, reduced bob amplitude |
| 9 | Climb | Climbing, springs frozen |
| 10 | Staggered | Hit stagger, exaggerated flinch |

#### `WeaponSpringState`

**File:** `Assets/Scripts/ProceduralMotion/Components/WeaponSpringState.cs`
**Entity:** Player — **Ghost:** None (purely client-side visual)

| Field | Type | Purpose |
|-------|------|---------|
| `PositionValue` | float3 | Current position displacement (meters) |
| `PositionVelocity` | float3 | Current position velocity (m/s) |
| `RotationValue` | float3 | Current rotation offset (degrees, euler) |
| `RotationVelocity` | float3 | Current angular velocity (deg/s) |
| `PositionFrequency` | float3 | Spring natural frequency per axis (Hz) |
| `PositionDampingRatio` | float3 | 0=undamped, 1=critical, >1=overdamped |
| `RotationFrequency` | float3 | Rotation spring frequency per axis (Hz) |
| `RotationDampingRatio` | float3 | Rotation spring damping ratio |
| `PositionMin` / `PositionMax` | float3 | Position clamp (meters) |
| `RotationMin` / `RotationMax` | float3 | Rotation clamp (degrees) |

#### `ProceduralMotionState`

**File:** `Assets/Scripts/ProceduralMotion/Components/ProceduralMotionState.cs`
**Entity:** Player — **Ghost:** None (client-only tracking)

| Field | Type | Purpose |
|-------|------|---------|
| `CurrentState` | MotionState | Active motion state |
| `PreviousState` | MotionState | Previous state (for blending) |
| `StateBlendT` | float | Transition progress 0 to 1 |
| `StateTransitionSpeed` | float | 1 / transitionDuration |
| `PreviousVelocity` | float3 | Last frame velocity (inertia calc) |
| `SmoothedLookDelta` | float2 | EMA-filtered mouse input (sway) |
| `BobPhase` | float | Lissajous phase accumulator |
| `TimeSinceLanding` | float | Seconds since last ground contact |
| `LandingImpactSpeed` | float | Vertical speed at moment of landing |
| `IdleNoiseTime` | float | Perlin noise time accumulator |
| `WallTuckT` | float | Wall tuck interpolation 0 to 1 |
| `WasGrounded` | bool | Previous frame grounded state |
| `FPMotionWeight` | float | Cached paradigm weight: FP weapon motion |
| `CameraMotionWeight` | float | Cached paradigm weight: camera forces |
| `WeaponMotionWeight` | float | Cached paradigm weight: weapon visual |

#### `ProceduralMotionConfig`

**File:** `Assets/Scripts/ProceduralMotion/Components/ProceduralMotionConfig.cs`
**Entity:** Player — **Ghost:** None

| Field | Type | Purpose |
|-------|------|---------|
| `ProfileBlob` | BlobAssetReference\<ProceduralMotionBlob\> | Baked profile data for Burst access |

#### `MotionIntensitySettings`

**File:** `Assets/Scripts/ProceduralMotion/Components/MotionIntensitySettings.cs`
**Entity:** Singleton (subscene) — **Ghost:** None

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `GlobalIntensity` | float | 1.0 | Master scale (0=disabled, 1=normal, 2=exaggerated) |
| `CameraMotionScale` | float | 1.0 | Camera procedural force scale |
| `WeaponMotionScale` | float | 1.0 | Weapon procedural force scale |

### Modified Components

#### `CameraSpringState` — ADD 4 Fields

**File:** `Assets/Scripts/Player/Components/CameraSpringState.cs`

| New Field | Type | Default | Purpose |
|-----------|------|---------|---------|
| `PositionFrequency` | float3 | 0 | If >0, use analytical solver |
| `PositionDampingRatio` | float3 | 0 | Damping ratio for analytical solver |
| `RotationFrequency` | float3 | 0 | Rotation analytical frequency |
| `RotationDampingRatio` | float3 | 0 | Rotation analytical damping |

New fields have NO `[GhostField]` attribute — not serialized over network. Default to zero, triggering the existing Opsive solver path. Zero regression.

---

## Spring Solver Mathematics

### Current Solver (Opsive-Derived) — Problems

```
force = (0 - value) * stiffness
velocity += force * (dt * 60)           // frame-rate approximation
velocity *= pow(damping, dt * 60)       // frame-rate approximation
value += velocity * (dt * 60)           // frame-rate approximation
```

The `dt * 60` normalization is an approximation that produces different behavior at 30fps vs 240fps. At extreme stiffness values or very low frame rates, the system can become unstable.

### New Analytical Solver — Exact Second-Order System

**Parameters (designer-facing):**
- `f` = Natural frequency in Hz. "How fast does it oscillate?" Higher = snappier.
- `z` = Damping ratio. "Does it overshoot?" 0=no damping, 1=critically damped (fastest settle, no overshoot), >1=overdamped (sluggish).

**Derived constants:**
```
omega   = 2 * PI * f                        // Angular frequency
omega_d = omega * sqrt(1 - z*z)             // Damped frequency (underdamped only)
```

#### Underdamped (z < 1) — Most Common for Game Feel

Weapons typically use z=0.5-0.8. The spring overshoots then settles, creating organic motion.

```
e = exp(-z * omega * dt)
c = cos(omega_d * dt)
s = sin(omega_d * dt)

new_value    = e * (value * c + (velocity + z * omega * value) / omega_d * s)
new_velocity = e * (velocity * (c - z * omega / omega_d * s) - value * omega^2 / omega_d * s)
```

#### Critically Damped (z = 1) — Snappiest Without Overshoot

Used for ADS transitions where overshoot would look wrong.

```
e = exp(-omega * dt)
new_value    = e * (value * (1 + omega * dt) + velocity * dt)
new_velocity = e * (velocity * (1 - omega * dt) - value * omega^2 * dt)
```

#### Overdamped (z > 1) — Sluggish, Heavy Weapons

Used for very heavy weapons (LMG, minigun) where the weapon feels like it's fighting you.

```
s  = sqrt(z*z - 1)
r1 = -omega * (z - s)
r2 = -omega * (z + s)
c1 = (velocity - r2 * value) / (r1 - r2)
c2 = value - c1
new_value    = c1 * exp(r1 * dt) + c2 * exp(r2 * dt)
new_velocity = c1 * r1 * exp(r1 * dt) + c2 * r2 * exp(r2 * dt)
```

#### Frame-Rate Independence

The analytical solution is exact for any `dt`. Whether the game runs at 30fps or 240fps, the spring arrives at the same position at the same wall-clock time. No `dt*60` scaling needed.

#### Designer Tuning Guide

| Weapon Feel | Frequency (Hz) | Damping Ratio | Behavior |
|-------------|----------------|---------------|----------|
| Pistol (snappy) | 12 | 0.6 | Fast settle, slight overshoot |
| Rifle (balanced) | 8 | 0.7 | Medium settle, natural bounce |
| LMG (heavy) | 5 | 0.5 | Slow settle, visible overshoot |
| Shotgun (punchy) | 10 | 0.55 | Fast respond, pronounced kick |
| Melee (weighty) | 6 | 0.65 | Medium-slow, weapon has "mass" |
| Bow (precise) | 10 | 0.8 | Fast settle, minimal overshoot |
| ADS (any weapon) | 15 | 1.0 | Critically damped, zero overshoot |

---

## Force Providers

All forces computed in `ProceduralWeaponForceSystem` in a single Burst pass. Each reads specific input, computes an impulse, and accumulates into the weapon spring velocity.

### 1. Sway (Mouse Input to Weapon Drag)

**Input:** `PlayerInput.LookDelta` (float2)
**Output:** Weapon rotation displacement opposite to mouse direction

```
SmoothedLookDelta = lerp(SmoothedLookDelta, rawLookDelta, EMASmoothing)

weaponSpring.PositionVelocity.x += -SmoothedLookDelta.x * SwayPositionScale
weaponSpring.PositionVelocity.y += -SmoothedLookDelta.y * SwayPositionScale
weaponSpring.RotationVelocity.y += -SmoothedLookDelta.x * SwayRotationScale
weaponSpring.RotationVelocity.x += SmoothedLookDelta.y * SwayRotationScale
```

### 2. Bob (Movement to Weapon Oscillation)

**Input:** Horizontal velocity magnitude
**Output:** Lissajous figure-8 pattern on weapon position

```
BobPhase += speed * BobFrequency * dt

bobX = sin(BobPhase) * BobAmplitudeX * BobScale[state]
bobY = abs(cos(BobPhase)) * BobAmplitudeY * BobScale[state]
bobRoll = sin(BobPhase) * BobRotationScale
```

Bob frequency should match footstep animation cadence. At walk speed (~3 m/s) with BobFrequency=1.8, the weapon bobs ~5.4 times/sec.

### 3. Inertia (Acceleration to Weapon Lag)

**Input:** Velocity change between frames
**Output:** Position impulse opposing acceleration

```
velocityDelta = clamp(velocity - PreviousVelocity, -InertiaMaxForce, +InertiaMaxForce)
weaponSpring.PositionVelocity -= velocityDelta * InertiaPositionScale
```

Sudden stop = weapon swings forward. Sudden start = weapon pulls back. Primary contributor to "weapon weight."

### 4. Landing Impact (Ground Contact to Downward Dip)

**Input:** `PlayerState.IsGrounded` transition (false to true), fall speed
**Output:** Downward position impulse + forward pitch impulse

```
if (!WasGrounded && IsGrounded):
    impulseScale = clamp(fallSpeed / LandingSpeedThreshold, 0, 1)
    weaponSpring.PositionVelocity.y -= LandingPositionImpulse * impulseScale
    weaponSpring.RotationVelocity.x += LandingRotationImpulse * impulseScale
```

Camera landing runs separately in `ProceduralCameraForceSystem` (PredictedFixedStep) and works in ALL paradigms.

### 5. Idle Noise (Breathing / Micro-Movements)

**Input:** Time, player idle (speed < 0.1 m/s)
**Output:** Perlin noise on position and rotation

```
if speed < 0.1:
    IdleNoiseTime += dt * IdleNoiseFrequency
    noiseX = perlin(IdleNoiseTime, 0) * IdleNoiseAmplitude
    noiseY = perlin(IdleNoiseTime, 100) * IdleNoiseAmplitude
```

ADS idle noise reduced to 30% via state override — subtle breathing sway while scoped.

### 6. Wall Probe (Raycast to Weapon Tuck)

**Input:** SphereCast forward from camera
**Output:** Weapon pulls back (Z) and tilts down (pitch)

```
if SphereCast(cameraPos, WallProbeRadius, cameraForward, WallProbeDistance):
    targetTuck = 1.0 - hit.distance / WallProbeDistance
else:
    targetTuck = 0

WallTuckT = lerp(WallTuckT, targetTuck, WallTuckBlendSpeed * dt)
```

Uses managed `UnityEngine.Physics.SphereCast` (PresentationSystemGroup, client-only). 1 cast/frame = negligible cost.

### 7. Visual Recoil (Fire Event to Weapon Kick)

**Input:** `WeaponFireState.IsFiring && TimeSinceLastShot < dt * 1.5`
**Output:** Weapon kicks backward, pitches up, rolls randomly

```
weaponSpring.PositionVelocity.z += VisualRecoilKickZ * VisualRecoilPositionSnap
weaponSpring.RotationVelocity.x += VisualRecoilPitchUp
weaponSpring.RotationVelocity.z += random(-VisualRecoilRollRange, +VisualRecoilRollRange)
```

Separate from camera recoil (`WeaponRecoilSystem`, existing). Visual recoil is 2-3x larger than camera recoil — sells the shot without ruining aim.

### 8. Hit Reaction (Damage Direction to Directional Flinch)

**Input:** Recent damage event with direction vector
**Output:** Camera + weapon spring impulse from damage direction

Camera hit reactions work in ALL paradigms (ARPG=0.7, MOBA=0.5). Weapon hit reactions only apply in Shooter/MMO.

---

## Motion State Machine

### State Mapping

```
PlayerMovementState      + WeaponAimState.IsAiming = MotionState
─────────────────────────  ────────────────────────   ────────────
Idle                     + false                    = Idle
Walking / Running        + false                    = Walk
Sprinting                + any                      = Sprint
any                      + true                     = ADS
Jumping / Falling        + any                      = Airborne
Sliding                  + any                      = Slide
Climbing                 + any                      = Climb
Swimming                 + any                      = Swim
Crouching                + false                    = Crouch
Staggered / Knockdown    + any                      = Staggered
Rolling / Diving         + any                      = Airborne
Vaulting / Mantling      + any                      = Vault
```

### State Transition Rules

| From to To | Duration | Notes |
|------------|----------|-------|
| any to ADS | 0.15s | Fast transition to aim position |
| ADS to Walk/Idle | 0.20s | Slightly slower return |
| Walk to Sprint | 0.10s | Quick ramp-up |
| Sprint to Walk | 0.15s | Gradual slow-down |
| any to Airborne | 0.05s | Near-instant (jump is immediate) |
| Airborne to Idle/Walk | 0.12s | Landing recovery |
| any to Slide | 0.08s | Fast entry |
| any to Staggered | 0.03s | Near-instant (damage is sudden) |
| Staggered to Idle | 0.30s | Slow recovery (sells the hit) |
| any to Swim | 0.20s | Gradual water entry |
| any to Vault | 0.05s | Quick tuck |

### Per-State Spring Overrides

| State | Pos Hz | Pos Zeta | Bob Scale | Sway Scale | Inertia Scale | Noise Scale | Pos Offset | Rot Offset |
|-------|--------|----------|-----------|------------|---------------|-------------|------------|------------|
| Idle | profile | profile | 0.0 | 1.0 | 0.5 | 1.0 | 0,0,0 | 0,0,0 |
| Walk | profile | profile | 1.0 | 1.0 | 1.0 | 0.0 | 0,0,0 | 0,0,0 |
| Sprint | x1.2 | x0.9 | 1.6 | 0.5 | 1.5 | 0.0 | 0,-0.03,0 | 5,0,3 |
| ADS | 15 | 1.0 | 0.0 | 0.2 | 0.3 | 0.3 | ads_offset | ads_rot |
| Slide | 8 | 0.6 | 0.0 | 0.3 | 2.0 | 0.0 | 0,-0.05,0 | 0,0,15 |
| Airborne | x0.8 | profile | 0.0 | 0.8 | 0.5 | 0.0 | 0,0,0 | 0,0,0 |
| Crouch | profile | x1.1 | 0.6 | 0.8 | 0.8 | 0.5 | 0,-0.02,0 | 0,0,0 |
| Staggered | 4 | 0.3 | 0.0 | 0.0 | 3.0 | 0.0 | 0,0,0 | 0,0,0 |
| Vault | frozen | frozen | 0.0 | 0.0 | 0.0 | 0.0 | tuck_pos | tuck_rot |
| Climb | frozen | frozen | 0.0 | 0.0 | 0.0 | 0.0 | climb_pos | climb_rot |

**"frozen"**: Spring solver skipped, weapon holds static offset. Used for vault/climb where weapon is held in a fixed position.

### State Blend Interpolation

During transitions, all parameters are linearly interpolated:

```
t = StateBlendT  // 0 to 1 over transition duration

blendedFreq = lerp(overrides[PreviousState].Frequency, overrides[CurrentState].Frequency, t)
blendedDamp = lerp(overrides[PreviousState].DampingRatio, overrides[CurrentState].DampingRatio, t)
blendedOffset = lerp(overrides[PreviousState].PositionOffset, overrides[CurrentState].PositionOffset, t)
blendedBob = lerp(overrides[PreviousState].BobScale, overrides[CurrentState].BobScale, t)
```

When entering ADS, the weapon smoothly glides to the ADS position offset while the spring tightens (frequency increases, damping ratio approaches 1.0).

---

## Paradigm Integration

### Force Weights Per InputParadigm

| Force | Shooter | MMO | ARPG | MOBA | TwinStick | SideScroller |
|-------|---------|-----|------|------|-----------|--------------|
| **Sway** | 1.0 | 0.7 | 0.0 | 0.0 | 0.0 | 0.0 |
| **Bob** | 1.0 | 0.8 | 0.0 | 0.0 | 0.3 | 0.0 |
| **Inertia** | 1.0 | 0.6 | 0.0 | 0.0 | 0.0 | 0.0 |
| **Landing (camera)** | 1.0 | 0.5 | 0.2 | 0.1 | 0.3 | 0.5 |
| **Idle Noise** | 1.0 | 0.5 | 0.0 | 0.0 | 0.0 | 0.0 |
| **Wall Probe** | 1.0 | 0.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| **Hit React (camera)** | 1.0 | 1.0 | 0.7 | 0.5 | 0.8 | 0.8 |
| **Hit React (weapon)** | 1.0 | 0.5 | 0.0 | 0.0 | 0.0 | 0.0 |
| **Visual Recoil** | 1.0 | 0.5 | 0.0 | 0.0 | 0.0 | 0.0 |
| **Camera Shake** | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 |

**Key rules:**
- **Camera shake** works in ALL paradigms (existing `CameraShakeEffect`, not modified)
- **Camera hit reaction** works in ALL paradigms — ARPG players still feel damage through camera flinch
- **All FP weapon forces** zero out for ARPG/MOBA/TwinStick — no first-person weapon view
- **MMO** gets reduced weights — camera is farther, subtle is better
- **TwinStick** gets slight bob (0.3) — character model subtly bounces

### Runtime Implementation

Weights are stored in `BlobArray<ParadigmMotionWeights>` indexed by `(int)InputParadigm` for O(1) lookup. Cached in `ProceduralMotionState` on paradigm change. `ProceduralWeaponForceSystem` early-outs when `FPMotionWeight < 0.001`.

---

## ScriptableObject: ProceduralMotionProfile

**File:** `Assets/Scripts/ProceduralMotion/Data/ProceduralMotionProfile.cs`
**Menu:** `Create > DIG/Procedural Motion/Motion Profile`

### Fields

| Section | Field | Type | Range | Default (Rifle) |
|---------|-------|------|-------|-----------------|
| **Sway** | Position Scale | float | 0-0.1 | 0.02 |
| | Rotation Scale | float | 0-5 | 1.5 |
| | EMA Smoothing | float | 0.05-0.5 | 0.15 |
| | Max Angle | float | 1-15 | 5.0 |
| **Bob** | Amplitude X | float | 0-0.05 | 0.01 |
| | Amplitude Y | float | 0-0.08 | 0.025 |
| | Frequency | float | 0.5-4 | 1.8 |
| | Sprint Multiplier | float | 1-3 | 1.6 |
| | Rotation Scale | float | 0-2 | 0.5 |
| **Inertia** | Position Scale | float | 0-0.02 | 0.005 |
| | Rotation Scale | float | 0-2 | 0.8 |
| | Max Force | float | 0.01-0.5 | 0.1 |
| **Landing** | Position Impulse | float | 0-0.1 | 0.04 |
| | Rotation Impulse | float | 0-5 | 2.0 |
| | Speed Threshold | float | 0-10 | 2.0 |
| | Max Impulse | float | 0-0.2 | 0.1 |
| **Idle Noise** | Amplitude | float | 0-0.005 | 0.001 |
| | Frequency | float | 0.1-2 | 0.8 |
| | Rotation Scale | float | 0-1 | 0.5 |
| **Wall Probe** | Probe Distance | float | 0.3-2 | 0.8 |
| | Tuck Position Z | float | -0.3-0 | -0.15 |
| | Tuck Rotation Pitch | float | -30-0 | -15 |
| | Blend Speed | float | 1-20 | 8.0 |
| | Probe Radius | float | 0.01-0.2 | 0.05 |
| **Hit Reaction** | Position Scale | float | 0-0.05 | 0.02 |
| | Rotation Scale | float | 0-5 | 3.0 |
| | Camera Scale | float | 0-1 | 0.3 |
| **Visual Recoil** | Kick Z | float | -0.1-0 | -0.03 |
| | Pitch Up | float | 0-5 | 2.0 |
| | Roll Range | float | 0-3 | 1.0 |
| | Position Snap | float | 1-10 | 5.0 |
| **State Overrides** | (per MotionState) | array | — | See state table |
| **Paradigm Weights** | (per InputParadigm) | array | — | See paradigm table |

### Weapon Class Presets

| Preset | Sway Rot | Bob Y | Bob Freq | Inertia | Kick Z | Hz | Zeta | Feel |
|--------|----------|-------|----------|---------|--------|-----|------|------|
| **Pistol** | 2.0 | 0.02 | 2.0 | 0.003 | -0.02 | 12 | 0.6 | Snappy, light |
| **Rifle** | 1.5 | 0.025 | 1.8 | 0.005 | -0.03 | 8 | 0.7 | Balanced |
| **LMG** | 0.8 | 0.03 | 1.5 | 0.008 | -0.05 | 5 | 0.5 | Heavy, sluggish |
| **Shotgun** | 1.8 | 0.025 | 1.8 | 0.004 | -0.06 | 10 | 0.55 | Punchy kick |
| **Melee** | 2.5 | 0.035 | 2.2 | 0.006 | 0 | 6 | 0.65 | Weighty swing |
| **Bow** | 1.0 | 0.01 | 1.5 | 0.002 | -0.01 | 10 | 0.8 | Precise, steady |

---

## BlobAsset Structure

**File:** `Assets/Scripts/ProceduralMotion/Data/ProceduralMotionBlob.cs`

```
ProceduralMotionBlob
├── Sway: SwayPositionScale, SwayRotationScale, SwayEMASmoothing, SwayMaxAngle
├── Bob: BobAmplitudeX, BobAmplitudeY, BobFrequency, BobSprintMultiplier, BobRotationScale
├── Inertia: InertiaPositionScale, InertiaRotationScale, InertiaMaxForce
├── Landing: LandingPositionImpulse, LandingRotationImpulse, LandingSpeedThreshold, LandingMaxImpulse
├── IdleNoise: IdleNoiseAmplitude, IdleNoiseFrequency, IdleNoiseRotationScale
├── WallProbe: WallProbeDistance, WallTuckPositionZ, WallTuckRotationPitch, WallTuckBlendSpeed, WallProbeRadius
├── HitReaction: HitReactionPositionScale, HitReactionRotationScale, HitReactionCameraScale
├── VisualRecoil: VisualRecoilKickZ, VisualRecoilPitchUp, VisualRecoilRollRange, VisualRecoilPositionSnap
├── StateOverrides: BlobArray<MotionStateOverride> (indexed by MotionState byte)
└── ParadigmWeights: BlobArray<ParadigmMotionWeights> (indexed by InputParadigm byte)

MotionStateOverride
├── PositionFrequency, PositionDampingRatio: float3
├── RotationFrequency, RotationDampingRatio: float3
├── BobScale, SwayScale, InertiaScale, IdleNoiseScale: float
├── TransitionDuration: float
├── PositionOffset, RotationOffset: float3  (static offsets, e.g., ADS position)

ParadigmMotionWeights
├── FPMotionWeight, CameraMotionWeight, WeaponMotionWeight: float
├── HitReactionWeight, BobWeight, SwayWeight: float
```

---

## System Architecture — Detailed

### System A: ProceduralMotionStateSystem

| Property | Value |
|----------|-------|
| File | `Assets/Scripts/ProceduralMotion/Systems/ProceduralMotionStateSystem.cs` |
| Type | ISystem (Burst) |
| Group | PresentationSystemGroup |
| Filter | ClientSimulation \| LocalSimulation |
| Purpose | Maps PlayerState to MotionState, handles transitions, caches paradigm weights |

### System B: ProceduralCameraForceSystem

| Property | Value |
|----------|-------|
| File | `Assets/Scripts/ProceduralMotion/Systems/ProceduralCameraForceSystem.cs` |
| Type | ISystem (Burst) |
| Group | PredictedFixedStepSimulationSystemGroup |
| Order | AFTER WeaponRecoilSystem, BEFORE CameraSpringSolverSystem |
| Filter | ClientSimulation \| ServerSimulation |
| Purpose | Landing + hit reaction impulses to CameraSpringState |

Why PredictedFixedStep: Camera spring is ghost-replicated. Forces must be deterministic across prediction ticks.

### System C: ProceduralWeaponForceSystem

| Property | Value |
|----------|-------|
| File | `Assets/Scripts/ProceduralMotion/Systems/ProceduralWeaponForceSystem.cs` |
| Type | ISystem (Burst) |
| Group | PresentationSystemGroup |
| Order | AFTER ProceduralMotionStateSystem |
| Filter | ClientSimulation \| LocalSimulation |
| Purpose | All 8 weapon force providers in single pass |

### System D: WeaponSpringSolverSystem

| Property | Value |
|----------|-------|
| File | `Assets/Scripts/ProceduralMotion/Systems/WeaponSpringSolverSystem.cs` |
| Type | ISystem (Burst) |
| Group | PresentationSystemGroup |
| Order | AFTER ProceduralWeaponForceSystem |
| Filter | ClientSimulation \| LocalSimulation |
| Purpose | Analytical second-order spring solver for WeaponSpringState |

### System E: WeaponMotionApplySystem

| Property | Value |
|----------|-------|
| File | `Assets/Scripts/ProceduralMotion/Systems/WeaponMotionApplySystem.cs` |
| Type | Managed SystemBase (needs GameObject access) |
| Group | PresentationSystemGroup |
| Order | AFTER WeaponSpringSolverSystem, BEFORE HandIKSystem |
| Filter | ClientSimulation \| LocalSimulation |
| Purpose | Writes spring offsets to weapon model root transform |

### System F: ProceduralSoundBridgeSystem

| Property | Value |
|----------|-------|
| File | `Assets/Scripts/ProceduralMotion/Systems/ProceduralSoundBridgeSystem.cs` |
| Type | Managed SystemBase |
| Group | PresentationSystemGroup |
| Order | AFTER WeaponSpringSolverSystem |
| Filter | ClientSimulation \| LocalSimulation |
| Purpose | Spring velocity to weapon foley audio events |

### Modified: CameraSpringSolverSystem

**File:** `Assets/Scripts/Player/Systems/CameraSpringSolverSystem.cs`

Add analytical solver branch: if `frequency > 0`, use analytical. Otherwise, existing Opsive solver. 100% backward-compatible.

### Modified: HudSwaySystem

**File:** `Assets/Scripts/Visuals/Systems/HudSwaySystem.cs`

Replace stub lerp-to-zero with actual sway from `ProceduralMotionState.SmoothedLookDelta`.

---

## Performance Budget

| System | Budget | Burst? | Entity Count | Notes |
|--------|--------|--------|-------------|-------|
| ProceduralMotionStateSystem | 0.02ms | Yes | 1 (local player) | State machine, paradigm lookup |
| ProceduralCameraForceSystem | 0.03ms | Yes | 1 (predicted) | Landing + hit reaction only |
| ProceduralWeaponForceSystem | 0.05ms | Yes | 1 (local player) | All 8 forces, single pass |
| WeaponSpringSolverSystem | 0.01ms | Yes | 1 (local player) | 6 exp + 6 sin/cos |
| WeaponMotionApplySystem | 0.03ms | No | 1 (local player) | 1 GameObject write |
| ProceduralSoundBridgeSystem | 0.02ms | No | 1 (local player) | Velocity threshold |
| CameraSpringSolverSystem (delta) | +0.01ms | Yes | All predicted | +2 branches for analytical |
| **Total** | **0.17ms** | | | Well under 0.5ms target |

### Remote Player LOD

Remote players do NOT run procedural weapon motion. All weapon force systems filter by `GhostOwnerIsLocal`. The `WeaponSpringState` component is only added to the local player via conditional authoring.

---

## Implementation Phases

### Phase 1: Foundation — Spring Core + Weapon Spring

**Goal:** Analytical spring solver and weapon spring component.

- **Task 1.1:** `Assets/Scripts/ProceduralMotion/Components/MotionState.cs` (NEW) — Enum
- **Task 1.2:** `Assets/Scripts/ProceduralMotion/Components/WeaponSpringState.cs` (NEW)
- **Task 1.3:** `Assets/Scripts/ProceduralMotion/Components/ProceduralMotionState.cs` (NEW)
- **Task 1.4:** `Assets/Scripts/ProceduralMotion/Components/ProceduralMotionConfig.cs` (NEW)
- **Task 1.5:** `Assets/Scripts/ProceduralMotion/Components/MotionIntensitySettings.cs` (NEW)
- **Task 1.6:** `Assets/Scripts/ProceduralMotion/Data/ProceduralMotionBlob.cs` (NEW)
- **Task 1.7:** `Assets/Scripts/ProceduralMotion/Systems/WeaponSpringSolverSystem.cs` (NEW) — Analytical solver
- **Task 1.8:** `Assets/Scripts/Player/Components/CameraSpringState.cs` (MODIFY) — Add 4 float3 fields
- **Task 1.9:** `Assets/Scripts/Player/Systems/CameraSpringSolverSystem.cs` (MODIFY) — Add analytical branch
- **Task 1.10:** `Assets/Scripts/ProceduralMotion/Authoring/ProceduralMotionAuthoring.cs` (NEW) — Baker

### Phase 2: State Machine + Profile

**Goal:** Motion states blend spring parameters. Designer-friendly profile SO.

- **Task 2.1:** `Assets/Scripts/ProceduralMotion/Data/ProceduralMotionProfile.cs` (NEW) — ScriptableObject
- **Task 2.2:** `Assets/Scripts/ProceduralMotion/Systems/ProceduralMotionStateSystem.cs` (NEW) — State machine
- **Task 2.3:** `Assets/Resources/MotionProfiles/*.asset` (NEW) — 7 preset profiles
- **Task 2.4:** Implement `BakeToBlob()` in ProceduralMotionProfile

### Phase 3: Weapon Force Providers

**Goal:** All 8 force providers. Weapon model responds to input.

- **Task 3.1:** `Assets/Scripts/ProceduralMotion/Systems/ProceduralWeaponForceSystem.cs` (NEW) — All forces
- **Task 3.2:** `Assets/Scripts/ProceduralMotion/Systems/WeaponMotionApplySystem.cs` (NEW) — Managed bridge

### Phase 4: Camera Integration + Paradigm Adaptation

**Goal:** Camera forces in all paradigms. Genre auto-adaptation.

- **Task 4.1:** `Assets/Scripts/ProceduralMotion/Systems/ProceduralCameraForceSystem.cs` (NEW) — Camera forces
- **Task 4.2:** Add paradigm weight lookup to ProceduralMotionStateSystem
- **Task 4.3:** `Assets/Scripts/Visuals/Systems/HudSwaySystem.cs` (MODIFY) — Wire SmoothedLookDelta

### Phase 5: Physical Interaction + Accessibility

**Goal:** Wall tuck, hit reactions, intensity control.

- **Task 5.1:** Wall probe SphereCast + tuck in ProceduralWeaponForceSystem
- **Task 5.2:** Hit reaction forces (damage direction to spring impulse)
- **Task 5.3:** `Assets/Scripts/ProceduralMotion/Authoring/MotionIntensityAuthoring.cs` (NEW)
- **Task 5.4:** `Assets/Scripts/ProceduralMotion/UI/MotionIntensitySlider.cs` (NEW) — Settings UI

### Phase 6: Sound + Polish + Secondary Motion

**Goal:** Audio integration, attachments, editor tools.

- **Task 6.1:** `Assets/Scripts/ProceduralMotion/Systems/ProceduralSoundBridgeSystem.cs` (NEW) — Foley
- **Task 6.2:** Charm/attachment sub-springs in WeaponMotionApplySystem
- **Task 6.3:** `Assets/Scripts/ProceduralMotion/Editor/ProceduralMotionProfileEditor.cs` (NEW) — Custom inspector
- **Task 6.4:** `Assets/Scripts/ProceduralMotion/Editor/ProceduralMotionProfilePresetCreator.cs` (NEW) — Wizard

---

## File Inventory

### NEW Files (17 source + 7 assets)

```
Assets/Scripts/ProceduralMotion/
├── Components/
│   ├── MotionState.cs
│   ├── WeaponSpringState.cs
│   ├── ProceduralMotionState.cs
│   ├── ProceduralMotionConfig.cs
│   └── MotionIntensitySettings.cs
├── Data/
│   ├── ProceduralMotionBlob.cs
│   └── ProceduralMotionProfile.cs
├── Systems/
│   ├── ProceduralMotionStateSystem.cs
│   ├── ProceduralCameraForceSystem.cs
│   ├── ProceduralWeaponForceSystem.cs
│   ├── WeaponSpringSolverSystem.cs
│   ├── WeaponMotionApplySystem.cs
│   └── ProceduralSoundBridgeSystem.cs
├── Authoring/
│   ├── ProceduralMotionAuthoring.cs
│   └── MotionIntensityAuthoring.cs
├── UI/
│   └── MotionIntensitySlider.cs
└── Editor/
    ├── ProceduralMotionProfileEditor.cs
    └── ProceduralMotionProfilePresetCreator.cs

Assets/Resources/MotionProfiles/
├── MotionProfile_Default.asset
├── MotionProfile_Pistol.asset
├── MotionProfile_Rifle.asset
├── MotionProfile_LMG.asset
├── MotionProfile_Shotgun.asset
├── MotionProfile_Melee.asset
└── MotionProfile_Bow.asset
```

### MODIFIED Files (3)

| File | Change |
|------|--------|
| `Assets/Scripts/Player/Components/CameraSpringState.cs` | +4 float3 fields (no GhostField) |
| `Assets/Scripts/Player/Systems/CameraSpringSolverSystem.cs` | +analytical solver branch |
| `Assets/Scripts/Visuals/Systems/HudSwaySystem.cs` | Wire SmoothedLookDelta |

---

## Verification Checklist

### Phase 1
- [ ] Analytical spring: impulse settles in expected time for f=8Hz, z=0.7
- [ ] Analytical spring: identical behavior at 30fps and 240fps
- [ ] CameraSpringState with Frequency=0 uses old Opsive solver (zero regression)
- [ ] WeaponRecoilSystem camera recoil unchanged

### Phase 2
- [ ] State transitions produce smooth parameter interpolation (no snapping)
- [ ] ADS state: spring tightens, weapon moves to ADS offset
- [ ] Sprint state: bob amplifies, weapon tilts forward
- [ ] Profile presets produce visibly distinct weapon feels

### Phase 3
- [ ] Sway: move mouse left, weapon drags right
- [ ] Bob: walk produces visible oscillation, sprint amplifies
- [ ] Inertia: sudden stop makes weapon swing forward
- [ ] Landing: fall from 3m produces visible downward dip
- [ ] Idle noise: standing still 3s shows subtle breathing motion
- [ ] Visual recoil: fire weapon, weapon kicks back then settles
- [ ] Pistol feels snappy, LMG feels heavy

### Phase 4
- [ ] ARPG paradigm: zero weapon motion, camera hit reaction works
- [ ] MMO paradigm: weapon motion reduces to ~50%
- [ ] Camera landing works in isometric view (subtle)
- [ ] HUD sway responds to mouse look input
- [ ] Paradigm switch mid-gameplay: smooth blend

### Phase 5
- [ ] Walk toward wall: weapon tucks at 0.8m, untucks on retreat
- [ ] Take damage from left: weapon pushes right, camera flinches
- [ ] Intensity slider at 0: zero procedural motion
- [ ] Intensity slider at 2: doubled motion

### Phase 6
- [ ] Quick aim swing produces weapon rattle sound
- [ ] Weapon at rest: no foley audio
- [ ] Charm/keychain wobbles when weapon is kicked
- [ ] Editor graph shows spring response curve

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Adding fields to CameraSpringState breaks ghost serialization | HIGH | New fields have NO [GhostField]. Default zero triggers old solver. Test with network replay. |
| Wall probe raycast performance | LOW | 1 cast/frame, local player only, short distance. Budget: ~0.01ms. |
| Analytical solver exp/sin/cos cost | LOW | 6 calls/frame per spring. Burst SIMD. Under 0.01ms. |
| Weapon offset conflicts with Hand IK | MEDIUM | WeaponMotionApplySystem runs BEFORE HandIKSystem. IK targets in world space, unaffected. |
| BlobAsset lifecycle (leak) | MEDIUM | Dispose via BlobAssetStore in authoring cleanup. |

---

## Best Practices

1. **Tune in Play Mode** — Profile changes apply next frame via blob re-bake (or live SO read during development)
2. **Start with presets** — Use the 7 built-in presets as starting points
3. **Bob frequency must match footstep cadence** — If footsteps at 2.7 steps/sec, BobFrequency ~1.35
4. **ADS should always be critically damped (z=1.0)** — Overshoot during ADS feels broken
5. **Visual recoil should be 2-3x camera recoil** — Sells the shot without ruining aim
6. **Don't fight the Animator** — Procedural motion is ADDITIVE. Reduce visual recoil if animations already have it
7. **Use GlobalIntensity for accessibility** — Slider at 0 disables all motion without affecting gameplay
8. **Wall probe radius should match weapon barrel** — Too large causes false positives near doorways
9. **Fill ALL paradigm weights** — Forgetting ARPG=0 means weapon motion plays in isometric view
10. **Profile changes require subscene reimport** — Blob is baked during subscene baking

---

## Troubleshooting

| Issue | Check |
|-------|-------|
| Weapon doesn't move at all | Verify ProceduralMotionAuthoring on player prefab with valid profile. Check GlobalIntensity > 0. Check paradigm weights. |
| Weapon jitters/vibrates | EMA smoothing too low (< 0.1). Increase SwayEMASmoothing to 0.15-0.3. |
| Weapon explodes on fire | Visual recoil values too high. Reduce VisualRecoilKickZ and PitchUp. Check WeaponSpringSolverSystem is running. |
| Weapon feels identical across types | Profile blob not baked. Verify BakeToBlob() called. Check weapon has correct profile reference. |
| Spring feels different at low FPS | Using old Opsive solver (Frequency=0). Upgrade by setting Frequency > 0. |
| ADS position wrong | Check StateOverrides[ADS].PositionOffset. Measure from weapon model root to sight alignment. |
| Wall tuck in open space | WallProbeRadius too large. Reduce to 0.03-0.05. |
| Camera hit reaction missing in ARPG | Check ParadigmWeights[ARPG].CameraMotionWeight — should be 0.5-0.7. |
| Weapon rattle plays constantly | Rattle threshold too low. Increase threshold. |
| State transition pops | TransitionDuration is 0. Set to 0.1-0.3s. |
| Bob feels like skating | BobFrequency doesn't match footstep timing. Calibrate: BobFrequency = walkSpeed / strideLength. |
| Existing recoil broke after upgrade | CameraSpringState.PositionFrequency should default to 0. If non-zero, analytical solver overrides. Verify defaults. |
