# EPIC 15.26 Setup Guide: Smart HUD & Widget Ecosystem

**Status:** Implemented (Phases 1-6)
**Last Updated:** February 14, 2026
**Requires:** Enemies with `ShowHealthBarTag` + `Health` + `DamageableAuthoring`. `EnemyHealthBarPool` in scene (from EPIC 15.9). `ParadigmStateMachine` (EPIC 15.20) for paradigm-adaptive profiles. Combat systems (EPIC 15.22+) for combat state tracking.

This guide covers the Unity Editor setup for the **Smart HUD & Widget Ecosystem** — the centralized framework for world-space health bars, nameplates, cast bars, boss plates, off-screen indicators, and all paradigm-adaptive UI widgets above entities.

---

## Overview

The Widget Framework replaces per-system standalone bridges with a unified pipeline:

- **Centralized Projection** — All widget-bearing entities projected to screen space once per frame
- **Budget Enforcement** — Importance-scored entity ranking with configurable max active widget count
- **LOD Tiers** — Distance-based detail reduction (Full / Reduced / Minimal / Culled)
- **Paradigm Adaptation** — Per-paradigm profiles control widget scale, budget, LOD distances, and enabled features
- **Adapter Pattern** — Pluggable renderers (health bars, nameplates, cast bars, boss plates, off-screen indicators)
- **Accessibility** — Colorblind palettes, font scaling, reduced motion, high contrast modes
- **Backward Compatible** — When no adapters are in the scene, the old `EnemyHealthBarBridgeSystem` runs standalone

---

## Quick Start (Widget Workstation)

The fastest way to set up the widget framework is the **Widget Workstation** editor tool. It automates GameObject creation, component attachment, ScriptableObject asset creation, and inspector wiring.

### Open the Workstation

**Menu: DIG > Widget Workstation**

### Scene Status Section

The top section shows the current state of your scene at a glance:

| Indicator | Meaning |
|-----------|---------|
| Green check | Component or asset exists and is configured |
| Red X | Missing — needs to be created |

### Scene Setup

1. Check which components you want on the `WidgetFramework` GameObject
2. Click **Create Widget Framework GameObject**
3. The workstation creates the GO, adds selected components, and auto-wires references

**Available components:**

| Component | Required | Description |
|-----------|----------|-------------|
| **Health Bar Widget Adapter** | Yes | Routes health bar data to EnemyHealthBarPool |
| **Paradigm Widget Config** | Yes | Holds paradigm profile array, swaps on paradigm change |
| **Damage Number Widget Adapter** | Recommended | Routes damage numbers through widget budget/culling |
| **Boss Plate Renderer** | Optional | Screen-space boss health bar (requires Canvas + UI setup) |
| **Cast Bar Renderer** | Optional | Pooled cast bars for enemy abilities |
| **Off-Screen Indicator Renderer** | Optional | Edge-of-screen arrows for tracked entities |
| **Widget Accessibility Manager** | Optional | Colorblind modes, font scaling, reduced motion |
| **Widget Debug Overlay** | Optional | Runtime debug display of framework stats |

### Asset Setup

1. Check which ScriptableObject assets to create
2. Click **Create Selected Assets**
3. Assets are created at `Assets/Settings/Widgets/` with sensible defaults

**Available assets:**

| Asset | Path | Description |
|-------|------|-------------|
| Paradigm Widget Profile (Shooter) | `Assets/Settings/Widgets/WidgetProfile_Shooter.asset` | Default Shooter paradigm profile |
| Widget Style Config (Standard) | `Assets/Settings/Widgets/WidgetStyle_Standard.asset` | Standard health bar style |
| Widget Accessibility Config | `Assets/Settings/Widgets/WidgetAccessibility_Default.asset` | Default accessibility settings |
| Colorblind Palette (Deuteranopia) | `Assets/Settings/Widgets/ColorblindPalette_Deuteranopia.asset` | Red-green colorblind palette |
| Colorblind Palette (Protanopia) | `Assets/Settings/Widgets/ColorblindPalette_Protanopia.asset` | Red-green variant palette |
| Colorblind Palette (Tritanopia) | `Assets/Settings/Widgets/ColorblindPalette_Tritanopia.asset` | Blue-yellow colorblind palette |
| Off-Screen Indicator Config | `Assets/Settings/Widgets/OffScreenIndicatorConfig.asset` | Off-screen arrow settings |

