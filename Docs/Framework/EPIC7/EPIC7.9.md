### Epic 7.9: Debug Tools & Visualization
**Priority**: MEDIUM (elevated from LOW - critical for iterating on collision feel)  
**Goal**: Developer tools for debugging collision issues, visualizing physics state, and tuning collision parameters in real-time

**IMPORTANT: Debug Tool Philosophy**
Collision systems are notoriously difficult to debug because:
- ✅ **Transient events**: Collisions happen in 1-2 frames, easy to miss
- ✅ **Multi-entity interactions**: N players → N² potential collision pairs
- ✅ **State dependencies**: Stagger/knockdown depend on velocity, stance, direction
- ✅ **Network effects**: Prediction/rollback creates "ghost" collisions
- ✅ **Performance coupling**: Debug tools must not impact gameplay performance

**Debug tools must provide**:
- Real-time visualization of collision state (what's happening NOW)
- Historical logging (what HAPPENED in past frames)
- Parameter tuning (change values without recompiling)
- Performance metrics (is collision the bottleneck?)

**Sub-Epic 7.9.1: Runtime Collision Visualization** *(Not Started)*
**Goal**: See collision physics state in real-time during gameplay
**Design Notes**:
- Use `PhysicsDebugDisplay` for Unity-provided visualization
- Add custom gizmos for game-specific collision data (stagger vectors, power ratios)
- Color-coding: Green = gentle contact, Yellow = stagger, Red = knockdown, Purple = tackle

**Tasks**:
- [ ] **Enable Unity PhysicsDebugDisplay**:
  - [ ] Add `PhysicsDebugDisplaySystem` to client world
  - [ ] Configure display options: Colliders, Contacts, BVH, Joints
  - [ ] Add toggle key (F1) to enable/disable physics debug view
- [ ] **Create CollisionDebugOverlay.cs** runtime UI:
  - [ ] Display active collision count this frame
  - [ ] Show stagger/knockdown player counts
  - [ ] Display spatial hash cell occupancy
  - [ ] Show bandwidth estimate (from CollisionNetworkStats)
  - [ ] Toggle via console command: `collision.debug.overlay`
- [ ] **Implement collision force arrows**:
  - [ ] Draw arrow from collision point in push direction
  - [ ] Arrow length proportional to force magnitude
  - [ ] Fade over 0.5s for visibility
  - [ ] Color based on severity (green→yellow→red gradient)
- [ ] **Add player collider capsule visualization**:
  - [ ] Draw wireframe capsule around each player
  - [ ] Color based on current state: Idle (gray), Staggered (yellow), Knockdown (red)
  - [ ] Show contact points as spheres when collision occurs
- [ ] **Create spatial hash grid visualization**:
  - [ ] Draw cell boundaries as wireframe boxes
  - [ ] Color cells by occupancy (empty=invisible, 1=green, 2+=yellow, 5+=red)
  - [ ] Show player count per cell as floating number
  - [ ] Toggle via: `collision.debug.spatialhash`
- [ ] **Add collision cooldown indicator**:
  - [ ] Draw shrinking ring around player during cooldown
  - [ ] Color: Red (can't collide) → Green (ready)
  - [ ] Show remaining time as floating text (debug only)

**Sub-Epic 7.9.2: Collision Event Logging** *(Not Started)*
**Goal**: Record collision history for post-mortem debugging
**Design Notes**:
- Log to both console and file for persistence
- Include frame number, entities, impact speed, outcome (push/stagger/knockdown)
- Filter by severity to avoid log spam

**Log Entry Format**:
```
[Frame 1234] Collision: Player_A ↔ Player_B | Impact: 5.2 m/s | Power: 0.65:0.35 | Outcome: A=Push, B=Stagger | Pos: (10.2, 0, 5.1)
```

**Tasks**:
- [ ] **Create CollisionEventLogger.cs**:
  - [ ] Subscribe to CollisionEventBuffer changes
  - [ ] Format log entries with all relevant data
  - [ ] Write to `Debug.Log` with category prefix `[Collision]`
  - [ ] Support log level filtering (All, StaggerOnly, KnockdownOnly)
- [ ] **Implement collision history buffer**:
  - [ ] Store last 100 collision events in ring buffer
  - [ ] Include timestamp, entities, positions, velocities, outcome
  - [ ] Query via console: `collision.history [count]`
- [ ] **Add file logging for sessions**:
  - [ ] Write collision log to `Logs/collision_[timestamp].log`
  - [ ] Include session start time, player count, game mode
  - [ ] Flush on application quit or crash
- [ ] **Create collision event filter UI**:
  - [ ] Checkboxes for event types: Push, Stagger, Knockdown, Tackle
  - [ ] Filter by player name/ID
  - [ ] Time range slider (last N seconds)
- [ ] **Add network collision logging**:
  - [ ] Log prediction vs server outcome differences
  - [ ] Show rollback events with frame delta
  - [ ] Highlight desync (prediction != server by >0.5m)

**Sub-Epic 7.9.3: Runtime Parameter Tuning** *(Not Started)*
**Goal**: Adjust collision parameters in real-time without recompiling
**Design Notes**:
- Use Unity's RuntimeInspector or custom ImGui-style UI
- Parameters exposed: push force, stagger threshold, knockdown ratio, cooldowns
- Changes immediately affect gameplay (hot-reload)
- Save/load presets for A/B testing

**Tunable Parameters (from PlayerCollisionSettings)**:
| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| PushForceMultiplier | 1.0 | 0.0-5.0 | Base push force scaling |
| StaggerPowerThreshold | 5.0 | 1.0-20.0 | Minimum power to trigger stagger |
| KnockdownPowerThreshold | 0.9 | 0.5-1.0 | Power ratio for knockdown |
| CollisionCooldown | 0.3 | 0.0-2.0 | Seconds between collisions |
| StaggerDuration | 0.5 | 0.1-2.0 | Base stagger time |
| KnockdownDuration | 2.0 | 0.5-5.0 | Base knockdown time |
| SprintStanceMultiplier | 1.5 | 1.0-3.0 | Sprinting collision bonus |
| ProneStanceMultiplier | 0.3 | 0.1-1.0 | Prone collision reduction |
| DirectionalBracedBonus | 0.8 | 0.5-1.5 | Front-hit resistance |
| DirectionalBackPenalty | 1.5 | 1.0-2.0 | Back-hit vulnerability |

**Tasks**:
- [ ] **Create CollisionTuningUI.cs** runtime panel:
  - [ ] Slider for each tunable parameter
  - [ ] Real-time update (no apply button needed)
  - [ ] Display current value and default
  - [ ] Reset to default button per parameter
- [ ] **Implement preset system**:
  - [ ] Save current values to named preset
  - [ ] Load preset from dropdown
  - [ ] Built-in presets: Realistic, Arcade, Tactical, TestExtreme
  - [ ] Export/import presets as JSON
- [ ] **Add console command tuning**:
  - [ ] `collision.set <param> <value>` - Set parameter
  - [ ] `collision.get <param>` - Query current value
  - [ ] `collision.reset` - Reset all to defaults
  - [ ] `collision.preset <name>` - Load preset
- [ ] **Create comparison mode**:
  - [ ] Split-screen A/B with different parameter sets
  - [ ] Synchronized input for fair comparison
  - [ ] Export comparison results for design review

**Sub-Epic 7.9.4: Performance Profiling Dashboard** *(Not Started)*
**Goal**: Real-time display of collision system performance metrics
**Design Notes**:
- Show frame cost per collision subsystem
- Track trends over time (detect degradation)
- Highlight when exceeding target budget

**Metrics to Display**:
| Metric | Target | Source |
|--------|--------|--------|
| Total Collision Cost | <2.0ms | All collision systems combined |
| Detection Phase | <0.5ms | PlayerProximityCollisionSystem |
| Response Phase | <1.0ms | PlayerCollisionResponseSystem |
| Separation Phase | <0.2ms | PlayerSeparationSystem |
| Spatial Hash Update | <0.3ms | PlayerSpatialHashSystem |
| Collision Count | N/A | Pairs detected this frame |
| Players in Collision | N/A | Active collision state count |
| GC Allocations | 0 | Should be zero in collision systems |

**Tasks**:
- [ ] **Create CollisionProfilerOverlay.cs**:
  - [ ] Display metrics in top-right corner
  - [ ] Color-code: Green (<50% budget), Yellow (50-80%), Red (>80%)
  - [ ] Show frame graph (last 60 frames)
  - [ ] Toggle via: `collision.debug.profiler`
- [ ] **Add profiler marker integration**:
  - [ ] Ensure all collision systems have ProfilerMarker
  - [ ] Query marker timing via ProfilerRecorder
  - [ ] Calculate 99th percentile over rolling window
- [ ] **Implement budget alert system**:
  - [ ] Flash warning when exceeding 2ms total
  - [ ] Log warning with offending system name
  - [ ] Auto-reduce quality if sustained overage (optional)
- [ ] **Add memory tracking**:
  - [ ] Track NativeContainer allocations
  - [ ] Alert on any managed allocation in collision code
  - [ ] Show current memory usage vs capacity
- [ ] **Create export for CI regression testing**:
  - [ ] Write performance metrics to JSON after benchmark
  - [ ] Compare against baseline (fail if >20% regression)
  - [ ] Include in automated test pipeline

**Sub-Epic 7.9.5: Collision Replay System** *(Not Started)*
**Goal**: Record and playback collision sequences for debugging and design review
**Design Notes**:
- Record entity states, inputs, and collision events over time
- Playback with scrubbing (pause, rewind, slow-mo)
- Export as video or data file for sharing

**Tasks**:
- [ ] **Create CollisionRecorder.cs**:
  - [ ] Record player positions, velocities, states each frame
  - [ ] Record collision events with full context
  - [ ] Store in efficient binary format (not JSON)
  - [ ] Circular buffer for last N seconds (configurable, default 30s)
- [ ] **Implement CollisionPlayback.cs**:
  - [ ] Load recording file
  - [ ] Replay entities as ghosts (non-interactive)
  - [ ] Playback controls: Play, Pause, Step, Speed (0.25x-4x)
  - [ ] Scrub timeline with slider
- [ ] **Add collision event markers on timeline**:
  - [ ] Show collision events as colored dots on timeline
  - [ ] Click to jump to that moment
  - [ ] Filter by event type
- [ ] **Create recording UI**:
  - [ ] Start/Stop recording buttons
  - [ ] Save recording to file
  - [ ] Load recording for playback
  - [ ] Trim recording (start/end)
- [ ] **Export capabilities**:
  - [ ] Export as GIF/MP4 (requires ffmpeg or Unity Recorder)
  - [ ] Export as JSON for data analysis
  - [ ] Share recordings between developers

**Sub-Epic 7.9.6: Collision Test Scene** *(Not Started)*
**Goal**: Dedicated scene for testing collision behaviors in isolation
**Design Notes**:
- Controllable player spawning with precise positioning
- Automated collision scenario execution
- Integrates with Epic 7.8 test cases

**Tasks**:
- [ ] **Create CollisionTestScene.unity**:
  - [ ] Flat ground plane with grid markings
  - [ ] No environment obstacles (pure collision testing)
  - [ ] Camera positioned for clear overview
- [ ] **Create CollisionTestSpawner.cs**:
  - [ ] Spawn N players at specified positions
  - [ ] Configure velocity, stance, facing direction
  - [ ] Presets: Head-On, Side-Swipe, Pile-Up, Wall-Pin
- [ ] **Implement scenario execution**:
  - [ ] Run test case with expected outcome
  - [ ] Display pass/fail result
  - [ ] Log detailed comparison (expected vs actual)
- [ ] **Add manual control mode**:
  - [ ] Control Player A with WASD, Player B with arrows
  - [ ] Force stagger/knockdown buttons for testing animations
  - [ ] Adjust velocity multiplier in real-time
- [ ] **Create test harness UI**:
  - [ ] Dropdown for selecting test scenario
  - [ ] Run button with progress indicator
  - [ ] Results panel with detailed diff

**Files to Create**:
- `Assets/Scripts/Debug/CollisionDebugOverlay.cs`
- `Assets/Scripts/Debug/CollisionEventLogger.cs`
- `Assets/Scripts/Debug/CollisionTuningUI.cs`
- `Assets/Scripts/Debug/CollisionProfilerOverlay.cs`
- `Assets/Scripts/Debug/CollisionRecorder.cs`
- `Assets/Scripts/Debug/CollisionPlayback.cs`
- `Assets/Scripts/Debug/CollisionTestSpawner.cs`
- `Assets/Scenes/CollisionTestScene.unity`

**Files to Modify**:
- `Assets/Scripts/Player/Components/PlayerCollisionSettings.cs` (add tuning accessors)
- `Assets/Scripts/Player/Systems/PlayerProximityCollisionSystem.cs` (add logging hooks)
