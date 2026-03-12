# EPIC 15.27: Dynamic Audio Ecosystem

**Status:** Implemented
**Priority:** Medium-High (Immersion & Polish)
**Dependencies:**
- `AudioManager` (Existing — EPIC 5.1, enhanced EPIC 15.24)
- `SurfaceMaterialRegistry` + `SurfaceDetectionService` (Existing — EPIC 15.24)
- `GameplayFeedbackManager` (Existing — EPIC 15.9)
- `AudioEnvironmentSystem` (Existing — EPIC 5.1, vacuum filtering)
- `Audio Workstation` (Existing — 6 modules)
- `ProceduralSoundBridgeSystem` (Existing — EPIC 15.25 weapon foley)
- `AudioMixer` (Existing — FEEL MMSoundManagerAudioMixer)

---

## Overview

### What Exists (Foundation)

The audio infrastructure is **functional but not spatial**. The following systems are implemented:

| System | Status | What It Does |
|--------|--------|-------------|
| `AudioManager` | Working | 8-source pool, surface-aware clips, pitch/volume variance, no-repeat, fallback beep QA mode |
| `SurfaceMaterialRegistry` | Working | O(1) material lookup by ID, 24+ surface types, per-surface clip arrays (walk/run/crouch/land/impact/jump/roll/dive/slide/climb) |
| `SurfaceDetectionService` | Working | Priority chain: ECS component → physics material → renderer → tag → layer → default |
| `GameplayFeedbackManager` | Working | 20+ event types bridging ECS → FEEL (MMF_Player). No AudioMixer routing — plays directly via MMF_Sound |
| `AudioEnvironmentSystem` | Working | Vacuum low-pass filtering (400Hz ↔ 22kHz). No reverb, no multi-zone |
| `VitalAudioSystem` | Working | Breathing + heartbeat from stamina/health state |
| `ImpactAudioSystem` | Working | Physics collision → sound (force threshold, material ID, mass factor) |
| `Networked Audio` | Working | Footstep events serialized + deduplicated across clients (18-frame window) |
| `ProceduralSoundBridgeSystem` | Working | Weapon spring velocity → foley audio (rattle on movement, kick on fire) |
| `CollisionAudioSystem` | Working | Player collision impacts with intensity-based clip selection (bump/impact/grunt/evade) |
| `Audio Workstation` | Working | 6 editor modules: Sound Banks, Impact Surfaces, Randomization, Distance Atten, Batch Assign, Audio Preview |
| `AudioTelemetry` | Working | Tracks footstep/landing/action event counts, cache misses, playback failures, throttled events |

### What's Missing (The Gap)

Despite the rich foundation, the audio experience has three AAA-level gaps:

1. **Static Sources** — Enemy screams, projectile sounds, ability SFX are fire-and-forget at spawn position. A falling enemy's yell stays at the cliff edge. A fireball whoosh doesn't follow the projectile.

2. **Flat Propagation** — A gunshot 10m away through a concrete wall sounds identical to one in the open. No occlusion, no obstruction, no distance-based frequency attenuation beyond Unity's basic rolloff.

3. **Dry Environments** — Gunshots in a tunnel sound the same as in an open field. No reverb zones, no interior/exterior transitions, no environmental acoustics beyond vacuum filtering.

4. **No Bus Architecture** — All audio routes through FEEL's MMF_Sound or the 8-source pool with no bus separation. No independent volume control for combat / ambient / music / dialogue / UI. No sidechain ducking.

5. **No Audio LOD** — Every sound plays at full quality regardless of distance. No distant source simplification, no batch culling when source count exceeds budget.

6. **No Spatial Accessibility** — Hearing-impaired players get no visual indication of spatial audio direction (sound radar, subtitle captions with directional indicators).

---

## Architecture

### Audio Bus Topology (New)

```
MasterBus
 ├── CombatBus         (weapons, impacts, abilities, hit feedback)
 │   └── Sidechain → duck AmbientBus by -6dB during combat
 ├── AmbientBus        (environment loops, weather, wildlife, reverb sends)
 ├── MusicBus          (score, combat music, exploration themes)
 ├── DialogueBus       (NPC speech, barks, callouts)
 │   └── Sidechain → duck MusicBus by -9dB during dialogue
 ├── UIBus             (menu clicks, notifications, HUD sounds — always 2D)
 └── FootstepBus       (surface footsteps, movement — separate for fine control)
```

Each bus maps to an `AudioMixerGroup` on the existing FEEL `AudioMixer`. Exposed parameters: `{BusName}Volume`, `{BusName}Cutoff`, `{BusName}ReverbSend`.

### Entity-Linked Audio Pipeline (New)

```
[AudioEmitter Component (ECS)]
    ↓
[AudioSourcePoolSystem (Managed, PresentationSystemGroup)]
 ├── Acquires pooled AudioSource from AudioSourcePool
 ├── Assigns AudioMixerGroup (bus routing) from emitter config
 ├── Sets clip, priority, spatial blend, rolloff
 └── Tracks Entity → AudioSource mapping
    ↓
[AudioTransformSyncSystem (PresentationSystemGroup, after Pool)]
 ├── Updates source.transform.position = entity LocalToWorld.Position
 └── Handles entity death/despawn → return source to pool
    ↓
[AudioOcclusionSystem (PresentationSystemGroup, after Sync)]
 ├── Batched raycasts: listener → source (10Hz spread)
 ├── Writes OcclusionFactor per source
 └── Applies: LowPassFilter cutoff + volume attenuation
    ↓
[AudioPrioritySystem (PresentationSystemGroup, after Occlusion)]
 ├── Scores all active sources (priority × distance × occlusion)
 ├── Enforces voice budget (mutes lowest-scored over limit)
 └── Implements Audio LOD (reduce quality at distance)
```

### Reverb Zone Pipeline (New)