When the Workstation creates assets AND scene components together, it **auto-wires** references:
- `ParadigmWidgetConfig._profiles` → populated with the Shooter profile
- `ParadigmWidgetConfig._fallbackProfile` → set to the Shooter profile
- `WidgetAccessibilityManager._defaultConfig` → set to the accessibility config

---

## Manual Setup (Without Workstation)

If you prefer manual setup or need to understand what the Workstation does:

### 1. Create the Framework GameObject

1. In your gameplay scene, create an empty GameObject named `WidgetFramework`
2. Add the required MonoBehaviour components (see table above)

### 2. Create Paradigm Widget Profile

**Create:** `Assets > Create > DIG/Widgets/Paradigm Widget Profile`

This is the core configuration asset. One profile per input paradigm.

#### 2.1 Budget & LOD

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Paradigm** | Which input paradigm this profile configures | Shooter | Enum |
| **Max Active Widgets** | Maximum widgets rendered simultaneously. Excess culled by importance score | 40 | 10–300 |
| **LOD Distance Multiplier** | Multiplier on LOD distance thresholds. Higher = widgets visible further. Isometric cameras benefit from 2.0 | 1.0 | 0.5–3.0 |
| **Widget Scale Multiplier** | Widget size multiplier. Isometric cameras need larger widgets (1.5–1.8) | 1.0 | 0.5–3.0 |
| **Distance Falloff** | Importance distance falloff rate. Higher = closer entities score much higher. Shooter: 3.0, ARPG: 1.0 | 3.0 | 0.5–5.0 |
| **Health Bar Y Offset** | Height above entity root for widget anchor (meters) | 2.5 | 0–5 |

**LOD tier thresholds (before multiplier):**

| Tier | Base Distance | Behavior |
|------|--------------|----------|
| Full | 0–15m | All detail shown |
| Reduced | 15–35m | Simplified display |
| Minimal | 35–60m | Dot/icon only |
| Culled | 60m+ | Not rendered |

Multiply by `LODDistanceMultiplier` for effective distances. At multiplier 2.0, Full extends to 30m.

#### 2.2 Widget Type Toggles

| Field | Default | Description |
|-------|---------|-------------|
| **Health Bar Enabled** | true | Show health bars over entities |
| **Nameplate Enabled** | false | Show name text above entities |
| **Cast Bar Enabled** | true | Show casting progress bars |
| **Buff Row Enabled** | false | Show buff/debuff icons |
| **Loot Label Enabled** | true | Show loot item labels |
| **Quest Marker Enabled** | false | Show quest objective markers |
| **Off Screen Enabled** | true | Show off-screen indicators |
| **Show Health Bar On Player** | false | Show health bar on the local player |

#### 2.3 Visual Style

| Field | Default | Description |
|-------|---------|-------------|
| **Billboard** | CameraAligned | How widgets face the camera (CameraAligned / WorldUp / Fixed) |
| **Style** | Thin | Health bar visual style (Thin / Standard) |
| **Complexity** | Compact | Nameplate detail level (Compact / Detailed) |
| **Damage Number Scale** | 1.0 | Damage number size multiplier (ARPG: 1.5) |
| **Accessibility Font Scale** | 1.0 | Font scale combined with accessibility config |

#### 2.4 Advanced

| Field | Default | Description |
|-------|---------|-------------|
| **Stacking Enabled** | true | Resolve overlapping widgets by displacing vertically |
| **Grouping Enabled** | false | Group clusters of same-type entities into a single badge |
| **Grouping Threshold** | 4 | Minimum cluster size before grouping activates |
| **Buff Row Max Icons** | 5 | Maximum buff/debuff icons per entity |
| **Boss Plate Position** | Top | Screen position for boss health plate (Top / Bottom) |

#### 2.5 Per-Paradigm Tuning Recommendations

| Paradigm | Max Widgets | LOD Mult | Scale Mult | Falloff | Key Differences |
|----------|-------------|----------|------------|---------|-----------------|
| **Shooter** | 40 | 1.0 | 1.0 | 3.0 | Standard FPS view, close engagement |
| **MMO** | 80 | 1.5 | 1.0 | 2.0 | More entities visible, larger LOD range |
| **ARPG** | 60 | 2.0 | 1.8 | 1.0 | Isometric camera needs larger widgets, flatter falloff |
| **MOBA** | 50 | 2.0 | 1.5 | 1.5 | Similar to ARPG with tighter budget |
| **TwinStick** | 30 | 1.5 | 1.3 | 2.0 | Overhead view, moderate density |

