### Epic 7.12: Deterministic Physics & Rollback Support
**Priority**: HIGH (if targeting competitive multiplayer)
**Goal**: Ensure collision system is fully deterministic for rollback netcode, enabling fair competitive play and spectator features

**IMPORTANT: Determinism Philosophy**
Determinism means: same inputs → same outputs, every time, on every platform.
This is critical for:
- ✅ **Rollback netcode**: Client predicts ahead, server corrects; must replay exactly
- ✅ **Competitive fairness**: All clients see same collision outcomes
- ✅ **Spectator mode**: Replays and spectating must match live game
- ✅ **Anti-cheat**: Deterministic simulation can be verified server-side

**Unity's Determinism Guarantees**:
| System | Deterministic? | Notes |
|--------|---------------|-------|
| Unity.Physics | ✅ Yes | IEEE-754, Burst-compiled, fixed timestep |
| Unity.Mathematics | ✅ Yes | Same results across platforms |
| NetCode for Entities | ✅ Yes | Built for rollback netcode |
| UnityEngine.Random | ❌ No | Use `Unity.Mathematics.Random` instead |
| Time.deltaTime | ❌ No | Use fixed timestep |
| Floating-point mode | ⚠️ Depends | Must use Strict (not Fast) |

**Sub-Epic 7.12.1: Leverage Unity Physics Determinism** *(Not Started)*
**Goal**: Verify and configure Unity Physics for deterministic simulation
**Design Notes**:
- Unity.Physics is deterministic by design (unlike PhysX)
- Must use `SimulationType.UnityPhysics` (not Havok)
- Must use fixed timestep (not variable delta time)

**Configuration Checklist**:
| Setting | Required Value | Location |
|---------|---------------|----------|
| SimulationType | UnityPhysics | PhysicsStep component |
| Fixed Timestep | 1/60 (0.01667) | Project Settings → Time |
| Floating Point Mode | Strict | Burst settings or per-job |
| Random Seed | Fixed per simulation | Game initialization |

**Tasks**:
- [ ] **Verify PhysicsStep configuration**:
  - [ ] Confirm `SimulationType = SimulationType.UnityPhysics`
  - [ ] Confirm fixed timestep is used (not Time.deltaTime)
  - [ ] Document in project settings guide
- [ ] **Configure Burst for determinism**:
  - [ ] Set `FloatMode.Strict` on collision jobs
  - [ ] Avoid `FloatMode.Fast` (allows reordering)
  - [ ] Verify via Burst Inspector
- [ ] **Replace UnityEngine.Random usage**:
  - [ ] Search codebase for `UnityEngine.Random`
  - [ ] Replace with `Unity.Mathematics.Random`
  - [ ] Seed from deterministic source (tick number)
- [ ] **Create determinism test harness**:
  - [ ] Run same simulation twice with identical inputs
  - [ ] Compare all entity positions/states at end
  - [ ] Must be bit-for-bit identical
- [ ] **Document determinism requirements**:
  - [ ] List all systems that must be deterministic
  - [ ] Document any non-deterministic fallbacks (and why)
  - [ ] Add to onboarding documentation

**Sub-Epic 7.12.2: Fixed-Point Math** *(Optional - Not Started)*
**Goal**: Implement fixed-point alternative for guaranteed cross-platform determinism
**Design Notes**:
- IEEE-754 floating-point should be deterministic across platforms
- Fixed-point is a fallback if platform-specific issues found
- Significantly slower than floating-point (2-3x)

**Fixed-Point Format**:
```csharp
// 32.32 fixed-point (64-bit total)
// Integer part: 32 bits (-2B to +2B range)
// Fractional part: 32 bits (sub-nanometer precision)
public struct FixedPoint64
{
    public long RawValue;  // 32.32 fixed-point
    
    public static implicit operator FixedPoint64(float f) =>
        new FixedPoint64 { RawValue = (long)(f * 4294967296L) };
    
    public static implicit operator float(FixedPoint64 fp) =>
        fp.RawValue / 4294967296f;
}
```

**Tasks**:
- [ ] **Evaluate need for fixed-point**:
  - [ ] Run determinism test across platforms (Win, Mac, Linux)
  - [ ] Run on different CPUs (Intel, AMD, ARM)
  - [ ] Only proceed if determinism failures found
- [ ] **Create FixedPoint math library**:
  - [ ] Basic operations: Add, Subtract, Multiply, Divide
  - [ ] Advanced: Sqrt, Sin, Cos, Atan2
  - [ ] Vector types: FixedPoint3 (position, velocity)
- [ ] **Convert collision calculations to fixed-point**:
  - [ ] Distance calculations
  - [ ] Power ratio calculations
  - [ ] Stagger force calculations
- [ ] **Profile fixed-point vs floating-point**:
  - [ ] Run benchmark with both implementations
  - [ ] Document performance difference (expect 2-3x slower)
  - [ ] Add toggle for debug/competitive modes
