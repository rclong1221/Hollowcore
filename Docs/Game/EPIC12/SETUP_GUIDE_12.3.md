# EPIC 12.3 Setup Guide: Mini-Map Integration & Tactical Overlay Config

**Status:** Planned
**Requires:** EPIC 12.1 (Scar Map Data Model), EPIC 12.2 (Scar Map Rendering); Optional: EPIC 6 (Gate Screen), EPIC 4 (District graph)

---

## Overview

Configure the Scar Map as a tactical decision-making tool: a mini-map on the Gate Screen for informed gate selection, a full-view mode from the pause menu, district hover summaries, route highlighting, and risk assessment. This guide covers the Gate Screen mini-map rect, the tactical bridge system, tooltip configuration, and route highlight materials.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Gate Screen UI prefab | Gate selection panel (EPIC 6) | Host for the mini-map variant |
| ScarMapOverlay prefab | Full renderer (EPIC 12.2) | Full-view mode target |
| ScarMapDistrictSummary buffer | Aggregated data (EPIC 12.1) | Source for hover tooltips |
| District graph edges | Gate connections | Route highlight path data |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| ScarMapTacticalComponents.cs | `Assets/Scripts/ScarMap/Components/` | C# (ECS components) |
| ScarMapTacticalBridge.cs | `Assets/Scripts/ScarMap/Systems/` | C# (SystemBase) |
| ScarMapGateInfoSystem.cs | `Assets/Scripts/ScarMap/Systems/` | C# (SystemBase) |
| ScarMapRouteHighlightSystem.cs | `Assets/Scripts/ScarMap/Systems/` | C# (SystemBase) |
| ScarMapHoverSystem.cs | `Assets/Scripts/ScarMap/Systems/` | C# (SystemBase) |
| GateScreenMapConfigAuthoring | Gate Screen prefab | C# (MonoBehaviour + Baker) |
| RouteHighlight.mat | `Assets/Materials/UI/ScarMap/` | Material (additive, cyan glow) |
| ScarMapTooltip.prefab | `Assets/Prefabs/UI/ScarMap/` | UI Prefab |

---

## 1. Configure the Gate Screen Mini-Map
**Create:** `GateScreenMapConfigAuthoring` MonoBehaviour, attach to Gate Screen prefab.

### 1.1 GateScreenMapConfig Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| MiniMapRect | Screen-space rect (normalized 0-1): x, y, width, height | (0.65, 0.1, 0.3, 0.35) | 0.0-1.0 per axis |
| VisibleHopRadius | District hops visible from current position | 2 | 1-4 |
| ShowMarkerIcons | Whether marker icons appear on mini-map | true | bool |
| MiniMapIconScale | Icon scale reduction for mini-map context | 0.6 | 0.2-1.0 |
| AutoCenter | Auto-center viewport on player's current district | true | bool |

**Tuning tip:** MiniMapRect `(0.65, 0.1, 0.3, 0.35)` places the mini-map in the bottom-right quadrant of the Gate Screen. Adjust x/y to move it. Keep width/height proportional to avoid distortion. VisibleHopRadius=2 shows the current district plus 2 hops in each direction, which gives the player enough context for gate decisions without cluttering the screen.

---

## 2. Configure the Tactical Bridge

The `ScarMapTacticalBridge` system switches the Scar Map renderer between contexts:

### 2.1 ScarMapContext Modes
| Context | Trigger | Viewport | Interactions |
|---------|---------|----------|-------------|
| None | Default | Hidden | None |
| GateScreen | Gate Screen opens | Mini-map rect, centered on player district | Gate hover highlights route |
| PauseMenu | Pause menu "Scar Map" button | Full screen | Full pan/zoom/hover |
| Standalone | M key press | Full screen | Full pan/zoom/hover |

### 2.2 Wiring Gate Screen Events
The Gate Screen UI must fire events that `ScarMapTacticalBridge` listens for:

| UI Event | System Action |
|----------|---------------|
| Gate option hovered | Set `ScarMapTacticalState.HoveredGateTarget` = destination district ID |
| Gate option unhovered | Clear HoveredGateTarget, clear RouteHighlight buffer |
| Gate Screen opened | Set Context = GateScreen, populate GateOptionA/B/C |
| Gate Screen closed | Set Context = None |

---

## 3. Configure Route Highlights

### 3.1 Route Highlight Material
**Create:** `Assets/Materials/UI/ScarMap/RouteHighlight.mat`

