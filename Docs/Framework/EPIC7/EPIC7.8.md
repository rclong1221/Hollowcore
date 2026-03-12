### Epic 7.8: Testing & Edge Cases
**Priority**: HIGH  
**Goal**: Validate collision system handles all scenarios robustly through comprehensive automated and manual testing

**IMPORTANT: Testing Philosophy**
The collision system is complex with many interacting features:
- ✅ **Physics integration** (7.1-7.2): PhysicsWorld, rotation locking, damping
- ✅ **Response mechanics** (7.3): Two-phase detection, aggregation, power ratios
- ✅ **State effects** (7.4): Stagger, knockdown, recovery transitions
- ✅ **Prediction/rollback** (7.5): Misprediction detection, reconciliation, smoothing
- ✅ **Game rules** (7.6): Teams, friendly fire, grace periods, group index
- ✅ **Performance** (7.7): Spatial hash, memory, profiling, platform presets

**Testing must cover**:
- Correctness: Physics behaves as designed under all conditions
- Robustness: Edge cases don't crash, desync, or produce invalid state
- Performance: Frame budget maintained under stress
- Network: Prediction feels good, rollback recovers cleanly

**Sub-Epic 7.8.1: Basic Collision Scenarios** *(Not Started)*
**Goal**: Verify fundamental collision behaviors work as designed
**Design Notes**:
- These tests validate Epic 7.3's push mechanics and Epic 7.4's state transitions
- Expected outcomes documented here serve as acceptance criteria
- Each scenario should be reproducible in test scene with controlled inputs

**Test Scene Setup**:
- Create `CollisionTestScene.unity` with flat ground plane
- Add `CollisionTestController.cs` MonoBehaviour for spawning test scenarios
- Include UI buttons to trigger each test case
- Record pass/fail results to console log with timestamps

**Tasks**:
- [ ] **Test Infrastructure Setup**:
  - [ ] Create `Assets/Scenes/CollisionTestScene.unity`
  - [ ] Create `Assets/Scripts/Testing/CollisionTestController.cs`
  - [ ] Create `Assets/Scripts/Testing/CollisionTestCase.cs` base class
  - [ ] Add spawn positions for controlled collision scenarios
- [ ] **TC-001: Standing Collision (Gentle Push)**:
  - Spawn two standing players facing each other at 2m distance
  - Walk both toward each other at walking speed (2 m/s)
  - Expected: Both pushed apart ~0.3m each, no stagger
  - Pass criteria: Separation completes within 0.5s, no overlap remaining
- [ ] **TC-002: Sprint vs Sprint (Head-On)**:
  - Spawn two players 10m apart, both sprinting toward each other (6 m/s each)
  - Expected: High impact (12 m/s relative), both staggered
  - Pass criteria: Both enter `Staggered` state, knockback ~2m each
- [ ] **TC-003: Sprint vs Stationary (Asymmetric)**:
  - Spawn sprinting player (6 m/s) approaching stationary player
  - Expected: Stationary player staggers, sprinting player minor stagger
  - Pass criteria: PowerRatio > 0.7 for sprinter, < 0.3 for standing
- [ ] **TC-004: Crouch Collision (Reduced Force)**:
  - Spawn crouching player vs standing player, both walking
  - Expected: Crouching player's StanceMultiplier (0.5) reduces their push force
  - Pass criteria: Standing player receives less knockback than TC-001
- [ ] **TC-005: Prone Collision (Step Over)**:
  - Spawn prone player, have standing player walk over them
  - Expected: Standing player can step over without collision (if enabled) or gentle push
  - Pass criteria: No stuck state, physics resolves within 1s
- [ ] **TC-006: Three-Way Collision (Multi-Contact)**:
  - Spawn three players in triangle formation, all move to center
  - Expected: Each player pushed by aggregated force from both others
  - Pass criteria: All three separate cleanly, no stuck cluster
- [ ] **TC-007: Stance Transition Mid-Collision**:
  - Player A collides with Player B while B is transitioning crouch→stand
  - Expected: Collision uses interpolated stance multiplier
  - Pass criteria: No discontinuity in push force, smooth transition
- [ ] **TC-008: Mass Difference (50kg vs 100kg)**:
  - Spawn light player (mass 0.5) and heavy player (mass 1.0), walking collision
  - Expected: Light player knocked back further, heavy player minor movement
  - Pass criteria: Knockback ratio matches mass ratio within 20%
- [ ] **TC-009: Slope Collision (Gravity + Collision)**:
  - Spawn two players on 30° slope, collide perpendicular to slope
  - Expected: Gravity and collision forces combine correctly
  - Pass criteria: Players don't slide uphill, downhill player moves more

