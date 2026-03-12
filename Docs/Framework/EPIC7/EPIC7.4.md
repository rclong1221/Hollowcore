### Epic 7.4: Advanced Collision Behaviors *(3/4 complete)*
**Priority**: MEDIUM  
**Goal**: Add gameplay-specific collision interactions beyond basic stagger

**Design Notes (Post-7.3 Revision)**:
- 7.3.5 already implements asymmetric stagger based on mass/velocity/stance
- 7.3.6 already implements directional bonuses (braced/side/back)
- 7.4 focuses on **new movement states** and **intentional actions** that go beyond automatic collision response
- Key distinction: Stagger is automatic (collision triggers it), Knockdown/Tackle are state escalations

**Sub-Epic 7.4.1: Knockdown State (Extreme Stagger Escalation)** ✅ **COMPLETE**
**Goal**: When power ratio exceeds threshold, loser enters Knockdown state instead of Stagger
**Design Notes**:
- Knockdown is a **longer, more severe** version of stagger
- Player falls to ground, must recover (1-2 seconds)
- Uses the `KnockdownPowerThreshold` (0.8) already in `PlayerCollisionSettings`
- Requires new `PlayerMovementState.Knockdown` enum value
- Different from tackle (knockdown is passive result, tackle is intentional)
- Uses enableable `Staggered` and `KnockedDown` tag components for parallel-safe state changes
- CharacterController prevents Unity Physics collision events, so we use position-based proximity detection

**Tasks**:
- [X] Added `Knockdown = 12` to `PlayerMovementState` enum in `PlayerStateComponent.cs`
- [X] Added knockdown fields to `PlayerCollisionState.cs`:
  - [X] `KnockdownTimeRemaining` - time in full knockdown phase
  - [X] `IsRecoveringFromKnockdown` - bool indicating recovery phase
  - [X] `KnockdownRecoveryTimeRemaining` - time in recovery phase  
  - [X] `KnockdownImpactSpeed` - for animation intensity
- [X] Added knockdown settings to `PlayerCollisionSettings.cs`:
  - [X] `KnockdownDuration` (0.8s default)
  - [X] `KnockdownRecoveryDuration` (0.5s default)
  - [X] `KnockdownRecoverySpeedMultiplier` (0.3 = 30% movement during recovery)
- [X] `PlayerCollisionResponseSystem`: triggers Knockdown when `powerRatio > KnockdownPowerThreshold`
- [X] `PlayerMovementSystem`: handles Knockdown state with two phases:
  - [X] Full knockdown: no input, knockback velocity with friction decay
  - [X] Recovery: limited movement (30% speed), countdown to exit
- [X] `PlayerStateSystem`: manages knockdown->recovery->normal state transitions
  - [X] Counts down `CollisionCooldown` each frame (allows repeat collisions)
  - [X] Counts down `StaggerTimeRemaining` and disables `Staggered` tag when expired
  - [X] Counts down `KnockdownTimeRemaining` and disables `KnockedDown` tag when expired
- [X] Created `KnockdownAnimatorBridge.cs` with TriggerStagger/EndStagger/TriggerKnockdown/StartRecovery/EndKnockdown
- [X] Created `LocalPlayerKnockdownAnimationSystem.cs` and `RemotePlayerKnockdownAnimationSystem.cs`
- [X] Created `PlayerProximityCollisionSystem.cs` - Position-based collision detection before CharacterController
- [X] Added `Staggered` and `KnockedDown` enableable tag components in `PlayerAuthoring.cs`

**Implementation Files**:
- `KnockdownAnimatorBridge.cs` - Animation bridge with intensity control for both stagger and knockdown
- `LocalPlayerKnockdownAnimationSystem.cs` - Triggers animations for local player
- `RemotePlayerKnockdownAnimationSystem.cs` - Triggers animations for remote players with NativeHashMap state tracking
- `PlayerProximityCollisionSystem.cs` - Position-based proximity detection (runs before CharacterController)
- `PlayerMovementSystem.cs` - Handles knockdown movement (no input, friction, recovery speed)
- `PlayerStateSystem.cs` - Manages knockdown/stagger timing, cooldown countdown, and state transitions
- `PlayerAuthoring.cs` - Added Staggered/KnockedDown enableable components (disabled by default)
- `CollisionDebugSystem.cs` - DEBUG: Logs collision detection status (remove after debugging)

