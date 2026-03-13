# EPIC 12.2: Scar Map Rendering

**Status**: Planning
**Epic**: EPIC 12 — Scar Map
**Priority**: High — Visual layer for all Scar Map features
**Dependencies**: EPIC 12.1 (Scar Map Data Model), EPIC 4 (District graph structure), Framework: UI/

---

## Overview

The Scar Map renderer transforms the marker data model into a stylized graph view with a data-punk cyberpunk aesthetic. Districts appear as recognizable silhouettes connected by gate lines, overlaid with animated marker icons. The renderer supports two zoom levels: expedition overview (all districts at a glance) and district detail (zone-level marker placement). Color-coded Front gradients wash over district silhouettes, bleed tendrils animate between connected districts, and marker icons pulse, twinkle, or glow based on their type and activity state. The renderer is a managed system operating in PresentationSystemGroup, reading ECS data and driving a UI Toolkit or Canvas-based overlay.

---

## Component Definitions

### ScarMapViewState (IComponentData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapViewComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.ScarMap
{
    public enum ScarMapZoomLevel : byte
    {
        ExpeditionOverview = 0,  // All districts visible, small icons
        DistrictDetail = 1       // Single district zoomed, zone-level markers
    }

    /// <summary>
    /// Client-side view state for the Scar Map renderer.
    /// Singleton on client. NOT replicated — purely visual.
    /// </summary>
    public struct ScarMapViewState : IComponentData
    {
        /// <summary>Current zoom level.</summary>
        public ScarMapZoomLevel ZoomLevel;

        /// <summary>District ID being viewed in detail mode (-1 = overview).</summary>
        public int FocusedDistrictId;

        /// <summary>Camera pan offset in normalized map space.</summary>
        public float2 PanOffset;

        /// <summary>Whether the Scar Map overlay is currently visible.</summary>
        public bool IsVisible;

        /// <summary>Animation time accumulator for pulsing/glowing effects.</summary>
        public float AnimationTime;

        /// <summary>Whether markers should animate (false during screenshot capture).</summary>
        public bool AnimationsEnabled;
    }
}
```

### ScarMapRenderConfig (IComponentData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapViewComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Visual configuration for the Scar Map renderer.
    /// Singleton set via authoring.
    /// </summary>
    public struct ScarMapRenderConfig : IComponentData
    {
        // Front phase colors (packed as float4 RGBA)
        public float4 FrontColorGreen;     // Phase 0 — safe
        public float4 FrontColorYellow;    // Phase 1 — cautious
        public float4 FrontColorOrange;    // Phase 2 — dangerous
        public float4 FrontColorRed;       // Phase 3 — critical
        public float4 FrontColorCritical;  // Phase 4 — lethal

        // Marker icon animation speeds
        public float EchoSpiralPulseSpeed;
        public float StarTwinkleSpeed;
        public float BleedTendrilFlowSpeed;

        // Layout
        public float DistrictSpacing;        // Distance between district nodes
        public float GateLineWidth;          // Thickness of gate connection lines
        public float MarkerIconScale;        // Base icon size
        public float DetailZoomScale;        // Multiplier when zoomed into district

        // Visual identity
        public float CircuitLineOpacity;     // Background circuitry pattern
        public float HolographicFlicker;     // Subtle flicker rate for hologram feel
    }
}
```

### ScarMapDistrictLayout (IBufferElementData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapViewComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Pre-computed layout position for each district on the Scar Map.
    /// Generated once from the expedition graph, stored as buffer.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ScarMapDistrictLayout : IBufferElementData
    {
        /// <summary>District ID matching graph node.</summary>
        public int DistrictId;

        /// <summary>Position in normalized map space (0-1 range).</summary>
        public float2 Position;

        /// <summary>Silhouette variant index (selects district shape sprite).</summary>
        public int SilhouetteVariant;

        /// <summary>Scale factor (larger districts rendered bigger).</summary>
        public float Scale;

        /// <summary>Whether this district has been visited by the player.</summary>
        public bool IsVisited;
    }
}
```

---

## Systems

### ScarMapLayoutSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapLayoutSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Generates district layout from expedition graph (runs once per expedition):
//   1. Read district graph from EPIC 4 (node IDs, edges/gates)
//   2. Apply force-directed layout algorithm to position districts:
//      a. Districts repel each other (prevents overlap)
//      b. Gate connections attract connected districts
//      c. Start district centered, extraction districts at edges
//   3. Normalize positions to (0,1) range
//   4. Assign silhouette variants based on district type
//   5. Write ScarMapDistrictLayout buffer
//   6. Only recalculates if expedition graph changes (new districts discovered)
```

