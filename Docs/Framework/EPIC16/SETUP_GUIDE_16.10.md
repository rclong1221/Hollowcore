# SETUP GUIDE 16.10: Surface Material Gameplay

**Status:** Implemented
**Last Updated:** February 22, 2026
**Requires:** SurfaceMaterial SOs (existing audio/VFX pipeline), SurfaceMaterialAuthoring on terrain/geometry entities

This guide covers Unity Editor setup for the surface material gameplay layer. After setup, every surface type affects gameplay -- stealth noise, movement speed, friction, slip/ice physics, fall damage, and hazard damage zones. All existing audio/VFX/decal surface pipelines continue working unchanged.

---

## What Changed

Previously, surfaces were cosmetic-only. Walking on ice looked and sounded different (via existing audio/VFX) but played identically to concrete. Lava floors had no damage. Mud did not slow you down. Gravel was as quiet as carpet.

Now:

- **Ground surface detection** on any entity (players, NPCs, enemies) via raycast
- **Movement modifiers** -- speed and friction scale per surface (mud slows, ice speeds up)
- **Slip physics** -- ice/slippery surfaces reduce turning control via momentum blending
- **Stealth noise** -- hard surfaces amplify footstep noise, soft surfaces muffle it
- **Fall damage modifiers** -- sand cushions landings, concrete hurts more
- **Damage zones** -- lava, acid, electrified floors deal DOT with ramp-up and tick intervals
- **Per-feature toggles** -- disable any subsystem independently at runtime

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Surface cache resolution | GroundSurfaceCacheSystem resolves SurfaceMaterialId to SurfaceID + hardness/density/flags on change |
| Movement smoothing | SurfaceMovementModifierSystem lerps between surface values at boundaries (no jarring transitions) |
| Slip momentum blending | SurfaceSlipSystem blends intended velocity with current momentum based on SlipFactor |
| NPC noise modifier | SurfaceStealthModifierSystem writes SurfaceNoiseModifier on NPC entities automatically |
| Hearing integration | HearingDetectionSystem reads SurfaceNoiseModifier to adjust NPC detectability by surface |
| Player noise | StealthSystem reads GroundSurfaceState and applies noise multiplier from config blob |
| Fall damage scaling | FallDetectionSystem applies FallDamageMultiplier from config blob on landing |
| Friction application | PlayerMovementSystem reads FrictionMultiplier from SurfaceMovementModifier |
| Default config | If no SurfaceGameplayConfig.asset exists, SurfaceGameplayConfigSystem builds sensible defaults |
| Graceful fallback | If any component is missing from an entity, all systems fall back to 1.0x multipliers |

---

## 1. Ground Surface Detection

### 1.1 Add to Entity Prefabs

1. Select your player, NPC, or enemy prefab root
2. Click **Add Component** > search for **Ground Surface Detection Authoring**

### 1.2 Inspector Fields

| Field | Type | Default | Range | Description |
|-------|------|---------|-------|-------------|
| **Query Interval** | float | 0.25 | 0--1 | How often to raycast for surface detection (seconds). 0 = every frame |
| **Add Movement Modifier** | bool | true | -- | Adds SurfaceMovementModifier (speed/friction/slip). Required for movement effects |
| **Add Noise Modifier** | bool | false | -- | Adds SurfaceNoiseModifier for NPC hearing detection. Enable on NPCs/enemies, NOT players |

### 1.3 Recommended Settings

| Entity Type | Query Interval | Movement Modifier | Noise Modifier |
|-------------|---------------|-------------------|----------------|
| **Player** | 0 (every frame) | Yes | No (StealthSystem handles player noise) |
| **Combat NPC** | 0.25 | Yes | Yes |
| **Background NPC** | 0.5 | No | No |
| **Enemy (BoxingJoe)** | 0.25 | Yes | Yes |

### 1.4 How Detection Works

