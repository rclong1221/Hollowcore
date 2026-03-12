### Epic 1.9: Climbing System
**Priority**: MEDIUM  
**Goal**: Climb ladders, pipes, and rock walls
**Status**: ✅ COMPLETE (Core system complete, advanced voxel integration deferred)

**Tasks**:
- [X] Define `ClimbableObject` component:
  - [X] `Type` (Ladder, Pipe, RockWall)
  - [X] `TopPosition`, `BottomPosition`, `ClimbDirection`
  - [X] `ClimbSpeed` (configurable per-climbable)
- [X] Define `ClimbingState` component:
  - [X] `IsClimbing`, `ClimbTarget`, `ClimbProgress`
- [X] Add `ClimbCandidate` helper component to mark nearest climbable
- [X] Create `Player/Systems/ClimbDetectionSystem.cs` (marks nearest climbable within interaction radius)
- [X] Create `Player/Systems/ClimbInteractionSystem.cs` (starts climb on local Jump input)
- [X] Create `Player/Systems/ClimbExecutionSystem.cs` (moves player along climbable Bottom->Top)
- [X] Create `Player/Systems/ClimbingMovementSystem.cs` to override normal movement while climbing
- [X] Read vertical input for up/down movement and horizontal movement for pipes
- [X] Handle dismount (Jump key or reach top/bottom)
- [X] Create `Player/Systems/ClimbStaminaSystem.cs` to drain stamina for rock walls
- [X] Show "Press Space to Climb" interaction prompt (UI) - `ClimbPromptUI.cs`
- [X] Add `ClimbAnimatorBridge` MonoBehaviour to handle climb visuals and IK:
  - [X] Drive per-frame IK hand/foot targets, rig weights, and `ClimbProgress` animator params.
  - [X] Optionally translate animator root-motion along anchors into predicted MoveRequests consumed by DOTS movement systems.
  - [X] Subscribe to animator events (`OnGrabAnchor`, `OnReleaseAnchor`) and forward to Audio/VFX systems.

**CLIMBABLE OBJECT VARIETY** (Diverse surfaces for fluid multi-object climbing):
- [X] Add climbable spheres and cylinders to TraversalObjectCreator.cs:
  - [X] CreateClimbableSphere() - Spherical rock/boulder (2m, 3m, 4m radius options)
  - [X] CreateClimbableCylinder() - Vertical pipe/column (various heights)
  - [X] CreateClimbablePipe() - Horizontal traversable pipe
- [X] Add climbable walls with varied geometry:
  - [X] CreateAngledClimbWall() - Walls at 60°, 75°, 80° angles
  - [X] CreateOverhangWall() - Negative angle overhang (100°, 110°)
  - [X] CreateCurvedClimbWall() - Concave/convex curved surfaces
- [X] Add composite climbable structures:
  - [X] CreateClimbableArch() - Archway with climbable underside
  - [X] CreateClimbableBridge() - Multi-section bridge structure
  - [X] CreateClimbableColumn() - Pillar with wrap-around climbing
  - [X] CreateClimbableTower() - Multi-level climbable tower
- [X] Add dimension variations to all climbables (width, height, depth presets)
- [X] Update CreateCompleteTestCourse() to include "Advanced Climbables" section
- [X] Ensure all new climbables use ClimbableObjectAuthoring with proper Type assignment

