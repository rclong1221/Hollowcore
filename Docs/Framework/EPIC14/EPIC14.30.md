# EPIC 14.30: Codebase Refactoring Plan

## Goal
To reduce technical debt by decomposing "God Classes" into smaller, single-responsibility components. This increases readability, testability, and maintainability.

---

## File Size Summary (Current State)

| File | Lines | Priority |
|------|-------|----------|
| WeaponEquipVisualBridge.cs | 3,694 | High |
| TraversalObjectCreator.cs | 2,967 | High |
| CharacterControllerSystem.cs | 1,381 | Medium |
| WeaponAnimationEventRelay.cs | 1,172 | Medium |
| ChunkMeshingSystem.cs | 1,068 | Low |
| PlayerProximityCollisionSystem.cs | 1,038 | Medium |
| ClimbAnimatorBridge.cs | 1,006 | Medium |
| RagdollPresentationBridge.cs | 931 | Medium |

---

## 1. WeaponEquipVisualBridge Refactoring

**Current:** `Assets/Scripts/Items/Bridges/WeaponEquipVisualBridge.cs` (3,694 lines)
**Target Structure:** `Assets/Scripts/Items/Bridges/Visuals/`

### New Files & Responsibilities

#### Core Logic
* **`Assets/Scripts/Items/Bridges/Visuals/WeaponVisualsDispatcher.cs`**
    * *Role:* The new main entry point. Replaces `WeaponEquipVisualBridge`.
    * *Responsibility:* Listens to ECS events and delegates to the appropriate Strategy or Sub-component. Maintains backward compatibility via pass-through properties.

#### Strategy Pattern for Visuals
* **`Assets/Scripts/Items/Bridges/Visuals/Strategies/IWeaponVisualStrategy.cs`**
    * *Role:* Interface defining `Show()`, `Hide()`, `Update()`.
* **`Assets/Scripts/Items/Bridges/Visuals/Strategies/GunVisualStrategy.cs`**
    * *Role:* Handles logic specific to guns (muzzle flashes, shell ejection points, pistol slides).
* **`Assets/Scripts/Items/Bridges/Visuals/Strategies/MeleeVisualStrategy.cs`**
    * *Role:* Handles logic specific to swords/axes (trail renderers, blood effects).
* **`Assets/Scripts/Items/Bridges/Visuals/Strategies/BowVisualStrategy.cs`**
    * *Role:* Handles bow-specific draw/release animations, arrow nocking.

#### Sub-Systems
* **`Assets/Scripts/Items/Bridges/Visuals/WeaponAnimationBridge.cs`**
    * *Extracted From:* Animation state management, Opsive parameter driving.
    * *Role:* Manages the interop between ECS state and Opsive's AnimatorMonitor.
* **`Assets/Scripts/Items/Bridges/Visuals/WeaponVFXController.cs`**
    * *Extracted From:* VFX triggering logic.
    * *Role:* Handles particle instantiation and audio playback requests.
* **`Assets/Scripts/Items/Bridges/Visuals/WeaponSocketResolver.cs`**
    * *Extracted From:* Weapon parent finding and caching.
    * *Role:* Finds the correct Transform bone for a weapon based on ID or Name.
* **`Assets/Scripts/Items/Bridges/Visuals/WeaponReloadController.cs`**
    * *Extracted From:* Reload state machine and animation logic.
    * *Role:* Manages reload state, magazine controller references, and reload animations.

#### Related Files (Already Exist)
These files in the same folder can remain as-is:
* `MecanimAnimatorBridge.cs`
* `ShellSpawnBridge.cs`
* `ThirdPersonViewHandler.cs`
* `WeaponAttachmentConfig.cs`
* `WeaponParentConfig.cs`

---

## 2. TraversalObjectCreator Refactoring

**Current:** `Assets/Editor/TraversalObjectCreator.cs` (2,967 lines)
**Target Structure:** `Assets/Editor/Traversal/` (folder already exists, currently empty)

### New Files & Responsibilities

#### Partial Class Split
Using Partial Classes to organize by feature without over-engineering.

* **`Assets/Editor/Traversal/TraversalObjectCreator.cs`** (Main, ~400 lines)
    * *Role:* Menu items, `CreateCompleteTestCourse()`, shared helper methods.
    * *Contains:* `CreateBox()`, `CreateGroundPlane()`, `CreateHeightLabel()`, `CreateSectionLabel()`, material creation.

* **`Assets/Editor/Traversal/TraversalObjectCreator.Climbing.cs`** (~500 lines)
    * *Role:* Climbing-specific test objects.
    * *Contains:* `CreateClimbingWallSection()`, `CreateAdvancedClimbablesSection()`, SetupClimbableAuthoring, wall/ladder generation.

