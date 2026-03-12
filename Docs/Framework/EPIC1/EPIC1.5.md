### Epic 1.5: Ground Movement System ✅ COMPLETE
**Priority**: HIGH  
**Goal**: Implement walk, run, sprint, jump with proper physics

**Tasks**:
- [X] Create `Player/Components/PlayerSettingsComponent.cs`
- [X] Define `PlayerMovementSettings` component:
  - [X] Speed multipliers (WalkSpeed: 2 m/s, RunSpeed: 4 m/s, SprintSpeed: 7 m/s, CrouchSpeed: 2 m/s, ProneSpeed: 1 m/s)
  - [X] Jump settings (JumpForce: 5 m/s, Gravity: -9.81 m/s², MaxFallSpeed: -20 m/s)
  - [X] Acceleration (GroundAcceleration: 20 m/s², AirAcceleration: 2 m/s², Friction: 10)
- [X] Define `PlayerJumpState` component (coyote time, jump buffering)
- [X] Define `PlayerStamina` component (Current, Max, DrainRate, RegenRate)
- [X] Update `Player/Systems/PlayerMovementSystem.cs`
- [X] Implement ground movement (acceleration, friction, max speed)
- [X] Implement air movement (reduced control)
- [X] Implement jump mechanics (coyote time: 0.1s, jump buffering: 0.1s)
- [X] Create `Player/Systems/PlayerGroundCheckSystem.cs`
- [X] Implement raycast-based ground detection
- [X] Create `Player/Systems/PlayerStaminaSystem.cs`
- [X] Implement stamina drain when sprinting (20/sec)
- [X] Implement stamina regeneration when not sprinting (10/sec)
- [X] Prevent sprinting when stamina depleted
- [X] Test movement feels responsive and natural