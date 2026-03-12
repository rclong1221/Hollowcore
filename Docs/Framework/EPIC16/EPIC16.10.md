# EPIC 16.10: Surface Material Gameplay Integration

**Status:** **IMPLEMENTED**
**Priority:** High (Core Gameplay Systems)
**Dependencies:**
- `SurfaceMaterial` ScriptableObject (existing -- `Audio.Systems`)
- `SurfaceMaterialId` IComponentData (existing -- baked by `SurfaceMaterialAuthoring`)
- `SurfaceMaterialRegistry` loaded from Resources (existing -- `TryGetById()`)
- `SurfaceID` enum (existing -- `DIG.Surface.SurfaceComponents`)
- `PlayerGroundCheckSystem` (existing -- already reads `SurfaceMaterialId` from ground hits)
- `StealthSystem` (existing -- has `surfaceMultiplier = 1.0f` placeholder at line 85)
- `FallDetectionSystem` (existing -- has `SurfaceMaterialId = 0` TODO at line 225)
- `PlayerMovementSystem` / `CharacterControllerSystem` (existing -- movement pipeline)
- `FootstepSystem` (existing -- reads ground surface for audio)
- `Unity.Physics` / `Unity.NetCode`

**Feature:** A unified ground-surface gameplay layer that makes every surface type affect player and NPC behavior -- stealth noise, movement speed, slip/friction, fall damage modifiers, and damage-over-time zones. Builds entirely on existing `SurfaceMaterial` assets and `SurfaceMaterialRegistry` without duplicating audio/VFX/decal pipelines.

**Supersedes:** Loose TODOs in `StealthSystem.cs:84`, `FallDetectionSystem.cs:225`

---

## Problem

DIG has a mature surface material pipeline covering 68+ files for audio, VFX, decals, haptics, and bullet impacts. The `SurfaceMaterial` ScriptableObject already defines physical properties (`Hardness`, `Density`, `IsSlippery`, `FrictionModifier`, `SlideFrictionMultiplier`, `IsLiquid`) but **no gameplay system reads them**.

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `SurfaceMaterial` SO with Hardness, Density, IsSlippery, FrictionModifier | No system applies these to movement or stealth |
| `SurfaceMaterialId` baked on terrain/geometry entities | No centralized ground-surface query for arbitrary entities |
| `PlayerGroundCheckSystem` detects ground surface, writes `SurfaceMaterialId` to player entity | Only runs for players, not NPCs/enemies |
| `StealthSystem` calculates noise with `surfaceMultiplier = 1.0f` placeholder | Surface hardness never feeds into noise |
| `FallDetectionSystem` landing impact with `SurfaceMaterialId = 0` TODO | Landing sound/damage not surface-aware |
| `SurfaceMaterial.IsSlippery` + `SlideFrictionMultiplier` fields exist | No movement system reads them |
| `SurfaceMaterial.FrictionModifier` field exists | Not applied to character controller friction |
| `EnvironmentZone` system for radiation/temperature/oxygen hazards | No surface-driven damage zones (lava, acid, electrified floor) |
| `SurfaceContactAudioSystem` for continuous loop audio on surfaces | No surface-driven movement speed modifiers |

**The gap:** Walking on ice looks and sounds different (via existing audio/VFX) but plays identically to concrete. A player on gravel makes the same noise as one on carpet. Lava floors have no damage-over-time. Mud does not slow you down. Surfaces are cosmetic-only.

---

## Architecture Overview

### Principle: Extend, Don't Duplicate

Every system in this EPIC reads from the **existing** `SurfaceMaterial` SO and `SurfaceMaterialRegistry`. No new surface asset type is created. The only new SO is `SurfaceGameplayConfig`, which maps `SurfaceID` values to gameplay multipliers -- keeping gameplay tuning separate from audio/VFX authoring.

### Data Flow

```
[Scene Geometry with SurfaceMaterialAuthoring]
        |  (bake time)
        v
[SurfaceMaterialId on terrain entities]
        |
        v
[GroundSurfaceQuerySystem] ──> Raycast at QueryInterval ──> GroundSurfaceState on characters
        |
        ├──> [SurfaceMovementModifierSystem] ──> Speed/Friction modifiers on CharacterController
        ├──> [SurfaceStealthModifierSystem] ──> Noise multiplier on PlayerNoiseStatus
        ├──> [SurfaceSlipSystem] ──> Reduced control authority on ice/slippery surfaces
        ├──> [SurfaceFallDamageModifierSystem] ──> Surface-aware landing damage
        |
[SurfaceDamageZoneAuthoring on trigger volumes]
        |  (bake time)
        v
[SurfaceDamageZone on zone entities]
        |
        v
[SurfaceDamageSystem] ──> DOT when entity on matching surface inside zone
        |
        v
[Existing DamageEvent / SimpleDamageApplySystem pipeline]
```

### The SurfaceGameplayConfig Bridge

```
SurfaceMaterial SO          SurfaceGameplayConfig SO
  ├── Hardness ──────┐         ├── NoiseMultiplier ────> StealthSystem
  ├── Density        │         ├── SpeedMultiplier ────> MovementSystem
  ├── IsSlippery ────┤         ├── SlipFactor ─────────> SlipSystem
  ├── FrictionMod ───┤         ├── FallDamageMultiplier > FallDetection
  ├── IsLiquid ──────┘         ├── DamagePerSecond ────> SurfaceDamageSystem
  └── SurfaceId ───────────────┘   (keyed by SurfaceID)
```

The `SurfaceGameplayConfig` SO allows designers to tune gameplay per surface type without modifying `SurfaceMaterial` assets (which are shared with audio/VFX artists). Different game modes (survival, stealth, racing) can use different `SurfaceGameplayConfig` profiles.

---

## Phase 1: Ground Surface Detection (Shared Query System)

### Problem

`PlayerGroundCheckSystem` already detects which `SurfaceMaterialId` the player stands on, but this detection is embedded inside the ground check job and only runs for player entities. NPCs, enemies, vehicles, and any other grounded entity cannot query their surface. Multiple systems (stealth, movement, fall damage) each need surface data but should not each perform their own raycast.

### Components

