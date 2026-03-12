# EPIC 10.12: ECS Simulation Group Optimization

**Status**: 🔴 NOT STARTED  
**Priority**: HIGH  
**Dependencies**: EPIC 10.11 (Completed), Performance Capture Tool  

---

## Problem Statement
Performance captures show `ECS.SimulationGroup` consuming **~13.8ms per frame**, but `ChunkPhysics` only accounts for **2.9ms** of that.
There is **~11ms of unaccounted execution time** frame after frame. This prevents us from reaching a stable 60 FPS (16.6ms budget).

**Suspects**:
1. **Unity Physics**: Solver, Broadphase, Narrowphase (often expensive with many colliders)
2. **NetCode**: GhostUpdateSystem, Prediction, Command sending/receiving
3. **Transforms**: Parent/Child hierarchy updates
4. **Gameplay Logic**: Voxel interaction or other custom systems

---

## Objectives
1. **Identify the Bottleneck**: Use `ProfilerRecorder` or Unity Profiler to find exactly which SystemGroup is eating the 11ms.
2. **Optimize or Throttle**: Once identified, apply throttles, jobification, or algorithmic improvements.
3. **Budget Compliance**: Bring `ECS.SimulationGroup` down to **< 8ms**.

---

## Tasks

### Task 10.12.1: Granular Profiling
- [ ] Fix `PerformanceCaptureSession.cs` markers to correctly capture Unity internal groups
- [ ] Verify `PhysicsSystemGroup` execution time
- [ ] Verify `NetworkTimeSystemGroup` / `GhostUpdateSystemGroup` execution time
- [ ] Create a breakdown of the 13.8ms

### Task 10.12.2: Physics Optimization (If Bottleneck)
- [ ] Tune `PhysicsStep` (is it running too often?)
- [ ] Check Broadphase config (MultiBox vs Sweep)
- [ ] Reduce active RigidBody count if necessary

### Task 10.12.3: NetCode Optimization (If Bottleneck)
- [ ] Check Ghost prediction cost
- [ ] Profile bandwidth usage impact on CPU (serialization cost)

### Task 10.12.4: Transform Optimization
- [ ] Check `TransformSystemGroup` cost
- [ ] Optimize hierarchy depth if needed

---

## Acceptance Criteria
- [ ] `ECS.SimulationGroup` breakdown is fully understood (no "mystery" time > 1ms)
- [ ] Total `ECS.SimulationGroup` time reduced to **< 10ms** (primary target)