### 3. Wire Paradigm Widget Config

1. Select the `WidgetFramework` GameObject
2. In the `Paradigm Widget Config` component:
   - Drag your profile asset(s) into the **Profiles** array
   - Set the **Fallback Profile** (used when no paradigm-specific match is found)

The config subscribes to `ParadigmStateMachine.OnParadigmChanged` at runtime and swaps the active profile automatically.

**If `ParadigmWidgetConfig` is not in the scene**, all systems use hardcoded Shooter defaults (safe fallback — the framework still works).

---

## 3. Widget Style Config

**Create:** `Assets > Create > DIG/Widgets/Widget Style Config`

Controls the visual appearance of health bars.

| Field | Description | Default |
|-------|-------------|---------|
| **Style** | Which style this config defines | Standard |
| **Bar Width** | Health bar width in world units | 1.5 |
| **Bar Height** | Health bar height in world units | 0.15 |
| **Border Width** | Border thickness in world units | 0.01 |
| **Health Full Color** | Bar fill color at full health | Green (0.2, 0.85, 0.2) |
| **Health Low Color** | Bar fill color at low health | Red (0.85, 0.2, 0.2) |
| **Low Health Threshold** | Health fraction where color transitions (0–1) | 0.25 |
| **Background Color** | Bar background | Dark gray (0.1, 0.1, 0.1, 0.8) |
| **Border Color** | Border color | Black |
| **Trail Color** | Delayed damage trail color | Yellow (0.85, 0.85, 0.2, 0.8) |
| **Name Font Size** | Font size for name text | 14 |
| **Level Font Size** | Font size for level text | 12 |
| **Show Name** | Display name text above bar | true |
| **Show Level** | Display level text beside name | false |

---

## 4. Accessibility Setup

### 4.1 Widget Accessibility Config

**Create:** `Assets > Create > DIG/Widgets/Widget Accessibility Config`

| Field | Description | Default |
|-------|-------------|---------|
| **Font Scale Multiplier** | Global font scale for all text widgets | 1.0 |
| **Mode** | Colorblind mode (None / Deuteranopia / Protanopia / Tritanopia) | None |
| **Reduced Motion** | Disable spawn/despawn animations and damage shake | false |
| **High Contrast** | Add dark outlines behind text, thicker borders | false |
| **Widget Size Multiplier** | Additional scale on top of paradigm scale | 1.0 |

Wire this asset into the `Widget Accessibility Manager` component's **Default Config** field.

User preferences are persisted via PlayerPrefs and override the default config at runtime.

### 4.2 Colorblind Palettes

**Create:** `Assets > Create > DIG/Widgets/Colorblind Palette`

Each palette remaps standard game colors to colorblind-safe alternatives.

| Field | Normal | Deuteranopia | Protanopia | Tritanopia |
|-------|--------|-------------|------------|------------|
| **Health Full** | Green | Blue | Green | Green |
| **Health Low** | Red | Orange | Yellow | Red |
| **Damage Text** | Red | Orange | Yellow | Red |
| **Healing Text** | Green | Cyan | Green | Pink |
| **Critical Text** | Yellow | Yellow | Yellow | Yellow |
| **Shield Color** | Blue | Blue | Blue | Purple |
| **Buff Tint** | Cyan | Cyan | Cyan | Cyan |
| **Debuff Tint** | Red | Orange | Yellow | Red |

**Colorblind modes:**

| Mode | Affects | Prevalence |
|------|---------|------------|
| **Deuteranopia** | Red-green confusion (green → blue, red → orange) | ~8% of males |
| **Protanopia** | Red-green variant (red → yellow) | ~1% of males |
| **Tritanopia** | Blue-yellow confusion (blue → pink) | Rare (<0.01%) |

---

## 5. Off-Screen Indicators

### 5.1 Off-Screen Indicator Config

**Create:** `Assets > Create > DIG/Widgets/Off-Screen Indicator Config`

| Field | Description | Default |
|-------|-------------|---------|
| **Edge Margin** | Pixel margin from screen edge | 40 |
| **Max Indicators** | Maximum simultaneous arrows | 5 |
| **Show Distance Text** | Show meters below arrow icon | true |
| **Distance Format** | Text format ({0} = meters) | `{0:F0}m` |