* **`Assets/Editor/Traversal/TraversalObjectCreator.Movement.cs`** (~400 lines)
    * *Role:* Basic movement test objects.
    * *Contains:* `CreateMantleSection()`, `CreateVaultSection()`, `CreateRampSection()`, `CreateMixedObstacleSection()`.

* **`Assets/Editor/Traversal/TraversalObjectCreator.Swimming.cs`** (~300 lines)
    * *Role:* Swimming/water test objects.
    * *Contains:* `CreateSwimmingSection()`, `CreatePool()`, water volume setup.

* **`Assets/Editor/Traversal/TraversalObjectCreator.Physics.cs`** (~400 lines)
    * *Role:* Physics-based test objects.
    * *Contains:* `CreatePushableSection()`, `CreateRagdollTest()`, `CreateMovingPlatformSection()`, `CreateExternalForceSection()`.

* **`Assets/Editor/Traversal/TraversalObjectCreator.Hazards.cs`** (~300 lines)
    * *Role:* Hazard and special zone test objects.
    * *Contains:* `CreateHazardSection()`, `CreateHorrorSection()`.

* **`Assets/Editor/Traversal/TraversalObjectCreator.Tests.cs`** (~400 lines)
    * *Role:* Specific test suites.
    * *Contains:* `CreateFallTestSection()`, `CreateCrouchTestSection()`, `CreateGapCrossingSection()`.

---

## 3. CharacterControllerSystem Refactoring

**Current:** `Assets/Scripts/Player/Systems/CharacterControllerSystem.cs` (1,381 lines)
**Target Structure:** `Assets/Scripts/Player/Systems/` and `Assets/Scripts/Player/Systems/Cache/`

### New Files & Responsibilities

#### Cache Helper (New Folder)
* **`Assets/Scripts/Player/Systems/Cache/CapsuleColliderCache.cs`** (~150 lines)
    * *Role:* A struct managing `NativeHashMap<long, BlobAssetReference<Collider>>`.
    * *Moves:* `GetOrCreateCapsuleBlob()`, `MakeKey()`, cache hit/miss tracking, deferred blob disposal.
    * *Reasoning:* Cache management is boilerplate that obscures the actual controller logic.

#### Job Extraction
* **`Assets/Scripts/Player/Jobs/CharacterControllerJobs.cs`** (~500 lines)
    * *Role:* Burst-compiled jobs for character movement.
    * *Moves:* `MoveRequest`, `PushRequest` structs, any `IJobEntity` implementations.
    * *Reasoning:* Jobs represent the bulk of the file; separating them clarifies the System's orchestration role.
    * *Note:* `Assets/Scripts/Player/Jobs/` already exists with collision jobs.

#### Simplified System
* **`Assets/Scripts/Player/Systems/CharacterControllerSystem.cs`** (Cleaned, ~400 lines)
    * *Role:* Orchestrates the update loop, schedules jobs, manages dependencies.
    * *Keeps:* `OnCreate()`, `OnDestroy()`, `OnUpdate()`, diagnostic toggles.

---

## 4. ClimbAnimatorBridge Refactoring (NEW)

**Current:** `Assets/Scripts/Player/Bridges/ClimbAnimatorBridge.cs` (1,006 lines)
**Target Structure:** `Assets/Scripts/Player/Bridges/Climbing/`

### Analysis
This file has grown to handle multiple animation event domains beyond just climbing:
- Climbing animation events (FreeClimb, Hang)
- Agility animation events (Dodge, Roll, Vault, Crawl) - EPIC 14.12
- Swimming animation events (Swim, Dive, Drown) - EPIC 14.13
- General Opsive parameter bridging

### New Files & Responsibilities

* **`Assets/Scripts/Player/Bridges/Climbing/ClimbAnimatorBridge.cs`** (Cleaned, ~400 lines)
    * *Role:* Core climbing state, IK control, and climb-specific animator parameters.
    * *Keeps:* `ApplyAnimationState()`, `UpdateClimbingIK()`, `TriggerGrab()`, `TriggerRelease()`.

* **`Assets/Scripts/Player/Bridges/Climbing/ClimbAnimationEventReceiver.cs`** (~150 lines)
    * *Role:* Receives Opsive FreeClimb/Hang animation events.
    * *Moves:* `OnAnimatorFreeClimbStartInPosition()`, `OnAnimatorFreeClimbComplete()`, `OnAnimatorFreeClimbTurnComplete()`, `OnAnimatorHangStartInPosition()`, `OnAnimatorHangComplete()`.

* **`Assets/Scripts/Player/Bridges/Climbing/AgilityAnimationEventReceiver.cs`** (~100 lines)
    * *Role:* Receives Opsive agility animation events.
    * *Moves:* `OnAnimatorDodgeComplete()`, `OnAnimatorRollComplete()`, `OnAnimatorVaultComplete()`, `OnAnimatorCrawlComplete()`.