```
[ReverbZoneAuthoring (MonoBehaviour, Trigger Volume)]
    ↓ OnTriggerEnter/Exit
[AudioReverbZoneManager (Managed Singleton)]
 ├── Tracks active zones (stack-based, innermost wins)
 ├── Blends AudioMixer Snapshots (crossfade duration per zone)
 └── Exposes current zone for debug overlay
```

---

## Components

### New ECS Components

```csharp
/// <summary>
/// Marks an entity as an audio source that should be tracked by the spatial audio pipeline.
/// Baked by AudioEmitterAuthoring. The AudioSourcePoolSystem reads this to
/// acquire/release pooled AudioSources and link them to the entity.
/// </summary>
public struct AudioEmitter : IComponentData
{
    /// <summary>Audio bus for mixer routing.</summary>
    public AudioBusType Bus;

    /// <summary>Voice priority. Higher = harder to cull (0=Ambient, 50=Footstep, 100=Weapon, 200=Dialogue).</summary>
    public byte Priority;

    /// <summary>3D spatial blend (0=2D, 1=full 3D). Set by paradigm or emitter config.</summary>
    public float SpatialBlend;

    /// <summary>Max audible distance in meters. Beyond this, source is culled.</summary>
    public float MaxDistance;

    /// <summary>Rolloff mode index (0=Logarithmic, 1=Linear, 2=Custom).</summary>
    public byte RolloffMode;

    /// <summary>Whether this emitter should follow the entity position each frame.</summary>
    public bool TrackPosition;

    /// <summary>Whether occlusion raycasts should be performed for this source.</summary>
    public bool UseOcclusion;
}

/// <summary>
/// Runtime state for an active audio source linked to this entity.
/// Managed by AudioSourcePoolSystem. Not baked — added at runtime when a sound plays.
/// </summary>
public class AudioSourceState : IComponentData  // managed (holds AudioSource reference)
{
    /// <summary>The pooled Unity AudioSource currently assigned to this entity.</summary>
    public AudioSource Source;

    /// <summary>The AudioLowPassFilter on the source (for occlusion).</summary>
    public AudioLowPassFilter LowPass;

    /// <summary>Current occlusion factor (0=fully occluded, 1=clear).</summary>
    public float OcclusionFactor;

    /// <summary>Target occlusion factor (lerped toward over time for smooth transitions).</summary>
    public float TargetOcclusionFactor;

    /// <summary>Frame counter for spread-scheduling occlusion raycasts.</summary>
    public int OcclusionFrameSlot;
}

/// <summary>
/// Request to play a one-shot sound at a position or attached to an entity.
/// Buffer on a singleton entity. Consumed by AudioSourcePoolSystem each frame.
/// </summary>
public struct PlayAudioRequest : IBufferElementData
{
    /// <summary>Clip ID (index into AudioClipBank). -1 for surface-resolved.</summary>
    public int ClipId;

    /// <summary>Surface material ID for surface-resolved clips. -1 if using ClipId directly.</summary>
    public int SurfaceMaterialId;

    /// <summary>World position to play at (if TargetEntity is Null).</summary>
    public float3 Position;

    /// <summary>Entity to attach the source to (Null for fire-and-forget at Position).</summary>
    public Entity TargetEntity;

    /// <summary>Audio bus routing.</summary>
    public AudioBusType Bus;

    /// <summary>Voice priority (higher = harder to cull).</summary>
    public byte Priority;

    /// <summary>Volume multiplier (0-1).</summary>
    public float Volume;

    /// <summary>Pitch multiplier (default 1.0).</summary>
    public float Pitch;

    /// <summary>Whether to loop this sound.</summary>
    public bool Loop;

    /// <summary>Max audible distance. 0 = use bus default.</summary>
    public float MaxDistance;
}

/// <summary>Bus routing enum matching AudioMixer groups.</summary>
public enum AudioBusType : byte
{
    Combat = 0,
    Ambient = 1,
    Music = 2,
    Dialogue = 3,
    UI = 4,
    Footstep = 5
}
```

### Existing Components (Upgraded)

| Component | Change |
|-----------|--------|
| `AudioListenerState` | Add `ReverbZoneId` (int), `IndoorFactor` (float 0-1) |
| `ImpactAudioData` | Add `Bus` (AudioBusType), `UseOcclusion` (bool) |

---

## Implementation Phases

### Phase 1: Audio Bus Architecture & Pool Upgrade

**Goal:** Establish mixer bus routing and expand the source pool from 8 to a managed, priority-aware pool.

**Tasks:**

- [ ] **Create AudioMixer bus groups** on the existing FEEL mixer
  - CombatBus, AmbientBus, MusicBus, DialogueBus, UIBus, FootstepBus
  - Expose volume + cutoff parameters per bus
  - Configure sidechain: CombatBus ducks AmbientBus (-6dB, 0.3s attack, 1s release)
  - Configure sidechain: DialogueBus ducks MusicBus (-9dB, 0.2s attack, 1.5s release)

- [ ] **Create `AudioSourcePool`** (managed singleton, replaces inline pool in AudioManager)
  - Configurable pool size (default 32, max 64)
  - Each pooled source has: `AudioSource` + `AudioLowPassFilter` + `AudioHighPassFilter`
  - Acquire/Release API with bus assignment (sets `outputAudioMixerGroup`)
  - Priority-based eviction: when pool is full, lowest-priority furthest source is stolen
  - Telemetry: active count, peak count, evictions/sec

- [ ] **Create `AudioBusConfig`** (ScriptableObject)
  - Per-bus settings: default volume, default spatial blend, default max distance, default rolloff
  - Paradigm overrides: Shooter=full 3D, ARPG=0.3 blend, TwinStick=0.4 blend
  - Sidechain duck amounts and timing per bus pair

- [ ] **Migrate `AudioManager`** to use `AudioSourcePool`
  - AudioManager.PlayFootstep/PlayJump/etc. acquire from pool instead of internal queue
  - AudioManager remains the surface-clip-resolution facade
  - Pool handles lifecycle; AudioManager handles clip selection

- [ ] **Migrate `GameplayFeedbackManager`** to route through bus groups
  - Combat feedbacks → CombatBus
  - Movement feedbacks → FootstepBus
  - Interaction feedbacks → UIBus

