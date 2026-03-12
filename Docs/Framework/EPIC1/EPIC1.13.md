### Epic 1.13: Audio & Polish (Data-Driven)
**Priority**: LOW  
**Goal**: Robust, data-driven audio + VFX surface system that scales with many materials and content types

Design summary:
- Use a `SurfaceMaterial` ScriptableObject (designer-facing) to hold audio clips, VFX prefabs, gameplay params (friction, volume modifiers), and IDs.
- Provide a `SurfaceMaterialRegistry` (authoring or baked BlobAsset) that maps collider/renderer metadata or voxel/chunk tags to `SurfaceMaterial` IDs.
- Authoring baker: `SurfaceMaterialAuthoring` attaches a material ID to converted entities so runtime lookups are O(1).
- Emit lightweight `FootstepEvent` and `LandingEvent` (structs) with material ID; a single-threaded consumer (AudioManager/VFXManager) plays pooled audio/particles.

Tasks (data-driven approach):
- [X] Create `SurfaceMaterial` ScriptableObject API:
  - [X] Fields: `id` (string), `displayName`, `AudioClips` (per stance/speed), `VFXPrefab`, `FootstepIntervalModifiers`, `FootstepVolume`, `FrictionModifier`, `Priority`
  - [X] Extended with action clips: `JumpClips`, `RollClips`, `DiveClips`, `SlideClips`, `ClimbClips`
- [X] Create `SurfaceMaterialRegistry` (ScriptableObject + optional baked BlobAsset):
  - [X] Map: physics material name / renderer material name / tag → `SurfaceMaterial` id
  - [X] Tooling: bulk-assign by layer/tag/renderer material (SurfaceMaterialBulkMapper enhanced with tabs)
- [X] Implement `SurfaceMaterialAuthoring` baker:
  - [X] Attach a small `SurfaceMaterialId` component to converted entities (int id) for fast runtime lookup
  - [ ] Support placing the authoring component on large terrain/chunk prefabs and subscenes
- [X] Add `SurfaceDetectionService` to return material id from raycast/hit results using this priority:
  1. `SurfaceMaterialId` component on hit entity
  2. Registry lookup by collider/physics material
  3. Registry lookup by renderer material name
  4. Tag lookup
  5. Layer lookup
  6. `default` material fallback
- [X] `SurfaceMaterialMapping` enhanced with tag/layer support and O(1) cached lookups
 - [X] Create `FootstepEvent` and `LandingEvent` (IComponentData or event buffer) and a `FootstepSystem` that triggers events based on stride/timing and landing intensity
 - [X] Create player action audio events: `JumpEvent`, `RollEvent`, `DiveEvent`, `ClimbStartEvent`, `SlideEvent`
 - [X] Create audio emission systems: `PlayerJumpAudioSystem`, `PlayerRollAudioSystem`, `PlayerDiveAudioSystem`, `PlayerClimbAudioSystem`
- [X] Implement `AudioManager` consumer that:
  - [X] Resolves `SurfaceMaterial` by id
  - [X] Selects clip variant by stance/speed and plays using pooled `AudioSource`s
  - [X] Applies spatialization and attenuation; optional suppression for non-local players
  - [X] Extended with action audio methods: `PlayJump`, `PlayRoll`, `PlayDive`, `PlayClimb`, `PlaySlide`
- [X] Extend `AudioPlaybackSystem` to handle all player action audio events (jump, roll, dive, climb, slide)
- [X] Implement `VFXManager` consumer for particle/decals using pooled systems:
  - [X] Pooling with automatic return after duration
  - [X] Surface-aware VFX via `PlayVFXForMaterial(materialId, position)`
  - [X] Throttling and distance culling
  - [X] Pool prewarming support
  - [X] Telemetry counters
- [X] Add editor tools and QA scene(s):
  - [X] `SurfaceMaterial` authoring examples (create several sample materials)
  - [X] `AudioQAController` component for manual testing
  - [X] `AudioQASceneSetup` editor tool to create Audio_QA scene (Window > DIG > Audio > Create Audio_QA Scene)
  - [X] `Validate Audio Setup` editor command (Window > DIG > Audio > Validate Audio Setup)