```csharp
/// <summary>
/// Add to any entity that needs to know what surface it stands on.
/// GroundSurfaceQuerySystem reads this and writes results back.
/// Players, NPCs, enemies, vehicles -- anything that touches the ground.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct GroundSurfaceState : IComponentData
{
    /// <summary>SurfaceMaterial.Id of the ground surface. -1 = unknown/airborne.</summary>
    [GhostField] public int SurfaceMaterialId;

    /// <summary>SurfaceID enum value for fast Burst-friendly switch statements.</summary>
    [GhostField] public SurfaceID SurfaceId;

    /// <summary>Whether the entity is currently on solid ground.</summary>
    [GhostField] public bool IsGrounded;

    /// <summary>Elapsed time since last query (internal -- do not set).</summary>
    public float TimeSinceLastQuery;

    /// <summary>
    /// How often to raycast (seconds). Default 0.25s.
    /// Lower = more responsive but more expensive.
    /// 0 = every frame (use sparingly).
    /// </summary>
    public float QueryInterval;

    /// <summary>Surface hardness cached from SurfaceMaterial (0-255).</summary>
    [GhostField] public byte CachedHardness;

    /// <summary>Surface density cached from SurfaceMaterial (0-255).</summary>
    [GhostField] public byte CachedDensity;

    /// <summary>Cached flags for fast Burst checks without managed lookups.</summary>
    [GhostField] public SurfaceFlags Flags;

    public static GroundSurfaceState Default => new()
    {
        SurfaceMaterialId = -1,
        SurfaceId = SurfaceID.Default,
        IsGrounded = false,
        TimeSinceLastQuery = 0f,
        QueryInterval = 0.25f,
        CachedHardness = 128,
        CachedDensity = 128,
        Flags = SurfaceFlags.None
    };
}

/// <summary>
/// Bit flags for surface properties. Cached on GroundSurfaceState
/// so Burst jobs can branch without managed SurfaceMaterial lookups.
/// </summary>
[System.Flags]
public enum SurfaceFlags : byte
{
    None        = 0,
    IsSlippery  = 1 << 0,
    IsLiquid    = 1 << 1,
    AllowsRicochet   = 1 << 2,
    AllowsPenetration = 1 << 3
}
```

### Systems

#### `GroundSurfaceQuerySystem`

- **Group:** `PredictedFixedStepSimulationSystemGroup`
- **After:** `PlayerGroundCheckSystem` (reuse ground state where available)
- **Filter:** `ServerSimulation | ClientSimulation`
- **Burst:** Yes
- **Query:** All entities with `GroundSurfaceState` + `LocalTransform`

**Logic:**

1. Increment `TimeSinceLastQuery` by `deltaTime`.
2. Skip entity if `TimeSinceLastQuery < QueryInterval`.
3. **Frame-spread:** Only query entity if `(entity.Index + frameCount) % spreadFactor == 0`. Default `spreadFactor = 4`. With 200 entities at 0.25s interval and spread factor 4, worst case is ~12 raycasts/frame.
4. Raycast down from `LocalTransform.Position + (0, 0.2, 0)` to `Position - (0, 1.5, 0)`. Uses `CollisionFilter` that hits environment only (excludes characters).
5. On hit: read `SurfaceMaterialId` from hit entity via `ComponentLookup<SurfaceMaterialId>`.
6. Write `GroundSurfaceState.SurfaceMaterialId`, set `IsGrounded = true`, reset `TimeSinceLastQuery = 0`.
7. On miss: set `SurfaceMaterialId = -1`, `IsGrounded = false`.
8. **Player optimization:** For entities that also have `PlayerState`, read `PlayerState.IsGrounded` instead of raycasting. Read the `SurfaceMaterialId` already written by `PlayerGroundCheckSystem`. Avoids duplicate raycasts for players.

**Cache resolution (managed bridge, runs once per unique surface change):**

A companion managed system `GroundSurfaceCacheSystem` (PresentationSystemGroup) detects when `GroundSurfaceState.SurfaceMaterialId` changes and resolves it against `SurfaceMaterialRegistry` to populate the cached fields (`SurfaceId`, `CachedHardness`, `CachedDensity`, `Flags`). This runs once on change, not every frame.

### Authoring

```csharp
/// <summary>
/// Add to any prefab that needs ground surface detection.
/// Players, NPCs, enemies, vehicles.
/// </summary>
public class GroundSurfaceDetectionAuthoring : MonoBehaviour
{
    [Tooltip("How often to raycast for surface detection (seconds). 0 = every frame.")]
    [Range(0f, 1f)]
    public float QueryInterval = 0.25f;
}

// Baker
public class GroundSurfaceDetectionBaker : Baker<GroundSurfaceDetectionAuthoring>
{
    public override void Bake(GroundSurfaceDetectionAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new GroundSurfaceState
        {
            SurfaceMaterialId = -1,
            SurfaceId = SurfaceID.Default,
            IsGrounded = false,
            TimeSinceLastQuery = 0f,
            QueryInterval = authoring.QueryInterval,
            CachedHardness = 128,
            CachedDensity = 128,
            Flags = SurfaceFlags.None
        });
    }
}
```

### Implementation Tasks

- [x] Create `GroundSurfaceState` IComponentData and `SurfaceFlags` enum in `Assets/Scripts/Surface/Components/`
- [x] Create `GroundSurfaceQuerySystem` (PredictedFixedStepSimulation, Burst, scheduled job with frame-spread)
- [x] Create `GroundSurfaceCacheSystem` (managed, PresentationSystemGroup) for SurfaceMaterialRegistry resolution
- [x] Create `GroundSurfaceDetectionAuthoring` MonoBehaviour + Baker
- [ ] Add `GroundSurfaceDetectionAuthoring` to player prefab (QueryInterval = 0, reuse PlayerGroundCheckSystem data)
- [ ] Add `GroundSurfaceDetectionAuthoring` to BoxingJoe and enemy prefabs (QueryInterval = 0.25)
- [x] **Optimization:** Player path reads from `PlayerGroundCheckSystem` output, skips raycast
- [ ] **Test:** 200 NPC entities, verify < 0.05ms average for GroundSurfaceQuerySystem
- [ ] **Test:** Entity on grass reports SurfaceID.Grass, entity on metal reports SurfaceID.Metal_Thin
- [ ] **Test:** Airborne entity reports SurfaceMaterialId = -1, IsGrounded = false

---

## Phase 2: Surface Gameplay Configuration

### Problem

`SurfaceMaterial` SO is shared between audio artists, VFX artists, and gameplay designers. Gameplay multipliers (noise, speed, damage) should not be embedded in the same asset. Different game modes need different tuning (stealth game: noise matters; survival game: lava damage matters; racing: friction matters).

### ScriptableObject

