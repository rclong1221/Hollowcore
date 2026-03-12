# EPIC 5.1: Immersive Audio System

**Priority**: LOW  
**Status**: **IMPLEMENTED** (See Assets/Scripts/Audio/)  
**Goal**: Create a dynamic, data-driven audio engine that reflects the player's physical state (vacuum, injury), enhances horror through occlusion, and integrates with NetCode/Physics.
**Dependencies**: Epic 1.13 (Audio Foundation), Epic 3.1 (Environment Zones), Epic 4.1 (Vitals), Epic 7 (Collision)

## Design Notes
1.  **Vacuum vs. Atmosphere (The "Muffle" Filter)**:
    *   **Mechanic**: When `CurrentEnvironmentZone` is Vacuum, apply a Low-Pass Filter (cutoff ~300-500Hz) to the "External" Audio Mixer Group.
    *   **Implementation**: `AudioEnvironmentSystem` Lerps `PressureFactor` and drives Mixer parameters.
2.  **Vital Feedback (Diegetic Audio)**:
    *   **Breathing**: Loop volume/pitch modulated by `Stamina` (inverse) and `Oxygen`.
    *   **Heartbeat**: OneShoots triggered when `Health < 30%`.
    *   **Pain Grunts**: Triggered on `DamageEvent`.
    *   **Implementation**: `VitalAudioSystem` updates `VitalAudioSource` component and drives Unity `AudioSource`s.
3.  **Impact Audio**:
    *   **Mechanic**: Physics collisions trigger sounds based on `SurfaceMaterial`.
    *   **Implementation**: `ImpactAudioSystem` consumes `CollisionEvent` buffer and calls `AudioManager.PlayImpact`.

## Implemented Components
- `AudioListenerState`: Tracks pressure/zone.
- `VitalAudioSource`: Tracks breath intensity.
- `ImpactAudioData`: Config for physics props.
- `AudioSourceReference`: Managed component holding Unity AudioSources.

## Implemented Systems
- `AudioEnvironmentSystem`: Client-side mixer control.
- `VitalAudioSystem`: Client-side vital audio feedback.
- `ImpactAudioSystem`: Client-side collision audio.

## File Locations
- `Assets/Scripts/Audio/Components/AudioComponents.cs`
- `Assets/Scripts/Audio/Systems/AudioEnvironmentSystem.cs`
- `Assets/Scripts/Audio/Systems/VitalAudioSystem.cs`
- `Assets/Scripts/Audio/Systems/ImpactAudioSystem.cs`
- `Assets/Scripts/Audio/Authoring/AudioListenerAuthoring.cs`
- `Assets/Scripts/Audio/Authoring/ImpactAudioAuthoring.cs`

## Final Integration Guide (How to Wire Up)

### 1. Unity Audio Mixer Setup
1.  Create an `AudioMixer` asset (e.g., `MasterMixer`).
2.  Create a Group "External" (child of Master). Add a **Low Pass Filter** effect.
3.  Right-click "Cutoff Freq" on the filter -> "Expose Parameter". Rename it to `VacuumCutoff`.
4.  (Optional) Expose Volume of External group as `VacuumVolume`.
5.  Find the `AudioManager` GameObject in your scene (or create one).
6.  Assign `MasterMixer` to the `MasterMixer` field.
7.  Verify `VacuumCutoffParam` string matches your exposed parameter name.

### 2. Player Prefab Setup
1.  Open the `GhostPlayer` (or main Player) prefab.
2.  Add the `AudioListenerAuthoring` component to the root.
3.  **Audio Sources**: The system will automatically create "BreathSource" and "HeartbeatSource" child objects at runtime.
    - **Pro Tip**: Create them manually in the Prefab if you want to assign specific `AudioClip`s (Looping Breath) and settings (Volume curves) in the Inspector. Name them exactly "BreathSource" and "HeartbeatSource".
    - Assign a Looping Breath Clip to "BreathSource".
    - Assign a Heartbeat Clip to "HeartbeatSource".

### 3. Surface Materials (Impacts)
1.  Locate your `SurfaceMaterial` assets (e.g., Concrete, Metal).
2.  Populate the new **Impact Clips** list with collision sounds (thuds/clanks).
3.  (Optional) Add `ImpactAudioAuthoring` to physics props (Crates) to override their material/mass.

### 4. Testing
- **Vacuum**: Enter an Airlock. Cycle it. Listen for the LowPass filter appearing.
- **Vitals**: Sprint until stamina is low. Listen for heavy breathing.
- **Impacts**: Run into a wall. Listen for thud.

## Acceptance Criteria
- [x] External sounds are heavily muffled in Vacuum zones.
- [x] Breathing audio ramps up brightness/speed as Stamina drains.
- [x] Large impacts play appropriate sounds.
- [x] Systems utilize `ScheduleParallel` or efficient Querys (Client Side).
