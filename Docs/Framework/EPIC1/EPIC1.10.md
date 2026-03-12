### Epic 1.10: Mantling & Vaulting ✅ COMPLETE (Core Implementation)
**Priority**: MEDIUM  
**Goal**: Smooth traversal over obstacles

**Tasks**:
- [X] Define `MantleState` component:
  - [X] `IsActive`, `Progress`, `Elapsed`, `StartPosition`, `EndPosition`, `Duration`
  - [X] `VaultDirection`, `ObstacleHeight`, `CooldownRemaining`, `StartTick`
- [X] Define `MantleSettings` component:
  - [X] `MaxMantleHeightStanding` (2m), `MaxMantleHeightCrouching` (1m), `MaxVaultHeight` (1.2m)
  - [X] `MantleReachDistance` (0.5m), `MinLedgeWidth` (0.3m)
  - [X] `MantleDuration`, `VaultDuration`, stamina costs, cooldown
- [X] Create `Player/Components/MantleComponents.cs` with `MantleState`, `MantleSettings`, `MantleCandidate`
- [X] Create `Player/Systems/MantleDetectionSystem.cs`
  - [X] Raycast forward to detect obstacles
  - [X] Raycast down from above to find ledge top
  - [X] Verify ledge is wide enough (left/right raycasts)
  - [X] Check clearance above ledge (no low ceiling)
  - [X] Validate height is within stance limits
  - [X] Add `MantleCandidate` component when valid
- [X] Create `Player/Systems/MantleExecutionSystem.cs`
  - [X] Start mantle/vault from candidate component
  - [X] Lock player input during execution (zeros velocity)
  - [X] Smoothly interpolate from start to end position with ease-in-out
  - [X] Parabolic arc trajectory for vaults (clears obstacle with margin)
  - [X] Consume stamina and set cooldown
  - [X] Restore control when complete
- [X] Implement vaulting (sprint + jump near waist-high obstacle)
  - [X] Auto-vault over objects < 1.2m tall
  - [X] Arc trajectory maintains forward momentum
- [X] Create `Player/Authoring/MantleAuthoring.cs` for designer-friendly configuration
- [ ] Add `MantleAnimatorBridge` for animation and IK support
- [ ] Test mantling feels automatic and smooth
- [ ] Create QA test scene with various obstacle heights