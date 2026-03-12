# EPIC 16.7: Unified VFX Event Pipeline

**Status:** Complete (Implementation)
**Priority:** High (Core Infrastructure)
**Dependencies:**
- `VFXManager` MonoBehaviour singleton (existing — `Audio.Systems`)
- `GroundEffectQueue` + `AbilityGroundEffectSystem` (existing — `DIG.Surface`)
- `SurfaceImpactQueue` + `SurfaceImpactPresenterSystem` (existing — `DIG.Surface`)
- `DamageVisualQueue` + `CombatUIBridgeSystem` (existing — `DIG.Combat.UI`)
- `CollisionVFXBridge` + `CollisionVFXSystem` (existing — `DIG.Player`)
- `ItemVFXAuthoring` + `VFXDefinition` (existing — `DIG.Items.Authoring`)
- `CorpseSinkSystem` (existing — EPIC 16.3)
- `EffectLODTier` enum (existing — `DIG.Surface`)
- `Unity.Entities` 1.x
- `Unity.NetCode`
- `Unity.Burst`
- URP Shader Graph (for dissolve shader, Phase 5)

**Feature:** A unified, ECS-native VFX event pipeline that replaces the current ad-hoc static queue pattern with a single transient-entity request system. Provides per-category budget throttling, formal VFX LOD tiers, standardized parameterization (scale, color tint, duration, intensity), a dissolve/alpha fade shader for corpse presentation, and backward-compatible legacy bridges for all existing VFX consumers.

---

## Overview

### Problem

The current VFX infrastructure has grown organically across multiple EPICs, producing **four independent static queue pipelines** that each solve the same ECS-to-managed bridge problem in slightly different ways:

| Queue | Producer(s) | Consumer | Pattern |
|-------|------------|----------|---------|
| `SurfaceImpactQueue` | WeaponFireSystem, ProjectileImpactSystem, FootstepSystem, BodyFallImpactSystem | `SurfaceImpactPresenterSystem` | Static `Queue<SurfaceImpactData>` |
| `GroundEffectQueue` | Ability systems (AOE casts) | `AbilityGroundEffectSystem` | Static `Queue<GroundEffectRequest>` |
| `DamageVisualQueue` | CRS, DamageEventVisualBridgeSystem, ClientDamageVisualBridgeSystem | `CombatUIBridgeSystem` | Static `Queue<DamageVisualData>` + `NativeHashMap` hints |
| `CollisionVFXBridge` | `CollisionVFXSystem` | Direct MonoBehaviour call | Per-entity bridge cache pattern |

**Consequences:**
1. **No unified throttling** — `VFXManager` has a single global 30/sec rate limiter, but each queue has its own independent per-frame cap (`MaxEventsPerFrame = 32` in SurfaceImpact, `MaxEffectsPerFrame = 8` in GroundEffect). A combat scene can blow its entire VFX budget on surface impacts with zero remaining for ability ground effects.
2. **No ECS-native request path** — All bridges use managed static queues or MonoBehaviour caches. Burst-compiled systems cannot directly request VFX without a managed hop.
3. **No standardized parameterization** — `SurfaceImpactData` has `Intensity` + `LODTier` + `ImpactClass`. `GroundEffectRequest` has `Intensity` + `Radius` + `Duration`. `DamageVisualData` has `HitType` + `DamageType` + `Flags`. No shared vocabulary for scale, color tint, or duration.
4. **No dissolve shader** — `CorpseSinkSystem` (EPIC 16.3) uses position sinking as a workaround because no `_DissolveAmount` shader property exists. Alpha fade mode is deferred (EPIC 16.3 Phase 4.1) pending this work.
5. **No VFX LOD system** — `VFXManager` has binary distance culling (50m hard cutoff). `SurfaceImpactPresenterSystem` has a proper 4-tier LOD (`Full < 15m, Reduced < 40m, Minimal < 60m, Culled`) but it is local to surface impacts and not shared with other VFX pipelines.

### Solution

Replace the scattered static queue pattern with a **transient VFX event entity** system:

1. **`VFXRequest` IComponentData** on short-lived event entities (created by any system, destroyed after execution)
2. **`VFXTypeRegistry`** managed singleton mapping integer type IDs to prefabs, configs, and LOD tiers
3. **`VFXBudgetSystem`** allocates per-category frame budgets before execution
4. **`VFXExecutionSystem`** (managed, PresentationSystemGroup) consumes request entities, delegates to `VFXManager` pooling
5. **Legacy bridge adapters** route existing static queues into the new pipeline for backward compatibility
6. **Dissolve shader** with `_DissolveAmount` float property for corpse fade and death VFX
7. **Unified VFX LOD** shared across all pipelines with configurable distance thresholds

### Principles

1. **Cosmetic isolation** — The entire VFX pipeline is removable. No gameplay system depends on VFX execution. Request entities are fire-and-forget; if no execution system exists, they are cleaned up harmlessly.
2. **Burst on the request side, managed on the execution side** — Any Burst job can create a `VFXRequest` entity via `EntityCommandBuffer`. Only the final execution step (instantiating GameObjects) requires managed code.
3. **Budget-first** — Every VFX request is categorized. The budget system allocates frame slots per category before any spawning occurs. This prevents any single subsystem from monopolizing the VFX budget.
4. **Extend, don't break** — Existing static queues continue working during migration. Legacy bridges feed them into the new pipeline. Systems can be migrated one at a time.
5. **Shared LOD vocabulary** — The existing `EffectLODTier` enum (Full/Reduced/Minimal/Culled) becomes the project-wide standard for all VFX distance decisions.

### Data Flow

```
[Any ECS System — Burst or Managed]
        |
        | ECB.CreateEntity() + AddComponent<VFXRequest>
        v
[VFX Request Entities in World]
        |
        v
[VFXBudgetSystem] (SimulationSystemGroup, after all producers)
    Counts requests per VFXCategory
    Marks excess requests as Culled (enables VFXCulled tag)
    Enforces per-category caps
        |
        v
[VFXLODSystem] (SimulationSystemGroup, after VFXBudgetSystem)
    Computes distance to camera per request
    Assigns EffectLODTier
    Marks Culled requests (VFXCulled tag)
        |
        v
[VFXExecutionSystem] (Managed, PresentationSystemGroup, ClientSimulation)
    Queries VFXRequest entities WITHOUT VFXCulled
    Resolves VFXTypeId via VFXTypeRegistry → prefab + config
    Calls VFXManager.SpawnVFX() with parameterization
    Applies LOD-tier adjustments (reduced particles, particle-only, etc.)
    Destroys request entities
        |
        v
[VFXCleanupSystem] (PresentationSystemGroup, after VFXExecutionSystem)
    Destroys ALL remaining VFXRequest entities (culled, unresolved, etc.)
    Guarantees zero request entity accumulation
```

---

## Phase 1: Core VFX Request Pipeline

### 1.1 VFX Category & Request Components