- [ ] **Add Audio Workstation module: Bus Monitor**
  - Real-time per-bus VU meters (peak + RMS)
  - Active source count per bus
  - Sidechain duck indicator (shows when ducking is active)
  - Bus solo/mute toggles for debugging

**Files:**

| File | Type | Lines (est) |
|------|------|-------------|
| `Assets/Scripts/Audio/AudioSourcePool.cs` | Managed Singleton | ~200 |
| `Assets/Scripts/Audio/Config/AudioBusConfig.cs` | ScriptableObject | ~80 |
| `Assets/Scripts/Audio/Components/AudioBusType.cs` | Enum | ~15 |
| `Assets/Editor/AudioWorkstation/Modules/BusMonitorModule.cs` | Editor Module | ~150 |

**Modified:**

| File | Change |
|------|--------|
| `AudioManager.cs` | Replace `_pool` Queue with `AudioSourcePool.Instance.Acquire(bus)` |
| `GameplayFeedbackManager.cs` | Add bus routing to MMF_Sound setup |
| `AudioWorkstationWindow.cs` | Register BusMonitorModule as 7th tab |

---

### Phase 2: Entity-Linked Audio Sources

**Goal:** Sounds follow their source entity. A falling enemy's scream moves with them. A fireball whoosh tracks the projectile.

**Tasks:**

- [ ] **Create `AudioEmitter`** component + `AudioEmitterAuthoring`
  - Baker reads config fields (Bus, Priority, SpatialBlend, MaxDistance, TrackPosition, UseOcclusion)
  - Default: Bus=Combat, Priority=100, SpatialBlend=1, MaxDistance=50, TrackPosition=true, UseOcclusion=true

- [ ] **Create `PlayAudioRequest`** buffer + singleton dispatcher
  - Buffer on singleton entity `AudioRequestSingleton`
  - Systems append requests; `AudioSourcePoolSystem` consumes each frame
  - Request struct holds: ClipId or SurfaceMaterialId, Position or TargetEntity, Bus, Priority, Volume, Pitch, Loop, MaxDistance

- [ ] **Create `AudioSourcePoolSystem`** (Managed SystemBase, PresentationSystemGroup)
  - Consumes `PlayAudioRequest` buffer
  - For entity-linked requests: adds `AudioSourceState` managed component, tracks Entity→Source mapping
  - For fire-and-forget: acquires source, plays at position, returns to pool on completion
  - Handles entity destruction: when entity is destroyed or gets `Disabled`/`DeathState`, stop + return source

- [ ] **Create `AudioTransformSyncSystem`** (Managed SystemBase, PresentationSystemGroup, after AudioSourcePoolSystem)
  - Queries entities with `AudioSourceState` + `LocalToWorld`
  - Updates `AudioSource.transform.position` = `LocalToWorld.Position` each frame
  - If source finished playing (!isPlaying && !loop): remove `AudioSourceState`, return to pool

- [ ] **Create `AudioClipBank`** (ScriptableObject)
  - Central registry mapping int ClipId → AudioClip
  - Categories: Combat, Ambient, Creature, Ability, UI
  - Supports clip variations (array per ID, random selection)
  - Integrates with Audio Workstation Sound Banks module

- [ ] **Add helper API on `AudioEmitter`** for common patterns
  - Static `PlayOneShot(EntityCommandBuffer, Entity, int clipId, AudioBusType bus)` — appends request
  - Static `PlayAtPosition(EntityCommandBuffer, float3 pos, int clipId, AudioBusType bus)` — fire-and-forget
  - Static `PlayOnEntity(EntityCommandBuffer, Entity, int clipId, AudioBusType bus)` — entity-linked

**Files:**

| File | Type | Lines (est) |
|------|------|-------------|
| `Assets/Scripts/Audio/Components/AudioEmitter.cs` | IComponentData | ~40 |
| `Assets/Scripts/Audio/Components/AudioSourceState.cs` | Managed IComponentData | ~30 |
| `Assets/Scripts/Audio/Components/PlayAudioRequest.cs` | IBufferElementData | ~50 |
| `Assets/Scripts/Audio/Systems/AudioSourcePoolSystem.cs` | Managed SystemBase | ~250 |
| `Assets/Scripts/Audio/Systems/AudioTransformSyncSystem.cs` | Managed SystemBase | ~80 |
| `Assets/Scripts/Audio/Config/AudioClipBank.cs` | ScriptableObject | ~100 |
| `Assets/Scripts/Audio/Authoring/AudioEmitterAuthoring.cs` | Baker | ~60 |

---

### Phase 3: Occlusion & Obstruction

**Goal:** Sounds behind walls are muffled. Sounds through thin cover are partially attenuated. Smooth transitions as line-of-sight changes.

**Tasks:**

- [ ] **Create `AudioOcclusionSystem`** (Managed SystemBase, PresentationSystemGroup, after AudioTransformSyncSystem)
  - Queries all entities with `AudioSourceState` where `AudioEmitter.UseOcclusion == true`
  - Frame-spread scheduling: entity index % SpreadFrames == frameCount % SpreadFrames (default SpreadFrames=6, ~10Hz per source)
  - Batch raycasts using `RaycastCommand` (Physics.RaycastCommand job):
    - From: listener position (Camera.main or AudioListener)
    - To: source position
    - Layer mask: environment + structures (exclude creatures, projectiles)
  - Occlusion factor calculation:
    - 0 hits = clear (factor 1.0)
    - 1 hit = partial (factor 0.5)
    - 2+ hits = heavy (factor 0.15)
    - Surface material of hit adjusts: thin metal = 0.7 factor, thick concrete = 0.2 factor
  - Apply per source (smooth lerp, 0.15s transition):
    - `LowPassFilter.cutoffFrequency` = lerp(500, 22000, factor)
    - Volume attenuation = lerp(0.15, 1.0, factor)

