# EPIC 9.10: Quick Setup & Test Automation (Advanced)

**Status**: ✅ COMPLETED  
**Priority**: MEDIUM  
**Dependencies**: All EPIC 9 sub-epics, EPIC 8.16  
**Estimated Time**: 2 days  
**Last Updated**: 2025-12-20

---

## 🚀 Quick Start Guide

### One-Click Complete Setup
1. Open **DIG → Quick Setup → Open Setup Dashboard**
2. Click **"Setup All (Epic 9)"** button
3. Visual materials, LOD config, and profiler config are now ready!

### Individual Setups
| Menu Item | What It Creates |
|-----------|-----------------|
| `Create Complete Advanced Setup` | All Epic 9 assets in one click |
| `Create Visual Materials` | VoxelVisualMaterial assets for enhanced rendering |
| `Create LOD Config` | Balanced LOD configuration |
| `Create LOD Config (Performance)` | Aggressive LOD for weak hardware |
| `Create LOD Config (Quality)` | Extended view distance for high-end PCs |
| `Create Profiler Config` | Profiler thresholds and settings |
| `Validate Advanced Setup` | Checks all advanced assets |
| `Delete All Advanced Assets` | Cleanup |

---

## 📖 Component Reference

### AdvancedQuickSetup.cs
Main quick setup utility class for Epic 9 advanced features:

| Method | Menu Path | Purpose |
|--------|-----------|---------|
| `CreateCompleteAdvancedSetup()` | DIG/Quick Setup/Advanced/Create Complete Advanced Setup | Creates everything |
| `CreateVisualMaterials()` | DIG/Quick Setup/Advanced/Create Visual Materials | Visual material configs |
| `CreateLODConfig()` | DIG/Quick Setup/Advanced/Create LOD Config | Balanced LOD preset |
| `CreateLODConfigPerformance()` | DIG/Quick Setup/Advanced/Create LOD Config (Performance) | Aggressive LOD |
| `CreateLODConfigQuality()` | DIG/Quick Setup/Advanced/Create LOD Config (Quality) | High-quality LOD |
| `CreateProfilerConfig()` | DIG/Quick Setup/Advanced/Create Profiler Config | Profiler settings |
| `ValidateAdvancedSetup()` | DIG/Quick Setup/Advanced/Validate Advanced Setup | Validation |
| `DeleteAllAdvancedAssets()` | DIG/Quick Setup/Advanced/Delete All Advanced Assets | Cleanup |

### VoxelProfilerConfig.cs
ScriptableObject for profiler thresholds:

```csharp
[CreateAssetMenu(menuName = "DIG/Voxel/Profiler Config")]
public class VoxelProfilerConfig : ScriptableObject
{
    public float FrameBudgetMs = 16.6f;      // 60 FPS target
    public float WarningThresholdMs = 8f;    // Yellow in dashboard
    public float CriticalThresholdMs = 14f;  // Red in dashboard
    public int SampleHistoryCount = 60;      // Rolling average window
    public bool EnableAutoCapture = true;    // Auto-profile on spikes
    public float AutoCaptureThresholdMs = 20f;
}
```

### VoxelSetupDashboard (Updated)
Now includes Epic 9 section:

```
┌─────────────────────────────────────────────────────────┐
│  Voxel Setup Dashboard                        [Refresh] │
├─────────────────────────────────────────────────────────┤
│  ████████████████████  Setup Progress: 12/12 (100%)     │
│  ✅ All systems configured! Ready to use.               │
├─────────────────────────────────────────────────────────┤
│  ▼ Core Voxel System (Epic 8)                           │
│    ✅ Material Registry      13 materials defined       │
│    ✅ Loot Prefabs           10 loot prefabs            │
│    ✅ Texture Config         Texture2DArray ready       │
│                                           [Setup All]   │
├─────────────────────────────────────────────────────────┤
│  ▼ Advanced Features (Epic 9)          ← NEW!           │
│    ✅ Visual Materials       11 visual materials        │
│    ✅ LOD Config             Level of Detail settings   │
│    ✅ Profiler Config        Performance profiling      │
│                                           [Setup All]   │
├─────────────────────────────────────────────────────────┤
│  ▼ Geology & Resources (Epic 10)                        │
│    ✅ World Generation       Master config loaded       │
│    ✅ Strata Profile         Rock layer configuration   │
│    ✅ Ore Definitions        8 ore types defined        │
│                                           [Setup All]   │
└─────────────────────────────────────────────────────────┘
```

---

## 🛠️ Setup Guide for Designers

### Understanding LOD Presets

| Preset | Use Case | LOD0 Range | Max View |
|--------|----------|------------|----------|
| **Balanced** (Default) | Most PCs | 0-32m | 256m |
| **Performance** | Mobile/Weak PCs | 0-16m | 128m |
| **Quality** | High-end PCs | 0-64m | 512m |

### LOD Level Breakdown

| Level | Resolution | Colliders | Purpose |
|-------|------------|-----------|---------|
| LOD 0 | Full (1:1) | ✅ ON | Near player, mining area |
| LOD 1 | Half (2:1) | ✅ ON | Walking distance |
| LOD 2 | Quarter (4:1) | ❌ OFF | Visible background |
| LOD 3 | Eighth (8:1) | ❌ OFF | Distant horizon |