```csharp
// ─── VFX Categories ───

/// <summary>
/// Budget category for VFX throttling. Each category has an independent frame cap.
/// </summary>
public enum VFXCategory : byte
{
    Combat = 0,         // Weapon impacts, muzzle flashes, projectile trails
    Environment = 1,    // Surface impacts, footsteps, water splashes, weather
    Ability = 2,        // Ability casts, AOE ground effects, buffs/debuffs
    Death = 3,          // Death VFX, gibs, blood splatter, corpse dissolve
    UI = 4,             // Damage numbers, status icons, pickup indicators
    Ambient = 5,        // Ambient particles, fireflies, dust motes, fog
    Interaction = 6     // Interaction prompts, crafting sparks, loot glow
}

/// <summary>
/// VFX LOD quality level. Shared project-wide, extends existing EffectLODTier.
/// Reuses DIG.Surface.EffectLODTier values for backward compatibility.
/// </summary>
// NOTE: Uses existing EffectLODTier enum from DIG.Surface.SurfaceComponents:
//   Full = 0      (<15m)   — All particles, decals, trails, sub-emitters
//   Reduced = 1   (15-40m) — 50% particle emission, skip sub-emitters
//   Minimal = 2   (40-80m) — Single billboard sprite or simplified particle
//   Culled = 3    (80m+)   — Skip entirely

/// <summary>
/// Transient VFX request component. Created by any system, consumed by VFXExecutionSystem.
/// Uses the event-entity pattern: each request is a standalone entity destroyed after processing.
/// </summary>
public struct VFXRequest : IComponentData
{
    /// <summary>World-space position to spawn the VFX.</summary>
    public float3 Position;

    /// <summary>World-space rotation for directional VFX (impacts, trails).</summary>
    public quaternion Rotation;

    /// <summary>Integer ID mapping to VFXTypeRegistry entry. Resolved to prefab at execution time.</summary>
    public int VFXTypeId;

    /// <summary>Budget category for throttling. Determines which per-category cap applies.</summary>
    public VFXCategory Category;

    /// <summary>Intensity scalar [0-1]. Affects particle count, emission rate, scale multiplier.</summary>
    public float Intensity;

    /// <summary>Uniform scale multiplier applied to the spawned VFX. Default 1.0.</summary>
    public float Scale;

    /// <summary>
    /// Color tint applied to the VFX. Uses particle system start color modulation.
    /// Default (0,0,0,0) means "use prefab default" — no tint override.
    /// </summary>
    public float4 ColorTint;

    /// <summary>
    /// Duration override in seconds. 0 = use prefab default (ParticleSystem.main.duration).
    /// Positive values override the natural lifetime.
    /// </summary>
    public float Duration;

    /// <summary>
    /// Source entity that caused this VFX (e.g., the attacker, the ability caster).
    /// Used for deduplication and source-relative effects. Entity.Null if no source.
    /// </summary>
    public Entity SourceEntity;

    /// <summary>
    /// Priority within category. Higher priority requests survive budget culling.
    /// Default 0. Boss death effects might use 100. Ambient effects use -10.
    /// </summary>
    public int Priority;
}

/// <summary>
/// Enableable tag added to VFX request entities that have been budget-culled or LOD-culled.
/// VFXExecutionSystem skips entities with this tag. VFXCleanupSystem destroys them.
/// </summary>
public struct VFXCulled : IComponentData, IEnableableComponent { }
```

### 1.2 VFX Type Registry

```csharp
/// <summary>
/// ScriptableObject entry defining a single VFX type.
/// Referenced by VFXTypeId integer in VFXRequest.
/// </summary>
[Serializable]
public struct VFXTypeEntry
{
    /// <summary>Unique integer ID. Assigned by VFXTypeRegistry. Must be stable across sessions.</summary>
    public int TypeId;

    /// <summary>Human-readable name for debug/editor display.</summary>
    public string Name;

    /// <summary>The GameObject prefab with ParticleSystem(s) to spawn.</summary>
    public GameObject Prefab;

    /// <summary>Default category if not overridden by the request.</summary>
    public VFXCategory DefaultCategory;

    /// <summary>Minimum LOD tier at which this VFX is visible. Some effects skip Reduced entirely.</summary>
    public EffectLODTier MinimumLODTier;

    /// <summary>If true, VFXManager prewarms a pool for this prefab on startup.</summary>
    public bool Prewarm;

    /// <summary>Pool prewarm count.</summary>
    public int PrewarmCount;

    /// <summary>Maximum simultaneous instances of this VFX type. 0 = unlimited.</summary>
    public int MaxInstances;

    /// <summary>LOD-Reduced prefab variant (fewer particles). Null = use main prefab with emission reduction.</summary>
    public GameObject ReducedPrefab;

    /// <summary>LOD-Minimal prefab variant (billboard only). Null = skip at Minimal tier.</summary>
    public GameObject MinimalPrefab;
}

/// <summary>
/// Managed singleton registry mapping VFXTypeId → VFXTypeEntry.
/// Loaded from a ScriptableObject database at startup.
/// </summary>
// File: Assets/Scripts/VFX/VFXTypeRegistry.cs
// Pattern: ScriptableObject + runtime Dictionary<int, VFXTypeEntry>
```

```csharp
/// <summary>
/// ScriptableObject database containing all VFX type entries.
/// Lives in Resources/ for runtime loading.
/// </summary>
[CreateAssetMenu(menuName = "DIG/VFX/VFX Type Database")]
public class VFXTypeDatabase : ScriptableObject
{
    [SerializeField] private List<VFXTypeEntry> _entries = new();

    /// <summary>Runtime lookup. Built on first access.</summary>
    private Dictionary<int, VFXTypeEntry> _lookup;

    public bool TryGetEntry(int typeId, out VFXTypeEntry entry)
    {
        EnsureLookup();
        return _lookup.TryGetValue(typeId, out entry);
    }

    public IReadOnlyList<VFXTypeEntry> AllEntries => _entries;

    private void EnsureLookup()
    {
        if (_lookup != null) return;
        _lookup = new Dictionary<int, VFXTypeEntry>(_entries.Count);
        foreach (var e in _entries)
            _lookup[e.TypeId] = e;
    }
}
```

### 1.3 Budget Configuration

```csharp
/// <summary>
/// ECS singleton holding per-category VFX budget caps.
/// Authored via VFXBudgetConfigAuthoring in subscene, or created with defaults by bootstrap.
/// </summary>
public struct VFXBudgetConfig : IComponentData
{
    /// <summary>Max VFX requests executed per frame for Combat category.</summary>
    public int CombatBudget;            // Default: 16

    /// <summary>Max VFX requests executed per frame for Environment category.</summary>
    public int EnvironmentBudget;       // Default: 24

    /// <summary>Max VFX requests executed per frame for Ability category.</summary>
    public int AbilityBudget;           // Default: 12

    /// <summary>Max VFX requests executed per frame for Death category.</summary>
    public int DeathBudget;             // Default: 8

    /// <summary>Max VFX requests executed per frame for UI category.</summary>
    public int UIBudget;                // Default: 20

    /// <summary>Max VFX requests executed per frame for Ambient category.</summary>
    public int AmbientBudget;           // Default: 10

    /// <summary>Max VFX requests executed per frame for Interaction category.</summary>
    public int InteractionBudget;       // Default: 8

    /// <summary>Global hard cap across all categories. Safety valve.</summary>
    public int GlobalMaxPerFrame;       // Default: 64

    /// <summary>Returns the budget for a given category.</summary>
    public int GetBudget(VFXCategory category) => category switch
    {
        VFXCategory.Combat => CombatBudget,
        VFXCategory.Environment => EnvironmentBudget,
        VFXCategory.Ability => AbilityBudget,
        VFXCategory.Death => DeathBudget,
        VFXCategory.UI => UIBudget,
        VFXCategory.Ambient => AmbientBudget,
        VFXCategory.Interaction => InteractionBudget,
        _ => 8
    };

    public static VFXBudgetConfig Default => new()
    {
        CombatBudget = 16,
        EnvironmentBudget = 24,
        AbilityBudget = 12,
        DeathBudget = 8,
        UIBudget = 20,
        AmbientBudget = 10,
        InteractionBudget = 8,
        GlobalMaxPerFrame = 64
    };
}
```

### 1.4 VFX LOD Configuration

```csharp
/// <summary>
/// ECS singleton for VFX LOD distance thresholds.
/// Extends existing EffectLODTier with project-wide configurable distances.
/// </summary>
public struct VFXLODConfig : IComponentData
{
    /// <summary>Distance below which Full LOD applies. All effects visible.</summary>
    public float FullDistance;           // Default: 15.0

    /// <summary>Distance below which Reduced LOD applies. 50% particles, no sub-emitters.</summary>
    public float ReducedDistance;        // Default: 40.0

    /// <summary>Distance below which Minimal LOD applies. Billboard/sprite only.</summary>
    public float MinimalDistance;        // Default: 80.0

    /// <summary>Beyond MinimalDistance, VFX are culled entirely.</summary>
    // Culled = distance >= MinimalDistance (implicit)

    public static VFXLODConfig Default => new()
    {
        FullDistance = 15f,
        ReducedDistance = 40f,
        MinimalDistance = 80f
    };
}
```

### Systems