**Animator Parameters** (add to Animator Controller):
- [ ] `StaggerTrigger` (Trigger) - starts stagger animation
- [ ] `IsStaggered` (Bool) - true during stagger
- [ ] `StaggerIntensity` (Float, 0-1) - animation intensity based on impact
- [ ] `KnockdownTrigger` (Trigger) - starts knockdown animation
- [ ] `RecoveryTrigger` (Trigger) - starts get-up animation
- [ ] `IsKnockedDown` (Bool) - true during entire knockdown
- [ ] `IsRecovering` (Bool) - true during recovery phase only
- [ ] `KnockdownIntensity` (Float, 0-1) - animation intensity based on impact

**Testing Checklist**:
- [ ] Collisions repeatedly trigger (not just once)
- [ ] Stagger ends and player returns to normal after timer expires
- [ ] Knockdown transitions to recovery, then to normal
- [ ] Debug log shows cooldown and stagger timers counting down
- [ ] Remove `CollisionDebugSystem.cs` after testing complete

**Sub-Epic 7.4.2: Tackle System (Intentional Knockdown)** ✅ COMPLETE
**Goal**: Allow players to intentionally tackle others with a dedicated input
**Design Notes**:
- Tackle is an **intentional action** triggered by input (e.g., Sprint + Melee)
- Tackler commits to animation, both players affected
- Tackler gets brief stagger/recovery, target gets knockdown
- High risk/reward: miss = you're vulnerable
- Requires stamina cost and cooldown

