# EPIC 12.2 Setup Guide: Scar Map Rendering — Camera, Canvas & Visual Config

**Status:** Planned
**Requires:** EPIC 12.1 (Scar Map Data Model), EPIC 4 (District graph structure), Framework: UI/

---

## Overview

Configure the Scar Map renderer: the visual layer that transforms marker data into a stylized graph view with a data-punk cyberpunk aesthetic. This guide covers the render config singleton, the layout system, the UI overlay prefab, camera and canvas setup, district silhouettes, marker icon atlas, and animation tuning.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Expedition entity | `ScarMapState` + `ScarMapMarker` buffer (EPIC 12.1) | Data source for rendering |
| District graph | EPIC 4 district node/edge data | Input for force-directed layout |
| Marker icon atlas | 9 sprites (one per MarkerType) | Visual icons on the map |
| District silhouette sprites | 4-6 variants | Recognizable district shapes |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| ScarMapViewComponents.cs | `Assets/Scripts/ScarMap/Components/` | C# (ECS components) |
| ScarMapRenderConfigAuthoring.cs | `Assets/Scripts/ScarMap/Authoring/` | C# (MonoBehaviour + Baker) |
| ScarMapLayoutSystem.cs | `Assets/Scripts/ScarMap/Systems/` | C# (SystemBase, managed) |
| ScarMapRenderer.cs | `Assets/Scripts/ScarMap/Systems/` | C# (SystemBase, managed) |
| ScarMapInputSystem.cs | `Assets/Scripts/ScarMap/Systems/` | C# (SystemBase, managed) |
| ScarMapOverlay.prefab | `Assets/Prefabs/UI/` | UI Prefab |
| Marker icon sprites (x9) | `Assets/Art/UI/ScarMap/Icons/` | Sprite |
| District silhouettes (4-6) | `Assets/Art/UI/ScarMap/Silhouettes/` | Sprite |
| Background textures | `Assets/Art/UI/ScarMap/` | Texture2D |

---

## 1. Create the ScarMapRenderConfig Singleton
**Create:** `Assets > Create > Hollowcore/ScarMap/Render Config`
**Recommended location:** `Assets/Data/ScarMap/ScarMapRenderConfig.asset`

### 1.1 Front Phase Colors
| Field | Description | Default (Hex) | Range |
|-------|-------------|---------------|-------|
| FrontColorGreen | Phase 0 -- safe | #2ECC71 | Any color |
| FrontColorYellow | Phase 1 -- cautious | #F1C40F | Any color |
| FrontColorOrange | Phase 2 -- dangerous | #E67E22 | Any color |
| FrontColorRed | Phase 3 -- critical | #E74C3C | Any color |
| FrontColorCritical | Phase 4 -- lethal | #8E44AD | Any color |

**Tuning tip:** These colors wash over district silhouettes. Use high saturation but moderate brightness (0.6-0.8) so marker icons remain readable on top.

### 1.2 Marker Animation Speeds
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| EchoSpiralPulseSpeed | Pulse rate for active echo icons | 2.0 | 0.5-6.0 |
| StarTwinkleSpeed | Twinkle rate for star event icons | 3.0 | 0.5-8.0 |
| BleedTendrilFlowSpeed | Flow animation speed on bleed lines | 1.5 | 0.5-4.0 |

**Tuning tip:** Higher speeds draw more attention. Keep EchoSpiral at 2.0 and Star at 3.0 for distinct visual rhythm. Set all to 0 during screenshot capture via `ScarMapViewState.AnimationsEnabled = false`.

### 1.3 Layout Parameters
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| DistrictSpacing | Minimum separation between district nodes | 0.15 | 0.05-0.5 |
| GateLineWidth | Thickness of gate connection lines (pixels) | 2.0 | 0.5-5.0 |
| MarkerIconScale | Base icon size multiplier | 1.0 | 0.1-3.0 |
| DetailZoomScale | Magnification when zoomed into a district | 2.0 | 1.0-5.0 |

### 1.4 Visual Identity
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| CircuitLineOpacity | Background circuitry pattern intensity | 0.3 | 0.0-1.0 |
| HolographicFlicker | Flicker rate for hologram feel | 0.5 | 0.0-2.0 (0 = disabled) |

---

## 2. Create the UI Overlay Prefab

### 2.1 Canvas Setup
**Create:** `Assets/Prefabs/UI/ScarMapOverlay.prefab`

| Setting | Value | Notes |
|---------|-------|-------|
| Canvas Render Mode | Screen Space - Overlay | Full-screen map |
| Canvas Scaler UI Scale Mode | Scale With Screen Size | Responsive layout |
| Reference Resolution | 1920 x 1080 | Standard reference |
| Sort Order | 100 | Above gameplay UI, below tooltip |

### 2.2 Hierarchy
```
ScarMapOverlay (Canvas)
  Background (Image)           -- circuitry pattern, alpha = CircuitLineOpacity
  DistrictContainer (RectTransform) -- parent for district silhouette instances
  GateLineContainer (RectTransform) -- parent for gate connection lines
  MarkerContainer (RectTransform)   -- parent for marker icon instances
  BleedTendrilContainer (RectTransform) -- animated bleed lines
  TooltipPanel (RectTransform)      -- hover tooltip (initially hidden)
  ZoomControls (RectTransform)      -- zoom in/out buttons, back button
  HUDOverlay (RectTransform)        -- marker counts: "Skulls: 3 | Echoes: 2"
```

### 2.3 Background Layers
| Layer | Texture | Notes |
|-------|---------|-------|
| Circuitry pattern | `Assets/Art/UI/ScarMap/circuit_pattern.png` | Tiled, alpha controlled by CircuitLineOpacity |
| Grid lines | `Assets/Art/UI/ScarMap/grid_overlay.png` | Subtle, 10% opacity |
| Holographic flicker | Shader effect or animated alpha | Rate from HolographicFlicker |

