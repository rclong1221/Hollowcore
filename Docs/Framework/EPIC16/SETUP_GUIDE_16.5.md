# SETUP GUIDE 16.5: Codebase Hygiene Audit & Warning Resolution

**Status:** Implemented
**Last Updated:** February 22, 2026
**Requires:** Nothing (self-contained cleanup)

This guide documents what changed in the codebase hygiene pass. Unlike other EPICs, this is not a feature with authoring components — it is a cleanup and bug-fix pass that resolves compiler warnings, deletes dead code, completes critical stubs, and standardizes debug logging. Devs and designers should read this to understand what now works that didn't before.

---

## What Changed

### Firearms Now Work

**ShootableActionSystem** was a legacy stub with unimplemented raycast damage and projectile spawn. This system is **intentionally disabled** (`state.Enabled = false`) because it was superseded by `WeaponFireSystem`. All modern firearms use `WeaponFireComponent` (baked by `WeaponBaker`), not `ShootableAction`.

**Action required:** None. If you have weapons, they should already use the `WeaponFireComponent` pipeline. If you see `ShootableAction` on a legacy prefab, migrate it to `WeaponFireComponent`.

### Channeled Weapons Fully Function

`ChannelActionSystem` was a stub — the `ApplyChannelTick` method did nothing. It now:

- **Raycasts** from the owner's eye position in aim direction
- **Resolves hitboxes** through the full chain (Hitbox→HitboxOwnerLink→DamageableLink)
- **Heals** via `HealEvent` (healing beams, self-heal channels)
- **Damages** via `DamageEvent` (drain life, flame channels)
- **Drains resources** per tick via EPIC 16.8 `ResourcePool.TryDeduct()` — channel force-stops if resource depleted
- **Tracks current target** in `ChannelState.CurrentTarget` for VFX beam rendering

### Impact Explosions Work on Projectiles

Projectiles with `ExplodeOnImpact = true` and `ImpactRadius > 0` now create `ModifierExplosionRequest` entities at the hit point. Previously this was a TODO stub.

**Action required:** Verify your grenade/rocket prefabs have `ExplodeOnImpact = true` and `ImpactRadius > 0` on the projectile impact component.

### Weapon Spread Penalizes Movement

`WeaponSpreadSystem` now reads the owner's `PhysicsVelocity.Linear` and applies a movement penalty to weapon spread. Faster movement = more spread.

**Tuning:** Adjust `MovementMultiplier` on `WeaponSpreadComponent` per weapon.

### AI Enemies Now Strafe in Melee

`AICombatBehaviorSystem` now performs lateral strafing when enemies are within melee range of their target, instead of standing still. Direction changes every ~1.5 seconds at 40% of chase speed.

**Action required:** None. This is automatic for all melee AI.

### Stealth Noise is Surface-Aware

`StealthSystem` now reads `GroundSurfaceState.SurfaceId` and applies surface-specific noise multipliers from the EPIC 16.10 `SurfaceGameplayConfig` blob asset. Metal surfaces amplify noise; grass reduces it.

**Action required:** Ensure `SurfaceGameplayConfig` is set up (see SETUP_GUIDE_16.10).

### Lifesteal Modifier Works

`CombatResolutionSystem` now processes `ModifierType.Lifesteal` — applies `HealEvent` to the attacker for `damage * mod.Intensity`.

### Item Equip Duration is Data-Driven

`ItemEquipSystem` now reads `ItemDefinition.EquipDuration` instead of using a hardcoded value. Defaults to 0.5s if the field is zero.

---

## Debug Logging Framework

All debug logging is now gated behind conditional compilation defines. Zero overhead in release builds.

### How It Works

`DebugLog` static class at `Assets/Scripts/Core/DebugLog.cs` provides per-category methods:

| Category | Define | Method |
|----------|--------|--------|
| Combat | `DEBUG_LOG_COMBAT` | `DebugLog.LogCombat()` |
| Items | `DEBUG_LOG_ITEMS` | `DebugLog.LogItems()` |
| AI | `DEBUG_LOG_AI` | `DebugLog.LogAI()` |
| Weapons | `DEBUG_LOG_WEAPONS` | `DebugLog.LogWeapons()` |
| Aggro | `DEBUG_LOG_AGGRO` | `DebugLog.LogAggro()` |
| Surface | `DEBUG_LOG_SURFACE` | `DebugLog.LogSurface()` |
| Input | `DEBUG_LOG_INPUT` | `DebugLog.LogInput()` |
| Movement | `DEBUG_LOG_MOVEMENT` | `DebugLog.LogMovement()` |
| Camera | `DEBUG_LOG_CAMERA` | `DebugLog.LogCamera()` |
| Network | `DEBUG_LOG_NETWORK` | `DebugLog.LogNetwork()` |
| Physics | `DEBUG_LOG_PHYSICS` | `DebugLog.LogPhysics()` |
| **All** | `DEBUG_LOG_ALL` | Enables everything |

