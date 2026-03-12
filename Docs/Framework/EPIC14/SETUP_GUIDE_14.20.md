# Epic 14.20 Setup Guide: Weapon Audio & Effects System

This guide covers how to set up and configure the weapon audio, visual effects, and UI systems in the Unity Editor. The system supports **all weapon types**: firearms, bows, melee weapons, throwables, and shields.

---

## Table of Contents

1. [Equipment Workstation Tool](#equipment-workstation-tool)
2. [Prerequisites](#prerequisites)
3. [Scene Setup](#scene-setup)
4. [Creating Audio Configurations](#creating-audio-configurations)
5. [Creating Effect Prefabs](#creating-effect-prefabs)
6. [Setting Up the Effect Registry](#setting-up-the-effect-registry)
7. [Configuring Surface Materials](#configuring-surface-materials)
8. [Setting Up Weapon Prefabs](#setting-up-weapon-prefabs)
9. [UI Setup](#ui-setup)
10. [Testing & Debugging](#testing--debugging)

---

## Equipment Workstation Tool

The **Equipment Workstation** is an Editor tool that helps you set up weapon prefabs with audio and VFX components. It provides automatic detection of missing components and one-click fixes.

### Accessing the Tool

1. Open Unity Editor
2. Go to menu: **DIG > Equipment Workstation**
3. Click the **Audio/FX** tab

### Audio/FX Tab Features

The Audio/FX module has four sub-tabs:

#### Quick Setup Tab
- **Analyze Prefab**: Scans weapon prefab for missing components
- **Fix All Missing**: Adds all missing components in one click
- Shows status summary: Complete, Incomplete, Missing items
- Grouped by category: Core, Audio, VFX, Transforms

#### Audio Tab
- Validates `WeaponAudioBridge` component
- Assigns or creates `WeaponAudioConfig` assets
- Shows audio clip status for each sound category
- Auto-assigns muzzle transform for 3D audio positioning

#### VFX Tab
- Validates `ItemVFXAuthoring` component
- Shows recommended VFX entries (Fire, ShellEject, etc.)
- Displays VFX entry configuration

#### Transforms Tab
- Finds or creates required transforms:
  - **Muzzle Point** - for VFX and audio positioning
  - **Ejection Port** - for shell casing spawning
  - **Magazine** - for reload animations
- Auto-assigns transforms to components

### Quick Start Workflow

1. Select your weapon prefab in the Project window
2. Open **DIG > Equipment Workstation > Audio/FX**
3. Click **Use Selected** to load the prefab
4. Click **Analyze Prefab** to check status
5. Review missing items and click **Fix All Missing** or fix individually
6. Create or assign a `WeaponAudioConfig` asset in the Audio tab
7. Verify transforms are set up in the Transforms tab

---

## Prerequisites

Before starting, ensure you have:
- Unity 2022.3+ with DOTS/ECS packages installed
- TextMeshPro package (for UI)
- Weapon prefabs with animation controllers
- Audio clips for weapon sounds
- Particle system prefabs for effects

---

## Scene Setup

### Required Manager Objects

Create an empty GameObject in your scene hierarchy and add these manager components:

1. **WeaponAudioManager**
   - Create: `GameObject > Create Empty > "WeaponAudioManager"`
   - Add Component: `DIG.Weapons.Audio.WeaponAudioManager`

   | Property | Recommended Value | Description |
   |----------|-------------------|-------------|
   | Initial Pool Size | 20 | Starting AudioSources |
   | Max Pool Size | 50 | Maximum AudioSources |
   | Default Min Distance | 1 | 3D audio min distance |
   | Default Max Distance | 100 | 3D audio max distance |

2. **EffectPoolManager**
   - Create: `GameObject > Create Empty > "EffectPoolManager"`
   - Add Component: `DIG.Weapons.Effects.EffectPoolManager`

   | Property | Recommended Value | Description |
   |----------|-------------------|-------------|
   | Default Pool Size | 10 | Instances per effect type |
   | Max Pool Size | 50 | Maximum per effect type |
   | Cleanup Interval | 30 | Seconds between cleanups |

3. **DecalSystem**
   - Create: `GameObject > Create Empty > "DecalSystem"`
   - Add Component: `DIG.Weapons.Effects.DecalSystem`

   | Property | Recommended Value | Description |
   |----------|-------------------|-------------|
   | Max Decals | 100 | Maximum active decals |
   | Remove Oldest | ✓ | Remove old decals when limit reached |
   | Default Lifetime | 60 | Decal duration in seconds |
   | Fade Time | 5 | Seconds to fade before removal |

### Manager Hierarchy Example
```
Scene
├── Managers
│   ├── WeaponAudioManager
│   ├── EffectPoolManager
│   └── DecalSystem
```

---

## Creating Audio Configurations

### Step 1: Create a WeaponAudioConfig Asset

1. In Project window: `Right-click > Create > DIG > Weapons > Audio Config`
2. Name it descriptively (e.g., `AudioConfig_AssaultRifle`)

### Step 2: Configure Sound Arrays

| Section | Clips to Add | Notes |
|---------|--------------|-------|
| **Fire Sounds** | 3-5 gunshot variations | Main firing sounds |
| Fire Volume | 0.8 - 1.0 | Adjust to taste |
| Fire Pitch Variation | 0.05 | Adds natural variety |
| Fire Distant Clips | 1-3 distant sounds | Played at range |
| Distant Sound Distance | 50 | Meters before distant sound |
| **Dry Fire** | 1-2 click sounds | Empty magazine sound |
| Dry Fire Volume | 0.7 | Usually quieter |
| **Reload Sounds** | | |
| Reload Start Clips | 1-2 clips | Initial reload sound |
| Mag Out Clips | 1-2 clips | Magazine removal |
| Mag In Clips | 1-2 clips | Magazine insertion |
| Bolt Pull Clips | 1-2 clips | Charging handle/bolt |
| Reload Complete Clips | 1-2 clips | Optional finish sound |
| Reload Volume | 0.8 | |
| **Shell Casing** | 3-5 bounce sounds | Shell hitting ground |
| Shell Volume | 0.4 | Should be subtle |
| **Equip/Unequip** | 1-2 each | Draw/holster sounds |
| Equip Volume | 0.6 | |
| **Melee** | | (for melee weapons) |
| Melee Swing Clips | 2-4 whoosh sounds | Swing audio |
| Melee Hit Clips | 2-4 impact sounds | Hit confirmation |
| Melee Volume | 0.8 | |
| **Bow/Crossbow** | | (for ranged non-firearm) |
| Bow Draw Clips | 1-2 string tension sounds | Draw/charge |
| Bow Release Clips | 1-2 twang sounds | Arrow release |
| Bow Cancel Clips | 1-2 string relax sounds | Cancel draw |
| Arrow Nock Clips | 1-2 click sounds | Reload equivalent |
| Bow Volume | 0.7 | |
| **Throwable** | | (grenades, etc.) |
| Throw Charge Clips | 1-2 wind-up sounds | Charge animation |
| Throw Release Clips | 1-2 release sounds | Throw moment |
| Throw Volume | 0.7 | |
| **Shield** | | (blocking weapons) |
| Block Start Clips | 1-2 raise sounds | Shield raise |
| Block Impact Clips | 2-4 impact sounds | Blocked hit |
| Parry Success Clips | 1-2 distinct sounds | Perfect parry |
| Shield Volume | 0.8 | |
| **3D Audio** | | |
| Min Distance | 1 | Full volume range |
| Max Distance | 100 | Falloff distance |
| Spatial Blend | 1.0 | 1 = full 3D |

### Step 3: Create Surface Audio Library

1. `Right-click > Create > DIG > Weapons > Surface Audio Library`
2. Name it `SurfaceAudioLibrary_Main`

For each surface type, add a Sound Set:

| Surface Type | Bullet Impact Clips | Notes |
|--------------|---------------------|-------|
| Default | 3-5 generic impacts | Fallback sounds |
| Metal | 3-5 metallic pings | High-pitched |
| Wood | 3-5 wood thuds | Softer impact |
| Concrete | 3-5 stone/concrete | Hard impact |
| Dirt | 3-5 dirt impacts | Muffled |
| Glass | 2-3 glass breaks | Sharp sounds |
| Flesh | 3-5 flesh impacts | Wet sounds |
| Water | 2-3 splash sounds | |

---

## Creating Effect Prefabs

### Muzzle Flash Prefab

1. Create prefab: `GameObject > Effects > Particle System`
2. Configure Particle System:

   | Module | Setting | Value |
   |--------|---------|-------|
   | **Main** | Duration | 0.1 |
   | | Start Lifetime | 0.05 - 0.1 |
   | | Start Speed | 0 |
   | | Start Size | 0.3 - 0.5 |
   | | Simulation Space | World |
   | **Emission** | Rate over Time | 0 |
   | | Bursts | 1 burst, Count: 1 |
   | **Shape** | Shape | Cone |
   | | Angle | 15 |
   | **Renderer** | Material | Additive particle |
   | | Render Mode | Billboard |

3. Add `MuzzleLightController` component:

   | Property | Value |
   |----------|-------|
   | Light Color | Orange (255, 200, 100) |
   | Max Intensity | 4 |
   | Light Range | 6 |
   | Flash Duration | 0.05 |
   | Smooth Fade | ✓ |

4. Save as prefab: `Assets/Prefabs/Effects/MuzzleFlash_Rifle.prefab`

### Shell Casing Prefab

1. Create: `GameObject > 3D Object > Capsule` (scale to bullet size)
2. Add components:
   - `Rigidbody` (mass: 0.01, drag: 0.5)
   - `ShellCasingController`

   | Property | Value |
   |----------|-------|
   | Ejection Force | 3 |
   | Force Variation | 0.5 |
   | Spin Torque | 10 |
   | Play Bounce Sound | ✓ |
   | Min Bounce Velocity | 0.5 |
   | Max Bounce Sounds | 3 |
   | Lifetime | 5 |
   | Fade Out | ✓ |

3. Save as prefab: `Assets/Prefabs/Effects/Shell_Rifle.prefab`

### Tracer Prefab

1. Create: `GameObject > Create Empty > "Tracer"`
2. Add `TracerRenderer` component:

   | Property | Value |
   |----------|-------|
   | Render Mode | LineRenderer |
   | Line Width | 0.03 |
   | Line Length | 3 |
   | Start Color | Yellow (255, 230, 128, 255) |
   | End Color | Orange (255, 128, 50, 0) |
   | Emit Light | ✓ |
   | Light Intensity | 1 |
   | Light Range | 2 |

3. Save as prefab: `Assets/Prefabs/Effects/Tracer_Standard.prefab`

### Impact Effect Prefab

1. Create particle system for dust/sparks
2. Add `ImpactEffectController` component:

   | Property | Value |
   |----------|-------|
   | Main Particles | (assign particle system) |
   | Use Surface Colors | ✓ |
   | Spawn Debris | ✓ (for wood/concrete) |
   | Enable Ricochet Sparks | ✓ |

3. Create variants for each surface type:
   - `Impact_Metal.prefab` - sparks, metallic particles
   - `Impact_Wood.prefab` - wood chips, dust
   - `Impact_Concrete.prefab` - dust, debris
   - `Impact_Dirt.prefab` - dirt puffs
   - `Impact_Flesh.prefab` - blood particles

### Decal Prefabs

1. Create: `GameObject > 3D Object > Quad`
2. Scale: (0.1, 0.1, 0.1)
3. Assign bullet hole material (use alpha cutout shader)
4. Remove collider
5. Save variants:
   - `Decal_BulletHole_Default.prefab`
   - `Decal_BulletHole_Metal.prefab`
   - `Decal_BulletHole_Wood.prefab`
   - `Decal_Blood.prefab`

---

## Setting Up the Effect Registry

### Create the Registry Asset

1. `Right-click > Create > DIG > Weapons > Effect Prefab Registry`
2. Name it `EffectPrefabRegistry_Main`

### Configure Effect Entries

#### Muzzle Flash Effects (IDs 0-99)

| Effect ID | Name | Prefab | Default Lifetime |
|-----------|------|--------|------------------|
| 0 | MuzzleFlash_Pistol | MuzzleFlash_Pistol | 0.1 |
| 1 | MuzzleFlash_Rifle | MuzzleFlash_Rifle | 0.1 |
| 2 | MuzzleFlash_Shotgun | MuzzleFlash_Shotgun | 0.15 |
| 3 | MuzzleFlash_SMG | MuzzleFlash_SMG | 0.08 |
| 5 | MuzzleFlash_Suppressed | MuzzleFlash_Suppressed | 0.05 |

#### Shell Ejection Effects (IDs 100-199)

| Effect ID | Name | Prefab | Default Lifetime |
|-----------|------|--------|------------------|
| 100 | Shell_Pistol | Shell_Pistol | 3 |
| 101 | Shell_Rifle | Shell_Rifle | 3 |
| 102 | Shell_Shotgun | Shell_Shotgun | 3 |

#### Tracer Effects (IDs 200-299)

| Effect ID | Name | Prefab | Default Lifetime |
|-----------|------|--------|------------------|
| 200 | Tracer_Standard | Tracer_Standard | 0.5 |
| 201 | Tracer_Green | Tracer_Green | 0.5 |
| 202 | Tracer_Red | Tracer_Red | 0.5 |

#### Impact Effects (IDs 300-399)

| Effect ID | Name | Prefab | Default Lifetime |
|-----------|------|--------|------------------|
| 300 | Impact_Default | Impact_Default | 2 |
| 301 | Impact_Concrete | Impact_Concrete | 2 |
| 302 | Impact_Metal | Impact_Metal | 2 |
| 303 | Impact_Wood | Impact_Wood | 2 |
| 304 | Impact_Dirt | Impact_Dirt | 2 |
| 305 | Impact_Water | Impact_Water | 2 |
| 306 | Impact_Flesh | Impact_Flesh | 2 |

#### Decal Effects (IDs 400-499)

| Effect ID | Name | Prefab | Default Lifetime |
|-----------|------|--------|------------------|
| 400 | Decal_BulletHole | Decal_BulletHole_Default | 30 |
| 401 | Decal_BulletHole_Metal | Decal_BulletHole_Metal | 30 |
| 402 | Decal_BulletHole_Wood | Decal_BulletHole_Wood | 30 |
| 403 | Decal_Blood | Decal_Blood | 30 |

### Configure Surface Impact Mapping

In the **Surface-Specific Impacts** section, add entries:

| Surface Type | Impact Type | Effect Entry |
|--------------|-------------|--------------|
| Default | Bullet | Impact_Default |
| Metal | Bullet | Impact_Metal |
| Wood | Bullet | Impact_Wood |
| Concrete | Bullet | Impact_Concrete |
| Dirt | Bullet | Impact_Dirt |
| Flesh | Bullet | Impact_Flesh |
| Water | Bullet | Impact_Water |

---

## Configuring Surface Materials

### Tagging Scene Objects

For objects that need specific impact effects:

1. Select the GameObject
2. Add Component: `SurfaceMaterialTag`
3. Set **Material Type** dropdown to appropriate surface

| Object Type | Suggested Surface |
|-------------|-------------------|
| Metal walls/doors | Metal |
| Wooden crates | Wood |
| Concrete floors | Concrete |
| Ground terrain | Dirt/Grass |
| Windows | Glass |
| Characters | Flesh |
| Water volumes | Water |

### Automatic Detection

The system automatically detects surfaces by:

1. **SurfaceMaterialTag component** (highest priority)
2. **Parent hierarchy** (inherited tags)
3. **Terrain splat maps** (terrain textures)
4. **Material names** (keywords like "metal", "wood")
5. **Physics material names** (fallback)

For automatic detection, name your materials descriptively:
- `M_Wall_Concrete_01`
- `M_Floor_Wood_Dark`
- `M_Prop_Metal_Rusty`

---

## Setting Up Weapon Prefabs

### Adding the Audio Bridge

1. Select your weapon prefab
2. Add Component: `WeaponAudioBridge`

   | Property | Description |
   |----------|-------------|
   | Audio Config | Assign the WeaponAudioConfig asset |
   | Weapon Type Id | Unique ID for this weapon type |
   | Dedicated Source | Optional AudioSource on weapon |
   | Muzzle Transform | Transform at barrel end |
   | Debug Logging | Enable for testing |

### Animation Event Setup

Animation events are routed through `WeaponAnimationEventRelay` which auto-triggers audio via `WeaponAudioBridge`. Add animation events using these names:

#### Firearm Events

| Animation Event | Aliases | Audio Triggered |
|-----------------|---------|-----------------|
| `OnAnimatorItemFire` | `Fire`, `OnItemUse` | Fire (or DryFire if no ammo) |
| `OnAnimatorDryFire` | `DryFire` | DryFire |
| `OnAnimatorReloadStart` | `ReloadStart` | ReloadStart |
| `OnAnimatorItemReloadDropClip` | `DropClip` | MagOut |
| `OnAnimatorItemReloadAttachClip` | `AttachClip` | MagIn |
| `OnAnimatorBoltPull` | `BoltPull`, `SlidePull` | BoltPull |
| `OnAnimatorReloadComplete` | `ReloadComplete` | ReloadComplete |
| `OnAnimatorEquipStart` | `EquipStart` | Equip |
| `OnAnimatorUnequipStart` | `UnequipStart` | Unequip |

#### Melee Events

| Animation Event | Aliases | Audio Triggered |
|-----------------|---------|-----------------|
| `OnAnimatorMeleeStart` | `MeleeStart` | MeleeSwing |
| `OnAnimatorMeleeHitFrame` | `MeleeHit`, `HitFrame` | MeleeHit |

#### Bow/Crossbow Events

| Animation Event | Aliases | Audio Triggered |
|-----------------|---------|-----------------|
| `OnAnimatorBowDraw` | `BowDraw`, `DrawBow` | BowDraw |
| `OnAnimatorBowRelease` | `BowRelease`, `BowFire` | BowRelease |
| `OnAnimatorBowCancel` | `BowCancel` | BowCancel |
| `OnAnimatorArrowNock` | `ArrowNock`, `NockArrow` | ArrowNock |

#### Throwable Events

| Animation Event | Aliases | Audio Triggered |
|-----------------|---------|-----------------|
| `OnAnimatorThrowChargeStart` | `ChargeStart` | ThrowCharge |
| `OnAnimatorThrowRelease` | `ThrowRelease`, `Release` | ThrowRelease |

#### Shield Events

| Animation Event | Aliases | Audio Triggered |
|-----------------|---------|-----------------|
| `OnAnimatorBlockStart` | `BlockStart` | BlockStart |
| `OnAnimatorBlockImpact` | `BlockImpact`, `ShieldHit` | BlockImpact |
| `OnAnimatorParrySuccess` | `ParrySuccess` | ParrySuccess |

### Direct WeaponAudioBridge Methods

You can also call these methods directly on `WeaponAudioBridge`:

| Method | Description |
|--------|-------------|
| `OnFire()` | Fire sound |
| `OnDryFire()` | Empty click |
| `OnReloadStart()` | Reload start |
| `OnMagOut()` | Magazine out |
| `OnMagIn()` | Magazine in |
| `OnBoltPull()` | Bolt/slide |
| `OnReloadComplete()` | Reload complete |
| `OnEquip()` | Weapon draw |
| `OnUnequip()` | Weapon holster |

### Effect Configuration (ECS)

In your weapon's authoring component, configure:

| Property | Description |
|----------|-------------|
| Muzzle Flash Prefab Index | 0-5 (see registry) |
| Shell Eject Prefab Index | 100-102 |
| Tracer Prefab Index | 200-202 |
| Impact Prefab Index | 300 (default) |
| Tracer Probability | 0.0 - 1.0 (e.g., 0.25 = every 4th) |
| Muzzle Offset | Local position of muzzle |
| Shell Eject Offset | Local position of ejection port |
| Shell Eject Direction | Local direction for shells |
| Shell Eject Speed | Force of ejection |

---

## UI Setup

### WeaponHUD Setup

1. Create UI Canvas: `GameObject > UI > Canvas`
2. Create empty child: `"WeaponHUD"`
3. Add Component: `WeaponHUD`

#### Required UI Elements

Create these as children of WeaponHUD:

**Ammo Display:**
```
WeaponHUD
├── AmmoDisplay (Panel)
│   ├── AmmoCurrentText (TextMeshPro)
│   ├── AmmoDivider (Text "/" )
│   └── AmmoReserveText (TextMeshPro)
```

**Weapon Info:**
```
├── WeaponInfo (Panel)
│   ├── WeaponNameText (TextMeshPro)
│   ├── WeaponIcon (Image)
│   └── FireModeText (TextMeshPro)
```

**Reload Indicator:**
```
├── ReloadIndicator (Panel)
│   ├── ReloadProgressBar (Slider)
│   └── ReloadText (TextMeshPro)
```

**Hit Marker:**
```
├── HitMarker (Image, centered)
```

#### WeaponHUD Configuration

| Property | Assignment |
|----------|------------|
| Ammo Current Text | AmmoCurrentText |
| Ammo Reserve Text | AmmoReserveText |
| Ammo Display | AmmoDisplay panel |
| Weapon Name Text | WeaponNameText |
| Weapon Icon | WeaponIcon |
| Fire Mode Text | FireModeText |
| Reload Progress Bar | ReloadProgressBar |
| Reload Text | ReloadText |
| Hit Marker Image | HitMarker |
| Low Ammo Threshold | 0.25 |
| Normal Ammo Color | White |
| Low Ammo Color | Red |
| Hit Marker Duration | 0.2 |

### Crosshair Setup

1. Create child under Canvas: `"Crosshair"`
2. Add Component: `CrosshairController`

#### Crosshair Structure
```
Crosshair
├── CenterDot (Image, small circle)
├── TopLine (Image, vertical line)
├── BottomLine (Image, vertical line)
├── LeftLine (Image, horizontal line)
└── RightLine (Image, horizontal line)
```

#### CrosshairController Configuration

| Property | Value |
|----------|-------|
| Center Dot | CenterDot RectTransform |
| Top Line | TopLine RectTransform |
| Bottom Line | BottomLine RectTransform |
| Left Line | LeftLine RectTransform |
| Right Line | RightLine RectTransform |
| Min Spread | 10 |
| Max Spread | 100 |
| Spread Lerp Speed | 10 |
| Hit Punch Scale | 1.2 |
| Hide When Aiming | ✓ |
| Normal Color | White |
| Enemy Hover Color | Red |
| Friendly Hover Color | Green |

---

## Testing & Debugging

### Enable Debug Logging

On each manager component, enable **Debug Logging** to see console output:

- `WeaponAudioManager` - Shows audio playback events
- `EffectPoolManager` - Shows effect spawning/pooling
- `WeaponAudioBridge` - Shows animation event triggers

### Common Issues

| Issue | Solution |
|-------|----------|
| No sounds playing | Check WeaponAudioManager exists in scene |
| Effects not spawning | Check EffectPrefabRegistry is assigned |
| Wrong impact effects | Add SurfaceMaterialTag to objects |
| Decals z-fighting | Increase decal offset in DecalSystem |
| Audio too quiet/loud | Adjust volumes in WeaponAudioConfig |
| Effects not pooling | Check EffectPoolManager is in scene |
| UI not updating | Ensure player has GhostOwnerIsLocal |

### Performance Tips

1. **Pre-warm pools** - Call `EffectPoolManager.PrewarmPool()` during loading
2. **Limit decals** - Keep max decals under 100 for performance
3. **Audio pool size** - 20-30 is usually sufficient
4. **Effect lifetimes** - Keep muzzle flashes under 0.15s
5. **Tracer probability** - Use 0.25 (1 in 4) for automatic weapons

### Validation Checklist

- [ ] WeaponAudioManager in scene
- [ ] EffectPoolManager in scene
- [ ] DecalSystem in scene (if using decals)
- [ ] EffectPrefabRegistry asset created and populated
- [ ] SurfaceAudioLibrary asset created
- [ ] WeaponAudioConfig assets for each weapon type
- [ ] WeaponAudioBridge on weapon prefabs
- [ ] Animation events calling audio methods
- [ ] UI Canvas with WeaponHUD configured
- [ ] Crosshair set up with all line references

---

## Quick Reference: Asset Creation Menu

| Asset Type | Menu Path |
|------------|-----------|
| Weapon Audio Config | Create > DIG > Weapons > Audio Config |
| Surface Audio Library | Create > DIG > Weapons > Surface Audio Library |
| Effect Prefab Registry | Create > DIG > Weapons > Effect Prefab Registry |

---

## File Locations

| File | Path |
|------|------|
| WeaponAudioManager | `Assets/Scripts/Weapons/Audio/` |
| WeaponAudioConfig | `Assets/Scripts/Weapons/Audio/` |
| WeaponAudioBridge | `Assets/Scripts/Weapons/Audio/` |
| SurfaceAudioLibrary | `Assets/Scripts/Weapons/Audio/` |
| EffectPoolManager | `Assets/Scripts/Weapons/Effects/` |
| EffectPrefabRegistry | `Assets/Scripts/Weapons/Effects/` |
| DecalSystem | `Assets/Scripts/Weapons/Effects/` |
| SurfaceMaterialDetector | `Assets/Scripts/Weapons/Effects/` |
| WeaponHUD | `Assets/Scripts/Weapons/UI/` |
| CrosshairController | `Assets/Scripts/Weapons/UI/` |
