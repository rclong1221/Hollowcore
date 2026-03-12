# EPIC 15.24: Surface & Material FX Engine

**Status:** Phases 1-6 Implemented, Phases 7-12 Planned
**Last Updated:** 2026-02-14
**Priority:** High (Core Gameplay Feel)
**Dependencies:**
- [x] EPIC 15.23 — Physics optimization (collision layers, separation, broadphase)
- [x] EPIC 14.20 — Weapon effects pipeline (EffectPoolManager, EffectPrefabRegistry)
- [x] EPIC 13.18 — Footstep system, surface detection service
- [x] EPIC 15.20 — Input paradigm system (ParadigmStateMachine, InputParadigm enum)
- [x] EPIC 15.32 — Enemy ability framework (AbilityExecutionState, telegraph zones)
- [ ] EPIC 15.25 — Procedural motion layer (shared MotionIntensitySettings)

**Feature:** A universal surface-aware effects pipeline that makes every physical interaction — bullet impacts, footsteps, explosions, ability AOEs, vehicle tracks — produce material-correct VFX, audio, decals, haptics, and camera feedback. Auto-adapts effect scale and behavior per InputParadigm (Shooter/MMO/ARPG/MOBA/TwinStick/SideScroller) and platform tier (PC/Console/Mobile).

---

## Problem Statement

### Current State (Pre-EPIC 15.24)

| System | Status | Issue |
|--------|--------|-------|
| Impact VFX paths | 4 competing | `SurfaceImpactSystem` (Instantiate), `ImpactEffectSpawnerSystem` (ECS), `EffectPresentationSystem` (pooled), `ProjectileImpactPresentationSystem` (delegates). No single source of truth |
| Decal systems | 2 redundant | `DecalManager` (URP ring-buffer, efficient) vs `DecalSystem` (legacy GameObject pool, CPU fade). They don't know about each other |
| Pooling managers | 3 separate | `VFXManager`, `EffectPoolManager`, `DecalManager` — different APIs, different lifetime models |
| Surface resolution | GC-heavy | `SurfaceManager` uses string dictionary lookups with `" (Instance)"` suffix stripping. Not Burst-safe |
| Multi-genre support | NONE | All effects hardcoded for FPS camera distance. No paradigm awareness |
| Continuous audio | NONE | Event-driven only. No sliding, wading, or surface contact loops |
| Ability surface FX | NONE | Spells/abilities produce no ground marks (scorch, ice, poison) |
| Vehicle surface FX | NONE | No tire tracks, skid marks, or surface spray |
| Haptic feedback | NONE | No per-surface controller vibration profiles |
| Accessibility | NONE | No intensity slider, no platform scaling, no motion sensitivity control |
| Debug tooling | NONE | No surface ID overlay, no impact counter, no profiler markers |

### Desired State

Every physical interaction produces surface-correct, paradigm-aware, performance-scaled feedback:
- **Genre-aware**: Automatically adapts LOD distances, decal sizes, camera shake, and feature toggles per InputParadigm
- **Material-correct**: 24 surface types with unique VFX, audio, decals, ricochet behavior, and penetration physics
- **Continuous**: Moving surfaces produce ambient loops (ice crackle, water wading, gravel crunch) not just one-shot events
- **Ability-aware**: AOE spells leave persistent ground effects (fire scorch, ice patches, poison puddles)
- **Vehicle-aware**: Mounted movement produces surface-appropriate tracks, spray, and skid marks
- **Accessible**: Global intensity slider (0=disabled, 1=normal, 2=exaggerated) + platform tier scaling
- **Performant**: <0.5ms total budget, managed presentation layer with static queue bridge, 32 events/frame cap

---

## Architecture Decisions

### Decision 1: Static Queue Bridge (Not IBufferElementData)

| Approach | Pros | Cons |
|----------|------|------|
| `IBufferElementData` on ghost entity | Burst-safe, ECS-native | Creates ghost replication overhead, violates MEMORY.md constraint ("NEVER create new IBufferElementData on ghost-replicated entities") |
| Static `Queue<T>` bridge | Zero GC (struct queue), decoupled producers/consumers, matches `DamageVisualQueue` pattern | Not Burst-safe (managed container), single-threaded only |
| NativeQueue shared between systems | Burst-safe, thread-safe | Complex lifecycle management, requires explicit disposal |

**Decision:** Static `Queue<SurfaceImpactData>` bridge (`SurfaceImpactQueue`). Matches the established `DamageVisualQueue` pattern used throughout the codebase. Producers (any ECS system or managed code) enqueue, single consumer (`SurfaceImpactPresenterSystem`) dequeues in PresentationSystemGroup. Zero GC because `SurfaceImpactData` is an unmanaged struct. Single-threaded access is guaranteed because all enqueuing happens from managed-side system code (not from Burst jobs).

### Decision 2: Unified Presenter (Not Separate VFX/Decal/Audio Systems)

| Approach | Pros | Cons |
|----------|------|------|
| Three separate systems (VFX, Decal, Audio) | Clean separation of concerns, each system Burst-compatible | 3x queue drain overhead, 3x dependency resolution, harder to coordinate LOD decisions |
| Single unified presenter | One drain pass, shared LOD computation, simpler paradigm integration | Larger single file, not Burst-compatible (managed singletons) |

**Decision:** Single unified `SurfaceImpactPresenterSystem`. Drains the queue once, computes LOD once, then spawns VFX + decals + audio + camera shake in a single pass per event. The presentation layer is inherently managed code (VFXManager, DecalManager, AudioManager are MonoBehaviour singletons), so Burst compatibility is impossible regardless of system count.

### Decision 3: ScriptableObject Paradigm Config (Not BlobAsset)

| Approach | Pros | Cons |
|----------|------|------|
| BlobAsset with paradigm weights | Burst-safe, O(1) indexed | Overkill for 6 paradigms, only needed in managed code, complex baker setup |
| ScriptableObject per paradigm | Designer-friendly Inspector, hot-reloadable, simple | Not Burst-safe (irrelevant — consumer is managed) |

**Decision:** `ParadigmSurfaceProfile` ScriptableObject per paradigm, cached at runtime by `ParadigmSurfaceConfig` singleton. Data is tiny (6 profiles x ~40 bytes = 240 bytes), paradigm switches are rare (1-2/session), and the consumer (`SurfaceImpactPresenterSystem`) is already managed code.

### Decision 4: Separate Loop Manager for Continuous Audio

| Approach | Pros | Cons |
|----------|------|------|
| Extend AudioManager with loop support | Single manager, shared pool | Looping AudioSources interfere with one-shot pool, AudioManager API change risk |
| Dedicated SurfaceAudioLoopManager | Clean separation, independent pool, crossfade logic isolated | Another singleton, small additional memory |

**Decision:** Dedicated `SurfaceAudioLoopManager` with its own pool of 4 looping AudioSources. Continuous surface audio (wading, sliding, crunching) has fundamentally different lifecycle from one-shot impacts. Crossfade logic and velocity-driven volume modulation don't belong in the one-shot AudioManager.

---

## Architecture Overview

### Data Flow (Implemented)