- **Players**: Reads ground data from the existing `PlayerGroundCheckSystem` output -- no duplicate raycast
- **NPCs**: Raycast down from entity position, frame-spread across multiple frames to stay under budget
- **Frame spread**: With 200 NPCs at spread factor 4, worst case is ~12 raycasts/frame (~0.04ms)

---

## 2. Surface Gameplay Configuration

### 2.1 Create a Config Asset (Optional)

1. **Right-click** in Project window > **Create** > **DIG** > **Surface** > **Surface Gameplay Config**
2. Name it `SurfaceGameplayConfig`
3. Place in `Assets/Resources/` (must be in Resources for runtime loading)

If no asset exists in Resources, the system builds defaults automatically (see table below).

### 2.2 Config Entry Fields

Add one entry per surface type you want to tune:

| Field | Type | Range | Description |
|-------|------|-------|-------------|
| **Surface Id** | SurfaceID | -- | Which surface this entry configures |
| **Noise Multiplier** | float | 0--3 | Footstep noise. >1 = louder (hard surfaces). <1 = quieter (soft) |
| **Speed Multiplier** | float | 0.1--2 | Movement speed. <1 = slower (mud). >1 = faster (smooth) |
| **Slip Factor** | float | 0--1 | 0 = full control. 0.8 = ice. 1 = no control (not recommended) |
| **Fall Damage Multiplier** | float | 0--3 | <1 = soft landing (sand). >1 = hard landing (concrete) |
| **Damage Per Second** | float | -- | DOT inside SurfaceDamageZones. 0 = no damage |
| **Damage Type** | DamageType | -- | Physical, Heat, Radiation, Toxic, etc. |

### 2.3 Built-in Default Values

These values are used when no `SurfaceGameplayConfig.asset` is found in Resources:

| SurfaceID | Noise | Speed | Slip | Fall Dmg | Effect |
|-----------|-------|-------|------|----------|--------|
| **Concrete** | 1.3 | 1.0 | 0.0 | 1.2 | Loud, hard landing |
| **Metal_Thin** | 1.5 | 1.0 | 0.0 | 1.3 | Very loud, hard |
| **Metal_Thick** | 1.5 | 1.0 | 0.0 | 1.3 | Very loud, hard |
| **Wood** | 1.1 | 1.0 | 0.0 | 1.0 | Slightly loud, normal |
| **Dirt** | 0.7 | 0.9 | 0.0 | 0.7 | Quiet, slightly slow |
| **Sand** | 0.5 | 0.7 | 0.0 | 0.5 | Quiet, slow, soft landing |
| **Grass** | 0.6 | 0.95 | 0.0 | 0.6 | Quiet, soft landing |
| **Gravel** | 1.4 | 0.85 | 0.0 | 0.9 | Loud, crunchy, slightly slow |
| **Snow** | 0.4 | 0.8 | 0.15 | 0.4 | Very quiet, slow, slight slip |
| **Ice** | 0.3 | 1.1 | 0.8 | 1.4 | Silent, fast, very slippery, brutal landing |
| **Water** | 0.3 | 0.6 | 0.0 | 0.3 | Silent, very slow, cushioned |
| **Mud** | 0.5 | 0.5 | 0.0 | 0.4 | Quiet, very slow, soft |
| **Glass** | 1.6 | 1.0 | 0.1 | 1.5 | Loudest, slight slip, harsh landing |
| **Stone** | 1.2 | 1.0 | 0.0 | 1.1 | Loud, hard |
| **Foliage** | 0.4 | 0.9 | 0.0 | 0.5 | Quiet, slightly slow |
| **Rubber** | 0.3 | 1.0 | 0.0 | 0.5 | Very quiet, normal speed, cushioned |

Unlisted surfaces (Flesh, Armor, Fabric, Plastic, Ceramic, Bark, Energy_Shield) all default to 1.0 / 1.0 / 0.0 / 1.0 (neutral).

### 2.4 SurfaceID Reference

