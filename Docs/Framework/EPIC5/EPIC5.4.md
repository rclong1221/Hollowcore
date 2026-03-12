# EPIC 5.4: Procedural Horror Events

**Priority**: LOW  
**Status**: **IMPLEMENTED**  
**Goal**: Generate dynamic, unpredictable horror events (hallucinations, system failures) to keep tension high.
**Dependencies**: Epic 5.1 (Audio), Epic 5.2 (Visuals), Epic 5.3 (Stress)

## Design Notes
1.  **Stress-Triggered Events**:
    *   As `PlayerStressState` (Epic 5.3) increases, probability of events increases.
    *   **Low Stress**: Occasional flicker.
    *   **High Stress**: Auditory hallucinations, fake radar blips.
2.  **Event Types**:
    *   **Light Flicker**: Global or local lights dim/off for split second.
    *   **Phantom Audio**: Footsteps behind player (Stereo panning).
    *   **Radar Ghost**: Dot appears on Motion Tracker then vanishes.
    *   **Vent Burst**: Steam particle burst + Hiss.
    *   **Whispers**: Creepy whispers from random directions.
    *   **Visual Distortion**: Post-processing distortion effect.

---

## Implemented Components

### HorrorComponents.cs
Location: `Assets/Scripts/Horror/Components/HorrorComponents.cs`

| Component | Type | Description |
|-----------|------|-------------|
| `HorrorDirector` | Singleton | Global tension tracking, event timing |
| `HorrorEventRequest` | Entity | Request for an effect to play |
| `PlayerHallucinationState` | Per-Player | Tracks hallucination state/cooldown |
| `HorrorSettings` | Singleton | Configuration values |
| `HorrorEventType` | Enum | LightFlicker, PhantomFootsteps, Whispers, RadarGhost, VentBurst, VisualDistortion |

### HorrorDirector Fields
| Field | Type | Description |
|-------|------|-------------|
| `GlobalTension` | float | 0-1, ramps up over mission time |
| `TimeSinceLastEvent` | float | Cooldown timer |
| `MissionTime` | float | Elapsed mission time |
| `TensionBuildRate` | float | How fast tension builds |
| `MinEventCooldown` | float | Min seconds between events |
| `MaxEventCooldown` | float | Max seconds between events |

### HorrorEventRequest Fields
| Field | Type | Description |
|-------|------|-------------|
| `EventType` | enum | Type of horror effect |
| `Intensity` | float | 0-1 effect strength |
| `Duration` | float | Seconds (0 = instant) |
| `Position` | float3 | For spatial events |
| `TargetPlayer` | Entity | For private events |
| `IsPrivate` | bool | True = only target sees it |

---

## Implemented Systems

### HorrorDirectorSystem (Server)
Location: `Assets/Scripts/Horror/Systems/HorrorDirectorSystem.cs`

- Runs on **Server** only
- Tracks mission time and builds global tension
- Monitors average player stress
- Triggers **global events** (visible to all players):
  - Light flickers
  - Vent bursts
- Event frequency scales with tension + stress
- Creates `HorrorEventRequest` entities that replicate to clients

### HallucinationSystem (Client)
Location: `Assets/Scripts/Horror/Systems/HallucinationSystem.cs`

- Runs on **Client** only for local player
- Triggers **private hallucinations** (only target player sees/hears):
  - Phantom footsteps (stereo panned behind player)
  - Whispers (random pan, looping)
  - Visual distortion (post-processing)
- Stress threshold: Default 60% to start
- Cooldown between hallucinations (default 15s)
- Creates `HorrorEventRequest` entities locally

### HorrorEventPresentationSystem (Client)
Location: `Assets/Scripts/Horror/Systems/HorrorEventPresentationSystem.cs`

- Runs in `PresentationSystemGroup`
- Processes `HorrorEventRequest` entities
- Calls `HorrorEventManager` to play actual effects
- Destroys request entities after processing

---

## Implemented MonoBehaviours

### HorrorEventManager
Location: `Assets/Scripts/Horror/HorrorEventManager.cs`

Singleton MonoBehaviour that plays the actual effects:

| Effect | Implementation |
|--------|----------------|
| Light Flicker | Coroutine that dims/restores scene lights |
| Phantom Footsteps | Random footstep clip, stereo panned |
| Whispers | Looping whisper clips with random pan |
| Vent Burst | 3D positioned audio + particle effect |
| Visual Distortion | Post-processing volume weight animation |
| Radar Ghost | Placeholder (needs Motion Tracker integration) |

