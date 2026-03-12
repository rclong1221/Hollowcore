# Epic 9.7: Performance Profiling Suite

**Status**: ✅ COMPLETE
**Priority**: HIGH  
**Dependencies**: EPIC 8.1 (Core Data Structures), EPIC 9.2 (LOD System)  
**Estimated Time**: 2 days

---

## Goal

Comprehensive profiling tools to:
- Track all voxel system timings
- Identify performance bottlenecks
- Set and enforce performance budgets
- Catch regressions early
- **Monitor LOD system metrics** from Epic 9.2

---

## Quick Start Guide

### 1. Performance Dashboard
1. Go to **DIG > Voxel > Performance Dashboard**.
2. Enter **Play Mode** to see live data.
3. Observe average and peak timings for key systems (`LODSystem`, `MeshSystem`, `GenerationSystem`).
4. **Green/Red** indicators show if a system is within its budget.

### 2. Setting Budgets
1. Create a Budget asset: Right-click > **Create > DIG > Voxel > Performance Budget**.
2. Define budgets for systems (e.g., "MeshSystem" -> 5ms).
3. Assign this asset to the `Budget Config` field in the Performance Dashboard.

### 3. Export Data
1. Click **Export CSV** in the dashboard to save current timings to a file for comparison or history tracking.

---

## Tool 1: Voxel Performance Dashboard

**File**: `Assets/Scripts/Voxel/Editor/PerformanceDashboard.cs`

Real-time window for monitoring voxel system performance.

### Features
- **Live Timings**: Rolling average and peak milliseconds.
- **Budget Tracking**: Visual feedback when systems exceed defined limits.
- **Call Counting**: Tracks frequency of system updates.
- **CSV Export**: Data export for offline analysis.

---

## Tool 2: Voxel Profiler (Core)

**File**: `Assets/Scripts/Voxel/Debug/VoxelProfiler.cs`

Thread-safe static profiling class used by systems.

```csharp
// Usage
VoxelProfiler.BeginSample("MySystem");
// ... work ...
VoxelProfiler.EndSample("MySystem");
```

### Features
- **Dictionary-based storage**: No compilation overhead for adding new samples.
- **Rolling Buffer**: Keeps history for stable averages.
- **Null-safe**: Can be left in production code (though typically wrapped in `#if UNITY_EDITOR` or debug flags if high performance is critical, currently always active for alpha).

---

## Tool 3: Performance Budgets

**File**: `Assets/Scripts/Voxel/Debug/VoxelPerformanceBudget.cs`

ScriptableObject configuration for defining soft limits on frame time.

---

## Implemented Metrics

| System Name | Description | Budget (Default) |
|-------------|-------------|------------------|
| `LODSystem` | LOD distance checks & updates | 0.5 ms |
| `MeshSystem` | Main thread scheduling overhead | 3.0 ms |
| `GenerationSystem` | Job scheduling overhead | 2.0 ms |

> Note: Actual heavy lifting happens in worker threads (Burst Jobs), which are not directly measured here to avoid stalling the main thread. This tool measures the **Main Thread impact** of the voxel systems.

---

## Acceptance Criteria

- [x] Dashboard shows all voxel system timings
- [x] Budgets defined and visually indicated
- [x] Export functionality for comparison
- [x] Voxel systems (LOD, Mesh, Gen) instrumented with profiler calls
