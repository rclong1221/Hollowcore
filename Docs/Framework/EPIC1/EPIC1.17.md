### Epic 1.17: Hanging & Ledge Movement System
**Priority**: MEDIUM
**Goal**: Allow players to hang from ledges with lateral movement, transition to climbing vertically, or mantle up onto the ledge.

**Tasks**:
- [ ] Create `Player/Components/HangingComponents.cs`:
  - [ ] Define `HangingState` component:
    - [ ] `IsHanging` (bool), `HangStartTime` (float), `LedgePosition` (float3), `LedgeNormal` (float3)
    - [ ] `LateralOffset` (float), `GripStrength` (float, 0-100)
  - [ ] Define `HangingSettings` component:
    - [ ] `MaxHangDuration` (20s), `GripDrainRate` (5/sec), `LateralMoveSpeed` (1.5 m/s)
    - [ ] `ClimbTransitionThreshold` (vertical input magnitude for climb), `MantleUpThreshold` (upward input for mantle)
    - [ ] `StaminaCostPerSecond` (2), `MinGripToHang` (10)
- [ ] Create `Player/Systems/HangingDetectionSystem.cs`:
  - [ ] Detect when player is falling near a ledge (raycast forward + down from hands)
  - [ ] Validate ledge is grabbable (sufficient width, player velocity allows catch)
  - [ ] Add `HangingCandidate` component when valid
  - [ ] Allow manual grab input when near ledge
- [ ] Create `Player/Systems/HangingExecutionSystem.cs`:
  - [ ] Lock normal movement when `IsHanging == true`
  - [ ] Position player below ledge at fixed hang offset
  - [ ] Handle lateral movement input (left/right along ledge)
  - [ ] Drain grip strength and stamina over time
  - [ ] Detect transitions:
    - [ ] Vertical input up → trigger climb system or mantle up
    - [ ] Jump input → dismount (controlled drop or leap back)
    - [ ] Grip depleted → forced drop
  - [ ] Apply fatigue effects when grip low (shaking, reduced lateral speed)
- [ ] Create `Player/Systems/HangingStaminaSystem.cs`:
  - [ ] Drain stamina while hanging
  - [ ] Drain grip strength faster when stamina depleted
  - [ ] Prevent hang if stamina too low
- [ ] Integration with existing systems:
  - [ ] Wire to `ClimbingSystem` (vertical input while hanging → start climbing)
  - [ ] Wire to `MantleSystem` (mantle up input while hanging → mantle onto ledge)
  - [ ] Update `PlayerStateSystem` to recognize `Hanging` as valid `PlayerMovementState`
- [ ] Add client-side `HangingAnimatorBridge` MonoBehaviour:
  - [ ] Expose `IsHanging` bool parameter
  - [ ] Expose `LateralMovement` float parameter (-1 to 1)
  - [ ] Expose `GripStrength` float parameter (0 to 1)
  - [ ] Optional IK hand placement on ledge
  - [ ] Receive animation events for grip slip sounds, fatigue breathing
- [ ] Add `HangingAuthoring` + Baker for designer-friendly configuration
- [ ] Add `HangingAnimationTriggerSystem` for client presentation layer
- [ ] Create QA test scene:
  - [ ] Various ledge heights and widths
  - [ ] Obstacles requiring lateral movement while hanging
  - [ ] Integration test: hang → lateral move → mantle up sequence
  - [ ] Integration test: hang → vertical input → climb sequence

**Acceptance criteria:**
- Player can catch ledges while falling and enter hanging state
- Lateral movement along ledge works smoothly with directional input
- Grip strength drains over time; forced drop when depleted
- Vertical input transitions to climbing system
- Mantle up input transitions to mantle system
- Jump input performs controlled dismount
- Animations and IK show proper hand placement on ledge
- Stamina system integrates properly (drains while hanging, affects grip)
- Network prediction maintains smooth hanging state for local player

**Outstanding / Follow-ups:**
- [ ] PlayMode QA scene with varied ledge scenarios
- [ ] Advanced grab detection (auto-grab on jump near ledge)
- [ ] Shimmy around corners (90-degree ledge transitions)
- [ ] Dynamic ledges (hanging from moving platforms/ships)