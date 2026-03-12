### Epic 1.14: Dodge Roll System
**Priority**: MEDIUM
**Goal**: Fast, responsive evade roll with brief invulnerability, stamina cost, and animation/IK support while keeping DOTS authoritative for gameplay.

**Tasks**:
- [X] Create `Player/Components/DodgeRollComponent.cs` (IComponentData) holding roll parameters: `Duration`, `Distance`, `InvulnWindowStart`, `InvulnWindowEnd`, `StaminaCost`.
- [X] Add `DodgeRollAuthoring` + Baker to author per-prefab tuning values into entities.
- [X] Implement `Player/Systems/DodgeRollSystem.cs` (predicted/systematic): applies displacement requests, sets temporary movement lock, manages invulnerability windows, and consumes stamina.
- [X] Wire input: map dodge-roll key/button in `PlayerInput`/`PlayerInputComponent` (e.g., double-tap directional or dedicated key) and sample in `PlayerInputSystem`/`PlayerInputWriterSystem`.
- [X] Add client-side `DodgeRollAnimatorBridge`/animation events so local players see roll animation and IK; keep DOTS authoritative for actual position/replication.
- [X] Add rollback/prediction handling for authoritative server reconciliation (ensure no long pop on correction).
- [x] Add layer-based ignores (floor vs dynamic objects) so the heuristic for floor hits is stricter.
- [x] Replace the surface-normal heuristic with additional checks (hit entity, hit point height) for robustness?
- [ ] Add PlayMode QA scene (open area with incoming hazards) and automated tests for timing, invulnerability window, and stamina interactions.

**Acceptance criteria**:

- [X] Create `Player/Components/DodgeRollComponent.cs` (IComponentData) holding roll parameters: `Duration`, `Distance`, `InvulnWindowStart`, `InvulnWindowEnd`, `StaminaCost`.
- [X] Add `DodgeRollAuthoring` + Baker to author per-prefab tuning values into entities.
- [X] Implement core `Player/Systems/DodgeRollSystem.cs` (start/track rolls, apply cosmetic and DOTS displacement requests, manage invulnerability window, consume stamina).  
  - Note: Core system implemented; NetCode prediction/rollback still pending.
- [X] Wire input: added `DodgeRoll` to networked `PlayerInput` struct, updated new-input and legacy sampling to set `DodgeRoll` on `Ctrl+Space`, and ensured hybrid writer copies into `PlayerInputComponent` for local players.
- [X] Add client-side `DodgeRollAnimatorBridge` adapter and example `TriggerRoll()` cosmetic trigger that designers can extend for animations/IK.
- [X] Added `DodgeRollInvuln` tag and updated `ApplyDamageAdapterSystem` to skip damage when tag is present (invuln window enforcement).
- [X] Integrated with movement: added `ComputeDodgeDispJob` wiring in `CharacterControllerSystem` to enqueue MoveRequests for active rolls.
- [X] Added `PlayerStamina` consumption on roll start (respects existing stamina component if present).

**Remaining / Pending**

- [X] Add rollback/prediction handling for authoritative server reconciliation (ensure no long pop on correction).
  - Implemented smooth reconciliation using `ServerElapsed`, `IsReconciling`, and `ReconcileSmoothing` fields in `DodgeRollState`
  - Client smoothly blends predicted elapsed time toward server authoritative time over ~200ms
  - Reconciliation automatically disables when synchronization is within 10ms threshold
- [X] Add layer-based ignores (floor vs dynamic objects) so heuristic for floor hits is stricter
  - Added `CollisionLayerMask` to `DodgeRollComponent` for filtering collision layers
  - Exposed in `DodgeRollAuthoring` with designer-friendly tooltip
- [X] Replace surface-normal heuristic with additional checks (hit entity, hit point height) for robustness
  - Added `MinFloorNormalY` (default 0.7 for ~45° slopes) for better floor detection
  - Added `MaxFloorHeight` (default 0.2m) to validate hit point is near ground level
  - Both parameters exposed in authoring component with Range sliders
- [ ] Add PlayMode QA scene (open area with incoming hazards) and automated tests for timing, invulnerability window, and stamina interactions.

**Acceptance criteria**:
- Dodge rolls trigger on configured input, move player by expected distance, and respect `Stamina` and cooldowns.
- Invulnerability window prevents damage only during configured frames and does not persist after roll.
- Animations/IK are cosmetic only on client and do not desync authoritative player state.