- [ ] **Create fixed-point test suite**:
  - [ ] Verify all operations match expected results
  - [ ] Test edge cases (overflow, underflow, precision)
  - [ ] Compare fixed-point vs float results (should be close)

**Sub-Epic 7.12.3: NetCode Rollback Integration** *(Not Started)*
**Goal**: Ensure collision state works correctly with NetCode's rollback prediction
**Design Notes**:
- NetCode for Entities handles snapshots and rollback automatically
- Collision components must be marked as `[GhostComponent]`
- Collision systems must run in `PredictedFixedStepSimulationSystemGroup`

**Rollback Flow**:
```
1. Client receives server snapshot (tick 100)
2. Client is currently at tick 105 (predicted ahead)
3. NetCode compares tick 100 prediction vs server
4. If mismatch: rollback to tick 100, apply server state
5. Re-simulate ticks 101-105 with corrected state
6. Collision systems re-run during re-simulation
```

**Tasks**:
- [ ] **Verify GhostComponent attributes**:
  - [ ] `PlayerCollisionState` is `[GhostComponent]`
  - [ ] `LocalTransform` is `[GhostComponent]` (position)
  - [ ] `PhysicsVelocity` is `[GhostComponent]` (velocity)
- [ ] **Confirm system group placement**:
  - [ ] Collision systems in `PredictedFixedStepSimulationSystemGroup`
  - [ ] Systems have correct `[UpdateBefore]`/`[UpdateAfter]` attributes
  - [ ] No systems in wrong group (would miss rollback)
- [ ] **Test rollback behavior**:
  - [ ] Artificially create misprediction (force server disagreement)
  - [ ] Verify client rolls back and re-simulates
  - [ ] Verify final state matches server
- [ ] **Profile rollback overhead**:
  - [ ] Measure re-simulation cost (typically 3-5 frames)
  - [ ] Verify <0.5ms per rollback frame
  - [ ] Optimize if rollback is expensive
- [ ] **Add rollback debugging**:
  - [ ] Log rollback events with frame delta
  - [ ] Visualize predicted vs corrected positions
  - [ ] Track rollback frequency (should be rare)

**Sub-Epic 7.12.4: Input Delay & Buffering** *(Not Started)*
**Goal**: Implement input buffering to hide network latency for collision actions
**Design Notes**:
- Input delay allows client to receive server state before acting
- Reduces rollback frequency at cost of input responsiveness
- Variable delay based on network conditions

**Input Delay Tiers**:
| Network Quality | RTT | Input Delay | Experience |
|----------------|-----|-------------|------------|
| Excellent | <30ms | 0 frames | Instant response |
| Good | 30-80ms | 1 frame | Barely noticeable |
| Moderate | 80-150ms | 2 frames | Slight delay |
| Poor | 150-250ms | 3 frames | Noticeable delay |
| Bad | >250ms | 4+ frames | Consider matchmaking warning |

**Tasks**:
- [ ] **Implement input buffer**:
  - [ ] Store last N frames of input (default N=4)
  - [ ] Delay input processing by configured amount
  - [ ] Configurable per-client based on RTT