- [ ] **Create `OcclusionProfile`** (ScriptableObject)
  - Configurable per-material occlusion multipliers (how much each surface blocks)
  - Raycast spread frame count (default 6)
  - Transition speed (default 0.15s)
  - Max occlusion distance (sources beyond this skip raycasts, assume clear)
  - Minimum source priority for occlusion (skip low-priority sources to save raycasts)

- [ ] **Integrate with existing `AudioEnvironmentSystem`**
  - Vacuum filtering continues as-is (takes priority over occlusion — if in vacuum, all external sounds fully muffled)
  - Occlusion system respects `AudioListenerState.PressureFactor`: at 0 pressure, occlusion is irrelevant (already muted by vacuum)

- [ ] **Add Audio Workstation module: Occlusion Debug**
  - Scene view overlay: draw raycasts (green=clear, yellow=partial, red=heavy)
  - Per-source occlusion factor readout
  - Raycast budget display (raycasts/frame, raycasts/sec)
  - Toggle to disable occlusion globally for A/B comparison

**Files:**

| File | Type | Lines (est) |
|------|------|-------------|
| `Assets/Scripts/Audio/Systems/AudioOcclusionSystem.cs` | Managed SystemBase | ~220 |
| `Assets/Scripts/Audio/Config/OcclusionProfile.cs` | ScriptableObject | ~60 |
| `Assets/Editor/AudioWorkstation/Modules/OcclusionDebugModule.cs` | Editor Module | ~180 |

---

### Phase 4: Reverb Zones & Interior/Exterior Transitions

**Goal:** Different spaces sound different. Tunnels echo. Open fields are dry. Caves resonate. Transitioning between spaces crossfades smoothly.

**Tasks:**

- [ ] **Create `ReverbZoneAuthoring`** (MonoBehaviour + Trigger Collider)
  - Inspector fields: ZoneName, ReverbPreset (enum), TransitionDuration, Priority (for overlapping zones)
  - ReverbPreset enum: OpenField, Forest, SmallRoom, LargeHall, Tunnel, Cave, Underwater, Ship_Interior, Ship_Exterior, Custom
  - Custom mode: direct AudioMixer Snapshot reference
  - Gizmo: transparent colored volume in Scene view

- [ ] **Create `AudioReverbZoneManager`** (Managed Singleton)
  - Stack-based zone tracking: OnTriggerEnter pushes, OnTriggerExit pops
  - Innermost zone wins (highest priority if same depth)
  - Crossfades between AudioMixer Snapshots (configurable duration per zone, default 1.5s)
  - Exposes `CurrentZone`, `PreviousZone`, `TransitionProgress` for debug/UI
  - Fallback zone: OpenField when no zones active

- [ ] **Create AudioMixer Snapshots** for each ReverbPreset
  - Each snapshot configures: Reverb Send level, Reverb decay time, Reverb wet/dry mix, High-frequency damping
  - Reference presets:

  | Preset | Decay (s) | Wet Mix | HF Damp | Notes |
  |--------|-----------|---------|---------|-------|
  | OpenField | 0.3 | 0.05 | 0.8 | Dry, minimal reflection |
  | Forest | 0.6 | 0.10 | 0.6 | Soft diffuse scatter |
  | SmallRoom | 1.2 | 0.25 | 0.4 | Tight, metallic |
  | LargeHall | 2.5 | 0.35 | 0.3 | Spacious, warm |
  | Tunnel | 3.0 | 0.45 | 0.5 | Long tail, hard walls |
  | Cave | 4.0 | 0.50 | 0.2 | Very long, dark resonance |
  | Underwater | 1.5 | 0.30 | 0.9 | Heavy HF damp, murky |
  | Ship_Interior | 1.0 | 0.20 | 0.5 | Metallic, contained |
  | Ship_Exterior | 0.1 | 0.02 | 0.9 | Near-silent (vacuum nearby) |

- [ ] **Update `AudioListenerState`**
  - Add `ReverbZoneId` (int) and `IndoorFactor` (float 0-1) fields
  - `AudioEnvironmentSystem` reads both vacuum state AND reverb zone to drive mixer

- [ ] **Interior/Exterior transition system**
  - `IndoorFactor` lerps 0→1 when entering interior zones, 1→0 when exiting
  - Drives: reverb wet/dry blend, ambient volume crossfade, wind/weather audio fade
  - Separate from vacuum filtering (which is pressure-based)

- [ ] **Add Audio Workstation module: Reverb Zone Preview**
  - List all reverb zones in scene with their presets
  - "Preview" button: transitions to that snapshot in editor (requires play mode)
  - Zone overlap visualization (warns about priority conflicts)
  - Snapshot parameter table (side-by-side comparison)

**Files:**

| File | Type | Lines (est) |
|------|------|-------------|
| `Assets/Scripts/Audio/Zones/ReverbZoneAuthoring.cs` | MonoBehaviour | ~90 |
| `Assets/Scripts/Audio/Zones/AudioReverbZoneManager.cs` | Managed Singleton | ~180 |
| `Assets/Scripts/Audio/Zones/ReverbPreset.cs` | Enum | ~20 |
| `Assets/Editor/AudioWorkstation/Modules/ReverbZoneModule.cs` | Editor Module | ~200 |

**Modified:**

| File | Change |
|------|--------|
| `AudioComponents.cs` | Add ReverbZoneId + IndoorFactor to AudioListenerState |
| `AudioEnvironmentSystem.cs` | Read reverb zone + indoor factor alongside vacuum state |

---

### Phase 5: Audio Priority & LOD

**Goal:** Enforce a voice budget. Distant/unimportant sounds are culled or simplified. Close/critical sounds are always audible.

**Tasks:**