**DEFERRED TASKS** (Until after model integration and voxels):
- [ ] Implement more advanced mount detection (raycast forward + auto-aim)
  - Status: Work in progress. Core detection pipeline has been jobified and a job-friendly voxel anchor provider was added; remaining work focuses on integrating voxel metadata, accurate DOTS collider sweeps, model-assisted prediction, and QA/tests.
  - [x] Implement voxel-aware mount sampling (partial)
    - [x] Define a job-friendly anchor provider (blob) and `VoxelAnchorProvider` component + authoring baker (created).
    - [x] Add a Burst `VoxelRefineJob` that consumes ray hits and the anchor blob (selects nearest baked anchor) — fallback to hit point when blob absent.
    - [ ] Integrate real per-chunk voxel metadata: expose top-surface / ledge extents in job-friendly form (BlobAsset or NativeArrays) and use them in the refine job.
    - Acceptance: `VoxelClimbHelper` still exists as a managed fallback; final goal is the refine job to produce `MountAnchor` info (Position, Normal, Top/Bottom) directly in Burst.
  - [ ] Implement model-assisted mount-point prediction (auto-aim)
    - [ ] Define a predictor interface (job-friendly and managed) so either an ML runtime or a deterministic heuristic can be plugged in.
    - [ ] Integrate a model runtime adapter (optional) and a deterministic fallback (score by distance, normal alignment, visibility).
    - Acceptance: detection system can query the predictor and receive ranked mount candidates; fallback operates when model not present.
  - [ ] Add accurate DOTS sphere-sweep detection path
    - [ ] Implement a DOTS-native collider-sweep (CastCollider) path; evaluate whether to enable `unsafe` or to use a safe API provided by the Unity.Physics version in use.
    - [ ] Add a collider blob LRU cache (BlobAssetReference<Collider>) and prewarm tooling (reuse `CharacterControllerSystem` cache patterns where appropriate).
    - Acceptance: sphere-sweep detection produces more reliable anchors on curved and voxel terrain than multi-ray sampling.
  - [x] Provide a safe multi-ray fallback and tuning presets (partial)
    - [x] Jobified multi-ray sampling implemented (`RaycastSamplesJob`) and mapped to climbables in a Burst `MapToClimbablesJob`.
    - [x] Layer-mask support added for DOTS raycasts (maps `ClimbSensorConfig.LayerMask` to `CollisionFilter`).
    - [ ] Add `ClimbSensorConfig` quality presets (Low/Medium/High) that tune radial/fan sample counts and switch between multi-ray vs. collider-sweep.
    - Acceptance: fallback produces acceptable results for projects that cannot use collider-cast APIs.
  - [ ] Voxel chunk metadata & baker support
  - [ ] QA scene and automated PlayMode tests  
  - [ ] Performance profiling, tuning, and safety checks
  - [ ] Documentation & data-driven settings
- [X] Test climbing feels smooth and responsive (core behavior validated)

---

## FREE-CLIMBING ENHANCEMENT (Peak-Style Climbing)

**Priority**: HIGH  
**Goal**: Transform rail-based climbing into grip-point-based free-climbing for rock walls  
**Status**: 🔲 NOT STARTED

**Context**: Current implementation moves player along a fixed Bottom→Top line using a single `Progress` float (0-1). This feels like an "elevator" rather than actual climbing. Free-climbing requires discrete grip points, two-handed movement, and player-controlled navigation across 2D surfaces.

### Phase 1: Enhanced Ladder Climbing (Quick Wins)

Improve ladder feel while keeping rail-based movement:

- [ ] **Add rung snapping to ladder movement**
  - Add `RungSpacing` field to `ClimbableObject` component (default 0.3m)
  - Add `RungCount` computed property (distance / spacing)
  - Modify `ClimbingMovementSystem` to snap Progress to discrete rung intervals
  - Add `CurrentRung` to `ClimbingState` for tracking which rung player is on
  - Trigger hand-switch animation event on each rung transition
  - Acceptance: player visibly "steps" between rungs rather than gliding smoothly

- [ ] **Add lateral movement for pipes and walls**
  - Add `SurfaceWidth` field to `ClimbableObject` component
  - Add `LateralOffset` field to `ClimbingState` (range -0.5 to 0.5 of surface width)
  - Read `Horizontal` input in `ClimbingMovementSystem`
  - Compute final position as: `lerp(Bottom, Top, Progress) + right * LateralOffset * Width`
  - Clamp lateral offset to prevent falling off edges
  - Acceptance: player can shimmy left/right on pipes and walls