```csharp
/// <summary>
/// Maps SurfaceID enum values to gameplay modifiers.
/// Loaded from Resources. One active config per game mode.
/// Designers tune this independently of SurfaceMaterial audio/VFX.
/// </summary>
[CreateAssetMenu(menuName = "DIG/Surface/Surface Gameplay Config")]
public class SurfaceGameplayConfig : ScriptableObject
{
    [System.Serializable]
    public struct SurfaceGameplayEntry
    {
        public SurfaceID SurfaceId;

        [Header("Stealth")]
        [Tooltip("Multiplier on noise level. 1.0 = normal. >1 = louder (hard surfaces). <1 = quieter (soft).")]
        [Range(0f, 3f)]
        public float NoiseMultiplier;

        [Header("Movement")]
        [Tooltip("Multiplier on movement speed. 1.0 = normal. <1 = slower (mud). >1 = faster (smooth).")]
        [Range(0.1f, 2f)]
        public float SpeedMultiplier;

        [Tooltip("Slip factor. 0 = full control. 1 = no control (pure ice). Overrides IsSlippery from SurfaceMaterial.")]
        [Range(0f, 1f)]
        public float SlipFactor;

        [Header("Fall Damage")]
        [Tooltip("Multiplier on fall damage. 1.0 = normal. 0.5 = soft landing (sand). 1.5 = hard landing (concrete).")]
        [Range(0f, 3f)]
        public float FallDamageMultiplier;

        [Header("Hazard")]
        [Tooltip("Damage per second when standing on this surface inside a SurfaceDamageZone. 0 = no damage.")]
        public float DamagePerSecond;

        [Tooltip("DamageType for surface DOT (uses Player.Components.DamageType for survival integration).")]
        public Player.Components.DamageType DamageType;
    }

    public List<SurfaceGameplayEntry> Entries = new();

    // Runtime O(1) lookup
    private Dictionary<SurfaceID, SurfaceGameplayEntry> _cache;

    public bool TryGetEntry(SurfaceID id, out SurfaceGameplayEntry entry)
    {
        if (_cache == null) RebuildCache();
        return _cache.TryGetValue(id, out entry);
    }

    public SurfaceGameplayEntry GetEntryOrDefault(SurfaceID id)
    {
        if (TryGetEntry(id, out var entry)) return entry;
        return DefaultEntry;
    }

    public static SurfaceGameplayEntry DefaultEntry => new()
    {
        SurfaceId = SurfaceID.Default,
        NoiseMultiplier = 1.0f,
        SpeedMultiplier = 1.0f,
        SlipFactor = 0f,
        FallDamageMultiplier = 1.0f,
        DamagePerSecond = 0f,
        DamageType = Player.Components.DamageType.Physical
    };

    private void OnEnable() => RebuildCache();
    private void OnValidate() => RebuildCache();

    private void RebuildCache()
    {
        _cache = new Dictionary<SurfaceID, SurfaceGameplayEntry>();
        foreach (var e in Entries)
            _cache[e.SurfaceId] = e;
    }
}
```

### ECS Singleton (Burst-Compatible Cache)

```csharp
/// <summary>
/// BlobAsset containing gameplay modifiers per SurfaceID.
/// Created at runtime from SurfaceGameplayConfig SO.
/// Burst-friendly -- no managed lookups in hot path.
/// </summary>
public struct SurfaceGameplayBlob
{
    /// <summary>
    /// Indexed by (byte)SurfaceID. 24 entries (one per SurfaceID enum value).
    /// </summary>
    public BlobArray<SurfaceGameplayModifiers> Modifiers;
}

public struct SurfaceGameplayModifiers
{
    public float NoiseMultiplier;
    public float SpeedMultiplier;
    public float SlipFactor;
    public float FallDamageMultiplier;
    public float DamagePerSecond;
    public byte DamageType; // cast to Player.Components.DamageType
}

/// <summary>
/// Singleton component referencing the BlobAsset. Created by SurfaceGameplayConfigSystem.
/// </summary>
public struct SurfaceGameplayConfigSingleton : IComponentData
{
    public BlobAssetReference<SurfaceGameplayBlob> Config;
}
```

### Systems

#### `SurfaceGameplayConfigSystem`

- **Group:** `InitializationSystemGroup`
- **Filter:** `ServerSimulation | ClientSimulation`
- **Managed:** Yes (loads SO from Resources, creates BlobAsset)
- Creates `SurfaceGameplayConfigSingleton` on first update
- Builds `BlobAsset<SurfaceGameplayBlob>` from `SurfaceGameplayConfig` SO
- Array indexed by `(byte)SurfaceID` for O(1) Burst-compatible lookup
- Recreates blob if SO changes at runtime (editor hot-reload support)

### Recommended Default Values

| SurfaceID | NoiseMultiplier | SpeedMultiplier | SlipFactor | FallDamageMultiplier | DamagePerSecond |
|-----------|----------------|-----------------|------------|---------------------|-----------------|
| Concrete | 1.3 | 1.0 | 0.0 | 1.2 | 0 |
| Metal_Thin | 1.5 | 1.0 | 0.0 | 1.3 | 0 |
| Metal_Thick | 1.5 | 1.0 | 0.0 | 1.3 | 0 |
| Wood | 1.1 | 1.0 | 0.0 | 1.0 | 0 |
| Dirt | 0.7 | 0.9 | 0.0 | 0.7 | 0 |
| Sand | 0.5 | 0.7 | 0.0 | 0.5 | 0 |
| Grass | 0.6 | 0.95 | 0.0 | 0.6 | 0 |
| Gravel | 1.4 | 0.85 | 0.0 | 0.9 | 0 |
| Snow | 0.4 | 0.8 | 0.15 | 0.4 | 0 |
| Ice | 0.3 | 1.1 | 0.8 | 1.4 | 0 |
| Water | 0.3 | 0.6 | 0.0 | 0.3 | 0 |
| Mud | 0.5 | 0.5 | 0.0 | 0.4 | 0 |
| Glass | 1.6 | 1.0 | 0.1 | 1.5 | 0 |
| Foliage | 0.4 | 0.9 | 0.0 | 0.5 | 0 |
| Rubber | 0.3 | 1.0 | 0.0 | 0.5 | 0 |
| Stone | 1.2 | 1.0 | 0.0 | 1.1 | 0 |

### Implementation Tasks

- [x] Create `SurfaceGameplayConfig` ScriptableObject in `Assets/Scripts/Surface/Config/`
- [x] Create `SurfaceGameplayBlob`, `SurfaceGameplayModifiers`, `SurfaceGameplayConfigSingleton` in `Assets/Scripts/Surface/Components/`
- [x] Create `SurfaceGameplayConfigSystem` (managed, InitializationSystemGroup) to build BlobAsset
- [ ] Create default `SurfaceGameplayConfig` asset in `Assets/Resources/` with recommended values
- [ ] **Test:** BlobAsset lookup returns correct modifiers for each SurfaceID
- [ ] **Test:** Hot-reload in editor: change SO values, verify BlobAsset rebuilds

---

## Phase 3: Surface Stealth Integration

### Problem

`StealthSystem.cs:84` has a TODO: `// TODO: Integrate with SurfaceMaterialId from ground raycast`. The `surfaceMultiplier` is hardcoded to `1.0f`. Walking on gravel should be louder than walking on carpet.

### System Modifications

#### `StealthSystem` (Modify Existing)

**File:** `Assets/Scripts/Player/Systems/StealthSystem.cs`

The system already runs in `PredictedFixedStepSimulationSystemGroup` after `PlayerMovementSystem`, with `ServerSimulation | ClientSimulation` filter. The query must add `RefRO<GroundSurfaceState>` to the existing `foreach`.

**Changes:**

1. Add `GroundSurfaceState` to the query (optional via `WithAll/WithNone` -- if entity lacks it, default to 1.0).
2. Replace `float surfaceMultiplier = 1.0f` with lookup from `SurfaceGameplayConfigSingleton` BlobAsset.
3. Burst-compatible: read `GroundSurfaceState.SurfaceId`, index into `SurfaceGameplayBlob.Modifiers[(byte)surfaceId].NoiseMultiplier`.

**Noise formula (updated):**