**Sub-Epic 7.8.2: Edge Cases** *(Not Started)*
**Goal**: Test boundary conditions and unusual situations that could cause bugs
**Design Notes**:
- Edge cases often reveal race conditions, floating point issues, or missing guards
- Each test should document the failure mode being prevented
- Focus on physics stability and preventing invalid game states

**Tasks**:
- [ ] **TC-010: Player Pinned Between Two (Escape Mechanism)**:
  - Spawn Player B between Player A and Player C, A+C move toward B
  - Expected: B receives push forces from both directions, escapes upward or sideways
  - Failure mode prevented: B trapped permanently, physics explodes
  - Pass criteria: B escapes within 2s, no NaN positions
- [ ] **TC-011: Wall Pin (No Clipping)**:
  - Player A pushes Player B against solid wall
  - Expected: B is pressed against wall but doesn't clip through
  - Failure mode prevented: B clips through wall geometry
  - Pass criteria: B's position never penetrates wall collider
- [ ] **TC-012: Mid-Air Collision (Jump)**:
  - Two players jump and collide mid-air
  - Expected: Horizontal push applied, both fall naturally after
  - Failure mode prevented: Players stuck in air, gravity disabled
  - Pass criteria: Both land within expected time (gravity unaffected)
- [ ] **TC-013: Climbing Collision (Cancel or Deflect)**:
  - Player climbing ledge gets hit by another player
  - Expected: Climb cancelled, player falls/pushed off
  - Failure mode prevented: Player frozen in climb state while being pushed
  - Pass criteria: Clean state transition to Falling or Staggered
- [ ] **TC-014: Slide Collision (Momentum Preservation)**:
  - Sliding player (fast horizontal movement) collides with standing player
  - Expected: Slide momentum contributes to collision force
  - Failure mode prevented: Slide velocity ignored in power calculation
  - Pass criteria: Impact force includes slide velocity component
- [ ] **TC-015: Spawn Overlap (Immediate Separation)**:
  - Spawn two players at exact same position
  - Expected: Immediate separation impulse pushes them apart
  - Failure mode prevented: Players stuck overlapping, physics jitter
  - Pass criteria: Separation completes within 0.2s, no physics explosion
- [ ] **TC-016: High-Speed Collision (>20 m/s)**:
  - Use debug teleport to create 25 m/s relative velocity collision
  - Expected: Knockdown triggered, but no physics explosion or clipping
  - Failure mode prevented: CCD failure, tunneling through other player
  - Pass criteria: Both players in valid positions after collision
- [ ] **TC-017: Zero Velocity Overlap**:
  - Two stationary players placed overlapping (no relative motion)
  - Expected: Separation system pushes apart gently
  - Failure mode prevented: Division by zero in impact calculation
  - Pass criteria: Separation completes, no NaN/Inf values logged
- [ ] **TC-018: Maximum Stagger Chain**:
  - Player A staggers, gets hit again before recovery
  - Expected: Stagger timer resets or extends, not stacked indefinitely
  - Failure mode prevented: Infinite stagger duration from chained hits
  - Pass criteria: StaggerTimeRemaining capped at MaxStaggerTime (3s per 7.7.8)
- [ ] **TC-019: Knockdown During Knockdown**:
  - Knocked down player gets hit by another player
  - Expected: No additional knockdown applied (already in state)
  - Failure mode prevented: Knockdown timer stacking, invulnerability bypass
  - Pass criteria: IsKnockedDown remains true, timer doesn't extend beyond max

**Sub-Epic 7.8.3: Networked Edge Cases** *(Not Started)*
**Goal**: Validate collision prediction, rollback, and reconciliation under adverse network conditions
**Design Notes**:
- NetCode for Entities handles prediction/rollback automatically, but collision-specific issues can occur
- Use Unity NetCode's Network Simulator to introduce artificial latency, jitter, packet loss
- Focus on visual smoothness and state consistency, not physics determinism (already guaranteed)

**Network Simulator Configuration**:
| Scenario | RTT | Packet Loss | Jitter | Use Case |
|----------|-----|-------------|--------|----------|
| LAN | 5ms | 0% | 1ms | Ideal conditions |
| Good | 50ms | 1% | 5ms | Typical broadband |
| Moderate | 100ms | 3% | 15ms | WiFi, cross-region |
| Poor | 200ms | 5% | 30ms | Mobile, bad connection |
| Extreme | 300ms | 10% | 50ms | Stress test only |

**Tasks**:
- [ ] **TC-020: Prediction Divergence (Rollback Recovery)**:
  - Client predicts collision, server disagrees (different timing)
  - Expected: Client rolls back and replays, reconciliation smooths visual
  - Failure mode prevented: Visible teleport/snap, desync persists
  - Pass criteria: Position error <0.1m within 0.5s, no visible pop