- [ ] **Add hand alternation tracking**
  - Add `LeadingHand` enum (Left/Right) to `ClimbingState`
  - Toggle leading hand every N progress units (e.g., every 0.1 = 10% of climb)
  - Expose `LeadingHand` to animation bridge for hand-over-hand animation
  - Add animator parameter `LeadingHand` (0 = left, 1 = right)
  - Acceptance: animation shows alternating hand placement during climb

- [ ] **Add grip stamina variations by surface type**
  - Define stamina drain rates per `ClimbableType`: Ladder (0x), Pipe (0.5x), RockWall (1x)
  - Add `GripStaminaCost` field to `ClimbableObject` for per-object override
  - Modify `ClimbStaminaSystem` to use type-based drain
  - Add stamina recovery when stationary on ledges (Progress = 0 or 1)
  - Add visual feedback (hand shake, screen effect) when stamina < 25%
  - Force dismount when stamina reaches 0
  - Acceptance: rock walls feel more challenging than ladders

### Phase 2: Grip Point System (Core Free-Climbing)

Introduce discrete grip points for rock wall surfaces:

- [ ] **Define grip point data structures**
  - Create `GripPoint` struct with fields:
    - `Position` (float3) - world position of grip
    - `Normal` (float3) - surface normal at grip
    - `Difficulty` (float 0-1) - stamina cost multiplier
    - `Type` (enum: Hold, Ledge, Crack, Pinch, Jug, Sloper)
    - `ReachRadius` (float) - how far player can reach from this grip
  - Create `GripPointBuffer` dynamic buffer component for storing grips on climbables
  - Create `GripPointBlob` BlobAsset for read-only grip data access in jobs
  - Acceptance: grip data can be authored and baked efficiently

- [ ] **Create grip point authoring and baker**
  - Create `GripPointAuthoring` MonoBehaviour for placing grips in editor
  - Support manual placement (child GameObjects as grip positions)
  - Support grid-based auto-generation (grips every N meters within bounds)
  - Support noise-based randomization (offset grips for natural look)
  - Create baker to convert grip GameObjects into `GripPointBuffer` entries
  - Optionally bake to BlobAsset for large surfaces
  - Add Gizmo visualization (spheres with color by difficulty)
  - Acceptance: designers can place or auto-generate grips on surfaces

- [ ] **Create grip reachability system**
  - Create `GripReachabilitySystem` in SimulationSystemGroup
  - For each climbing player, find all grips within reach radius of current hand positions
  - Store reachable grips in `ReachableGripsBuffer` on player entity
  - Consider grip difficulty and player's current stamina
  - Filter by look direction (prioritize grips player is facing)
  - Use spatial hashing or octree for performance with many grips
  - Acceptance: system identifies 5-15 valid next grips each frame

- [ ] **Create grip targeting system**
  - Create `GripTargetingSystem` to select best grip based on player input
  - Combine look direction with movement input to determine target direction
  - Score each reachable grip by: direction alignment, distance, difficulty
  - Select highest-scoring grip as `TargetGrip`
  - Add `TargetGrip` component with target grip entity and position
  - Provide visual feedback: highlight targeted grip (UI or shader)
  - Acceptance: player can intuitively aim at desired grips

### Phase 3: Two-Handed Climbing Movement

Rewrite climbing state to track individual hand positions:

- [ ] **Redesign ClimbingState for two-handed model**
  - Replace single `Progress` with explicit hand tracking:
    - `LeftHandPosition` (float3) - current left hand world position
    - `RightHandPosition` (float3) - current right hand world position  
    - `LeftHandGripEntity` (Entity) - which grip left hand is holding
    - `RightHandGripEntity` (Entity) - which grip right hand is holding
    - `LeftHandAnchored` (bool) - is left hand currently gripping
    - `RightHandAnchored` (bool) - is right hand currently gripping
    - `ActiveHand` (enum) - which hand is currently reaching
    - `ReachProgress` (float 0-1) - animation progress of reach
  - Derive body position from hand positions (midpoint + offset from surface)
  - Derive foot positions via downward grip search from each hand
  - Acceptance: player state accurately tracks both hands

