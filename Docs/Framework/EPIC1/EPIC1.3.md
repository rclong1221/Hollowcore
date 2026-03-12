### Epic 1.3: Enhanced Input System ✅ COMPLETE BUT TEST TO CONFIRM
**Priority**: HIGH  
**Goal**: Support all player actions (movement, camera, interaction, tools)

**Tasks**:
- [X] Create `Player/Components/PlayerInputComponent.cs`
- [X] Define `PlayerInput` struct with:
  - [X] Movement: `Horizontal`, `Vertical`, `Jump`, `Crouch`, `Sprint`
  - [X] Camera: `LookDelta` (float2), `ZoomDelta` (float)
  - [X] Interaction: `Interact`, `Use`, `AltUse`
  - [X] Tools: `ToolSlotDelta`, `Reload`, `ToggleFlashlight`
  - [X] Lean: `LeanLeft`, `LeanRight`
- [X] Define `InputEvent` struct (IsSet, FrameCount for press/hold detection)
- [X] Create `Player/Systems/PlayerInputSystem.cs`
- [X] Extract input reading logic from `PlayerInputAuthoring.cs`
- [X] Implement keyboard/mouse input reading (both Input System and legacy Input)
- [X] Add input buffering for prediction/rollback compatibility
- [ ] Test all input actions register correctly