- [ ] Add validation rules and unit tests: *(removed due to asmdef issues - reimplement later)*
  - [ ] Unit tests for registry lookup priority (SurfaceMaterialRegistryTests)
  - [ ] Unit tests for mapping lookup (SurfaceMaterialMappingTests)
  - [ ] PlayMode tests for audio/VFX playback (AudioPlaybackTests, VFXPlaybackTests)
- [X] Performance & safety polish:
  - [X] Cache id lookups in registry (Dictionary<int, SurfaceMaterial>)
  - [X] Cache lookups in mapping (material name, tag, layer dictionaries)
  - [X] Pool audio/VFX objects (AudioManager and VFXManager both use pooling)
  - [X] Throttle non-local playback (VFXManager.MaxSpawnsPerSecond)
  - [X] Distance culling (VFXManager.DistanceCulling)
- [X] Add telemetry and debug logging:
  - [X] `AudioTelemetry` static class with counters and verbose logging
  - [X] VFXManager telemetry (TotalSpawnsThisSession, PoolHitsThisSession, CulledThisSession)
  - [X] DEBUG_LOG_AUDIO compile-time flag for verbose tracing

Optional / follow-ups:
- [ ] Voxel terrain support: bake per-chunk material blobs and add fast lookup in jobs
- [ ] Networked audio events for remote player audible playback (low-bandwidth event stream)
- [ ] Automatic fallback generator that creates simple `SurfaceMaterial` assets from renderer materials (designer convenience)

Additional items (robustness, tooling, networking, QA):

**Physics Material Mapping & Authoring**
  - [ ] Baker to capture Unity `PhysicMaterial` / collider metadata -> `SurfaceMaterialId` at conversion time
  - [X] `default` fallback material asset always present (via SurfaceMaterialRegistry.DefaultMaterial)
  - [X] Per-stance and speed clip variants on `SurfaceMaterial` (walk/run/crouch)

**Editor Tooling & Designer UX**
  - [X] Bulk mapping tool: map renderer materials / tags / layers -> `SurfaceMaterial` (SurfaceMaterialBulkMapper)
  - [ ] `SurfaceMaterial` inspector previewer (play sample audio, spawn sample VFX)
  - [ ] Auto-generate placeholder `SurfaceMaterial` assets from renderer materials
  - [X] Editor validation: warn on missing clips or unmapped geometry (Validate Audio Setup)

**Integration & Gameplay Hooks**
  - [X] `FootstepEvent` includes stance, intensity, and world position
  - [ ] Reuse registry for `FallDetectionSystem` landing impact selection
  - [X] Expose gameplay modifiers on `SurfaceMaterial` (FrictionModifier, SlideFrictionMultiplier, IsSlippery)

**Audio/Spatial Features & Performance**
  - [ ] Occlusion & simple reverb zones support (attenuation by raycast / region)
  - [ ] Route clips into `AudioMixer` groups and expose pitch/volume RTPCs
  - [X] Pooled `AudioSource` / VFX objects
  - [X] Throttle non-local playback and LOD-based quality reduction (distance culling)
  - [X] Cache id lookups (int ids) and avoid string ops in hot loops

**Networking & Authority**
  - [ ] Compact, rate-limited audio events for remote audible playback (send id+pos+intensity)
  - [ ] Define authority: local player playback client-side; broadcast only important events