| Value | Name | Value | Name |
|-------|------|-------|------|
| 0 | Default | 12 | Mud |
| 1 | Concrete | 13 | Glass |
| 2 | Metal_Thin | 14 | Flesh |
| 3 | Metal_Thick | 15 | Armor |
| 4 | Wood | 16 | Fabric |
| 5 | Dirt | 17 | Plastic |
| 6 | Sand | 18 | Stone |
| 7 | Grass | 19 | Ceramic |
| 8 | Gravel | 20 | Foliage |
| 9 | Snow | 21 | Bark |
| 10 | Ice | 22 | Rubber |
| 11 | Water | 23 | Energy_Shield |

---

## 3. Surface Damage Zones

### 3.1 Create a Hazard Zone

1. Create an empty GameObject in your SubScene
2. Add a **Box Collider** (or other trigger collider), check **Is Trigger**
3. Click **Add Component** > search for **Surface Damage Zone Authoring**
4. Position and scale the collider to cover the hazard area

### 3.2 Inspector Fields

| Field | Type | Default | Range | Description |
|-------|------|---------|-------|-------------|
| **Damage Per Second** | float | 10 | -- | DPS while entity stands on matching surface inside zone |
| **Damage Type** | DamageType | Heat | -- | Physical, Heat, Radiation, Suffocation, Explosion, Toxic |
| **Required Surface Id** | SurfaceID | Default | -- | Which surface must match. Default = any surface triggers damage |
| **Tick Interval** | float | 0.5 | 0.1--2 | How often damage ticks (seconds). Lower = smoother but more checks |
| **Ramp Up Duration** | float | 0 | 0--5 | Seconds to reach full damage. 0 = instant full damage |
| **Affects NPCs** | bool | true | -- | If false, only players take damage |

### 3.3 Example Zone Configurations

| Zone | Required Surface | DPS | Type | Tick | Ramp-Up | NPCs |
|------|-----------------|-----|------|------|---------|------|
| **Lava Pool** | Default | 25 | Heat | 0.5 | 0.5s | Yes |
| **Acid Floor** | Default | 15 | Toxic | 0.5 | 0 | Yes |
| **Electrified Grating** | Metal_Thin | 10 | Physical | 0.5 | 0 | Yes |
| **Radioactive Sludge** | Water | 5 | Radiation | 1.0 | 2.0s | Yes |
| **Freezing Ice** | Ice | 3 | Physical | 1.0 | 1.0s | No |
| **Hot Coals** | Gravel | 8 | Heat | 0.5 | 0 | Yes |

### 3.4 How Damage Zones Work

- Entity must be **grounded** (`IsGrounded = true`) -- airborne/flying entities take no damage
- Entity must be **inside the zone** (distance check, ~50m radius)
- Entity's `GroundSurfaceState.SurfaceId` must match `RequiredSurfaceId` (or zone uses Default = any)
- **Tick interval**: Damage applies every `TickInterval` seconds, not every frame
- **Ramp-up**: Damage scales linearly from 0% to 100% over `RampUpDuration` seconds after entering the zone. Leaving and re-entering resets the ramp
- Only one zone's damage applies per entity per frame (highest priority first)

### 3.5 Gizmo Visualization

Selected `SurfaceDamageZoneAuthoring` objects render an orange-red semi-transparent cube in the Scene view matching the Box Collider bounds.

---

## 4. Feature Toggles

### 4.1 Add the Toggles Singleton (Optional)

1. Create an empty GameObject in your SubScene (one per SubScene is sufficient)
2. Click **Add Component** > search for **Surface Gameplay Toggles Authoring**

If this component is absent from the world, all features default to **enabled**.

### 4.2 Inspector Fields

| Field | Default | What It Controls |
|-------|---------|-----------------|
| **Enable Movement Modifiers** | true | Speed and friction scaling per surface |
| **Enable Stealth Modifiers** | true | Noise multipliers for stealth/hearing |
| **Enable Slip Physics** | true | Ice/slippery surface momentum blending |
| **Enable Fall Damage Modifiers** | true | Surface-aware fall damage scaling |
| **Enable Surface Damage Zones** | true | Lava/acid/hazard DOT zones |