```
                         ┌──────────────────────────────────────┐
                         │         IMPACT SOURCES                │
                         │  Hitscan / Projectile / Melee /       │
                         │  Footstep / Explosion / BodyFall /    │
                         │  Ability AOE / Vehicle Contact        │
                         └──────────┬───────────────────────────┘
                                    │
                    ┌───────────────┼───────────────────┐
                    ▼               ▼                   ▼
          ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐
          │ Hitscan      │  │ Ricochet /   │  │ Water / Body /   │
          │ Impact       │  │ Penetration  │  │ Ability / Mount  │
          │ Bridge       │  │ System       │  │ Systems          │
          └──────┬───────┘  └──────┬───────┘  └──────┬───────────┘
                 │                 │                  │
                 └────────────┬────┘──────────────────┘
                              ▼
                 ┌────────────────────────┐
                 │  SurfaceImpactQueue    │
                 │  (Static Queue)        │
                 │  Enqueue / TryDequeue  │
                 └──────────┬─────────────┘
                            ▼
          ┌──────────────────────────────────────┐
          │  SurfaceImpactPresenterSystem         │
          │  (PresentationSystemGroup, Client)    │
          │                                       │
          │  1. Dequeue (max 32/frame)            │
          │  2. Paradigm multipliers              │
          │  3. LOD tier (distance-based)         │
          │  4. Spawn VFX  → VFXManager           │
          │  5. Spawn Decal → DecalManager        │
          │  6. Play Audio → AudioManager         │
          │  7. Camera Shake → CameraShakeEffect  │
          │  8. Screen Dirt → ScreenDirtOverlay    │
          │  9. Haptic → GameplayFeedbackManager  │
          └──────────────────────────────────────┘

   Parallel consumer systems (also PresentationSystemGroup):
     FootprintDecalSpawnerSystem  → DecalManager
     SurfaceContactAudioSystem   → SurfaceAudioLoopManager
     AbilityGroundEffectSystem   → DecalManager + VFXManager
     MountSurfaceEffectSystem    → DecalManager + SurfaceImpactQueue
     SurfaceHapticBridgeSystem   → GameplayFeedbackManager
     ScreenDirtSystem            → ScreenDirtOverlay
```

### Design Principles

1. **Single pipeline** — every impact event flows through `SurfaceImpactQueue` to one presenter
2. **Keep what works** — VFXManager, DecalManager, AudioManager, CameraShakeEffect, SurfaceDetectionService, GameplayFeedbackManager
3. **Paradigm-adaptive** — all effects auto-scale per InputParadigm via `ParadigmSurfaceConfig`
4. **Burst-safe core** — BlobAsset for surface property lookup, integer-only in hot paths
5. **Managed presentation** — pool managers stay as MonoBehaviour singletons (they manage GameObjects)
6. **Accessibility-first** — global intensity slider affects all output channels

---

## Performance Budget

| Resource | Budget | Rationale |
|----------|--------|-----------|
| Simultaneous impact particles | 64 | VFXManager throttles at 30/s, steals oldest beyond limit |
| Active decal projectors | 100 | DecalManager ring-buffer. URP handles GPU fade |
| Audio voices (impact) | 16 | AudioManager pool size. Beyond this, skip lowest-priority |
| Surface events per frame | 32 | `MaxEventsPerFrame` cap. Overflow queues to next frame (FIFO) |
| Footprint decals | 32 | Separate ring-buffer, independent of impact decals |
| Continuous audio loops | 4 | SurfaceAudioLoopManager pool size |
| Max effect distance | 60m | Beyond this, skip entirely (paradigm-scaled) |
| Ground effect decals | 16 | Persistent ability decals with extended lifetime |

### System Performance Budget

| System | Budget | Burst? | Notes |
|--------|--------|--------|-------|
| SurfaceImpactPresenterSystem | 0.15ms | No | Main consumer, 32 events/frame max |
| RicochetPenetrationSystem | 0.03ms | No | Reads EnvironmentHitRequest, enqueues extras |
| HitscanImpactBridgeSystem | 0.02ms | No | Bridges EnvironmentHitRequest → queue |
| FootprintDecalSpawnerSystem | 0.02ms | No | Footstep events → DecalManager |
| WaterInteractionSystem | 0.01ms | No | Water footstep replacement |
| BodyFallImpactSystem | 0.01ms | No | Corpse ragdoll detection |
| ScreenDirtSystem | 0.01ms | No | Static trigger check |
| SurfaceContactAudioSystem | 0.02ms | No | Velocity → loop volume |
| AbilityGroundEffectSystem | 0.02ms | No | Queue drain + ability query |
| MountSurfaceEffectSystem | 0.02ms | No | Distance tracking + decal spawn |
| SurfaceHapticBridgeSystem | 0.01ms | No | Impact proximity check |
| **Total** | **0.32ms** | | Well under 0.5ms |

---

## Surface Database

### SurfaceID Enum

Compile-time byte IDs. No string lookups at runtime.

```csharp
public enum SurfaceID : byte
{
    Default = 0,      Concrete = 1,     Metal_Thin = 2,   Metal_Thick = 3,
    Wood = 4,         Dirt = 5,         Sand = 6,         Grass = 7,
    Gravel = 8,       Snow = 9,         Ice = 10,         Water = 11,
    Mud = 12,         Glass = 13,       Flesh = 14,       Armor = 15,
    Fabric = 16,      Plastic = 17,     Stone = 18,       Ceramic = 19,
    Foliage = 20,     Bark = 21,        Rubber = 22,      Energy_Shield = 23
    // 24-255 reserved
}
```

### BlobAsset: SurfaceDatabaseBlob

Built at world initialization from `SurfaceMaterialRegistry` by `SurfaceDatabaseInitSystem`.

```csharp
public struct SurfaceDatabaseBlob
{
    public BlobArray<SurfaceEntry> Surfaces;  // indexed by (byte)SurfaceID
}

public struct SurfaceEntry
{
    public byte SurfaceId;
    public byte Hardness;            // 0=soft, 255=hardest. Controls ricochet threshold
    public byte Density;             // 0=light, 255=dense. Controls penetration resistance
    public bool AllowsPenetration;
    public bool AllowsRicochet;
    public bool AllowsFootprints;
    public bool IsLiquid;
    public int AudioMaterialId;      // index into managed SurfaceMaterialRegistry
}

public struct SurfaceDatabaseSingleton : IComponentData
{
    public BlobAssetReference<SurfaceDatabaseBlob> Database;
}
```

### Why BlobAsset

- **Burst-safe**: O(1) integer index into contiguous memory. No managed references.
- **Cache-friendly**: 24 entries x ~20 bytes = 480 bytes. Fits in L1 cache.
- **Thread-safe**: Read-only from any worker thread. No race conditions.
- **Managed bridge**: BlobAsset stores `AudioMaterialId` (int) that the managed presentation layer resolves to actual assets via `SurfaceMaterialRegistry.GetById()`.

### Resolution Priority Chain

Surface ID resolution follows this order:

1. **Explicit** — `SurfaceImpactData.SurfaceId` set by producer (e.g., `SurfaceID.Flesh` for enemy hits)
2. **SurfaceMaterial SO** — `SurfaceIdResolver.FromMaterial()` reads `SurfaceMaterial.SurfaceId` field
3. **Name heuristic** — `SurfaceIdResolver` checks `DisplayName` for keywords ("metal", "concrete", etc.)
4. **Legacy enum** — `SurfaceIdResolver.FromSurfaceMaterialType()` maps old enum values
5. **Fallback** — `SurfaceID.Default` (concrete-like generic)

---

## ImpactClass System

Every impact event carries an `ImpactClass` that drives effect variant selection:

```csharp
public enum ImpactClass : byte
{
    Bullet_Light = 0,     // Pistol, SMG
    Bullet_Medium = 1,    // Rifle, LMG
    Bullet_Heavy = 2,     // Shotgun, Sniper
    Melee_Light = 3,      // Knife, fist
    Melee_Heavy = 4,      // Katana, hammer
    Explosion_Small = 5,  // Grenade
    Explosion_Large = 6,  // Rocket, C4
    Footstep = 7,         // Walking/running
    BodyFall = 8,         // Ragdoll hitting ground
    Environmental = 9     // Falling debris, ricochet spark
}
```

### Scaling Tables (Static Arrays in SurfaceImpactPresenterSystem)

| ImpactClass | Particle Scale | Decal Scale | Camera Shake | Screen Dirt |
|-------------|---------------|-------------|-------------|-------------|
| Bullet_Light | 0.5x | 0.05m | 0 | No |
| Bullet_Medium | 1.0x | 0.10m | 0 | No |
| Bullet_Heavy | 1.5x | 0.15m | 0.10 | No |
| Melee_Light | 0.8x | 0.10m | 0 | No |
| Melee_Heavy | 1.2x | 0.20m | 0.15 | No |
| Explosion_Small | 2.0x | 0.80m | 0.30 | No |
| Explosion_Large | 3.0x | 1.50m | 0.50 | Yes |
| Footstep | 0.3x | 0 (separate) | 0 | No |
| BodyFall | 0.6x | 0.30m | 0.05 | No |
| Environmental | 0.5x | 0.10m | 0 | No |

### ImpactClass Resolution

`ImpactClassResolver` provides static resolution methods:

| Method | Input | Logic |
|--------|-------|-------|
| `FromWeaponCategory()` | WeaponCategory enum | Pistol/SMG→Light, Rifle/LMG→Medium, Shotgun/Sniper→Heavy |
| `FromImpactType()` | Legacy ImpactType enum | Bullet→Medium, Melee→MeleeLight, Explosion→ExplosionSmall |
| `FromDamage()` | float damage value | <20→Light, <50→Medium, 50+→Heavy |

---

## Ricochet & Penetration

### Ricochet Logic

`RicochetPenetrationSystem` runs in `SimulationSystemGroup` (Server|Local), `UpdateBefore(HitscanImpactBridgeSystem)`.

```
incidentAngle = acos(dot(-normalize(velocity), normal))  → degrees

ricochetThreshold = 75 - (surface.Hardness / 255) * 45   → degrees
  Hardness 0   → threshold 75° (only very grazing angles ricochet)
  Hardness 255 → threshold 30° (ricochets easily)

if surface.AllowsRicochet AND incidentAngle > ricochetThreshold:
    → Enqueue ricochet spark trail
    → Reflected velocity = reflect(velDir, normal) * |velocity| * 0.6
    → ImpactClass = Environmental, Intensity = 0.7
```

### Penetration Logic

```
if surface.AllowsPenetration:
    bulletPower = |velocity|
    resistance = surface.Density
    if bulletPower > resistance:
        → Enqueue exit-side dust puff
        → Exit position = position + velDir * 0.15m (thin-wall assumption)
        → Exit normal = -normal (opposite direction)
        → Exit velocity = velDir * |velocity| * 0.4
        → ImpactClass = Bullet_Light, Intensity = 0.5
```

### Surface-Specific Impact Behavior

| Surface | Ricochet | Penetration | VFX Character |
|---------|----------|-------------|---------------|
| Glass | No | Always | Shatter + tinkle audio |
| Metal_Thin | Likely | 50% chance | Spark shower + metallic ring |
| Metal_Thick | Very likely | Never | Single spark + deep clang |
| Wood | Unlikely | Likely | Splinter burst + thud |
| Concrete | Moderate | Never | Dust cloud + chip debris |
| Flesh | Never | Always | Blood spray + wet impact |
| Stone | Likely | Never | Dust puff + rock chip |
| Ice | Moderate | Likely | Crystal shatter + crack audio |

---

## Water & Liquid Interactions

When `SurfaceMaterial.IsLiquid == true`, `WaterInteractionSystem` intercepts footstep events:

| Event | Effect |
|-------|--------|
| Player walks in water | Footstep replaced with splash VFX, normal always UP, intensity clamped 0.3-0.8 |
| Bullet hits water | Splash column (standard impact pipeline), muted "plunk" audio at 50% volume |
| Body falls in water | Splash proportional to ragdoll velocity |

Water depth scaling is VFX-prefab-driven (designers control splash height in the particle system).

---

## Footprint System

`FootprintDecalSpawnerSystem` (PresentationSystemGroup, Client|Local) consumes `FootstepEvent` components:

1. Check `SurfaceMaterial.AllowFootprints` and `FootprintDecal != null`
2. Determine left/right foot from `FootstepEvent.FootIndex` (0=left, 1=right)
3. Flip right foot decal rotation by 180° around Y axis
4. Spawn via `DecalManager.SpawnDecal()` with surface-specific lifetime

### Surface-Specific Footprint Lifetimes

| Surface | Lifetime | Character |
|---------|----------|-----------|
| Snow | 60s | Deep impression, slow fade |
| Mud | 30s | Dark impression, squelch audio |
| Sand | 15s | Shallow scatter, fast fade |
| Dirt | 20s | Standard print |
| Default | 10s | Generic footprint |

---

## Effect LOD System

LOD tier computed per event from camera distance, then multiplied by paradigm profile:

| Distance | LOD Tier | Particles | Decals | Audio | Debris |
|----------|----------|-----------|--------|-------|--------|
| 0 — LOD_Full (15m) | Full | 100% emission | Full size, random rotation | 3D spatial, full priority | Yes |
| LOD_Full — LOD_Reduced (40m) | Reduced | 50% emission | Half size | 3D spatial, low priority | No |
| LOD_Reduced — LOD_Minimal (60m) | Minimal | Skip (billboard only) | Skip | 2D falloff only | No |
| LOD_Minimal+ | Culled | Skip entirely | Skip | Skip | No |

**Paradigm adaptation**: All LOD distances are multiplied by the active `ParadigmSurfaceProfile.LODFullMultiplier` etc. Isometric cameras use 2.0x multipliers because the camera is farther from action.

---

## Paradigm Integration

### Effect Weight Table Per InputParadigm

| Feature | Shooter | MMO | ARPG | MOBA | TwinStick | SideScroller |
|---------|---------|-----|------|------|-----------|--------------|
| Particle scale | 1.0x | 1.0x | 1.5x | 1.5x | 1.2x | 0.8x |
| Decal scale | 1.0x | 1.2x | 2.0x | 2.0x | 1.5x | 1.0x |
| Camera shake | 1.0x | 0.7x | 0.3x | 0.2x | 0.6x | 0.5x |
| LOD Full range | 15m | 20m | 30m | 30m | 20m | 15m |
| LOD Reduced range | 40m | 50m | 60m | 60m | 50m | 40m |
| LOD Minimal range | 60m | 70m | 80m | 80m | 70m | 60m |
| Screen dirt | Yes | Yes | No | No | No | No |
| Footprints | Yes | Yes | Yes | No | Yes | No |
| Audio occlusion | Yes | Yes | Yes | No | Yes | No |
| Audio 3D blend | 1.0 | 1.0 | 0.5 | 0.3 | 0.7 | 0.0 |
| Max events/frame | 32 | 32 | 24 | 16 | 24 | 16 |
| Continuous audio | Yes | Yes | Yes | No | Yes | No |
| Haptic feedback | Full | Full | Reduced | None | Full | Reduced |

### Runtime Resolution