- [ ] **Implement hand reach cycle**
  - On movement input toward target grip:
    - Set `ActiveHand` to the hand closer to target grip
    - Begin reach animation (increase `ReachProgress`)
    - Move active hand position toward target grip over reach duration
  - On reach complete (ReachProgress >= 1):
    - Anchor active hand to target grip
    - Release other hand (ready for next reach)
    - Reset `ReachProgress` to 0
  - If no valid grip at target: hand returns to original position (failed reach)
  - Acceptance: climbing feels like alternating hand movements

- [ ] **Compute body position from hands**
  - Body position = midpoint of left and right hands + body offset along surface normal
  - Body offset distance based on character capsule radius
  - Body rotation = face toward surface, up aligned with climb direction
  - Smoothly interpolate body position as hands move
  - Handle edge cases: only one hand anchored (swinging), both unanchored (falling)
  - Acceptance: body naturally follows hand positions

- [ ] **Implement foot position auto-targeting**
  - For each hand, search for grip points below (within leg length range)
  - Prefer grips that maintain balanced stance (left foot under left hand area)
  - Interpolate foot positions when body moves
  - Feet provide stability bonuses (reduce stamina drain when both feet gripping)
  - Acceptance: feet visibly seek grip points during climb

### Phase 4: Climbing IK Integration

Connect two-handed climbing to animation system:

- [ ] **Update ClimbAnimatorBridge for dynamic IK targets**
  - Replace static IK target references with position setters from ECS
  - Create `ClimbIKAdapterSystem` to read hand/foot positions from ClimbingState
  - Push positions to ClimbAnimatorBridge via companion lookup
  - Smooth IK transitions when changing grips
  - Blend IK weight based on hand state (full weight when anchored, reduced when reaching)
  - Acceptance: IK targets follow actual grip positions

- [ ] **Add reach animation support**
  - Create animator blend tree for reach animations (reach up, reach left, reach right, reach down)
  - Drive blend tree with reach direction parameter (`ReachDirection` float2)
  - Trigger reach animation when ReachProgress changes
  - Blend between reach and idle based on `ReachProgress`
  - Acceptance: character visibly reaches toward target grips

- [ ] **Add grip type variation to animations**
  - Map `GripType` enum to animator parameter (`GripTypeId` int)
  - Different hand poses for: Hold (open), Ledge (hook), Crack (insert), Pinch (pinch), Jug (cup)
  - Blend between poses based on current and target grip types
  - Acceptance: hand shapes vary based on grip type

### Phase 5: Dynamic Jump / Dyno System

Enable jumping between distant grips:

- [ ] **Define dyno mechanics**
  - Dyno = jump between grips that are beyond normal reach radius
  - Triggered by: look at distant grip + jump input
  - Stamina cost: proportional to jump distance
  - Success chance: based on distance, stamina, and grip difficulty
  - Miss: player falls (trigger ragdoll or catch with one hand)
  - Acceptance: player can make risky jumps for shortcuts

- [ ] **Create ClimbJumpSystem**
  - Detect jump input during climbing
  - Check if target grip is in dyno range (beyond reach, within jump distance)
  - Calculate jump trajectory (parabola from current to target)
  - Consume stamina based on distance
  - Transition to `ClimbJumping` state during jump
  - Acceptance: jump execution feels responsive

- [ ] **Implement jump success/failure**
  - On jump complete: check distance to nearest grip
  - Success (within grab radius): anchor both hands to grip, resume climbing
  - Partial success (one hand grabs): anchor one hand, player swings
  - Failure (miss): trigger fall with brief catch window
  - Add invulnerability frames during catch window for recovery input
  - Acceptance: near-misses feel tense with recovery opportunity