### 4.3 Use Cases

| Scenario | Toggles |
|----------|---------|
| **Full gameplay** (default) | All enabled |
| **Cosmetic-only mode** | All disabled -- surfaces only affect audio/VFX |
| **Stealth-focused game mode** | Stealth + Slip enabled, others disabled |
| **Accessibility (motor control)** | Slip disabled, others enabled |
| **Debug: isolate movement** | Only Movement enabled |

---

## 5. Debug and Inspection Tools

### 5.1 Gameplay Inspector Window

**Menu:** DIG > Surface > Gameplay Inspector

**Requires:** Play Mode active

| Section | Shows |
|---------|-------|
| **Overlay Toggle** | Checkbox to toggle scene overlay on/off |
| **Config Table** | All 24 SurfaceID values with Noise, Speed, Slip, FallDmg, DPS columns from the active blob |
| **Entity List** | First 30 entities with GroundSurfaceState -- shows Entity ID, SurfaceID, Grounded status, Hardness, Flags |

Refreshes at 2 Hz to minimize editor overhead.

### 5.2 Scene Debug Overlay

**Enable:** Toggle in the Gameplay Inspector window, or set `SurfaceDebugOverlaySystem.ShowOverlay = true` in code.

Shows floating labels above each entity with `GroundSurfaceState`:

```
[Surface Name]
H:128 D:128
Noise:1.3x Spd:0.9x
Slip:0.00
```

**Color coding:**

| Color | Meaning |
|-------|---------|
| Blue | IsSlippery flag set |
| Dark blue | IsLiquid flag set |
| Orange | Hard surface (Hardness > 200) |
| Green | Soft surface (Hardness < 80) |
| White | Default |

---

## 6. System Execution Order

```
InitializationSystemGroup:
  SurfaceGameplayConfigSystem         <- Loads SO, builds BlobAsset (runs once)

PredictedFixedStepSimulationSystemGroup:
  PlayerGroundCheckSystem             <- (existing) Player ground detection
  GroundSurfaceQuerySystem            <- Raycasts for NPCs, reads player data
  SurfaceMovementModifierSystem       <- Writes speed/friction/slip with smoothing
  SurfaceSlipSystem                   <- Momentum blending for slippery surfaces
  PlayerMovementSystem                <- (existing) Reads SpeedMultiplier + FrictionMultiplier
  StealthSystem                       <- (existing) Reads GroundSurfaceState for player noise

SimulationSystemGroup:
  SurfaceStealthModifierSystem        <- Writes NPC SurfaceNoiseModifier
  HearingDetectionSystem              <- (existing) Reads SurfaceNoiseModifier on source entities
  SurfaceDamageSystem                 <- DOT in damage zones (server-only)
  FallDetectionSystem                 <- (existing) Reads FallDamageMultiplier on landing

PresentationSystemGroup:
  GroundSurfaceCacheSystem            <- Resolves SurfaceMaterialId -> SurfaceID + properties
  SurfaceDebugOverlaySystem           <- Editor-only scene labels (when enabled)
```

---

## 7. After Setup: Reimport SubScene

After adding or modifying surface authoring components on prefabs in a SubScene:

1. Right-click the SubScene > **Reimport**
2. Wait for baking to complete

---