`ParadigmSurfaceConfig` (MonoBehaviour singleton) subscribes to `ParadigmStateMachine.Instance.OnParadigmChanged`. On paradigm switch, it caches the matching `ParadigmSurfaceProfile` SO. `SurfaceImpactPresenterSystem` reads `ParadigmSurfaceConfig.Instance.ActiveProfile` each frame for O(1) access to all multipliers.

---

## Continuous Surface Audio

Surfaces that produce persistent ambient audio when the player moves across them:

| Surface | Loop Clip | Speed Threshold | Max Volume | Character |
|---------|-----------|-----------------|------------|-----------|
| Ice | Ice crackle | 0.5 m/s | 0.6 | Crisp crackling, pitch rises with speed |
| Water | Wading splash | 0.3 m/s | 0.7 | Rhythmic sloshing, intensity scales with depth |
| Gravel | Gravel crunch | 0.5 m/s | 0.5 | Crunching, rate proportional to speed |
| Snow | Snow compression | 0.5 m/s | 0.4 | Soft compression crunch |
| Sand | Sand shifting | 1.0 m/s | 0.3 | Subtle grain movement |
| Metal_Thin | Metal rattle | 3.0 m/s | 0.3 | Metallic resonance (sprint only) |

### Audio State Machine

```
IDLE (no loop playing)
  → Player moving on loopable surface + speed > threshold
  → START: fade in loop over 0.2s

PLAYING (loop active, volume tracks speed)
  → Speed drops below threshold
  → FADE_OUT: fade over 0.5s → IDLE

  → Surface changes to different loopable surface
  → CROSSFADE: fade old 0.3s, start new simultaneously

  → Surface changes to non-loopable
  → FADE_OUT → IDLE

  → Player leaves ground (airborne)
  → FADE_OUT → IDLE
```

---

## Ability Ground Effects

Enemy abilities with ground-targeted AOEs produce persistent surface modifications:

### GroundEffectType Enum

```csharp
public enum GroundEffectType : byte
{
    None = 0,
    FireScorch = 1,       // DamageType.Fire → charred decal + ember particles
    IcePatch = 2,         // DamageType.Ice → frost decal + crystal particles
    PoisonPuddle = 3,     // DamageType.Poison → puddle decal + noxious cloud
    LightningScorch = 4,  // DamageType.Lightning → scorch mark + arcing sparks
    HolyGlow = 5,         // DamageType.Holy → golden ring decal + light motes
    ShadowPool = 6,       // DamageType.Shadow → dark pool decal + tendrils
    ArcaneBurn = 7        // DamageType.Arcane → arcane rune decal + energy wisps
}
```

### Effect Lifecycle

```
Ability enters Active phase (AbilityExecutionState.Phase == Active)
  → AbilityGroundEffectSystem reads AbilityDefinition.DamageType
  → Maps DamageType → GroundEffectType
  → Resolves GroundEffectLibrary SO → DecalData + VFX prefab + duration
  → Spawns persistent decal via DecalManager.SpawnDecal(data, pos, rot, lifetimeOverride)
  → Spawns lingering VFX via VFXManager (fire embers, frost crystals, etc.)
  → Decal fades naturally via DecalManager lifetime system
```

---

## Vehicle/Mount Surface Interactions

When `MountState.IsMounted == true`, the mount produces surface-specific effects:

| Effect | Trigger | Surface Dependency |
|--------|---------|-------------------|
| Track decals | Every N meters traveled | All surfaces — different track textures per mount type |
| Skid marks | Sudden deceleration > threshold | Hard surfaces (Concrete, Metal, Stone, Ice) |
| Surface spray | Speed > spray threshold | Soft surfaces (Dirt, Mud, Sand, Gravel, Snow) |
| Dust trail | Sustained high speed | Dry surfaces (Dirt, Sand, Gravel) |

---

## Haptic Feedback

Per-surface haptic profiles drive controller vibration on nearby impacts:

| Surface | Footstep Haptic | Impact Haptic | Character |
|---------|----------------|---------------|-----------|
| Concrete | 0.3, 0.05s | 0.7, 0.1s | Sharp, solid |
| Metal | 0.4, 0.08s | 0.8, 0.15s | Bright, ringy |
| Wood | 0.2, 0.04s | 0.5, 0.08s | Warm thud |
| Sand | 0.1, 0.06s | 0.3, 0.06s | Soft, wide |
| Ice | 0.3, 0.03s | 0.6, 0.1s | Crisp crack |
| Water | 0.15, 0.08s | 0.4, 0.1s | Wet slap |
| Glass | 0.2, 0.05s | 0.9, 0.2s | Sharp shatter |
| Flesh | 0.1, 0.04s | 0.6, 0.1s | Wet thump |

Haptic intensity is multiplied by `MotionIntensitySettings.GlobalIntensity` (0=disabled, 1=normal, 2=exaggerated) and gated by `ParadigmSurfaceProfile` (disabled for MOBA, reduced for ARPG).

---

## Accessibility & Platform Scaling

### MotionIntensitySettings

Global singleton shared with EPIC 15.25 (Procedural Motion Layer):

| Field | Type | Default | Range | Effect |
|-------|------|---------|-------|--------|
| GlobalIntensity | float | 1.0 | 0.0–2.0 | Multiplier on all VFX/audio/shake/haptics output |

- **0.0**: All surface effects disabled (motion sensitivity accommodation)
- **1.0**: Normal intensity
- **2.0**: Exaggerated effects (hearing impaired accommodation — stronger visual/haptic feedback)

### Platform Tier Scaling

| Tier | Detection | Particle | Decal | Audio | Haptic |
|------|-----------|----------|-------|-------|--------|
| PC | Default | Full | Full | Full | Full |
| Console | `SystemInfo.graphicsDeviceType` | 75% emission | Full | Full | Full |
| Mobile | `Application.isMobilePlatform` | Billboard-only | Skip | 2D only | None |

Platform tier auto-detected in `MotionIntensitySettings.Awake()`. Applied by demoting `EffectLODTier` values on lower platforms.

---

## Existing Systems: Keep vs Replace vs Modify

| System | Action | Status | Reason |
|--------|--------|--------|--------|
| `VFXManager` | **Keep** | Done | Solid per-prefab queue pooling with throttling + distance culling |
| `DecalManager` | **Keep** | Done | Efficient URP DecalProjector ring-buffer with GPU fade |
| `EffectPoolManager` | **Keep** | Done | Unity ObjectPool for muzzle/shell/tracer (weapon pipeline) |
| `AudioManager` | **Keep** | Done | One-shot pool with pitch/volume variance + no-repeat |
| `SurfaceMaterialRegistry` | **Keep** | Done | Managed-side audio/VFX asset registry |
| `SurfaceMaterial` SOs | **Modify** | Done (P1-6), Planned (P8,11) | Add continuous audio + haptic fields |
| `SurfaceDetectionService` | **Keep** | Done | 5-tier resolution chain for legacy compatibility |
| `CameraShakeEffect` | **Keep** | Done | Feature-complete trauma system |
| `GameplayFeedbackManager` | **Keep** | Done | FEEL integration for haptics + feedback |
| `SurfaceManager` | **Modified** | Done | Routes SpawnEffect through SurfaceImpactQueue |
| `SurfaceImpactPresenterSystem` | **Modify** | Done (P1-6), Planned (P7,11,12) | Add paradigm multipliers, intensity scaling, profilers |

---

## Implementation Phases

### Phase 1: Consolidation [COMPLETE]

Unified the 4 competing impact paths into one pipeline via static queue bridge.