**Tracked entity type priorities (lower = higher priority):**

| Priority | Type | Default Color |
|----------|------|---------------|
| 0 | Boss | Red |
| 1 | Quest Objective | Yellow |
| 2 | Party Member | Green |
| 3 | Targeted | Red |
| 4 | Waypoint | White |
| 5 | Legendary Loot | Orange |

Each entry has an **Icon** (Sprite) field for the arrow graphic and a **Color** tint.

### 5.2 Off-Screen Indicator Renderer

On the `WidgetFramework` GameObject (or a separate UI holder):

| Field | Description |
|-------|-------------|
| **Config** | The OffScreenIndicatorConfig asset |
| **Indicator Prefab** | UI prefab for the arrow (must have RectTransform) |
| **Canvas** | Screen-space overlay Canvas to parent indicators to |

---

## 6. Boss Plate Setup

The `Boss Plate Renderer` component shows a large screen-space health bar when a boss entity is visible.

### Inspector Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Plate Root** | UI root GameObject for the boss plate | None (required) |
| **Fill Image** | Image (type=Filled, fillMethod=Horizontal) for health fill | None (required) |
| **Name Text** | UI Text for boss name | None (required) |
| **Health Text** | UI Text for "current / max" display | None (required) |
| **Rect Transform** | RectTransform for positioning | None (required) |
| **Top Y Anchor** | Y anchor for top-of-screen placement | 0.92 |
| **Bottom Y Anchor** | Y anchor for bottom-of-screen placement | 0.08 |
| **Fill Lerp Speed** | Smooth health fill animation speed | 5.0 |

Position (Top vs Bottom) is controlled by `ParadigmWidgetProfile.BossPlatePosition`.

### UI Hierarchy Example

```
Canvas (Screen Space - Overlay)
 └── BossPlate (inactive by default)
     ├── Background (Image)
     ├── HealthFill (Image, type=Filled, fillMethod=Horizontal)
     ├── BossNameText (Text)
     └── HealthValueText (Text)
```

---

## 7. Cast Bar Setup

The `Cast Bar Renderer` shows ability casting progress bars above enemies.

| Field | Description | Default |
|-------|-------------|---------|
| **Cast Bar Prefab** | World-space prefab for cast bars | None (required) |
| **Pool Size** | Number of pre-instantiated cast bars | 8 |
| **Y Offset Above Health Bar** | Vertical offset above the health bar | 0.35 |

Cast bars are only shown at **LOD Full** tier (within 15m by default). They auto-hide at further distances.

---

## 8. Debug Window

**Menu: Window > DIG > Widget Debug**

The debug window shows real-time widget framework stats during play mode:

- **Framework Active** — Whether the widget pipeline is running
- **Total Projected / Visible** — Entity counts before and after budget cull
- **Registered Renderers** — List of active IWidgetRenderer adapters
- **Per-Entity Details** — Entity, distance, importance, LOD, visible status

Use this to verify the framework is active and to diagnose visibility/budget issues.

---

## 9. How It Works (Architecture)

Understanding the pipeline helps when debugging:

```
WidgetProjectionSystem (PresentationSystemGroup)
 ├── Queries Health + LocalToWorld + ShowHealthBarTag entities
 ├── Projects to screen space via VP matrix
 ├── Computes distance, importance, LOD tier
 ├── Sorts by importance, enforces budget (top N visible)
 └── Outputs: ProjectedWidgets HashMap, FrameworkActive flag

     ↓

WidgetBridgeSystem (PresentationSystemGroup, runs after projection)
 ├── Reads ProjectedWidgets
 ├── Tracks visibility transitions (new / existing / gone)
 ├── Dispatches OnWidgetVisible / OnWidgetUpdate / OnWidgetHidden
 └── Routes to registered IWidgetRenderer adapters by WidgetType

     ↓

IWidgetRenderer Adapters (MonoBehaviours)
 ├── HealthBarWidgetAdapter → EnemyHealthBarPool
 ├── BossPlateRenderer → UGUI boss health plate
 ├── CastBarRenderer → Pooled world-space cast bars
 └── OffScreenIndicatorRenderer → Screen-space edge arrows
```

When `FrameworkActive = false` (no adapters registered in scene), the old `EnemyHealthBarBridgeSystem` runs standalone. This ensures backward compatibility — removing the `WidgetFramework` GameObject reverts to pre-15.26 behavior.