### Enabling a Category

1. Open **Edit > Project Settings > Player > Other Settings > Scripting Define Symbols**
2. Add the define (e.g., `DEBUG_LOG_WEAPONS`)
3. Unity recompiles — logs for that category become active
4. Remove the define to disable (zero-cost in builds without the define)

### DemoTools

All files in `Assets/Scripts/DemoTools/` are wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. They compile out of release builds entirely.

---

## Deleted Files

These files were removed as obsolete or temporary:

| File | Why Removed |
|------|------------|
| `Assets/Scripts/Items/Interfaces/EquipmentSlotConfig.cs` | Obsolete — replaced by `EquipmentSlotDefinition` |
| `Assets/Scripts/Visuals/UI/FlashlightHUD.cs` | Temporary — replaced by `FlashlightHUDView` (MVVM) |
| `Assets/Scripts/DemoTools/WeaponPositionDiagnosticSystem.cs` | Diagnostic — no longer needed |
| `Assets/Scripts/DemoTools/PlayerTelemetry.cs` | Diagnostic — no longer needed |
| `Assets/Scripts/DemoTools/BoneNameList.cs` | Diagnostic — no longer needed |
| `VoxelDamageRequest_Legacy` struct (in ExplosiveComponents.cs) | Obsolete — replaced by `DIG.Voxel.VoxelDamageRequest` |
| `FlashlightHUDView.RegisterWithECS` method | Obsolete — replaced by `TryRegisterWithECS` |

---

## Disabled Stub Systems

These systems exist but are intentionally disabled (`state.Enabled = false`):

| System | Why Disabled |
|--------|-------------|
| `ShootableActionSystem` | Superseded by `WeaponFireSystem`. Matches zero entities (WeaponBaker bakes WeaponFireComponent, not ShootableAction) |
| `AddSpawnSystem` | Empty stub, awaiting spawn system design |
| `TelegraphVisualBridge` | Empty stub, awaiting telegraph visual implementation |
| `DamageDebugLogSystem` | Test system, no production use |

---

## Still Deferred

| Item | Reason |
|------|--------|
| Knockback event from combat modifiers | Now implemented in EPIC 16.9 |
| Enemy health bar level/elite text | Requires TextMeshPro on ECS world-space mesh bars |
| Animation-exact throw timing | Throw works via ThrowableActionSystem on input release; animation-exact timing is visual polish |
| Item Systems debug migration | `ItemSetSwitchSystem`, `ItemSpawnSystem`, etc. still use `#pragma warning disable CS0162` pattern instead of `DebugLog` |

---

## Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | Zero warnings from deleted/cleaned files |
| 2 | Firearms | Shoot a hitscan weapon at an enemy | Enemy takes damage (WeaponFireSystem pipeline) |
| 3 | Channel weapon | Use a healing beam or drain channel | Heals/damages target per tick, drains resource |
| 4 | Channel resource depletion | Channel until resource empty | Channel force-stops, weapon idle |
| 5 | Impact explosion | Fire a rocket/grenade at a surface | Explosion AOE damage at hit point |
| 6 | Weapon spread | Walk while firing | Spread increases proportionally to movement speed |
| 7 | AI strafing | Engage melee enemy | Enemy strafes laterally within melee range |
| 8 | Lifesteal | Hit with lifesteal modifier weapon | Attacker health increases |
| 9 | Debug logging | Add `DEBUG_LOG_COMBAT` to scripting defines | Combat logs appear in Console |
| 10 | Release build | Build without debug defines | No debug logs, no DemoTools code |

---

## Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Weapon fire, hitscan, projectiles | SETUP_GUIDE_15.29 |
| Channel action resource drain | SETUP_GUIDE_16.8 |
| Surface-aware stealth noise | SETUP_GUIDE_16.10 |
| Knockback from combat modifiers | SETUP_GUIDE_16.9 |
| **Codebase hygiene & fixes** | **This guide (16.5)** |