```
finalNoise = baseNoise * stanceMultiplier * surfaceMultiplier
```

Where `surfaceMultiplier` comes from `SurfaceGameplayConfig` based on current ground surface:
- Hard surfaces (Metal, Concrete, Glass): 1.3x - 1.6x
- Normal surfaces (Wood, Stone): 1.0x - 1.2x
- Soft surfaces (Grass, Dirt, Sand): 0.5x - 0.7x
- Liquid surfaces (Water, Mud): 0.3x - 0.5x

### NPC Surface Noise (New)

#### `SurfaceStealthModifierSystem`

- **Group:** `SimulationSystemGroup`
- **Filter:** `ServerSimulation | LocalSimulation`
- **Burst:** Yes
- **Query:** Entities with `GroundSurfaceState` + `WithNone<PlayerTag>` (NPCs only)

NPCs with `GroundSurfaceState` emit surface-derived noise that feeds into the hearing detection pipeline. When an NPC walks on gravel, players' `HearingDetectionSystem` receives a louder noise event than when the same NPC walks on grass.

**Logic:**

1. Read `GroundSurfaceState.SurfaceId`.
2. Look up `NoiseMultiplier` from `SurfaceGameplayBlob`.
3. Write a noise modifier to a new `SurfaceNoiseModifier` component on the NPC entity.
4. `HearingDetectionSystem` reads `SurfaceNoiseModifier` when evaluating NPC detectability.

```csharp
/// <summary>
/// Written by SurfaceStealthModifierSystem on NPCs.
/// Read by HearingDetectionSystem to adjust NPC detectability by surface.
/// </summary>
public struct SurfaceNoiseModifier : IComponentData
{
    public float Multiplier; // 1.0 = default, >1 = louder surface, <1 = quieter
}
```

### Implementation Tasks

- [x] Modify `StealthSystem.cs`: add `GroundSurfaceState` to query, replace `surfaceMultiplier = 1.0f` with BlobAsset lookup
- [x] Add `RequireForUpdate<SurfaceGameplayConfigSingleton>()` to `StealthSystem.OnCreate` — uses TryGetSingleton for graceful fallback
- [x] Create `SurfaceNoiseModifier` IComponentData
- [x] Create `SurfaceStealthModifierSystem` for NPC surface noise
- [x] Wire `HearingDetectionSystem` to read `SurfaceNoiseModifier` on NPC entities
- [ ] **Test:** Player on Metal_Thin surface: noise level ~1.5x vs Grass at ~0.6x
- [ ] **Test:** Player crouching on Metal: stanceMultiplier(0.0) * surfaceMultiplier(1.5) = 0.0 (silent)
- [ ] **Test:** NPC on gravel detected by player's hearing system at greater range than NPC on grass

---

## Phase 4: Surface Movement Modifiers

### Problem

Mud should slow you down. Ice should speed you up but reduce control. Sand is heavy to walk through. The `SurfaceMaterial` SO already has `FrictionModifier` and `SlideFrictionMultiplier` fields, but `PlayerMovementSystem` and `CharacterControllerSystem` never read them.

### Components

```csharp
/// <summary>
/// Written by SurfaceMovementModifierSystem onto entities with GroundSurfaceState.
/// Read by PlayerMovementSystem to adjust speed and by CharacterControllerSystem for friction.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct SurfaceMovementModifier : IComponentData
{
    /// <summary>Speed multiplier from ground surface. 1.0 = normal.</summary>
    [GhostField(Quantization = 100)] public float SpeedMultiplier;

    /// <summary>Friction multiplier from ground surface. 1.0 = normal. Lower = more slippery.</summary>
    [GhostField(Quantization = 100)] public float FrictionMultiplier;

    /// <summary>Slip factor from ground surface. 0 = full control. 1 = no control.</summary>
    [GhostField(Quantization = 100)] public float SlipFactor;
}
```

### Systems

#### `SurfaceMovementModifierSystem`

- **Group:** `PredictedFixedStepSimulationSystemGroup`
- **After:** `GroundSurfaceQuerySystem`
- **Before:** `PlayerMovementSystem`
- **Filter:** `ClientSimulation | ServerSimulation`
- **Burst:** Yes

**Logic:**

1. For each entity with `GroundSurfaceState` + `SurfaceMovementModifier`:
2. Read `SurfaceId` from `GroundSurfaceState`.
3. Look up `SpeedMultiplier`, `SlipFactor` from `SurfaceGameplayBlob`.
4. Read `FrictionModifier` from the `SurfaceMaterial` SO via cached BlobAsset (or use `GroundSurfaceState.CachedHardness`-derived friction -- see below).
5. Write to `SurfaceMovementModifier`.
6. **Smoothing:** Lerp between old and new values at `rate = 8.0 * deltaTime` to prevent jarring speed changes at surface boundaries.

**Friction derivation from Hardness (Burst-friendly, no managed lookup):**

```
frictionMultiplier = lerp(0.5, 1.5, cachedHardness / 255.0)
```

Hard surfaces (255) = high friction (1.5). Soft surfaces (0) = low friction (0.5). This is a fallback; `SurfaceGameplayConfig` values take priority when available.

#### `PlayerMovementSystem` Modifications

**File:** `Assets/Scripts/Player/Systems/PlayerMovementSystem.cs`

Add `RefRO<SurfaceMovementModifier>` to the existing query (optional -- entities without the component use 1.0x). Multiply the computed movement speed by `SurfaceMovementModifier.SpeedMultiplier` before passing to `CharacterControllerSystem`.

```
finalSpeed = baseSpeed * stanceMultiplier * sprintMultiplier * surfaceSpeedMultiplier
```

#### `SurfaceSlipSystem`

- **Group:** `PredictedFixedStepSimulationSystemGroup`
- **After:** `SurfaceMovementModifierSystem`
- **Before:** `CharacterControllerSystem`
- **Filter:** `ClientSimulation | ServerSimulation`
- **Burst:** Yes

**Logic:**

When `SurfaceMovementModifier.SlipFactor > 0`:

1. Read the entity's intended velocity (from `PlayerMovementSystem` output or `PhysicsVelocity`).
2. Blend between intended direction and current momentum: `actualVelocity = lerp(intendedVelocity, currentVelocity, SlipFactor)`.
3. This causes the player to slide on ice -- they try to turn but momentum carries them.
4. SlipFactor 0.0 = full control (concrete). SlipFactor 0.8 = mostly sliding (ice). SlipFactor 1.0 = no control (pure ice -- not recommended).

**Player feedback:**

- When `SlipFactor > 0.3`, enable a "sliding" camera wobble (subtle, 0.2 degree amplitude).
- When `SlipFactor > 0.5`, play the continuous ice-crackle loop from `SurfaceMaterial.ContinuousLoopClip` (already handled by existing `SurfaceContactAudioSystem`).

### Implementation Tasks

