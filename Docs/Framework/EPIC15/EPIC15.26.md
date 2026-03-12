# EPIC 15.26: Smart HUD & Widget Ecosystem

**Status:** Planning
**Priority:** Medium-High (UI/UX)
**Dependencies:**
- EPIC 15.9: Combat UI Foundation (CombatUIRegistry, EnemyHealthBarPool, FloatingTextManager, InteractionRingPool)
- EPIC 15.14: Health Bar Visibility System (HealthBarVisibilityConfig, 17 visibility modes)
- EPIC 15.16: Target Lock UI (LockOnReticleUI)
- EPIC 15.18: Cursor Hover/Select (CursorHoverSystem)
- EPIC 15.20: Input Paradigm Framework (ParadigmStateMachine, InputParadigm enum, CameraMode enum)
- EPIC 15.25: Procedural Motion Layer (ProceduralMotionState — SmoothedLookDelta for HUD parallax)

**Feature:** A unified, paradigm-adaptive widget framework that manages all world-space UI elements (health bars, nameplates, damage numbers, cast bars, status icons, quest markers, interact prompts, off-screen indicators) through a single ECS-driven pipeline with Burst-compiled projection, importance-based budgeting, and camera-mode-aware layout — while preserving all existing rendering backends (MeshRenderer health bars, DamageNumbersPro, TextMeshPro floating text).

---

## Problem Statement

### Current State

The project has 4 independent widget systems, each with its own pooling, positioning, culling, and lifecycle logic:

| System | Rendering | Pool | Bridge | Culling | Paradigm-Aware |
|--------|-----------|------|--------|---------|----------------|
| EnemyHealthBarPool | MeshRenderer + shader | Custom (20-40) | EnemyHealthBarBridgeSystem | Distance (managed) | No |
| DamageNumbersProAdapter | Third-party DamageNumbersPro | Built-in | CombatUIBridgeSystem | Frustum (built-in) | No |
| FloatingTextManager | TextMeshPro | Custom (30-50) | CombatUIBridgeSystem | None | No |
| InteractionRingPool | MeshRenderer | Custom (10-20) | IInteractionRingProvider | None | No |