- [ ] **TC-021: Mid-Collision Disconnect**:
  - Player A is pushing Player B, A disconnects mid-collision
  - Expected: B's collision resolves cleanly, A's ghost despawns
  - Failure mode prevented: B stuck being pushed by ghost, physics NaN
  - Pass criteria: B returns to normal state within 1s of disconnect
- [ ] **TC-022: Join During Collision**:
  - New player spawns at location where collision is ongoing
  - Expected: New player not affected by in-progress collision
  - Failure mode prevented: New player inherits invalid collision state
  - Pass criteria: New player has zeroed collision state on spawn
- [ ] **TC-023: High Latency Collision (200ms+)**:
  - Two players collide with 200ms RTT (each player sees 100ms delay)
  - Expected: Local player predicts collision, feels responsive
  - Failure mode prevented: Collision feels delayed or unresponsive
  - Pass criteria: Input-to-visual-feedback <50ms local, reconcile within 200ms
- [ ] **TC-024: Packet Loss During Collision**:
  - 10% packet loss during active collision
  - Expected: Interpolation/extrapolation covers gaps, no jitter
  - Failure mode prevented: Visual stutter, state desync
  - Pass criteria: Smooth visual motion, server state matches within 500ms
- [ ] **TC-025: Snapshot Ordering (Out-of-Order Packets)**:
  - Simulate packet reordering during collision sequence
  - Expected: NetCode handles ordering, collision state consistent
  - Failure mode prevented: Old snapshot applied after newer, state reversion
  - Pass criteria: Monotonic tick progression, no state flickering
- [ ] **TC-026: Bandwidth Throttle During Collision**:
  - Reduce available bandwidth during 5-player collision
  - Expected: Priority system (Epic 7.7.8) sends active collision players first
  - Failure mode prevented: Active collision players deprioritized
  - Pass criteria: High priority players update at 60Hz, low priority at 10Hz
- [ ] **TC-027: Ghost Relevancy Change**:
  - Player moves out of relevancy range during collision
  - Expected: Collision completes on server, client sees last known state
  - Failure mode prevented: Collision state stuck, ghost not properly hidden
  - Pass criteria: Ghost despawns cleanly, no dangling collision references

**Sub-Epic 7.8.4: State Machine Validation** *(Not Started)*
**Goal**: Ensure collision-related state transitions are valid and complete
**Design Notes**:
- Player states: Idle, Walking, Sprinting, Crouching, Prone, Jumping, Climbing, Sliding, Staggered, Knockdown
- Collision can transition: Any → Staggered, Any → Knockdown, Staggered → Knockdown
- Recovery transitions: Staggered → Previous, Knockdown → GetUp → Idle
- Invalid transitions should be rejected or logged

**State Transition Matrix**:
| From State | Stagger Allowed | Knockdown Allowed | Notes |
|------------|-----------------|-------------------|-------|
| Idle | ✅ | ✅ | Standard case |
| Walking | ✅ | ✅ | Standard case |
| Sprinting | ✅ | ✅ | Standard case |
| Crouching | ✅ | ✅ | Reduced incoming force |
| Prone | ⚠️ Limited | ✅ | Already low, harder to stagger |
| Jumping | ✅ | ✅ | Mid-air collision |
| Climbing | ✅ | ✅ | Cancels climb |
| Sliding | ✅ | ✅ | Cancels slide |
| Staggered | ✅ (refresh) | ✅ | Can escalate to knockdown |
| Knockdown | ❌ | ❌ (refresh) | Invulnerable to additional |
| Dead | ❌ | ❌ | Ignore all collisions |

**Tasks**:
- [ ] **TC-028: State Transition Logging**:
  - Enable debug logging for all PlayerMovementState transitions
  - Run all TC-001 through TC-027 tests
  - Expected: All transitions match matrix above
  - Pass criteria: No invalid transitions logged
- [ ] **TC-029: Recovery State Restoration**:
  - Stagger player while sprinting, wait for recovery
  - Expected: Player returns to Walking (not Sprinting)
  - Pass criteria: Post-recovery state is valid movement state
- [ ] **TC-030: Knockdown Invulnerability**:
  - Knocked down player receives another knockdown-eligible hit
  - Expected: No state change, invulnerability active
  - Failure mode prevented: Knockdown timer reset, infinite ground state
  - Pass criteria: IsKnockedDown unchanged, hit ignored or logged
- [ ] **TC-031: Dead Player Collision**:
  - Dead player's collider vs living player
  - Expected: No collision response (dead = no gameplay collision)
  - Pass criteria: Living player passes through or minimal physics push