- [ ] **Add jump animation support**
  - Create dyno animation clips (push off, airborne, catch)
  - Drive via animator triggers: `DynoStart`, `DynoCatch`, `DynoFail`
  - Blend catch animation based on catch success type
  - Acceptance: jumps look dynamic and athletic

### Phase 6: Surface Transition System

Handle moving between climbable surfaces:

- [ ] **Detect adjacent surfaces**
  - When player near edge of climbable, query for adjacent climbables
  - Use physics overlap or authored connection data
  - Create `AdjacentClimbable` buffer on climbable entities
  - Mark valid transition points (where surfaces connect)
  - Acceptance: system knows when surfaces are reachable

- [ ] **Implement surface-to-surface transitions**
  - When reaching beyond current surface toward adjacent surface grip:
    - Transfer player's target hand to new surface
    - Update `ClimbSurface` entity in `ClimbingState`
    - Handle mixed-surface grip (one hand each surface) temporarily
  - When both hands on new surface, complete transition
  - Maintain continuity of animation during transition
  - Acceptance: player can fluidly climb across multiple surfaces

- [ ] **Add corner and overhang handling**
  - Inside corner: hands can reach both adjacent surfaces
  - Outside corner: requires reaching around with body rotation
  - Overhang: inverted surface requires different grip mechanics
  - Roof/ceiling: horizontal climbing mode
  - Adjust stamina costs based on angle (overhangs cost more)
  - Acceptance: player can navigate complex geometry

### Phase 7: Procedural Grip Generation (For Voxels)

Dynamically generate grips on voxel terrain:

- [ ] **Integrate with voxel surface detection**
  - Query voxel SDF for surface positions within climbable zones
  - Generate grips at surface discontinuities (ledges, protrusions)
  - Use surface normal to orient grip direction
  - Filter by climbability (too smooth = no grips)
  - Acceptance: voxel terrain has dynamic grips

- [ ] **Implement runtime grip generation**
  - Generate grips within player's view frustum + buffer zone
  - Cache generated grips (invalidate when voxel chunk modified)
  - Use job system for parallel grip generation
  - Limit grip count per chunk for performance
  - Acceptance: grips appear seamlessly as player explores

- [ ] **Handle grip invalidation**
  - When voxel modified, invalidate affected grips
  - Remove grips that no longer have surface
  - Regenerate grips in modified area
  - If player on invalidated grip: force grab nearest valid grip or fall
  - Acceptance: destructible terrain works with climbing

### Phase 8: Configuration and Presets

Make free-climbing designer-configurable:

- [ ] **Create FreeClimbConfig ScriptableObject**
  - Movement settings: reach speed, body offset, reach radius
  - Stamina settings: base drain, per-grip-type multipliers, recovery rates
  - Dyno settings: max jump distance, stamina cost curve, success thresholds
  - IK settings: blend speeds, pole targets, foot search range
  - Default presets: Realistic, Arcade, Casual
  - Acceptance: designers can tune climbing without code

- [ ] **Create ClimbablePreset ScriptableObject**
  - Surface type: Ladder, Pipe, WallEasy, WallMedium, WallHard, Overhang
  - Default grip density and difficulty distribution
  - Stamina multipliers
  - Can assign preset to ClimbableObjectAuthoring for quick setup
  - Acceptance: consistent difficulty across similar surfaces

- [ ] **Add runtime difficulty adjustment**
  - Support dynamic difficulty based on player performance
  - Track climb success/failure rates
  - Adjust grip difficulty, stamina costs, dyno tolerance
  - Expose difficulty API for game integration
  - Acceptance: climbing adapts to player skill

### Testing Checklist (Free-Climbing)

