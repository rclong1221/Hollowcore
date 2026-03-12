# EPIC 21.8: Performance Optimization (9.9)

**Status**: 🔲 NOT STARTED  
**Priority**: HIGH  
**Estimated Effort**: 4-5 days  
**Dependencies**: 21.7 (Profiling Suite)

---

## Goal

Complete EPIC 9.9 - Apply optimizations identified by profiling to achieve target performance.

---

## Target Performance

| Metric | Current | Target |
|--------|---------|--------|
| Chunk generation | ~8ms | < 5ms |
| Chunk meshing | ~12ms | < 8ms |
| LOD transition | ~2ms | < 1ms |
| Memory per chunk | ~80KB | < 64KB |
| Visible chunks | 200 | 300+ |

---

## Tasks

### Phase 1: Job Optimization
- [ ] Audit all `Schedule().Complete()` calls (blocking)
- [ ] Implement double-buffering for generation
- [ ] Reduce job overhead with batch processing
- [ ] Profile and optimize hot paths

### Phase 2: Memory Optimization
- [ ] Implement native collection pooling (expand existing)
- [ ] Reduce per-chunk allocations
- [ ] Use FixedList where possible
- [ ] Compress mesh data

### Phase 3: Burst Audit
- [ ] Ensure all jobs are Burst-compiled
- [ ] Remove Burst blockers (managed types)
- [ ] Use math operations over Unity operations
- [ ] Vectorization where applicable

### Phase 4: LOD Optimization
- [ ] Faster LOD calculation (SIMD)
- [ ] Reduce LOD mesh generation cost
- [ ] Aggressive LOD culling
- [ ] Pre-bake low LOD meshes

### Phase 5: GPU Optimization
- [ ] Batch draw calls
- [ ] GPU instancing for decorators
- [ ] Reduce shader variants
- [ ] LOD-based shader complexity

---

## Known Issues to Address

| Issue | Priority | Approach |
|-------|----------|----------|
| Synchronous job completion | HIGH | Double-buffer |
| Per-chunk allocations | HIGH | Pooling |
| Mesh rebuild frequency | MEDIUM | Dirty batching |
| LOD hysteresis overhead | LOW | Simplified math |

---

## Success Criteria

- [ ] All performance targets met
- [ ] 60 FPS with 300 visible chunks
- [ ] No frame spikes > 20ms
- [ ] Memory stable over 30+ minutes
- [ ] Profiler shows no major bottlenecks