- [x] Created `SurfaceImpactData` struct + `EnvironmentHitRequest` IComponentData
- [x] Created `SurfaceImpactQueue` static queue bridge (matches DamageVisualQueue pattern)
- [x] Created `SurfaceImpactPresenterSystem` — unified consumer in PresentationSystemGroup
- [x] Created `HitscanImpactBridgeSystem` — bridges EnvironmentHitRequest + PendingCombatHit to queue
- [x] Created `SurfaceIdResolver` — maps SurfaceMaterial SO → SurfaceID
- [x] Created `ImpactClassResolver` — maps WeaponCategory/damage → ImpactClass
- [x] Created `SurfaceDatabaseBlob` + `SurfaceDatabaseSingleton` — Burst-safe surface lookup
- [x] Created `SurfaceDatabaseInitSystem` — builds BlobAsset from SurfaceMaterialRegistry

**Architecture note:** The original plan proposed separate `ImpactVFXPresenterSystem`, `ImpactDecalPresenterSystem`, and `ImpactAudioPresenterSystem`. Implementation consolidated these into a single `SurfaceImpactPresenterSystem` for simpler LOD coordination (see Decision 2).

**Architecture note:** The original plan proposed `SurfaceImpactEvent` as a ghost-replicated `IBufferElementData`. Implementation used a static queue instead to comply with the MEMORY.md constraint (see Decision 1).

### Phase 2: Impact Quality [COMPLETE]

Added physical realism to impacts via scaling tables and reflection vectors.

- [x] Reflection vector particles — bullet velocity reflected along surface normal
- [x] ImpactClass scaling tables — `ParticleScales[10]`, `DecalScales[10]`, `CameraShakeAmounts[10]`
- [x] Camera shake integration — distance-attenuated trauma via `CameraShakeEffect.TriggerTrauma()`
- [x] Decal improvements — normal-aligned with random rotation (0-360°)
- [x] LOD-aware emission — Reduced tier halves particle emission rate

### Phase 3: Ricochet & Penetration [COMPLETE]

Physics-driven bullet behavior for ricochets and surface penetration.

- [x] `RicochetPenetrationSystem` — reads EnvironmentHitRequest before bridge destroys them
- [x] Incident angle vs hardness threshold for ricochet detection
- [x] Reflected velocity at 60% power for ricochet sparks
- [x] Penetration power vs surface density for through-shot detection
- [x] Exit-side dust puff at 0.15m penetration depth assumption
- [x] UpdateBefore(HitscanImpactBridgeSystem) ordering

### Phase 4: Environmental Effects [COMPLETE]

Surface interactions beyond bullet impacts.

- [x] `FootprintDecalSpawnerSystem` — surface-specific footprint decals with varying lifetimes
- [x] `WaterInteractionSystem` — footstep splash replacement on liquid surfaces
- [x] `BodyFallImpactSystem` — corpse ragdoll dust puff (one-shot via BodyFallTriggered tag)
- [x] Water audio muting — 50% volume for plunk audio on liquid surfaces

### Phase 5: LOD & Optimization [COMPLETE]

Performance scaling for large battles.

- [x] LOD tier computation — Full/Reduced/Minimal/Culled based on camera distance
- [x] Frame budget enforcement — max 32 events per frame, overflow queues to next frame
- [x] BlobAsset surface database — O(1) integer-indexed lookup, 480 bytes total

### Phase 6: Polish & Feel [COMPLETE]

Final quality pass for game feel.

- [x] `ScreenDirtSystem` — fullscreen overlay on nearby Explosion_Large (5m radius, 1s cooldown)
- [x] `ScreenDirtOverlay` — CanvasGroup with linear alpha fade over 2s
- [x] Decal clustering — skip if 3+ decals within 0.2m (LRU position cache, max 100 entries)
- [x] Audio occlusion — LOS raycast reduces volume to 30% for impacts behind walls
- [x] Wind-affected particles — force over lifetime on particles with startLifetime > 0.5s

---

### Phase 7: Paradigm-Adaptive Surface FX [NEW]

Auto-scale all surface effects per InputParadigm. Isometric cameras need larger decals visible from farther away, top-down cameras don't need screen dirt, etc.

- [ ] **Create `ParadigmSurfaceProfile` ScriptableObject**
  - Per-paradigm effect multipliers: LOD, particle scale, decal scale, camera shake
  - Feature toggles: screen dirt, footprints, audio occlusion
  - Audio spatial blend (1.0=3D for FPS, 0.3=mostly-2D for top-down)
  - Performance tuning: max events/frame, distance culling multiplier
  - `Assets/Scripts/Surface/Config/ParadigmSurfaceProfile.cs`

- [ ] **Create `ParadigmSurfaceConfig` MonoBehaviour singleton**
  - Holds array of `ParadigmSurfaceProfile` references (one per paradigm)
  - Subscribes to `ParadigmStateMachine.Instance.OnParadigmChanged` in `Start()`
  - Caches `ActiveProfile` for O(1) access
  - Provides default profile when no match found
  - `Assets/Scripts/Surface/Config/ParadigmSurfaceConfig.cs`

- [ ] **Modify `SurfaceImpactPresenterSystem`**
  - Add `ParadigmSurfaceConfig _paradigmConfig` field, lazy-loaded in `EnsureDependencies()`
  - Replace hardcoded `LOD_Full/Reduced/Minimal` with paradigm-multiplied values
  - Multiply `ParticleScales`, `DecalScales`, `CameraShakeAmounts` by paradigm multipliers
  - Gate screen dirt trigger on `ActiveProfile.ScreenDirtEnabled`
  - Replace hardcoded `MaxEventsPerFrame` with `ActiveProfile.MaxEventsPerFrame`
  - Gate audio occlusion on `ActiveProfile.AudioOcclusionEnabled`

- [ ] **Create 6 paradigm profile assets**
  - Shooter (1.0x everything, all features enabled)
  - MMO (1.2x decals, 0.7x shake, all features enabled)
  - ARPG (2.0x decals, 1.5x particles, 0.3x shake, no screen dirt)
  - MOBA (2.0x decals, 0.2x shake, no screen dirt/footprints/audio occlusion)
  - TwinStick (1.5x decals, 0.6x shake, no screen dirt)
  - SideScroller (0.8x particles, 0.5x shake, no screen dirt/audio occlusion)

### Phase 8: Continuous Surface Audio [NEW]

Persistent looping audio when the player moves across specific surfaces.

- [ ] **Create `SurfaceAudioLoopManager` MonoBehaviour singleton**
  - Pool of 4 looping `AudioSource` components (separate from AudioManager one-shot pool)
  - API: `StartLoop(SurfaceID, AudioClip, volume)`, `StopLoop(SurfaceID, fadeOut)`, `UpdateLoopVolume(SurfaceID, volume)`
  - Crossfade support: fade old source while ramping new simultaneously
  - `SetSpatialBlend(float)` for paradigm adaptation (Phase 7 Audio3DBlend)
  - `Assets/Scripts/Surface/Audio/SurfaceAudioLoopManager.cs`

- [ ] **Create `SurfaceAudioLoopConfig` ScriptableObject**
  - Per-surface loop configuration: clip, speed threshold, max volume
  - Fallback defaults for unconfigured surfaces
  - `Assets/Scripts/Surface/Audio/SurfaceAudioLoopConfig.cs`

