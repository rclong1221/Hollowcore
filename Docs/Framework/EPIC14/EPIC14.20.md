# Epic 14.20: Weapon Features - Projectiles, Effects, and Sound

## Overview
This epic covers bringing complete audio and visual functionality to all equipment types (firearms, bows, melee, throwables, shields), including projectile systems, impact effects, muzzle effects, and sound. The project already has a sophisticated ECS weapon foundation - this epic focuses on filling presentation layer gaps.

---

## Current State Analysis

### Already Implemented (ECS)
| Feature | Location | Status |
|---------|----------|--------|
| Multi-weapon types | `WeaponActionComponents.cs` | Complete |
| Hitscan + Projectile firing | `WeaponFireSystem.cs` | Complete |
| Ammo/Reload system | `WeaponAmmoSystem.cs` | Complete |
| Recoil + Spread | `WeaponRecoilSystem.cs`, `WeaponSpreadSystem.cs` | Complete |
| Projectile physics | `ProjectileSystem.cs` | Complete |
| Hitbox multipliers | `ProjectileSystem.cs` | Complete |
| Animation event bridge | `WeaponAnimationEventRelay.cs` | Complete |
| Fire effects framework | `FireEffectSpawnerSystem.cs` | Framework only |
| Impact effects framework | `ImpactEffectSpawnerSystem.cs` | Framework only |
| Equipment slots | `EquipmentSlots.cs` | Complete |
| NetCode prediction | Multiple systems | Complete |

### Missing/Incomplete
| Feature | Gap |
|---------|-----|
| Audio playback | Events queued but no playback system |
| VFX prefab registry | Effects reference indices with no actual prefabs |
| Surface material detection | Impacts don't know what material was hit |
| Networked projectile spawning | Client spawns locally, needs server authority |
| UI ammo display | No HUD integration |

---

## Opsive Features Available for Reference/Integration

### Surface System (`/Assets/Opsive/.../SurfaceSystem/`)
- **SurfaceManager.cs** - Texture-based material identification
- **SurfaceEffect.cs** - Material-specific impact effects
- **SurfaceType.cs** - Material definitions (wood, metal, concrete, etc.)
- **DecalManager.cs** - Decal pooling and cleanup

### Audio System (`/Assets/Opsive/.../AnimatorAudioStates/`)
- **AnimatorAudioStateSet.cs** - Audio state machine
- **PlayAudioClip.cs** - Sound playback with variations
- Random/sequence/recoil-based sound selection

### Effect Modules (`/Assets/Opsive/.../Items/Actions/Modules/`)
- **FireEffectModule.cs** - Muzzle flash + sound patterns
- **ImpactModule.cs** - Hit detection and effects
- **ProjectileModule.cs** - Visual projectile spawning

### UI (`/Assets/Opsive/.../UI/`)
- **ItemMonitor.cs** - HUD weapon display
- **ItemAttributeMonitorBinding.cs** - Ammo binding

---

## Feature Implementation Plan

### Phase 1: Audio System Integration ✅ COMPLETED
**Goal**: Play weapon sounds for ALL weapon types (firearms, bows, melee, throwables, shields)

- [x] Create `WeaponAudioManager.cs` MonoBehaviour for audio playback
- [x] Define audio event types for all weapon categories:
  - **Firearms**: Fire, DryFire, ReloadStart, MagOut, MagIn, BoltPull, ReloadComplete, ShellBounce
  - **Melee**: MeleeSwing, MeleeHit
  - **Bow/Crossbow**: BowDraw, BowRelease, BowCancel, ArrowNock
  - **Throwable**: ThrowCharge, ThrowRelease
  - **Shield**: BlockStart, BlockImpact, ParrySuccess
  - **Shared**: Equip, Unequip
- [x] Create `WeaponAudioConfig` ScriptableObject for per-weapon sound definitions
- [x] Support sound variations (multiple clips per event, random selection)
- [x] Integrate with `WeaponAnimationEventRelay` to trigger sounds on animation events
- [x] Add 3D spatial audio for gunshots (attenuation, occlusion)
- [x] Pool AudioSources to avoid allocation during gameplay