- [x] Create `SurfaceMovementModifier` IComponentData in `Assets/Scripts/Surface/Components/`
- [x] Create `SurfaceMovementModifierSystem` (PredictedFixedStep, Burst)
- [x] Create `SurfaceSlipSystem` (PredictedFixedStep, Burst)
- [x] Modify `PlayerMovementSystem`: read `SurfaceMovementModifier.SpeedMultiplier`, apply to final speed
- [x] Wire `PlayerMovementSystem`: read `SurfaceMovementModifier.FrictionMultiplier`, apply to ground friction
- [x] Add `SurfaceMovementModifier` to player prefab via `GroundSurfaceDetectionAuthoring` baker
- [x] Implement velocity-momentum blending for slip (lerp between intended and current velocity)
- [x] Implement speed transition smoothing (lerp at 8x deltaTime) at surface boundaries
- [ ] **Test:** Player on Mud (SpeedMultiplier = 0.5): movement speed halved
- [ ] **Test:** Player on Ice (SlipFactor = 0.8): turning radius dramatically increased
- [ ] **Test:** Player on Concrete (SpeedMultiplier = 1.0, SlipFactor = 0.0): no change from current behavior
- [ ] **Test:** Surface boundary crossing: smooth 0.125s transition, no jarring speed change

---

## Phase 5: Surface Fall Damage Integration

### Problem

`FallDetectionSystem.cs:225` has a TODO: `SurfaceMaterialId = 0, // TODO: Get from ground raycast hit`. Landing on sand should reduce fall damage. Landing on concrete should increase it. Landing on water should eliminate it (separate splash/swimming system handles water entry).

### System Modifications

#### `FallDetectionSystem` (Modify Existing)

**File:** `Assets/Scripts/Player/Systems/FallDetectionSystem.cs`

**Changes:**

1. Add `RefRO<GroundSurfaceState>` to the existing query.
2. On landing (line 209), read `GroundSurfaceState.SurfaceId`.
3. Look up `FallDamageMultiplier` from `SurfaceGameplayBlob`.
4. Apply multiplier to computed fall damage:

```csharp
// After existing damage calculation (line 239):
if (damage > 0f)
{
    // Surface material modifier
    float surfaceFallDamageMultiplier = 1.0f;
    if (SystemAPI.HasSingleton<SurfaceGameplayConfigSingleton>())
    {
        ref var blob = ref SystemAPI.GetSingleton<SurfaceGameplayConfigSingleton>()
            .Config.Value.Modifiers[(byte)groundSurfaceState.ValueRO.SurfaceId];
        surfaceFallDamageMultiplier = blob.FallDamageMultiplier;
    }
    damage *= surfaceFallDamageMultiplier;
}
```

5. Fix the `SurfaceImpactRequest` TODO at line 225:

```csharp
var impactRequest = new SurfaceImpactRequest
{
    ContactPoint = new float3(position.x, playerState.GroundHeight, position.z),
    ContactNormal = playerState.GroundNormal,
    ImpactVelocity = impactVelocity,
    SurfaceMaterialId = groundSurfaceState.ValueRO.SurfaceMaterialId, // FIXED: was 0
    SurfaceImpactId = fallSettings.LandSurfaceImpactId
};
```

### Fall Damage by Surface (Examples)

| Surface | FallDamageMultiplier | Gameplay Effect |
|---------|---------------------|-----------------|
| Sand | 0.5 | Soft landing -- half damage |
| Grass | 0.6 | Slightly cushioned |
| Dirt | 0.7 | Somewhat soft |
| Wood | 1.0 | Normal |
| Concrete | 1.2 | Slightly harder |
| Metal | 1.3 | Hard landing |
| Ice | 1.4 | Brutal -- slippery + hard |
| Glass | 1.5 | Shatters, cuts (extra damage) |
| Water | 0.3 | Mostly absorbed (belly flop) |
| Snow | 0.4 | Deep cushion |

### Implementation Tasks

- [x] Modify `FallDetectionSystem`: add `GroundSurfaceState` to query
- [x] Apply `FallDamageMultiplier` from BlobAsset to landing damage calculation
- [x] Fix `SurfaceImpactRequest.SurfaceMaterialId = 0` TODO: read from `GroundSurfaceState`
- [ ] **Test:** Fall from 10m onto Sand: damage ~50% of Concrete landing
- [ ] **Test:** Fall from 10m onto Metal: damage ~130% of default
- [ ] **Test:** `SurfaceImpactRequest` now carries correct material ID: correct VFX/audio plays on landing

---

## Phase 6: Surface Damage Zones

### Problem

Lava floors, acid pools, electrified metal grating, and other hazardous surfaces should deal damage-over-time to entities standing on them. The existing `EnvironmentZone` system handles atmospheric hazards (radiation, temperature, oxygen) via trigger volumes. Surface damage zones are different: damage applies only when the entity's **ground surface** matches a specific type, not just proximity to a volume.

### Components

```csharp
/// <summary>
/// Place on trigger volume entities to create surface-conditional damage zones.
/// Entity inside the zone only takes damage if their GroundSurfaceState matches
/// the required SurfaceID. This enables "lava floor" without damaging flying entities.
/// </summary>
public struct SurfaceDamageZone : IComponentData
{
    /// <summary>Damage per second while standing on matching surface inside zone.</summary>
    public float DamagePerSecond;

    /// <summary>Which DamageType to apply (Physical, Heat, Toxic, etc.).</summary>
    public Player.Components.DamageType DamageType;

    /// <summary>
    /// Required SurfaceID to take damage. Entity must be standing on this surface
    /// AND inside the zone trigger. SurfaceID.Default = any surface triggers damage.
    /// </summary>
    public SurfaceID RequiredSurfaceId;

    /// <summary>
    /// Damage tick interval in seconds. Lower = smoother DOT.
    /// Default 0.5s (2 ticks/sec). 0 = every frame (expensive).
    /// </summary>
    public float TickInterval;

    /// <summary>Optional: ramp-up time before full damage (seconds). Gives warning.</summary>
    public float RampUpDuration;

    /// <summary>If true, applies to any entity (players + NPCs). If false, players only.</summary>
    public bool AffectsNPCs;
}

/// <summary>
/// Tracking state for an entity inside a SurfaceDamageZone.
/// Internal -- created and managed by SurfaceDamageSystem.
/// </summary>
public struct SurfaceDamageZoneContact : ICleanupComponentData
{
    public Entity ZoneEntity;
    public float TimeInZone;
    public float TimeSinceLastTick;
}
```

### Systems

#### `SurfaceDamageSystem`

- **Group:** `SimulationSystemGroup`
- **After:** `GroundSurfaceQuerySystem`
- **Filter:** `ServerSimulation` (server-authoritative damage only)
- **Burst:** Yes (damage application via ECB)

**Logic:**