- [ ] **Create `SurfaceContactAudioSystem` SystemBase**
  - `PresentationSystemGroup`, `ClientSimulation | LocalSimulation`
  - Reads local player velocity + current surface (from last FootstepEvent or downward raycast)
  - State machine: IDLE → PLAYING → CROSSFADE → FADE_OUT
  - Volume = lerp(0, maxVol, speed / maxSpeed)
  - Surface change triggers crossfade (0.3s)
  - Stopped/airborne triggers fade out (0.5s)
  - `Assets/Scripts/Surface/Systems/SurfaceContactAudioSystem.cs`

- [ ] **Modify `SurfaceMaterial` ScriptableObject**
  - Add `AudioClip ContinuousLoopClip` field
  - Add `float LoopSpeedThreshold = 1f` field
  - Add `float LoopVolumeAtMaxSpeed = 0.6f` field

### Phase 9: Ability Ground Effects [NEW]

AOE abilities leave persistent surface modifications that linger and fade.

- [ ] **Create `GroundEffectData` components**
  - `GroundEffectType` enum (8 types: None through ArcaneBurn)
  - `GroundEffectRequest` struct (position, radius, type, duration, fade time)
  - `GroundEffectQueue` static queue (same pattern as SurfaceImpactQueue)
  - `Assets/Scripts/Surface/Components/GroundEffectData.cs`

- [ ] **Create `AbilityGroundEffectSystem` SystemBase**
  - `PresentationSystemGroup`, `UpdateAfter(SurfaceImpactPresenterSystem)`, Client|Local
  - Drains `GroundEffectQueue` (populated by ability systems externally)
  - Also queries `AbilityExecutionState` entities transitioning to Active phase
  - Maps `DamageType` → `GroundEffectType` (Fire→FireScorch, Ice→IcePatch, etc.)
  - Spawns persistent decals via `DecalManager.SpawnDecal()` with ability duration as lifetime
  - Spawns lingering VFX via `VFXManager` (fire embers, frost crystals, poison clouds)
  - `Assets/Scripts/Surface/Systems/AbilityGroundEffectSystem.cs`

- [ ] **Create `GroundEffectLibrary` ScriptableObject**
  - Maps `GroundEffectType` → `DecalData` + VFX prefab + default duration + fade time
  - Loaded from Resources on first access
  - `Assets/Scripts/Surface/Config/GroundEffectLibrary.cs`

### Phase 10: Vehicle/Mount Surface Interactions [NEW]

Mounted entities produce tire tracks, hoof prints, skid marks, and surface spray.

- [ ] **Create `MountSurfaceEffectSystem` SystemBase**
  - `PresentationSystemGroup`, `UpdateAfter(SurfaceImpactPresenterSystem)`, Client|Local
  - Queries entities with `MountState.IsMounted == true` + `LocalTransform`
  - Tracks accumulated distance per entity; spawns track decal every `TrackSpacing` meters
  - Detects sudden deceleration → spawns elongated skid mark decal
  - High speed + soft surface (Dirt/Mud/Sand/Gravel/Snow) → enqueues spray VFX to `SurfaceImpactQueue`
  - Surface resolved via downward raycast → `SurfaceDetectionService`
  - `Assets/Scripts/Surface/Systems/MountSurfaceEffectSystem.cs`

- [ ] **Create `MountSurfaceEffectConfig` ScriptableObject**
  - Per-mount type: track DecalData, spray VFX prefab, skid threshold, track spacing
  - `Assets/Scripts/Surface/Config/MountSurfaceEffectConfig.cs`

### Phase 11: Haptic Feedback & Accessibility [NEW]

Per-surface haptic profiles, global motion intensity, and platform scaling.

- [ ] **Create `MotionIntensitySettings` MonoBehaviour singleton**
  - `float GlobalIntensity` (0-2 range, default 1.0) — user-facing accessibility slider
  - `PlatformTier CurrentTier` — auto-detected from `SystemInfo` / `Application.isMobilePlatform`
  - `ApplyPlatformScaling(ref EffectLODTier)` — demotes LOD on lower platforms
  - Shared with EPIC 15.25 Procedural Motion Layer
  - `Assets/Scripts/Core/Settings/MotionIntensitySettings.cs`

- [ ] **Create `SurfaceHapticProfile` struct**
  - `float HapticIntensity`, `float HapticDuration` — per-surface haptic weights
  - `Assets/Scripts/Surface/Config/SurfaceHapticProfile.cs`

- [ ] **Create `SurfaceHapticBridgeSystem` SystemBase**
  - `PresentationSystemGroup`, `UpdateAfter(SurfaceImpactPresenterSystem)`, Client|Local
  - Reads recently-processed impacts from `SurfaceImpactPresenterSystem.RecentImpacts` static list
  - For each impact within 3m of local player camera:
    - Resolve haptic profile from `SurfaceMaterial.HapticIntensity/HapticDuration`
    - Scale by ImpactClass weight + distance attenuation + `MotionIntensitySettings.GlobalIntensity`
    - Trigger `GameplayFeedbackManager.Instance.OnDamage(scaledIntensity)`
  - `Assets/Scripts/Surface/Systems/SurfaceHapticBridgeSystem.cs`

- [ ] **Modify `SurfaceMaterial` ScriptableObject**
  - Add `float HapticIntensity = 0.5f` field
  - Add `float HapticDuration = 0.1f` field

- [ ] **Modify `SurfaceImpactPresenterSystem`**
  - Add `static List<SurfaceImpactData> RecentImpacts` — populated during ProcessImpact, cleared at start of OnUpdate
  - Apply `MotionIntensitySettings.Instance.GlobalIntensity` as multiplier to all VFX scales, audio volumes, camera shake amounts

### Phase 12: Debug & Profiling [NEW]

Developer tools for tuning and profiling the surface FX pipeline.

- [ ] **Create `SurfaceFXProfiler` static class**
  - `ProfilerMarker` instances: `Surface.ImpactPresenter`, `Surface.RicochetPenetration`, `Surface.DecalCluster`
  - Static counters: `EventsThisFrame`, `EventsThisSecond`, `QueueOverflowCount`, `DecalPoolUtilization`
  - Reset counters on domain reload via `[RuntimeInitializeOnLoadMethod]`
  - `Assets/Scripts/Surface/Debug/SurfaceFXProfiler.cs`

- [ ] **Create `SurfaceDebugOverlay` MonoBehaviour**
  - Toggle-able HUD (F9 key or debug menu) showing:
    - Events processed this frame / max
    - Queue depth
    - Active decals / max
    - Active particles
    - Current paradigm profile name
    - Current LOD multipliers
  - `Assets/Scripts/Surface/Debug/SurfaceDebugOverlay.cs`

- [ ] **Create `SurfaceFXDebugWindow` EditorWindow**
  - Real-time stats in Editor play mode
  - Event throughput graph
  - Pool health (decals, particles, audio voices)
  - Surface ID distribution pie chart
  - `Assets/Editor/Surface/SurfaceFXDebugWindow.cs`

- [ ] **Modify `SurfaceImpactPresenterSystem`**
  - Wrap `OnUpdate()` with `SurfaceFXProfiler.ImpactPresenterMarker.Begin/End()`
  - Increment `SurfaceFXProfiler.EventsThisFrame` per processed event

- [ ] **Modify `RicochetPenetrationSystem`**
  - Wrap `OnUpdate()` with `SurfaceFXProfiler.RicochetMarker.Begin/End()`

---

## Key Files

