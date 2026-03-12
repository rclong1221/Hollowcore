# Setup Guide: EPIC 13.18 (Surface Effects Parity)

## Overview

This epic brings the Surface system to feature parity with production-grade implementations, adding:
- **SurfaceManager** - Centralized effect spawning (VFX, Audio, Decals)
- **DecalManager** - Pooled bullet holes and scorch marks
- **Footstep Detection** - Ground raycast for accurate surface audio
- **Projectile Impacts** - Automatic VFX/decals on bullet hits

---

## Quick Start (For Designers)

### Step 1: Add Managers to Scene
1. Create empty GameObject: `GameObject > Create Empty`
2. Name it **"SurfaceManager"**
3. Add component: `Audio.Systems.SurfaceManager`
4. Create another empty GameObject named **"DecalManager"**
5. Add component: `Audio.Systems.DecalManager`

### Step 2: Create DecalData Assets
1. Right-click in Project: `Create > DIG > DecalData`
2. Create assets for each type:
   - `BulletHole_Default.asset`
   - `BulletHole_Metal.asset`
   - `ScorchMark.asset`
   - `Footprint_Mud.asset`

### Step 3: Configure DecalData
| Field | Typical Value | Description |
|-------|---------------|-------------|
| DecalMaterial | URP Decal Material | Must be compatible with URP Decal Projector |
| Size | 0.1 - 0.3 | World units |
| SizeVariation | 0.1 | 10% random size variation |
| ProjectionDepth | 0.5 | How deep into surface |
| RandomRotation | 360 | Full random rotation |
| Lifetime | 0 | 0 = permanent (relies on pool recycling) |
| FadeDuration | 1.0 | Seconds to fade before removal |

### Step 4: Assign Decals to SurfaceMaterials
1. Locate your `SurfaceMaterial` assets (e.g., `Metal.asset`, `Dirt.asset`)
2. In Inspector, find **"Decals (EPIC 13.18.2)"** section
3. Assign `ImpactDecal` (bullet holes)
4. Optionally assign `FootprintDecal`
5. Toggle `AllowFootprints` as needed

---

## URP Decal Setup

### Enable Decal Feature
1. Open your URP Renderer Asset (e.g., `ForwardRenderer.asset`)
2. Click **"Add Renderer Feature"**
3. Select **"Decal"**
4. Configure:
   - Technique: **DBuffer** (recommended) or Screen Space
   - Max Draw Distance: **100**

### Create Decal Material
1. Create Material: `Create > Material`
2. Shader: `Shader Graphs/Decal`
3. Assign albedo texture (bullet hole, scorch, etc.)
4. Adjust blend modes as needed

---

## Component Fields Reference

### SurfaceManager

| Field | Type | Description |
|-------|------|-------------|
| AudioManager | AudioManager | Auto-found if null |
| DecalManager | DecalManager | Auto-found if null |
| MinImpactVelocity | float | Min velocity to spawn effects |

### DecalManager

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| MaxDecals | int | 100 | Pool size (oldest recycled) |
| FallbackDecal | DecalData | null | Default if none specified |

### DecalData

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| DecalMaterial | Material | null | URP Decal material |
| Size | float | 0.2 | Base size in world units |
| SizeVariation | float | 0.1 | Random variation (0-0.5) |
| ProjectionDepth | float | 0.5 | Projection depth |
| RandomRotation | float | 360 | Random rotation range |
| Lifetime | float | 0 | 0 = permanent |
| FadeDuration | float | 1.0 | Fade out time |

### SurfaceMaterial (New Fields)

| Field | Type | Description |
|-------|------|-------------|
| ImpactDecal | DecalData | Decal for bullet impacts |
| FootprintDecal | DecalData | Decal for footprints |
| AllowFootprints | bool | Enable footprint spawning |

---

## API Reference

### Spawning Effects (Code)

```csharp
// Spawn impact effect (VFX + Audio + Decal)
SurfaceManager.Instance.SpawnEffect(
    position: hitPoint,
    normal: hitNormal,
    surfaceMaterialId: materialId,
    intensity: 1.0f);

// Spawn from RaycastHit (auto-resolves material)
SurfaceManager.Instance.SpawnEffectFromHit(hit, intensity: 1.0f);

// Spawn footprint (if surface allows)
SurfaceManager.Instance.SpawnFootprint(
    position: footPosition,
    footRotation: playerRotation,
    surfaceMaterialId: groundMaterialId,
    flipFootprint: isRightFoot);
```

### Direct Decal Spawning

```csharp
// Spawn custom decal
DecalManager.Instance.SpawnDecal(
    data: myDecalData,
    position: worldPos,
    rotation: decalRotation,
    lifetimeOverride: 30f);

// Clear all decals
DecalManager.Instance.ClearAllDecals();

// Get active count
int count = DecalManager.Instance.GetActiveDecalCount();
```

---

## Verification

### Test: Impact Effects
1. Enter Play Mode
2. Shoot a surface with `SurfaceMaterialId` component
3. ✅ VFX spawns at impact point
4. ✅ Audio plays
5. ✅ Decal appears (if configured)

### Test: Decal Pooling
1. Fire 150+ bullets at a wall
2. ✅ Oldest decals disappear after 100
3. ✅ No performance spikes

### Test: Footsteps
1. Walk across surfaces with different `SurfaceMaterialId`
2. ✅ Correct audio for each surface
3. ✅ Console shows detected material IDs (if logging enabled)

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No decals appearing | Enable Decal Renderer Feature in URP |
| Decals floating | Check ProjectionDepth, ensure rotation faces surface |
| Wrong surface audio | Add `SurfaceMaterialId` component to floor objects |
| No impact VFX | Assign VFXPrefab in SurfaceMaterial asset |
| SurfaceManager null | Add SurfaceManager GameObject to scene |

---

## Files Reference

### Core Systems
| File | Description |
|------|-------------|
| `SurfaceManager.cs` | Centralized effect spawning |
| `DecalManager.cs` | Pooled decal projectors |
| `FootstepSystem.cs` | Ground raycast detection |
| `ProjectileImpactPresentationSystem.cs` | Impact VFX spawning |

### Data
| File | Description |
|------|-------------|
| `DecalData.cs` | Decal configuration ScriptableObject |
| `SurfaceMaterial.cs` | Surface audio/VFX/decal data |

### Services
| File | Description |
|------|-------------|
| `SurfaceDetectionService.cs` | Material resolution from entities |
| `SurfaceMaterialRegistry.cs` | Material ID lookup |
