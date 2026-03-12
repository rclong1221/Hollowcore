### Epic 1.4: Player State Machine ✅ COMPLETE
**Priority**: HIGH  
**Goal**: Track player mode, stance, and movement state

**Tasks**:
- [X] Create `Player/Components/PlayerStateComponent.cs`
- [X] Define `PlayerState` component:
  - [X] `Mode` (InShip, EVA, Piloting, Dead, Spectating)
  - [X] `Stance` (Standing, Crouching, Prone)
  - [X] `MovementState` (Idle, Walking, Running, Jumping, Falling, Climbing, Swimming)
  - [X] `IsGrounded` (bool)
  - [X] `GroundCheckDistance` (float)
- [X] Define enums: `PlayerMode`, `PlayerStance`, `PlayerMovementState`
- [X] Create `Player/Systems/PlayerStateSystem.cs`
- [X] Implement state transitions based on input and physics
- [X] Create `Player/Systems/PlayerStanceSystem.cs`
- [X] Implement crouch/prone transitions
- [X] Adjust collider height based on stance (2m standing, 1m crouching, 0.5m prone)
- [X] Add stance change cooldowns to prevent spam