- [ ] **Create `AudioPrioritySystem`** (Managed SystemBase, PresentationSystemGroup, after AudioOcclusionSystem)
  - Each frame, scores all active `AudioSourceState` entities:
    - `Score = Priority * (1.0 / (1.0 + Distance * DistanceFalloff)) * OcclusionBonus`
    - OcclusionBonus: clear sources score higher than occluded (players should hear unblocked sounds first)
  - Voice budget enforcement:
    - Platform budgets: PC = 48, Console = 32
    - If active > budget: mute (volume=0) lowest-scored sources, mark as `LOD_Culled`
    - Exempt sources: `Priority >= 200` (dialogue, critical story audio) are never culled
  - Audio LOD tiers (distance-based quality reduction):
    - **Full** (0–20m): Stereo source, full occlusion processing, full reverb send
    - **Reduced** (20–40m): Mono downmix, occlusion skip (use last known value), reduced reverb send
    - **Minimal** (40–60m): Mono, no occlusion, no reverb, simplified rolloff
    - **Culled** (60m+): Source returned to pool

- [ ] **Create `AudioLODConfig`** (ScriptableObject)
  - Per-tier distance thresholds (with paradigm multiplier — isometric needs larger ranges)
  - Voice budget per platform
  - Distance falloff for scoring
  - Whether to downmix stereo → mono at Reduced tier

- [ ] **Integrate with existing `AudioTelemetry`**
  - Add metrics: active voices, culled voices, priority evictions, LOD tier distribution
  - Add per-bus active count to telemetry

**Files:**

| File | Type | Lines (est) |
|------|------|-------------|
| `Assets/Scripts/Audio/Systems/AudioPrioritySystem.cs` | Managed SystemBase | ~180 |
| `Assets/Scripts/Audio/Config/AudioLODConfig.cs` | ScriptableObject | ~50 |

**Modified:**

| File | Change |
|------|--------|
| `AudioTelemetry.cs` | Add voice count, culled count, per-bus metrics |

---

### Phase 6: Feel Integration & Combat Audio Polish

**Goal:** Explosions cause tinnitus. Gunshots have reverb tails. Heavy impacts have bass. Combat music ducks ambient.

**Tasks:**

- [ ] **Tinnitus System** (explosion deafness)
  - Trigger: `DamageEvent` with `DamageType.Explosion` and damage > threshold (50)
  - Effect: Set `AudioListenerState.IsDeafened = true`, `DeafenTimer = 3.0`
  - Audio: Duck MasterBus by -20dB, play high-pitched sine (12kHz) at 0.3 volume for duration
  - Recovery: Fade MasterBus back to 0dB over 1.5s
  - Accessibility: Option to disable tinnitus audio (keep visual flash only)

- [ ] **Gunshot Reverb Tails**
  - Weapon fire events read current `ReverbZoneId`
  - Outdoor: play long-decay tail sample (2-3s reverb, bus=AmbientBus so it persists after gunshot)
  - Indoor: play short metallic reflection sample (0.5s)
  - Implementation: `WeaponAudioTailSystem` reads `WeaponFireState` + `AudioListenerState.ReverbZoneId`

- [ ] **Bass Boost (LFE Channel)**
  - Heavy impacts (explosions, boss slams, heavy landings) route through CombatBus with LFE send
  - AudioMixer group: CombatBus → LFE Send (+6dB at 60Hz, -3dB rolloff above 120Hz)
  - Configurable in `AudioBusConfig`: `CombatLFESendLevel` (default -6dB, range -20 to +6)

- [ ] **Combat Music Ducking**
  - When `CombatState.IsInCombat` transitions true for local player:
    - MusicBus snapshot transitions to "CombatActive" (volume -3dB, low-pass at 8kHz to make room for SFX)
    - AmbientBus ducks by -4dB
  - On combat exit + 5s grace period:
    - Smooth return to exploration snapshot (2s crossfade)

- [ ] **Distant Explosion Bass Rumble**
  - Explosions beyond 30m play a low-frequency rumble (60-100Hz sine, 1s decay) instead of full explosion SFX
  - Creates "thunder of distant battle" effect

**Files:**

| File | Type | Lines (est) |
|------|------|-------------|
| `Assets/Scripts/Audio/Systems/TinnitusFeedbackSystem.cs` | Managed SystemBase | ~100 |
| `Assets/Scripts/Audio/Systems/WeaponAudioTailSystem.cs` | Managed SystemBase | ~80 |
| `Assets/Scripts/Audio/Systems/CombatMusicDuckSystem.cs` | Managed SystemBase | ~90 |

**Modified:**

| File | Change |
|------|--------|
| `AudioEnvironmentSystem.cs` | Handle tinnitus deafen/recovery mixer ducking |
| `GameplayFeedbackManager.cs` | Route explosion feedback through CombatBus with LFE send |

---

### Phase 7: Accessibility & Spatial Indicators

**Goal:** Hearing-impaired players can perceive audio direction and type through visual indicators.

**Tasks:**

- [ ] **Sound Radar Widget**
  - Screen-space circular radar in corner of HUD (optional, off by default)
  - Shows directional pips for significant sounds (weapons, abilities, dialogue, footsteps)
  - Color-coded: red=danger, blue=friendly, yellow=neutral, white=ambient
  - Size scales with volume/proximity
  - Only tracks sounds above a priority threshold (skip ambient, quiet footsteps)
  - Integrates with Widget Framework (EPIC 15.26) as a WidgetType.SoundRadar

- [ ] **Directional Subtitles**
  - Dialogue and NPC barks display with directional arrows: ← Speaker Name →
  - Arrow points toward sound source relative to camera forward
  - Distance indicator: [Near] / [Mid] / [Far]
  - Speaker color matches faction/hostility

- [ ] **Visual Sound Indicators**
  - Optional screen-edge flash when a loud sound plays from off-screen direction
  - Similar to damage direction indicator but for audio events
  - Red flash = threat (gunshot, explosion nearby)
  - White flash = neutral (door opening, ambient event)
  - Configurable in WidgetAccessibilityManager alongside colorblind/reduced motion settings

- [ ] **Accessibility Settings**
  - `AudioAccessibilityConfig` (ScriptableObject):
    - `EnableSoundRadar` (bool, default false)
    - `EnableDirectionalSubtitles` (bool, default false)
    - `EnableVisualSoundIndicators` (bool, default false)
    - `SubtitleFontScale` (float, 1.0-2.0)
    - `RadarSize` (float, 0.5-2.0)
    - `VisualIndicatorIntensity` (float, 0-1)
    - `DisableTinnitusAudio` (bool, default false)
  - Persisted via PlayerPrefs (same pattern as WidgetAccessibilityManager)

