# EPIC 8.16: Quick Setup & Test Automation

**Status**: ✅ COMPLETED  
**Priority**: MEDIUM  
**Dependencies**: All EPIC 8 sub-epics  
**Estimated Time**: 2 days  
**Last Updated**: 2025-12-20

---

## 🚀 Quick Start Guide

### One-Click Complete Setup
1. Open **DIG → Quick Setup → Open Setup Dashboard**
2. Click **"Complete Demo Setup (Epic 8 + 10)"**
3. Enter Play Mode - you now have a fully textured, mineable voxel world!

### Individual Setups
| Menu Item | What It Creates |
|-----------|-----------------|
| `Create Material & Loot Setup` | Material definitions + loot prefabs + registry |
| `Create Texture Config` | Procedural textures + Texture2DArray |
| `Create Collision Test Objects` | Drop test sphere for physics verification |
| `Validate Current Setup` | Checks all required assets |

---

## 📖 Component Reference

### VoxelQuickSetup.cs
Main quick setup utility class providing menu items:

| Method | Menu Path | Purpose |
|--------|-----------|---------|
| `CreateCompleteDemoSetup()` | DIG/Quick Setup/Core/Create Complete Demo | Creates everything |
| `CreateMaterialAndLootSetup()` | DIG/Quick Setup/Core/Create Material & Loot Setup | Materials + prefabs |
| `CreateTextureConfig()` | DIG/Quick Setup/Core/Create Texture Config | Textures + array |
| `CreateCollisionTestObjects()` | DIG/Quick Setup/Core/Create Collision Test Objects | Physics test |
| `ValidateSetup()` | DIG/Quick Setup/Core/Validate Current Setup | Validation |
| `DeleteAllQuickSetupAssets()` | DIG/Quick Setup/Core/Delete All Quick Setup Assets | Cleanup |

### VoxelSetupDashboard.cs
Unified editor window showing all setup status:

```
┌─────────────────────────────────────────────────────────┐
│  Voxel Setup Dashboard                        [Refresh] │
├─────────────────────────────────────────────────────────┤
│  ████████████████░░░░  Setup Progress: 7/9 (78%)        │
│  ⚠️ Some optional systems not configured.               │
├─────────────────────────────────────────────────────────┤
│  ▼ Core Voxel System (Epic 8)                           │
│    ✅ Material Registry      Defines all voxel types    │
│    ✅ Material Definitions   13 materials defined       │
│    ✅ Loot Prefabs           12 loot prefabs            │
│    ✅ Texture Config         Texture2DArray ready       │
│    ✅ Texture Array          Built texture array        │
│                                     [Setup All (Epic 8)]│
├─────────────────────────────────────────────────────────┤
│  ▼ Geology & Resources (Epic 10)                        │
│    ✅ World Generation       Master config loaded       │
│    ✅ Strata Profile         Rock layer configuration   │
│    ✅ Ore Definitions        8 ore types defined        │
│    ✅ Depth Curve            Rarity curves by depth     │
│                                    [Setup All (Epic 10)]│
├─────────────────────────────────────────────────────────┤
│  Quick Actions                                          │
│  [Complete Demo Setup]  [Validate All Systems]          │
│  [Strata Visualizer]  [Ore Distribution]  [Streaming]   │
└─────────────────────────────────────────────────────────┘
```

---

## 🛠️ Setup Guide for Designers

### Creating a Working Voxel Demo

**Fastest Method:**
1. Go to **DIG → Quick Setup → Open Setup Dashboard**
2. Click **"Complete Demo Setup (Epic 8 + 10)"**
3. Add a `VoxelWorldAuthoring` component to an empty GameObject in your scene
4. Press Play!

**What's Created:**

| Asset Type | Count | Location |
|------------|-------|----------|
| Material Definitions | 13 | `Resources/VoxelMaterials/` |
| Loot Prefabs | 12 | `Prefabs/Loot/` |
| Procedural Textures | 6 | `Textures/Voxel/` |
| Material Registry | 1 | `Resources/VoxelMaterialRegistry.asset` |
| Texture Config | 1 | `Resources/VoxelTextureConfig.asset` |
| Strata Profile | 1 | `Resources/Geology/DefaultStrataProfile.asset` |
| Ore Definitions | 8 | `Resources/Geology/Ores/` |
| Depth Curve | 1 | `Resources/Geology/DefaultDepthCurve.asset` |
| World Config | 1 | `Resources/WorldGenerationConfig.asset` |

### Default Materials Created

| Material | ID | Hardness | Loot | Texture |
|----------|----|---------:|------|---------|
| Air | 0 | - | ❌ | Transparent |
| Stone | 1 | 1.0s | ✅ | Gray |
| Dirt | 2 | 0.5s | ✅ | Brown |
| Iron | 3 | 2.0s | ✅ | Orange-brown |
| Gold | 4 | 2.5s | ✅ | Gold |
| Copper | 5 | 1.8s | ✅ | Orange |
| Granite | 10 | 1.5s | ✅ | Pinkish gray |
| Basalt | 11 | 1.8s | ✅ | Dark gray |
| Coal | 20 | 0.8s | ✅ | Black |
| Tin | 21 | 1.5s | ✅ | Silver |
| Silver | 22 | 2.2s | ✅ | White |
| Diamond | 23 | 4.0s | ✅ | Cyan |
| Mythril | 24 | 5.0s | ✅ | Blue |