- [ ] **Create adaptive delay system**:
  - [ ] Measure RTT continuously (rolling average)
  - [ ] Adjust delay automatically as network changes
  - [ ] Smooth transitions (don't jump suddenly)
- [ ] **Add delay visualization**:
  - [ ] Show current input delay in debug UI
  - [ ] Display RTT and jitter metrics
  - [ ] Warning indicator for high delay
- [ ] **Test delay impact on gameplay**:
  - [ ] Playtest with various delay settings
  - [ ] Find balance between responsiveness and rollback frequency
  - [ ] Document recommended settings per scenario
- [ ] **Implement delay compensation for collisions**:
  - [ ] Collision input (tackle) should feel instant
  - [ ] Predict collision locally, confirm with server
  - [ ] Smooth any corrections

**Sub-Epic 7.12.5: Ghost Collision Suppression** *(Not Started)*
**Goal**: Detect and suppress phantom collisions caused by prediction errors
**Design Notes**:
- "Ghost collisions" occur when client predicts a hit that server disagrees with
- Can cause duplicate audio/VFX or confusing gameplay
- Need to identify and suppress before presenting to player

**Ghost Collision Characteristics**:
| Characteristic | Real Collision | Ghost Collision |
|---------------|----------------|-----------------|
| Server confirms | ✅ Yes | ❌ No |
| High relative velocity | Common | More common (extrapolation error) |
| Distant players | Rare | More common (relevancy lag) |
| After network spike | Normal | More common |

**Tasks**:
- [ ] **Track server-confirmed collisions**:
  - [ ] Add `ServerConfirmed` flag to CollisionEvent
  - [ ] Server sends collision confirmation RPC
  - [ ] Client marks local prediction as confirmed/rejected
- [ ] **Implement ghost detection heuristics**:
  - [ ] Flag high-velocity collisions with distant players
  - [ ] Flag collisions immediately after network spike
  - [ ] Flag collisions with recently spawned players
- [ ] **Add collision confidence scoring**:
  - [ ] Score 0-1 based on likelihood of being real
  - [ ] Factors: proximity, velocity, network stability
  - [ ] Suppress presentation for low-confidence (<0.5)
- [ ] **Defer presentation until confirmed**:
  - [ ] Queue audio/VFX for predicted collisions
  - [ ] Wait for server confirmation (max 100ms)
  - [ ] If confirmed: play immediately
  - [ ] If rejected: discard silently
- [ ] **Profile ghost suppression**:
  - [ ] Track ghost collision rate (should be <5%)
  - [ ] Verify suppression doesn't add noticeable latency
  - [ ] Document network conditions that increase ghosts

**Sub-Epic 7.12.6: Reconciliation Smoothing** *(Not Started)*
**Goal**: Make server corrections feel smooth instead of jarring
**Design Notes**:
- When server corrects client prediction, don't snap instantly
- Interpolate from wrong position to correct position over several frames
- Use ease-out curve for natural deceleration

**Smoothing Parameters**:
| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| SmoothingDuration | 0.1s | 0.05-0.3s | Time to blend to correct position |
| ErrorThreshold | 0.1m | 0.01-0.5m | Below this, don't correct (prevents jitter) |
| MaxCorrection | 2.0m | 0.5-5.0m | Above this, snap (too far to smooth) |
| EasingCurve | EaseOutQuad | Various | Curve for position interpolation |

**Tasks**:
- [ ] **Implement position smoothing**:
  - [ ] Track predicted position and server position
  - [ ] Interpolate visual position over SmoothingDuration
  - [ ] Physics uses server position (authoritative)
- [ ] **Add error threshold**:
  - [ ] If error < threshold, ignore correction
  - [ ] Prevents micro-corrections from causing jitter
  - [ ] Threshold configurable (default 1cm)
- [ ] **Add snap threshold**:
  - [ ] If error > max, snap immediately
  - [ ] Smoothing large errors looks like rubber-banding
  - [ ] Log snap events for network debugging
- [ ] **Implement ease-out curve**:
  - [ ] Fast initial correction, slow approach to target
  - [ ] Feels more natural than linear interpolation
  - [ ] Use `math.smoothstep` or custom curve
- [ ] **Visualize corrections (debug)**:
  - [ ] Draw line from predicted to corrected position
  - [ ] Show correction magnitude and duration
  - [ ] Track correction frequency over time

**Sub-Epic 7.12.7: Cross-Platform Determinism Testing** *(Not Started)*
**Goal**: Verify deterministic behavior across all target platforms
**Design Notes**:
- Same simulation should produce identical results everywhere
- Different CPUs, OSes, and Unity versions could introduce variance
- Automated testing catches regressions early

**Test Matrix**:
| Platform | CPU | Test Priority |
|----------|-----|---------------|
| Windows x64 | Intel | High (primary dev) |
| Windows x64 | AMD | High (common) |
| macOS ARM64 | Apple Silicon | High (dev machines) |
| Linux x64 | Intel/AMD | Medium (servers) |
| PS5 | AMD | Medium (console) |
| Xbox Series | AMD | Medium (console) |
| Steam Deck | AMD | Medium (handheld) |
| iOS | Apple A-series | Low (mobile) |
| Android | Various ARM | Low (mobile) |

**Tasks**:
- [ ] **Create determinism test suite**:
  - [ ] Run 1000-frame simulation with fixed seed
  - [ ] Record all entity positions at frame 1000
  - [ ] Compare results across platforms (must match exactly)
- [ ] **Set up CI for cross-platform testing**:
  - [ ] Build for each target platform
  - [ ] Run determinism test on each
  - [ ] Fail build if results differ
- [ ] **Test with different Unity versions**:
  - [ ] Run test on Unity 2022 LTS, 2023 LTS
  - [ ] Document any version-specific issues
  - [ ] Pin Unity version if needed
- [ ] **Create determinism regression test**:
  - [ ] Store "golden" result from known-good run
  - [ ] Compare each build against golden result
  - [ ] Alert if any difference detected
- [ ] **Document platform-specific workarounds**:
  - [ ] List any platform quirks discovered
  - [ ] Provide workarounds or fixes
  - [ ] Update as new platforms added

**Files to Create**:
- `Assets/Scripts/Determinism/DeterminismTestHarness.cs`
- `Assets/Scripts/Determinism/FixedPointMath.cs` (if needed)
- `Assets/Scripts/Determinism/GhostCollisionSuppressor.cs`
- `Assets/Scripts/Determinism/ReconciliationSmoother.cs`
- `Assets/Tests/PlayMode/DeterminismTests.cs`

**Files to Modify**:
- `Assets/Scripts/Player/Components/PlayerCollisionState.cs` (add ServerConfirmed flag)
- `Assets/Scripts/Player/Systems/CollisionReconciliationSystem.cs` (add smoothing)