---

## 10. Scene Hierarchy Example

```
Scene Root
 ├── Player Prefab                    (existing)
 ├── GameplayFeedbackManager          (existing)
 ├── ParadigmStateMachine             (existing)
 ├── CombatUIManager                  (existing — DamageNumbersProAdapter, etc.)
 │
 ├── WidgetFramework                  ← NEW
 │   ├── HealthBarWidgetAdapter       (required)
 │   ├── ParadigmWidgetConfig         (required — wire profiles array)
 │   ├── DamageNumberWidgetAdapter    (recommended)
 │   ├── WidgetAccessibilityManager   (optional — wire accessibility config)
 │   ├── BossPlateRenderer            (optional — wire UI references)
 │   ├── CastBarRenderer              (optional — wire prefab)
 │   └── OffScreenIndicatorRenderer   (optional — wire config + canvas + prefab)
 │
 └── Canvas (Screen Space - Overlay)
     ├── BossPlate UI                 (if using BossPlateRenderer)
     └── OffScreenIndicators          (if using OffScreenIndicatorRenderer)
```

---

## 11. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Workstation | Open DIG > Widget Workstation | Window opens, status rows show scene state |
| 3 | Asset creation | Check all assets, click "Create Selected Assets" | Assets created in `Assets/Settings/Widgets/` |
| 4 | Scene setup | Check adapters, click "Create Widget Framework GameObject" | GO appears in Hierarchy with components, profiles wired |
| 5 | Framework active | Enter Play Mode, open Window > DIG > Widget Debug | Shows "ACTIVE" with renderer counts > 0 |
| 6 | Health bars | Walk near enemies | Health bars appear above enemies within LOD range |
| 7 | Budget cull | Spawn 50+ enemies | Only MaxActiveWidgets bars shown, closest/targeted prioritized |
| 8 | LOD tiers | Walk away from enemies | Bars simplify at Reduced distance, disappear at Culled |
| 9 | Standing still | Stand still near enemies | Health bars remain stable (no flickering) |
| 10 | No origin bar | Look around scene | No health bar floating at world origin (0,0,0) |
| 11 | Target highlight | Lock onto an enemy | Targeted enemy always has full LOD, exempt from budget cull |
| 12 | Combat state | Attack an enemy, then wait | Bar tracks combat state and fade-out timer correctly |
| 13 | Paradigm switch | Switch paradigm (if multiple profiles set up) | Widget scale/budget/LOD adjusts per profile |
| 14 | Framework off | Remove WidgetFramework GO, enter Play Mode | Old EnemyHealthBarBridgeSystem runs (backward compatible) |
| 15 | Debug window | Window > DIG > Widget Debug in play mode | Per-entity details, distance, importance, LOD visible |
| 16 | No console errors | Play for 60 seconds | No exceptions or warnings |

---

## 12. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| No health bars at all | WidgetFramework GameObject missing from scene | Run DIG > Widget Workstation > Create Widget Framework GameObject |
| No health bars, GO exists | HealthBarWidgetAdapter not on the GO | Add HealthBarWidgetAdapter component |
| No health bars, adapter exists | EnemyHealthBarPool not in scene | Ensure the existing health bar pool singleton is present (from EPIC 15.9 setup) |
| Health bars flicker when standing still | WidgetBridgeSystem dirty-check not dispatching updates | Ensure you're on the latest code — dirty-check was removed in favor of per-frame updates |
| Health bar floating at origin | Stale bar from old bridge not cleaned up | Ensure HealthBarWidgetAdapter calls pool.CleanupDeadEntities in OnFrameEnd (latest code) |
| Health bars disappear at close range | Entity LOD set to Culled | Check ParadigmWidgetProfile LOD Distance Multiplier — increase if bars vanish too close |
| Too few health bars visible | MaxActiveWidgets too low | Increase MaxActiveWidgets in the active ParadigmWidgetProfile |
| Bars too small in isometric view | WidgetScaleMultiplier too low | Set WidgetScaleMultiplier to 1.5–1.8 for isometric paradigms |
| Bars at wrong height | HealthBarYOffset incorrect | Adjust HealthBarYOffset in ParadigmWidgetProfile (default 2.5m) |
| Paradigm not switching profiles | ParadigmWidgetConfig._profiles array empty | Wire profile assets into the Profiles array (Workstation does this automatically) |
| No colorblind remapping | WidgetAccessibilityManager not in scene or no config wired | Add the component and wire the WidgetAccessibilityConfig asset |
| Debug window shows "INACTIVE" | No IWidgetRenderer adapters registered | Add at least HealthBarWidgetAdapter to the scene |
| Boss plate not appearing | UI references not wired on BossPlateRenderer | Wire Plate Root, Fill Image, Name Text, Health Text, and Rect Transform in Inspector |
| Off-screen arrows not showing | OffScreenIndicatorRenderer missing Canvas or Prefab | Wire the Canvas and Indicator Prefab fields |
| Cast bars not showing | CastBarRenderer missing prefab | Wire the Cast Bar Prefab field. Cast bars only show at LOD Full (within 15m) |