- [ ] Approach rock wall → grips become visible/highlighted within range
- [ ] Look at grip + press climb → hand reaches toward grip
- [ ] Reach complete → hand anchors, body repositions
- [ ] Movement input → alternating hand movement toward input direction
- [ ] Horizontal input on wall → player shimmies left/right
- [ ] Hold still → stamina slowly drains (faster on hard grips)
- [ ] Reach ledge → stamina recovers
- [ ] Stamina depleted → hands shake, then player falls
- [ ] Look at distant grip + jump → dyno executed
- [ ] Dyno success → catch and resume climbing
- [ ] Dyno miss → fall with brief catch window
- [ ] Near surface edge → can reach to adjacent surface
- [ ] Cross surfaces → seamless transition
- [ ] Destroy voxel under grip → grip removed, player adjusts or falls
- [ ] IK hands/feet match actual grip positions

---

## Implementation Summary

### Files Created/Modified

**Components (ECS)**:
- [ClimbableObject.cs](../Assets/Scripts/Player/Components/ClimbableObject.cs) - Climbable definition (Type, Top/Bottom, Speed, Radius)
- [ClimbingState.cs](../Assets/Scripts/Player/Components/ClimbingState.cs) - Player climbing state (IsClimbing, Target, Progress)
- [ClimbCandidate.cs](../Assets/Scripts/Player/Components/ClimbCandidate.cs) - Marks nearest climbable for player
- [ClimbMountSettings.cs](../Assets/Scripts/Player/Components/ClimbMountSettings.cs) - Mount detection configuration

**Systems (ECS/DOTS)**:
- [ClimbDetectionSystem.cs](../Assets/Scripts/Player/Systems/ClimbDetectionSystem.cs) - Finds nearest climbable within interaction radius
- [ClimbInteractionSystem.cs](../Assets/Scripts/Player/Systems/ClimbInteractionSystem.cs) - Starts climb on player input
- [ClimbExecutionSystem.cs](../Assets/Scripts/Player/Systems/ClimbExecutionSystem.cs) - Moves player along climbable
- [ClimbingMovementSystem.cs](../Assets/Scripts/Player/Systems/ClimbingMovementSystem.cs) - Handles climb input and dismount
- [ClimbStaminaSystem.cs](../Assets/Scripts/Player/Systems/ClimbStaminaSystem.cs) - Drains stamina for rock walls
- [ClimbMountDetectionSystem.cs](../Assets/Scripts/Player/Systems/ClimbMountDetectionSystem.cs) - Advanced mount detection (partial)
- [PlayerClimbAudioSystem.cs](../Assets/Scripts/Player/Systems/PlayerClimbAudioSystem.cs) - Emits ClimbStartEvent for audio

**Events**:
- [ClimbStartEvent.cs](../Assets/Scripts/Player/Events/ClimbStartEvent.cs) - One-shot event for audio playback

**Authoring**:
- [ClimbableObjectAuthoring.cs](../Assets/Scripts/Player/Authoring/ClimbableObjectAuthoring.cs) - Baker for ClimbableObject
- [ClimbableAuthoring.cs](../Assets/Scripts/Player/Authoring/ClimbableAuthoring.cs) - Alternative authoring
- [ClimbSensorConfigAuthoring.cs](../Assets/Scripts/Player/Authoring/ClimbSensorConfigAuthoring.cs) - Detection settings
- [ClimbMountSettingsAuthoring.cs](../Assets/Scripts/Player/Authoring/ClimbMountSettingsAuthoring.cs) - Mount settings

**Bridges (MonoBehaviour Presentation)**:
- [ClimbAnimatorBridge.cs](../Assets/Scripts/Player/Bridges/ClimbAnimatorBridge.cs) - Full-featured climbing animation bridge with:
  - IsClimbing, ClimbProgress, ClimbSpeed animator parameters
  - IK support for hands and feet with blending
  - Root motion support (optional)
  - Animation event receivers (OnGrabAnchor, OnReleaseAnchor, OnClimbStep)
  - UnityEvents for audio/VFX hookup

**UI**:
- [ClimbPromptUI.cs](../Assets/Scripts/UI/ClimbPromptUI.cs) - "Press Space to Climb" interaction prompt