**Files:**

| File | Type | Lines (est) |
|------|------|-------------|
| `Assets/Scripts/Audio/Accessibility/AudioAccessibilityConfig.cs` | ScriptableObject | ~40 |
| `Assets/Scripts/Audio/Accessibility/SoundRadarSystem.cs` | Managed SystemBase | ~200 |
| `Assets/Scripts/Audio/Accessibility/SoundRadarRenderer.cs` | MonoBehaviour (IWidgetRenderer) | ~120 |
| `Assets/Scripts/Audio/Accessibility/DirectionalSubtitleManager.cs` | Managed Singleton | ~150 |

---

### Phase 8: Existing System Quality Upgrades

**Goal:** Bring existing audio systems up to AAA standard without regressions.

**Tasks:**

- [ ] **AudioManager — Pool Exhaustion Handling**
  - Current: 8 sources, round-robin with no priority check. If all 8 are playing, new sounds are silently dropped
  - Fix: Migrate to `AudioSourcePool` (Phase 1). Until then, add logging when pool is exhausted + priority-steal oldest lowest-priority source
  - Add configurable pool size in Audio Workstation (currently hardcoded `PoolSize = 8`)

- [ ] **AudioManager — Distance Culling**
  - Current: No distance check before playing. A footstep 200m away still acquires a source
  - Fix: Add `MaxPlayDistance` check before acquiring a source. Default 60m for footsteps, 100m for combat, unlimited for dialogue
  - Use `AudioBusConfig` defaults per bus

- [ ] **AudioEnvironmentSystem — WorldSystemFilter**
  - Current: No `[WorldSystemFilter]` attribute → runs in ALL worlds including ServerWorld where it does nothing
  - Fix: Add `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]`

- [ ] **AudioEnvironmentSystem — FindFirstObjectByType Every Frame**
  - Current: `_audioManager = Object.FindFirstObjectByType<AudioManager>()` called every frame until found
  - Fix: Cache on first successful find. Add `RequireForUpdate` or guard with `Enabled = false` until found

- [ ] **VitalAudioSystem — Timer Reuse**
  - Current: `TimeSinceLastBreath` field is documented for breathing but also reused for heartbeat timing (comment: "Reuses TimeSinceLastBreath field for heartbeat timer")
  - Fix: Add separate `TimeSinceLastHeartbeat` field to `VitalAudioSource`. Prevents subtle timing bugs when both breath and heartbeat are active simultaneously

- [ ] **GameplayFeedbackManager — No AudioMixer Routing**
  - Current: All FEEL feedbacks play through default output with no bus separation
  - Fix: (Phase 1) Route feedbacks through bus groups. Combat → CombatBus, Movement → FootstepBus, etc.

- [ ] **ImpactAudioSystem — No Distance Culling**
  - Current: Plays impact sounds for ALL collision events regardless of distance from listener
  - Fix: Add distance check against listener position. Skip impacts beyond 40m.

- [ ] **SurfaceMaterial — Continuous Loop Audio (Incomplete)**
  - Current: `ContinuousLoopClip`, `LoopSpeedThreshold`, `LoopVolumeAtMaxSpeed` fields exist on SurfaceMaterial but no system reads them
  - Fix: Implement `SurfaceAudioLoopSystem` that reads player surface contact + velocity → plays/crossfades loop clips
  - Or: Wire into existing `SurfaceAudioLoopManager` if it exists (check EPIC 15.24 Phase 8 status)

- [ ] **Networked Audio — Deduplication Window**
  - Current: 18-frame window is hardcoded. At 30Hz tick rate, this is 0.6s — excessive for rapid footsteps during sprinting
  - Fix: Make dedup window configurable. Default 8 frames (~0.27s at 30Hz) for footsteps, 3 frames for combat sounds

- [ ] **Audio Workstation — Missing Telemetry Display**
  - Current: `AudioTelemetry` tracks events but the Workstation has no module to display them
  - Fix: Add a Telemetry Module tab showing real-time event rates, cache misses, playback failures, pool usage

**Files (new):**

| File | Type | Lines (est) |
|------|------|-------------|
| `Assets/Editor/AudioWorkstation/Modules/TelemetryModule.cs` | Editor Module | ~120 |

---

## Designer Workflow

### Setting Up Audio for a New Enemy Prefab

1. **Add `AudioEmitterAuthoring`** to the enemy prefab root in the subscene
   - Set Bus = Combat, Priority = 80, MaxDistance = 50, TrackPosition = true, UseOcclusion = true
2. **Add `ImpactAudioAuthoring`** if the enemy has physics collision sounds
   - Set MaterialId (lookup in SurfaceMaterialRegistry), MassFactor, VelocityThreshold
3. **Register ability sounds** in `AudioClipBank` (Assets > Create > DIG/Audio/Clip Bank)
   - Assign clip arrays for each ability SFX
4. **In ability systems**, use `PlayAudioRequest` to play sounds:
   - `AudioEmitter.PlayOnEntity(ecb, entity, clipId, AudioBusType.Combat)`
5. Reimport subscene

### Setting Up a Reverb Zone

1. Create empty GameObject in the scene
2. Add **Box Collider** (Is Trigger = true), size to match the room/area
3. Add **`ReverbZoneAuthoring`** component
   - Select ReverbPreset (e.g., Tunnel, Cave, LargeHall)
   - Set TransitionDuration (how long the crossfade takes, default 1.5s)
   - Set Priority (for overlapping zones — higher wins)
4. Test: Enter Play Mode, walk through the zone, listen for reverb transition
5. Debug: Open Audio Workstation > Reverb Zone tab to verify active zone

### Setting Up Audio Buses

