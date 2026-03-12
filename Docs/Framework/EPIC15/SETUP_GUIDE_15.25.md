# EPIC 15.25 Setup Guide: Procedural Motion Layer

**Status:** Implemented (Phases 1-6)
**Last Updated:** February 14, 2026
**Requires:** Player prefab with Damageable Authoring, PlayerState, PhysicsVelocity. ParadigmStateMachine (EPIC 15.20) for paradigm-adaptive weights. MotionIntensitySettings (EPIC 15.24) for accessibility slider integration.

This guide covers the Unity Editor setup for the **Procedural Motion Layer** — the universal procedural animation system that provides 6DOF spring physics on the held weapon and camera, auto-adapting per input paradigm.

---

## Overview

The Procedural Motion Layer adds physically-driven animation to the player's weapon and camera:

- **Weapon Sway** — Mouse/look input drags the weapon with smoothed lag
- **Weapon Bob** — Movement-driven Lissajous oscillation (walk, sprint, crouch)
- **Inertia** — Velocity changes produce weapon lag (acceleration/deceleration)
- **Landing Impact** — Fall-to-ground transitions produce a downward weapon kick
- **Idle Noise** — Subtle Perlin breathing/micro-movements at rest
- **Wall Tuck** — Weapon retracts and rotates when near a wall (SphereCast probe)
- **Visual Recoil** — Weapon kick on fire (Z-axis punch + pitch + random roll)
- **Hit Reaction** — Camera flinch on taking damage or landing
- **Analytical Spring Solver** — Frame-rate independent second-order springs (Hz + damping ratio)
- **Per-State Overrides** — Different spring parameters and force scales per movement state (Idle, Walk, Sprint, ADS, Slide, Vault, Swim, Airborne, Crouch, Climb, Staggered)
- **Per-Paradigm Weights** — Weapon motion auto-disables for ARPG/MOBA/TwinStick; camera forces scale down for isometric views
- **Accessibility** — Global intensity slider (shared with EPIC 15.24) scales all procedural motion

---

## Quick Start

### Prerequisites

These scene objects should already exist (from earlier EPICs):

| Object | Component | Purpose |
|--------|-----------|---------|
| Player Prefab | `PlayerState`, `PhysicsVelocity`, `PlayerInput`, `WeaponFireState`, `WeaponAimState` | Required player components (present since EPIC 15.20+) |
| GameplayFeedbackManager | `GameplayFeedbackManager` | Sound bridge triggers weapon foley audio |
| ParadigmStateMachine | `ParadigmStateMachine` | Paradigm weights auto-adapt per input mode |
| MotionSettings | `MotionIntensitySettings` | Shared accessibility slider (EPIC 15.24) |

### New Setup Required