### Visual Materials Explained

Visual materials add these properties to basic gameplay materials:

| Property | Purpose | Default Range |
|----------|---------|---------------|
| **Smoothness** | How shiny the surface is | 0.1-0.95 |
| **Metallic** | Metal-like reflections | 0-0.95 |
| **Tint** | Color adjustment | Per-material |
| **AOStrength** | Ambient occlusion intensity | 0.5 |
| **DetailStrength** | Close-up texture detail | 0-0.3 |
| **DetailScale** | Detail texture tiling | 8 |

### Created Visual Materials

| Material | Smoothness | Metallic | Notable |
|----------|------------|----------|---------|
| Stone | 0.2 | 0 | Has detail texture |
| Dirt | 0.1 | 0 | Has detail texture |
| Iron | 0.4 | 0.6 | Metallic ore |
| Gold | 0.8 | 0.9 | Very shiny metal |
| Copper | 0.5 | 0.7 | Metallic ore |
| Granite | 0.3 | 0.1 | Has detail texture |
| Basalt | 0.15 | 0.05 | Has detail texture |
| Coal | 0.1 | 0 | Matte black |
| Silver | 0.85 | 0.95 | Highly reflective |
| Diamond | 0.95 | 0.1 | Very shiny, non-metallic |
| Mythril | 0.9 | 0.8 | Fantasy metal |

---

## 💻 Integration Guide for Developers

### Using Visual Materials

```csharp
// Load visual material
var visualMat = Resources.Load<VoxelVisualMaterial>("VisualMaterials/Visual_Stone");

// Access properties
float smoothness = visualMat.Smoothness;
float metallic = visualMat.Metallic;
Texture2D albedo = visualMat.Albedo;

// Link to gameplay material
var gameplayMat = Resources.Load<VoxelMaterialDefinition>("VoxelMaterials/Mat_Stone");
gameplayMat.VisualMaterial = visualMat;
```

### Using LOD Config

```csharp
// Load config
var lodConfig = Resources.Load<VoxelLODConfig>("VoxelLODConfig");

// Get LOD level for distance
float distance = 100f;
int lodLevel = lodConfig.GetLODLevel(distance);

// Check with hysteresis (prevents flickering)
int currentLod = 1;
int newLod = lodConfig.GetLODLevelWithHysteresis(distance, currentLod);

// Check if collider should exist
bool needsCollider = lodConfig.ShouldHaveCollider(lodLevel);
```

### Using Profiler Config

```csharp
// Load config
var profilerConfig = Resources.Load<VoxelProfilerConfig>("VoxelProfilerConfig");

// Check thresholds
float frameTime = 12f; // ms
if (frameTime > profilerConfig.CriticalThresholdMs)
{
    Debug.LogWarning("Frame budget exceeded!");
}
else if (frameTime > profilerConfig.WarningThresholdMs)
{
    Debug.Log("Frame time getting high");
}
```

### Folder Structure

```
Assets/
├── Resources/
│   ├── VoxelLODConfig.asset           ← LOD settings
│   ├── VoxelProfilerConfig.asset      ← Profiler thresholds
│   └── VisualMaterials/
│       ├── Visual_Stone.asset
│       ├── Visual_Dirt.asset
│       ├── Visual_Iron.asset
│       └── ... (11 total)
```

---

## ✅ Acceptance Criteria

- [x] All menu items create assets without errors
- [x] Visual materials link to gameplay materials
- [x] LOD config provides multiple presets
- [x] Profiler config loads at runtime
- [x] Dashboard shows Epic 9 status section
- [x] Validation checks all advanced assets
- [x] Delete option removes only advanced assets

---

## 📁 Files Created

| File | Purpose |
|------|---------|
| `Assets/Scripts/Voxel/Editor/AdvancedQuickSetup.cs` | Menu items and asset creation |
| `Assets/Scripts/Voxel/Editor/VoxelSetupDashboard.cs` | Updated with Epic 9 section |

---

## 🔗 Related Epics

- **EPIC 8.16** (Core Quick Setup): Base setup infrastructure
- **EPIC 9.1** (Visual Refinement): Visual material definitions
- **EPIC 9.2** (LOD System): LOD config usage
- **EPIC 9.7** (Performance Profiling): Profiler integration

---

## Menu Structure

```
DIG/
├── Quick Setup/
│   ├── Open Setup Dashboard
│   ├── Core/
│   │   ├── Create Complete Demo
│   │   ├── Create Material & Loot Setup
│   │   ├── Create Texture Config
│   │   ├── Create Collision Test Objects
│   │   ├── Validate Current Setup
│   │   └── Delete All Quick Setup Assets
│   ├── Advanced/
│   │   ├── Create Complete Advanced Setup  ← NEW
│   │   ├── Create Visual Materials          ← NEW
│   │   ├── Create LOD Config               ← NEW
│   │   ├── Create LOD Config (Performance) ← NEW
│   │   ├── Create LOD Config (Quality)     ← NEW
│   │   ├── Create Profiler Config          ← NEW
│   │   ├── Validate Advanced Setup         ← NEW
│   │   └── Delete All Advanced Assets      ← NEW
│   └── Generation/
│       ├── Create Complete Geology Setup
│       ├── Create Strata Profile Only
│       ├── Create Sample Ores Only
│       └── Create Depth Curve Only
```