### Existing (Phase 1-6)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Surface/Components/SurfaceComponents.cs` | SurfaceID, ImpactClass, EffectLODTier enums + SurfaceImpactData struct + EnvironmentHitRequest |
| `Assets/Scripts/Surface/SurfaceImpactQueue.cs` | Static queue bridge (Enqueue/TryDequeue/Clear/Count) |
| `Assets/Scripts/Surface/Data/SurfaceDatabaseBlob.cs` | BlobAsset struct: SurfaceDatabaseBlob, SurfaceEntry, SurfaceDatabaseSingleton |
| `Assets/Scripts/Surface/Data/SurfaceDatabaseInitSystem.cs` | Builds BlobAsset from SurfaceMaterialRegistry at world init |
| `Assets/Scripts/Surface/Systems/SurfaceImpactPresenterSystem.cs` | Unified consumer: dequeues, LOD, VFX/Decal/Audio/Shake |
| `Assets/Scripts/Surface/Systems/HitscanImpactBridgeSystem.cs` | EnvironmentHitRequest + PendingCombatHit → SurfaceImpactQueue |
| `Assets/Scripts/Surface/Systems/RicochetPenetrationSystem.cs` | Ricochet/penetration detection and VFX enqueue |
| `Assets/Scripts/Surface/Systems/WaterInteractionSystem.cs` | Footstep splash replacement on liquid surfaces |
| `Assets/Scripts/Surface/Systems/FootprintDecalSpawnerSystem.cs` | Surface-specific footprint decals with lifetime |
| `Assets/Scripts/Surface/Systems/BodyFallImpactSystem.cs` | Corpse ragdoll dust puff (one-shot) |
| `Assets/Scripts/Surface/Systems/ScreenDirtSystem.cs` | Explosion proximity screen overlay + ScreenDirtTrigger + ScreenDirtOverlay |
| `Assets/Scripts/Surface/Systems/SurfaceIdResolver.cs` | SurfaceMaterial SO → SurfaceID (explicit + name heuristic + legacy enum) |
| `Assets/Scripts/Surface/Systems/ImpactClassResolver.cs` | WeaponCategory / ImpactType / damage → ImpactClass |

### New (Phase 7-12)

| File | Phase | Purpose |
|------|-------|---------|
| `Assets/Scripts/Surface/Config/ParadigmSurfaceProfile.cs` | 7 | SO: per-paradigm effect multipliers + feature toggles |
| `Assets/Scripts/Surface/Config/ParadigmSurfaceConfig.cs` | 7 | Singleton: caches active paradigm profile |
| `Assets/Scripts/Surface/Audio/SurfaceAudioLoopManager.cs` | 8 | Singleton: pool of 4 looping AudioSources with crossfade |
| `Assets/Scripts/Surface/Audio/SurfaceAudioLoopConfig.cs` | 8 | SO: per-surface loop clip + threshold + volume |
| `Assets/Scripts/Surface/Systems/SurfaceContactAudioSystem.cs` | 8 | SystemBase: velocity + surface → loop start/stop/crossfade |
| `Assets/Scripts/Surface/Components/GroundEffectData.cs` | 9 | GroundEffectType enum + GroundEffectRequest + GroundEffectQueue |
| `Assets/Scripts/Surface/Systems/AbilityGroundEffectSystem.cs` | 9 | SystemBase: ability AOE → persistent ground decals + VFX |
| `Assets/Scripts/Surface/Config/GroundEffectLibrary.cs` | 9 | SO: GroundEffectType → DecalData + VFX + duration |
| `Assets/Scripts/Surface/Systems/MountSurfaceEffectSystem.cs` | 10 | SystemBase: mount movement → tracks + spray + skid marks |
| `Assets/Scripts/Surface/Config/MountSurfaceEffectConfig.cs` | 10 | SO: per-mount track/spray/skid configuration |
| `Assets/Scripts/Surface/Systems/SurfaceHapticBridgeSystem.cs` | 11 | SystemBase: nearby impacts → haptic feedback |
| `Assets/Scripts/Core/Settings/MotionIntensitySettings.cs` | 11 | Singleton: global intensity slider + platform tier |
| `Assets/Scripts/Surface/Config/SurfaceHapticProfile.cs` | 11 | Struct: per-surface haptic intensity + duration |
| `Assets/Scripts/Surface/Debug/SurfaceFXProfiler.cs` | 12 | Static: ProfilerMarkers + counters |
| `Assets/Scripts/Surface/Debug/SurfaceDebugOverlay.cs` | 12 | MonoBehaviour: toggle-able stats HUD |
| `Assets/Editor/Surface/SurfaceFXDebugWindow.cs` | 12 | EditorWindow: real-time pool/event stats |

### Modified (Phase 7-12)

| File | Phases | Change |
|------|--------|--------|
| `Assets/Scripts/Surface/Systems/SurfaceImpactPresenterSystem.cs` | 7, 11, 12 | Paradigm multipliers, intensity scaling, RecentImpacts list, profiler markers |
| `Assets/Scripts/Audio/SurfaceMaterial.cs` | 8, 11 | Continuous loop clip fields + haptic intensity/duration fields |
| `Assets/Scripts/Surface/Systems/RicochetPenetrationSystem.cs` | 12 | Profiler marker |

---

## Verification Checklist

### Phase 1-6 (Existing)

| # | Test | Expected Result |
|---|------|-----------------|
| 1 | Shoot concrete wall | Dust puff + chip decal + ricochet sparks at grazing angle |
| 2 | Shoot metal surface | Sparks + metallic ping + bullet hole decal |
| 3 | Shoot wood surface | Splinter burst + thud + hole decal |
| 4 | Shoot glass | Shatter VFX + tinkle + bullet passes through |
| 5 | Shoot water | Splash column + muted plunk + no decal |
| 6 | Shoot enemy | Blood spray + wet impact + damage numbers |
| 7 | Shotgun vs pistol on same surface | Shotgun has larger decal, more particles, camera bump |
| 8 | Walk on snow | Footprints appear, fade after 60s |
| 9 | Walk through mud | Dark footprints + squelch audio |
| 10 | Explosion near player | Screen dirt + heavy camera shake + large crater decal |
| 11 | Kill enemy (ragdoll falls) | Dust puff on body impact + thud audio |
| 12 | Impact at 50m distance | Reduced particles, no decal, 2D audio only |
| 13 | Impact at 70m distance | No VFX spawned (culled) |
| 14 | 50+ simultaneous impacts | Stable framerate, max 32 processed, oldest recycled |
| 15 | 3+ impacts in same spot | Decal clustering skips after 3rd |
| 16 | Impact behind wall | Audio volume reduced to 30% (occlusion) |

### Phase 7 (Paradigm Adaptation)

| # | Test | Expected Result |
|---|------|-----------------|
| 17 | Switch to ARPG paradigm | Decals 2x size, particles 1.5x, shake at 30%, no screen dirt |
| 18 | Switch to MOBA paradigm | No footprints, no screen dirt, no audio occlusion, shake at 20% |
| 19 | Switch to Shooter paradigm | All effects at 1.0x, all features enabled |
| 20 | Switch to SideScroller | Audio fully 2D (spatialBlend=0), no screen dirt |
| 21 | ARPG explosion at 25m | Still Full LOD (30m ARPG threshold vs 15m Shooter) |

### Phase 8 (Continuous Audio)

| # | Test | Expected Result |
|---|------|-----------------|
| 22 | Walk on ice | Continuous crackle loop fades in, volume tracks speed |
| 23 | Stop on ice | Crackle fades out over 0.5s |
| 24 | Walk from ice to gravel | Crackle crossfades to crunch over 0.3s |
| 25 | Jump while on ice | Crackle fades out immediately |
| 26 | Walk on concrete (no loop clip) | No continuous audio, only footstep events |