---

## 13. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Damage numbers, combat feedback | SETUP_GUIDE_15.9, SETUP_GUIDE_15.22 |
| Input paradigm framework, paradigm switching | SETUP_GUIDE_15.20 |
| Procedural motion, HUD sway | SETUP_GUIDE_15.25 |
| Physics optimization, collision filters | SETUP_GUIDE_15.23 |
| Combat resolution, damage events | SETUP_GUIDE_15.29 |
| Corpse lifecycle (health bar removal on death) | SETUP_GUIDE_16.3 |
| **Smart HUD & Widget Ecosystem** | **This guide (15.26)** |

---

## 14. File Reference

### Config (ScriptableObjects)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Config/ParadigmWidgetProfile.cs` | Per-paradigm widget tuning SO |
| `Assets/Scripts/Widgets/Config/WidgetStyleConfig.cs` | Health bar visual style SO |
| `Assets/Scripts/Widgets/Config/WidgetAccessibilityConfig.cs` | Accessibility settings SO |
| `Assets/Scripts/Widgets/Config/ColorblindPalette.cs` | Colorblind color remapping SO |
| `Assets/Scripts/Widgets/Config/OffScreenIndicatorConfig.cs` | Off-screen indicator settings SO |

### Runtime (MonoBehaviours)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Config/ParadigmWidgetConfig.cs` | Runtime paradigm profile manager singleton |
| `Assets/Scripts/Widgets/Config/WidgetAccessibilityManager.cs` | Accessibility state manager singleton |
| `Assets/Scripts/Widgets/Adapters/HealthBarWidgetAdapter.cs` | Health bar adapter (routes to EnemyHealthBarPool) |
| `Assets/Scripts/Widgets/Adapters/DamageNumberWidgetAdapter.cs` | Damage number adapter |
| `Assets/Scripts/Widgets/Rendering/BossPlateRenderer.cs` | Screen-space boss health plate |
| `Assets/Scripts/Widgets/Rendering/CastBarRenderer.cs` | Pooled world-space cast bars |
| `Assets/Scripts/Widgets/Rendering/OffScreenIndicatorRenderer.cs` | Off-screen edge arrows |

### ECS Systems (auto-created, no setup required)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Systems/WidgetProjectionSystem.cs` | World-to-screen projection, LOD, importance, budget |
| `Assets/Scripts/Widgets/Systems/WidgetBridgeSystem.cs` | Dispatches lifecycle callbacks to adapters |

### Editor

| File | Purpose |
|------|---------|
| `Assets/Editor/WidgetWorkstation/WidgetWorkstationWindow.cs` | Widget Workstation setup tool (DIG > Widget Workstation) |
| `Assets/Editor/Widgets/WidgetDebugWindow.cs` | Runtime debug window (Window > DIG > Widget Debug) |

### Data

| File | Purpose |
|------|---------|
| `Assets/Scripts/Widgets/Data/WidgetProjection.cs` | Per-entity projection result struct |
| `Assets/Scripts/Widgets/Data/WidgetCameraData.cs` | Static camera data updated each frame |
| `Assets/Scripts/Widgets/Data/WidgetImportanceComputer.cs` | Importance scoring utility |
| `Assets/Scripts/Widgets/Rendering/WidgetRenderData.cs` | Data passed from bridge to adapters |
| `Assets/Scripts/Widgets/Rendering/IWidgetRenderer.cs` | Adapter interface |
| `Assets/Scripts/Widgets/Rendering/WidgetRendererRegistry.cs` | Static adapter registry |
| `Assets/Scripts/Widgets/Components/WidgetTypes.cs` | WidgetType, WidgetFlags, WidgetLODTier enums |
