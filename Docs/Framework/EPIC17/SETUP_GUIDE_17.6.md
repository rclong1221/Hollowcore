# EPIC 17.6: Minimap & World Map System — Setup Guide

## Overview

The Minimap & World Map System provides a real-time orthographic minimap, fog-of-war exploration tracking, a compass bar, POI auto-discovery, and a fullscreen world map. It is fully client-side — no ECS ghosts, no server cost, zero bytes on the player entity archetype.

**Key design**: An orthographic camera renders the minimap to a RenderTexture. Fog-of-war is a separate R8 texture stamped with circles as the player moves. Icons are projected from world-space ECS entities to 2D UI positions. All map data is saved via the persistence system (TypeId=13).

---

## Quick Start

### 1. Create Default Assets

1. **Menu** → **DIG → Map Workstation → Create Default Assets**
2. This creates three ScriptableObjects:
   - `Assets/Resources/MinimapConfig.asset`
   - `Assets/Resources/MapIconTheme.asset`
   - `Assets/Resources/POIRegistry.asset`
3. All three **must** remain in `Assets/Resources/` (loaded via `Resources.Load`)

### 2. Configure Minimap

Open `Assets/Resources/MinimapConfig.asset` in the Inspector:

| Field | Default | Description |
|-------|---------|-------------|
| DefaultZoom | 40 | Orthographic camera size (world units visible in each direction) |
| MinZoom | 20 | Closest zoom level (most detail) |
| MaxZoom | 80 | Farthest zoom level (widest view) |
| ZoomStep | 5 | Zoom increment per mouse scroll tick |
| RotateWithPlayer | true | Minimap rotates to match player facing direction |
| RenderTextureSize | 512 | Minimap camera render resolution (256/512/1024) |
| IconScale | 1.0 | Base scale multiplier for all map icons |
| UpdateFrameSpread | 4 | Frame-spread K — only 1/K of icon entities update per frame |
| MaxIconRange | 150 | Icons beyond this distance (meters) are hidden |
| CompassRange | 500 | Max distance for compass POI display |

**Fog-of-War settings** (same asset):

| Field | Default | Description |
|-------|---------|-------------|
| FogTextureWidth | 1024 | Fog reveal texture width (pixels) |
| FogTextureHeight | 1024 | Fog reveal texture height (pixels) |
| RevealRadius | 15 | World-space radius revealed per step |
| RevealMoveThreshold | 2 | Min player movement to trigger a new reveal circle |
| WorldBoundsMin | (-500, -500) | World XZ minimum for fog UV mapping |
| WorldBoundsMax | (500, 500) | World XZ maximum for fog UV mapping |
| FogUnexploredColor | black (0.9a) | Color overlay for unexplored areas |
| FogExploredColor | clear | Color for fully explored areas |

**Appearance**:

| Field | Default | Description |
|-------|---------|-------------|
| MinimapMaskShape | Circle | Circle or Square minimap mask |
| MinimapBorderColor | white | Border tint color |
| PlayerIconColor | green | Local player arrow color on minimap |

### 3. Configure Icon Theme

Open `Assets/Resources/MapIconTheme.asset`:

1. Click **+** to add entries, one per `MapIconType`
2. Each entry configures:

| Field | Description |
|-------|-------------|
| IconType | Which map icon type this entry applies to |
| Icon | Sprite displayed on minimap and world map |
| CompassIcon | Sprite variant for compass bar (can be smaller) |
| DefaultColor | Default tint (e.g., Enemy=red, NPC=yellow) |
| ScaleMultiplier | Per-type scale (Boss=1.5, Loot=0.8) |
| ShowOnCompass | Whether this type appears on the compass bar |
| ShowOnWorldMap | Whether this type appears on the fullscreen map |
| ShowDistance | Show distance text below compass icons |
| SortOrder | Z-order for overlap (higher = on top) |