### Customizing Materials

1. Navigate to `Assets/Resources/VoxelMaterials/`
2. Select any `Mat_*.asset` file
3. Modify:
   - **Hardness**: Mining time in seconds
   - **Drop Chance**: 0-1 probability
   - **Min/Max Drop Count**: Range of items spawned
   - **Debug Color**: Editor visualization color

### Creating Custom Loot

1. Create a new GameObject (cube, sphere, or custom mesh)
2. Add a **Rigidbody** component
3. Set **Mass** to ~0.5, **Drag** to ~1
4. Add a **Collider** (Box, Sphere, or Mesh)
5. Save as Prefab in `Assets/Prefabs/Loot/`
6. Assign to material's **Loot Prefab** field

---

## 💻 Integration Guide for Developers

### Using the Quick Setup Programmatically

```csharp
using DIG.Voxel.Editor;

// Trigger material setup
VoxelQuickSetup.CreateMaterialAndLootSetup();

// Access created assets
var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
var texConfig = Resources.Load<VoxelTextureConfig>("VoxelTextureConfig");
```

### Adding Custom Materials to Quick Setup

Edit `VoxelQuickSetup.cs` and add to `materialDefs` array:

```csharp
var materialDefs = new (string name, byte id, float hardness, bool mineable, Color color, bool needsLoot)[]
{
    // ... existing materials ...
    ("CustomOre", 30, 2.0f, true, new Color(0.5f, 0f, 0.5f), true), // Add custom
};
```

### Validation API

```csharp
// Check setup status
var registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
if (registry == null)
{
    Debug.LogError("VoxelMaterialRegistry not found! Run Quick Setup.");
    return;
}

// Validate material has loot
var mat = registry.GetMaterial(3); // Iron
if (mat != null && mat.LootPrefab == null)
{
    Debug.LogWarning($"Material {mat.MaterialName} missing loot prefab");
}
```

### Folder Structure

```
Assets/
├── Resources/
│   ├── VoxelMaterialRegistry.asset     ← Material lookup
│   ├── VoxelTextureConfig.asset        ← Texture array config
│   ├── WorldGenerationConfig.asset     ← Master generation config
│   ├── VoxelMaterials/
│   │   ├── Mat_Air.asset
│   │   ├── Mat_Stone.asset
│   │   ├── Mat_Dirt.asset
│   │   └── ... (13 total)
│   └── Geology/
│       ├── DefaultStrataProfile.asset
│       ├── DefaultDepthCurve.asset
│       └── Ores/ (8 ore definitions)
├── Prefabs/
│   └── Loot/
│       ├── Loot_Stone.prefab
│       ├── Loot_Iron.prefab
│       └── ... (12 total)
└── Textures/
    └── Voxel/
        ├── Tex_Stone.png
        ├── Tex_Dirt.png
        └── ... (6 procedural textures)
```

---

## ✅ Acceptance Criteria

- [x] `Create Complete Demo` works on a fresh project clone
- [x] All menu items create assets without errors
- [x] Created assets are properly linked together
- [x] Validation reports any missing or broken references
- [x] Delete option removes only quick-setup-generated assets
- [x] Each sub-setup can run independently
- [x] Console logs progress for debugging
- [x] Setup Dashboard shows real-time status
- [x] One-click access to visualization tools

---

## 📁 Files Created

| File | Purpose |
|------|---------|
| `Assets/Scripts/Voxel/Editor/VoxelQuickSetup.cs` | Main quick setup menu class |
| `Assets/Scripts/Voxel/Editor/VoxelSetupDashboard.cs` | Unified status dashboard window |

---

## 🔗 Related Epics

- **EPIC 8.7** (Voxel API): Material definitions and registry
- **EPIC 8.10** (Materials & Texturing): Texture config and array
- **EPIC 8.15** (Loot & Bug Fixes): Loot prefabs and spawning
- **EPIC 9.10** (Advanced Quick Setup): Visual materials, LOD, network
- **EPIC 10.1** (Geology): Strata profiles and ore definitions

---

## Menu Structure

```
DIG/
├── Quick Setup/
│   ├── Open Setup Dashboard           ← Unified status view
│   ├── Core/
│   │   ├── Create Complete Demo       ← Does everything
│   │   ├── Create Material & Loot Setup
│   │   ├── Create Texture Config
│   │   ├── Create Collision Test Objects
│   │   ├── Validate Current Setup
│   │   └── Delete All Quick Setup Assets
│   ├── Advanced/
│   │   ├── Create Complete Advanced Setup
│   │   ├── Create Visual Materials
│   │   ├── Create LOD Config
│   │   ├── Create Profiler Config
│   │   └── Validate Advanced Setup
│   └── Generation/
│       ├── Create Complete Geology Setup
│       ├── Create Strata Profile Only
│       ├── Create Sample Ores Only
│       └── Create Depth Curve Only
```