**Implementation Notes:**
- `WeaponAudioManager.cs` - Singleton with pooled AudioSources (20 initial, 50 max)
- `WeaponAudioConfig.cs` - ScriptableObject with sound arrays for all weapon types:
  - Firearm: FireClips, DryFireClips, ReloadStartClips, MagOutClips, MagInClips, BoltPullClips, ReloadCompleteClips, ShellBounceClips
  - Melee: MeleeSwingClips, MeleeHitClips
  - Bow: BowDrawClips, BowReleaseClips, BowCancelClips, ArrowNockClips
  - Throwable: ThrowChargeClips, ThrowReleaseClips
  - Shield: BlockStartClips, BlockImpactClips, ParrySuccessClips
  - Shared: EquipClips, UnequipClips
- `SurfaceAudioLibrary.cs` - Surface-specific impact sounds
- `WeaponAudioBridge.cs` - MonoBehaviour bridge for animation events to audio
- `WeaponAnimationEventRelay.cs` - Routes animation events to audio for all weapon types

### Phase 2: VFX Prefab Registry ✅ COMPLETED
**Goal**: Connect effect spawning systems to actual prefab assets

- [x] Create `EffectPrefabRegistry.cs` ScriptableObject
- [x] Define effect categories: MuzzleFlash, ShellEject, Tracer, Impact, Decal
- [x] Map effect IDs to prefab references
- [x] Support per-weapon effect overrides (different muzzle flash per gun)
- [x] Integrate registry with `FireEffectSpawnerSystem`
- [x] Integrate registry with `ImpactEffectSpawnerSystem`
- [x] Add effect pooling to avoid runtime instantiation

**Implementation Notes:**
- `EffectPrefabRegistry.cs` - Central registry with predefined EffectIds constants (0-99 muzzle, 100-199 shell, 200-299 tracer, 300-399 impact, 400-499 decal, 500-599 explosion)
- `EffectPoolManager.cs` - GameObject pooling with lifetime tracking and auto-return
- Supports surface-specific impact lookups via `GetSurfaceImpactEffect()`

### Phase 3: Surface Material Detection ✅ COMPLETED
**Goal**: Spawn appropriate impact effects based on hit surface material

**Implemented: Custom ECS Surface System (Option B)**
- [x] Create `SurfaceType` component for tagged surfaces
- [x] Create `SurfaceMaterialAuthoring` for scene objects
- [x] Add texture-to-material lookup table
- [x] Terrain surface detection via splat map sampling

**Shared Tasks:**
- [x] Define surface types: Concrete, Metal, Wood, Dirt, Flesh, Water, Glass
- [x] Create impact effect sets per surface type (particles + decal + sound)
- [x] Modify `ImpactEffectSpawnerSystem` to query surface material

**Implementation Notes:**
- `SurfaceMaterialDetector.cs` - Static utility with multi-priority detection:
  1. `SurfaceMaterialTag` MonoBehaviour component
  2. Parent hierarchy check
  3. TerrainCollider splat map sampling
  4. Render material name detection
  5. Physics material name detection
- `SurfaceMaterialComponent` - ECS IComponentData for entity-based detection
- `SurfaceMaterialAuthoring` - Baker for authoring surface materials
- Supports 13 surface types: Default, Concrete, Metal, Wood, Dirt, Grass, Sand, Water, Glass, Flesh, Cloth, Plastic, Foliage

### Phase 4: Muzzle Effects Enhancement ✅ COMPLETED
**Goal**: Complete muzzle flash, shell ejection, and tracer systems

- [x] Create muzzle flash VFX prefabs (per weapon class)
- [x] Add muzzle light flash (brief point light on fire)
- [x] Create shell ejection prefabs with physics
- [x] Implement tracer system with line renderer or particle trail
- [ ] Add smoke/heat distortion for sustained fire (Future)
- [ ] Support first-person vs third-person effect variants (Future)

**Implementation Notes:**
- `EffectPresentationSystem.cs` - Hybrid system bridging ECS to GameObjects
- `MuzzleLightController.cs` - Configurable muzzle flash light with fade animation
- `TracerRenderer.cs` - LineRenderer/TrailRenderer based tracer visuals
- `ShellCasingController.cs` - Physics-based shell ejection with bounce audio

### Phase 5: Impact Effects Enhancement ✅ COMPLETED
**Goal**: Rich impact feedback with decals, particles, and debris

- [x] Create impact particle prefabs per surface type
- [x] Implement decal system with proper UV projection
- [x] Add decal fading over time
- [x] Implement decal limits (max decals, oldest removal)
- [x] Add debris spawning for destructible surfaces
- [x] Add ricochet spark effects for metal/concrete
- [x] Blood splatter effects for flesh hits (with intensity scaling)