| Property | Value |
|----------|-------|
| Shader | UI/Default or custom glow shader |
| Blend Mode | Additive |
| Color | Cyan (#00BCD4) with alpha 0.8 |
| Glow Radius | 4px (if using custom shader) |

### 3.2 ScarMapRouteHighlight Buffer
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| FromDistrictId | Source district of segment | (computed) | -- |
| ToDistrictId | Destination district of segment | (computed) | -- |
| Intensity | Highlight opacity: 1.0 = primary, 0.5 = branch | 1.0 | 0.0-1.0 |

**Tuning tip:** When the player hovers a gate option, the system computes the shortest path from the current district to the destination and fills the route highlight buffer. Primary path segments get Intensity=1.0; any branching alternatives get 0.5.

---

## 4. Configure Hover Tooltips

### 4.1 Tooltip Prefab
**Create:** `Assets/Prefabs/UI/ScarMap/ScarMapTooltip.prefab`

| Element | Type | Notes |
|---------|------|-------|
| Background | Image | Semi-transparent black (#000000 alpha 0.85), rounded corners |
| Title | TextMeshPro | District name, bold, 18pt |
| FrontPhaseIndicator | Image + Text | Color dot + "Phase 2 (Dangerous)" |
| BodyCount | TextMeshPro | "2 bodies (lootable)" |
| EchoCount | TextMeshPro | "1 active echo (Epic reward)" |
| RivalPresence | TextMeshPro | "Rival team 'Chrome Dogs' reported" (hidden if no rival) |
| EventCount | TextMeshPro | "Merchant discovered" |
| RiskBadge | Image | Green/Yellow/Orange/Red circle |

### 4.2 Risk Assessment Colors
| Risk Level | Color | Criteria |
|------------|-------|----------|
| Green | #2ECC71 | Front phase 0, no echoes, no rivals |
| Yellow | #F1C40F | Front phase 1, or 1-2 echoes |
| Orange | #E67E22 | Front phase 2-3, or 3+ echoes |
| Red | #E74C3C | Front phase 4, or rival presence |

### 4.3 Unvisited District Tooltip
If the district has not been visited (`ScarMapDistrictSummary.IsVisited == false`), the tooltip displays:
```
[District Name]
Unexplored -- no intel available
```

---

## 5. Wire Pause Menu Integration

### 5.1 Pause Menu Button
Add a "Scar Map" button to the pause menu UI. On click:
1. Set `ScarMapTacticalState.Context = PauseMenu`
2. Set `ScarMapViewState.IsVisible = true`
3. Set `ScarMapViewState.ZoomLevel = ExpeditionOverview`

### 5.2 Return from Scar Map
On Escape or Back button in Scar Map:
1. Set `ScarMapTacticalState.Context = None`
2. Set `ScarMapViewState.IsVisible = false`
3. Return to pause menu

---

## Scene & Subscene Checklist

- [ ] Gate Screen prefab has `GateScreenMapConfigAuthoring` component
- [ ] `RouteHighlight.mat` created with additive blending and cyan color
- [ ] `ScarMapTooltip.prefab` created with all text fields
- [ ] Pause menu has "Scar Map" button wired to set Context = PauseMenu
- [ ] Assembly `Hollowcore.ScarMap` references `Hollowcore.GateScreen` and `Hollowcore.UI`
- [ ] "You are here" marker sprite assigned for current district indicator

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| GateScreenMapConfigAuthoring missing from Gate Screen prefab | Mini-map does not appear on Gate Screen | Add the authoring component to the Gate Screen prefab |
| MiniMapRect overlaps Gate Screen buttons | Mini-map covers gate selection options | Adjust MiniMapRect to non-overlapping region (e.g., bottom-right) |
| Route highlight buffer not cleared on unhover | Previous route stays highlighted after moving mouse off gate option | Clear ScarMapRouteHighlight buffer and set RouteHighlightActive=false on hover exit |
| Tooltip shows stale data | Old district summary displayed after markers change | Tooltip reads ScarMapDistrictSummary each frame, which is recalculated when IsDirty |
| Risk assessment always green | Never shows orange/red even at high Front phases | Check risk calculation reads DominantFrontPhase, not raw FrontGradient marker count |
| "You are here" marker missing | Player cannot locate their current district on the map | Ensure ScarMapTacticalState.PlayerDistrictId is updated from current district tracking |

---

## Verification

- [ ] Mini-map appears on Gate Screen at configured rect position
- [ ] Mini-map centers on player's current district
- [ ] Mini-map shows districts within VisibleHopRadius hops only
- [ ] Mini-map icon scale correctly reduced from full-view
- [ ] Hovering gate option highlights route on mini-map with cyan glow
- [ ] Destination district pulses on route highlight
- [ ] Gate tooltip shows Front phase, body count, echo count, rival presence
- [ ] Unvisited districts show "Unexplored" text
- [ ] Risk badge color matches district danger level
- [ ] Full-view Scar Map accessible from pause menu
- [ ] Standalone Scar Map accessible via M key
- [ ] "You are here" marker visible on player's current district
- [ ] Route highlight clears when gate hover exits
- [ ] Context switching (GateScreen <-> PauseMenu <-> Standalone) updates viewport correctly