1. Use `PhysicsWorld.OverlapAabb` or trigger event processing to find entities inside `SurfaceDamageZone` trigger volumes.
2. For each entity inside a zone:
   a. Read entity's `GroundSurfaceState.SurfaceId`.
   b. If `RequiredSurfaceId == SurfaceID.Default` OR `GroundSurfaceState.SurfaceId == RequiredSurfaceId`:
   c. Increment `TimeInZone` and `TimeSinceLastTick`.
   d. If `TimeSinceLastTick >= TickInterval`:
      - Calculate damage: `DamagePerSecond * TickInterval`.
      - If `TimeInZone < RampUpDuration`: scale damage by `TimeInZone / RampUpDuration`.
      - Apply via `DamageEvent` buffer (existing pipeline) for players.
      - Apply via `Health` direct write for NPCs (via `SimpleDamageApplySystem` path).
      - Reset `TimeSinceLastTick = 0`.
3. When entity exits zone: remove `SurfaceDamageZoneContact` cleanup component.

### Authoring

```csharp
/// <summary>
/// Place on GameObjects with trigger colliders to create surface damage zones.
/// Example: Lava pool (RequiredSurfaceId = Default, DamageType = Heat, DPS = 25).
/// Example: Electrified grating (RequiredSurfaceId = Metal_Thin, DamageType = Physical, DPS = 10).
/// </summary>
public class SurfaceDamageZoneAuthoring : MonoBehaviour
{
    public float DamagePerSecond = 10f;
    public Player.Components.DamageType DamageType = Player.Components.DamageType.Heat;
    public SurfaceID RequiredSurfaceId = SurfaceID.Default;
    [Range(0.1f, 2f)]
    public float TickInterval = 0.5f;
    [Range(0f, 5f)]
    public float RampUpDuration = 0f;
    public bool AffectsNPCs = true;

    // Baker
    public class Baker : Baker<SurfaceDamageZoneAuthoring>
    {
        public override void Bake(SurfaceDamageZoneAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SurfaceDamageZone
            {
                DamagePerSecond = authoring.DamagePerSecond,
                DamageType = authoring.DamageType,
                RequiredSurfaceId = authoring.RequiredSurfaceId,
                TickInterval = authoring.TickInterval,
                RampUpDuration = authoring.RampUpDuration,
                AffectsNPCs = authoring.AffectsNPCs
            });
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f); // Orange-red for hazard
        var col = GetComponent<BoxCollider>();
        if (col != null)
            Gizmos.DrawCube(transform.position + col.center, col.size);
    }
}
```

### Example Configurations

| Zone Name | RequiredSurfaceId | DamagePerSecond | DamageType | RampUpDuration |
|-----------|-------------------|-----------------|------------|----------------|
| Lava Pool | Default | 25 | Heat | 0.5s |
| Acid Floor | Default | 15 | Toxic | 0s |
| Electrified Grating | Metal_Thin | 10 | Physical | 0s |
| Radioactive Sludge | Water | 5 | Radiation | 2.0s |
| Freezing Ice | Ice | 3 | Physical | 1.0s |
| Hot Coals | Gravel | 8 | Heat | 0s |

### Implementation Tasks

- [x] Create `SurfaceDamageZone` IComponentData in `Assets/Scripts/Surface/Components/`
- [x] Create `SurfaceDamageZoneContact` ICleanupComponentData
- [x] Create `SurfaceDamageSystem` (SimulationSystemGroup, ServerSimulation)
- [x] Implement proximity-based zone detection with contact tracking (NativeHashMap)
- [x] Implement ramp-up damage scaling + tick interval throttling
- [x] Create `SurfaceDamageZoneAuthoring` MonoBehaviour + Baker with gizmos
- [x] Wire damage output to direct `Health.Current` write
- [ ] **Test:** Player standing in lava zone on Default surface: 25 DPS applied
- [ ] **Test:** Player standing in electrified zone on Grass surface: NO damage (surface mismatch)
- [ ] **Test:** Player entering zone with 2s ramp-up: 0 damage at t=0, 50% damage at t=1, full damage at t=2
- [ ] **Test:** NPC walks into lava zone with AffectsNPCs=true: takes damage
- [ ] **Test:** Flying entity (IsGrounded=false) in lava zone: no damage

---

## Phase 7: NPC/Enemy Surface Detection

### Problem

Only player entities get ground surface detection (via `PlayerGroundCheckSystem`). Enemies and NPCs have no concept of what surface they stand on. This prevents:
- AI enemies on noisy surfaces being easier to detect by hearing
- Enemies slipping on ice
- Enemies avoiding hazardous surface zones
- Designers debugging NPC surface state

### Strategy

Add `GroundSurfaceDetectionAuthoring` (from Phase 1) to enemy prefabs. The `GroundSurfaceQuerySystem` handles both player and NPC entities uniformly. NPCs use a longer `QueryInterval` (0.25-0.5s) since precision matters less.

### NPC Surface Query Budget

The frame-spread pattern from `DetectionSystem.SensorSpreadFrames` prevents all NPC queries from executing on the same frame:

```
spreadFactor = max(1, entityCount / maxQueriesPerFrame)
execute = (entity.Index + frameCount) % spreadFactor == 0
```

With `maxQueriesPerFrame = 16` and 200 NPCs, `spreadFactor = 13`. Each frame runs ~15 raycast queries. At 60 FPS, all 200 NPCs update within ~13 frames (~0.22s) -- well within the 0.25s `QueryInterval`.

### Enemy AI Integration

Systems that already read NPC state can optionally branch on surface:

1. **AI Pathfinding (future):** Weight surface cost in navigation mesh. Metal corridors are "noisy" -- stealth-oriented AI avoids them.
2. **AI Patrol Selection:** Patrol points on quieter surfaces weighted higher for stealth enemies.
3. **Enemy Footstep Audio:** Existing `FootstepSystem` already reads `SurfaceMaterialId` -- adding `GroundSurfaceState` to NPCs enables correct enemy footstep sounds.

### Implementation Tasks

- [ ] Add `GroundSurfaceDetectionAuthoring` to BoxingJoe prefab (QueryInterval = 0.25)
- [ ] Add `GroundSurfaceDetectionAuthoring` to enemy template prefabs (QueryInterval = 0.5)
- [x] Verify `GroundSurfaceQuerySystem` handles NPC entities (no `PlayerTag` requirement) — NPC path raycasts, player path reads PlayerGroundCheckSystem
- [ ] Verify frame-spread keeps NPC query budget under 0.1ms/frame with 200 entities
- [ ] **Test:** BoxingJoe reports correct SurfaceID when standing on different terrain
- [ ] **Test:** 200 NPCs with surface detection: total system cost < 0.1ms average

---

## Phase 8: Modularity & Graceful Degradation

### Design Principle: Surfaces Are Always Cosmetic-Safe

Every gameplay system in this EPIC is **additive**. If a system is disabled or a component is removed:
- Audio, VFX, decals, haptics continue working via the existing 68-file surface pipeline
- `SurfaceMaterial` assets remain valid and complete
- `SurfaceMaterialAuthoring` continues baking `SurfaceMaterialId` for audio/VFX systems
- Players can still walk, fight, and play -- surfaces just become cosmetic

### Removal Strategy

To disable surface gameplay entirely (make surfaces cosmetic-only):