**Sub-Epic 7.8.5: Performance Stress Testing** *(Not Started)*
**Goal**: Verify collision system maintains frame budget under extreme load
**Design Notes**:
- Target: <2ms total collision cost at 50 players (60 FPS budget = 16.67ms)
- Scaling: Sub-linear with player count due to spatial hashing (Epic 7.7.3)
- Platform-specific targets defined in Epic 7.7.7

**Stress Test Configurations**:
| Scenario | Players | Collisions/Frame | Target Time | Platform |
|----------|---------|------------------|-------------|----------|
| Light | 16 | 2-4 | <0.5ms | All |
| Medium | 50 | 10-20 | <2.0ms | PC/Console |
| Heavy | 100 | 40-80 | <4.0ms | PC only |
| Extreme | 200 | 100+ | <8.0ms | High-end PC |

**Tasks**:
- [ ] **TC-032: 50-Player Benchmark (Primary Target)**:
  - Spawn 50 players in 20m x 20m area with random movement
  - Run for 60 seconds, collect profiler data
  - Expected: Collision systems <2.0ms combined
  - Pass criteria: 99th percentile frame time <3.0ms for collision
- [ ] **TC-033: Collision Hotspot (Dense Cluster)**:
  - Spawn 20 players in 5m x 5m area (high density)
  - Expected: Spatial hash still effective, no O(N²) fallback
  - Pass criteria: Frame time <50% higher than spread-out configuration
- [ ] **TC-034: Zero Collision Frame (Skip Optimization)**:
  - Spawn 50 players with no collisions occurring
  - Expected: Early-out optimization skips response phase
  - Pass criteria: Collision cost <0.2ms when no collisions detected
- [ ] **TC-035: Memory Allocation Check**:
  - Run 50-player benchmark with Unity Profiler in Allocation mode
  - Expected: Zero per-frame managed allocations (Epic 7.7.2)
  - Pass criteria: No GC.Alloc markers in collision systems
- [ ] **TC-036: Platform-Specific Limits**:
  - Run on target platforms with recommended player counts (per 7.7.7)
  - PC High-end: 200 players, Console: 75 players, Mobile: 25 players
  - Pass criteria: Maintains 60 FPS on console, 30 FPS on mobile

**Sub-Epic 7.8.6: Automated Test Suite** *(Not Started)*
**Goal**: Create reproducible automated tests for CI/CD integration
**Design Notes**:
- Unity Test Framework for Edit/Play mode tests
- Collision tests should run headless (no rendering needed for logic tests)
- Performance tests record metrics for regression detection

**Tasks**:
- [ ] Create `Assets/Tests/EditMode/CollisionComponentTests.cs`:
  - [ ] Test `PlayerCollisionState` default values
  - [ ] Test `PlayerCollisionSettings` validation
  - [ ] Test `CollisionNetworkStats` bandwidth calculation
  - [ ] Test power ratio calculation formula
- [ ] Create `Assets/Tests/PlayMode/CollisionIntegrationTests.cs`:
  - [ ] Implement TC-001 through TC-009 as automated tests
  - [ ] Use `UnityTest` attribute for multi-frame tests
  - [ ] Assert position/state outcomes within tolerance
- [ ] Create `Assets/Tests/PlayMode/CollisionPerformanceTests.cs`:
  - [ ] Implement TC-032, TC-033, TC-034 as benchmarks
  - [ ] Use `PerformanceTest` attribute with `SampleGroup`
  - [ ] Set regression thresholds (fail if >20% slower)
- [ ] Create `Assets/Tests/PlayMode/CollisionNetworkTests.cs`:
  - [ ] Implement TC-020 through TC-024 with NetCode test utilities
  - [ ] Use `NetCodeTestWorld` for client/server simulation
  - [ ] Simulate latency/packet loss via test configuration
- [ ] Configure CI to run tests on PR:
  - [ ] Add test job to GitHub Actions / Unity Cloud Build
  - [ ] Run edit mode tests (fast, <30s)
  - [ ] Run play mode tests (slower, <5min)
  - [ ] Performance tests weekly (expensive, <30min)

**Files to Create**:
- `Assets/Scenes/CollisionTestScene.unity`
- `Assets/Scripts/Testing/CollisionTestController.cs`
- `Assets/Scripts/Testing/CollisionTestCase.cs`
- `Assets/Tests/EditMode/CollisionComponentTests.cs`
- `Assets/Tests/PlayMode/CollisionIntegrationTests.cs`
- `Assets/Tests/PlayMode/CollisionPerformanceTests.cs`
- `Assets/Tests/PlayMode/CollisionNetworkTests.cs`

**Files to Modify**:
- `.github/workflows/test.yml` (if using GitHub Actions)
- `ProjectSettings/EditorBuildSettings.asset` (add test scene)