- **`VFXBudgetConfigBootstrapSystem`** (SimulationSystemGroup, runs once)
    - Creates `VFXBudgetConfig` singleton with defaults if none exists from authoring
    - Creates `VFXLODConfig` singleton with defaults if none exists from authoring
    - Ensures singletons are available before any producer system runs

- **`VFXBudgetSystem`** (SimulationSystemGroup, OrderLast)
    - Queries all `VFXRequest` entities this frame (no `VFXCulled`)
    - Groups by `VFXCategory`, counts per category
    - For each category exceeding its budget: sort by `Priority` descending, enable `VFXCulled` on lowest-priority excess requests
    - Enforces `GlobalMaxPerFrame` across all categories
    - Burst-compatible: uses `EntityQuery` + `NativeArray` sort
    - Profiler marker: `VFXBudgetSystem.Cull`

- **`VFXLODSystem`** (SimulationSystemGroup, OrderLast, after VFXBudgetSystem)
    - Queries all `VFXRequest` entities without `VFXCulled`
    - Computes distance from `VFXRequest.Position` to main camera position
    - Assigns LOD tier based on `VFXLODConfig` thresholds
    - Enables `VFXCulled` on requests beyond `MinimalDistance`
    - Stores computed LOD tier in a transient `VFXResolvedLOD` component for execution
    - Burst-compatible: camera position passed as singleton or system state

- **`VFXExecutionSystem`** (Managed, PresentationSystemGroup, ClientSimulation | LocalSimulation)
    - Queries `VFXRequest` + `VFXResolvedLOD` without `VFXCulled`
    - Resolves `VFXTypeId` via `VFXTypeDatabase` (loaded from Resources)
    - LOD prefab selection: Full → main prefab, Reduced → `ReducedPrefab` fallback to main with emission halving, Minimal → `MinimalPrefab` fallback to skip
    - Calls `VFXManager.SpawnVFX(prefab, position, rotation)` for pooled instantiation
    - Applies parameterization post-spawn:
        - `Scale`: `go.transform.localScale = Vector3.one * request.Scale`
        - `ColorTint`: if non-zero, `ps.main.startColor = new Color(tint.x, tint.y, tint.z, tint.w)`
        - `Intensity`: modulates `emission.rateOverTimeMultiplier *= request.Intensity`
        - `Duration`: if > 0, overrides `ParticleSystem.main.duration` and schedules `Destroy` accordingly
    - Destroys processed request entities via ECB
    - Telemetry: increments `VFXTelemetry` counters (spawned per category, pool hits, culled)

- **`VFXCleanupSystem`** (PresentationSystemGroup, after VFXExecutionSystem)
    - Destroys ALL remaining `VFXRequest` entities (culled, unresolved type, errors)
    - Guarantees zero request entity accumulation across frames
    - Safety net: if `VFXExecutionSystem` is disabled/removed, requests still get cleaned up

```csharp
/// <summary>
/// Transient component added by VFXLODSystem with the resolved LOD tier.
/// Read by VFXExecutionSystem for prefab variant selection.
/// Destroyed along with the VFXRequest entity.
/// </summary>
public struct VFXResolvedLOD : IComponentData
{
    public EffectLODTier Tier;
    public float DistanceToCamera;
}
```

### Authoring

- **`VFXBudgetConfigAuthoring`** MonoBehaviour — Exposes all `VFXBudgetConfig` fields in inspector. Place in subscene.
- **`VFXLODConfigAuthoring`** MonoBehaviour — Exposes `VFXLODConfig` distance thresholds. Place in subscene.

### Implementation Tasks

- [x] Create `VFXCategory` enum
- [x] Create `VFXRequest` IComponentData with all fields (Position, Rotation, VFXTypeId, Category, Intensity, Scale, ColorTint, Duration, SourceEntity, Priority)
- [x] Create `VFXCulled` IComponentData + IEnableableComponent
- [x] Create `VFXResolvedLOD` IComponentData
- [x] Create `VFXBudgetConfig` IComponentData singleton with `Default` factory and `GetBudget()` method
- [x] Create `VFXLODConfig` IComponentData singleton with `Default` factory
- [x] Create `VFXTypeEntry` struct and `VFXTypeDatabase` ScriptableObject with `TryGetEntry()`, inspector list, runtime dictionary
- [x] Create `VFXBudgetConfigAuthoring` MonoBehaviour + Baker
- [x] Create `VFXLODConfigAuthoring` MonoBehaviour + Baker
- [x] Implement `VFXBudgetConfigBootstrapSystem` — singleton creation with defaults
- [x] Implement `VFXBudgetSystem` — per-category counting, priority sort, culling via VFXCulled enable
- [x] Implement `VFXLODSystem` — distance computation, LOD tier assignment, VFXResolvedLOD creation, culling
- [x] Implement `VFXExecutionSystem` — managed, PresentationSystemGroup, VFXTypeDatabase resolution, VFXManager delegation, parameterization (scale, tint, intensity, duration), entity cleanup
- [x] Implement `VFXCleanupSystem` — destroy all remaining VFXRequest entities
- [x] Create `VFXTelemetry` static counters (spawned per category, culled per category, pool hits, total this frame)
- [ ] Create initial `VFXTypeDatabase` asset in `Resources/VFXTypeDatabase` with placeholder entries
- [ ] **Test:** Create 100 VFXRequest entities in one frame, verify budget system culls excess per category
- [ ] **Test:** VFXRequest at 50m uses Reduced prefab variant, at 90m is culled
- [ ] **Test:** Remove VFXExecutionSystem from world — verify VFXCleanupSystem prevents entity accumulation
- [ ] **Test:** Burst-compiled system creates VFXRequest via ECB — verify no managed code required on request side

---

## Phase 2: Legacy Queue Bridge Adapters

### Problem

Existing systems use four independent static queue patterns. These must continue working during migration. New systems should use `VFXRequest` entities directly, but existing producers must not be rewritten all at once.

### Bridge Pattern

Each legacy bridge is a **managed system in PresentationSystemGroup** that drains its static queue and creates `VFXRequest` entities. The existing consumer systems are then disabled (or left as fallbacks during migration).

### Systems

- **`SurfaceImpactVFXBridgeSystem`** (Managed, PresentationSystemGroup, before VFXExecutionSystem)
    - Drains `SurfaceImpactQueue`
    - Maps `SurfaceImpactData` fields to `VFXRequest`:
        - `Position` → `Position`
        - `Normal` → compute `Rotation` via `Quaternion.LookRotation`
        - `SurfaceMaterialId` → resolve to `VFXTypeId` via material→VFX mapping table
        - `ImpactClass` → map to `VFXCategory.Combat` or `VFXCategory.Environment`
        - `Intensity` → `Intensity`
        - `LODTier` → passed through (already computed by producers)
    - **Migration note:** Once all surface impact producers create `VFXRequest` entities directly, this bridge and `SurfaceImpactPresenterSystem.SpawnVFX()` become obsolete

- **`GroundEffectVFXBridgeSystem`** (Managed, PresentationSystemGroup, before VFXExecutionSystem)
    - Drains `GroundEffectQueue`
    - Maps `GroundEffectRequest` fields to `VFXRequest`:
        - `Position` → `Position`
        - `EffectType` → resolve to `VFXTypeId` via `GroundEffectType`→VFXTypeId mapping
        - `Radius` → `Scale` (normalized to reference radius)
        - `Duration` → `Duration`
        - `Intensity` → `Intensity`
        - Category = `VFXCategory.Ability`
    - Does NOT bridge decal spawning — decals remain in `AbilityGroundEffectSystem` (separate pipeline)

- **`DamageVisualVFXBridgeSystem`** (Managed, PresentationSystemGroup, before VFXExecutionSystem)
    - Drains `DamageVisualQueue` for VFX-producing damage events only (hit flashes, blood splatter)
    - Does NOT replace damage number UI routing — `CombatUIBridgeSystem` continues to read `DamageVisualQueue` independently for number display
    - Maps `DamageVisualData` to `VFXRequest` for hit-reaction VFX (blood, sparks, elemental burst):
        - `HitPosition` → `Position`
        - `DamageType` → resolve to `VFXTypeId` (Physical=blood, Fire=ember, Ice=frost, etc.)
        - `HitType` → `Priority` (Critical = high priority)
        - Category = `VFXCategory.Combat`
    - **Optional:** Only active if `EnableDamageHitVFX` flag is set in config (default: false until VFX assets exist)

