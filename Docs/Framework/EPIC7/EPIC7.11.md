### Epic 7.11: Advanced Optimization Techniques
**Priority**: MEDIUM (implement only if profiling shows need)
**Goal**: Implement cutting-edge optimization strategies for maximum performance when Unity's built-in optimizations are insufficient

**IMPORTANT: Optimization Philosophy**
Before implementing ANY optimization in this epic:
1. ✅ **Profile first**: Use Unity Profiler to identify actual bottlenecks
2. ✅ **Measure baseline**: Record current performance metrics
3. ✅ **Implement incrementally**: One optimization at a time
4. ✅ **Verify gains**: Measure improvement (must be >10% to justify complexity)
5. ✅ **Test correctness**: Ensure optimization doesn't change behavior

**Unity already provides (don't duplicate)**:
- Burst auto-vectorization (SIMD for math operations)
- Job system parallelization (multi-core scaling)
- BVH spatial partitioning (O(log n) queries)
- Chunk iteration (cache-friendly data access)

**When to implement these optimizations**:
| Optimization | Implement When |
|-------------|----------------|
| SIMD Vectorization | Burst Inspector shows scalar code in hot loop |
| Branch Prediction | >5% branch misprediction in hardware counters |
| Transform Caching | Entity lookup takes >20% of system time |
| Async Physics | Physics wait time >2ms per frame |
| GPU Acceleration | >200 players AND CPU bottlenecked |
| Network-Aware | Bandwidth exceeds 200KB/s for collision |
| Micro-Optimizations | After all other optimizations applied |

**Sub-Epic 7.11.1: SIMD Vectorization** *(Not Started)*
**Goal**: Ensure maximum SIMD utilization for math-heavy collision code
**Design Notes**:
- Unity.Mathematics + Burst already provides SIMD for most operations
- Manual SIMD only needed for custom algorithms not covered by Unity
- Use Burst Inspector to verify auto-vectorization is working

**Verification Process**:
1. Open Burst Inspector (Jobs → Burst → Open Inspector)
2. Find collision job (e.g., `CollisionBroadphaseJob`)
3. Look for SIMD instructions: `movaps`, `mulps`, `addps` (SSE) or `vfmadd` (AVX)
4. If scalar code found (`movss`, `mulss`), investigate why

**Tasks**:
- [ ] **Verify Burst auto-vectorization**:
  - [ ] Inspect `CollisionBroadphaseJob` assembly output
  - [ ] Confirm `float3` operations use SIMD
  - [ ] Document any scalar fallbacks found
- [ ] **Implement SoA layout if needed**:
  - [ ] Convert `NativeArray<float3>` to `NativeArray<float>` x3
  - [ ] Process X, Y, Z separately for better vectorization
  - [ ] Only for loops processing 100+ entities
- [ ] **Add explicit SIMD for hot loops** (if auto-vectorization fails):
  - [ ] Use `Unity.Burst.Intrinsics` for manual SIMD
  - [ ] Support SSE4.1 (x86), NEON (ARM)
  - [ ] Fallback to scalar for unsupported platforms
- [ ] **Profile SIMD vs scalar**:
  - [ ] Run benchmark with forced scalar (disable Burst)
  - [ ] Compare to Burst-compiled (expected 2-4x faster)
  - [ ] Document speedup for each job

**Sub-Epic 7.11.2: Branch Prediction Optimization** *(Not Started)*
**Goal**: Minimize branch mispredictions in collision hot loops
**Design Notes**:
- Modern CPUs predict branch outcomes; mispredictions cost ~15-20 cycles
- Branchless code uses `math.select()` to avoid conditional jumps
- Sorting data by predicted outcome improves prediction accuracy

**Common Branching Patterns to Optimize**:
```csharp
// BEFORE (branching)
if (distance < threshold)
    ApplyCollision();

// AFTER (branchless mask)
float mask = math.select(0f, 1f, distance < threshold);
force *= mask;  // Zero force if no collision
```

**Tasks**:
- [ ] **Profile branch misprediction rate**:
  - [ ] Use hardware performance counters (Intel VTune, AMD μProf)
  - [ ] Target: <5% misprediction rate
  - [ ] Identify worst offenders
- [ ] **Convert hot branches to branchless**:
  - [ ] Use `math.select()` for conditional assignments
  - [ ] Use `math.max(0, x)` instead of `if (x > 0)`
  - [ ] Use bitmasks for multi-condition logic
- [ ] **Sort collision pairs by outcome**:
  - [ ] Pre-sort pairs by likely collision (closer pairs first)
  - [ ] Improves branch prediction for collision checks
  - [ ] Measure sorting overhead vs prediction gains
- [ ] **Add Burst hints for critical branches**:
  - [ ] Use `[BranchPrediction(true)]` for likely paths
  - [ ] Use `[BranchPrediction(false)]` for unlikely paths
  - [ ] Profile: verify hints improve prediction

**Sub-Epic 7.11.3: Data-Oriented Transform Caching** *(Not Started)*
**Goal**: Reduce entity lookup overhead by caching frequently accessed transforms
**Design Notes**:
- Entity lookups via `SystemAPI.GetComponent` have overhead
- Caching transforms in flat arrays enables faster iteration
- Double-buffering prevents read-after-write hazards

**Cache Structure**:
```csharp
public struct TransformCache : IComponentData
{
    public NativeArray<float3> Positions;      // All player positions
    public NativeArray<quaternion> Rotations;  // All player rotations
    public NativeArray<float3> Velocities;     // All player velocities
    public NativeArray<Entity> Entities;       // Entity lookup table
    public int Count;                          // Number of valid entries
}
```

**Tasks**:
- [ ] **Measure entity lookup overhead**:
  - [ ] Profile `GetComponent<LocalTransform>` calls per frame
  - [ ] Calculate percentage of collision system time
  - [ ] Only proceed if >20% of time spent on lookups
- [ ] **Create TransformCacheSystem**:
  - [ ] Gather all player transforms at frame start
  - [ ] Store in NativeArrays (persistent allocator)
  - [ ] Invalidate on player spawn/despawn
- [ ] **Implement double-buffering**:
  - [ ] Two caches: Read (previous frame), Write (current frame)
  - [ ] Swap buffers each frame
  - [ ] Prevents race conditions between read/write
- [ ] **Update collision systems to use cache**:
  - [ ] Replace `GetComponent` with cache index lookup
  - [ ] Pass cache as job parameter
  - [ ] Verify correctness matches non-cached version
- [ ] **Profile cache hit rate**:
  - [ ] Track cache hits vs misses (should be ~100% hits)
  - [ ] Measure speedup from caching (target 20-30%)

**Sub-Epic 7.11.4: Asynchronous Physics Integration** *(Not Started)*
**Goal**: Hide physics latency by running collision detection asynchronously
**Design Notes**:
- Run collision detection on previous frame's positions (1 frame lag)
- Apply results in current frame (hides detection latency)
- Requires careful handling of entity lifetime (spawns/despawns)

**Async Pipeline**:
```
Frame N:
  [Detection Job starts on Frame N-1 positions]
  [Game logic runs with Frame N-1 collision results]
  [Physics steps with current forces]
  [Detection Job completes, results stored for Frame N+1]
```

**Tasks**:
- [ ] **Measure current physics wait time**:
  - [ ] Profile time spent waiting for physics jobs
  - [ ] Only proceed if wait time >2ms
- [ ] **Implement double-buffered collision results**:
  - [ ] Buffer A: Results from previous frame (consumed this frame)
  - [ ] Buffer B: Results being computed (ready next frame)
  - [ ] Swap buffers each frame
- [ ] **Handle entity lifetime correctly**:
  - [ ] Skip results for despawned entities
  - [ ] Ignore collisions with newly spawned entities
  - [ ] Add entity version check to prevent stale references
- [ ] **Create job dependency chain**:
  - [ ] Detection job depends on previous frame's physics
  - [ ] Response job uses previous detection results
  - [ ] Chain via `JobHandle` for automatic scheduling
- [ ] **Profile latency hiding**:
  - [ ] Measure frame time before/after async
  - [ ] Target 30-50% reduction in collision wait time
  - [ ] Verify 1-frame lag is not perceptible

**Sub-Epic 7.11.5: GPU Acceleration** *(Future - Not Started)*
**Goal**: Offload broadphase collision to GPU for very high player counts
**Design Notes**:
- Only beneficial for >200 players (GPU overhead not worth it otherwise)
- Mobile/console may have GPU constraints (shared memory bandwidth)
- Requires careful CPU-GPU synchronization

**GPU Broadphase Approach**:
1. Upload player positions to ComputeBuffer (CPU → GPU)
2. Run parallel distance checks on GPU (1 thread per pair)
3. Output collision candidates to another buffer
4. Download candidates to CPU (GPU → CPU)
5. Run narrowphase on CPU (full component access)

**Tasks**:
- [ ] **Evaluate GPU broadphase feasibility**:
  - [ ] Profile CPU broadphase at 200+ players
  - [ ] Calculate GPU upload/download overhead
  - [ ] Only proceed if GPU would be net benefit
- [ ] **Create CollisionBroadphaseCompute.compute shader**:
  - [ ] Input: Player positions buffer
  - [ ] Output: Collision candidate pairs
  - [ ] Algorithm: Parallel O(N²) distance checks
- [ ] **Implement CPU-GPU synchronization**:
  - [ ] Use AsyncGPUReadback for non-blocking download
  - [ ] Double-buffer to hide download latency
  - [ ] Fallback to CPU if GPU not available
- [ ] **Profile GPU vs CPU performance**:
  - [ ] Test at 100, 200, 500, 1000 players
  - [ ] Find crossover point where GPU wins
  - [ ] Document platform-specific results
- [ ] **Document GPU approach for future**:
  - [ ] When to use (player count, platform)
  - [ ] Limitations (mobile battery, console TRC)
  - [ ] Maintenance requirements

**Sub-Epic 7.11.6: Network-Aware Optimizations** *(Not Started)*
**Goal**: Reduce collision processing for network-interpolated players
**Design Notes**:
- Local player needs full collision detection (authoritative input)
- Remote players are interpolated; simplified collision acceptable
- Reduces both CPU cost and bandwidth

**Optimization Tiers**:
| Player Type | Detection | Response | Bandwidth |
|------------|-----------|----------|-----------|
| Local | Full capsule | Full forces | N/A (local) |
| Remote (close) | Full capsule | Full forces | High priority |
| Remote (medium) | Sphere approx | Simplified | Medium priority |
| Remote (far) | Skip | Visual only | Low priority |

**Tasks**:
- [ ] **Implement distance-based LOD for collision**:
  - [ ] <10m: Full collision detection
  - [ ] 10-30m: Sphere-sphere approximation
  - [ ] >30m: Skip collision detection
- [ ] **Reduce collision response for remote players**:
  - [ ] Local player: Full stagger, knockdown, audio
  - [ ] Remote player: Visual stagger only, no audio
  - [ ] Server handles authoritative response
- [ ] **Implement collision interest management**:
  - [ ] Only replicate collisions within relevancy radius
  - [ ] Use spatial hash to determine nearby players
  - [ ] Reduces bandwidth for large player counts
- [ ] **Profile bandwidth reduction**:
  - [ ] Measure baseline bandwidth (from CollisionNetworkStats)
  - [ ] Measure with network-aware optimizations
  - [ ] Target 50% bandwidth reduction

**Sub-Epic 7.11.7: Micro-Optimizations** *(Not Started)*
**Goal**: Apply low-level optimizations for marginal gains
**Design Notes**:
- Each micro-optimization provides <5% improvement
- Cumulative effect can be 10-20%
- Only apply after major optimizations are done

**Optimization Checklist**:
| Optimization | Savings | Effort |
|-------------|---------|--------|
| `lengthsq()` vs `length()` | ~10% per call | Low |
| `rsqrt()` for normalize | ~5% per call | Low |
| Precompute reciprocals | ~3% for divisions | Low |
| `const` on immutables | ~1% (compiler hints) | Low |
| Inline hot functions | ~5% (reduced call overhead) | Medium |

**Tasks**:
- [ ] **Replace length() with lengthsq() where possible**:
  - [ ] Distance comparisons don't need sqrt
  - [ ] `lengthsq(a - b) < radiusSq` instead of `length(a - b) < radius`
- [ ] **Use rsqrt() for normalizations**:
  - [ ] `normalize(v)` = `v * rsqrt(lengthsq(v))`
  - [ ] Faster than `v / length(v)`
- [ ] **Precompute reciprocals**:
  - [ ] If dividing by same value multiple times
  - [ ] `float invMass = 1f / mass; a * invMass; b * invMass;`
- [ ] **Add const/readonly modifiers**:
  - [ ] Review all struct fields
  - [ ] Mark non-mutating data as readonly
  - [ ] Helps compiler optimization
- [ ] **Inline critical functions**:
  - [ ] Add `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
  - [ ] Only for small, frequently-called functions
  - [ ] Verify inlining via Burst Inspector
- [ ] **Profile cumulative impact**:
  - [ ] Measure before all micro-optimizations
  - [ ] Measure after all applied
  - [ ] Document total speedup (expect 10-20%)