**Issues:**
1. **No unified budget** — Each system independently creates widgets with no global cap. 60 enemies + damage numbers + floating text + interact rings can exceed 200 active widgets with no coordination
2. **No paradigm adaptation** — Health bar placement, nameplate complexity, and widget density are identical in FPS (where you see 2-3 enemies) and ARPG (where you see 30+)
3. **No camera-aware scaling** — Isometric cameras at 20m height need larger widgets than FPS cameras at 1.7m height. Currently all widgets are the same size regardless of camera
4. **Duplicate culling logic** — Distance culling in EnemyHealthBarBridgeSystem, frustum culling in DamageNumbersPro, nothing in FloatingTextManager. Each reimplements (or doesn't implement) independently
5. **No stacking resolution** — Overlapping health bars in dense encounters obscure each other
6. **No off-screen indicators** — Boss positions, quest objectives, and party members have no edge-of-screen arrows
7. **No widget LOD** — Full nameplate (health + name + buffs + cast bar) renders identically at 5m and 50m
8. **No accessibility** — No font scaling, colorblind modes, or reduced-motion support for widgets
9. **Missing widget types** — No cast bars, buff/debuff rows, loot labels, boss plates, minimap pips, or telegraph ground indicators managed through a unified pipeline

### Desired State

One framework that:
- **Coordinates all widget types** through a shared projection + importance + budget pipeline
- **Adapts per paradigm** — FPS shows minimal UI, MMO shows full nameplates, ARPG shows overhead bars scaled for isometric distance
- **Adapts per camera mode** — Perspective projection for FPS/TPS, orthographic-compensated projection for isometric/top-down
- **Preserves existing rendering** — MeshRenderer health bars, DamageNumbersPro, TextMeshPro all continue working via adapter wrappers
- **Scales to 200+ entities** — Burst-compiled projection/culling/importance, managed bridge only touches visible widgets
- **Includes accessibility** — Font scaling, colorblind, reduced motion, high contrast

---

## Architecture Decisions

### Decision 1: Rendering Backend Strategy

| Option | Pros | Cons |
|--------|------|------|
| **A: Replace all with UI Toolkit** | Single rendering path, batched draw calls | Throws away proven MeshRenderer health bars, forces DamageNumbersPro migration, UI Toolkit has per-frame style cost for 200+ elements |
| **B: Replace all with MeshRenderer** | GPU instanced, zero Canvas/layout overhead | Hard to render complex widgets (buff icons, text, cast bars) in shader |
| **C: Adapter pattern — keep existing backends** | Zero risk to existing systems, each widget type uses optimal renderer, incremental migration | Slightly more abstraction, rendering not unified |

**Decision: C (Adapter pattern).** Each existing system (EnemyHealthBarPool, DamageNumbersProAdapter, FloatingTextManager, InteractionRingPool) wraps in an `IWidgetRenderer` adapter. New widget types (cast bars, buff rows, loot labels) can choose MeshRenderer, UI Toolkit, or UGUI based on what's optimal. The framework provides projection, culling, importance, and lifecycle — rendering is delegated.

**Rationale:** The existing MeshRenderer health bars are GPU-instanced and proven at scale. DamageNumbersPro is a tested third-party solution. Replacing either with UI Toolkit is risk with no measurable benefit. The framework's value is in the coordination layer, not the rendering layer.

### Decision 2: Projection & Culling Architecture

| Option | Pros | Cons |
|--------|------|------|
| **A: Per-system managed projection** | Simple, current approach | Duplicate code, no Burst, O(N) per system |
| **B: Centralized Burst projection job** | Single pass for all widgets, SIMD, camera-mode-aware | Must pass camera data to Burst, slightly more complex |

**Decision: B (Centralized Burst projection).** A single `WidgetProjectionSystem` (Burst ISystem, `SimulationSystemGroup`) projects ALL widget world positions to screen space, computes distance, applies frustum culling, and assigns LOD tiers. Managed bridge systems read the results.

### Decision 3: Widget Ownership Model

| Option | Pros | Cons |
|--------|------|------|
| **A: Widget as ECS component on target entity** | Natural — health bar lives on the enemy entity | Widget lifecycle coupled to entity lifecycle, can't have multiple widgets per entity cleanly |
| **B: Widget as separate ECS entity** | Clean lifecycle, multiple widgets per target, Burst-friendly queries | More entities, need target reference |
| **C: Hybrid — lightweight tag on target, data in NativeHashMap** | Minimal ECS footprint, flexible | HashMap access pattern less cache-friendly |

**Decision: A with extension.** Primary widget data (`WidgetState`) lives as `IComponentData` on the target entity. For multiple widgets per entity (health bar + nameplate + cast bar), the `WidgetState` contains a `WidgetFlags` bitmask indicating which widget types are active. The projection system processes all entities with `WidgetState` in one query. Rendering adapters filter by their widget type flag.

### Decision 4: Paradigm Adaptation Approach

| Option | Pros | Cons |
|--------|------|------|
| **A: Hardcoded per-paradigm logic** | Simple | Rigid, designer-unfriendly |
| **B: Per-paradigm profile ScriptableObject** | Data-driven, designer-tunable, matches EPIC 15.24/15.25 pattern | More assets to manage |

**Decision: B (Profile ScriptableObject).** `ParadigmWidgetProfile` ScriptableObject per paradigm, indexed by `InputParadigm` enum. Matches the established pattern from `ParadigmSurfaceProfile` (EPIC 15.24) and `ProceduralMotionProfile` paradigm weights (EPIC 15.25).

---

## Architecture Overview

### Data Flow

```
                    ECS World (Burst-safe)                          Managed World
                    ══════════════════════                          ═════════════

  Entity + Health + WidgetState                              WidgetBridgeSystem
         │                                                          │
         ▼                                                          ▼
  WidgetProjectionSystem (Burst)                             Rendering Adapters
  ├─ World→Screen projection                                ├─ HealthBarWidgetAdapter
  ├─ Frustum culling                                         │   └─ EnemyHealthBarPool (MeshRenderer)
  ├─ Distance LOD tier                                       ├─ DamageNumberWidgetAdapter
  ├─ Importance scoring                                      │   └─ DamageNumbersProAdapter
  ├─ Budget enforcement                                     ├─ FloatingTextWidgetAdapter
  └─ Output: NativeList<WidgetProjection>                    │   └─ FloatingTextManager (TMP)
         │                                                   ├─ InteractWidgetAdapter
         ▼                                                   │   └─ InteractionRingPool (MeshRenderer)
  WidgetStackingSystem (Burst)                               ├─ CastBarWidgetAdapter
  ├─ Overlap detection                                       │   └─ (new — UI Toolkit or MeshRenderer)
  ├─ Displacement offsets                                    ├─ BuffRowWidgetAdapter
  ├─ Grouping ("3x")                                         │   └─ (new — UI Toolkit)
  └─ Output: NativeList<WidgetProjection> (adjusted)         └─ OffScreenIndicatorAdapter
         │                                                       └─ (new — screen-edge arrows)
         ▼
  WidgetBridgeSystem (Managed, PresentationSystemGroup)
  ├─ Reads NativeList<WidgetProjection>
  ├─ Dirty-checks data (skip if unchanged)
  ├─ Routes to registered IWidgetRenderer adapters
  └─ Applies paradigm profile scaling
```

### Design Principles

1. **Calculate in Burst, render in Managed** — Projection, culling, importance, stacking all run in Burst. Only the final UI update touches managed code
2. **Budget globally, render locally** — The projection system enforces a global widget cap. Each adapter only receives widgets that passed the budget
3. **Preserve what works** — Existing health bars, damage numbers, and floating text keep their rendering backends. The framework wraps, not replaces
4. **Data-driven paradigm adaptation** — Designers tune per-paradigm profiles without code changes
5. **Camera-mode-aware** — Projection math adapts to perspective vs orthographic. Widget scale compensates for camera distance

---

## Performance Budget

| Metric | Target | Notes |
|--------|--------|-------|
| Max active widgets (all types) | 200 | Paradigm-configurable (FPS: 40, ARPG: 200) |
| Projection system (Burst) | < 0.15ms | 200 entities, single job |
| Stacking system (Burst) | < 0.10ms | Sort + overlap on projected list |
| Bridge system (Managed) | < 0.30ms | Dirty-check skips ~80% of updates |
| Total widget pipeline | < 0.55ms | Well under 1ms budget |
| Pool warm-up | < 50ms | One-time at scene load |
| Per-widget UI update | < 1.5us | Only for dirty widgets |

---

## Widget Taxonomy

### Widget Types

| Type | Rendering | Active Count | Paradigm Presence | Description |
|------|-----------|-------------|-------------------|-------------|
| **HealthBar** | MeshRenderer (existing) | 0–60 | All | Enemy/ally/boss health + trail |
| **Nameplate** | MeshRenderer or UI Toolkit | 0–60 | MMO, ARPG | Name + guild + level above entity |
| **DamageNumber** | DamageNumbersPro (existing) | 0–30 | All | Floating damage/heal values |
| **FloatingText** | TextMeshPro (existing) | 0–20 | All | Status text, combat verbs |
| **InteractPrompt** | MeshRenderer (existing) | 0–5 | All | "Press E to interact" |
| **CastBar** | MeshRenderer or UI Toolkit | 0–10 | MMO, ARPG, Shooter | Channel/cast progress under nameplate |
| **BuffRow** | UI Toolkit | 0–30 | MMO, ARPG | Status effect icons under health bar |
| **BossPlate** | Screen-space UGUI | 0–3 | All | Large boss health bar at screen top/bottom |
| **QuestMarker** | Screen-space overlay | 0–10 | MMO, ARPG | Exclamation/question marks above NPCs |
| **LootLabel** | UI Toolkit or TMP | 0–30 | ARPG, MMO | Item name + rarity color above drops |
| **OffScreenIndicator** | Screen-space overlay | 0–10 | All | Edge arrows for tracked entities |
| **MinimapPip** | Screen-space overlay | 0–100 | MOBA, ARPG | Dots on minimap for entities |
| **TelegraphIndicator** | Ground-projected decal | 0–10 | All | AOE danger zones (shared with EPIC 15.32) |

### Widget Flags (Bitmask)

```
HealthBar        = 1 << 0   (0x0001)
Nameplate        = 1 << 1   (0x0002)
CastBar          = 1 << 2   (0x0004)
BuffRow          = 1 << 3   (0x0008)
InteractPrompt   = 1 << 4   (0x0010)
QuestMarker      = 1 << 5   (0x0020)
LootLabel        = 1 << 6   (0x0040)
BossPlate        = 1 << 7   (0x0080)
OffScreen        = 1 << 8   (0x0100)
```

Multiple flags can be set simultaneously — an MMO enemy might have `HealthBar | Nameplate | CastBar | BuffRow`.

---

## Widget LOD System

Widgets reduce complexity based on distance from camera. LOD tiers are computed in the Burst projection system.

### LOD Tiers

| Tier | Distance (Shooter) | Distance (ARPG) | Health Bar | Nameplate | Buffs | Cast Bar |
|------|-------------------|-----------------|------------|-----------|-------|----------|
| **Full** | 0–15m | 0–25m | Full bar + trail + flash | Name + level + guild | All icons | Full bar |
| **Reduced** | 15–35m | 25–50m | Thin bar only | Name only | 3 most important | Hidden |
| **Minimal** | 35–60m | 50–80m | Health pip (dot) | Hidden | Hidden | Hidden |
| **Culled** | 60m+ | 80m+ | Hidden | Hidden | Hidden | Hidden |

LOD distances are multiplied by the paradigm profile's `LODDistanceMultiplier`. Isometric cameras set this higher because all entities are further from the camera.

### LOD Exception Rules

- **Targeted entity** — Always Full LOD regardless of distance
- **Boss/Elite tier** — LOD demoted by one tier maximum (never goes below Reduced)
- **Party members** — Always at least Reduced LOD
- **Entities taking/dealing damage** — Temporarily promoted to Full for 2 seconds

---

## Importance Scoring

When the active widget count exceeds the budget, lowest-importance widgets are hidden first.

### Importance Formula

```
Importance = BaseImportance
           + DistanceBonus          (closer = higher)
           + TierBonus              (Boss=50, Elite=30, Champion=20, Normal=0)
           + CombatBonus            (in combat with player = +40)
           + TargetBonus            (targeted = +100, hovered = +30)
           + DamageRecencyBonus     (damaged in last 2s = +25)
           + ParadigmBonus          (paradigm-specific, e.g., MOBA boosts tower plates)
```

### Distance Bonus Curve

```
DistanceBonus = max(0, 100 - distance * DistanceFalloff)
```

Where `DistanceFalloff` is paradigm-configurable (Shooter: 3.0 = sharp falloff, ARPG: 1.0 = gradual falloff).

### Budget Enforcement

1. Sort all widgets by importance (descending)
2. Top N widgets (per paradigm budget) get `IsVisible = true`
3. Remaining get `IsVisible = false`
4. Targeted/Boss widgets are exempt from budget (always visible)

---

## Per-Paradigm Widget Profiles

### ParadigmWidgetProfile ScriptableObject

Each paradigm has a profile controlling widget behavior. Create via `Assets > Create > DIG/Widgets/Paradigm Widget Profile`.

### Paradigm Comparison Table

| Setting | Shooter | MMO | ARPG | MOBA | TwinStick | SideScroller |
|---------|---------|-----|------|------|-----------|--------------|
| **MaxActiveWidgets** | 40 | 80 | 200 | 60 | 30 | 20 |
| **LODDistanceMultiplier** | 1.0 | 1.2 | 2.0 | 1.8 | 1.0 | 1.0 |
| **WidgetScaleMultiplier** | 1.0 | 1.0 | 1.8 | 1.5 | 1.0 | 1.2 |
| **DistanceFalloff** | 3.0 | 2.0 | 1.0 | 1.5 | 3.0 | 2.0 |
| **HealthBarEnabled** | true | true | true | true | true | true |
| **NameplateEnabled** | false | true | true | false | false | false |
| **NameplateComplexity** | — | Full | Reduced | — | — | — |
| **CastBarEnabled** | true | true | true | false | false | false |
| **BuffRowEnabled** | false | true | true | false | false | false |
| **BuffRowMaxIcons** | — | 8 | 5 | — | — | — |
| **LootLabelEnabled** | true | true | true | false | false | false |
| **QuestMarkerEnabled** | false | true | true | false | false | false |
| **OffScreenEnabled** | true | true | false | true | true | false |
| **BossPlatePosition** | Top | Top | Bottom | Top | Top | Top |
| **StackingEnabled** | true | true | true | true | false | false |
| **GroupingEnabled** | false | false | true | true | false | false |
| **GroupingThreshold** | — | — | 4 | 3 | — | — |
| **BillboardMode** | CameraAligned | CameraAligned | FlatOverhead | FlatOverhead | CameraAligned | CameraAligned |
| **HealthBarStyle** | Thin | Standard | Standard | Compact | Thin | Standard |
| **DamageNumberScale** | 1.0 | 1.0 | 1.5 | 1.3 | 1.0 | 1.2 |
| **ShowHealthBarOnPlayer** | false | true | false | true | false | false |
| **HealthBarYOffset** | 2.5m | 2.8m | 0.3m | 0.2m | 2.5m | 2.0m |
| **AccessibilityFontScale** | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 |

### Billboard Modes

| Mode | Behavior | Used By |
|------|----------|---------|
| **CameraAligned** | Widget faces camera direction (parallel to near plane). Standard for perspective cameras | Shooter, MMO, TwinStick, SideScroller |
| **FlatOverhead** | Widget rendered horizontally above entity, rotated to face camera top-down. Optimal for isometric/top-down cameras | ARPG, MOBA |
| **ScreenSpace** | Widget projected to screen coordinates, rendered in screen-space overlay. No world-space object | BossPlates, OffScreenIndicators |

### Camera Mode Compatibility

| Camera Mode | Default Paradigm | Billboard Mode | Projection |
|------------|-----------------|----------------|------------|
| **FirstPerson** | Shooter | CameraAligned | Perspective |
| **ThirdPersonFollow** | Shooter/MMO | CameraAligned | Perspective |
| **IsometricFixed** | ARPG | FlatOverhead | Perspective (steep angle) |
| **IsometricRotatable** | ARPG | FlatOverhead | Perspective (steep angle) |
| **TopDownFixed** | MOBA | FlatOverhead | Perspective/Orthographic |

### Isometric Scale Compensation

In isometric/top-down cameras, all entities are 15-25m from the camera. Without compensation, widgets appear tiny. The `WidgetScaleMultiplier` on the paradigm profile handles this:

```
FinalScale = BaseScale * WidgetScaleMultiplier * LODScaleFactor * TierScaleFactor
```

Additionally, the projection system detects orthographic cameras and skips perspective-divide scaling (orthographic widgets don't shrink with distance).

---

## Nameplate Complexity Levels

For paradigms with nameplates enabled, complexity determines what's shown:

### Full Nameplate (MMO)

```
┌──────────────────────────────────────┐
│  [Guild Tag]                         │
│  Entity Name            Lv.42       │
│  ████████████░░░░░░░░  (75%)        │  ← Health bar
│  🔥 ❄️ ⚡ 🛡️ ...                    │  ← Buff/debuff icons
│  ████████████████████  Fireball     │  ← Cast bar + spell name
└──────────────────────────────────────┘
```

### Reduced Nameplate (ARPG)

```
┌──────────────────────┐
│  Entity Name         │
│  ████████████░░░░░░  │  ← Health bar
│  🔥 ❄️ ⚡             │  ← Top 3 buffs only
└──────────────────────┘
```

### Compact (MOBA)

```
  ████████░░░░  ← Health bar only, wider and thin
```

---

## Stacking & Overlap Resolution

### Screen-Space Overlap Detection

After projection, the stacking system (Burst) checks for overlapping widget rects in screen space:

1. Sort projected widgets by screen Y (top to bottom)
2. For each pair within proximity threshold: compute overlap area
3. If overlap > 30% of either widget rect: displace the lower-importance widget
4. Displacement direction: push vertically away from overlap center

### Displacement Rules

| Scenario | Resolution |
|----------|------------|
| 2 health bars overlap | Lower importance pushes up or down |
| 3+ bars in cluster | Fan out vertically with even spacing |
| Targeted widget overlapped | Other widget always yields |
| Boss widget overlapped | Boss never displaced |

### Grouping

When `GroupingEnabled` is true and `GroupingThreshold` or more entities of the same type cluster within a screen-space radius:

- Individual widgets are hidden
- A single group badge appears: `[EnemyType] x5` with aggregate health bar
- Clicking the group badge could expand to show individual bars (stretch goal)

Grouping is most useful in ARPG/MOBA where 10+ minions clump together.

---

## Off-Screen Indicator System

Tracked entities that are off-screen get edge-of-screen arrows pointing toward their world position.

### Tracked Entity Types

| Entity Type | Icon | Color | Always Tracked |
|------------|------|-------|----------------|
| Boss (alive) | Skull | Red | Yes |
| Quest objective | Exclamation | Yellow | Yes |
| Party member | Shield | Green | Yes (MMO/ARPG) |
| Targeted enemy | Crosshair | Red | Yes |
| Waypoint | Diamond | White | Yes |
| Loot (legendary+) | Star | Orange | Paradigm-dependent |

### Indicator Placement

```
     ┌─────────────────────────────┐
     │          ▼ Boss             │
     │                             │
  ◄──│                             │──► Quest
     │        [Game View]          │
     │                             │
     │                             │
     │          ▲ Party            │
     └─────────────────────────────┘
```

Indicators are clamped to screen edges with a configurable margin (default 40px). Distance text is shown below the arrow icon.

### Paradigm Configuration

| Paradigm | Off-Screen Indicators | Notes |
|----------|----------------------|-------|
| Shooter | Bosses, targeted, waypoints | Minimal — FPS players look around |
| MMO | Bosses, party, quests, waypoints | Social awareness |
| ARPG | Disabled | Isometric view shows most of the area |
| MOBA | Bosses, towers, objectives | Strategic awareness |
| TwinStick | Bosses, pickups | Quick reference |
| SideScroller | Disabled | Linear levels, not needed |

---

## Existing Systems: Keep / Replace / Modify

| System | File | Decision | Rationale |
|--------|------|----------|-----------|
| **EnemyHealthBar** | `Combat/UI/WorldSpace/EnemyHealthBar.cs` | **KEEP** | MeshRenderer + shader is GPU-instanced, proven |
| **EnemyHealthBarPool** | `Combat/UI/WorldSpace/EnemyHealthBarPool.cs` | **WRAP** | Wrap in `IWidgetRenderer` adapter. Pool logic stays |
| **EnemyHealthBarBridgeSystem** | `Combat/Bridges/EnemyHealthBarBridgeSystem.cs` | **MODIFY** | Remove projection/culling logic (now in WidgetProjectionSystem). Keep server/client world query logic. Feed data to bridge instead of directly to pool |
| **HealthBarVisibilityConfig** | `Combat/UI/WorldSpace/HealthBarVisibilityConfig.cs` | **KEEP** | Reuse `Evaluate()` as input to importance scoring |
| **HealthBarVisibilityComponents** | `Combat/UI/WorldSpace/HealthBarVisibilityComponents.cs` | **KEEP** | `HealthBarVisibilityState` feeds importance scoring |
| **HealthBarSettingsManager** | `Combat/UI/WorldSpace/HealthBarSettingsManager.cs` | **KEEP** | User settings flow into paradigm profile |
| **DamageNumbersProAdapter** | `Combat/UI/Adapters/DamageNumbersProAdapter.cs` | **WRAP** | Wrap in `IWidgetRenderer` adapter |
| **FloatingTextManager** | `Combat/UI/FloatingText/FloatingTextManager.cs` | **WRAP** | Wrap in `IWidgetRenderer` adapter |
| **InteractionRingPool** | `Combat/UI/WorldSpace/InteractionRingPool.cs` | **WRAP** | Wrap in `IWidgetRenderer` adapter |
| **CombatUIRegistry** | `Combat/UI/CombatUIRegistry.cs` | **EXTEND** | Add `IWidgetRenderer` registration alongside existing providers |
| **CombatUIBridgeSystem** | `Combat/UI/CombatUIBridgeSystem.cs` | **KEEP** | Continues routing CombatResultEvents. Adapters registered in both registries |
| **DamageVisualQueue** | `Combat/UI/DamageVisualQueue.cs` | **KEEP** | Static queue pattern still works for damage events |

---

## Implementation Phases

### Phase 1: Widget Core & Projection [NEW]

**Goal:** Centralized Burst-compiled projection for all widget-bearing entities. Foundation types.

| File | Lines | Purpose |
|------|-------|---------|
| `Assets/Scripts/Widgets/Components/WidgetTypes.cs` | ~60 | `WidgetType` enum, `WidgetFlags` bitmask, `WidgetLODTier` enum |
| `Assets/Scripts/Widgets/Components/WidgetState.cs` | ~80 | `IComponentData`: active flags, importance, LOD tier, screen position, visibility |
| `Assets/Scripts/Widgets/Data/WidgetProjection.cs` | ~40 | Struct: entity, screen XY, distance, LOD tier, importance, isVisible, billboard mode |
| `Assets/Scripts/Widgets/Systems/WidgetProjectionSystem.cs` | ~200 | Burst ISystem (SimulationSystemGroup, Client|Local): world-to-screen projection, frustum culling, distance LOD, importance scoring. Outputs to `NativeList<WidgetProjection>` singleton |
| `Assets/Scripts/Widgets/Systems/WidgetImportanceSystem.cs` | ~120 | Burst ISystem: computes importance per entity from distance, tier, combat state, target state, paradigm. Runs before projection |
| `Assets/Scripts/Widgets/Authoring/WidgetStateAuthoring.cs` | ~50 | Baker: adds WidgetState to entities with Health/DamageableTag. Configurable default flags per prefab |

**Key design:**
- `WidgetProjectionSystem` reads camera VP matrix from a managed singleton (`WidgetCameraData`) set by the bridge each frame
- Frustum test: `screenPos.z < 0 || screenPos.x < 0 || screenPos.x > width || screenPos.y < 0 || screenPos.y > height`
- Distance LOD: thresholds from paradigm profile (see LOD table above)
- Budget enforcement: sort by importance, top N visible
- Output `NativeList<WidgetProjection>` as a singleton component for downstream systems

**Estimated:** ~550 new lines

---

### Phase 2: Bridge & Adapter Framework [NEW]

**Goal:** Managed bridge that reads projection results and routes to rendering adapters.

| File | Lines | Purpose |
|------|-------|---------|
| `Assets/Scripts/Widgets/Rendering/IWidgetRenderer.cs` | ~50 | Interface: `WidgetType SupportedType`, `OnWidgetShow(data)`, `OnWidgetUpdate(data)`, `OnWidgetHide(entity)`, `OnFrameBegin()`, `OnFrameEnd()` |
| `Assets/Scripts/Widgets/Rendering/WidgetRenderData.cs` | ~60 | Struct passed to renderers: entity, screenPos, worldPos, health, name, LOD tier, importance, paradigm billboard mode, scale |
| `Assets/Scripts/Widgets/Systems/WidgetBridgeSystem.cs` | ~250 | Managed SystemBase (PresentationSystemGroup, Client|Local): reads NativeList, dirty-checks per entity, routes to registered `IWidgetRenderer`s. Sets `WidgetCameraData` for next frame |
| `Assets/Scripts/Widgets/Rendering/WidgetRendererRegistry.cs` | ~80 | Static registry for `IWidgetRenderer` adapters (like CombatUIRegistry pattern) |
| `Assets/Scripts/Widgets/Config/WidgetCameraData.cs` | ~30 | Managed singleton: VP matrix, screen dimensions, camera position, camera mode, updated by bridge each frame |

**Key design:**
- Bridge iterates `NativeList<WidgetProjection>` once per frame
- Per-widget dirty check: skip `OnWidgetUpdate` if health, position (>0.5px), and LOD tier unchanged
- Layout throttling: position update skipped if screen delta < 0.5px
- `OnFrameBegin/End` lets renderers batch their operations
- Bridge also handles spawn (first frame visible) and despawn (first frame hidden) transitions

**Estimated:** ~470 new lines

---

### Phase 3: Existing System Adapters [NEW]

**Goal:** Wrap all 4 existing widget systems in `IWidgetRenderer` adapters without modifying their internals.

| File | Lines | Purpose |
|------|-------|---------|
| `Assets/Scripts/Widgets/Adapters/HealthBarWidgetAdapter.cs` | ~120 | Wraps `EnemyHealthBarPool`. Translates `WidgetRenderData` to `ShowHealthBar/HideHealthBar` calls. Applies paradigm scale multiplier |
| `Assets/Scripts/Widgets/Adapters/DamageNumberWidgetAdapter.cs` | ~60 | Wraps `DamageNumbersProAdapter`. Applies paradigm `DamageNumberScale`. Passes through to existing spawn logic |
| `Assets/Scripts/Widgets/Adapters/FloatingTextWidgetAdapter.cs` | ~60 | Wraps `FloatingTextManager`. Applies font scale from accessibility config |
| `Assets/Scripts/Widgets/Adapters/InteractWidgetAdapter.cs` | ~50 | Wraps `InteractionRingPool`. Shows/hides based on WidgetProjection visibility |

**Key design:**
- Adapters implement `IWidgetRenderer` and register in `WidgetRendererRegistry`
- Adapters delegate ALL rendering to existing pools — no duplicate rendering code
- `HealthBarWidgetAdapter` is the most complex: must translate between `WidgetRenderData` and the existing `EnemyHealthBarPool` API, including visibility evaluation via `HealthBarVisibilityConfig`

**Modified files:**
- `EnemyHealthBarBridgeSystem.cs` (+30 lines): Add option to defer positioning to `WidgetProjectionSystem` when the widget framework is active. Fallback to existing logic when framework is not present (backward compatible)

**Estimated:** ~290 new lines + ~30 modified

---

### Phase 4: Paradigm-Adaptive Profiles [NEW]

**Goal:** Data-driven per-paradigm widget configuration.

| File | Lines | Purpose |
|------|-------|---------|
| `Assets/Scripts/Widgets/Config/ParadigmWidgetProfile.cs` | ~120 | ScriptableObject: all fields from the paradigm comparison table (MaxActiveWidgets, LODDistanceMultiplier, WidgetScaleMultiplier, enabled flags, billboard mode, style, offsets, etc.) |
| `Assets/Scripts/Widgets/Config/ParadigmWidgetConfig.cs` | ~80 | MonoBehaviour singleton: array of profiles, subscribes to `ParadigmStateMachine.OnParadigmChanged`, caches active profile. Fallback profile (Shooter) |
| `Assets/Scripts/Widgets/Config/WidgetStyleConfig.cs` | ~60 | ScriptableObject: per-style visual settings (thin bar, standard bar, compact bar — dimensions, colors, font sizes) |

**Key design:**
- `ParadigmWidgetConfig` subscribes to paradigm changes in `Start()`, swaps profile
- `WidgetProjectionSystem` reads active profile via `ParadigmWidgetConfig.Instance.ActiveProfile`
- If `ParadigmWidgetConfig` is not in scene, all settings use Shooter defaults (safe fallback)
- Designers create one profile per paradigm and assign them in the config

**Estimated:** ~260 new lines

---

### Phase 5: Stacking & Off-Screen Indicators [NEW]

**Goal:** Resolve overlapping widgets and add edge-of-screen tracking indicators.

| File | Lines | Purpose |
|------|-------|---------|
| `Assets/Scripts/Widgets/Systems/WidgetStackingSystem.cs` | ~180 | Burst ISystem (SimulationSystemGroup, after WidgetProjectionSystem): sorts by screen Y, detects overlaps, computes displacement offsets. Updates `NativeList<WidgetProjection>` in-place |
| `Assets/Scripts/Widgets/Systems/WidgetGroupingSystem.cs` | ~130 | Burst ISystem: detects clusters of same-type entities within screen radius, marks as grouped, computes aggregate data (count, average health) |
| `Assets/Scripts/Widgets/Components/OffScreenTracker.cs` | ~40 | `IComponentData`: flags entity for off-screen tracking (boss, quest, party, etc.) |
| `Assets/Scripts/Widgets/Rendering/OffScreenIndicatorRenderer.cs` | ~150 | MonoBehaviour: renders edge arrows for tracked entities. Reads off-screen entries from WidgetProjectionSystem. Clamps to screen edge with margin |
| `Assets/Scripts/Widgets/Config/OffScreenIndicatorConfig.cs` | ~50 | ScriptableObject: icon sprites, colors, margin, distance text format per entity type |

**Key design:**
- Stacking only runs when paradigm profile `StackingEnabled = true`
- Grouping only runs when `GroupingEnabled = true` and cluster size >= `GroupingThreshold`
- Off-screen indicators computed in the projection system (entities with `OffScreenTracker` that fail frustum test)
- Edge clamping: `clamp(screenPos, margin, screenSize - margin)`, arrow rotation points toward actual world position
- Distance text: `"12m"` below arrow icon

**Estimated:** ~550 new lines

---

### Phase 6: New Widget Types [NEW]

**Goal:** Cast bars, buff/debuff rows, loot labels, boss plates.

| File | Lines | Purpose |
|------|-------|---------|
| `Assets/Scripts/Widgets/Rendering/CastBarRenderer.cs` | ~120 | MeshRenderer-based cast bar (quad + fill shader, like health bar). Shows below nameplate when entity is casting |
| `Assets/Scripts/Widgets/Rendering/BuffRowRenderer.cs` | ~150 | UI Toolkit panel: horizontal row of buff/debuff icons. Reads from entity's StatusEffect buffer. Max icons from paradigm profile |
| `Assets/Scripts/Widgets/Rendering/LootLabelRenderer.cs` | ~100 | TextMeshPro-based: item name colored by rarity, floating above loot drops. Pool of 30 |
| `Assets/Scripts/Widgets/Rendering/BossPlateRenderer.cs` | ~130 | Screen-space UGUI: large health bar at screen top/bottom for boss encounters. Name + health + phase indicator. Positioned per paradigm profile |
| `Assets/Scripts/Widgets/Components/CastBarState.cs` | ~30 | `IComponentData`: CastProgress (0-1), CastDuration, SpellName (FixedString32), IsCasting |
| `Assets/Scripts/Widgets/Components/BossPlateTag.cs` | ~15 | `IComponentData` tag: marks entity for boss plate rendering |

**Key design:**
- Cast bars integrate with existing `AbilityExecutionState` (EPIC 15.32) — read cast progress from ability system
- Buff rows read from `StatusEffectElement` buffer (if present on entity)
- Loot labels spawn when loot entities are created, despawn on pickup
- Boss plates are screen-space — they don't go through the world-space projection pipeline. Managed by `BossPlateRenderer` directly
- All new renderers implement `IWidgetRenderer` and register in the registry

**Estimated:** ~545 new lines

---

### Phase 7: Accessibility & Animation [NEW]

**Goal:** Font scaling, colorblind support, reduced motion, spawn/despawn juice.

| File | Lines | Purpose |
|------|-------|---------|
| `Assets/Scripts/Widgets/Config/WidgetAccessibilityConfig.cs` | ~80 | ScriptableObject: FontScaleMultiplier (1x/1.25x/1.5x/2x), ColorblindMode (None/Deuteranopia/Protanopia/Tritanopia), ReducedMotion (bool), HighContrast (bool) |
| `Assets/Scripts/Widgets/Config/WidgetAccessibilityManager.cs` | ~90 | MonoBehaviour singleton: manages accessibility state, applies to renderers. Reads/writes PlayerPrefs |
| `Assets/Scripts/Widgets/Animation/WidgetAnimator.cs` | ~120 | Static utility: `AnimateSpawn(transform, style)`, `AnimateDespawn(transform, callback)`, `AnimateDamageFlash(renderer)`, `AnimateShake(transform, intensity, duration)`. Uses DOTween or manual lerp |
| `Assets/Scripts/Widgets/Config/ColorblindPalette.cs` | ~50 | ScriptableObject: remapped colors for health (green→blue), damage (red→orange), healing (green→cyan), shield (blue→purple) per colorblind mode |

**Key design:**
- `FontScaleMultiplier` applied to all text-based renderers (FloatingText, LootLabels, Nameplates, CastBars)
- `ColorblindMode` remaps health bar fill colors, damage number colors, buff/debuff icon tints via `ColorblindPalette`
- `ReducedMotion = true`: disables spawn elastic scale, despawn fade, damage shake. Widgets appear/disappear instantly
- `HighContrast = true`: adds dark outline/background behind all text, increases health bar border thickness
- Animations only fire when `ReducedMotion = false`

**Animation specification:**
| Animation | Duration | Easing | Reduced Motion |
|-----------|----------|--------|----------------|
| Spawn | 0.2s | Elastic out (scale 0→1) | Instant appear |
| Despawn | 0.15s | Ease-in (fade to 0) | Instant disappear |
| Damage flash | 0.1s | Linear (white→original) | Skip |
| Damage shake | 0.2s | Decay (5-10px random) | Skip |
| Health trail | 0.5s | Ease-out (trail catches fill) | Instant |
| Heal pulse | 0.3s | Ease-out (green glow) | Skip |

**Estimated:** ~340 new lines

---

### Phase 8: Debug & Profiling [NEW]

**Goal:** Developer tools for tuning widgets at runtime.

| File | Lines | Purpose |
|------|-------|---------|
| `Assets/Scripts/Widgets/Debug/WidgetDebugOverlay.cs` | ~130 | MonoBehaviour: F10-togglable HUD showing active widget count by type, budget utilization, culled count, stacking displacements, average importance |
| `Assets/Editor/Widgets/WidgetDebugWindow.cs` | ~100 | EditorWindow (`Window > DIG > Widget Debug`): real-time stats, per-type counts, pool health, paradigm profile name, LOD distribution histogram |
| `Assets/Scripts/Widgets/Debug/WidgetProfiler.cs` | ~50 | Static `ProfilerMarker`s: `Widget.Projection`, `Widget.Stacking`, `Widget.Bridge`, `Widget.Render` |

**Profiler markers:**
| Marker | System | Expected Cost |
|--------|--------|---------------|
| `Widget.Projection` | WidgetProjectionSystem | <0.15ms |
| `Widget.Importance` | WidgetImportanceSystem | <0.05ms |
| `Widget.Stacking` | WidgetStackingSystem | <0.10ms |
| `Widget.Bridge` | WidgetBridgeSystem | <0.30ms |
| `Widget.Render.[Type]` | Per-adapter render time | <0.10ms each |

**Estimated:** ~280 new lines

---

## Implementation Summary

| Phase | Files | New Lines | Modified Lines | Priority |
|-------|-------|-----------|----------------|----------|
| 1: Widget Core & Projection | 6 | ~550 | 0 | Critical |
| 2: Bridge & Adapter Framework | 5 | ~470 | 0 | Critical |
| 3: Existing System Adapters | 4 (+1 mod) | ~290 | ~30 | Critical |
| 4: Paradigm-Adaptive Profiles | 3 | ~260 | 0 | High |
| 5: Stacking & Off-Screen | 5 | ~550 | 0 | Medium |
| 6: New Widget Types | 6 | ~545 | 0 | Medium |
| 7: Accessibility & Animation | 4 | ~340 | 0 | High |
| 8: Debug & Profiling | 3 | ~280 | 0 | Low |
| **Total** | **36 (+1 mod)** | **~3,285** | **~30** | |

### Implementation Order

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 7 → Phase 5 → Phase 6 → Phase 8
```

Phase 7 (Accessibility) is prioritized before Phase 5/6 because accessibility should be built into new widget types from the start, not retrofitted.

---

## Key Files (After Implementation)

### Core

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Components/WidgetTypes.cs` | Enums: WidgetType, WidgetFlags, WidgetLODTier |
| `Assets/Scripts/Widgets/Components/WidgetState.cs` | Per-entity widget tracking component |
| `Assets/Scripts/Widgets/Data/WidgetProjection.cs` | Projection result struct |

### Systems

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Systems/WidgetImportanceSystem.cs` | Burst importance scoring |
| `Assets/Scripts/Widgets/Systems/WidgetProjectionSystem.cs` | Burst world-to-screen projection + culling + LOD |
| `Assets/Scripts/Widgets/Systems/WidgetStackingSystem.cs` | Burst overlap resolution |
| `Assets/Scripts/Widgets/Systems/WidgetGroupingSystem.cs` | Burst entity grouping |
| `Assets/Scripts/Widgets/Systems/WidgetBridgeSystem.cs` | Managed bridge to renderers |

### Rendering

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Rendering/IWidgetRenderer.cs` | Renderer adapter interface |
| `Assets/Scripts/Widgets/Rendering/WidgetRenderData.cs` | Data passed to renderers |
| `Assets/Scripts/Widgets/Rendering/WidgetRendererRegistry.cs` | Static adapter registry |
| `Assets/Scripts/Widgets/Rendering/OffScreenIndicatorRenderer.cs` | Edge-of-screen arrows |
| `Assets/Scripts/Widgets/Rendering/CastBarRenderer.cs` | Cast/channel progress bar |
| `Assets/Scripts/Widgets/Rendering/BuffRowRenderer.cs` | Status effect icon row |
| `Assets/Scripts/Widgets/Rendering/LootLabelRenderer.cs` | Item name labels |
| `Assets/Scripts/Widgets/Rendering/BossPlateRenderer.cs` | Screen-space boss health |

### Adapters (wrapping existing systems)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Adapters/HealthBarWidgetAdapter.cs` | Wraps EnemyHealthBarPool |
| `Assets/Scripts/Widgets/Adapters/DamageNumberWidgetAdapter.cs` | Wraps DamageNumbersProAdapter |
| `Assets/Scripts/Widgets/Adapters/FloatingTextWidgetAdapter.cs` | Wraps FloatingTextManager |
| `Assets/Scripts/Widgets/Adapters/InteractWidgetAdapter.cs` | Wraps InteractionRingPool |

### Config

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Config/ParadigmWidgetProfile.cs` | Per-paradigm widget settings SO |
| `Assets/Scripts/Widgets/Config/ParadigmWidgetConfig.cs` | Singleton managing active profile |
| `Assets/Scripts/Widgets/Config/WidgetStyleConfig.cs` | Visual style definitions |
| `Assets/Scripts/Widgets/Config/WidgetCameraData.cs` | Camera VP matrix bridge |
| `Assets/Scripts/Widgets/Config/WidgetAccessibilityConfig.cs` | Accessibility settings SO |
| `Assets/Scripts/Widgets/Config/WidgetAccessibilityManager.cs` | Accessibility singleton |
| `Assets/Scripts/Widgets/Config/ColorblindPalette.cs` | Colorblind color remapping |
| `Assets/Scripts/Widgets/Config/OffScreenIndicatorConfig.cs` | Off-screen indicator settings |

### Animation

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Animation/WidgetAnimator.cs` | Spawn/despawn/damage animation utilities |

### Authoring

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Authoring/WidgetStateAuthoring.cs` | Baker for WidgetState on damageable entities |

### Debug

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Debug/WidgetDebugOverlay.cs` | F10 runtime debug HUD |
| `Assets/Scripts/Widgets/Debug/WidgetProfiler.cs` | ProfilerMarker definitions |
| `Assets/Editor/Widgets/WidgetDebugWindow.cs` | Editor debug window |

---

## Verification Checklist

### Phase 1: Core & Projection
- [ ] Entities with Health + WidgetState get projected to screen coordinates
- [ ] Frustum-culled entities have `IsVisible = false`
- [ ] Distance LOD tiers computed correctly (Full/Reduced/Minimal/Culled)
- [ ] Importance scores reflect distance, tier, combat, target state
- [ ] Budget enforcement: only top N widgets visible when count exceeds max

### Phase 2: Bridge & Adapters
- [ ] WidgetBridgeSystem reads NativeList and routes to adapters
- [ ] Dirty-check skips update when position delta < 0.5px and health unchanged
- [ ] Spawn/despawn callbacks fire on first-visible and first-hidden frames
- [ ] Layout throttling measurably reduces UI style updates

### Phase 3: Existing System Integration
- [ ] Health bars render identically to pre-framework behavior
- [ ] Damage numbers appear at correct world positions
- [ ] Floating text spam prevention still works
- [ ] Interaction rings show/hide correctly
- [ ] Removing the widget framework falls back to original behavior (no hard dependency)

### Phase 4: Paradigm Profiles
- [ ] Switch to ARPG paradigm → widget scale increases to 1.8x
- [ ] Switch to MOBA paradigm → nameplates disabled, compact bars shown
- [ ] Switch to MMO paradigm → full nameplates with name + guild + level
- [ ] Shooter paradigm → minimal UI, no nameplates
- [ ] Billboard mode switches between CameraAligned and FlatOverhead per paradigm

### Phase 5: Stacking & Off-Screen
- [ ] Dense enemy clusters don't overlap — bars fan out vertically
- [ ] ARPG with GroupingEnabled → "Skeleton x5" group badge for clusters
- [ ] Boss off-screen → red skull arrow at screen edge
- [ ] Quest objective off-screen → yellow exclamation at screen edge
- [ ] Distance text shown below off-screen arrows

### Phase 6: New Widget Types
- [ ] Cast bar appears under nameplate when enemy casts ability
- [ ] Buff row shows status effect icons (max from paradigm profile)
- [ ] Loot labels appear above dropped items with rarity color
- [ ] Boss plate appears at screen top when boss entity is alive

### Phase 7: Accessibility
- [ ] Font scale 2x → all text widgets doubled in size
- [ ] Colorblind Deuteranopia → health green replaced with blue
- [ ] Reduced motion → no spawn animation, no shake, instant transitions
- [ ] High contrast → dark outlines behind all text

### Phase 8: Debug
- [ ] F10 toggle → widget debug overlay showing counts and budget utilization
- [ ] Profiler → Widget.Projection marker visible with correct timing
- [ ] Editor window shows real-time per-type widget counts

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Burst projection slower than expected with 200 entities | Pipeline exceeds 0.5ms budget | Profile early. Projection is embarrassingly parallel — can fall back to IJobParallelFor if needed |
| UI Toolkit overhead for buff rows at scale | Layout thrashing with 30+ buff panels | Use absolute positioning only. Consider MeshRenderer atlas for buff icons if UI Toolkit proves costly |
| Adapter wrapping breaks existing health bar behavior | Regression in shipped combat UI | Adapters are opt-in. If `ParadigmWidgetConfig` is not in scene, existing systems run standalone (backward compatible) |
| Paradigm switching causes widget pop-in | Jarring when changing camera mode | Cross-fade between paradigm profiles over 0.3s. Widgets scale/reposition smoothly |
| Off-screen indicators cluttered in boss arenas | Too many arrows at screen edges | Budget off-screen indicators separately (max 5). Priority: boss > quest > party > waypoint |
| Grouping hides important enemies | Player doesn't see elite in cluster | Elites/bosses never grouped. Only Normal-tier entities participate in grouping |

---

## Best Practices

1. **Assign WidgetFlags intentionally per prefab** — Don't default everything to `HealthBar | Nameplate | BuffRow`. NPCs that players never fight shouldn't have health bars
2. **Use WidgetStateAuthoring sparingly** — Only entities that need world-space widgets should have `WidgetState`. Projectiles, environmental objects, and decorations should not
3. **Create paradigm profiles early** — Even if only Shooter mode is used now, having profiles for all 6 paradigms lets designers tune widget behavior without code changes when new camera modes ship
4. **Test at density** — Spawn 50+ enemies and verify budget enforcement works. Check the debug overlay (F10) for budget utilization
5. **Profile with markers** — Check `Widget.Projection` and `Widget.Bridge` in the Unity Profiler. Total should stay under 0.55ms
6. **Don't break backward compatibility** — The adapter framework is opt-in. If `ParadigmWidgetConfig` is missing from the scene, all existing systems continue working independently
7. **Accessibility first for new widgets** — New renderers (cast bars, buff rows, loot labels) should read `WidgetAccessibilityConfig` from day one. Don't add accessibility as an afterthought
8. **Test colorblind modes** — Health green and damage red are the most commonly confused pair (deuteranopia). Verify the palette remapping is visible
9. **Keep importance scoring simple** — The formula should be understandable at a glance. Avoid complex weighting schemes that are hard to debug
10. **Use existing HealthBarVisibilityConfig for evaluation** — The 17-mode visibility system (EPIC 15.14) is battle-tested. Feed its `Evaluate()` results into importance scoring rather than reimplementing visibility logic

---

## Relationship to Other EPICs

| Concern | EPIC |
|---------|------|
| Combat UI foundation (CombatUIRegistry, providers, adapters) | EPIC 15.9 |
| Health bar visibility system (17 modes, data-driven config) | EPIC 15.14 |
| Target lock UI (reticle, target highlight) | EPIC 15.16 |
| Line-of-sight visibility for health bars | EPIC 15.17 |
| Cursor hover/click-select for widget interaction | EPIC 15.18 |
| Input paradigm framework (ParadigmStateMachine, profiles) | EPIC 15.20 |
| Surface FX paradigm profiles (established profile pattern) | EPIC 15.24 |
| Procedural motion paradigm weights (established weight pattern) | EPIC 15.25 |
| Ability execution state (cast bar data source) | EPIC 15.32 |
| **Smart HUD & Widget Ecosystem** | **This EPIC (15.26)** |