1. **Create a Motion Profile** asset (see [Creating a Motion Profile](#1-creating-a-motion-profile))
2. **Add `ProceduralMotionAuthoring`** to your player prefab in the subscene (see [Player Prefab Setup](#2-player-prefab-setup))
3. **Add `MotionIntensityAuthoring`** to a config GameObject in the subscene (see [Intensity Singleton Setup](#3-intensity-singleton-setup))
4. **(Optional)** Create weapon-class-specific profiles via the preset wizard (see [Preset Wizard](#4-preset-wizard))
5. **(Optional)** Add a `MotionIntensitySlider` to your settings UI (see [Settings UI Setup](#5-settings-ui-setup))
6. **Reimport subscenes** after adding authoring components

All ECS systems are auto-created by the world. No manual system registration is needed.

---

## 1. Creating a Motion Profile

Motion Profiles are ScriptableObjects that define all procedural motion tuning parameters. Each weapon class or character type can have its own profile.

**Create:** `Assets > Create > DIG/Procedural Motion/Motion Profile`

**Recommended location:** `Assets/Resources/MotionProfiles/`

### 1.1 Sway Parameters

Controls how mouse/look input drags the weapon.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Sway Position Scale** | How much look input displaces the weapon position (meters) | 0.02 | 0–0.1 |
| **Sway Rotation Scale** | How much look input rotates the weapon (degrees) | 1.5 | 0–5 |
| **Sway EMA Smoothing** | Exponential moving average factor for input smoothing. Lower = smoother, higher = snappier | 0.15 | 0.05–0.5 |
| **Sway Max Angle** | Maximum sway rotation clamp (degrees) | 5 | 1–15 |

**Tuning tip:** Pistols feel good with higher Sway Rotation Scale (2.0) for snappy responsiveness. LMGs feel better with lower values (0.8) and lower EMA smoothing (0.1) for heavy, lumbering sway.

### 1.2 Bob Parameters

Controls movement-driven weapon oscillation.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Bob Amplitude X** | Horizontal bob distance (meters) | 0.01 | 0–0.05 |
| **Bob Amplitude Y** | Vertical bob distance (meters) | 0.025 | 0–0.08 |
| **Bob Frequency** | Oscillation speed | 1.8 | 0.5–4 |
| **Bob Sprint Multiplier** | Frequency multiplier when sprinting | 1.6 | 1–3 |
| **Bob Rotation Scale** | Roll rotation from bob (degrees) | 0.5 | 0–2 |

**Tuning tip:** Bob is only active when the player is grounded and moving faster than 0.5 m/s. The Idle state override sets BobScale to 0 by default — no bob at rest.

### 1.3 Inertia Parameters

Controls weapon lag from velocity changes (start/stop/strafe).

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Inertia Position Scale** | Strength of position lag | 0.005 | 0–0.02 |
| **Inertia Rotation Scale** | Strength of rotation lag | 0.8 | 0–2 |
| **Inertia Max Force** | Clamp to prevent extreme forces on teleport/respawn | 0.1 | 0.01–0.5 |

### 1.4 Landing Impact Parameters

Controls the downward weapon punch when landing from a fall.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Landing Position Impulse** | Downward position kick strength (meters) | 0.04 | 0–0.1 |
| **Landing Rotation Impulse** | Pitch rotation kick strength (degrees) | 2.0 | 0–5 |
| **Landing Speed Threshold** | Fall speed for maximum impulse normalization (m/s) | 2.0 | 0–10 |
| **Landing Max Impulse** | Absolute clamp on the impulse magnitude | 0.1 | 0–0.2 |

### 1.5 Idle Noise Parameters

Controls subtle breathing/micro-movement when standing still.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Idle Noise Amplitude** | Position noise strength (meters) | 0.001 | 0–0.005 |
| **Idle Noise Frequency** | Perlin noise scroll speed | 0.8 | 0.1–2 |
| **Idle Noise Rotation Scale** | Rotation noise strength | 0.5 | 0–1 |

**Tuning tip:** Idle noise is only active when horizontal speed is below 0.1 m/s. The Walk/Sprint state overrides set IdleNoiseScale to 0 by default.

### 1.6 Wall Probe Parameters

Controls weapon retraction when the player is close to a wall.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Wall Probe Distance** | SphereCast range (meters). Set to 0 to disable wall tuck | 0.8 | 0.3–2 |
| **Wall Tuck Position Z** | How far back the weapon retracts (meters, negative) | -0.15 | -0.3–0 |
| **Wall Tuck Rotation Pitch** | How much the weapon tilts up (degrees, negative) | -15 | -30–0 |
| **Wall Tuck Blend Speed** | Speed of tuck-in/tuck-out interpolation | 8 | 1–20 |
| **Wall Probe Radius** | SphereCast radius (meters) | 0.05 | 0.01–0.2 |

### 1.7 Hit Reaction Parameters

Controls camera and weapon flinch on damage events and landing impacts.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Hit Reaction Position Scale** | Weapon flinch position strength | 0.02 | 0–0.05 |
| **Hit Reaction Rotation Scale** | Weapon flinch rotation strength | 3.0 | 0–5 |
| **Hit Reaction Camera Scale** | Camera spring impulse strength for landing impacts | 0.3 | 0–1 |

### 1.8 Visual Recoil Parameters

Controls the cosmetic weapon kick when firing (separate from functional recoil).

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Visual Recoil Kick Z** | Backward kick distance (meters, negative) | -0.03 | -0.1–0 |
| **Visual Recoil Pitch Up** | Upward rotation kick (degrees) | 2.0 | 0–5 |
| **Visual Recoil Roll Range** | Random roll range per shot (degrees, +/-) | 1.0 | 0–3 |
| **Visual Recoil Position Snap** | Multiplier on kick Z for snappier feel | 5.0 | 1–10 |

**Tuning tip:** For shotguns, increase Kick Z (-0.06) and reduce Roll Range (0.5) for a heavy, predictable slam. For SMGs, use small Kick Z (-0.01) and higher Roll Range (1.5) for rattly spray.

### 1.9 Default Spring Parameters

These define the default spring behavior for the weapon. Individual states can override these.

| Field | Description | Default |
|-------|-------------|---------|
| **Default Position Frequency** | Spring speed (Hz) for position recovery. Higher = snappier | (8, 8, 8) |
| **Default Position Damping Ratio** | Damping for position. <1 = overshoot, 1 = critical, >1 = overdamped | (0.7, 0.7, 0.7) |
| **Default Rotation Frequency** | Spring speed (Hz) for rotation recovery | (8, 8, 8) |
| **Default Rotation Damping Ratio** | Damping for rotation | (0.7, 0.7, 0.7) |

**Spring behavior reference:**

| Damping Ratio | Behavior | Use Case |
|---------------|----------|----------|
| 0.3–0.6 | Underdamped — visible overshoot and oscillation | Bouncy, energetic weapons (staggered state) |
| 0.7 | Slightly underdamped — one small overshoot, fast settle | Default. Good all-rounder |
| 1.0 | Critically damped — fastest return with no overshoot | ADS, precision weapons |
| 1.1–1.5 | Overdamped — slow, heavy return | Crouch, heavy weapons |

**Frequency reference:**

| Hz | Feel |
|----|------|
| 4 | Very slow, heavy, sluggish (swimming, staggered) |
| 8 | Default. Responsive but not twitchy |
| 12 | Snappy, light (pistols) |
| 15 | Very tight, almost instant return (ADS) |

### 1.10 Per-State Overrides

The profile contains an array of 11 per-state overrides (one per `MotionState`). Each override can modify spring parameters and force scales for that movement state.

**States:**

| Index | State | Description |
|-------|-------|-------------|
| 0 | **Idle** | Standing still |
| 1 | **Walk** | Walking or running |
| 2 | **Sprint** | Sprinting |
| 3 | **ADS** | Aiming down sights |
| 4 | **Slide** | Sliding |
| 5 | **Vault** | Vaulting (frozen spring) |
| 6 | **Swim** | Swimming |
| 7 | **Airborne** | Jumping or falling |
| 8 | **Crouch** | Crouched |
| 9 | **Climb** | Climbing (frozen spring) |
| 10 | **Staggered** | Knockdown / stagger |

**Per-state override fields:**

| Field | Description | Default |
|-------|-------------|---------|
| **Position Frequency** | Spring Hz override. (0,0,0) = use profile default | (0,0,0) |
| **Position Damping Ratio** | Damping override. (0,0,0) = use profile default | (0,0,0) |
| **Rotation Frequency** | Rotation spring Hz override | (0,0,0) |
| **Rotation Damping Ratio** | Rotation damping override | (0,0,0) |
| **Frequency Is Multiplier** | If true, frequency values multiply the profile default instead of replacing it | false |
| **Bob Scale** | Multiplier on bob force (0 = no bob) | 1.0 |
| **Sway Scale** | Multiplier on sway force | 1.0 |
| **Inertia Scale** | Multiplier on inertia force | 1.0 |
| **Idle Noise Scale** | Multiplier on idle noise force | 1.0 |
| **Transition Duration** | Blend duration when entering this state (seconds) | 0.15 |
| **Position Offset** | Static weapon position offset (meters) | (0,0,0) |
| **Rotation Offset** | Static weapon rotation offset (degrees) | (0,0,0) |
| **Is Frozen** | If true, spring solver is disabled and weapon holds the static offset | false |

**Default state tuning (pre-configured when you create a profile):**

| State | Bob | Sway | Inertia | Noise | Frozen | Key Overrides |
|-------|-----|------|---------|-------|--------|---------------|
| Idle | 0 | 1.0 | 0.5 | 1.0 | No | No bob at rest, full idle noise |
| Walk | 1.0 | 1.0 | 1.0 | 0 | No | Standard movement |
| Sprint | 1.6 | 0.5 | 1.5 | 0 | No | Freq x1.2, position offset (0,-0.03,0), roll tilt 3deg |
| ADS | 0 | 0.2 | 0.3 | 0.3 | No | Freq 15Hz, damping 1.0 (critical), very tight |
| Slide | 0 | 0.3 | 2.0 | 0 | No | Freq 8Hz, damping 0.6, offset down + 15deg roll |
| Vault | 0 | 0 | 0 | 0 | **Yes** | Weapon held at (-20, 0, 0) rotation |
| Swim | 0.3 | 0.5 | 0.5 | 0.8 | No | Freq 4Hz, damping 0.9 (slow, heavy) |
| Airborne | 0 | 0.8 | 0.5 | 0 | No | Freq x0.8 (loose in air) |
| Crouch | 0.6 | 0.8 | 0.8 | 0.5 | No | Damping 1.1 (overdamped), offset down |
| Climb | 0 | 0 | 0 | 0 | **Yes** | Weapon held at offset (-0.1, 0, -0.15) |
| Staggered | 0 | 0 | 3.0 | 0 | No | Freq 4Hz, damping 0.3 (bouncy), fast transition 0.03s |

### 1.11 Per-Paradigm Weights

The profile contains an array of 6 per-paradigm weight sets. These control how much procedural motion is active per input paradigm.

| Index | Paradigm | FPMotion | Camera | Weapon | HitReact | Bob | Sway |
|-------|----------|----------|--------|--------|----------|-----|------|
| 0 | **Shooter** | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 |
| 1 | **MMO** | 0.6 | 0.7 | 0.5 | 1.0 | 0.8 | 0.7 |
| 2 | **ARPG** | 0.0 | 0.3 | 0.0 | 0.7 | 0.0 | 0.0 |
| 3 | **MOBA** | 0.0 | 0.2 | 0.0 | 0.5 | 0.0 | 0.0 |
| 4 | **TwinStick** | 0.0 | 0.4 | 0.0 | 0.8 | 0.3 | 0.0 |
| 5 | **SideScroller2D** | 0.0 | 0.3 | 0.0 | 0.8 | 0.0 | 0.0 |

When **FPMotionWeight = 0**, the entire weapon force pipeline is skipped (zero cost). Camera forces (landing flinch, hit reaction) still run at their reduced Camera weight. This means:

- **Shooter/MMO**: Full weapon sway, bob, recoil, wall tuck
- **ARPG/MOBA/TwinStick/SideScroller**: No weapon motion at all. Camera still flinches on hits/landings at reduced intensity

### 1.12 Custom Inspector

The Motion Profile inspector includes:

- **Spring Response Preview** — Interactive curve showing spring behavior for a given frequency and damping ratio. Adjust the preview sliders to visualize how springs will feel before entering play mode
- **Per-State Overrides** — Foldable section showing all 11 states with their fields
- **Per-Paradigm Weights** — Foldable section showing all 6 paradigms

---

## 2. Player Prefab Setup

Add procedural motion to your player entity by placing the authoring component on the player prefab root inside the subscene.

### 2.1 Add the Component

1. Select your **player prefab root** in the subscene hierarchy
2. Click **Add Component** > search for **Procedural Motion Authoring**
3. Assign your Motion Profile to the **Profile** field

### 2.2 Inspector Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Profile** | The `ProceduralMotionProfile` ScriptableObject to bake | None (required) |
| **Position Clamp Min** | Weapon position lower bound (meters) | (-0.15, -0.15, -0.2) |
| **Position Clamp Max** | Weapon position upper bound (meters) | (0.15, 0.15, 0.1) |
| **Rotation Clamp Min** | Weapon rotation lower bound (degrees) | (-15, -15, -20) |
| **Rotation Clamp Max** | Weapon rotation upper bound (degrees) | (15, 15, 20) |

**Important:** If the Profile field is left empty, the system will create an empty config with no blob data. All forces will be skipped at runtime (safe, but no visual effect).

### 2.3 What Gets Baked

The baker adds three ECS components to the player entity:

| Component | Purpose |
|-----------|---------|
| **WeaponSpringState** | Runtime spring displacement + velocity (client-only, not ghost-replicated) |
| **ProceduralMotionState** | State machine, cached weights, force tracking values |
| **ProceduralMotionConfig** | BlobAssetReference to the baked profile data |

### 2.4 Hierarchy Example

```
PlayerPrefab (subscene)
 ├── Damageable Authoring          (existing)
 ├── PlayerState                   (existing)
 ├── PhysicsVelocity               (existing, from PhysicsBody)
 ├── WeaponFireState               (existing, from weapon system)
 ├── WeaponAimState                (existing, from weapon system)
 ├── PlayerInput                   (existing, from input system)
 ├── Procedural Motion Authoring   ← NEW
 │   └── Profile: MotionProfile_Rifle
 └── ... (other components)
```

---

## 3. Intensity Singleton Setup

The procedural motion systems read a global intensity singleton for master scaling. This must be baked in a subscene.

### 3.1 Add the Component

1. In your gameplay subscene, create an empty GameObject (e.g., `MotionIntensityConfig`) or use an existing config holder alongside other singletons
2. Click **Add Component** > search for **Motion Intensity Authoring**
3. Adjust defaults if needed

### 3.2 Inspector Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Global Intensity** | Master scale. 0 = all motion disabled, 1 = normal, 2 = exaggerated | 1.0 | 0–2 |
| **Camera Motion Scale** | Multiplier on camera forces (landing flinch, hit reaction) | 1.0 | 0–2 |
| **Weapon Motion Scale** | Multiplier on all weapon forces (sway, bob, recoil, etc.) | 1.0 | 0–2 |

### 3.3 Only One Per World

`ProceduralMotionIntensity` is an ECS singleton. Place the authoring component once per subscene. If multiple exist, only one will be active.

### 3.4 Runtime Modification

The `MotionIntensitySlider` UI component (Phase 5) writes to this singleton at runtime when the player adjusts settings. The ECS singleton value is what all Burst-compiled force systems read each frame.

---

## 4. Preset Wizard

A wizard is provided to quickly generate 7 pre-configured Motion Profile assets matching the EPIC 15.25 weapon class tuning table.

### 4.1 Open the Wizard

**Menu: Window > DIG > Create Motion Profile Presets**

### 4.2 Usage

| Field | Description | Default |
|-------|-------------|---------|
| **Output Folder** | Where preset profiles will be created | `Assets/Resources/MotionProfiles` |

Click **Create All Presets** to generate:

| Preset | Sway Rot | Bob Y | Bob Freq | Inertia | Kick Z | Hz | Damping |
|--------|----------|-------|----------|---------|--------|----|---------|
| **Default** | 1.5 | 0.025 | 1.8 | 0.005 | -0.03 | 8 | 0.7 |
| **Pistol** | 2.0 | 0.02 | 2.0 | 0.003 | -0.02 | 12 | 0.6 |
| **Rifle** | 1.5 | 0.025 | 1.8 | 0.005 | -0.03 | 8 | 0.7 |
| **LMG** | 0.8 | 0.03 | 1.5 | 0.008 | -0.05 | 5 | 0.5 |
| **Shotgun** | 1.8 | 0.025 | 1.8 | 0.004 | -0.06 | 10 | 0.55 |
| **Melee** | 2.5 | 0.035 | 2.2 | 0.006 | 0 | 6 | 0.65 |
| **Bow** | 1.0 | 0.01 | 1.5 | 0.002 | -0.01 | 10 | 0.8 |

Presets include default per-state overrides and per-paradigm weights. After creation, open any preset and customize it in the Inspector.

---

## 5. Settings UI Setup

An optional UI slider component lets the player control motion intensity at runtime.

### 5.1 Add the Component

1. On a UI panel in your settings menu, add the **`MotionIntensitySlider`** component
2. Bind the slider fields in the Inspector

### 5.2 Inspector Fields

| Field | Description | Required |
|-------|-------------|----------|
| **Global Slider** | UnityEngine.UI.Slider for master intensity (0–2) | Yes |
| **Camera Slider** | Slider for camera motion scale (0–2) | Optional |
| **Weapon Slider** | Slider for weapon motion scale (0–2) | Optional |
| **Global Label** | Text label showing current value | Optional |
| **Camera Label** | Text label for camera value | Optional |
| **Weapon Label** | Text label for weapon value | Optional |

### 5.3 Hierarchy Example

```
SettingsPanel (Canvas)
 └── MotionSettingsGroup
     ├── MotionIntensitySlider (component)
     ├── GlobalSlider (UI.Slider, range 0–2, default 1)
     │   └── Label: "Motion: 100%"
     ├── CameraSlider (UI.Slider, range 0–2, default 1)
     │   └── Label: "Camera: 100%"
     └── WeaponSlider (UI.Slider, range 0–2, default 1)
         └── Label: "Weapon: 100%"
```

### 5.4 Dual-Write Behavior

The slider writes to both:
- **ECS singleton** (`ProceduralMotionIntensity`) — read by all Burst force systems each frame
- **Managed MonoBehaviour** (`MotionIntensitySettings` from EPIC 15.24) — read by surface FX, haptics, and other managed systems

This ensures both the procedural motion pipeline (EPIC 15.25) and the surface FX pipeline (EPIC 15.24) respect the same user preference.

---

## 6. Camera Spring Integration

The procedural motion layer upgrades the existing camera spring solver (from the base Opsive framework) with an analytical second-order solver.

### 6.1 What's Automatic

If your camera spring already uses `CameraSpringState` (baked by the existing camera system), the upgrade is automatic:

- When the new analytical fields (`PositionFrequency`, `PositionDampingRatio`, etc.) are zero → the original Opsive spring solver runs (no regression)
- When frequency > 0 on any axis → the analytical solver runs for that spring

### 6.2 Enabling Analytical Springs on Camera

The `ProceduralCameraForceSystem` applies landing impact forces to `CameraSpringState`. To enable the analytical solver for the camera spring:

1. Open the camera authoring component on your player prefab
2. Set `PositionFrequency` to a non-zero value (e.g., `(8, 8, 8)`)
3. Set `PositionDampingRatio` (e.g., `(0.7, 0.7, 0.7)`)
4. Repeat for rotation if desired

If these fields are not exposed in your camera authoring, the camera spring continues using the legacy Opsive solver. Landing impacts still work — they apply velocity to the spring regardless of solver type.

---

## 7. HUD Sway Integration

The diegetic HUD sway system (`HudSwaySystem`) reads `ProceduralMotionState.SmoothedLookDelta` to drive visor/HUD parallax.

### 7.1 What's Automatic

If your player entity has both a `DiegeticHUD` component and `ProceduralMotionState`, the HUD sway activates automatically — the HUD lags opposite to look direction, creating a parallax effect.

Entities without `ProceduralMotionState` fall back to lerping sway to zero (safe default).

---

## 8. System Execution Order

Understanding the system order helps when debugging timing issues.

### PredictedFixedStepSimulationSystemGroup (prediction-safe)

```
WeaponRecoilSystem                (existing — functional recoil)
    ↓
ProceduralCameraForceSystem       (camera landing impact + hit reaction)
    ↓
CameraSpringSolverSystem          (solves camera spring with analytical or Opsive solver)
    ↓
PlayerCameraControlSystem         (existing — applies camera transforms)
```

### PresentationSystemGroup (client-only, visual)

```
ProceduralMotionStateSystem       (maps PlayerState → MotionState, caches paradigm weights)
    ↓
ProceduralWeaponForceSystem       (all 8 weapon forces in single Burst pass)
    ↓
WeaponSpringSolverSystem          (analytical spring solve for weapon)
    ↓
WeaponMotionApplySystem           (wall probe SphereCast, writes offset to weapon model)
    ↓
ProceduralSoundBridgeSystem       (spring velocity → foley audio triggers)
```

---

## 9. Tuning Guide

### Weapon Class Tuning

| Weapon Class | Spring Hz | Damping | Sway | Bob Y | Kick Z | Feel |
|-------------|-----------|---------|------|-------|--------|------|
| Pistol | 12 | 0.6 | High (2.0) | Low (0.02) | Light (-0.02) | Snappy, responsive |
| Rifle | 8 | 0.7 | Medium (1.5) | Medium (0.025) | Medium (-0.03) | Balanced default |
| LMG | 5 | 0.5 | Low (0.8) | High (0.03) | Heavy (-0.05) | Heavy, sluggish |
| Shotgun | 10 | 0.55 | Medium (1.8) | Medium (0.025) | Heavy (-0.06) | Weighty slam |
| Melee | 6 | 0.65 | High (2.5) | High (0.035) | None (0) | Loose, swinging |
| Bow | 10 | 0.8 | Low (1.0) | Low (0.01) | Light (-0.01) | Steady, precise |

### State Transition Timing

| Transition | Duration | Notes |
|-----------|----------|-------|
| Any → Idle | 0.15s | Standard settle |
| Any → Walk | 0.12s | Quick pickup |
| Any → Sprint | 0.10s | Fast transition — weapon lowers |
| Any → ADS | 0.15s | Should match ADS zoom animation |
| Any → Slide | 0.08s | Snappy entry |
| Any → Vault | 0.05s | Near-instant freeze |
| Any → Airborne | 0.05s | Immediate loose feel |
| Any → Staggered | 0.03s | Nearly instant for impact feel |

### Accessibility Tuning

| Player Preference | Global Intensity | Camera Scale | Weapon Scale |
|------------------|-----------------|--------------|--------------|
| Default | 1.0 | 1.0 | 1.0 |
| Reduced motion | 0.5 | 0.3 | 0.5 |
| Motion-sensitive | 0.0 | 0.0 | 0.0 |
| Exaggerated (streamer mode) | 1.5 | 1.5 | 1.5 |

When Global Intensity is 0, all weapon and camera procedural motion is disabled. The weapon stays static.

---

## 10. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Weapon sway | Move mouse left/right in play mode | Weapon visually lags behind camera rotation |
| 3 | Weapon bob | Walk forward | Weapon oscillates up/down and side-to-side |
| 4 | Sprint bob | Sprint | Bob frequency increases, weapon lowers slightly |
| 5 | ADS tighten | Aim down sights | All motion reduces dramatically, spring feels tight |
| 6 | Landing impact | Jump off a ledge | Weapon punches downward on landing, camera dips |
| 7 | Idle noise | Stand still for 5 seconds | Subtle breathing motion visible on weapon |
| 8 | Wall tuck | Walk up to a wall face-first | Weapon retracts and tilts up as you approach |
| 9 | Visual recoil | Fire weapon | Weapon kicks back on each shot with slight roll |
| 10 | Paradigm switch | Switch to ARPG paradigm | All weapon motion stops, camera flinch still works at reduced intensity |
| 11 | Intensity slider | Set Global Intensity to 0 | All procedural motion stops |
| 12 | Spring preview | Open Motion Profile inspector, expand Spring Response Preview | Interactive curve responds to frequency/damping slider changes |
| 13 | Preset wizard | Window > DIG > Create Motion Profile Presets | 7 profile assets created in output folder |
| 14 | No console errors | Play for 60 seconds with various movement | No exceptions or warnings |

---

## 11. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| No weapon motion at all | Profile not assigned on Procedural Motion Authoring | Assign a ProceduralMotionProfile to the Profile field. Reimport subscene |
| No weapon motion, profile assigned | ProceduralMotionIntensity singleton missing | Add MotionIntensityAuthoring to the subscene. Reimport |
| Weapon sway but no bob | Player speed below 0.5 m/s threshold | Ensure player is actually moving (check PhysicsVelocity in Entity Debugger) |
| Bob during idle | BobScale > 0 in Idle state override | Set State Overrides > Idle > BobScale to 0 |
| Weapon feels too stiff | Spring frequency too high or damping ratio >= 1 | Lower Default Position Frequency (try 6-8 Hz). Reduce damping toward 0.6-0.7 |
| Weapon feels too bouncy | Damping ratio too low | Increase damping ratio toward 0.8-1.0 |
| Weapon jitters at rest | Near-zero threshold not triggering | This should not happen — the solver has a built-in micro-jitter stop. Check if two systems are writing to the spring |
| No landing camera dip | CameraSpringState PositionFrequency is zero | ProceduralCameraForceSystem applies velocity impulses regardless of solver type — the Opsive solver should still respond. Check landing detection (player must be transitioning from !IsGrounded to IsGrounded) |
| Wall tuck not working | WallProbeDistance set to 0, or no collision geometry in front of player | Set WallProbeDistance > 0. Ensure walls have colliders |
| No visual recoil on fire | WeaponFireState.IsFiring not true, or TimeSinceLastShot too old | Verify weapon fire system sets IsFiring. Visual recoil triggers when TimeSinceLastShot < deltaTime * 1.5 |
| Paradigm weights not updating | ParadigmStateMachine not in scene, or ParadigmSettings ECS singleton missing | Add ParadigmStateMachine to scene. Verify it syncs to the ParadigmSettings ECS singleton |
| Motion continues in ARPG mode | FPMotionWeight > 0 in the ARPG paradigm weights | Open Motion Profile > Per-Paradigm Weights > ARPG > Set FPMotionWeight to 0 |
| Foley audio not triggering | GameplayFeedbackManager not in scene | Add GameplayFeedbackManager. The sound bridge calls OnFire()/OnHeavyHit() for weapon rattle |
| Spring Preview not showing | Inspector foldout collapsed | Expand "Spring Response Preview" in the Motion Profile inspector |
| Preset wizard creates empty profiles | Output folder doesn't exist | Ensure the folder path is valid (e.g., `Assets/Resources/MotionProfiles`). The wizard creates the last folder if its parent exists |

---

## 12. File Reference

### Components

| File | Purpose |
|------|---------|
| `Assets/Scripts/ProceduralMotion/Components/MotionState.cs` | Enum: 11 motion states |
| `Assets/Scripts/ProceduralMotion/Components/WeaponSpringState.cs` | Client-only weapon spring displacement + velocity |
| `Assets/Scripts/ProceduralMotion/Components/ProceduralMotionState.cs` | State machine, paradigm weights, force tracking |
| `Assets/Scripts/ProceduralMotion/Components/ProceduralMotionConfig.cs` | BlobAssetReference to baked profile |
| `Assets/Scripts/ProceduralMotion/Components/ProceduralMotionIntensity.cs` | ECS singleton for global intensity |

### Data

| File | Purpose |
|------|---------|
| `Assets/Scripts/ProceduralMotion/Data/ProceduralMotionBlob.cs` | Burst-safe BlobAsset with all profile data |
| `Assets/Scripts/ProceduralMotion/Data/ProceduralMotionProfile.cs` | Designer-facing ScriptableObject + BakeToBlob |

### Systems

| File | Purpose |
|------|---------|
| `Assets/Scripts/ProceduralMotion/Systems/ProceduralMotionStateSystem.cs` | Maps PlayerState to MotionState, caches paradigm weights |
| `Assets/Scripts/ProceduralMotion/Systems/ProceduralWeaponForceSystem.cs` | All 8 weapon forces in single Burst pass |
| `Assets/Scripts/ProceduralMotion/Systems/WeaponSpringSolverSystem.cs` | Analytical second-order spring solver (weapon) |
| `Assets/Scripts/ProceduralMotion/Systems/WeaponMotionApplySystem.cs` | Wall probe SphereCast + weapon offset application |
| `Assets/Scripts/ProceduralMotion/Systems/ProceduralCameraForceSystem.cs` | Camera landing impact + hit reaction |
| `Assets/Scripts/ProceduralMotion/Systems/ProceduralSoundBridgeSystem.cs` | Spring velocity to foley audio |
| `Assets/Scripts/Player/Systems/CameraSpringSolverSystem.cs` | Camera spring solver (analytical + Opsive dual-mode) |

### Authoring

| File | Purpose |
|------|---------|
| `Assets/Scripts/ProceduralMotion/Authoring/ProceduralMotionAuthoring.cs` | Baker for player prefab |
| `Assets/Scripts/ProceduralMotion/Authoring/MotionIntensityAuthoring.cs` | Baker for intensity singleton |

### UI

| File | Purpose |
|------|---------|
| `Assets/Scripts/ProceduralMotion/UI/MotionIntensitySlider.cs` | Settings slider binding (dual-write to ECS + managed) |

### Editor

| File | Purpose |
|------|---------|
| `Assets/Scripts/ProceduralMotion/Editor/ProceduralMotionProfileEditor.cs` | Custom inspector with spring preview curve |
| `Assets/Scripts/ProceduralMotion/Editor/ProceduralMotionProfilePresetCreator.cs` | Wizard: Window > DIG > Create Motion Profile Presets |

### Modified (existing)

| File | Change |
|------|--------|
| `Assets/Scripts/Player/Components/CameraSpringState.cs` | Added analytical solver fields (PositionFrequency, PositionDampingRatio, RotationFrequency, RotationDampingRatio) |
| `Assets/Scripts/Visuals/Systems/HudSwaySystem.cs` | Reads SmoothedLookDelta from ProceduralMotionState for HUD parallax |

---

## 13. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Input paradigm framework, paradigm switching | SETUP_GUIDE_15.20 |
| Surface FX, haptics, motion intensity settings | SETUP_GUIDE_15.24 |
| Weapon systems (fire, aim, recoil) | SETUP_GUIDE_15.28 |
| Combat resolution, damage events | SETUP_GUIDE_15.29 |
| Enemy AI brain, abilities | SETUP_GUIDE_15.31 / 15.32 |
| Corpse lifecycle | SETUP_GUIDE_16.3 |
| **Procedural motion layer** | **This guide (15.25)** |