### ScarMapRenderer

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapRenderer.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: ScarMapLayoutSystem
//
// Managed SystemBase — drives UI rendering from ECS data:
//   1. If ScarMapViewState.IsVisible == false, early-out
//   2. Update AnimationTime for effect cycling
//   3. Render district layer:
//      a. For each ScarMapDistrictLayout entry:
//         - Draw district silhouette sprite at position
//         - Apply Front gradient color wash from ScarMapDistrictSummary.DominantFrontPhase
//         - Visited districts: full opacity. Unvisited: ghosted/dimmed
//         - Focused district (detail mode): enlarged, centered
//   4. Render gate connection lines:
//      a. For each gate edge in district graph:
//         - Draw line between connected district positions
//         - Open gates: solid line. Locked gates: dashed
//   5. Render bleed tendrils:
//      a. For each BleedTendril marker:
//         - Animated flowing line from source district to Metadata (target district)
//         - Color: Front phase gradient
//         - Flow direction indicates bleed propagation
//   6. Render marker icons (overview mode):
//      a. For each ScarMapMarker, place icon at district position
//         - Skull: skull icon, red tint
//         - EchoSpiral: spiral icon, animated pulse (inactive: dimmed, no pulse)
//         - Star: star icon, twinkle animation
//         - RevivalNode: revival icon, green glow (inactive: grey)
//         - RivalMarker: rival team icon, orange tint
//         - Completed: checkmark icon, gold tint
//         - Death: large skull with cross, distinct from body Skull
//      b. Icon stacking: multiple markers at same district cluster around node
//   7. Render marker icons (detail mode):
//      a. Place icons at zone positions within district silhouette
//      b. Zone positions derived from seed-based zone layout
//      c. FrontGradient renders as zone-level color cells
//   8. Render background:
//      a. Circuitry line pattern at CircuitLineOpacity
//      b. Subtle holographic flicker overlay
//      c. Data-punk grid lines
//   9. Render hover tooltips:
//      a. District hover: show ScarMapDistrictSummary
//      b. Marker hover: show type-specific detail
```

### ScarMapInputSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapInputSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateBefore: ScarMapRenderer
//
// Handles Scar Map interaction:
//   1. Toggle visibility (keybind or pause menu)
//   2. Pan: click-drag to move viewport
//   3. Zoom: scroll wheel or click on district to enter detail mode
//   4. Back: click outside district or press back to return to overview
//   5. Hover detection: ray vs district layout positions for tooltips
//   6. Updates ScarMapViewState accordingly
```

---

## Setup Guide

1. **Add ScarMapViewComponents.cs** to `Assets/Scripts/ScarMap/Components/`
2. **Create ScarMapRenderConfigAuthoring** MonoBehaviour with inspector-friendly color pickers and speed sliders:
   - Front colors: green (#2ECC71), yellow (#F1C40F), orange (#E67E22), red (#E74C3C), critical (#8E44AD)
   - EchoSpiralPulseSpeed: 2.0, StarTwinkleSpeed: 3.0, BleedTendrilFlowSpeed: 1.5
   - DistrictSpacing: 0.15, GateLineWidth: 2.0, MarkerIconScale: 1.0
3. **Create marker icon sprites**: `Assets/Art/UI/ScarMap/Icons/` — one per MarkerType (9 icons)
4. **Create district silhouette sprites**: `Assets/Art/UI/ScarMap/Silhouettes/` — 4-6 variants matching district types
5. **Create background textures**: circuitry pattern, holographic overlay, grid lines
6. **Create ScarMap UI prefab**: `Assets/Prefabs/UI/ScarMapOverlay.prefab` — full-screen overlay
7. **Wire keybind** for Scar Map toggle (default: M key, also accessible from pause menu)
8. **Add assembly reference** to `Hollowcore.UI`

---

## Verification

- [ ] Scar Map overlay toggles on/off correctly
- [ ] District silhouettes positioned via force-directed layout from graph
- [ ] Gate connection lines drawn between connected districts
- [ ] Front gradient colors correctly map to phase values (0-4)
- [ ] Visited districts render at full opacity, unvisited districts dimmed
- [ ] Skull markers display skull icon with red tint
- [ ] EchoSpiral markers pulse when active, dim when inactive
- [ ] Star markers twinkle at configured speed
- [ ] BleedTendril lines animate flow between districts
- [ ] RivalMarker icons display with orange tint
- [ ] Death markers visually distinct from Skull (body) markers
- [ ] Zoom to district detail mode shows zone-level marker placement
- [ ] District hover tooltip shows summary (Front phase, body count, echo count)
- [ ] Marker hover tooltip shows type-specific detail
- [ ] Pan and zoom controls responsive
- [ ] Background circuitry pattern renders at correct opacity
- [ ] Holographic flicker effect subtle and non-distracting
- [ ] Icon stacking handles multiple markers at same district without overlap

---

## Live Tuning

| Parameter | Component | Effect |
|-----------|-----------|--------|
| MarkerIconScale | ScarMapRenderConfig | Base size of all marker icons |
| DetailZoomScale | ScarMapRenderConfig | Magnification when zoomed into a district |
| DistrictSpacing | ScarMapRenderConfig | Force-directed layout: minimum district node separation |
| FrontColor[0–4] | ScarMapRenderConfig | Phase gradient colors, live-editable |
| CircuitLineOpacity | ScarMapRenderConfig | Background data-punk pattern intensity |
| HolographicFlicker | ScarMapRenderConfig | Flicker rate, set to 0 to disable |

Renderer reads singleton each frame; inspector changes take effect immediately in Play Mode.

---

## Debug Visualization

```csharp
// File: Assets/Scripts/ScarMap/Debug/ScarMapRenderDebug.cs
// Overlay toggles (Development builds only):
//   - Show layout bounding boxes: green wireframe around each district silhouette AABB
//   - Show zone positions: yellow dots at each ScarMapZoneMapping.NormalizedPosition
//   - Show icon stacking offsets: red lines from zone center to final icon position
//   - Show gate edges: magenta lines with labels (open/locked)
//   - FPS counter: renderer frame time in ms (top-right corner)
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/ScarMap/ScarMapLayoutTest.cs
// [Test] ForceDirectedLayout_NoOverlap
//   Generate layout from 12-district graph, verify no two district positions
//   overlap (min distance > DistrictSpacing * 0.9).
//
// [Test] ForceDirectedLayout_Deterministic
//   Same graph input produces identical layout positions across 10 runs.
//
// [Test] MarkerIconStacking_8MarkersPerDistrict_NoOverlap
//   Place 8 markers on a single district, verify all icon positions distinct
//   with minimum separation > MarkerIconScale * 0.3.
```