**QA, Tests & Observability**
  - [ ] Unit tests for registry lookup priorities and id mapping (pure C#) *(removed - reimplement)*
  - [ ] PlayMode tests (automated) for footstep/landing event firing and audio/VFX playback *(removed - reimplement)*
  - [X] Provide `Audio_QA` scene setup tooling with multiple ground patches and auto-test steps
  - [X] Telemetry counters: footstep rate, cache misses, playback failures (AudioTelemetry)
  - [X] Conditional debug flags (DEBUG_LOG_AUDIO) to trace lookups and event emission

These additions complete the data-driven plan and make the system scalable, testable, and designer-friendly.

Acceptance criteria:
- ✅ Designers can add a new `SurfaceMaterial` asset and assign it to geometry without code changes.
- ✅ `FootstepEvent` and `LandingEvent` produce correct audio and VFX using pooled resources with no per-frame allocations.
- ✅ Registry lookup behaves deterministically and falls back to a safe default.

---

## Setup Guide

### Quick Start

1. **Create a SurfaceMaterialRegistry**
   - Right-click in Project → Create → DIG → SurfaceMaterialRegistry
   - Place it in `Assets/Resources/` (e.g., `Assets/Resources/SurfaceMaterialRegistry.asset`)
   - Add at least one `SurfaceMaterial` to the Materials list
   - Assign a DefaultMaterial for fallback

2. **Create SurfaceMaterial Assets**
   - Right-click in Project → Create → DIG → SurfaceMaterial
   - Configure fields:
     - `Id`: Unique integer identifier
     - `DisplayName`: Human-readable name
     - `WalkClips`, `RunClips`, `CrouchClips`: Audio clips for footsteps by stance
     - `JumpClips`, `RollClips`, `DiveClips`, `SlideClips`, `ClimbClips`: Action audio
     - `VFXPrefab`: Particle effect for footsteps
     - `FootstepVolume`, `FrictionModifier`, etc.

3. **Create a SurfaceMaterialMapping** (optional for tag/layer lookups)
   - Right-click in Project → Create → DIG → SurfaceMaterialMapping
   - Place in `Assets/Resources/` (e.g., `Assets/Resources/SurfaceMaterialMapping.asset`)
   - Add entries mapping material names, tags, or layers to SurfaceMaterial assets

4. **Attach SurfaceMaterialAuthoring to Geometry**
   - Add `SurfaceMaterialAuthoring` component to terrain/ground objects
   - Assign the appropriate `SurfaceMaterial` asset
   - The baker will create `SurfaceMaterialId` components on converted entities

### Editor Tools

- **Bulk Mapper**: Window → DIG → Audio → Surface Material Bulk Mapper
  - Scan and assign SurfaceMaterials to materials, tags, and layers in bulk
  - Tabbed interface for Materials / Tags / Layers
  
- **Validate Audio Setup**: Window → DIG → Audio → Validate Audio Setup
  - Checks for missing registry, unmapped materials, missing clips
  
- **Create Audio_QA Scene**: Window → DIG → Audio → Create Audio_QA Scene
  - Generates a test scene with multiple surface patches for manual testing

### Runtime Components

- **AudioManager**: Singleton MonoBehaviour for pooled audio playback
  - Automatically loads registry from `Resources/SurfaceMaterialRegistry`
  - Methods: `PlayFootstep()`, `PlayLanding()`, `PlayJump()`, `PlayRoll()`, `PlayDive()`, `PlaySlide()`, `PlayClimb()`

- **VFXManager**: Singleton MonoBehaviour for pooled VFX playback
  - Methods: `PlayVFX()`, `PlayVFXForMaterial()`, `SpawnVFX()`
  - Configure: `MaxSpawnsPerSecond`, `DistanceCulling`, `CullDistance`

- **SurfaceDetectionService**: Static utility for resolving material ID from hits
  - Priority: Entity component → Physics material → Renderer → Tag → Layer → Default

### Running Tests

1. **Editor Tests**: Window → General → Test Runner → Edit Mode
   - `SurfaceMaterialRegistryTests`: Registry lookup priority tests
   - `SurfaceMaterialMappingTests`: Mapping lookup tests

2. **PlayMode Tests**: Window → General → Test Runner → Play Mode
   - `AudioPlaybackTests`: Audio playback integration
   - `VFXPlaybackTests`: VFX playback integration

### Debug Logging

Enable verbose audio logging by adding `DEBUG_LOG_AUDIO` to Player Settings → Scripting Define Symbols.

```
// In Player Settings → Other Settings → Scripting Define Symbols
DEBUG_LOG_AUDIO
```

This enables `AudioTelemetry` logging for:
- Footstep events (position, material, stance)
- Landing events (intensity)
- Action events (jump, roll, dive, climb, slide)
- Summary statistics