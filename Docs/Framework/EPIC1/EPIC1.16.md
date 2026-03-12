### Epic 1.16: Dodge Dive System (ends in Prone)
**Priority**: MEDIUM
**Goal**: Add a forward dive move that transitions the player into a prone state at the end of the dive (combo-friendly with Dodge Roll), including animation, stamina use, and safe stand-up logic.

**Tasks**:
- [X] Create `Player/Components/DodgeDiveComponent.cs` with parameters: `DiveDistance`, `DiveDuration`, `EndInProne` (true), `StaminaCost`, and `Cooldown`.
- [X] Add `DodgeDiveAuthoring` + Baker to expose tuning values.
- [X] Implement `Player/Systems/DodgeDiveSystem.cs` to process dive input, produce displacement requests compatible with `CharacterControllerSystem`, trigger `ProneState` at the end, and manage invulnerability/rollup frames.
- [ ] Add animation clip(s) and Animator events: start dive, hit ground, transition to prone; wire these through `AnimatorEventBridge`/`AnimatorRigBridge` for client visuals and `PlayerAnimationState` updates.
- [X] Integrate with existing stamina, prevent dive when insufficient, and add combo rules (e.g., cannot roll immediately from dive unless exhausted/allowed).
- [X] Ensure network prediction/reconciliation works (client-side cosmetic interpolation while server authoritatively resolves final position and `ProneState`).
- [ ] Add PlayMode QA scene and tests: dive into prone near obstacles, verify safe stand-up checks, verify correct audio/VFX triggers on landing.

**Acceptance criteria**:
- Dive executes on input, moves player the expected distance, deducts stamina, and reliably sets `ProneState` at end.
- Animations and audio/VFX (footstep/landing) trigger as expected; server authoritative state remains consistent.
- Safe-stand checks and obstacle handling prevent pop-through behavior when transitioning out of prone.