## 8. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Player detection | Enter Play Mode, walk on different surfaces | Gameplay Inspector shows correct SurfaceID |
| 3 | Speed on mud | Walk on Mud surface | Movement speed visibly slower (~50% of normal) |
| 4 | Speed on ice | Walk on Ice surface | Movement speed slightly faster (1.1x), reduced turning control |
| 5 | Slip physics | Walk on Ice, try to turn sharply | Momentum carries you, turning radius greatly increased |
| 6 | Noise (stealth) | Walk on Metal vs Grass | StealthSystem noise multiplier differs (1.5x vs 0.6x) |
| 7 | NPC hearing | NPC on gravel near AI listener | AI detects NPC at greater range than NPC on grass |
| 8 | Fall damage (sand) | Fall from 10m onto Sand | Damage ~50% of Concrete landing |
| 9 | Fall damage (metal) | Fall from 10m onto Metal | Damage ~130% of default |
| 10 | Lava zone | Stand in lava damage zone | 25 DPS applied, visible in health decrease |
| 11 | Zone surface match | Stand in electrified zone on Grass | No damage (surface mismatch -- requires Metal_Thin) |
| 12 | Zone ramp-up | Enter zone with 2s ramp-up | 0 damage at t=0, ~50% at t=1s, full at t=2s |
| 13 | Zone airborne | Jump inside damage zone | No damage while airborne (IsGrounded = false) |
| 14 | NPC in zone | NPC walks into lava zone (AffectsNPCs=true) | NPC takes damage |
| 15 | Friction | Stop moving on Concrete vs Sand | Player decelerates faster on Concrete (higher friction) |
| 16 | Toggle off | Set all toggles to false | Game plays identically to pre-EPIC 16.10 |
| 17 | Toggle selective | Enable only stealth toggle | Noise affected, speed/friction/damage unchanged |
| 18 | No config asset | Delete SurfaceGameplayConfig from Resources | System builds defaults, all surfaces still have gameplay values |
| 19 | Debug overlay | Enable overlay in Gameplay Inspector | Color-coded surface labels above entities |
| 20 | Boundary crossing | Walk from Concrete to Mud | Smooth ~0.125s transition, no jarring speed snap |

---

## 9. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| Entity shows Default surface everywhere | Missing GroundSurfaceDetectionAuthoring on prefab | Add the authoring component |
| Entity not grounded | Entity airborne or QueryInterval too high | Lower QueryInterval, verify entity touches ground |
| Speed not changing per surface | SurfaceGameplayToggles.EnableMovementModifiers = false, or AddMovementModifier unchecked | Check toggle singleton and authoring checkbox |
| No slip on ice | SurfaceGameplayToggles.EnableSlipPhysics = false | Check toggle singleton |
| NPC footsteps not affecting hearing range | AddNoiseModifier unchecked on NPC prefab | Enable on GroundSurfaceDetectionAuthoring |
| Damage zone not dealing damage | Entity not grounded, surface mismatch, or toggle disabled | Check IsGrounded, RequiredSurfaceId, and EnableSurfaceDamageZones toggle |
| Damage zone deals instant full damage | RampUpDuration = 0 | Set RampUpDuration > 0 for gradual scaling |
| Damage ticks too fast / too slow | TickInterval misconfigured | Adjust TickInterval (0.5s = 2 ticks/sec recommended) |
| Fall damage not scaled by surface | EnableFallDamageModifiers toggle disabled | Check toggle singleton |
| All surfaces behave the same | SurfaceGameplayConfig.asset has all 1.0 values | Use built-in defaults (delete asset from Resources) or tune entries |
| Player noise not affected by surface | StealthSystem reads GroundSurfaceState directly -- ensure player has the component | Add GroundSurfaceDetectionAuthoring to player prefab |
| Gameplay Inspector shows "Enter Play Mode" | Not in Play Mode | Enter Play Mode first |
| Config changes not reflected | BlobAsset built once at startup | Re-enter Play Mode after editing SurfaceGameplayConfig.asset |
| SubScene entity missing components | Baking stale | Right-click SubScene > Reimport |

---

## 10. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Hearing detection, aggro threat from sound | SETUP_GUIDE_15.19 |
| Stealth system, player noise | SETUP_GUIDE_15.19 |
| Fall detection system | SETUP_GUIDE_13.14 |
| Player movement, character controller | Core |
| VFX pipeline (surface impacts, dissolve) | SETUP_GUIDE_16.7 |
| Corpse lifecycle (dissolve on death) | SETUP_GUIDE_16.3 |
| Environment zones (radiation, temperature) | Survival |
| **Surface material gameplay** | **This guide (16.10)** |