* **`Assets/Scripts/Player/Bridges/Climbing/SwimAnimationEventReceiver.cs`** (~100 lines)
    * *Role:* Receives Opsive swimming animation events.
    * *Moves:* `OnAnimatorSwimEnteredWater()`, `OnAnimatorSwimExitedWater()`, `OnAnimatorDiveAddForce()`, `OnAnimatorDiveComplete()`, `OnAnimatorClimbComplete()` (water), `OnAnimatorDrownComplete()`.

* **`Assets/Scripts/Player/Bridges/Climbing/AnimatorParameterCache.cs`** (~100 lines)
    * *Role:* Caches animator parameter hashes and validation.
    * *Moves:* `CacheHashes()`, all `h_*` hash fields, parameter validation logic.

---

## 5. WeaponAnimationEventRelay Refactoring (NEW)

**Current:** `Assets/Scripts/Weapons/Animation/WeaponAnimationEventRelay.cs` (1,172 lines)
**Target Structure:** `Assets/Scripts/Weapons/Animation/Events/`

### New Files & Responsibilities

* **`Assets/Scripts/Weapons/Animation/Events/WeaponAnimationEventRelay.cs`** (Cleaned, ~300 lines)
    * *Role:* Main dispatcher, receives animation events and routes to handlers.

* **`Assets/Scripts/Weapons/Animation/Events/FireEventHandler.cs`** (~200 lines)
    * *Role:* Handles fire/shoot animation events.
    * *Contains:* Muzzle flash, shell ejection, recoil triggers.

* **`Assets/Scripts/Weapons/Animation/Events/ReloadEventHandler.cs`** (~200 lines)
    * *Role:* Handles reload animation events.
    * *Contains:* Magazine drop, magazine insert, chamber round events.

* **`Assets/Scripts/Weapons/Animation/Events/MeleeEventHandler.cs`** (~200 lines)
    * *Role:* Handles melee attack animation events.
    * *Contains:* Swing impact, combo triggers, trail activation.

* **`Assets/Scripts/Weapons/Animation/Events/IKEventHandler.cs`** (~150 lines)
    * *Role:* Handles IK-related animation events.
    * *Contains:* Hand position updates, grip adjustments.

---

## 6. PlayerProximityCollisionSystem Refactoring (NEW)

**Current:** `Assets/Scripts/Player/Systems/PlayerProximityCollisionSystem.cs` (1,038 lines)
**Target Structure:** Uses existing `Assets/Scripts/Player/Jobs/` folder

### New Files & Responsibilities

* **`Assets/Scripts/Player/Jobs/ProximityCollisionJobs.cs`** (~400 lines)
    * *Role:* Burst jobs for collision detection and response.
    * *Moves:* Spatial hash jobs, narrowphase jobs, force calculation jobs.

* **`Assets/Scripts/Player/Systems/PlayerProximityCollisionSystem.cs`** (Cleaned, ~500 lines)
    * *Role:* System orchestration, job scheduling, event dispatch.

---

## 7. RagdollPresentationBridge Refactoring (NEW)

**Current:** `Assets/Scripts/Player/Animation/RagdollPresentationBridge.cs` (931 lines)
**Target Structure:** `Assets/Scripts/Player/Animation/Ragdoll/`

### Analysis
This file handles multiple concerns related to ragdoll presentation:
- Ragdoll enter/exit state management
- Physics bone setup and velocity clamping
- Settling detection for death finalization
- Remote sync for non-owned player ragdolls (EPIC 13.19)
- Push detection for networked impulse RPCs

### New Files & Responsibilities

* **`Assets/Scripts/Player/Animation/Ragdoll/RagdollPresentationBridge.cs`** (Cleaned, ~300 lines)
    * *Role:* Main entry point, state transitions, public API.
    * *Keeps:* `UpdateRagdollState()`, `EnterRagdoll()`, `ExitRagdoll()`, state flags.

* **`Assets/Scripts/Player/Animation/Ragdoll/RagdollPhysicsController.cs`** (~200 lines)
    * *Role:* Manages physics bones and colliders.
    * *Moves:* `SetRagdollEnabled()`, `ClampRagdollVelocities()`, `SetupBoneCollisionIgnore()`, `DisableAllCollidersOnTransform()`.
    * *Contains:* Rigidbody/Collider caching, velocity limits, collision ignore setup.

* **`Assets/Scripts/Player/Animation/Ragdoll/RagdollSettleDetector.cs`** (~150 lines)
    * *Role:* Detects when ragdoll has stopped moving.
    * *Moves:* `CheckIfSettled()`, `OnRagdollSettled()`, settling timer logic.
    * *Contains:* Velocity threshold checks, settle time tracking, settlement reporting.