**Tasks**:
- [X] Added `Tackling = 13` to `PlayerMovementState` enum in `PlayerStateComponent.cs`
- [X] Added `Tackle` input event to `PlayerInput` in `PlayerInput_Global.cs`
- [X] Created `TackleState.cs` component with:
  - [X] `TackleTimeRemaining` - active tackle duration
  - [X] `TackleDirection` - committed direction (can't change mid-tackle)
  - [X] `TackleCooldown` - cooldown remaining
  - [X] `DidHitTarget` - for animation branching (hit vs whiff)
  - [X] `TackleSpeed` - speed at initiation for impact calculation
  - [X] `HasProcessedHit` - prevents multiple hit processing
- [X] Created `TackleSettings.cs` singleton with all settings:
  - [X] `TackleMinSpeed` (5.0 m/s), `TackleDuration` (0.5s), `TackleSpeedMultiplier` (1.3)
  - [X] `TackleStaminaCost` (35), `TackleCooldownDuration` (3.0s)
  - [X] `TackleKnockdownDuration` (1.5s target), `TacklerHitRecoveryDuration` (0.3s)
  - [X] `TacklerMissRecoveryDuration` (0.6s - punishment for whiffing)
  - [X] `TackleHitRadius` (0.6m), `TackleHitDistance` (1.5m), `TackleHitAngle` (45°)
- [X] Created `TackleSystem.cs`:
  - [X] Checks input + sprint state + stamina + cooldown
  - [X] Commits to forward direction, applies speed boost
  - [X] Handles tackle timeout and state transitions
- [X] Created `TackleCollisionSystem.cs`:
  - [X] Cone-based hit detection (angle check in front of tackler)
  - [X] On hit: target gets Knockdown, tackler gets brief stagger
  - [X] On miss: tackler gets longer stagger (vulnerable)
- [X] Created animation systems:
  - [X] `TackleAnimatorBridge.cs` with TriggerTackle/TackleHit/TackleMiss
  - [X] `LocalPlayerTackleAnimationSystem.cs`
  - [X] `RemotePlayerTackleAnimationSystem.cs`
- [X] Updated `PlayerStateSystem` to not override Tackling/Staggered states
- [X] Updated `CollisionPowerUtility.GetMovementMultiplier` for Tackling (2.0 - high power)
- [X] Updated `PlayerProximityCollisionSystem.CalculatePower` for Tackling (2.0 - high power)
- [X] Added `TackleState` component to `PlayerAuthoring.cs`
- [X] Created `TackleSettingsInitSystem.cs` to bootstrap singleton

**Animator Parameters** (add to Animator Controller):
- [ ] `TackleTrigger` (Trigger) - starts tackle lunge
- [ ] `TackleHitTrigger` (Trigger) - successful hit reaction
- [ ] `TackleMissTrigger` (Trigger) - whiff/stumble
- [ ] `IsTackling` (Bool) - true during tackle
- [ ] `TackleSpeed` (Float, 0-1) - animation speed/intensity

**Sub-Epic 7.4.3: Dodge/Evasion Collision Immunity** ✅ COMPLETE
**Goal**: Players mid-dodge can slip past collisions with reduced effect
**Design Notes**:
- Dodge roll/dive already exists as movement states (Rolling/Diving in `PlayerMovementState`)
- This adds **collision immunity/reduction** during dodge i-frames
- Glancing blows deflect instead of full impact
- Uses existing `PlayerProximityCollisionSystem` for collision detection
- Follows enableable component pattern established in 7.4.1
- Uses per-dodge `InvulnStart`/`InvulnEnd` from `DodgeRollState`/`DodgeDiveState` (not global settings)

**Tasks**:
- [X] Add dodge immunity settings to `PlayerCollisionSettings.cs`:
  - [X] `DodgeCollisionMultiplier` (float, default 0.3 = 70% reduction)
  - [X] `DodgeDeflectionAngle` (float, default 30° tangent deflection)
  - [X] `DodgeIFrameStart` (float, default 0.1s after dodge starts)
  - [X] `DodgeIFrameDuration` (float, default 0.4s of immunity)
- [X] Add `Evading` enableable IComponentData to `StaggeredTag.cs` and `PlayerAuthoring.cs`
- [X] Update `PlayerProximityCollisionSystem.cs`:
  - [X] Added `DodgeRollState` and `DodgeDiveState` component lookups
  - [X] Added `DodgeInfo` struct to track dodge state per player
  - [X] Added `GetDodgeInfo()` helper to check if in i-frame window
  - [X] If dodging: multiply power by `DodgeCollisionMultiplier` (reduced damage)
  - [X] If dodging: rotate push direction by `DodgeDeflectionAngle` via `DeflectDirection()`
  - [X] If within full i-frames: skip stagger/knockdown entirely, enable `Evading` tag
  - [X] Extended `ApplyCollision()` with `hasIFrameImmunity` and `isDodging` parameters
- [X] Update `PlayerStateSystem.cs`:
  - [X] Disable `Evading` component when exiting Rolling/Diving state
  - [X] Added to both server and client processing loops
- [ ] Test: rolling through another player feels smooth (no sudden stops)
- [ ] Test: timing dodge correctly avoids stagger (i-frame window works)
- [ ] Test: late/early dodge still takes reduced damage (multiplier works)

**Implementation Files**:
- `PlayerCollisionSettings.cs` - Added dodge immunity settings (DodgeCollisionMultiplier, DodgeDeflectionAngle, DodgeIFrameStart, DodgeIFrameDuration)
- `StaggeredTag.cs` - Added `Evading` enableable IComponentData
- `PlayerAuthoring.cs` - Added `Evading` component to player entity (disabled by default)
- `PlayerProximityCollisionSystem.cs` - Added DodgeInfo struct, GetDodgeInfo(), DeflectDirection(), i-frame immunity logic
- `PlayerStateSystem.cs` - Added Evading tag management when exiting dodge states

**Sub-Epic 7.4.4: Collision Audio & VFX**
**Goal**: Audio/visual feedback for collisions using the event buffer
**Design Notes**:
- Consumes `CollisionEvent` buffer populated by `PlayerProximityCollisionSystem`
- Uses `HitDirection` and `ImpactForce` for differentiated feedback
- Runs in `PresentationSystemGroup` (client-only, after simulation)
- Follows bridge pattern from `KnockdownAnimatorBridge` for GameObject access

**Tasks**:
- [X] Add collision audio settings to `PlayerCollisionSettings.cs`:
  - [X] `CollisionAudioMinForce` (float, default 0.2 = minimum force for sound)
  - [X] `CollisionAudioMaxForce` (float, default 1.0 = max volume force)
  - [X] `MaxCollisionSoundsPerFrame` (int, default 3 = prevent audio spam)
  - [X] `CameraShakeForceThreshold` (float, default 0.6 = heavy impact)
  - [X] `CameraShakeIntensity` (float, default 0.3)
  - [X] `CameraShakeDuration` (float, default 0.2s)
- [X] Create `CollisionAudioBridge.cs` (MonoBehaviour on player prefab):
  - [X] `AudioSource CollisionAudioSource` - 3D audio source
  - [X] `AudioClip[] BumpSounds` - light collision sounds
  - [X] `AudioClip[] ImpactSounds` - heavy collision sounds
  - [X] `AudioClip[] GruntSounds` - player pain sounds (front/braced)
  - [X] `AudioClip[] SurprisedSounds` - back hit sounds
  - [X] `AudioClip[] EvadeSounds` - dodge deflection whoosh
  - [X] `PlayCollisionSound(float intensity, int hitDirection)` - selects and plays appropriate sound
  - [ ] Wire real collision clips on the player presentation prefab (beep fallback exists)
- [X] Create `CollisionAudioSystem.cs` in `PresentationSystemGroup`:
  - [X] Query entities with `CollisionEvent` buffer and `GhostPresentationGameObjectForEntity`
  - [X] Get `CollisionAudioBridge` via `GhostPresentationGameObjectSystem`
  - [X] Map `ImpactForce` to intensity: `Mathf.InverseLerp(MinForce, MaxForce, force)`
  - [X] Map `HitDirection` to sound category (0=front, 1=side, 2=back, 3=evaded)
  - [X] Call `bridge.PlayCollisionSound(intensity, hitDirection)`
  - [X] Track sounds played this frame, limit to `MaxCollisionSoundsPerFrame`
- [X] Populate directional `HitDirection` (front/side/back) for proximity-based collisions (uses BracedDotThreshold/BackHitDotThreshold)
- [X] Coalesce multiple collision events per entity per frame (audio/VFX pick strongest hit)
- [X] Cache `CollisionAudioBridge` lookup per entity (Dictionary<Entity, Bridge>)
- [X] Cache generated fallback beep `AudioClip`s in `CollisionAudioBridge` (_fallbackBeeps array)
- [X] Create `CollisionVFXBridge.cs` (MonoBehaviour on player prefab):
  - [X] `ParticleSystem DustImpact` - ground collision particles
  - [X] `ParticleSystem SparkImpact` - EVA suit collision particles
  - [X] `void PlayImpactVFX(Vector3 contactPoint, float intensity, bool isEVA)`
  - [ ] Wire particle prefabs on the player presentation prefab
- [X] Create `CollisionVFXSystem.cs` in `PresentationSystemGroup`:
  - [X] Query entities with `CollisionEvent` buffer and `GhostPresentationGameObjectForEntity`
  - [X] Get `CollisionVFXBridge` via `GhostPresentationGameObjectSystem`
  - [X] Spawn particles at `CollisionEvent.ContactPoint`
  - [X] Scale emission count by `ImpactForce`
  - [X] Choose particle type based on environment (EVA = sparks, normal = dust)
- [X] Use `Audio.Systems.VFXManager` pooling for collision VFX (SpawnVFX API with fallback)
- [X] Cache `CollisionVFXBridge` lookup per entity (Dictionary<Entity, Bridge>)
- [X] Create `LocalPlayerCollisionCameraShakeSystem.cs` in `PresentationSystemGroup`:
  - [X] Query local player entity with `GhostOwnerIsLocal`
  - [X] On collision with force > `CameraShakeForceThreshold`
  - [X] Apply camera shake via existing `CameraShake` + `CameraManager` pipeline
  - [X] Scale intensity by normalized force
- [X] Uses `CollisionEventClearSystem.cs` (runs last in presentation) to prevent duplicate audio/VFX on the same collision
- [X] Add controller vibration for local player collisions (Input System haptics)
- [ ] Test: audio matches visual impact intensity
- [ ] Test: back hits sound surprised, front hits sound grunts
- [ ] Test: evaded collisions play whoosh sound
- [ ] Test: camera shake only on heavy impacts
- [ ] Test: no audio spam with multiple simultaneous collisions

**Implementation Files**:
- `Assets/Scripts/Player/Components/PlayerCollisionSettings.cs` (added 7.4.4 tuning fields)
- `Assets/Scripts/Player/Bridges/CollisionAudioBridge.cs`
- `Assets/Scripts/Player/Systems/CollisionAudioSystem.cs`
- `Assets/Scripts/Player/Bridges/CollisionVFXBridge.cs`
- `Assets/Scripts/Player/Systems/CollisionVFXSystem.cs`
- `Assets/Scripts/Player/Systems/LocalPlayerCollisionCameraShakeSystem.cs`
- `Assets/Scripts/Player/Systems/LocalPlayerCollisionHapticsSystem.cs`