**Implementation Notes:**
- `DecalSystem.cs` - Decal pooling with limits, fading, and surface-specific decals
- `ImpactEffectController.cs` - Surface-aware particle effects with debris spawning
- `ImpactPresentationSystem.cs` - Bridges ECS impacts to GameObject effects

### Phase 6: Projectile Visuals ✅ COMPLETED
**Goal**: Visible projectiles for non-hitscan weapons

- [x] Create projectile mesh/trail prefabs
- [x] Add tracer rounds (every Nth bullet)
- [x] Implement projectile trail rendering (line renderer or particles)
- [x] Add projectile light emission for tracers
- [x] Support different projectile visuals (bullet, rocket, arrow)

**Implementation Notes:**
- `ProjectilePresentationSystem.cs` - Spawns visual GameObjects for ECS projectiles
- `ProjectileVisualTracker.cs` - Static tracker for position synchronization
- Uses EffectPrefabRegistry for projectile-type to prefab mapping

### Phase 7: Networked Projectile Authority ✅ ALREADY IMPLEMENTED
**Goal**: Server-authoritative projectile spawning with client prediction

- [x] Implement server-side projectile spawning
- [x] Add client-side predicted projectiles (visual only)
- [x] Reconcile predicted vs authoritative projectile positions
- [x] Handle projectile despawn across network
- [x] Ensure damage is server-authoritative

**Implementation Notes:**
- Already implemented in `ProjectileSystem.cs` with `[GhostField]` attributes
- `WeaponActionComponents.cs` has proper ghost configuration
- Damage application is server-authoritative via DamageEvent buffer

### Phase 8: UI - Ammo and Weapon Display ✅ COMPLETED
**Goal**: HUD elements showing weapon state

- [x] Create ammo counter UI (current clip / reserve)
- [x] Add reload indicator/progress bar
- [x] Show current weapon name/icon
- [x] Add crosshair with spread visualization
- [x] Implement hit marker feedback
- [x] Add low ammo warning indicator
- [x] Show fire mode indicator (auto/semi/burst)

**Implementation Notes:**
- `WeaponHUD.cs` - Complete ammo display with reload progress, hit markers, low ammo warning
- `CrosshairController.cs` - Dynamic crosshair with spread, hit punch, and target color
- `WeaponHUDSystem.cs` - ECS system that updates UI from weapon component data

### Phase 9: Advanced Features (Optional)
**Goal**: Polish and advanced mechanics

- [ ] Weapon attachments (sights, suppressors, grips)
- [ ] Attachment effect modifications (suppressor reduces sound, flash)
- [ ] Bullet penetration through thin surfaces
- [ ] Ricochet mechanics
- [ ] Bullet drop visualization (scope mil-dots)
- [ ] Weapon jam/malfunction system
- [ ] Overheating for sustained fire

---

## Files Created

| File | Purpose | Status |
|------|---------|--------|
| `Assets/Scripts/Weapons/Audio/WeaponAudioManager.cs` | Central audio playback | ✅ Created |
| `Assets/Scripts/Weapons/Audio/WeaponAudioConfig.cs` | Per-weapon sound definitions | ✅ Created |
| `Assets/Scripts/Weapons/Audio/SurfaceAudioLibrary.cs` | Surface-specific impact sounds | ✅ Created |
| `Assets/Scripts/Weapons/Audio/WeaponAudioBridge.cs` | Animation event to audio bridge | ✅ Created |
| `Assets/Scripts/Weapons/Effects/EffectPrefabRegistry.cs` | VFX prefab lookup | ✅ Created |
| `Assets/Scripts/Weapons/Effects/SurfaceMaterialDetector.cs` | Surface detection utility | ✅ Created |
| `Assets/Scripts/Weapons/Effects/EffectPoolManager.cs` | VFX pooling system | ✅ Created |
| `Assets/Scripts/Weapons/Effects/EffectPresentationSystem.cs` | ECS-to-GameObject bridge | ✅ Created |
| `Assets/Scripts/Weapons/Effects/MuzzleLightController.cs` | Muzzle flash light | ✅ Created |
| `Assets/Scripts/Weapons/Effects/TracerRenderer.cs` | Tracer visuals | ✅ Created |
| `Assets/Scripts/Weapons/Effects/ShellCasingController.cs` | Shell physics and audio | ✅ Created |
| `Assets/Scripts/Weapons/Effects/DecalSystem.cs` | Decal management | ✅ Created |
| `Assets/Scripts/Weapons/Effects/ImpactEffectController.cs` | Impact particles and debris | ✅ Created |
| `Assets/Scripts/Weapons/Effects/ProjectilePresentationSystem.cs` | Projectile visuals | ✅ Created |
| `Assets/Scripts/Weapons/UI/WeaponHUD.cs` | Ammo/weapon UI | ✅ Created |
| `Assets/Scripts/Weapons/UI/CrosshairController.cs` | Dynamic crosshair | ✅ Created |
| `Assets/Scripts/Weapons/UI/WeaponHUDSystem.cs` | UI update system | ✅ Created |