1. Open **DIG > Audio Workstation > Bus Monitor** tab
2. If buses not configured: click **"Initialize Bus Groups"** (creates mixer groups on first run)
3. Create `AudioBusConfig` asset: Assets > Create > DIG/Audio/Bus Config
4. Assign per-bus defaults (volume, spatial blend, max distance, rolloff)
5. Wire config into AudioSourcePool inspector
6. Test: Enter Play Mode, trigger various sounds, verify per-bus VU meters respond

---

## Performance Budget

| Resource | Budget | Notes |
|----------|--------|-------|
| AudioSource pool size | 32 (PC), 24 (Console) | Configurable via AudioSourcePool |
| Voice limit (active playing) | 48 (PC), 32 (Console) | Enforced by AudioPrioritySystem |
| Occlusion raycasts/frame | 5-8 | Spread across 6 frames = ~48 sources at 10Hz each |
| Occlusion raycast layer mask | Environment + Structures only | ~4 layers, excludes creatures/projectiles/triggers |
| Reverb zone transition cost | 1 mixer snapshot blend/transition | Negligible CPU |
| Bus sidechain evaluation | Per-frame mixer parameter read | Built into Unity AudioMixer |
| AudioTransformSync entities | 32 max (= pool size) | One position write per entity per frame |

### Paradigm Scaling

| Paradigm | Spatial Blend | Voice Budget | Occlusion | LOD Distances |
|----------|--------------|-------------|-----------|---------------|
| Shooter | 1.0 (full 3D) | 48 | Full | 20/40/60m |
| MMO | 0.8 | 48 | Full | 25/50/75m |
| ARPG | 0.3 | 32 | Simplified (3Hz) | 30/60/90m |
| MOBA | 0.3 | 24 | Off | 40/80/120m |
| TwinStick | 0.4 | 32 | Simplified (3Hz) | 30/60/90m |

---

## System Execution Order

### PresentationSystemGroup (Client/Local only)

```
AudioEnvironmentSystem          (existing — vacuum + reverb zone mixer state)
    ↓
AudioSourcePoolSystem           (NEW — consumes PlayAudioRequest, acquires sources)
    ↓
AudioTransformSyncSystem        (NEW — updates source positions from entity LocalToWorld)
    ↓
AudioOcclusionSystem            (NEW — batched raycasts, applies LPF + volume)
    ↓
AudioPrioritySystem             (NEW — scores, budgets, culls lowest-priority)
    ↓
TinnitusFeedbackSystem          (NEW — explosion deafness effect)
    ↓
WeaponAudioTailSystem           (NEW — reverb tails for gunshots)
    ↓
CombatMusicDuckSystem           (NEW — combat state → music/ambient ducking)
    ↓
SoundRadarSystem                (NEW — accessibility directional pips)
```

### Existing systems (unchanged ordering)

```
ProceduralSoundBridgeSystem     (EPIC 15.25 — weapon foley, runs after WeaponSpringSolverSystem)
VitalAudioSystem                (breathing/heartbeat — PresentationSystemGroup)
ImpactAudioSystem               (collision sounds — PresentationSystemGroup)
CollisionAudioSystem            (player impacts — PresentationSystemGroup)
AudioPlaybackSystem             (footstep/jump/roll events — SimulationSystemGroup)
NetworkedAudioPlaybackSystem    (remote footsteps — SimulationSystemGroup)
```

---

## Audio Workstation Extensions

After all phases, the Audio Workstation grows from 6 to 10 modules:

| # | Module | Status | Purpose |
|---|--------|--------|---------|
| 1 | Sound Banks | Existing | Organize clips by type/category |
| 2 | Impact Surfaces | Existing | Configure per-surface impact audio |
| 3 | Randomization | Existing | Pitch/volume variance settings |
| 4 | Distance Atten | Existing | 3D attenuation curves |
| 5 | Batch Assign | Existing | Bulk assign clips to materials |
| 6 | Audio Preview | Existing | Real-time clip playback/preview |
| 7 | **Bus Monitor** | **NEW (Phase 1)** | Per-bus VU meters, active counts, sidechain indicators |
| 8 | **Occlusion Debug** | **NEW (Phase 3)** | Raycast visualization, per-source factors, budget display |
| 9 | **Reverb Zones** | **NEW (Phase 4)** | Zone list, preset preview, overlap warnings |
| 10 | **Telemetry** | **NEW (Phase 8)** | Real-time event rates, pool usage, playback failures |

---

## Verification Checklist

| # | Phase | Test | Expected Result |
|---|-------|------|-----------------|
| 1 | 1 | Play footsteps while watching Bus Monitor | FootstepBus VU meter responds, CombatBus silent |
| 2 | 1 | Fire weapon during combat | CombatBus active, AmbientBus ducks by -6dB |
| 3 | 1 | Exhaust pool (spam 50+ sounds) | Priority eviction: footsteps dropped, gunshots kept |
| 4 | 2 | Enemy falls off ledge while screaming | Scream follows enemy downward |
| 5 | 2 | Fireball projectile whooshes past | Sound tracks projectile position |
| 6 | 2 | Enemy dies mid-scream | Sound stops, source returned to pool |
| 7 | 3 | Stand behind concrete wall, NPC fires weapon | Muffled (low-pass 500Hz), reduced volume |
| 8 | 3 | Step out from behind wall | Sound smoothly transitions to clear (0.15s) |
| 9 | 3 | Open Occlusion Debug module | Green/yellow/red raycast lines visible in scene |
| 10 | 4 | Walk from open field into tunnel | Reverb crossfades from dry to echoing over 1.5s |
| 11 | 4 | Walk from tunnel into cave | Reverb deepens, decay increases |
| 12 | 4 | Open Reverb Zone module | Lists all zones with presets and priorities |
| 13 | 5 | Spawn 60 sound-emitting entities | Only 48 audible (PC), rest silently culled |
| 14 | 5 | Walk away from sound source | At 40m: mono downmix. At 60m: source culled |
| 15 | 6 | Take 60 explosion damage | 3s tinnitus: ducked audio + high-pitched ring |
| 16 | 6 | Fire weapon in tunnel vs open field | Tunnel: short metallic tail. Field: long decay tail |
| 17 | 6 | Enter combat | Music ducks -3dB, ambient ducks -4dB |
| 18 | 7 | Enable Sound Radar (accessibility) | Directional pips appear for nearby sounds |
| 19 | 7 | Enable Directional Subtitles | NPC bark shows with ← arrow and [Near] tag |
| 20 | 8 | Stand 100m from footstep source | No sound played (distance culled) |
| 21 | 8 | Open Audio Workstation Telemetry tab | Event rates, pool usage, failures displayed |
| 22 | All | Play for 120 seconds with all features | No console errors, no audio pops/clicks |

