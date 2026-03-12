# EPIC 15.24 Setup Guide: Surface & Material FX Engine

**Status:** Implemented (Phases 1-12)
**Last Updated:** February 14, 2026
**Requires:** AudioManager, VFXManager, DecalManager in scene. ParadigmStateMachine (EPIC 15.20) for paradigm-adaptive features.

This guide covers the Unity Editor setup for the **Surface & Material FX Engine** — the unified pipeline for bullet impacts, decals, ricochet, footprints, body fall effects, screen dirt, surface-aware audio, paradigm-adaptive scaling, continuous audio loops, ability ground effects, mount surface effects, haptic feedback, and debug tooling.

---

## Overview

The Surface FX Engine consolidates all impact presentation into a single pipeline:

- **Unified Impact Queue** — Every impact source (hitscan, projectile, explosion, footstep, body fall) enqueues events to one queue; one presenter system renders them all
- **Surface-Aware VFX** — Impact particles, decals, and audio change based on the surface material hit
- **Ricochet & Penetration** — Bullets ricochet off hard surfaces at grazing angles and penetrate thin materials
- **Footprint Decals** — Surface-specific footprints with variable fade times (snow lasts 60s, sand 15s)
- **Water Interactions** — Splash VFX and muted audio on liquid surfaces
- **Body Fall Effects** — Dust puff and thud when enemy ragdolls hit the ground
- **Screen Dirt** — Fullscreen dust overlay on nearby large explosions
- **LOD & Budget** — Automatic quality reduction at distance; max 32 events/frame (paradigm-configurable)
- **Audio Variance** — Random pitch, volume variation, and no-repeat clip logic
- **Decal Clustering** — Prevents oversaturation when many bullets hit the same spot
- **Audio Occlusion** — Impacts behind walls play at reduced volume (paradigm-toggleable)
- **Paradigm-Adaptive Scaling** — LOD thresholds, particle/decal/shake scales, and feature toggles auto-adjust per input paradigm (Shooter, ARPG, MOBA, etc.)
- **Continuous Surface Audio** — Looping audio when the player moves on specific surfaces (ice crackle, gravel crunch, water wading)
- **Ability Ground Effects** — Persistent decals and lingering VFX for ability AOE zones (fire scorch, ice patch, poison puddle)
- **Mount Surface Effects** — Tire tracks, hoof prints, skid marks, and surface spray for mounted entities
- **Haptic Feedback** — Per-surface haptic profiles scaled by distance, impact class, and a global motion intensity slider
- **Debug & Profiling** — F9 debug overlay, Editor window, and Unity Profiler markers for the entire pipeline

---

## Quick Start

### Prerequisites

These scene objects should already exist (from earlier EPICs):

| Object | Component | Purpose |
|--------|-----------|---------|
| AudioManager | `AudioManager` | Audio source pooling |
| VFXManager | `VFXManager` | VFX pooling and distance culling |
| DecalManager | `DecalManager` | URP DecalProjector ring-buffer |
| SurfaceManager | `SurfaceManager` | Facade (now routes to unified queue) |
| CameraShakeEffect | `CameraShakeEffect` | On camera — trauma-based screen shake |
| GameplayFeedbackManager | `GameplayFeedbackManager` | FEEL-based haptic feedback |
| ParadigmStateMachine | `ParadigmStateMachine` | Input paradigm state machine (EPIC 15.20) |

### New Setup Required