1. Remove `GroundSurfaceDetectionAuthoring` from prefabs.
2. Delete or disable `SurfaceGameplayConfigSystem`.
3. All consumers (`StealthSystem`, `PlayerMovementSystem`, `FallDetectionSystem`) fall back to `1.0x` multipliers when `SurfaceGameplayConfigSingleton` is absent.

Each system checks for the singleton and defaults gracefully:

```csharp
// In any consuming system:
float surfaceMultiplier = 1.0f;
if (SystemAPI.HasSingleton<SurfaceGameplayConfigSingleton>() &&
    SystemAPI.HasComponent<GroundSurfaceState>(entity))
{
    var surfaceId = groundSurface.ValueRO.SurfaceId;
    ref var blob = ref configSingleton.Config.Value;
    surfaceMultiplier = blob.Modifiers[(byte)surfaceId].NoiseMultiplier;
}
```

### Per-System Feature Toggles

```csharp
/// <summary>
/// Singleton for runtime toggling of surface gameplay features.
/// Useful for difficulty settings, game modes, and debugging.
/// </summary>
public struct SurfaceGameplayToggles : IComponentData
{
    public bool EnableMovementModifiers;   // Speed/friction changes
    public bool EnableStealthModifiers;    // Noise multipliers
    public bool EnableSlipPhysics;         // Ice/slippery surfaces
    public bool EnableFallDamageModifiers; // Surface-aware fall damage
    public bool EnableSurfaceDamageZones;  // Lava/acid DOT

    public static SurfaceGameplayToggles AllEnabled => new()
    {
        EnableMovementModifiers = true,
        EnableStealthModifiers = true,
        EnableSlipPhysics = true,
        EnableFallDamageModifiers = true,
        EnableSurfaceDamageZones = true
    };
}
```

### Implementation Tasks

- [x] Create `SurfaceGameplayToggles` IComponentData singleton
- [x] Add toggle checks to all Phase 3-6 systems (early-out when disabled)
- [x] Add `SurfaceGameplayTogglesAuthoring` MonoBehaviour + Baker for singleton creation
- [ ] **Test:** Disable all toggles: game plays identically to pre-EPIC 16.10 behavior
- [ ] **Test:** Enable only stealth: surfaces affect noise but not speed/damage
- [ ] **Test:** Remove `GroundSurfaceDetectionAuthoring` from player: all systems fall back to 1.0x

---

## Phase 9: Editor Tooling & Debug

### Surface Debug Overlay

Add a debug visualization mode (editor-only) that shows surface gameplay data in the Scene view and as runtime overlays:

- **Per-entity labels:** Surface name, SurfaceID, noise multiplier, speed multiplier above each entity with `GroundSurfaceState`
- **Ground color coding:** Gizmo that tints the ground under entities based on gameplay effect (red = high damage, blue = slippery, green = quiet, yellow = slow)
- **Surface Damage Zone visualization:** Wireframe volumes with damage-per-second labels and color gradient (cool blue to hot red)

### Surface Gameplay Inspector

Editor window (`DIG > Surface > Gameplay Inspector`):

- **Active Surfaces:** List of all unique `SurfaceID` values currently detected across all entities
- **Per-Entity View:** Select entity, see all surface modifiers being applied
- **Config Preview:** Show `SurfaceGameplayConfig` values inline with live entity state
- **Performance:** Query count/frame, raycast budget usage, frame-spread distribution

### Implementation Tasks

- [x] Create `SurfaceDebugOverlaySystem` (managed, PresentationSystemGroup, editor-only) for world-space labels
- [x] Create `SurfaceGameplayInspectorWindow` EditorWindow in `Assets/Editor/Surface/`
- [x] Add SceneView gizmo rendering for SurfaceDamageZone volumes (via SurfaceDamageZoneAuthoring.OnDrawGizmosSelected)
- [x] Add runtime debug toggle for surface overlay visibility (SurfaceDebugOverlaySystem.ShowOverlay + inspector toggle)
- [ ] **Test:** Open inspector, select player entity, verify correct surface data shown

---

## File Structure

```
Assets/Scripts/Surface/
├── Components/
│   ├── SurfaceComponents.cs          (existing -- SurfaceID, ImpactClass, etc.)
│   ├── GroundSurfaceState.cs         (NEW -- Phase 1)
│   ├── SurfaceMovementModifier.cs    (NEW -- Phase 4)
│   ├── SurfaceNoiseModifier.cs       (NEW -- Phase 3)
│   ├── SurfaceDamageZone.cs          (NEW -- Phase 6)
│   └── SurfaceGameplayToggles.cs     (NEW -- Phase 8)
├── Config/
│   ├── SurfaceGameplayConfig.cs      (NEW -- Phase 2, ScriptableObject)
│   ├── SurfaceGameplayBlob.cs        (NEW -- Phase 2, BlobAsset)
│   └── (existing config files)
├── Systems/
│   ├── GroundSurfaceQuerySystem.cs   (NEW -- Phase 1)
│   ├── GroundSurfaceCacheSystem.cs   (NEW -- Phase 1, managed)
│   ├── SurfaceGameplayConfigSystem.cs (NEW -- Phase 2)
│   ├── SurfaceMovementModifierSystem.cs (NEW -- Phase 4)
│   ├── SurfaceSlipSystem.cs          (NEW -- Phase 4)
│   ├── SurfaceStealthModifierSystem.cs (NEW -- Phase 3, NPC noise)
│   ├── SurfaceDamageSystem.cs        (NEW -- Phase 6)
│   ├── SurfaceContactAudioSystem.cs  (existing)
│   └── MountSurfaceEffectSystem.cs   (existing)
├── Authoring/
│   ├── GroundSurfaceDetectionAuthoring.cs (NEW -- Phase 1)
│   └── SurfaceDamageZoneAuthoring.cs (NEW -- Phase 6)
└── Debug/
    └── SurfaceDebugOverlaySystem.cs  (NEW -- Phase 9)

Assets/Editor/Surface/
└── SurfaceGameplayInspectorWindow.cs (NEW -- Phase 9)

Assets/Resources/
└── SurfaceGameplayConfig.asset       (NEW -- Phase 2, default values)

Modified files:
├── Assets/Scripts/Player/Systems/StealthSystem.cs         (Phase 3 -- wire surface multiplier)
├── Assets/Scripts/Player/Systems/FallDetectionSystem.cs   (Phase 5 -- wire surface fall damage)
├── Assets/Scripts/Player/Systems/PlayerMovementSystem.cs  (Phase 4 -- wire speed modifier)
├── Assets/Scripts/Player/Systems/CharacterControllerSystem.cs (Phase 4 -- wire friction)
```

---

## NetCode Safety Rules