## Files Modified

| File | Changes | Status |
|------|---------|--------|
| `WeaponAnimationEventRelay.cs` | Trigger audio on events | ✅ Modified |
| `FireEffectSpawnerSystem.cs` | Creates ECS entities for effects | Already complete |
| `ImpactEffectSpawnerSystem.cs` | Creates ECS entities for impacts | Already complete |
| `ProjectileSystem.cs` | Already has network authority | Already complete |
| `WeaponEffectComponents.cs` | Already has surface material support | Already complete |

---

## Asset Requirements

### Audio Assets Needed

#### Firearms
- Fire sounds per weapon (3-5 variations each)
- Reload sounds (mag out, mag in, bolt pull, reload complete)
- Dry fire click
- Shell casing sounds (bounce on ground)
- Distant gunshot sounds (for 3D falloff)

#### Melee Weapons
- Swing/whoosh sounds (2-4 variations)
- Hit/impact sounds (2-4 variations)

#### Bow/Crossbow
- String draw/tension sounds (1-2 variations)
- String release/twang sounds (1-2 variations)
- Draw cancel sounds (1-2 variations)
- Arrow nock/click sounds (1-2 variations)

#### Throwables
- Charge/wind-up sounds (1-2 variations)
- Release sounds (1-2 variations)

#### Shield
- Block raise sounds (1-2 variations)
- Block impact sounds (2-4 variations)
- Parry success sounds (1-2 variations)

#### Shared
- Equip/draw sounds (1-2 variations)
- Unequip/holster sounds (1-2 variations)
- Impact sounds per surface (5+ variations each)

### VFX Assets Needed
- Muzzle flash particles (pistol, rifle, shotgun, etc.)
- Shell ejection prefabs
- Tracer prefabs
- Impact particles per surface type
- Bullet hole decals per surface type
- Blood splatter particles/decals
- Smoke/dust particles
- Ricochet spark particles

---

## Priority Order

1. **Phase 2: VFX Prefab Registry** - Unblocks all visual effects
2. **Phase 1: Audio System** - Critical for game feel
3. **Phase 3: Surface Detection** - Required for proper impacts
4. **Phase 5: Impact Effects** - Major visual feedback
5. **Phase 4: Muzzle Effects** - Polish
6. **Phase 8: UI** - Player information
7. **Phase 6: Projectile Visuals** - Non-hitscan weapons
8. **Phase 7: Network Authority** - Multiplayer correctness
9. **Phase 9: Advanced** - Future scope

---

## Design Decisions Needed

1. **Surface Detection**: Use Opsive SurfaceManager or build custom ECS system?
   - Opsive: Faster integration, proven solution
   - Custom: Full ECS compatibility, no MonoBehaviour dependency

2. **Audio Architecture**: Pooled AudioSources or FMOD/Wwise?
   - Unity native: Simpler, no middleware
   - FMOD/Wwise: Better mixing, occlusion, but adds complexity

3. **Decal System**: Opsive DecalManager or custom?
   - Opsive: Already pooled, working
   - Custom: Can be ECS-native

4. **Effect Pooling**: Entity pooling or GameObject pooling?
   - Entity: Full ECS, better for large counts
   - GameObject: Easier for complex VFX prefabs

---

## Notes

- The ECS weapon foundation is solid - focus is on presentation layer
- Opsive provides excellent reference implementations
- Audio is the highest-impact missing feature for game feel
- Surface detection is required before impact effects can be material-aware
- Consider using Opsive's SurfaceManager directly as a bridge component
