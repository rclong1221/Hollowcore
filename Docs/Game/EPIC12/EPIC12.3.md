# EPIC 12.3: Scar Map as Tactical Tool

**Status**: Planning
**Epic**: EPIC 12 — Scar Map
**Priority**: High — Core decision-support during gameplay
**Dependencies**: EPIC 12.1 (Scar Map Data Model), EPIC 12.2 (Scar Map Rendering); Optional: EPIC 6 (Gate Screen integration), EPIC 4 (District graph)

---

## Overview

The Scar Map becomes a tactical decision-making tool by integrating into the Gate Screen as a mini-map and providing a full-view mode accessible from the pause menu. Players use it to answer key expedition questions: Where are my bodies? Which echoes are still active? How bad is the Front in that district? Should I push forward or backtrack? District hover summaries provide at-a-glance intel, and route planning support highlights the consequences of different paths. The tactical bridge system connects Scar Map data to the gate selection UI, ensuring players make informed gate choices.

---

## Component Definitions

### ScarMapTacticalState (IComponentData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapTacticalComponents.cs
using Unity.Entities;

namespace Hollowcore.ScarMap
{
    public enum ScarMapContext : byte
    {
        None = 0,          // Not displayed
        GateScreen = 1,    // Mini-map on gate selection screen
        PauseMenu = 2,     // Full view in pause menu
        Standalone = 3     // Opened via keybind (M key)
    }

    /// <summary>
    /// Tracks which context the Scar Map is being viewed in.
    /// Different contexts have different interaction rules and display sizes.
    /// Singleton on client entity.
    /// </summary>
    public struct ScarMapTacticalState : IComponentData
    {
        /// <summary>Current display context.</summary>
        public ScarMapContext Context;

        /// <summary>District the player is currently in (for "you are here" marker).</summary>
        public int PlayerDistrictId;

        /// <summary>District IDs of available gates from current position.</summary>
        public int GateOptionA;
        public int GateOptionB;
        public int GateOptionC;

        /// <summary>Which gate option is currently hovered (-1 = none).</summary>
        public int HoveredGateTarget;

        /// <summary>Whether route highlight is active.</summary>
        public bool RouteHighlightActive;
    }
}
```

### ScarMapRouteHighlight (IBufferElementData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapTacticalComponents.cs
using Unity.Entities;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Highlighted route segments on the Scar Map.
    /// Populated when player hovers a gate option to preview the path.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ScarMapRouteHighlight : IBufferElementData
    {
        /// <summary>Source district of this route segment.</summary>
        public int FromDistrictId;

        /// <summary>Destination district of this route segment.</summary>
        public int ToDistrictId;

        /// <summary>Highlight intensity (1.0 = primary path, 0.5 = secondary/branching).</summary>
        public float Intensity;
    }
}
```

### GateScreenMapConfig (IComponentData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapTacticalComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Configuration for the Gate Screen mini-map variant.
    /// Controls size, position, and simplified display rules.
    /// </summary>
    public struct GateScreenMapConfig : IComponentData
    {
        /// <summary>Screen-space rect for the mini-map (normalized 0-1).</summary>
        public float4 MiniMapRect; // x, y, width, height

        /// <summary>How many district hops to show from current position.</summary>
        public int VisibleHopRadius;

        /// <summary>Whether to show marker icons on mini-map (false = only Front gradient).</summary>
        public bool ShowMarkerIcons;

        /// <summary>Scale factor for icons in mini-map context.</summary>
        public float MiniMapIconScale;

        /// <summary>Whether to auto-center on player's current district.</summary>
        public bool AutoCenter;
    }
}
```

---

## Systems

### ScarMapTacticalBridge

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapTacticalBridge.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateBefore: ScarMapRenderer (EPIC 12.2)
//
// Connects Scar Map data to the gate selection and pause menu contexts:
//   1. Track ScarMapTacticalState.Context:
//      a. GateScreen: configure ScarMapViewState for mini-map mode
//         - Set ZoomLevel to ExpeditionOverview
//         - Center on PlayerDistrictId
//         - Constrain viewport to VisibleHopRadius districts
//         - Reduce icon scale via MiniMapIconScale
//      b. PauseMenu/Standalone: configure for full-view mode
//         - Full viewport, all districts visible
//         - Full icon scale, all interactions enabled
//   2. Update PlayerDistrictId from current district tracking
//   3. On gate hover event from Gate Screen UI:
//      a. Set HoveredGateTarget to destination district
//      b. Populate ScarMapRouteHighlight buffer with path segments
//      c. Set RouteHighlightActive = true
//   4. On gate hover exit:
//      a. Clear ScarMapRouteHighlight buffer
//      b. Set RouteHighlightActive = false
```

### ScarMapGateInfoSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapGateInfoSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: ScarMapTacticalBridge
//
// Provides tactical summaries for gate selection:
//   1. For each available gate destination:
//      a. Look up ScarMapDistrictSummary for the target district
//      b. Compose a summary string:
//         - Front phase: "Phase 2 (Dangerous)"
//         - Bodies: "2 bodies (lootable)"
//         - Echoes: "1 active echo (Epic reward)"
//         - Rivals: "Rival team 'Chrome Dogs' reported in area"
//         - Events: "Merchant discovered"
//      c. Risk assessment:
//         - Green: no Front advancement, no threats
//         - Yellow: moderate Front, some echoes
//         - Orange: high Front, multiple threats
//         - Red: critical Front, rival hostiles possible
//   2. Push summary data to Gate Screen UI via bridge pattern
//   3. If district is unvisited: show "Unexplored — no intel available"
```

### ScarMapRouteHighlightSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapRouteHighlightSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: ScarMapTacticalBridge
// UpdateBefore: ScarMapRenderer (EPIC 12.2)
//
// Renders route highlights on the Scar Map:
//   1. If RouteHighlightActive == false, early-out
//   2. For each ScarMapRouteHighlight entry:
//      a. Draw highlighted connection line between FromDistrictId and ToDistrictId
//      b. Line color: bright cyan/teal with glow effect
//      c. Intensity scales opacity and glow radius
//   3. Highlight destination district with pulsing border
//   4. Show destination district summary tooltip
```

### ScarMapHoverSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapHoverSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: ScarMapInputSystem (EPIC 12.2)
// UpdateBefore: ScarMapRenderer
//
// District and marker hover summaries:
//   1. Detect cursor position over district layout nodes
//   2. On district hover:
//      a. Read ScarMapDistrictSummary for hovered district
//      b. Compose hover tooltip:
//         - District name and type
//         - Front phase with color indicator
//         - Body count, echo count, event count
//         - Rival presence indicator
//         - "3 gates available" (connection count)
//      c. Push tooltip data to UI overlay
//   3. On marker hover (detail mode):
//      a. Read specific ScarMapMarker at hovered zone
//      b. Compose marker-specific tooltip:
//         - Skull: "Operator body — Tier 3 gear, looted: No"
//         - EchoSpiral: "Active echo — Rare reward, Difficulty 3"
//         - Star: "Merchant discovered"
//         - RivalMarker: "Chrome Dogs — Last seen 2 transitions ago"
//      c. Push to tooltip UI
```

---

## Setup Guide

1. **Add ScarMapTacticalComponents.cs** to `Assets/Scripts/ScarMap/Components/`
2. **Create GateScreenMapConfigAuthoring** on Gate Screen prefab:
   - MiniMapRect: (0.65, 0.1, 0.3, 0.35) — bottom-right quadrant
   - VisibleHopRadius: 2
   - ShowMarkerIcons: true
   - MiniMapIconScale: 0.6
   - AutoCenter: true
3. **Wire Gate Screen integration**:
   - Gate Screen UI fires gate hover events → ScarMapTacticalBridge listens
   - ScarMapGateInfoSystem pushes summaries to Gate Screen tooltip panel
4. **Wire pause menu integration**:
   - Pause menu "Scar Map" button sets ScarMapTacticalState.Context = PauseMenu
   - Full-screen Scar Map overlay from EPIC 12.2 activates
5. **Create route highlight material**: `Assets/Materials/UI/ScarMap/RouteHighlight.mat` — cyan glow, additive blending
6. **Create tooltip prefab**: `Assets/Prefabs/UI/ScarMap/ScarMapTooltip.prefab` — text panel with semi-transparent background
7. **Add assembly references** to `Hollowcore.GateScreen` (EPIC 6), `Hollowcore.UI`

---

## Verification

- [ ] Mini-map appears on Gate Screen at configured rect position
- [ ] Mini-map centers on player's current district
- [ ] Mini-map shows districts within VisibleHopRadius hops
- [ ] Mini-map icon scale correctly reduced from full-view
- [ ] Hovering a gate option highlights the route on mini-map
- [ ] Route highlight uses bright cyan with glow effect
- [ ] Destination district pulses on route highlight
- [ ] Gate tooltip shows tactical summary (Front, bodies, echoes, rivals, events)
- [ ] Unvisited districts show "Unexplored" instead of data
- [ ] Risk assessment color (green/yellow/orange/red) matches district danger
- [ ] Full-view Scar Map accessible from pause menu
- [ ] Standalone Scar Map accessible via M key
- [ ] District hover tooltip shows ScarMapDistrictSummary data
- [ ] Marker hover tooltip shows type-specific detail (detail zoom mode)
- [ ] "You are here" marker visible on player's current district
- [ ] Route highlight clears when gate hover exits
- [ ] Switching between GateScreen and PauseMenu contexts updates viewport correctly

---

## Live Tuning

| Parameter | Component | Effect |
|-----------|-----------|--------|
| MiniMapRect | GateScreenMapConfig | Screen-space position/size of gate screen mini-map |
| VisibleHopRadius | GateScreenMapConfig | Number of district hops visible (1–4) |
| ShowMarkerIcons | GateScreenMapConfig | Toggle icons on mini-map |
| MiniMapIconScale | GateScreenMapConfig | Icon scale reduction for mini-map context |

---

## Debug Visualization

```csharp
// File: Assets/Scripts/ScarMap/Debug/ScarMapTacticalDebug.cs
// Development builds only:
//   - Show mini-map viewport rect: cyan wireframe on screen
//   - Show route highlight path: log FromDistrictId→ToDistrictId chain
//   - Show risk assessment calculation: overlay green/yellow/orange/red badge
//     with breakdown text (Front phase, body count, echo count)
//   - Show hover state: highlight which district/marker is under cursor
```
