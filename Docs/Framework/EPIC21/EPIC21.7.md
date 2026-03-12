# EPIC 21.7: Performance Profiling Suite (9.7)

**Status**: 🔲 NOT STARTED  
**Priority**: MEDIUM  
**Estimated Effort**: 3 days  
**Dependencies**: 21.1 (Networking Split)

---

## Goal

Complete EPIC 9.7 - Create performance profiling tools that help users optimize their voxel implementations.

---

## Current State

Existing:
- `PerformanceDashboard` - Basic metrics
- `LODVisualizerWindow` - LOD stats

Missing:
- Per-system timing breakdown
- Memory allocation tracking
- Frame budget analysis
- Bottleneck identification

---

## Tasks

### Phase 1: System Timing Profiler
- [ ] Measure time per voxel system
- [ ] Track job scheduling overhead
- [ ] Show main thread vs job time
- [ ] Historical graph (last 100 frames)

### Phase 2: Memory Profiler
- [ ] Track NativeArray allocations
- [ ] Measure blob memory usage
- [ ] Mesh buffer sizes
- [ ] Pool utilization stats

### Phase 3: Frame Budget Analyzer
- [ ] Show budget utilization %
- [ ] Identify budget exceeded frames
- [ ] Suggest optimizations
- [ ] Compare to target FPS

### Phase 4: Bottleneck Detector
- [ ] CPU vs GPU bound detection
- [ ] Generation vs Meshing breakdown
- [ ] Network overhead (if enabled)
- [ ] Recommendations engine

### Phase 5: Export & CI
- [ ] Export metrics to JSON
- [ ] Benchmark mode (automated runs)
- [ ] Regression detection
- [ ] Integration with Unity Profiler

---

## Dashboard Layout

```
┌─────────────────────────────────────────┐
│ VOXEL PERFORMANCE DASHBOARD             │
├─────────────────────────────────────────┤
│ Frame Time: 8.2ms / 16.6ms (50% budget) │
│ ████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │
├─────────────────────────────────────────┤
│ System Breakdown:                       │
│   Generation: 2.1ms ██░░░               │
│   Meshing:    3.4ms ████░               │
│   Physics:    1.2ms █░░░░               │
│   LOD:        0.8ms █░░░░               │
├─────────────────────────────────────────┤
│ Memory: 128MB chunks, 64MB meshes       │
│ Chunks: 245 loaded, 12 dirty            │
└─────────────────────────────────────────┘
```

---

## Success Criteria

- [ ] All metrics update in real-time
- [ ] < 0.5ms profiler overhead
- [ ] Clear visualization of bottlenecks
- [ ] Actionable recommendations
- [ ] Export functionality working
