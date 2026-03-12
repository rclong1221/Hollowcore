### Epic 1.15: Prone System
**Priority**: MEDIUM
**Goal**: Allow players to enter a prone state (laying) with reduced collider height, optional crawl, and transitions (stand↔crouch↔prone) that work consistently across DOTS and hybrid presentation.

**Tasks**:
- [X] Create `Player/Components/ProneStateComponent.cs` (IComponentData) with `IsProne`, `IsCrawling`, `TransitionTimer`, and movement modifiers (speed multiplier, stealth modifier).
- [X] Add `ProneAuthoring` + Baker for default per-prefab settings.
- [X] Update `CharacterControllerSettings`/controller to support a lower capsule height and safe stand-up checks (raycast overhead clearance) when exiting prone.
- [X] Add animation parameters (e.g., `IsProne`, `IsCrawling`) and ensure `AnimatorRigBridge` maps them via `PlayerAnimationState`/`PlayerAnimationStateSystem`.
- [X] Add input mapping (toggle prone, crawl forward/back) to `PlayerInput`/`PlayerInputComponent` and ensure hybrid writer populates states for local players.
- [X] Implement `Player/Systems/ProneSystem.cs` to handle transitions, speed modifiers, and blocking actions (cannot sprint while prone, limited interaction range).
- [X] Add UI affordances and camera adjustment when prone (lower FOV/offset) inside `PlayerCameraControlSystem` when entity has `ProneState`.
  - [X] Height-based camera pivot offset scaling (uses `PlayerState.CurrentHeight`)
  - [X] Height-based FOV adjustment (wider FOV when prone for peripheral vision)
  - [X] Fixed FOV exponential growth bug by using `BaseFOV` from settings
- [ ] PlayMode QA scene and tests: verify enter/exit prone in confined spaces, stand-up safety, crawling movement feel, and network parity.

**Acceptance criteria**:
- Players can enter and exit prone reliably; stand-up is blocked when overhead clearance is insufficient.
- Crawl movement uses configured speed modifiers and integrates with stamina and character collision.
- Animator and camera visuals reflect prone/crawl state without altering authoritative DOTS state.
- Camera automatically adjusts to character height for any stance (rig-agnostic, works with all humanoid characters).

**Implementation Notes**:
- **Height-based camera system**: Camera pivot and FOV adjust dynamically based on `PlayerState.CurrentHeight` rather than checking prone state directly. This makes the system universal and works automatically for any character height/posture.
- **Rig-agnostic design**: Uses capsule collider height (not skeleton bones), so it works with any character rig (tall aliens, short goblins, custom proportions).
- **Per-character tuning**: Each character prefab can set its own stance heights via `PlayerStanceConfig` or `ProneAuthoring`; camera scales proportionally.
- **FOV calculation**: Uses `PlayerCameraSettings.BaseFOV` (unmultiplied base value) to prevent exponential growth. Prone characters get ~10% wider FOV (60° → 66°) for better situational awareness.

**Future Work: Multi-Race/Species Camera System**:
- [ ] Create race/species data component (`CharacterRaceData`) with base measurements:
  - [ ] `BaseStandingHeight` (e.g., Human: 2.0m, Alien: 3.0m, Goblin: 1.2m)
  - [ ] `EyeHeightRatio` (eyes as % of total height, e.g., 0.85 for most humanoids)
  - [ ] `CrouchHeightRatio` (crouch height as % of standing, default: 0.5)
  - [ ] `ProneHeightRatio` (prone height as % of standing, default: 0.25)
- [ ] Update `PlayerCameraSettings.Default` to use race-specific base values:
  - [ ] `PivotOffset.y = raceData.BaseStandingHeight * raceData.EyeHeightRatio`
  - [ ] `FPSOffset.y = raceData.BaseStandingHeight * raceData.EyeHeightRatio + 0.1m`
- [ ] Create per-race camera profiles (ScriptableObjects):
  - [ ] Human profile: 2.0m standing, 1.6m eye height, standard 60° FOV
  - [ ] Alien profile: 3.0m standing, 2.55m eye height, 65° FOV (wider peripheral)
  - [ ] Goblin profile: 1.2m standing, 1.0m eye height, 70° FOV (compensate for low perspective)
- [ ] Add camera profile assignment in character authoring/spawning:
  - [ ] `PlayerAuthoring` references race-specific `CameraProfile` ScriptableObject
  - [ ] Baker applies race profile values to `PlayerCameraSettings` at spawn
- [ ] Support race-specific FOV modifiers for gameplay balance:
  - [ ] Larger species: narrower base FOV (limited peripheral vision)
  - [ ] Smaller species: wider base FOV (compensate for low viewpoint)
  - [ ] Maintain height-based prone FOV bonus across all races
- [ ] Test and tune camera feel for each race:
  - [ ] Verify tall characters don't feel "floaty" (ground distance visual reference)
  - [ ] Verify short characters don't feel claustrophobic (adequate FOV)
  - [ ] Ensure camera transitions feel natural when stance changes
- [ ] Document best practices for designers:
  - [ ] Guidelines: standing height 1.0m–4.0m, eye height 75–90% of standing
  - [ ] FOV range: 55°–75° base (extremes affect gameplay balance)
  - [ ] Provide template prefabs for common archetypes (small/medium/large humanoid)

**Design Considerations**:
- Current system already supports this via `heightRatio = CurrentHeight / 2.0m` normalization; future work replaces hardcoded 2.0m with `raceData.BaseStandingHeight`
- All camera scaling math remains the same; only base reference values change per-race
- Maintain backward compatibility: default to human values (2.0m) if `CharacterRaceData` absent