### Bridge Activation Strategy

```
Phase 2 (this phase):
    Legacy queues → Bridge systems → VFXRequest entities → VFXExecutionSystem → VFXManager
    (Legacy consumer systems remain active as fallback during testing)

Phase 2 verified:
    Disable legacy consumer VFX spawning (keep audio/decal paths)
    Bridge systems become the sole VFX path

Future (per-system migration):
    Producer systems create VFXRequest entities directly
    Bridge systems become no-ops (empty queues)
    Bridge systems removed
```

### Implementation Tasks

- [x] Implement `SurfaceImpactVFXBridgeSystem` — drain SurfaceImpactQueue, create VFXRequest entities per impact
- [x] Create `SurfaceMaterialToVFXTypeMapping` lookup (SurfaceMaterialId → VFXTypeId) — inline switch in bridge system
- [x] Implement `GroundEffectVFXBridgeSystem` — drain GroundEffectQueue, create VFXRequest entities per effect
- [x] Create `GroundEffectTypeToVFXTypeMapping` lookup (GroundEffectType → VFXTypeId) — inline switch in bridge system
- [x] Implement `DamageVisualVFXBridgeSystem` — static NotifyDamageVisual() feed for hit VFX (not damage numbers)
- [x] Create `DamageTypeToVFXTypeMapping` lookup (DamageType → VFXTypeId) — inline switch in bridge system
- [x] Add `VFXTypeIds` static class with well-known type ID constants
- [x] Bridge activation: DamageVisualVFXBridgeSystem disabled by default (no VFX assets yet)
- [ ] **Test:** Fire weapon → SurfaceImpactQueue receives data → bridge creates VFXRequest → VFXExecutionSystem spawns VFX → identical to direct SurfaceImpactPresenterSystem path
- [ ] **Test:** Cast AOE ability → GroundEffectQueue → bridge → VFXRequest → execution → VFX matches existing ground effect
- [ ] **Test:** Disable all bridges → legacy systems still work (fallback path)
- [ ] **Test:** Enable bridge + disable legacy VFX path → VFX still renders via new pipeline

---

## Phase 3: Per-Category Budget Throttling

### Problem

The current global throttle (`VFXManager.MaxSpawnsPerSecond = 30`) does not differentiate between a critical boss death explosion and an ambient dust mote. In a 40-enemy combat encounter, surface impact VFX can consume the entire budget, leaving zero for ability effects or death VFX. Players perceive this as "abilities have no VFX" — a visual quality regression.

### Budget Allocation Strategy

```
Default Budget Per Frame (60 FPS):

    Combat:      16  (weapon impacts, muzzle flashes, projectile trails)
    Environment: 24  (footsteps, water, weather — high volume, low priority)
    Ability:     12  (casts, AOE, buffs — medium volume, high visual importance)
    Death:        8  (gibs, blood, dissolve — low volume, high impact)
    UI:          20  (damage numbers, pickups — must never be dropped)
    Ambient:     10  (fireflies, dust — pure atmosphere, lowest priority)
    Interaction:  8  (crafting sparks, loot glow — low volume)
    ─────────────────
    Total:       98  (but GlobalMaxPerFrame = 64 enforces hard cap)
```

The per-category budgets intentionally sum to more than `GlobalMaxPerFrame`. In practice, not all categories are active simultaneously. The global cap is a safety valve for edge cases.

### Priority-Based Culling Within Category

When a category exceeds its budget:
1. Sort requests by `Priority` descending
2. Keep the top N (where N = category budget)
3. Enable `VFXCulled` on the rest

Priority guidelines:
- **100+**: Boss mechanics, player death, critical feedback (never cull these — assign to high-budget category)
- **50-99**: Ability effects, important combat hits
- **10-49**: Standard weapon impacts, footsteps
- **0**: Default — ambient, environmental
- **Negative**: Ultra-low priority — first to be culled

### Dynamic Budget Scaling

```csharp
/// <summary>
/// Optional singleton for runtime budget adjustment based on frame performance.
/// If frame time exceeds target, budgets are scaled down. If under target, scaled up.
/// </summary>
public struct VFXDynamicBudget : IComponentData
{
    /// <summary>Enable dynamic scaling. Default false (static budgets only).</summary>
    public bool Enabled;

    /// <summary>Target frame time in milliseconds. Budgets reduce when exceeded.</summary>
    public float TargetFrameTimeMs;     // Default: 16.67 (60 FPS)

    /// <summary>Minimum budget multiplier. Never scale below this. Default 0.25 (25%).</summary>
    public float MinBudgetMultiplier;

    /// <summary>Maximum budget multiplier. Never scale above this. Default 1.5 (150%).</summary>
    public float MaxBudgetMultiplier;

    /// <summary>Current computed multiplier (updated each frame by VFXBudgetSystem).</summary>
    public float CurrentMultiplier;

    /// <summary>Smoothing factor for multiplier changes. Higher = slower adaptation.</summary>
    public float SmoothingFrames;       // Default: 30 (half-second at 60fps)

    public static VFXDynamicBudget Default => new()
    {
        Enabled = false,
        TargetFrameTimeMs = 16.67f,
        MinBudgetMultiplier = 0.25f,
        MaxBudgetMultiplier = 1.5f,
        CurrentMultiplier = 1.0f,
        SmoothingFrames = 30f
    };
}
```

### Systems

- **`VFXBudgetSystem`** (enhanced from Phase 1)
    - Reads `VFXDynamicBudget` singleton (if present)
    - If dynamic scaling enabled: measure previous frame time, smooth-interpolate `CurrentMultiplier`, apply to all category budgets
    - Per-category culling with priority sort (unchanged from Phase 1)
    - Telemetry: writes per-category cull counts to `VFXTelemetry`

### Implementation Tasks

- [x] Create `VFXDynamicBudget` IComponentData with `Default` factory
- [x] Add `VFXDynamicBudget` creation to `VFXBudgetConfigBootstrapSystem` (disabled by default)
- [x] Enhance `VFXBudgetSystem` to read frame time and compute dynamic multiplier
- [x] Implement smooth multiplier interpolation (exponential moving average over `SmoothingFrames`)
- [x] Apply multiplier to per-category budgets: `effectiveBudget = (int)(baseBudget * CurrentMultiplier)`
- [x] Add per-category telemetry to `VFXTelemetry`: requested, executed, culled (per category per frame)
- [ ] **Test:** Spawn 200 Combat VFX requests in one frame → only 16 execute, 184 culled
- [ ] **Test:** High-priority (100) requests survive when low-priority (0) are culled
- [ ] **Test:** Dynamic budget: artificially increase frame time → multiplier decreases → fewer VFX spawn
- [ ] **Test:** Dynamic budget disabled → static budgets apply regardless of frame time

---

## Phase 4: VFX LOD System

### Problem

`VFXManager` has a binary 50m hard cutoff. `SurfaceImpactPresenterSystem` has a 4-tier LOD but it is local to surface impacts. Other VFX subsystems (ground effects, death VFX, ability effects) have no LOD awareness at all — a full particle system plays identically at 5m and 75m.

### LOD Tiers (Project-Wide Standard)

| Tier | Distance | Behavior |
|------|----------|----------|
| **Full** | 0-15m | All particles, sub-emitters, trails, decals. Full emission rate. |
| **Reduced** | 15-40m | 50% emission rate. No sub-emitters. Use `ReducedPrefab` if available. |
| **Minimal** | 40-80m | Single billboard sprite or simplified 2-particle burst. Use `MinimalPrefab` if available. Skip if no minimal variant exists. |
| **Culled** | 80m+ | Skip entirely. Entity destroyed without spawning. |

### LOD Application in VFXExecutionSystem