---

## 3. Create District Silhouette Sprites

**Recommended location:** `Assets/Art/UI/ScarMap/Silhouettes/`

| Variant Index | Suggested Shape | Used By |
|---------------|-----------------|---------|
| 0 | Tall spire / tower | Necrospire, Chrome Cathedral |
| 1 | Wide industrial | The Burn, The Auction |
| 2 | Vertical scaffold | The Lattice, Skyfall Ruins |
| 3 | Organic blob | Old Growth, Quarantine |
| 4 | Grid / matrix | Glitch Quarter, Synapse Row |
| 5 | Water / flowing | Wetmarket, The Shoals |

**Sprite Settings:**
| Setting | Value |
|---------|-------|
| Texture Type | Sprite (2D and UI) |
| Pixels Per Unit | 100 |
| Max Size | 256x256 |
| Compression | None (for clean edges) |

**Tuning tip:** District silhouettes should be recognizable at both overview (small) and detail (large) zoom levels. Keep shapes simple with bold outlines.

---

## 4. Create Marker Icon Atlas

**Recommended location:** `Assets/Art/UI/ScarMap/Icons/`

Create 9 individual sprites (or a sprite atlas):

| Index | MarkerType | Icon Description | Size |
|-------|------------|------------------|------|
| 0 | Skull | Human skull silhouette | 64x64 |
| 1 | EchoSpiral | Spiral/vortex pattern | 64x64 |
| 2 | FrontGradient | N/A (rendered as color wash, not icon) | -- |
| 3 | BleedTendril | N/A (rendered as animated line) | -- |
| 4 | Star | Six-pointed star | 64x64 |
| 5 | RevivalNode | Upward arrow / resurrection symbol | 64x64 |
| 6 | RivalMarker | Crossed swords / team badge | 64x64 |
| 7 | Completed | Checkmark in circle | 64x64 |
| 8 | Death | Large skull with cross-bones | 64x64 |

---

## 5. Configure ScarMapLayoutSystem (Force-Directed)

The layout system runs once per expedition to position district nodes.

### 5.1 Algorithm Parameters
| Parameter | Description | Default | Range |
|-----------|-------------|---------|-------|
| RepulsionStrength | Force pushing districts apart | 0.01 | 0.001-0.1 |
| AttractionStrength | Force pulling connected districts together | 0.005 | 0.001-0.05 |
| Iterations | Layout solver iterations | 100 | 50-500 |
| DampingFactor | Velocity damping per iteration | 0.95 | 0.8-0.99 |

**Tuning tip:** More iterations = better layout but slower startup. 100 iterations is sufficient for 15 districts. If districts overlap, increase RepulsionStrength.

### 5.2 Layout Rules
- Start district centered in normalized space
- Extraction/boss districts pushed to edges
- Visited districts at full opacity, unvisited at 30% opacity
- SilhouetteVariant assigned from `DistrictDefinitionSO.ArtTheme` mapping

---

## 6. Wire the Keybind

| Action | Default Key | Context |
|--------|-------------|---------|
| Toggle Scar Map | M | Standalone mode (ScarMapContext.Standalone) |
| Zoom In / Click District | Left Click | Switch to DistrictDetail zoom |
| Zoom Out / Back | Right Click or Escape | Return to ExpeditionOverview |
| Pan | Middle Click + Drag | Move viewport |

---

## Scene & Subscene Checklist

- [ ] Subscene contains singleton entity with `ScarMapRenderConfigAuthoring`
- [ ] `ScarMapOverlay.prefab` exists at `Assets/Prefabs/UI/`
- [ ] 9 marker icon sprites created and assigned in render config blob
- [ ] 4-6 district silhouette sprites created
- [ ] Background textures (circuitry, grid) created
- [ ] Keybind for M key registered in Input System
- [ ] Assembly `Hollowcore.ScarMap` references `Hollowcore.UI`

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| ScarMapRenderConfig singleton missing | Renderer early-outs every frame, nothing displayed | Add `ScarMapRenderConfigAuthoring` to subscene |
| Marker icon sprites not assigned in blob | All markers render as white squares | Populate all 9 MarkerRenderEntry.IconSpriteIndex values |
| Canvas Sort Order too low | Scar Map renders behind gameplay HUD | Set ScarMapOverlay canvas sort order >= 100 |
| Force-directed layout not converging | Districts overlap or bunch in corner | Increase Iterations (try 200), increase RepulsionStrength |
| AnimationsEnabled left false | EchoSpiral and Star markers do not pulse/twinkle | Set `ScarMapViewState.AnimationsEnabled = true` (only false during screenshot) |
| Detail zoom not updating zone positions | Zone-level markers appear at wrong positions | Ensure zone positions derived from seed match the active topology variant |

---

## Verification

- [ ] Scar Map overlay toggles on/off with M key
- [ ] District silhouettes positioned without overlap via force-directed layout
- [ ] Gate connection lines drawn between connected districts (solid = open, dashed = locked)
- [ ] Front gradient colors match phase values 0-4
- [ ] Visited districts at full opacity, unvisited districts dimmed
- [ ] Skull markers display with red tint
- [ ] EchoSpiral markers pulse when active, dim when inactive
- [ ] Star markers twinkle at configured speed
- [ ] BleedTendril lines animate flow direction
- [ ] Zoom to district detail mode shows zone-level markers
- [ ] Hover tooltip shows district summary
- [ ] Background circuitry pattern renders at configured opacity
- [ ] Icon stacking handles 8+ markers at same district without overlap
- [ ] Renderer completes in under 2ms for 100 markers