1. **Tag your SurfaceMaterial assets** with the new fields (see [Surface Material Setup](#surface-material-setup))
2. **(Optional)** Add a **Screen Dirt Overlay** to your UI Canvas (see [Screen Dirt Setup](#screen-dirt-overlay-setup))
3. **(Optional)** Add **ParadigmSurfaceConfig** to the scene (see [Paradigm-Adaptive Setup](#paradigm-adaptive-setup-phase-7))
4. **(Optional)** Add **SurfaceAudioLoopManager** to the scene (see [Continuous Audio Setup](#continuous-surface-audio-setup-phase-8))
5. **(Optional)** Create a **GroundEffectLibrary** for ability ground effects (see [Ability Ground Effects Setup](#ability-ground-effects-setup-phase-9))
6. **(Optional)** Create a **MountSurfaceEffectConfig** for mounted entities (see [Mount Surface Effects Setup](#mount-surface-effects-setup-phase-10))
7. **(Optional)** Add **MotionIntensitySettings** for accessibility/haptic control (see [Haptic & Accessibility Setup](#haptic-feedback--accessibility-setup-phase-11))
8. **(Optional)** Add **SurfaceDebugOverlay** for runtime debugging (see [Debug & Profiling Setup](#debug--profiling-setup-phase-12))
9. **Reimport subscenes** if you modified any prefabs with physics shapes

All ECS systems are auto-created by the world. No authoring components need to be placed in subscenes.

---

## Surface Material Setup

Surface Materials are ScriptableObjects that define how each surface looks, sounds, and behaves when hit.

**Create:** `Assets > Create > DIG > SurfaceMaterial`

### Existing Fields (unchanged)

| Property | Description | Example |
|----------|-------------|---------|
| **Id** | Unique numeric ID for runtime lookup | 0, 1, 2... |
| **Display Name** | Human-readable label | "Concrete", "Metal_Thin" |
| **Walk/Run/Crouch Clips** | Footstep audio per stance | Drag AudioClips |
| **Impact Clips** | Audio played on bullet/explosion impacts | Drag AudioClips |
| **VFX Prefab** | Particle effect spawned on impact | Drag prefab with ParticleSystem |
| **Impact Decal** | DecalData for bullet holes / explosion marks | Drag DecalData SO |
| **Footprint Decal** | DecalData for footprints (optional) | Drag DecalData SO |
| **Allow Footprints** | Whether this surface shows footprint decals | true for Snow/Mud/Sand/Dirt |
| **Footstep Volume** | Audio volume multiplier | 1.0 |

### Surface Identity (Phase 1)

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Surface Id** | Surface type enum — controls ricochet, penetration, and VFX selection | Set explicitly per material |

If left as `Default`, the system uses a **name-based heuristic** (e.g., DisplayName containing "metal" maps to `Metal_Thin`). Setting it explicitly is preferred.

**Available Surface IDs:**

| ID | Use For |
|----|---------|
| Default | Fallback — uses name heuristic |
| Concrete | Sidewalks, bunkers, walls |
| Metal_Thin | Sheet metal, vents, thin panels |
| Metal_Thick | Heavy armor, vault doors, structural beams |
| Wood | Crates, fences, furniture |
| Dirt | Ground, paths |
| Sand | Beaches, deserts |
| Grass | Lawns, fields |
| Gravel | Driveways, rubble |
| Snow | Snow-covered terrain |
| Ice | Frozen surfaces |
| Water | Lakes, puddles, streams |
| Mud | Swamps, wet ground |
| Glass | Windows, bottles |
| Flesh | Living targets (auto-assigned for enemy hits) |
| Armor | Armored enemies, vehicles |
| Fabric | Cloth, tents, curtains |
| Plastic | Containers, equipment |
| Stone | Boulders, ruins, cliffs |
| Ceramic | Tiles, pottery |
| Foliage | Bushes, leaves |
| Bark | Tree trunks |
| Rubber | Tires, mats |
| Energy_Shield | Sci-fi shields, barriers |

### Physical Properties (Phase 3)

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Hardness** | 0-255. Controls ricochet threshold angle. Higher = ricochets at steeper angles | Metal_Thick: 220, Concrete: 180, Wood: 80, Flesh: 20 |
| **Density** | 0-255. Controls penetration resistance. Bullets pass through if velocity > density | Metal_Thick: 240, Concrete: 200, Wood: 100, Glass: 60, Fabric: 30 |
| **Allows Penetration** | Whether bullets can pass through this surface | true for Wood, Glass, Fabric. false for Metal_Thick, Stone |
| **Allows Ricochet** | Whether bullets at shallow angles bounce off | true for Metal, Concrete, Stone. false for Flesh, Fabric, Water |
| **Is Liquid** | Marks this surface as water/lava — changes impact VFX to splashes, mutes audio | true for Water only |

#### Ricochet Behavior

The ricochet angle threshold depends on **Hardness**:

| Hardness | Ricochet Threshold | Effect |
|----------|-------------------|--------|
| 0 (soft) | 75deg | Only extremely grazing shots ricochet |
| 128 (medium) | ~52deg | Moderate angles ricochet |
| 255 (hard) | 30deg | Most angled shots ricochet |

When a ricochet occurs, the system spawns additional spark VFX along the reflection vector.

#### Penetration Behavior

When `Allows Penetration` is true and the bullet's velocity exceeds the surface `Density`:
- **Entry side**: Normal impact VFX + decal (handled by base pipeline)
- **Exit side**: Smaller dust puff VFX at a point 0.15m behind the surface

### Continuous Audio Fields (Phase 8)

| Property | Description | Default |
|----------|-------------|---------|
| **Continuous Loop Clip** | Looping audio clip played while the player moves on this surface (e.g. ice crackle, gravel crunch). Leave empty for no loop. | None |
| **Loop Speed Threshold** | Minimum player speed (m/s) to start the continuous loop | 1.0 |
| **Loop Volume At Max Speed** | Volume when the player is at maximum speed (0-1) | 0.6 |

### Haptic Feedback Fields (Phase 11)

| Property | Description | Default |
|----------|-------------|---------|
| **Haptic Intensity** | Haptic feedback intensity when impacts hit this surface (0=none, 1=max) | 0.5 |
| **Haptic Duration** | Haptic duration in seconds for impacts on this surface | 0.1 |

---

## Surface Material Presets

### Concrete
```
Surface Id: Concrete
Hardness: 180
Density: 200
Allows Penetration: false
Allows Ricochet: true
Is Liquid: false
VFX Prefab: ConcreteImpact (dust cloud + chip debris)
Impact Decal: BulletHole_Concrete
Haptic Intensity: 0.6
```

### Metal (Thin)
```
Surface Id: Metal_Thin
Hardness: 200
Density: 150
Allows Penetration: true
Allows Ricochet: true
Is Liquid: false
VFX Prefab: MetalSpark (spark shower)
Impact Decal: BulletHole_Metal
Haptic Intensity: 0.7
```

### Metal (Thick)
```
Surface Id: Metal_Thick
Hardness: 240
Density: 250
Allows Penetration: false
Allows Ricochet: true
Is Liquid: false
VFX Prefab: MetalSpark (single spark + deep clang)
Impact Decal: BulletHole_Metal
Haptic Intensity: 0.8
```

### Wood
```
Surface Id: Wood
Hardness: 80
Density: 100
Allows Penetration: true
Allows Ricochet: false
Is Liquid: false
VFX Prefab: WoodSplinter (splinter burst)
Impact Decal: BulletHole_Wood
Haptic Intensity: 0.4
```

### Glass
```
Surface Id: Glass
Hardness: 150
Density: 60
Allows Penetration: true
Allows Ricochet: false
Is Liquid: false
VFX Prefab: GlassShatter (shatter + tinkle)
Impact Decal: BulletHole_Glass
Haptic Intensity: 0.5
```

### Water
```
Surface Id: Water
Hardness: 0
Density: 10
Allows Penetration: true
Allows Ricochet: false
Is Liquid: true
Allow Footprints: false
VFX Prefab: WaterSplash (splash column)
Impact Decal: (none)
Continuous Loop Clip: WaterWading_Loop
Loop Speed Threshold: 0.5
Loop Volume At Max Speed: 0.5
Haptic Intensity: 0.2
```

### Snow
```
Surface Id: Snow
Hardness: 10
Density: 20
Allows Penetration: true
Allows Ricochet: false
Is Liquid: false
Allow Footprints: true
Footprint Decal: Footprint_Snow
VFX Prefab: SnowPuff
Continuous Loop Clip: SnowCrunch_Loop
Loop Speed Threshold: 0.8
Loop Volume At Max Speed: 0.4
Haptic Intensity: 0.2
```

### Ice
```
Surface Id: Ice
Hardness: 180
Density: 160
Allows Penetration: false
Allows Ricochet: true
Is Liquid: false
VFX Prefab: IceChip (white spark + chip)
Continuous Loop Clip: IceCrackle_Loop
Loop Speed Threshold: 0.5
Loop Volume At Max Speed: 0.5
Haptic Intensity: 0.5
```

### Gravel
```
Surface Id: Gravel
Hardness: 60
Density: 80
Allows Penetration: false
Allows Ricochet: false
Is Liquid: false
VFX Prefab: GravelDust
Continuous Loop Clip: GravelCrunch_Loop
Loop Speed Threshold: 1.0
Loop Volume At Max Speed: 0.6
Haptic Intensity: 0.3
```

---

## Surface Material Registry

The **SurfaceMaterialRegistry** ScriptableObject holds all surface materials for O(1) runtime lookup.

**Location:** `Resources/SurfaceMaterialRegistry` (must be in a `Resources` folder)

| Property | Description |
|----------|-------------|
| **Default Material** | Fallback material when no surface is detected |
| **Materials** | List of all SurfaceMaterial assets — drag them here |

**Important:** Every SurfaceMaterial you create must be added to this list, and each must have a unique `Id`.

---

## AudioManager — Audio Variance Settings

The AudioManager has inspector fields for impact audio quality:

| Property | Description | Default |
|----------|-------------|---------|
| **Pitch Variance** | Random pitch offset (plus/minus this value around 1.0) applied to impact/footstep sounds | 0.1 |
| **Volume Variance** | Random volume offset (plus/minus this fraction of base volume) | 0.05 |

**No-repeat logic** is automatic — the system avoids playing the same clip twice in a row when multiple clips are available in the material's clip list.

**Tip:** Add 2-3 variations of each impact clip per SurfaceMaterial for the best audio variety.

---

## Screen Dirt Overlay Setup

The screen dirt effect triggers when a large explosion occurs within 5m of the player camera. It shows a fullscreen dust overlay that fades over 2 seconds.

### Setup Steps

1. **Create a UI Canvas** (or use your existing HUD canvas)
2. **Create a child Image** covering the full screen
   - Set the Image to a dust/dirt texture with alpha (e.g., a radial dirt pattern)
   - Stretch anchors to fill the canvas
3. **Add a CanvasGroup** component to the Image
4. **Add the `ScreenDirtOverlay` component** to the Image
5. **Drag the CanvasGroup** into the `Canvas Group` field

```
UI Canvas (Screen Space - Overlay)
 └── ScreenDirtImage
     ├── Image (dirt texture, Raycast Target = false)
     ├── CanvasGroup (alpha starts at 0)
     └── ScreenDirtOverlay (Canvas Group → drag CanvasGroup here)
```

**Tip:** Set the CanvasGroup `Blocks Raycasts` to false and `Interactable` to false so it doesn't intercept clicks.

If the `ScreenDirtOverlay` is not present in the scene, the feature is silently disabled — no errors.

Screen dirt is automatically disabled when the active paradigm profile has `ScreenDirtEnabled = false` (e.g., isometric view modes).

---

## Impact Scaling by Weapon Class

The system automatically scales VFX, decals, and camera shake based on the weapon/impact type. These base values are then multiplied by paradigm profile multipliers (Phase 7).

| Impact Class | Particle Scale | Camera Shake | Typical Source |
|-------------|---------------|--------------|----------------|
| Bullet_Light | 0.5x | None | Pistol, SMG |
| Bullet_Medium | 1.0x | None | Rifle, LMG |
| Bullet_Heavy | 1.5x | Bump (0.1) | Shotgun, Sniper |
| Melee_Light | 0.8x | None | Light melee |
| Melee_Heavy | 1.2x | Bump (0.15) | Heavy melee |
| Explosion_Small | 2.0x | Medium (0.3) | Grenade, small AOE |
| Explosion_Large | 3.0x | Heavy (0.5) + Screen Dirt | RPG, barrel explosion |
| Footstep | 0.3x | None | Player movement |
| BodyFall | 0.6x | Light (0.05) | Enemy ragdoll landing |
| Environmental | 0.5x | None | Ricochet sparks, debris |

Impact class is resolved automatically from weapon category. Designers do not need to configure this.

---

## Effect LOD (Distance-Based Quality)

Effects automatically reduce quality based on distance from the camera. Phase 7 paradigm profiles can multiply these thresholds.

| Distance (Shooter default) | Tier | Particles | Decals | Audio |
|----------|------|-----------|--------|-------|
| 0-15m | Full | 100% emission | Full size | Full volume, 3D spatial |
| 15-40m | Reduced | 50% emission | Full size | Reduced volume if occluded |
| 40-60m | Minimal | Skipped | Skipped | Played |
| 60m+ | Culled | Skipped | Skipped | Skipped |

**Frame budget:** Maximum 32 impact events processed per frame (configurable via paradigm profile). Overflow events queue to the next frame (FIFO).

---

## Decal Clustering

When 3 or more bullet impacts land within 0.2m of each other, subsequent decals in that area are skipped. This prevents visual noise from concentrated automatic fire hitting the same spot.

The system tracks the last 100 decal positions. Oldest entries are evicted first.

---

## Audio Occlusion

For impacts at `Reduced` LOD tier (15-40m), the system performs a line-of-sight raycast from the camera. If the impact is behind a wall, its audio volume is reduced to 30%.

Audio occlusion can be disabled per paradigm via the `AudioOcclusionEnabled` toggle on the paradigm surface profile (useful for top-down and 2D views where occlusion doesn't make sense).

---

## Body Fall Effects

When an NPC dies and enters ragdoll phase, the system automatically enqueues a body fall impact (dust puff + thud audio) at the corpse position. This is a one-shot event per death.

No setup required — works automatically with the existing corpse lifecycle system (EPIC 16.3).

---

## Water Footstep Splashes

When the player walks on a surface marked `Is Liquid = true`, the system enqueues splash VFX events instead of normal footstep particles. Water-specific audio is automatically muted to 50% volume for a "plunk" feel.

**Setup:** Mark your water SurfaceMaterial with `Is Liquid = true`. Assign a splash VFX prefab to the `VFX Prefab` field.

---

## Wind-Affected Particles

Longer-lived impact particles (> 0.5s start lifetime) automatically receive a gentle wind drift force. This affects dust, smoke, and debris but not sparks.

The wind direction is currently a fixed constant `(1, 0, 0.3)`. Future integration with a weather system will make this dynamic.

---

## Paradigm-Adaptive Setup (Phase 7)

The paradigm-adaptive system auto-scales all surface effects based on the active input paradigm. Isometric cameras get larger decals and reduced shake; top-down views disable screen dirt; MOBA disables footprints.

### 7.1 Create Paradigm Surface Profiles

**Create:** `Assets > Create > DIG > Surface > Paradigm Surface Profile`

Create one profile per paradigm you support. Each profile contains:

| Field | Description | Shooter | ARPG | MOBA |
|-------|-------------|---------|------|------|
| **Paradigm** | Which paradigm this profile applies to | Shooter | ARPG | MOBA |
| **LOD Full Multiplier** | Multiplier for full-quality distance (>1 = larger range) | 1.0 | 2.0 | 1.5 |
| **LOD Reduced Multiplier** | Multiplier for reduced-quality distance | 1.0 | 2.0 | 1.5 |
| **LOD Minimal Multiplier** | Multiplier for minimal-quality distance | 1.0 | 2.0 | 1.5 |
| **Particle Scale Multiplier** | All particle effects scaled by this | 1.0 | 1.5 | 1.3 |
| **Decal Scale Multiplier** | All decals scaled by this | 1.0 | 2.0 | 1.5 |
| **Camera Shake Multiplier** | Camera shake intensity scaled by this | 1.0 | 0.2 | 0.1 |
| **Screen Dirt Enabled** | Whether explosion screen dirt overlay is shown | true | false | false |
| **Footprints Enabled** | Whether footprint decals are spawned | true | true | false |
| **Audio Occlusion Enabled** | Whether LOS audio occlusion is used | true | false | false |
| **Audio 3D Blend** | Spatial blend for audio (1=3D, 0=2D) | 1.0 | 0.5 | 0.3 |
| **Max Events Per Frame** | Impact event budget per frame | 32 | 16 | 16 |
| **Distance Culling Multiplier** | Cull distance multiplier (>1 = cull further) | 1.0 | 1.5 | 1.5 |

### 7.2 Add ParadigmSurfaceConfig to Scene

1. Create an **empty GameObject** in the scene (e.g., `SurfaceFXConfig`)
2. Add the **`ParadigmSurfaceConfig`** component
3. Drag your paradigm surface profiles into the **Profiles** array
4. Drag a fallback profile (typically Shooter) into the **Fallback Profile** field

```
SurfaceFXConfig
 └── ParadigmSurfaceConfig
     ├── Profiles: [Shooter_Surface, ARPG_Surface, MOBA_Surface, ...]
     └── Fallback Profile: Shooter_Surface
```

The system auto-subscribes to `ParadigmStateMachine.OnParadigmChanged` and swaps profiles on transition. If `ParadigmSurfaceConfig` is not in the scene, all effects use default (Shooter) values — no errors.

---

## Continuous Surface Audio Setup (Phase 8)

Continuous audio plays a looping clip while the player moves on specific surfaces (ice crackle, gravel crunch, water wading, snow compression). The loop fades in/out with player speed and crossfades when switching surfaces.

### 8.1 Set Per-Material Loop Fields

On each SurfaceMaterial where you want continuous audio, set:

| Field | Example (Ice) | Example (Gravel) |
|-------|--------------|-------------------|
| **Continuous Loop Clip** | IceCrackle_Loop.wav | GravelCrunch_Loop.wav |
| **Loop Speed Threshold** | 0.5 | 1.0 |
| **Loop Volume At Max Speed** | 0.5 | 0.6 |

Leave `Continuous Loop Clip` empty on surfaces that don't need it (Concrete, Metal, etc.).

### 8.2 Create SurfaceAudioLoopConfig

**Create:** `Assets > Create > DIG > Surface > Audio Loop Config`

Place in `Resources/SurfaceAudioLoopConfig` (must be in a Resources folder).

This SO maps SurfaceID to loop settings and controls crossfade timing:

| Field | Description | Default |
|-------|-------------|---------|
| **Entries** | Array of SurfaceID-to-AudioClip mappings (see below) | Empty |
| **Crossfade Duration** | Duration of crossfade when switching surfaces (seconds) | 0.3 |
| **Fade Out Duration** | Duration of fade out when player stops (seconds) | 0.3 |

**Each Entry:**

| Field | Description |
|-------|-------------|
| **Surface** | SurfaceID (Ice, Snow, Gravel, Water, etc.) |
| **Loop Clip** | Looping AudioClip |
| **Speed Threshold** | Minimum player speed to start loop (m/s) |
| **Max Volume** | Volume at max speed (0-1) |
| **Max Speed For Volume** | Speed at which volume reaches MaxVolume (m/s) |

### 8.3 Add SurfaceAudioLoopManager to Scene

1. Create an **empty GameObject** (e.g., `SurfaceAudioLoops`)
2. Add the **`SurfaceAudioLoopManager`** component

The manager creates 4 child AudioSources for pooling internally. No further Inspector configuration needed.

If `SurfaceAudioLoopManager` is not in the scene, continuous loops are silently disabled.

---

## Ability Ground Effects Setup (Phase 9)

Ability ground effects spawn persistent decals and lingering VFX for AOE abilities (fire scorch marks, ice patches, poison puddles). Ability systems enqueue requests to `GroundEffectQueue`, and the system consumes them.

### 9.1 Create GroundEffectLibrary

**Create:** `Assets > Create > DIG > Surface > Ground Effect Library`

Place in `Resources/GroundEffectLibrary` (must be in a Resources folder).

**Each Entry:**

| Field | Description |
|-------|-------------|
| **Effect Type** | GroundEffectType enum (FireScorch, IcePatch, PoisonPuddle, LightningScorch, HolyGlow, ShadowPool, ArcaneBurn) |
| **Decal** | DecalData SO for the ground mark |
| **Lingering VFX Prefab** | Optional particle VFX (fire embers, frost crystals, poison bubbles) |
| **Default Duration** | Decal/VFX lifetime in seconds (if not overridden by ability) |
| **Fade Out Duration** | Additional seconds for visual fade at end of life |
| **Min Radius / Max Radius** | Radius clamp range for the decal/VFX |

### 9.2 How Abilities Enqueue Ground Effects

Ability systems (or any gameplay code) enqueue requests via the static API:

```csharp
GroundEffectQueue.Enqueue(new GroundEffectRequest
{
    EffectType = GroundEffectType.FireScorch,
    Position = abilityTargetPosition,
    Radius = abilityRadius,
    Duration = abilityDuration,
    Intensity = 1f
});
```

The `AbilityGroundEffectSystem` drains this queue each frame and spawns decals/VFX through the existing `DecalManager` and `VFXManager`. No additional scene setup is needed beyond the library asset.

**GroundEffectType to DamageType mapping:**

| DamageType (Targeting) | GroundEffectType |
|------------------------|-----------------|
| Fire | FireScorch |
| Ice | IcePatch |
| Poison | PoisonPuddle |
| Lightning | LightningScorch |
| Holy | HolyGlow |
| Shadow | ShadowPool |
| Arcane | ArcaneBurn |

---

## Mount Surface Effects Setup (Phase 10)

When a player is mounted (vehicle, animal, turret), the system spawns tire tracks/hoof prints, skid marks on sudden deceleration, and surface spray (dust, mud, snow) at speed.

### 10.1 Create MountSurfaceEffectConfig

**Create:** `Assets > Create > DIG > Surface > Mount Surface Effect Config`

Place in `Resources/MountSurfaceEffectConfig` (must be in a Resources folder).

| Field | Description | Default |
|-------|-------------|---------|
| **Track Decal** | DecalData for tire tracks / hoof prints | None |
| **Track Spacing** | Distance between track spawns (meters) | 1.5 |
| **Track Lifetime** | Track decal fade time (seconds) | 30 |
| **Skid Decal** | DecalData for skid marks on hard braking | None |
| **Skid Decel Threshold** | Deceleration (m/s^2) to trigger skid marks | 15 |
| **Spray Speed Threshold** | Min speed (m/s) for surface spray VFX | 5 |
| **Spray Surfaces** | List of SurfaceIDs that produce spray (default: Dirt, Mud, Sand, Gravel, Snow) | Dirt, Mud, Sand, Gravel, Snow |

### 10.2 How It Works

The system queries the local player's `MountState.IsMounted`. When mounted:

- Every `Track Spacing` meters traveled, a track decal is spawned aligned to movement direction
- On sudden deceleration exceeding `Skid Decel Threshold`, a skid mark decal is spawned
- At speeds above `Spray Speed Threshold` on spray-eligible surfaces, a dust/mud/snow VFX is enqueued to the main impact queue behind the mount

If `MountSurfaceEffectConfig` is not found in Resources, mount effects are silently disabled.

---

## Haptic Feedback & Accessibility Setup (Phase 11)

Phase 11 adds per-surface haptic profiles and a global motion intensity slider that scales all VFX, audio, camera shake, and haptics.

### 11.1 Set Per-Material Haptic Fields

On each SurfaceMaterial, configure:

| Field | Recommended Values |
|-------|--------------------|
| **Haptic Intensity** | Metal: 0.7-0.8, Concrete: 0.6, Wood: 0.4, Water: 0.2, Snow: 0.2 |
| **Haptic Duration** | 0.05-0.15s for impacts, 0.02-0.05s for footsteps |

### 11.2 Add MotionIntensitySettings to Scene

1. Create an **empty GameObject** (e.g., `MotionSettings`)
2. Add the **`MotionIntensitySettings`** component

| Field | Description | Default |
|-------|-------------|---------|
| **Global Intensity** | Master intensity slider (0=disabled, 1=normal, 2=exaggerated). Expose this in your settings UI as an accessibility control. | 1.0 |
| **Current Tier** | Auto-detected platform tier (PC/Console/Mobile). On Mobile, LOD tiers are demoted by one level automatically. | PC (auto) |

**Usage in settings UI:** Bind the `GlobalIntensity` property to a slider in your accessibility/video settings menu:

```csharp
MotionIntensitySettings.Instance.GlobalIntensity = slider.value;
```

When `GlobalIntensity` is 0, all impact VFX, shake, and haptics are suppressed. Audio still plays.

If `MotionIntensitySettings` is not in the scene, all effects use intensity 1.0 — no errors.

### 11.3 How Haptics Work

The `SurfaceHapticBridgeSystem` reads recently processed impacts, finds the strongest nearby impact (within 5m of camera), resolves the surface's haptic profile, and calls `GameplayFeedbackManager.Instance.OnDamage()` with the scaled intensity.

Haptic scaling stack: `SurfaceMaterial.HapticIntensity x ImpactClass weight x distance attenuation x MotionIntensitySettings.GlobalIntensity`

---

## Debug & Profiling Setup (Phase 12)

### 12.1 Runtime Debug Overlay

1. Create an **empty GameObject** (e.g., `SurfaceDebug`)
2. Add the **`SurfaceDebugOverlay`** component

| Field | Description | Default |
|-------|-------------|---------|
| **Toggle Key** | Key to show/hide the overlay | F9 |
| **Show On Start** | Whether the overlay is visible at startup | false |
| **Font Size** | Text size | 14 |
| **Background Color** | Overlay background | Black (70% alpha) |

Press **F9** during play mode to toggle the overlay. It displays:

```
--- Surface FX Debug ---
Queue Depth:    5
Events/Frame:   12  (avg: 8.3)
Culled/Frame:   2
VFX Spawned:    8
Decals Spawned: 6
Ricochets:      1
Penetrations:   0
[F9] to toggle
```

Values that approach limits (queue > 24, events > 30) are highlighted yellow.

### 12.2 Editor Debug Window

**Menu: Window > DIG > Surface FX Debug**

Available during play mode. Shows the same stats as the runtime overlay plus a **Clear Impact Queue** button for testing.

### 12.3 Unity Profiler Markers

The following markers appear in the Unity Profiler timeline during play mode:

| Marker | System |
|--------|--------|
| `Surface.ImpactPresenter` | SurfaceImpactPresenterSystem.OnUpdate() |
| `Surface.ProcessImpact` | Per-impact processing (VFX + Decal + Audio + Shake) |
| `Surface.RicochetPenetration` | RicochetPenetrationSystem.OnUpdate() |

Use **Window > Analysis > Profiler** and look for these markers in the CPU timeline to identify surface FX performance costs.

---

## Troubleshooting

| Issue | Check |
|-------|-------|
| No impact VFX on bullet hits | Verify `SurfaceMaterialRegistry` is in `Resources/` folder and has materials listed |
| No decals on impacts | Verify `DecalManager` exists in scene and `Impact Decal` is set on the SurfaceMaterial |
| No impact audio | Verify `AudioManager` exists in scene with `Registry` assigned. Check `Impact Clips` list on the SurfaceMaterial |
| Ricochet never happens | Check `Allows Ricochet = true` and `Hardness` is high enough. Increase hardness for more ricochets |
| Penetration never happens | Check `Allows Penetration = true` and `Density` is lower than bullet velocity |
| No footprint decals | Check `Allow Footprints = true` and `Footprint Decal` is assigned. If using MOBA paradigm, check `FootprintsEnabled` on the paradigm profile |
| Screen dirt doesn't appear | Verify `ScreenDirtOverlay` component on a UI Image with a `CanvasGroup` assigned. Check paradigm profile `ScreenDirtEnabled` |
| Audio sounds identical every time | Add 2+ clips to the `Impact Clips` list. Check `Pitch Variance` > 0 on AudioManager |
| Too many decals overlapping | Working as intended — clustering skips decals after 3 in the same 0.2m area |
| Impact VFX at wrong scale | Check paradigm profile `ParticleScaleMultiplier`. Impact class scaling is automatic |
| Impacts lag at high fire rate | Frame budget caps at 32/frame (configurable via paradigm profile). Check debug overlay for queue depth |
| Paradigm switch doesn't change effects | Verify `ParadigmSurfaceConfig` is in the scene with profiles assigned. Check that profile `Paradigm` field matches the paradigm enum |
| No continuous surface audio | Verify `SurfaceAudioLoopManager` is in scene. Check `SurfaceAudioLoopConfig` is in `Resources/`. Check `Continuous Loop Clip` on SurfaceMaterial |
| Continuous audio too quiet/loud | Adjust `Loop Volume At Max Speed` on SurfaceMaterial and `Max Volume` on SurfaceAudioLoopConfig entry |
| No ability ground effects | Verify `GroundEffectLibrary` is in `Resources/`. Check that ability code calls `GroundEffectQueue.Enqueue()` |
| No mount tracks | Verify `MountSurfaceEffectConfig` is in `Resources/` with `Track Decal` assigned. Player must have `MountState.IsMounted = true` |
| No haptic feedback on impacts | Verify `GameplayFeedbackManager` is in scene. Check `MotionIntensitySettings.GlobalIntensity > 0`. Check `HapticIntensity > 0` on SurfaceMaterial |
| All effects disabled | Check `MotionIntensitySettings.GlobalIntensity` — if set to 0, all effects are suppressed |
| Debug overlay not showing | Press F9 (or configured toggle key). Verify `SurfaceDebugOverlay` component is in the scene |
| Profiler markers not visible | Markers only appear when the systems actually run. Ensure you're in play mode with impacts occurring |

---

## File Reference

### Core Pipeline (Phases 1-6)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Surface/Components/SurfaceComponents.cs` | Core enums: SurfaceID, ImpactClass, EffectLODTier, data structs |
| `Assets/Scripts/Surface/SurfaceImpactQueue.cs` | Static managed queue — central event bus |
| `Assets/Scripts/Surface/Systems/SurfaceImpactPresenterSystem.cs` | Unified presenter — VFX, decals, audio, LOD, clustering, occlusion, wind, paradigm scaling |
| `Assets/Scripts/Surface/Systems/HitscanImpactBridgeSystem.cs` | Bridges hitscan hits from WeaponFireSystem to queue |
| `Assets/Scripts/Surface/Systems/RicochetPenetrationSystem.cs` | Ricochet and penetration VFX logic |
| `Assets/Scripts/Surface/Systems/FootprintDecalSpawnerSystem.cs` | Footprint decals from footstep events |
| `Assets/Scripts/Surface/Systems/WaterInteractionSystem.cs` | Water splash VFX for footsteps on liquid |
| `Assets/Scripts/Surface/Systems/BodyFallImpactSystem.cs` | Dust puff + thud on ragdoll corpses |
| `Assets/Scripts/Surface/Systems/ScreenDirtSystem.cs` | Fullscreen dirt overlay on nearby explosions |
| `Assets/Scripts/Surface/Systems/ImpactClassResolver.cs` | Maps weapon category to impact class |
| `Assets/Scripts/Surface/Systems/SurfaceIdResolver.cs` | Maps SurfaceMaterial to SurfaceID (with name heuristic) |
| `Assets/Scripts/Surface/Data/SurfaceDatabaseBlob.cs` | BlobAsset for Burst-safe surface lookups |
| `Assets/Scripts/Surface/Data/SurfaceDatabaseInitSystem.cs` | Builds BlobAsset at world init |
| `Assets/Scripts/Audio/SurfaceMaterial.cs` | ScriptableObject — surface definition (designer-facing) |
| `Assets/Scripts/Audio/SurfaceMaterialRegistry.cs` | O(1) material lookup by ID |
| `Assets/Scripts/Audio/AudioManager.cs` | Audio pooling with pitch/volume variance |
| `Assets/Scripts/Presentation/DecalManager.cs` | URP DecalProjector ring-buffer |
| `Assets/Scripts/Audio/VFXManager.cs` | VFX pooling with distance culling |
| `Assets/Scripts/Audio/SurfaceManager.cs` | Legacy facade — now routes to unified queue |

### Phase 7: Paradigm-Adaptive

| File | Purpose |
|------|---------|
| `Assets/Scripts/Surface/Config/ParadigmSurfaceProfile.cs` | ScriptableObject — per-paradigm effect multipliers and feature toggles |
| `Assets/Scripts/Surface/Config/ParadigmSurfaceConfig.cs` | MonoBehaviour singleton — caches active profile, subscribes to paradigm changes |

### Phase 8: Continuous Audio

| File | Purpose |
|------|---------|
| `Assets/Scripts/Surface/Audio/SurfaceAudioLoopConfig.cs` | ScriptableObject — SurfaceID-to-loop-clip mapping |
| `Assets/Scripts/Surface/Audio/SurfaceAudioLoopManager.cs` | MonoBehaviour singleton — pool of 4 looping AudioSources with crossfade |
| `Assets/Scripts/Surface/Systems/SurfaceContactAudioSystem.cs` | ECS system — reads player velocity + ground surface, drives loop manager |

### Phase 9: Ability Ground Effects

| File | Purpose |
|------|---------|
| `Assets/Scripts/Surface/Components/GroundEffectData.cs` | GroundEffectType enum, GroundEffectRequest struct, GroundEffectQueue static queue |
| `Assets/Scripts/Surface/Config/GroundEffectLibrary.cs` | ScriptableObject — maps effect type to decal + VFX assets |
| `Assets/Scripts/Surface/Systems/AbilityGroundEffectSystem.cs` | ECS system — drains queue, spawns persistent decals and VFX |

### Phase 10: Mount Surface Effects

| File | Purpose |
|------|---------|
| `Assets/Scripts/Surface/Config/MountSurfaceEffectConfig.cs` | ScriptableObject — track decal, skid settings, spray surfaces |
| `Assets/Scripts/Surface/Systems/MountSurfaceEffectSystem.cs` | ECS system — track marks, skid marks, surface spray for mounted players |

### Phase 11: Haptic Feedback & Accessibility

| File | Purpose |
|------|---------|
| `Assets/Scripts/Core/Settings/MotionIntensitySettings.cs` | MonoBehaviour singleton — global intensity slider, platform tier detection |
| `Assets/Scripts/Surface/Systems/SurfaceHapticBridgeSystem.cs` | ECS system — bridges impacts to GameplayFeedbackManager haptics |

### Phase 12: Debug & Profiling

| File | Purpose |
|------|---------|
| `Assets/Scripts/Surface/Debug/SurfaceFXProfiler.cs` | Static ProfilerMarkers + frame counters |
| `Assets/Scripts/Surface/Debug/SurfaceDebugOverlay.cs` | MonoBehaviour — F9-togglable runtime HUD overlay |
| `Assets/Editor/Surface/SurfaceFXDebugWindow.cs` | EditorWindow — Window > DIG > Surface FX Debug |

---

## Best Practices

1. **Tag every SurfaceMaterial explicitly** — Don't rely on the name heuristic. Set `Surface Id` on each material for deterministic behavior
2. **Add 2-3 clip variations** — Impact Clips, Walk Clips, etc. The no-repeat and pitch variance make even 2 clips sound much more natural
3. **Set physical properties intentionally** — Hardness/Density/AllowsPenetration dramatically affect gameplay feel. Playtest ricochet angles
4. **Assign both VFX Prefab and Impact Decal** — VFX provides the instant feedback; decals provide the persistent evidence
5. **Use Is Liquid sparingly** — Only for actual water/lava. It suppresses decals and changes audio behavior
6. **Test at distance** — Walk 50m+ away from impacts and verify LOD culling works (no visual spam in the distance)
7. **Keep VFX lifetimes reasonable** — Particles > 0.5s get wind drift. Very long-lived particles (5s+) may drift noticeably
8. **Create paradigm profiles early** — Even if you only use Shooter mode, having a profile lets you tune per-paradigm scaling later without code changes
9. **Add continuous loops to standout surfaces** — Ice, Snow, Gravel, and Water benefit most from continuous audio. Don't add loops to Concrete or Metal — they don't need them
10. **Set GlobalIntensity to 0 for motion-sensitive players** — Expose the MotionIntensitySettings slider in your accessibility settings UI
11. **Use the debug overlay during playtesting** — F9 shows queue depth and events/frame. If queue depth stays high, increase `MaxEventsPerFrame` or reduce impact sources
12. **Check Profiler markers for performance** — `Surface.ImpactPresenter` and `Surface.ProcessImpact` show per-frame cost. Target under 0.5ms total

---

## Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Input paradigm framework, paradigm profiles | SETUP_GUIDE_15.20 |
| Procedural motion (weapon sway, landing bob) | SETUP_GUIDE_15.25 |
| Combat resolution, damage formulas | SETUP_GUIDE_15.28 |
| Weapon modifiers, status effects | SETUP_GUIDE_15.29 |
| Enemy ability framework, telegraph zones | SETUP_GUIDE_15.32 |
| Corpse lifecycle, ragdoll, death | SETUP_GUIDE_16.3 |
| **Surface & Material FX Engine** | **This guide (15.24)** |