```
LOD Tier → Prefab Selection:
    Full    → VFXTypeEntry.Prefab
    Reduced → VFXTypeEntry.ReducedPrefab ?? VFXTypeEntry.Prefab (with emission halved)
    Minimal → VFXTypeEntry.MinimalPrefab ?? skip (no VFX)

LOD Tier → Post-Spawn Modifications:
    Full    → No changes
    Reduced → ParticleSystem.emission.rateOverTimeMultiplier *= 0.5
              Disable sub-emitters: ps.subEmitters.enabled = false
    Minimal → No ParticleSystem modifications (minimal prefab is pre-configured)
```

### Systems

- **`VFXLODSystem`** (enhanced from Phase 1)
    - Reads `VFXLODConfig` singleton for distance thresholds
    - Per-request entity: compute distance, assign tier, create `VFXResolvedLOD`
    - Per-type override: if `VFXTypeEntry.MinimumLODTier > computedTier`, use the entry's minimum (some effects should never be reduced)
    - Burst-compiled main loop

### Implementation Tasks

- [x] Implement LOD tier computation in `VFXLODSystem` using `VFXLODConfig` thresholds
- [x] Implement `MinimumLODTier` per-type override in `VFXExecutionSystem` (skips below minimum)
- [x] Implement LOD prefab variant selection in `VFXExecutionSystem` (Full/Reduced/Minimal prefab chain)
- [x] Implement post-spawn emission reduction for Reduced tier (when no ReducedPrefab exists)
- [x] Implement sub-emitter disabling for Reduced tier
- [x] Implement Minimal tier skip-if-no-variant behavior
- [x] Add LOD tier distribution to `VFXTelemetry` (count per tier per frame)
- [ ] **Test:** VFX at 10m = Full (all particles), same VFX at 25m = Reduced (half particles), at 60m = Minimal (billboard), at 90m = no VFX
- [ ] **Test:** VFXTypeEntry with MinimumLODTier=Full at 30m still renders Full (override)
- [ ] **Test:** LOD thresholds change at runtime via VFXLODConfig modification → immediate effect

---

## Phase 5: Dissolve/Alpha Fade Shader

### Problem

EPIC 16.3 `CorpseSinkSystem` uses position-based sinking because no dissolve shader exists. The result looks mechanical — corpses slide into the floor like an elevator. AAA games use edge-dissolve with emissive burn lines, noise-based alpha erosion, or vertex displacement for organic fade-out. This phase provides the shader infrastructure that EPIC 16.3 Phase 4.1 is blocked on.

### Shader: `DIG/URP/Dissolve`

A URP Shader Graph shader with the following properties:

```
_BaseMap            (Texture2D)  — Albedo texture (existing)
_BumpMap            (Texture2D)  — Normal map (existing)
_MetallicGlossMap   (Texture2D)  — Metallic/Smoothness (existing)
_DissolveAmount     (Float, 0-1) — 0 = fully visible, 1 = fully dissolved
_DissolveNoise      (Texture2D)  — Noise texture driving dissolve edge pattern
_DissolveEdgeWidth  (Float)      — Width of the emissive burn edge in UV space
_DissolveEdgeColor  (Color)      — HDR color of the dissolve edge (default: orange-white)
_DissolveDirection  (Vector)     — World-space direction for directional dissolve (0,1,0 = bottom-up)
_UseDirectional     (Float, 0/1) — Toggle noise-based vs directional dissolve
```

### Shader Behavior

```
Noise-based dissolve (_UseDirectional = 0):
    float noise = tex2D(_DissolveNoise, uv).r;
    float alpha = step(_DissolveAmount, noise);
    float edge = smoothstep(_DissolveAmount - _DissolveEdgeWidth, _DissolveAmount, noise)
               * (1 - alpha);
    emission += edge * _DissolveEdgeColor;
    clip(alpha - 0.001);

Directional dissolve (_UseDirectional = 1):
    float height = dot(worldPos, normalize(_DissolveDirection));
    float heightNorm = remap(height, minBounds, maxBounds, 0, 1);
    float dissolveThreshold = heightNorm + tex2D(_DissolveNoise, uv).r * 0.3;
    float alpha = step(_DissolveAmount, dissolveThreshold);
    float edge = smoothstep(_DissolveAmount - _DissolveEdgeWidth, _DissolveAmount, dissolveThreshold)
               * (1 - alpha);
    emission += edge * _DissolveEdgeColor;
    clip(alpha - 0.001);
```

### CorpseSinkSystem Enhancement

```csharp
/// <summary>
/// Enhanced CorpseSinkSystem with dissolve shader support.
/// When dissolve materials are detected, drives _DissolveAmount instead of position sinking.
/// Falls back to position sinking for non-dissolve materials.
/// </summary>
// Modifications to existing CorpseSinkSystem:
//   1. Add MaterialPropertyBlock cache per entity
//   2. Detect if entity's Renderer uses DIG/URP/Dissolve shader
//   3. If dissolve shader: lerp _DissolveAmount from 0→1 over FadeOutDuration
//   4. If no dissolve shader: existing position sink behavior (unchanged)
//
// NOTE: This requires converting CorpseSinkSystem from ISystem (Burst) to SystemBase (managed)
// for Renderer/MaterialPropertyBlock access, OR using a companion managed system.
```

### Companion System Pattern

To preserve the Burst-compiled `CorpseSinkSystem` for position sinking while adding dissolve support:

```csharp
/// <summary>
/// Managed companion to CorpseSinkSystem that drives dissolve shader parameters.
/// Runs after CorpseSinkSystem. If an entity has a dissolve-capable material,
/// this system handles fade-out via _DissolveAmount and skips position sinking.
/// </summary>
// File: Assets/Scripts/VFX/Systems/CorpseDissolveSystem.cs
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: CorpseSinkSystem
// WorldSystemFilter: ClientSimulation | LocalSimulation
// Pattern: GhostPresentationGameObjectSystem to access Renderers
```

```csharp
/// <summary>
/// Tag component added during baking when an entity's material supports dissolve.
/// Presence of this tag causes CorpseSinkSystem to skip position sinking for this entity,
/// deferring to CorpseDissolveSystem for visual fade.
/// </summary>
public struct DissolveCapable : IComponentData { }

/// <summary>
/// Runtime state for dissolve animation. Added by CorpseDissolveSystem when fade begins.
/// </summary>
public struct DissolveState : IComponentData, IEnableableComponent
{
    /// <summary>Current dissolve amount [0-1]. Driven by CorpseDissolveSystem.</summary>
    public float Amount;

    /// <summary>Dissolve rate (amount per second). Computed from FadeOutDuration.</summary>
    public float Rate;
}
```

### Implementation Tasks

- [x] Create `DIG/URP/Dissolve` HLSL shader with all properties (_DissolveAmount, _DissolveNoise, _DissolveEdgeWidth, _DissolveEdgeColor, _DissolveDirection, _UseDirectional)
- [x] Implement noise-based dissolve path in shader (alpha clip + emissive edge)
- [x] Implement directional dissolve path in shader (height + noise hybrid)
- [ ] Create default dissolve noise texture (Perlin-based, 256x256, single channel) — requires Unity asset creation
- [x] Create `DissolveCapable` IComponentData tag
- [x] Create `DissolveState` IComponentData + IEnableableComponent
- [x] Implement `CorpseDissolveSystem` (managed, PresentationSystemGroup, after CorpseSinkSystem)
    - Access Renderer via CompanionLink
    - Detect dissolve-capable materials (check for _DissolveAmount property)
    - Drive `_DissolveAmount` via MaterialPropertyBlock from 0→1 over FadeOutDuration
    - Skip entities without dissolve materials (fall through to CorpseSinkSystem position sink)
- [x] Modify `CorpseSinkSystem` to skip entities with `DissolveCapable` tag (`.WithNone<DissolveCapable>()`)
- [x] Create `DissolveCapableAuthoring` MonoBehaviour + Baker (auto-detects dissolve shader on renderers)
- [ ] Create sample dissolve material instance using DIG/URP/Dissolve shader — requires Unity asset creation
- [ ] Apply dissolve material to BoxingJoe enemy for testing — requires Unity prefab modification
- [ ] **Test:** Enemy dies → dissolve animation plays (emissive edge crawls across mesh) → entity destroyed
- [ ] **Test:** Enemy without dissolve material → position sink (existing behavior unchanged)
- [ ] **Test:** Dissolve `_DissolveAmount` driven from 0 to 1 over configured FadeOutDuration
- [ ] **Test:** Directional dissolve (_UseDirectional=1) — dissolve sweeps bottom-to-top
- [ ] **Test:** URP compatibility — shader renders correctly in URP with forward and deferred renderers