**Utilities**:
- [VoxelClimbHelper.cs](../Assets/Scripts/Player/Utilities/VoxelClimbHelper.cs) - Managed fallback for voxel climbing

**Debug**:
- [ClimbDebugVisualizer.cs](../Assets/Scripts/Debug/ClimbDebugVisualizer.cs) - Editor visualization

---

## Hookup / User Instructions

### 1. Player Prefab Setup

**Architecture Note**: Uses hybrid Ghost/UI prefab pattern:
- **Ghost Prefab** = CharacterController + ECS components (networked)
- **UI Prefab** = Animator + ClimbAnimatorBridge (client-side presentation)

Add to your **UI Prefab** (GameObject with Animator):

```
UI Prefab
├── Animator (required)
├── ClimbAnimatorBridge (required - drives climb animations)
└── [IK Target Transforms - optional for procedural IK]
```

### 2. Component Configuration

#### ClimbAnimatorBridge (Inspector)

| Field | Description | Default |
|-------|-------------|---------|
| **Animator** | Reference to Animator component | Auto-found |
| **ParamIsClimbing** | Bool parameter for climbing state | `IsClimbing` |
| **ParamClimbProgress** | Float (0-1) for climb progress | `ClimbProgress` |
| **ParamClimbSpeed** | Float for vertical climb speed | `ClimbSpeed` |
| **ParamGrabTrigger** | Trigger when grabbing climbable | `ClimbGrab` |
| **ParamReleaseTrigger** | Trigger when dismounting | `ClimbRelease` |
| **EnableIK** | Enable procedural IK for hands/feet | `false` |
| **ClimbingIKWeight** | IK weight when climbing (0-1) | `1.0` |
| **ApplyRootMotion** | Use animator root motion | `false` |

#### ClimbableObjectAuthoring (on climbable objects)

| Field | Description | Default |
|-------|-------------|---------|
| **Type** | Ladder, Pipe, or RockWall | `Ladder` |
| **TopPosition** | World position of climb top | - |
| **BottomPosition** | World position of climb bottom | - |
| **ClimbSpeed** | Meters per second | `2.0` |
| **InteractionRadius** | Detection radius | `2.0` |

### 3. Animator Controller Setup

Create these parameters in your **Animator Controller**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `IsClimbing` | Bool | True while actively climbing |
| `ClimbProgress` | Float | 0 = bottom, 1 = top |
| `ClimbSpeed` | Float | Vertical climb velocity |
| `ClimbGrab` | Trigger | Fires when grabbing climbable |
| `ClimbRelease` | Trigger | Fires when dismounting |
| `ClimbHorizontal` | Float | For pipe/wall lateral movement |

**Animation State Machine Example**:
```
[Locomotion] ──IsClimbing=true──▶ [Climb_Idle]
                                      │
                   ┌──────────────────┴──────────────────┐
                   ▼                                      ▼
           [Climb_Up]                              [Climb_Down]
         (ClimbSpeed > 0)                        (ClimbSpeed < 0)
                   │                                      │
                   └──────────────────┬──────────────────┘
                                      ▼
                              [Climb_Dismount]
                           (IsClimbing=false)
                                      │
                                      ▼
                              [Locomotion]
```

### 4. Animation Events (Optional)

Add these events to your climbing animation clips for precise audio/VFX timing:

| Event Name | When to Place | Purpose |
|------------|---------------|---------|
| `OnGrabAnchor` | When hand contacts surface | Grab sound/VFX |
| `OnReleaseAnchor` | When hand leaves surface | Release sound |
| `OnClimbStep` | Each climbing motion cycle | Footstep/handhold sounds |

### 5. Audio/VFX Integration via UnityEvents

In **ClimbAnimatorBridge** Inspector, wire up these events:

- **OnClimbStart** → `AudioManager.PlayClimbGrabSound()`
- **OnClimbEnd** → `AudioManager.PlayClimbReleaseSound()`
- **OnGrabAnchorEvent** → `VFXManager.SpawnGrabParticle()`
- **OnClimbStepEvent** → `AudioManager.PlayClimbStepSound()`