### Phase 9 (Ability Ground Effects)

| # | Test | Expected Result |
|---|------|-----------------|
| 27 | Enemy casts fire AOE | Scorch decal appears at target, fire embers VFX lingers |
| 28 | Fire AOE expires | Decal fades naturally via DecalManager lifetime |
| 29 | Enemy casts ice AOE | Frost decal + crystal particle VFX |
| 30 | Enemy casts poison AOE | Puddle decal + noxious cloud VFX |

### Phase 10 (Vehicle/Mount)

| # | Test | Expected Result |
|---|------|-----------------|
| 31 | Mount vehicle, drive on dirt | Tire track decals behind every N meters |
| 32 | Sudden brake on concrete | Elongated skid mark decal |
| 33 | High speed on sand | Dust spray VFX behind vehicle |
| 34 | Drive on grass (no spray) | Track decals only, no spray |

### Phase 11 (Haptics & Accessibility)

| # | Test | Expected Result |
|---|------|-----------------|
| 35 | Explosion within 3m | Controller rumble proportional to proximity |
| 36 | Set intensity slider to 0 | All VFX/audio/shake/haptics disabled |
| 37 | Set intensity slider to 2 | All effects at 2x intensity |
| 38 | Bullet impact on metal near player | Short, bright haptic pulse |
| 39 | Bullet impact on sand near player | Soft, wide haptic pulse |

### Phase 12 (Debug)

| # | Test | Expected Result |
|---|------|-----------------|
| 40 | Open Unity Profiler during combat | `Surface.ImpactPresenter` marker visible with timing |
| 41 | Toggle debug overlay | HUD shows events/frame, queue depth, paradigm name |
| 42 | Heavy combat (50+ impacts/s) | Overlay shows queue overflow count > 0 |

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| SurfaceImpactPresenterSystem becomes too large with Phase 7+11+12 additions | Medium | Extract paradigm multiplier logic into helper method. System stays single consumer but delegates scaling |
| Continuous audio crossfade timing feels abrupt on fast surface transitions | Low | Configurable crossfade duration (default 0.3s). Increase for smoother transitions |
| AbilityGroundEffectSystem can't read AbilityExecutionState on remote clients (server-authoritative) | Medium | On remote clients, derive ground effects from replicated telegraph entities instead of execution state |
| MotionIntensitySettings shared with EPIC 15.25 creates coupling | Low | Place in `Core/Settings/` namespace. First EPIC to implement creates it, second consumes without modification |
| DecalManager ring-buffer overflow with persistent ground effect decals | Medium | Ground effects use separate lifetime parameter but share pool. Monitor pool utilization via Phase 12 telemetry |
| Platform tier detection may be wrong on some devices | Low | Allow manual override via `MotionIntensitySettings` Inspector field |
| Haptic feedback not available on keyboard/mouse | None | `GameplayFeedbackManager.SetHapticsEnabled()` already handles this. Haptic bridge is a no-op when disabled |

---

## Best Practices

1. **Always test paradigm switching** — Switch between Shooter and ARPG during gameplay to verify effect scaling. Decals should visibly grow/shrink.
2. **Profile regularly** — Check `Surface.ImpactPresenter` in the Unity Profiler after changing enemy counts or adding new impact sources.
3. **Set SurfaceID explicitly** — Don't rely on name heuristics. Set `SurfaceMaterial.SurfaceId` in the Inspector for predictable resolution.
4. **Use the frame budget** — If impacts feel delayed, check the debug overlay for queue overflow. Increase `MaxEventsPerFrame` in the paradigm profile or reduce enemy density.
5. **Test continuous audio transitions** — Walk between surfaces rapidly to verify crossfade smoothness. Adjust `SurfaceAudioLoopManager` crossfade duration if needed.
6. **Keep ground effect durations reasonable** — Persistent decals consume DecalManager pool slots. Don't set ability durations beyond 30s.
7. **Test accessibility extremes** — Set intensity slider to 0 and 2, verify all channels respect it. Motion-sensitive players should see zero screen effects at 0.
8. **Match haptic profiles to surface character** — Metal should feel bright and ringy, sand should feel soft and wide. Test with actual controller.
9. **Use debug overlay during content creation** — Toggle the HUD when placing new surfaces to verify SurfaceID resolution and effect coverage.
10. **Don't over-configure paradigm profiles** — Start with the default multiplier tables, tune only what feels wrong. Most paradigms work well with the defaults.
11. **Test mount effects on multiple surfaces** — Drive across surface boundaries to verify track decal style changes and spray VFX toggles.
12. **Profile on target platform** — Mobile tier scaling demotes LOD aggressively. Verify that effects still feel impactful at Reduced/Minimal tiers.

---

## Troubleshooting

| Issue | Check |
|-------|-------|
| No impact VFX spawning | Verify `VFXManager.Instance` is not null. Check `SurfaceMaterial.VFXPrefab` is assigned. Check LOD tier (may be Culled at distance) |
| No impact audio | Verify `AudioManager` exists in scene. Check `SurfaceMaterialRegistry` loaded from Resources. Check `SurfaceMaterial.ImpactClips` populated |
| No decals spawning | Verify `DecalManager.Instance` not null. Check `SurfaceMaterial.ImpactDecal` assigned. Check clustering (may be skipping saturated area) |
| Screen dirt not appearing | Check paradigm profile `ScreenDirtEnabled`. Verify `ScreenDirtOverlay` MonoBehaviour in scene. Check explosion distance < 5m |
| Footprints not appearing | Check `SurfaceMaterial.AllowFootprints = true`. Verify `FootprintDecal` assigned. Check paradigm profile `FootprintsEnabled` |
| Effects too small in isometric view | Check `ParadigmSurfaceConfig.ActiveProfile`. ARPG/MOBA should have DecalScaleMultiplier ≥ 2.0 |
| Camera shake too strong in top-down | Check paradigm `CameraShakeMultiplier`. MOBA should be 0.2 |
| Continuous audio not playing | Verify `SurfaceMaterial.ContinuousLoopClip` assigned. Check player speed > `LoopSpeedThreshold`. Verify `SurfaceAudioLoopManager` in scene |
| Audio sounds flat/non-spatial | Check paradigm `Audio3DBlend`. FPS should be 1.0 (full 3D). Verify AudioSource spatialBlend setting |
| Ground effect decals not fading | Verify `DecalManager` lifetime system working. Check `GroundEffectLibrary` duration value |
| No haptic feedback on impact | Check `GameplayFeedbackManager.SetHapticsEnabled(true)`. Verify controller connected. Check paradigm haptic setting |
| Effects disabled entirely | Check `MotionIntensitySettings.GlobalIntensity` — may be set to 0. Check platform tier — Mobile skips most effects |
| Ricochet sparks not appearing | Check `SurfaceMaterial.AllowsRicochet = true`. Verify incident angle exceeds threshold (surface may be too soft) |
| Penetration not working | Check `SurfaceMaterial.AllowsPenetration = true`. Verify bullet velocity > surface density |
| Queue overflow during heavy combat | Normal behavior — frame budget caps processing. Events process next frame. Increase `MaxEventsPerFrame` in paradigm profile if needed |
| Profiler markers not visible | Ensure `SurfaceFXProfiler` static initializer ran. Check profiler is recording the correct thread |
| Debug overlay not toggling | Verify `SurfaceDebugOverlay` MonoBehaviour in scene. Check input binding (F9 default) |