---

## Phase 6: VFX Authoring & Designer Tooling

### 6.1 VFX Request Authoring Helper

```csharp
/// <summary>
/// Convenience authoring component for placing VFX-emitting entities in scenes.
/// Bakes a VFXRequest template that systems can clone at runtime.
/// Used for ambient VFX points, spawn effects, zone entry effects, etc.
/// </summary>
// File: Assets/Scripts/VFX/Authoring/VFXEmitterAuthoring.cs
public class VFXEmitterAuthoring : MonoBehaviour
{
    [Header("VFX Configuration")]
    public int VFXTypeId;
    public VFXCategory Category = VFXCategory.Ambient;
    public float Intensity = 1.0f;
    public float Scale = 1.0f;
    public Color ColorTint = Color.clear;   // Clear = no override
    public float Duration = 0f;             // 0 = use default
    public int Priority = 0;

    [Header("Emission")]
    public VFXEmissionMode EmissionMode = VFXEmissionMode.OneShot;
    public float RepeatInterval = 1.0f;     // For Repeating mode

    [Header("Trigger")]
    public float TriggerRadius = 0f;        // 0 = emit immediately, >0 = proximity trigger
}

public enum VFXEmissionMode : byte
{
    OneShot = 0,    // Emit once on entity creation
    Repeating = 1,  // Emit every RepeatInterval seconds
    Proximity = 2   // Emit when player enters TriggerRadius
}
```

### 6.2 VFX Type ID Constants

```csharp
/// <summary>
/// Well-known VFX type IDs for code references. Matches entries in VFXTypeDatabase.
/// Systems that create VFXRequest entities use these constants for type-safe references.
/// </summary>
public static class VFXTypeIds
{
    // ─── Combat ───
    public const int BulletImpactDefault = 1000;
    public const int BulletImpactMetal = 1001;
    public const int BulletImpactDirt = 1002;
    public const int BulletImpactFlesh = 1003;
    public const int BulletImpactWater = 1004;
    public const int MuzzleFlashRifle = 1010;
    public const int MuzzleFlashPistol = 1011;
    public const int MuzzleFlashShotgun = 1012;
    public const int ProjectileTrailDefault = 1020;
    public const int ProjectileTrailFire = 1021;
    public const int ProjectileTrailIce = 1022;

    // ─── Ability / Elemental ───
    public const int AbilityFireBurst = 2000;
    public const int AbilityIceBurst = 2001;
    public const int AbilityLightningStrike = 2002;
    public const int AbilityPoisonCloud = 2003;
    public const int AbilityHolySmite = 2004;
    public const int AbilityShadowBlast = 2005;
    public const int AbilityArcanePulse = 2006;
    public const int BuffApply = 2100;
    public const int DebuffApply = 2101;

    // ─── Death ───
    public const int DeathBloodSplatter = 3000;
    public const int DeathGibExplosion = 3001;
    public const int DeathDissolve = 3002;
    public const int DeathSoulRelease = 3003;

    // ─── Environment ───
    public const int FootstepDust = 4000;
    public const int FootstepWater = 4001;
    public const int WaterSplashSmall = 4010;
    public const int WaterSplashLarge = 4011;

    // ─── Interaction / UI ───
    public const int LootGlow = 5000;
    public const int PickupFlash = 5001;
    public const int InteractionComplete = 5002;
    public const int LevelUp = 5003;

    // ─── Ambient ───
    public const int AmbientDust = 6000;
    public const int AmbientFireflies = 6001;
    public const int AmbientEmber = 6002;
}
```

### 6.3 VFX Debug Window

```
Assets/Editor/VFXWorkstation/
├── VFXWorkstationWindow.cs         // EditorWindow with tabs
├── VFXWorkstationStyles.cs         // Shared GUIStyles
└── Modules/
    ├── BudgetMonitorModule.cs      // Live per-category budget usage bars
    ├── LODVisualizerModule.cs      // Scene view LOD distance rings around camera
    ├── RequestLogModule.cs         // Scrolling log of recent VFX requests (type, position, category, culled?)
    └── RegistryBrowserModule.cs    // Browse VFXTypeDatabase entries, preview prefabs
```

- **Budget Monitor Tab:** Horizontal bars per category showing requests/budget ratio. Red when exceeding. Updates every frame in play mode.
- **LOD Visualizer Tab:** Toggle scene-view wire spheres at Full/Reduced/Minimal/Culled distances. Color-coded. Centered on main camera.
- **Request Log Tab:** Ring buffer of last 100 VFX requests. Columns: Frame, TypeId, Category, Position, Priority, LOD Tier, Result (Spawned/Culled/NoType).
- **Registry Browser Tab:** List all VFXTypeDatabase entries. Click to ping prefab. Preview button spawns in scene. Validate button checks for missing prefabs.

### Implementation Tasks

- [x] Create `VFXEmitterAuthoring` MonoBehaviour + Baker
- [x] Create `VFXEmissionMode` enum
- [x] Implement `VFXEmitterSystem` (PresentationSystemGroup) — reads VFXEmitter state, creates VFXRequest entities on schedule (OneShot/Repeating/Proximity)
- [x] Create `VFXTypeIds` static class with well-known ID constants
- [x] Create `VFXWorkstationWindow` EditorWindow with 4-tab layout (Budget/LOD/Log/Registry)
- [x] Implement Budget Monitor tab — reads VFXTelemetry per-category counters, draws colored horizontal bars
- [x] Implement LOD Visualizer tab — displays distance thresholds and per-tier execution counts
- [x] Implement Request Log tab — displays VFXTelemetry counters and debug logging info
- [x] Implement Registry Browser tab — loads VFXTypeDatabase, lists entries, ping/create buttons
- [ ] **Test:** Place VFXEmitterAuthoring in subscene with Repeating mode → VFX spawns every N seconds in play mode
- [ ] **Test:** VFX Workstation Budget Monitor shows correct per-category fill during combat

---

## Phase 7: Quality Presets & Runtime Configuration

### Problem

Different hardware and game genres require different VFX budgets. A high-end PC running a 10-player raid needs different settings than a Steam Deck running solo survival. Designers need presets, and players need a settings slider.

### Quality Presets

```csharp
/// <summary>
/// Predefined VFX quality levels. Applied via VFXQualityApplySystem when changed.
/// </summary>
public enum VFXQualityPreset : byte
{
    Ultra = 0,      // Maximum VFX, no culling under 120m, full emission
    High = 1,       // Default — standard budgets, 80m cull
    Medium = 2,     // 50% budgets, 60m cull, no sub-emitters
    Low = 3,        // 25% budgets, 40m cull, minimal prefabs only
    Minimal = 4     // 10% budgets, 20m cull, critical VFX only (death, UI)
}
```

| Setting | Ultra | High | Medium | Low | Minimal |
|---------|-------|------|--------|-----|---------|
| GlobalMaxPerFrame | 128 | 64 | 32 | 16 | 8 |
| Combat Budget | 32 | 16 | 8 | 4 | 2 |
| Environment Budget | 48 | 24 | 12 | 6 | 2 |
| Ability Budget | 24 | 12 | 6 | 3 | 2 |
| Death Budget | 16 | 8 | 4 | 2 | 2 |
| UI Budget | 32 | 20 | 16 | 12 | 8 |
| Ambient Budget | 24 | 10 | 4 | 0 | 0 |
| Interaction Budget | 16 | 8 | 4 | 2 | 1 |
| LOD Full Distance | 25m | 15m | 10m | 8m | 5m |
| LOD Reduced Distance | 60m | 40m | 30m | 20m | 12m |
| LOD Minimal Distance | 120m | 80m | 60m | 40m | 20m |
| Dynamic Budget | On (1.5x max) | Off | On (0.5x max) | On (0.25x max) | On (0.1x max) |