* **`Assets/Scripts/Player/Animation/Ragdoll/RagdollRemoteSync.cs`** (~150 lines)
    * *Role:* Handles non-owned player ragdoll synchronization.
    * *Moves:* `SetRemoteSyncData()`, `ApplyRemoteSyncData()`, remote sync state.
    * *Contains:* Position/rotation/velocity sync from server, spring-based interpolation.

---

## Implementation Order

### Phase 1: Editor Tools (Low Risk)
1. TraversalObjectCreator partial class split
   - No runtime impact, purely editor code
   - Easy to test by creating test objects

### Phase 2: Animation Bridges (Medium Risk)
2. ClimbAnimatorBridge decomposition
   - Extract event receivers first (minimal logic change)
   - Test climbing, agility, and swimming animations

3. WeaponAnimationEventRelay decomposition
   - Extract event handlers
   - Test all weapon types (guns, melee, bow)

4. RagdollPresentationBridge decomposition
   - Extract physics controller first (isolated concern)
   - Extract settle detector and remote sync
   - Test death/ragdoll scenarios in single and multiplayer

### Phase 3: Core Systems (Higher Risk)
5. CharacterControllerSystem refactoring
   - Extract cache first (isolated concern)
   - Extract jobs second
   - Requires thorough movement testing

6. PlayerProximityCollisionSystem refactoring
   - Extract jobs to existing Jobs folder
   - Test multiplayer collision scenarios

### Phase 4: Weapon Visuals (Highest Risk)
7. WeaponEquipVisualBridge decomposition
   - Most complex, touching many systems
   - Implement strategy pattern incrementally
   - Test each weapon type thoroughly

---

## Notes

### Folder Structure After Refactoring

```
Assets/
├── Editor/
│   └── Traversal/
│       ├── TraversalObjectCreator.cs (main)
│       ├── TraversalObjectCreator.Climbing.cs
│       ├── TraversalObjectCreator.Movement.cs
│       ├── TraversalObjectCreator.Swimming.cs
│       ├── TraversalObjectCreator.Physics.cs
│       ├── TraversalObjectCreator.Hazards.cs
│       └── TraversalObjectCreator.Tests.cs
│
└── Scripts/
    ├── Items/
    │   └── Bridges/
    │       ├── Visuals/
    │       │   ├── WeaponVisualsDispatcher.cs
    │       │   ├── WeaponAnimationBridge.cs
    │       │   ├── WeaponVFXController.cs
    │       │   ├── WeaponSocketResolver.cs
    │       │   ├── WeaponReloadController.cs
    │       │   └── Strategies/
    │       │       ├── IWeaponVisualStrategy.cs
    │       │       ├── GunVisualStrategy.cs
    │       │       ├── MeleeVisualStrategy.cs
    │       │       └── BowVisualStrategy.cs
    │       └── (existing files remain)
    │
    ├── Player/
    │   ├── Animation/
    │   │   ├── Ragdoll/
    │   │   │   ├── RagdollPresentationBridge.cs
    │   │   │   ├── RagdollPhysicsController.cs
    │   │   │   ├── RagdollSettleDetector.cs
    │   │   │   └── RagdollRemoteSync.cs
    │   │   └── (existing files remain)
    │   │
    │   ├── Bridges/
    │   │   ├── Climbing/
    │   │   │   ├── ClimbAnimatorBridge.cs
    │   │   │   ├── ClimbAnimationEventReceiver.cs
    │   │   │   ├── AgilityAnimationEventReceiver.cs
    │   │   │   ├── SwimAnimationEventReceiver.cs
    │   │   │   └── AnimatorParameterCache.cs
    │   │   └── (existing files remain)
    │   │
    │   ├── Jobs/
    │   │   ├── (existing collision jobs)
    │   │   ├── CharacterControllerJobs.cs
    │   │   └── ProximityCollisionJobs.cs
    │   │
    │   └── Systems/
    │       ├── Cache/
    │       │   └── CapsuleColliderCache.cs
    │       └── (existing systems)
    │
    └── Weapons/
        └── Animation/
            └── Events/
                ├── WeaponAnimationEventRelay.cs
                ├── FireEventHandler.cs
                ├── ReloadEventHandler.cs
                ├── MeleeEventHandler.cs
                └── IKEventHandler.cs
```

### Existing Good Patterns to Follow
- `Assets/Scripts/Player/Systems/Abilities/` - Good example of system decomposition
- `Assets/Scripts/Player/Jobs/` - Already has collision jobs extracted
- `Assets/Editor/EquipmentWorkstation/Modules/` - Good example of modular editor tools

### Files NOT Needing Refactoring
These large files are well-structured for their purpose:
- `ChunkMeshingSystem.cs` (1,068 lines) - Voxel system, complex but cohesive
- `FreeClimbMovementSystem.cs` (501 lines) - Appropriate size, single responsibility
- `FreeClimbWallJumpSystem.cs` (405 lines) - Appropriate size, single responsibility