---

## File Manifest (All Phases)

### New Files

| File | Phase | Type | Lines (est) |
|------|-------|------|-------------|
| `Assets/Scripts/Audio/AudioSourcePool.cs` | 1 | Managed Singleton | ~200 |
| `Assets/Scripts/Audio/Config/AudioBusConfig.cs` | 1 | ScriptableObject | ~80 |
| `Assets/Scripts/Audio/Config/AudioBusType.cs` | 1 | Enum | ~15 |
| `Assets/Scripts/Audio/Components/AudioEmitter.cs` | 2 | IComponentData | ~40 |
| `Assets/Scripts/Audio/Components/AudioSourceState.cs` | 2 | Managed IComponentData | ~30 |
| `Assets/Scripts/Audio/Components/PlayAudioRequest.cs` | 2 | IBufferElementData | ~50 |
| `Assets/Scripts/Audio/Systems/AudioSourcePoolSystem.cs` | 2 | Managed SystemBase | ~250 |
| `Assets/Scripts/Audio/Systems/AudioTransformSyncSystem.cs` | 2 | Managed SystemBase | ~80 |
| `Assets/Scripts/Audio/Config/AudioClipBank.cs` | 2 | ScriptableObject | ~100 |
| `Assets/Scripts/Audio/Authoring/AudioEmitterAuthoring.cs` | 2 | Baker | ~60 |
| `Assets/Scripts/Audio/Systems/AudioOcclusionSystem.cs` | 3 | Managed SystemBase | ~220 |
| `Assets/Scripts/Audio/Config/OcclusionProfile.cs` | 3 | ScriptableObject | ~60 |
| `Assets/Scripts/Audio/Zones/ReverbZoneAuthoring.cs` | 4 | MonoBehaviour | ~90 |
| `Assets/Scripts/Audio/Zones/AudioReverbZoneManager.cs` | 4 | Managed Singleton | ~180 |
| `Assets/Scripts/Audio/Zones/ReverbPreset.cs` | 4 | Enum | ~20 |
| `Assets/Scripts/Audio/Systems/AudioPrioritySystem.cs` | 5 | Managed SystemBase | ~180 |
| `Assets/Scripts/Audio/Config/AudioLODConfig.cs` | 5 | ScriptableObject | ~50 |
| `Assets/Scripts/Audio/Systems/TinnitusFeedbackSystem.cs` | 6 | Managed SystemBase | ~100 |
| `Assets/Scripts/Audio/Systems/WeaponAudioTailSystem.cs` | 6 | Managed SystemBase | ~80 |
| `Assets/Scripts/Audio/Systems/CombatMusicDuckSystem.cs` | 6 | Managed SystemBase | ~90 |
| `Assets/Scripts/Audio/Accessibility/AudioAccessibilityConfig.cs` | 7 | ScriptableObject | ~40 |
| `Assets/Scripts/Audio/Accessibility/SoundRadarSystem.cs` | 7 | Managed SystemBase | ~200 |
| `Assets/Scripts/Audio/Accessibility/SoundRadarRenderer.cs` | 7 | MonoBehaviour | ~120 |
| `Assets/Scripts/Audio/Accessibility/DirectionalSubtitleManager.cs` | 7 | Managed Singleton | ~150 |
| `Assets/Editor/AudioWorkstation/Modules/BusMonitorModule.cs` | 1 | Editor Module | ~150 |
| `Assets/Editor/AudioWorkstation/Modules/OcclusionDebugModule.cs` | 3 | Editor Module | ~180 |
| `Assets/Editor/AudioWorkstation/Modules/ReverbZoneModule.cs` | 4 | Editor Module | ~200 |
| `Assets/Editor/AudioWorkstation/Modules/TelemetryModule.cs` | 8 | Editor Module | ~120 |

**Total new files: 28** (~3,155 lines estimated)

### Modified Files

| File | Phase | Change |
|------|-------|--------|
| `AudioManager.cs` | 1, 8 | Migrate to AudioSourcePool, add distance culling |
| `GameplayFeedbackManager.cs` | 1 | Bus routing for FEEL feedbacks |
| `AudioWorkstationWindow.cs` | 1, 3, 4, 8 | Register 4 new module tabs |
| `AudioComponents.cs` | 4, 8 | Add ReverbZoneId + IndoorFactor to AudioListenerState, add TimeSinceLastHeartbeat to VitalAudioSource |
| `AudioEnvironmentSystem.cs` | 4, 6, 8 | Handle reverb zone + tinnitus + WorldSystemFilter + cached reference |
| `AudioTelemetry.cs` | 5 | Add voice count, per-bus metrics |
| `ImpactAudioSystem.cs` | 8 | Add distance culling |
| `NetworkedAudioPlaybackSystem.cs` | 8 | Configurable dedup window |

---

## Relationship to Other EPICs

| Concern | EPIC |
|---------|------|
| Surface materials, impact SFX, audio variance | 15.24 |
| Weapon foley (spring → sound) | 15.25 |
| Widget framework (Sound Radar integration) | 15.26 |
| **Dynamic Audio Ecosystem** | **15.27 (this)** |
| Combat resolution (damage events → audio) | 15.28 / 15.29 |
| Enemy AI abilities (ability SFX) | 15.31 / 15.32 |
| Airlock audio | 5.1 (existing) |