### Systems

- **`VFXQualityApplySystem`** (SimulationSystemGroup, runs when preset changes)
    - Reads `VFXQualityPreset` from a singleton or settings bridge
    - Writes preset values to `VFXBudgetConfig` and `VFXLODConfig` singletons
    - Updates `VFXDynamicBudget` settings per preset
    - Only runs when preset value changes (dirty flag check)

### Implementation Tasks

- [x] Create `VFXQualityPreset` enum
- [x] Create preset value table (switch statement mapping preset → config values for all 5 presets)
- [x] Create `VFXQualityState` IComponentData singleton (`VFXQualityPreset CurrentPreset`, `bool IsDirty`)
- [x] Implement `VFXQualityApplySystem` — reads VFXQualityState, applies preset to VFXBudgetConfig + VFXLODConfig + VFXDynamicBudget
- [x] Create `VFXQualityAuthoring` MonoBehaviour for setting initial quality preset in subscene
- [ ] Wire quality preset to game settings menu (if settings UI exists) — deferred to UI EPIC
- [ ] **Test:** Switch from High to Low at runtime → budget caps halve, LOD distances shrink, VFX spawn rate drops
- [ ] **Test:** Switch from Low to Ultra → budgets increase, visual density increases
- [ ] **Test:** Quality preset persists across scene loads (stored in singleton, not per-entity)

---

## Design Considerations

### NetCode Safety

1. **VFXRequest entities are client-only.** They are created in `ClientSimulation` or `LocalSimulation` worlds only. They are never ghost-replicated. No `[GhostComponent]` attributes.
2. **No `IBufferElementData` on ghost entities.** All VFX state uses `IComponentData` on transient entities or singletons. This follows the project rule in MEMORY.md: "NEVER create new IBufferElementData on ghost-replicated entities."
3. **VFXBudgetConfig / VFXLODConfig are client-side singletons.** They exist only in the client world. Server has no VFX systems.
4. **CorpseDissolveSystem** reads `DeathState.StateStartTime` (ghost-replicated from server) but writes to `MaterialPropertyBlock` (client-only presentation). No server-side writes.
5. **WorldSystemFilter** on all VFX systems: `ClientSimulation | LocalSimulation`. No VFX systems in `ServerSimulation`.
6. **DissolveState** is NOT ghost-replicated. It is client-only state for driving the shader. Created and managed entirely in `PresentationSystemGroup`.

### Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| VFXBudgetSystem | < 0.05ms | Yes | NativeArray sort on ~100 requests max. O(N log N) per category. |
| VFXLODSystem | < 0.03ms | Yes | Single float3 distance per request. O(N). |
| VFXExecutionSystem | < 0.5ms | No (managed) | VFXManager.SpawnVFX is the bottleneck — pooling amortizes. Max 64 spawns/frame. |
| VFXCleanupSystem | < 0.02ms | Yes | Structural change (DestroyEntity). Batch ECB playback. |
| CorpseDissolveSystem | < 0.1ms | No (managed) | MaterialPropertyBlock.SetFloat per dissolving corpse. Max ~30 corpses. |
| Legacy bridges | < 0.1ms | No (managed) | Queue drain + entity creation. Transitional cost — removed after migration. |
| **Total VFX Budget** | **< 0.8ms** | — | All VFX systems combined. Well under 1ms target. |

### Memory Impact

| Resource | Size | Lifetime | Notes |
|----------|------|----------|-------|
| VFXRequest entity | ~128 bytes | 1 frame | Transient — created and destroyed same frame. Zero accumulation. |
| VFXTypeDatabase | ~4 KB | Session | ScriptableObject in Resources. ~50 entries typical. |
| VFXBudgetConfig singleton | 36 bytes | Session | Single entity. |
| VFXLODConfig singleton | 12 bytes | Session | Single entity. |
| VFXManager pools | Variable | Session | Existing — unchanged. Pool size controlled by VFXTypeEntry.PrewarmCount. |
| MaterialPropertyBlock cache | ~64 bytes/corpse | Corpse lifetime | CorpseDissolveSystem caches one MPB per dissolving entity. Max ~30. |

### Modularity Strategy

The system is designed for zero-impact removal:

1. **Remove VFXExecutionSystem** → VFXCleanupSystem still destroys request entities. No accumulation. No errors. Gameplay unaffected.
2. **Remove all VFX systems** → Producers still create VFXRequest entities (they are fire-and-forget). Entities accumulate for one frame, then... actually, we need VFXCleanupSystem OR use `CleanupComponent` pattern. See below.
3. **Structural safety:** `VFXRequest` entities include a `VFXCleanupTag : ICleanupComponentData` to ensure they are destroyed even if all VFX systems are removed. `VFXCleanupSystem` handles the normal case. Unity's structural change cleanup handles the edge case where even VFXCleanupSystem is missing (cleanup components are auto-processed).

```csharp
/// <summary>
/// Cleanup component ensuring VFXRequest entities are properly destroyed
/// even if VFX systems are removed from the world.
/// </summary>
public struct VFXCleanupTag : ICleanupComponentData { }
```

### Backward Compatibility

| Existing System | Migration Path | Breaking Changes |
|----------------|---------------|-----------------|
| `SurfaceImpactPresenterSystem` | Phase 2 bridge wraps its queue. Eventually, producers write VFXRequest directly. | None — bridge is additive. |
| `AbilityGroundEffectSystem` | Phase 2 bridge wraps GroundEffectQueue VFX path. Decal path stays in original system. | None — decals untouched. |
| `CombatUIBridgeSystem` | Damage numbers stay in existing pipeline. Only hit-reaction VFX routed via bridge. | None — DamageVisualQueue still consumed by existing system for numbers. |
| `CollisionVFXSystem` | Can be migrated to create VFXRequest entities instead of calling VFXManager directly. Low priority. | None — migration is optional. |
| `ItemVFXAuthoring` | Remains unchanged (MonoBehaviour pattern, not ECS pipeline). Could wrap calls in VFXRequest creation in future. | None. |
| `VFXManager` | Continues as the pool/instantiation backend. VFXExecutionSystem calls into it. | None — VFXManager gains consumers, not changes. |

### Integration with EPIC 16.3 (Corpse Management)

This EPIC unblocks EPIC 16.3 Phase 4.1 (alpha fade mode for corpses):

1. **Phase 5** of this EPIC delivers the `DIG/URP/Dissolve` shader with `_DissolveAmount`
2. **`CorpseDissolveSystem`** drives the shader property over `FadeOutDuration`
3. **`CorpseSinkSystem`** gains `.WithNone<DissolveCapable>()` to skip dissolve-capable entities
4. **Enemy prefabs** can be upgraded to use the dissolve shader material. Non-upgraded enemies continue sinking.
5. **Death VFX** can be emitted as `VFXRequest` entities from `DeathSpawnProcessingSystem` using VFXTypeIds.DeathDissolve, DeathBloodSplatter, etc.

### Integration with EPIC 15.32 (Enemy Ability Framework)

Ability systems can emit `VFXRequest` entities for cast VFX, projectile trails, and impact effects:

```csharp
// Example: Fire ability cast VFX
var ecb = ecbSystem.CreateCommandBuffer();
var e = ecb.CreateEntity();
ecb.AddComponent(e, new VFXRequest
{
    Position = casterPosition,
    Rotation = casterRotation,
    VFXTypeId = VFXTypeIds.AbilityFireBurst,
    Category = VFXCategory.Ability,
    Intensity = 1.0f,
    Scale = abilityRadius / referenceRadius,
    ColorTint = default, // Use prefab default
    Duration = 0f,       // Use prefab default
    SourceEntity = casterEntity,
    Priority = 50
});
ecb.AddComponent<VFXCulled>(e); // Baked disabled — VFXBudgetSystem decides
ecb.SetComponentEnabled<VFXCulled>(e, false);
ecb.AddComponent<VFXCleanupTag>(e);
```

### Thread Safety & Static Queue Coexistence

During the migration period, both old (static queues) and new (entity-based) VFX paths are active:

1. **Static queues are main-thread only.** `Queue<T>` is not thread-safe. All existing producers (managed systems) enqueue on the main thread. This is unchanged.
2. **VFXRequest entities use ECB.** Burst jobs write to `EntityCommandBuffer.ParallelWriter`. ECB playback is deterministic and thread-safe. No contention with static queues.
3. **Double-spawning risk:** If a producer writes to BOTH the legacy queue AND creates a VFXRequest entity, the VFX spawns twice. Migration must be atomic per-producer: switch from queue to entity, never both.
4. **Bridge systems drain queues.** If a bridge is active AND the legacy consumer is active, VFX spawns twice. Solution: when enabling a bridge, disable the corresponding legacy consumer's VFX path (not audio/decal path).

---

## Integration Points

| System | EPIC | Integration |
|--------|------|-------------|
| VFXManager (pooling backend) | Core | VFXExecutionSystem delegates all instantiation to VFXManager.SpawnVFX() |
| SurfaceImpactPresenterSystem | 15.24 | Phase 2 bridge wraps SurfaceImpactQueue. Eventually migrated to VFXRequest. |
| AbilityGroundEffectSystem | 15.24 | Phase 2 bridge wraps GroundEffectQueue. Decal path unchanged. |
| CombatUIBridgeSystem | 15.28 | DamageVisualQueue continues for damage numbers. Hit VFX optionally routed. |
| CollisionVFXSystem | 7.4.4 | Optional migration target — low priority. |
| CorpseSinkSystem | 16.3 | Phase 5 provides dissolve shader + CorpseDissolveSystem. Sink system gains WithNone filter. |
| DeathSpawnProcessingSystem | 16.6 | Can create VFXRequest entities for death VFX instead of direct VFXManager calls. |
| Enemy Ability Systems | 15.32 | Create VFXRequest entities for cast/impact VFX. |
| EffectLODTier | 15.24 | Shared enum — VFXLODSystem uses existing tier values. No duplication. |
| ParadigmSurfaceConfig | 15.24 | Paradigm multipliers can feed into VFXBudgetConfig at runtime for genre adaptation. |
| Settings Menu | Future | VFXQualityPreset exposed as player-facing Graphics → VFX Quality setting. |

---

## File Structure

```
Assets/Scripts/VFX/
├── Components/
│   ├── VFXRequest.cs               // VFXRequest, VFXCulled, VFXResolvedLOD, VFXCleanupTag
│   ├── VFXCategory.cs              // VFXCategory enum
│   ├── VFXBudgetConfig.cs          // VFXBudgetConfig, VFXDynamicBudget singletons
│   ├── VFXLODConfig.cs             // VFXLODConfig singleton
│   ├── VFXQualityPreset.cs         // VFXQualityPreset enum, VFXQualityState
│   ├── VFXTypeIds.cs               // Static class with well-known type ID constants
│   └── DissolveComponents.cs       // DissolveCapable, DissolveState
├── Systems/
│   ├── VFXBudgetConfigBootstrapSystem.cs
│   ├── VFXBudgetSystem.cs
│   ├── VFXLODSystem.cs
│   ├── VFXExecutionSystem.cs       // Managed, PresentationSystemGroup
│   ├── VFXCleanupSystem.cs
│   ├── VFXEmitterSystem.cs         // OneShot/Repeating/Proximity emitter logic
│   ├── VFXQualityApplySystem.cs
│   └── CorpseDissolveSystem.cs     // Managed, PresentationSystemGroup
├── Bridges/
│   ├── SurfaceImpactVFXBridgeSystem.cs
│   ├── GroundEffectVFXBridgeSystem.cs
│   └── DamageVisualVFXBridgeSystem.cs
├── Data/
│   ├── VFXTypeDatabase.cs          // ScriptableObject
│   └── VFXTypeEntry.cs
├── Authoring/
│   ├── VFXBudgetConfigAuthoring.cs
│   ├── VFXLODConfigAuthoring.cs
│   ├── VFXEmitterAuthoring.cs
│   ├── VFXQualityAuthoring.cs
│   └── DissolveCapableAuthoring.cs
├── Debug/
│   └── VFXTelemetry.cs             // Static counters for debug window

Assets/Scripts/VFX/Shaders/
├── DIG_URP_Dissolve.shadergraph    // URP Shader Graph
└── Textures/
    └── DissolveNoise_256.png       // Default noise texture

Assets/Editor/VFXWorkstation/
├── VFXWorkstationWindow.cs
├── VFXWorkstationStyles.cs
└── Modules/
    ├── BudgetMonitorModule.cs
    ├── LODVisualizerModule.cs
    ├── RequestLogModule.cs
    └── RegistryBrowserModule.cs

Assets/Resources/
└── VFXTypeDatabase.asset           // Runtime-loaded ScriptableObject
```

---

## Verification Checklist

### Core Pipeline
- [ ] Burst-compiled system creates VFXRequest entity via ECB → VFX spawns on client
- [ ] Managed system creates VFXRequest entity → VFX spawns on client
- [ ] VFXRequest entity with invalid VFXTypeId → cleaned up without error (warning log)
- [ ] VFXRequest entity created on server world → ignored (no VFX systems in ServerSimulation)
- [ ] All VFXRequest entities destroyed by end of frame (zero accumulation after 1000 frames)

### Budget Throttling
- [ ] 100 Combat VFX requests → only CombatBudget (16) execute, rest culled
- [ ] High-priority requests survive budget culling, low-priority culled first
- [ ] Two categories at capacity simultaneously → each respects its own cap
- [ ] GlobalMaxPerFrame enforced when combined category budgets exceed it
- [ ] Dynamic budget: frame time spike → budget multiplier decreases → fewer VFX next frame

### LOD
- [ ] VFX at 10m = Full LOD (all particles, sub-emitters)
- [ ] VFX at 25m = Reduced LOD (half emission, no sub-emitters)
- [ ] VFX at 60m = Minimal LOD (billboard prefab or skip)
- [ ] VFX at 90m = Culled (no spawn, entity destroyed)
- [ ] VFXTypeEntry.MinimumLODTier overrides distance-based tier

### Legacy Bridges
- [ ] SurfaceImpactQueue → bridge → VFXRequest → execution = identical visual to direct presenter
- [ ] GroundEffectQueue → bridge → VFXRequest → execution = identical visual to direct system
- [ ] Disable all bridges → legacy systems still produce VFX (fallback path intact)
- [ ] Enable bridge + disable legacy VFX consumer → no double-spawning

### Dissolve Shader
- [ ] _DissolveAmount=0 → fully visible mesh
- [ ] _DissolveAmount=0.5 → partial dissolve with emissive edge
- [ ] _DissolveAmount=1 → fully invisible (all pixels clipped)
- [ ] Directional dissolve sweeps bottom-to-top
- [ ] CorpseDissolveSystem drives _DissolveAmount over FadeOutDuration
- [ ] Non-dissolve enemy → CorpseSinkSystem position sink (unchanged)
- [ ] Shader compiles in URP forward and deferred renderers

### Quality Presets
- [ ] Switch High → Low → budgets halve, LOD distances shrink
- [ ] Switch Low → Ultra → budgets quadruple, LOD distances extend
- [ ] Preset change takes effect within 1 frame

### Performance
- [ ] All VFX systems combined < 0.8ms at 64 requests/frame
- [ ] VFXBudgetSystem < 0.05ms with 100 requests
- [ ] VFXLODSystem < 0.03ms with 100 requests
- [ ] Zero GC allocations per frame in steady state (no List/Dictionary growth)
- [ ] VFXManager pool hit rate > 80% after warmup

### Modularity
- [ ] Remove VFXExecutionSystem → no errors, no VFX, entities cleaned up
- [ ] Remove all VFX systems → VFXCleanupTag ICleanupComponentData prevents entity leak
- [ ] No gameplay system references VFX components (cosmetic isolation)

### Multiplayer
- [ ] VFX spawns on local client — correct position and parameters
- [ ] VFX spawns on remote client via replicated state (DeathState, Health delta)
- [ ] No VFX entities in ServerSimulation world
- [ ] CorpseDissolveSystem works on both listen server (LocalSimulation) and dedicated (ClientSimulation)