1. **`GroundSurfaceState`** is `[GhostComponent(PrefabType = AllPredicted)]` -- predicted for responsive movement feel. Server is authoritative; client predicts.
2. **`SurfaceMovementModifier`** is `[GhostComponent(PrefabType = AllPredicted)]` -- same reasoning as GroundSurfaceState.
3. **`SurfaceNoiseModifier`** is NOT ghost-replicated -- server-only, used only by server-side hearing detection.
4. **`SurfaceDamageZone`** is NOT ghost-replicated -- lives on scene-placed trigger volumes (server-owned entities).
5. **`SurfaceDamageZoneContact`** is an `ICleanupComponentData` on player/NPC entities -- NOT an `IBufferElementData` on ghost entities. Safe. Cleanup component pattern ensures removal even if zone entity is destroyed.
6. **NO new `IBufferElementData`** on ghost-replicated entities. All tracking is via singleton lookups or per-entity IComponentData.
7. **BlobAsset** (`SurfaceGameplayBlob`) for read-only gameplay config. Shared, immutable, Burst-friendly.
8. All managed systems (`GroundSurfaceCacheSystem`, `SurfaceGameplayConfigSystem`) run in `PresentationSystemGroup` or `InitializationSystemGroup` with appropriate `WorldSystemFilter`.

---

## Performance Budget

| System | Target | Approach | Notes |
|--------|--------|----------|-------|
| `GroundSurfaceQuerySystem` | < 0.05ms | Frame-spread + player skip | 200 entities, ~15 raycasts/frame max |
| `GroundSurfaceCacheSystem` | < 0.01ms | Only runs on surface change | Managed, non-Burst, rare execution |
| `SurfaceMovementModifierSystem` | < 0.02ms | Simple BlobAsset lookup + lerp | Per-entity, no allocation |
| `SurfaceSlipSystem` | < 0.02ms | Velocity blend, no raycast | Only entities with SlipFactor > 0 |
| `SurfaceStealthModifierSystem` | < 0.01ms | BlobAsset lookup, write float | NPC entities only |
| `SurfaceDamageSystem` | < 0.03ms | Trigger overlap + DOT tick | Only entities in damage zones |
| **Total Surface Budget** | **< 0.15ms** | All surface gameplay systems | Well within frame budget |

### Worst Case Analysis

- **1000 entities with `GroundSurfaceState`:** At `spreadFactor = 63` and `maxQueriesPerFrame = 16`, each frame runs ~16 raycasts. Raycast cost ~0.003ms each = 0.048ms. Within budget.
- **50 entities in `SurfaceDamageZone`:** 50 overlap checks + DOT ticks = ~0.02ms. Trivial.
- **All systems combined during a 200-entity combat encounter:** ~0.1ms total. Less than 1% of a 16ms frame budget.

---

## Integration Points

| System | EPIC | Integration |
|--------|------|-------------|
| Stealth / Noise | 15.19 | `StealthSystem` reads `GroundSurfaceState` for `surfaceMultiplier` |
| Hearing Detection | 15.19 | `HearingDetectionSystem` reads `SurfaceNoiseModifier` for NPC detectability |
| Fall Detection | 13.14 | `FallDetectionSystem` reads `GroundSurfaceState` for fall damage modifier + impact material |
| Movement | Core | `PlayerMovementSystem` reads `SurfaceMovementModifier.SpeedMultiplier` |
| Character Controller | Core | `CharacterControllerSystem` reads `SurfaceMovementModifier.FrictionMultiplier` |
| Footstep Audio | 13.18 | `FootstepSystem` already reads `SurfaceMaterialId` -- no change needed |
| Surface Audio | 15.24 | `SurfaceContactAudioSystem` already reads surface for loop audio -- no change needed |
| Decals | 13.18.2 | Existing footprint/impact decal pipeline -- no change needed |
| VFX | 15.24 | Existing surface VFX pipeline -- no change needed |
| Haptics | 15.24 | Existing haptic feedback pipeline -- no change needed |
| Environment Zones | Survival | `SurfaceDamageZone` complements `EnvironmentZone` (atmospheric vs surface hazards) |
| AI Vision | 15.17 | `DetectionSystem` frame-spread pattern reused for surface query staggering |
| Enemy AI | 15.31 | NPC `GroundSurfaceState` enables surface-aware pathfinding (future) |

---

## Design Considerations

### Why SurfaceGameplayConfig SO Instead of Extending SurfaceMaterial

1. **Separation of concerns:** Audio/VFX artists own `SurfaceMaterial`. Gameplay designers own `SurfaceGameplayConfig`. No merge conflicts.
2. **Game mode profiles:** Survival mode uses aggressive lava damage. Racing mode uses aggressive friction. Stealth mode amplifies noise differences. Each is a different `SurfaceGameplayConfig` asset.
3. **BlobAsset optimization:** The gameplay config is converted to a BlobAsset array indexed by `SurfaceID` (24 entries, ~576 bytes). This is orders of magnitude cheaper than managed SO lookups in Burst jobs.
4. **Backward compatibility:** Existing `SurfaceMaterial` assets are untouched. Zero migration cost.

### Why GroundSurfaceState Instead of Reading PlayerGroundCheckSystem Directly

1. **Entity-agnostic:** NPCs, enemies, vehicles, and any grounded entity can use `GroundSurfaceState`. `PlayerGroundCheckSystem` is player-only.
2. **Configurable frequency:** Different entities need different update rates. Players want per-frame precision. Background NPCs can query every 0.5s.
3. **Single consumer interface:** All gameplay systems read `GroundSurfaceState` uniformly instead of depending on different source systems.
4. **Player optimization preserved:** For player entities, `GroundSurfaceQuerySystem` reads from `PlayerGroundCheckSystem` output (no duplicate raycast).

### Why Frame-Spread Instead of Fixed Interval Timer

A fixed 0.25s timer causes all entities to query simultaneously if they spawn at the same time (e.g., wave spawns). Frame-spread using `entity.Index % spreadFactor` distributes queries evenly across frames regardless of spawn timing, preventing CPU spikes.

### Why ICleanupComponentData for SurfaceDamageZoneContact

If a `SurfaceDamageZone` entity is destroyed while an entity is inside it (e.g., explosion destroys the lava floor), `ICleanupComponentData` ensures the tracking state is properly cleaned up by the structural change system, preventing orphaned damage tracking.

### Surface-Based AI Pathfinding (Future)

This EPIC establishes `GroundSurfaceState` on NPCs as the foundation for surface-aware AI navigation. A future EPIC can add surface cost to the navigation mesh: stealth-oriented enemies prefer quiet surfaces (grass, dirt) over noisy ones (metal, gravel). The infrastructure from Phase 7 makes this a small incremental addition rather than a ground-up implementation.

---

## Accessibility

1. **Visual indicators for surface effects:** When surface modifiers are active, a subtle HUD icon shows the current surface effect (snowflake for ice/slip, footprint for noise, snail for slow). Colorblind-safe: icons are shape-coded, not just color-coded.
2. **Screen reader support:** Surface effect names are exposed as localization keys for vocalization ("You are walking on ice -- movement is slippery").
3. **Difficulty scaling:** `SurfaceGameplayConfig` can have per-difficulty entries. Easy mode: all multipliers closer to 1.0. Hard mode: extreme values (ice SlipFactor = 0.9, lava DPS = 50).
4. **Toggle individual effects:** `SurfaceGameplayToggles` allows disabling slip physics for players with motor control difficulties while keeping stealth and damage modifiers active.