**Inspector Fields:**
- `PhantomFootstepClips` - List of footstep AudioClips
- `WhisperClips` - List of whisper AudioClips
- `VentBurstClip` - Steam hiss AudioClip
- `FlickerableLights` - Lights that can flicker
- `DistortionVolume` - Post-process volume for distortion
- `VentBurstVFX` - Particle system for steam

---

## Authoring Components

### HorrorDirectorAuthoring
Location: `Assets/Scripts/Horror/Authoring/HorrorDirectorAuthoring.cs`

Add to a single GameObject in your subscene to configure the horror system.

**Inspector Fields:**
- Tension Build Rate (default: 0.005/s = ~3.3 min to max)
- Min/Max Event Cooldown
- Hallucination Threshold (default: 60% stress)
- Hallucination Cooldown
- Flicker Duration Range

### HallucinationReceiverAuthoring
Location: `Assets/Scripts/Horror/Authoring/HallucinationReceiverAuthoring.cs`

Add to player prefab to enable hallucinations.

---

## Integration Guide

### 1. Scene Setup

1. Create empty GameObject named **HorrorEventManager** in scene
2. Add `HorrorEventManager` component
3. Assign audio clips:
   - Phantom footstep clips (scary footstep sounds)
   - Whisper clips (creepy whispers)
   - Vent burst clip (steam hiss)
4. Optionally assign:
   - Distortion Volume (post-processing)
   - Vent Burst VFX (particle system)
5. `AutoFindLights = true` will auto-populate flickerable lights

### 2. Horror Director (Subscene)

1. Create empty GameObject in **subscene**
2. Add `HorrorDirectorAuthoring` component
3. Configure timing parameters as desired
4. Bake the subscene

### 3. Player Setup

1. Open player prefab (Warrok_Server)
2. Add `HallucinationReceiverAuthoring` component
3. Ensure `StressAuthoring` is also present

---

## Event Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                         SERVER                                   │
├─────────────────────────────────────────────────────────────────┤
│  HorrorDirectorSystem                                            │
│    ├─ Updates GlobalTension (slow ramp)                         │
│    ├─ Reads average player stress                               │
│    ├─ Rolls for global events (flicker, vent)                   │
│    └─ Creates HorrorEventRequest (replicated)                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼ (Ghost Replication)
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENT                                   │
├─────────────────────────────────────────────────────────────────┤
│  HallucinationSystem                                             │
│    ├─ Reads local player stress                                 │
│    ├─ If stress > threshold, rolls for hallucination            │
│    └─ Creates HorrorEventRequest (local only)                   │
│                                                                  │
│  HorrorEventPresentationSystem                                   │
│    ├─ Queries all HorrorEventRequest entities                   │
│    ├─ Calls HorrorEventManager.ProcessEvent()                   │
│    └─ Destroys request entities                                 │
│                                                                  │
│  HorrorEventManager (MonoBehaviour)                              │
│    └─ Plays audio, flickers lights, shows VFX                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Testing

1. **Global Events (Server-triggered)**:
   - Wait for mission time to build tension (~2-3 min)
   - Observe light flickers affecting all players

2. **Hallucinations (Client-triggered)**:
   - Enter a dark zone with flashlight OFF
   - Wait for stress to build above 60%
   - Hear whispers or phantom footsteps
   - See visual distortion (if post-process configured)

4. **Test Object**: Use menu `GameObject > DIG - Test Objects > Traversal > Horror Corridor`.
   - Dark environment designed to trigger Stress quickly.
   - Wait for stress to build and observe Hallucinations.

3. **Verification Logs**:
   - `[Horror] Global Event: LightFlicker, Intensity=X`
   - `[Horror] Hallucination: Whispers, Intensity=X`
   - `[Horror] Phantom footsteps (pan=X)`

---

## Acceptance Criteria

- [x] Lights flicker randomly when tension is high
- [x] High stress players hear non-diegetic whispers
- [x] Events are randomized (time/type)
- [x] HorrorDirector singleton created
- [x] LightFlicker event implemented
- [x] PhantomAudio event implemented
- [x] Visual distortion effect implemented
- [ ] Radar ghost integration (requires Motion Tracker system)

---

## Files Created

| File | Description |
|------|-------------|
| `Horror/Components/HorrorComponents.cs` | ECS components and enums |
| `Horror/Systems/HorrorDirectorSystem.cs` | Server-side global event system |
| `Horror/Systems/HallucinationSystem.cs` | Client-side hallucination system |
| `Horror/Systems/HorrorEventPresentationSystem.cs` | Effect playback bridge |
| `Horror/HorrorEventManager.cs` | MonoBehaviour effect handler |
| `Horror/Authoring/HorrorDirectorAuthoring.cs` | Director configuration |
| `Horror/Authoring/HallucinationReceiverAuthoring.cs` | Player hallucination component |