**Recommended default colors**:
- Player: green (#00FF00)
- PartyMember: blue (#4488FF)
- Enemy: red (#FF3333)
- NPC: yellow (#FFDD00)
- QuestObjective: gold (#FFD700)
- QuestGiver: gold (#FFD700)
- Vendor: yellow (#FFDD00)
- CraftStation: orange (#FF8800)
- Loot: white (#FFFFFF)
- POI: silver (#CCCCCC)
- FastTravel: cyan (#00DDFF)
- Danger: dark red (#880000)
- Boss: red (#FF3333), ScaleMultiplier=1.5

### 4. Configure POI Registry

Open `Assets/Resources/POIRegistry.asset`:

| Field | Default | Description |
|-------|---------|-------------|
| AutoDiscoverRadius | 30 | Distance (meters) at which POIs are auto-discovered |
| DiscoverXPReward | 25 | XP awarded per discovery (uses XPGrantAPI from EPIC 16.14) |

Add entries to the **POIs** array for each permanent landmark:

| Field | Description |
|-------|-------------|
| POIId | Stable unique integer (never change after ship) |
| Label | Display name shown on compass and world map |
| Type | Town, Dungeon, BossArena, FastTravel, Landmark, Camp, Cave, Ruins, Shrine, Vendor |
| WorldPosition | World-space position for compass/world map placement |
| IsFastTravel | Enables fast travel from the world map |
| RequiresDiscovery | Must be visited before appearing on world map |
| Description | Tooltip text on world map hover |
| OverrideIcon | Custom sprite (null = use theme default) |

---

## Adding Map Icons to Entities

### Enemy/NPC/Boss Prefabs

1. Select the prefab root GameObject
2. **Add Component** → **DIG → Map → Map Icon**
3. Configure:
   - **IconType**: Enemy, NPC, Boss, Vendor, etc.
   - **Priority**: 100 for normal, 200+ for important
   - **VisibleOnMinimap**: true (default)
   - **VisibleOnWorldMap**: true (default)
   - **UseCustomColor**: false (uses theme default)
4. **No impact on player archetype** — MapIcon is placed on the NPC/enemy entity, not the player

### Points of Interest (Landmarks)

1. Create an empty GameObject in your subscene at the landmark position
2. **Add Component** → **DIG → Map → Point of Interest**
3. Configure:
   - **POIId**: Must match a POIRegistrySO entry
   - **Type**: Town, Dungeon, FastTravel, etc.
   - **Label**: Display name (max 29 characters)
   - **DiscoveredByDefault**: true for towns, false for hidden locations
4. The baker automatically adds a MapIcon component — no need to add MapIconAuthoring separately

---

## Setting Up the UI

### Minimap Widget

1. Create a UI Canvas (or use your existing HUD canvas)
2. Add a **RawImage** for the minimap background
3. Add a child **RawImage** for the fog overlay
4. Add a child **RectTransform** as the icon container
5. Add a child **Image** as the player arrow
6. Attach the **MinimapView** MonoBehaviour to the root
7. Wire up the serialized references:
   - `_minimapImage` → minimap RawImage
   - `_fogOverlayImage` → fog overlay RawImage
   - `_iconContainer` → icon container RectTransform
   - `_playerArrow` → player arrow RectTransform
   - `_fogPercentText` → (optional) Text for exploration %
   - `_iconPrefab` → (optional) custom icon prefab, or leave null for default squares

The `MinimapBootstrapSystem` will automatically call `SetRenderTexture()` on the registered provider.

### Compass Bar

1. Add a horizontal bar at the top of the HUD
2. Attach the **CompassView** MonoBehaviour
3. Wire up:
   - `_compassBar` → the bar RectTransform
   - `_northLabel`, `_southLabel`, `_eastLabel`, `_westLabel` → cardinal Text elements
   - `_iconContainer` → container for POI icons
   - `_compassIconPrefab` → (optional) custom compass icon prefab
4. Configure `_compassWidthDegrees` (default 180 = half of view)

### World Map (Fullscreen)

1. Create a full-screen panel (initially inactive)
2. Attach the **WorldMapView** MonoBehaviour
3. Wire up:
   - `_mapImage` → RawImage for map texture
   - `_fogOverlayImage` → RawImage for fog overlay
   - `_mapContent` → scrollable content RectTransform
   - `_playerMarker` → player arrow RectTransform
   - `_labelContainer` → container for POI labels
4. Set `_toggleKey` (default: M key)
5. Set world bounds to match your MinimapConfigSO bounds

### Discovery Notifications

1. Create a toast UI element (CanvasGroup + Text elements)
2. Attach the **MapNotificationView** MonoBehaviour
3. Wire up:
   - `_toastGroup` → CanvasGroup for fade animation
   - `_titleText` → Text for "Discovered: Whispering Ruins"
   - `_subtitleText` → Text for POI type subtitle
   - `_iconImage` → (optional) icon for the discovery type
4. Configure fade timings (default: 0.3s in, 2.5s hold, 0.5s out)

---

## Fog-of-War Shader Setup

### FogReveal Material

1. Create a new Material: **Right-click** → **Create → Material**
2. Set shader to **DIG/Map/FogReveal**
3. Leave all properties at defaults — the system sets `_Center` and `_Radius` at runtime
4. Place the material where `MinimapBootstrapSystem` can find it, or assign it programmatically

### MinimapFogOverlay Material

1. Create a new Material with shader **DIG/Map/MinimapFogOverlay**
2. Configure:
   - **Unexplored Color**: black with ~0.85 alpha (dark overlay)
   - **Explored Color**: fully transparent
   - **Edge Softness**: 0.05 (smooth transition)
3. Assign to the fog overlay RawImage's material

---

## Editor Workstation

Open via **DIG → Map Workstation**. Five tabs:

| Tab | Purpose | Requires Play Mode |
|-----|---------|-------------------|
| **Config Editor** | Edit MinimapConfigSO fields, zoom preview, VRAM budget display | No |
| **Icon Theme** | Edit MapIconThemeSO entries, preview all 13 icon types | No |
| **POI Manager** | List/edit POIRegistrySO entries, duplicate ID validator, scene gizmos | No (gizmos need scene) |
| **Fog Preview** | Live fog texture, reveal stats, "Reveal All" / "Reset Fog" buttons | Yes |
| **Live Inspector** | Icon/compass buffer counts, camera position, discovered POIs, zoom override | Yes |

---

## Save Integration

Map data is automatically saved/loaded via the persistence system:

- **TypeId**: 13 (Map)
- **What's saved**: Fog-of-war texture (RLE compressed), discovered POI list, total reveal count
- **Dirty check**: Only saves when `TotalRevealed` changes since last save
- **Typical save size**: 20-100 KB (1024x1024 fog texture compresses well with RLE)
- **No setup needed** — `MapSaveModule` is registered automatically by `PersistenceBootstrapSystem`

---

## Performance Tuning

### Frame Spread (UpdateFrameSpread)

Controls how icon processing is distributed across frames:

| Entity Count | K=4 (default) | Per-Frame Icons | Budget |
|-------------|---------------|-----------------|--------|
| 100 | 25/frame | ~0.15ms | Well under |
| 500 | 125/frame | ~0.4ms | At budget |
| 1000 | 250/frame | ~0.8ms | Increase K to 8 |

Change `UpdateFrameSpread` in MinimapConfigSO — no code changes needed.

### VRAM Budget

| Resource | Size | Format |
|----------|------|--------|
| Minimap RT (512) | 1.0 MB | ARGB32 |
| Fog RT (1024) | 1.0 MB | R8 |
| **Total** | **~2.0 MB** | |

Reduce `RenderTextureSize` to 256 or `FogTextureWidth/Height` to 512 for low-end targets.

### Fog Reveal

The GPU path (shader blit) costs ~0.02ms. The CPU fallback is significantly slower and only used if the FogReveal material is missing. Always ensure the material is assigned.

---

## System Execution Order

```
InitializationSystemGroup (Client|Local):
  MinimapBootstrapSystem              — runs once, creates singletons + camera

SimulationSystemGroup (Client|Local):
  MapIconUpdateSystem                 — frame-spread entity → icon buffer

PresentationSystemGroup (Client|Local):
  MinimapCameraSystem                 — positions camera on player, handles zoom
  FogOfWarSystem (after Camera)       — GPU blit fog reveal, POI auto-discovery
  CompassSystem (after Fog)           — computes POI angles/distances
  MapUIBridgeSystem (after Compass)   — dispatches all data to UI providers
```

---

## Architecture

```
 ┌─────────────────────────────────────────────────────────┐
 │                  ECS SYSTEMS (Client)                    │
 │                                                          │
 │  MinimapBootstrapSystem (once) ──→ Creates singletons   │
 │  MapIconUpdateSystem ──→ IconBuffer (NativeList)         │
 │  MinimapCameraSystem ──→ Ortho camera position           │
 │  FogOfWarSystem ──→ GPU blit reveal + POI discovery      │
 │  CompassSystem ──→ CompassBuffer (NativeList)            │
 │  MapUIBridgeSystem ──→ MapUIRegistry dispatch            │
 │                                                          │
 └───────────────────────────┬─────────────────────────────┘
                             │ Static registry calls
                             ▼
 ┌─────────────────────────────────────────────────────────┐
 │              MapUIRegistry (static)                      │
 │  IMinimapProvider ←→ MinimapView                        │
 │  IWorldMapProvider ←→ WorldMapView                      │
 │  ICompassProvider ←→ CompassView                        │
 │  IMapNotificationProvider ←→ MapNotificationView        │
 └─────────────────────────────────────────────────────────┘
```

---

## Checklist

- [ ] `Resources/MinimapConfig.asset` exists with correct world bounds
- [ ] `Resources/MapIconTheme.asset` exists with entries for all 13 icon types
- [ ] `Resources/POIRegistry.asset` exists with all landmark POIs
- [ ] Enemy prefabs have **MapIconAuthoring** (DIG/Map/Map Icon)
- [ ] NPC prefabs have **MapIconAuthoring**
- [ ] Boss prefabs have **MapIconAuthoring** with IconType=Boss
- [ ] Landmark GameObjects have **PointOfInterestAuthoring** in subscenes
- [ ] POIId values match between PointOfInterestAuthoring and POIRegistrySO entries
- [ ] HUD has **MinimapView** MonoBehaviour with references wired
- [ ] HUD has **CompassView** MonoBehaviour with references wired
- [ ] HUD has **WorldMapView** MonoBehaviour with toggle key configured
- [ ] HUD has **MapNotificationView** MonoBehaviour for toast popups
- [ ] FogReveal material created with shader DIG/Map/FogReveal
- [ ] MinimapFogOverlay material created with shader DIG/Map/MinimapFogOverlay
- [ ] WorldBoundsMin/Max covers entire playable area
- [ ] No duplicate POIId values (check in Map Workstation → POI Manager)
- [ ] Play mode: minimap renders, fog reveals, compass shows discovered POIs

---

## File Manifest

### Components (4 files)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Map/Components/MapIconComponents.cs` | MapIcon IComponentData + MapIconType enum |
| `Assets/Scripts/Map/Components/PointOfInterestComponents.cs` | PointOfInterest IComponentData + POIType enum |
| `Assets/Scripts/Map/Components/MapSingletons.cs` | MinimapConfig, MapRevealState, MapManagedState singletons |
| `Assets/Scripts/Map/Components/MapDataStructs.cs` | MapIconEntry, CompassEntry, DiscoveredPOIRecord structs |

### Definitions (3 files)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Map/Definitions/MinimapConfigSO.cs` | Minimap + fog configuration ScriptableObject |
| `Assets/Scripts/Map/Definitions/MapIconThemeSO.cs` | Per-icon-type visual theme ScriptableObject |
| `Assets/Scripts/Map/Definitions/POIRegistrySO.cs` | POI definitions + auto-discover settings |

### Systems (6 files)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Map/Systems/MinimapBootstrapSystem.cs` | One-time init: singletons, camera, textures |
| `Assets/Scripts/Map/Systems/MapIconUpdateSystem.cs` | Frame-spread entity→icon buffer projection |
| `Assets/Scripts/Map/Systems/MinimapCameraSystem.cs` | Camera positioning + zoom input |
| `Assets/Scripts/Map/Systems/FogOfWarSystem.cs` | GPU fog reveal + POI auto-discovery |
| `Assets/Scripts/Map/Systems/CompassSystem.cs` | POI angle/distance computation |
| `Assets/Scripts/Map/Systems/MapUIBridgeSystem.cs` | Dispatch to UI registry providers |

### Authoring (2 files)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Map/Authoring/MapIconAuthoring.cs` | Baker for MapIcon on entity prefabs |
| `Assets/Scripts/Map/Authoring/PointOfInterestAuthoring.cs` | Baker for PointOfInterest + auto MapIcon |

### UI Bridge (2 files)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Map/Bridges/MapUIRegistry.cs` | Static registry for UI providers |
| `Assets/Scripts/Map/Bridges/IMapUIProviders.cs` | IMinimapProvider, IWorldMapProvider, ICompassProvider, IMapNotificationProvider |

### UI Views (4 files)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Map/UI/MinimapView.cs` | Minimap RawImage + icon pooling |
| `Assets/Scripts/Map/UI/WorldMapView.cs` | Fullscreen map with zoom/pan |
| `Assets/Scripts/Map/UI/CompassView.cs` | Horizontal compass bar |
| `Assets/Scripts/Map/UI/MapNotificationView.cs` | Discovery toast popup |

### Shaders (2 files)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Map/Shaders/FogReveal.shader` | Circle stamp for fog reveal (Max blend) |
| `Assets/Scripts/Map/Shaders/MinimapFogOverlay.shader` | Fog overlay composite |

### Save Integration (1 new + 2 modified)
| File | Purpose |
|------|---------|
| `Assets/Scripts/Persistence/Modules/MapSaveModule.cs` | ISaveModule TypeId=13 — fog RLE + discovered POIs |
| `Assets/Scripts/Persistence/Core/SaveModuleTypeIds.cs` | Added `Map = 13` constant |
| `Assets/Scripts/Persistence/Systems/PersistenceBootstrapSystem.cs` | Registers MapSaveModule |

### Editor (7 files)
| File | Purpose |
|------|---------|
| `Assets/Editor/MapWorkstation/IMapWorkstationModule.cs` | Module interface |
| `Assets/Editor/MapWorkstation/MapWorkstationWindow.cs` | EditorWindow (5 tabs) |
| `Assets/Editor/MapWorkstation/Modules/ConfigEditorModule.cs` | MinimapConfigSO editor |
| `Assets/Editor/MapWorkstation/Modules/IconThemeModule.cs` | MapIconThemeSO editor |
| `Assets/Editor/MapWorkstation/Modules/POIManagerModule.cs` | POI list + validator |
| `Assets/Editor/MapWorkstation/Modules/FogPreviewModule.cs` | Live fog texture preview |
| `Assets/Editor/MapWorkstation/Modules/LiveInspectorModule.cs` | Runtime data inspector |

**Total: 27 new files + 2 modified files**