### 6. IK Setup (Optional)

For procedural IK-driven hand/foot placement:

1. Create 4 empty child GameObjects under your character:
   - `LeftHandIKTarget`
   - `RightHandIKTarget`
   - `LeftFootIKTarget`
   - `RightFootIKTarget`

2. Assign these to ClimbAnimatorBridge's IK Target fields

3. Enable `EnableIK` checkbox

4. An adapter system can call `SetIKTargets()` to position them based on climbable geometry

### 7. UI Prompt Setup

Add `ClimbPromptUI` component to your UI canvas:

1. Create a Text UI element (or TextMeshPro)
2. Add `ClimbPromptUI` component to any GameObject
3. Assign the Text reference to `promptText` field
4. Prompt auto-shows when player has `ClimbCandidate` component

### 8. Testing Checklist

- [ ] Approach ladder → "Press Space to Climb" prompt appears
- [ ] Press Space → Player attaches to ladder, climbing animation plays
- [ ] W/S keys → Player moves up/down ladder
- [ ] Reach top → Player auto-dismounts at top
- [ ] Reach bottom + press S → Player dismounts at bottom
- [ ] Press Space while climbing → Player dismounts
- [ ] Rock wall climbing → Stamina drains
- [ ] Audio plays on grab/release/step events
- [ ] IK hands/feet align to climbable (if IK enabled)

### 9. Code API Reference

```csharp
// Get the bridge
var bridge = GetComponent<ClimbAnimatorBridge>();

// Manually trigger grab/release (usually done automatically)
bridge.TriggerGrab();
bridge.TriggerRelease();

// Check climbing state
if (bridge.IsClimbing)
{
    float progress = bridge.ClimbProgress; // 0-1
}

// Set horizontal input for pipe traversal
bridge.SetHorizontalInput(horizontalInput);

// Update IK targets from adapter
bridge.SetIKTargets(leftHand, rightHand, leftFoot, rightFoot);
```

---

## Data Flow Diagram

```
[Player Near Climbable]
        │
        ▼
[ClimbDetectionSystem] ── finds nearest ──▶ [ClimbCandidate] added
        │                                          │
        ▼                                          ▼
[ClimbPromptUI] shows                     [ClimbInteractionSystem]
"Press Space to Climb"                    (waits for Jump input)
                                                   │
                                                   ▼
                                          [ClimbingState] added
                                          IsClimbing = true
                                                   │
                        ┌──────────────────────────┴──────────────────────────┐
                        ▼                                                      ▼
               [ClimbingMovementSystem]                              [PlayerAnimatorBridgeSystem]
               (reads W/S input, updates Progress)                            │
                        │                                                      ▼
                        │                                            [ClimbAnimatorBridge]
                        │                                            ApplyAnimationState()
                        │                                                      │
                        ▼                                                      ▼
               [Progress 0→1]                                        [Animator Parameters]
               [Position lerped]                                     [IK Weights]
                        │                                            [UnityEvents → Audio/VFX]
                        │                                                      │
        ┌───────────────┴───────────────┐                                      ▼
        ▼                               ▼                              [Animation Plays]
[Reached Top]                    [Reached Bottom]
[Dismount]                       [Dismount]
        │                               │
        └───────────────┬───────────────┘
                        ▼
               [ClimbingState removed]
               [Return to Locomotion]
```

---

## Network Considerations

- **ClimbingState** is a `GhostComponent` with `AllPredicted` prefab type
- **IsClimbing**, **Target**, and **Progress** are all `GhostField` synchronized
- **ClimbDetectionSystem** runs on client and server
- **ClimbInteractionSystem** triggers climb on predicted clients
- **ClimbAnimatorBridge** is client-side only (presentation layer)
- Animation is cosmetic; DOTS state is authoritative