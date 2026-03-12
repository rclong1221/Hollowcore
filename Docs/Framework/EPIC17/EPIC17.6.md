# EPIC 17.6: Minimap & World Map System

**Status:** PLANNED
**Priority:** Medium (Quality of Life / Navigation)
**Dependencies:**
- `LocalToWorld` IComponentData (existing -- `Unity.Transforms`, world-space position for all entities)
- `GhostOwnerIsLocal` IComponentData (existing -- `Unity.NetCode`, identifies local player entity for camera following)
- `PlayerTag` IComponentData (existing -- `Player.Components`, marks player entities)
- `CharacterAttributes` IComponentData (existing -- `DIG.Combat.Components`, Ghost:All, Level for icon display)
- `HasHitboxes` IComponentData (existing -- `DIG.Combat.Components`, marks real enemy entities, avoids phantom duplicates)
- `DeathState` IComponentData (existing -- `Player.Components`, hides dead entity icons)
- `Health` IComponentData (existing -- `Player.Components`, Ghost:All, for HP-based icon coloring)
- `QuestProgress` / `ObjectiveProgress` IBufferElementData (existing -- `DIG.Quest`, EPIC 16.12, active quest objective positions)
- `DialogueSpeakerData` IComponentData (existing -- `DIG.Dialogue.Components`, NPC entities for Talk icon markers)
- `CorpseState` IEnableableComponent (existing -- `DIG.Combat.Components`, EPIC 16.3, hide corpse icons)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, EPIC 16.6, for vendor icon markers)
- `ISaveModule` interface (existing -- `DIG.Persistence.Core`, EPIC 16.15, for fog-of-war persistence)
- `SaveModuleTypeIds` constants (existing -- `DIG.Persistence.Core`, TypeId registry)
- `SaveContext` / `LoadContext` structs (existing -- `DIG.Persistence.Core`)
- `DetectionSystem` frame-spread pattern (existing -- `DIG.Aggro.Systems`, `entityIndex % K == frameCount % K`)
- `CombatUIRegistry` / `CombatUIBridgeSystem` (existing -- static registry + provider interface pattern)
- `ProgressionBootstrapSystem` (existing -- bootstrap singleton pattern reference)

**Feature:** A client-only minimap and world map system providing real-time entity tracking via orthographic render-texture camera, fog-of-war exploration persistence, point-of-interest discovery, compass navigation, and quest waypoint integration. All map systems run exclusively on ClientSimulation | LocalSimulation -- zero server cost. No components added to the player entity (0 bytes archetype impact). Fog-of-war state saved via ISaveModule (TypeId=13).

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `LocalToWorld` (world position) | Unity.Transforms | Built-in | Every entity with `LocalTransform` has world-space matrix |
| `GhostOwnerIsLocal` (local player) | Unity.NetCode | Built-in | Used by ProgressionUIBridgeSystem, DialogueUIBridgeSystem for local player identification |
| `HasHitboxes` (enemy marker) | `DIG.Combat.Components` | Fully implemented | Filters phantom ghost duplicates (MEMORY.md) |
| `DeathState` (dead entity check) | `Player.Components` | Fully implemented | `.IsDead` flag for suppressing icons |
| `QuestProgress` + `ObjectiveProgress` | `DIG.Quest` | Fully implemented (EPIC 16.12) | ObjectiveProgress has WorldPosition float3 for waypoint targets |
| `DialogueSpeakerData` (NPC marker) | `DIG.Dialogue.Components` | Fully implemented (EPIC 16.16) | NPC speaker entities identifiable for Talk icon |
| `ISaveModule` pattern | `DIG.Persistence.Core` | Fully implemented (EPIC 16.15) | TypeId=11 (Talents) is last registered; 12 reserved, 13 available |
| Frame-spread pattern | `DIG.Aggro.Systems.DetectionSystem` | Fully implemented | `entityIndex % SensorSpreadFrames == frameCount % SensorSpreadFrames` |
| Static registry + provider interface | `CombatUIRegistry`, `ProgressionUIRegistry`, `SaveUIRegistry` | Established pattern | MonoBehaviours register on enable, unregister on disable |

### What's Missing

- **No minimap camera** -- no orthographic render-texture camera tracking the local player
- **No map icon system** -- no ECS components to classify entities as map markers
- **No world map UI** -- no fullscreen map panel with zoom/pan
- **No fog-of-war** -- no exploration tracking or reveal texture
- **No compass** -- no screen-edge directional indicators for POIs
- **No quest waypoint integration** -- ObjectiveProgress has WorldPosition but nothing renders it on a minimap
- **No POI discovery** -- no persistent landmark/fast-travel markers
- **No map save module** -- fog-of-war and discovered POIs not persisted

---

## Problem

DIG has a fully networked world with enemies, NPCs, quest objectives, crafting stations, and loot -- but players navigate entirely by line-of-sight. There is no minimap, no compass, no world map, and no fog-of-war exploration tracking. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `LocalToWorld` on every entity | No system projects world positions onto a 2D map overlay |
| `HasHitboxes` filters real enemies | No system renders red dots for nearby enemies |
| `QuestProgress.ObjectiveProgress.WorldPosition` | No waypoint marker on minimap or compass |
| `DialogueSpeakerData` on NPC entities | No yellow NPC icon on minimap |
| `ISaveModule` pattern (TypeId 1-11 allocated) | No module persists exploration state |
| `DeathState.IsDead` on dead entities | No system hides icons for dead entities |
| Frame-spread pattern in DetectionSystem | No system applies frame-spread to map icon updates |

**The gap:** Players cannot see nearby enemies, quest objectives, or party members without direct line-of-sight. New players get lost in the world with no directional guidance. Exploration has no tangible progress indicator. Returning to a previously-visited area provides no visual feedback that the area was explored.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  MinimapConfigSO         POIRegistrySO          MapIconThemeSO
  (zoom levels, sizes,    (POI definitions,       (icon sprites,
   update spread, mask)    labels, categories)     colors, priorities)
           |                    |                    |
           └────── MinimapBootstrapSystem ──────────┘
                   (loads from Resources/, creates singletons,
                    initializes render texture + fog texture,
                    follows ProgressionBootstrapSystem pattern)
                              |
                    ECS DATA LAYER (Client-only components)
  MapIcon (8 bytes)           PointOfInterest (20 bytes)
  (on enemy/NPC/POI entities)  (on landmark entities)
           |                           |
  MinimapConfig singleton      MapRevealState singleton
  (zoom, rotation, spread)     (fog texture dimensions, reveal stats)
                              |
                    SYSTEM PIPELINE (Client|Local ONLY)
                              |
  MapIconUpdateSystem (SimulationSystemGroup)
  - frame-spread icon position updates
  - reads LocalToWorld, DeathState, CorpseState
  - writes to MapIconBuffer (NativeList)
           |
  MinimapCameraSystem (PresentationSystemGroup)
  - positions orthographic camera on local player
  - configures zoom, rotation, culling
           |
  FogOfWarSystem (PresentationSystemGroup)
  - reveals fog texture as player moves
  - GPU compute shader OR CPU fallback
           |
  CompassSystem (PresentationSystemGroup)
  - calculates POI angles/distances from player forward
  - reads PointOfInterest + quest objective positions
           |
  MapUIBridgeSystem (PresentationSystemGroup)
  - pushes icon data + fog state to UI registry
  - managed SystemBase → MapUIRegistry → IMapUIProvider
```

### Data Flow (Entity Spawn → Map Icon → UI)

```
Frame N (Client):
  1. Entity spawns with MapIconAuthoring → Baker adds MapIcon component
     (enemies get MapIcon.IconType=Enemy, NPCs get IconType=NPC, etc.)

Frame N+K (Client, frame-spread):
  2. MapIconUpdateSystem: entityIndex % UpdateFrameSpread == frameCount % UpdateFrameSpread
     - Read LocalToWorld.Position → project to 2D map coordinates
     - Check DeathState.IsDead → skip dead entities
     - Check CorpseState enabled → skip corpses
     - Check distance to player → skip beyond MaxIconRange
     - Write to MapIconBuffer: { WorldPos2D, IconType, Priority, CustomColor }

Frame N+K (Client, same frame):
  3. MinimapCameraSystem: Position orthographic camera at local player XZ
     - Camera.orthographicSize = MinimapConfig.Zoom
     - Camera.transform.rotation = player rotation (or north-up based on config)
     - RenderTexture assigned to minimap UI RawImage

  4. FogOfWarSystem: Check if player moved since last reveal
     - Convert player world position → fog texture UV
     - Draw circle (RevealRadius) on fog RenderTexture
     - Increment MapRevealState.TotalRevealed

  5. CompassSystem: For each PointOfInterest + quest objective:
     - angle = atan2(poi.z - player.z, poi.x - player.x) - playerYaw
     - distance = length(poi.xz - player.xz)
     - Write to CompassEntryBuffer: { Angle, Distance, IconType, Label }

  6. MapUIBridgeSystem: Push all data to MapUIRegistry
     - Minimap icon overlay positions
     - Compass entries
     - Fog texture reference
     - Player position + rotation for world map marker
```

### Key Design: Client-Only Architecture

All map systems carry `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`. The server never processes map icons, fog-of-war, or compass data. This means:
- **Zero server CPU cost** for the entire minimap feature
- **No new ghost components** -- `MapIcon` and `PointOfInterest` are baked by authoring components, not ghost-replicated
- **No 16KB archetype impact** on the player entity
- Fog-of-war is saved via `MapSaveModule` (ISaveModule TypeId=13) which serializes the fog RenderTexture to bytes on the client side, transmitted to server for persistence via the existing save pipeline

### Key Design: MapIcon on Non-Player Entities Only

`MapIcon` is placed on enemy prefabs, NPC prefabs, POI prefabs, crafting stations, loot containers -- never on the player entity. The local player's position is read directly from `GhostOwnerIsLocal` + `LocalToWorld`. Party member positions come from querying other `PlayerTag` entities (or future `PartyLink` from EPIC 17.2). This keeps player archetype impact at exactly 0 bytes.

---

## ECS Components

### MapIcon (On Non-Player Entities)

**File:** `Assets/Scripts/Map/Components/MapIconComponents.cs`

```csharp
// 8 bytes -- placed on enemies, NPCs, POIs, crafting stations, loot, etc.
public struct MapIcon : IComponentData
{
    public MapIconType IconType;         // byte (enum)
    public byte Priority;                // 0=low, 255=highest (for icon overlap)
    public bool VisibleOnMinimap;        // 1 byte
    public bool VisibleOnWorldMap;       // 1 byte
    public uint CustomColorPacked;       // RGBA packed into 4 bytes (0 = use theme default)
}

public enum MapIconType : byte
{
    Player       = 0,
    PartyMember  = 1,
    Enemy        = 2,
    NPC          = 3,
    QuestObjective = 4,
    QuestGiver   = 5,
    Vendor       = 6,
    CraftStation = 7,
    Loot         = 8,
    POI          = 9,
    FastTravel   = 10,
    Danger       = 11,
    Boss         = 12
}
```

### PointOfInterest (On Landmark Entities)

**File:** `Assets/Scripts/Map/Components/PointOfInterestComponents.cs`

```csharp
// 20 bytes -- placed on town markers, dungeon entrances, boss arenas, fast travel points
public struct PointOfInterest : IComponentData
{
    public POIType Type;                      // byte (enum)
    public FixedString32Bytes Label;          // 15 bytes (FixedString32 = 2 + 29 + 1 alignment)
    public bool DiscoveredByPlayer;           // 1 byte (client-side, set by FogOfWarSystem on reveal)
    // Padding: 3 bytes to align
}

public enum POIType : byte
{
    Town         = 0,
    Dungeon      = 1,
    BossArena    = 2,
    FastTravel   = 3,
    Landmark     = 4,
    Camp         = 5,
    Cave         = 6,
    Ruins        = 7,
    Shrine       = 8,
    Vendor       = 9
}
```

### Singletons (Client-Only)

**File:** `Assets/Scripts/Map/Components/MapSingletons.cs`

```csharp
// Minimap configuration singleton -- created by MinimapBootstrapSystem
public struct MinimapConfig : IComponentData
{
    public float Zoom;                 // Orthographic camera size (world units visible)
    public float MinZoom;              // Minimum zoom level
    public float MaxZoom;              // Maximum zoom level
    public float ZoomStep;             // Zoom increment per scroll
    public bool RotateWithPlayer;      // true = rotate minimap, false = north-up
    public float IconScale;            // Base scale multiplier for map icons
    public int UpdateFrameSpread;      // Frame-spread K for MapIconUpdateSystem
    public int RenderTextureSize;      // Minimap render texture resolution (256/512/1024)
    public float MaxIconRange;         // Max world-space distance for icon visibility
    public float CompassRange;         // Max distance for compass POI display
}

// Fog-of-war state singleton -- created by MinimapBootstrapSystem
public struct MapRevealState : IComponentData
{
    public int FogTextureWidth;        // Fog texture pixel width
    public int FogTextureHeight;       // Fog texture pixel height
    public float RevealRadius;         // World-space radius revealed per frame
    public float WorldMinX;            // World bounds mapping (fog UV 0,0)
    public float WorldMinZ;            // World bounds mapping
    public float WorldMaxX;            // World bounds mapping (fog UV 1,1)
    public float WorldMaxZ;            // World bounds mapping
    public int TotalRevealed;          // Total fog pixels revealed (for stats/achievements)
    public int TotalPixels;            // FogTextureWidth * FogTextureHeight
    public float LastRevealX;          // Last player X position that triggered reveal
    public float LastRevealZ;          // Last player Z position that triggered reveal
    public float RevealMoveThreshold;  // Min movement before new reveal circle (avoids redundant draws)
}

// Managed singleton for render texture references (cannot store in unmanaged IComponentData)
public class MapManagedState : IComponentData
{
    public RenderTexture MinimapRenderTexture;     // Orthographic camera target
    public RenderTexture FogOfWarTexture;          // Fog reveal state (R8 format)
    public RenderTexture FogOfWarTextureStaging;   // Double-buffer for async readback
    public Camera MinimapCamera;                   // Orthographic camera instance
    public Material FogRevealMaterial;             // Shader for circle stamp drawing
    public NativeList<MapIconEntry> IconBuffer;    // Frame-coherent icon list for UI
    public NativeList<CompassEntry> CompassBuffer; // Frame-coherent compass entries
    public bool IsInitialized;
}
```

### Shared Structs (Not ECS Components)

**File:** `Assets/Scripts/Map/Components/MapDataStructs.cs`

```csharp
// Icon entry written by MapIconUpdateSystem, read by MapUIBridgeSystem
public struct MapIconEntry
{
    public float2 WorldPos2D;          // XZ world position
    public MapIconType IconType;       // byte
    public byte Priority;              // for overlap resolution
    public uint ColorPacked;           // RGBA or 0 for theme default
    public Entity SourceEntity;        // for click-to-track in world map
}

// Compass entry written by CompassSystem, read by MapUIBridgeSystem
public struct CompassEntry
{
    public float Angle;                // Radians from player forward direction
    public float Distance;             // World-space distance from player
    public MapIconType IconType;       // byte
    public FixedString32Bytes Label;   // POI label or quest objective name
    public bool IsQuestWaypoint;       // Highlighted differently on compass
}

// Discovered POI record for save/load
public struct DiscoveredPOIRecord
{
    public int POIId;                  // Stable identifier from POIRegistrySO
    public float DiscoverTimestamp;    // Elapsed playtime when discovered
}
```

---

## ScriptableObjects

### MinimapConfigSO

**File:** `Assets/Scripts/Map/Definitions/MinimapConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Map/Minimap Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| DefaultZoom | float | 40 | Starting orthographic camera size |
| MinZoom | float | 20 | Closest zoom (most detail) |
| MaxZoom | float | 80 | Farthest zoom (widest view) |
| ZoomStep | float | 5 | Increment per mouse scroll |
| RotateWithPlayer | bool | true | Minimap rotates with player facing |
| IconScale | float | 1.0 | Base scale for all map icons |
| UpdateFrameSpread | int | 4 | Frame-spread K (update 25% of icons per frame) |
| RenderTextureSize | int | 512 | Minimap RT resolution (power of 2) |
| MaxIconRange | float | 150 | World units, icons beyond this hidden |
| CompassRange | float | 500 | World units, compass POI max distance |
| MinimapMaskShape | MinimapMaskShape enum | Circle | Circle or Square mask overlay |
| MinimapBorderColor | Color | white | UI border tint |
| PlayerIconColor | Color | green | Local player arrow color |
| FogTextureWidth | int | 1024 | Fog-of-war resolution width |
| FogTextureHeight | int | 1024 | Fog-of-war resolution height |
| RevealRadius | float | 15 | World units revealed per step |
| RevealMoveThreshold | float | 2 | Min movement to trigger new reveal |
| WorldBoundsMin | Vector2 | (-500, -500) | World XZ min for fog UV mapping |
| WorldBoundsMax | Vector2 | (500, 500) | World XZ max for fog UV mapping |
| FogUnexploredColor | Color | black (0.9 alpha) | Unexplored area overlay |
| FogExploredColor | Color | clear | Fully explored area (transparent) |

```csharp
public enum MinimapMaskShape : byte
{
    Circle = 0,
    Square = 1
}
```

### MapIconThemeSO

**File:** `Assets/Scripts/Map/Definitions/MapIconThemeSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Map/Icon Theme")]
```

| Field | Type | Purpose |
|-------|------|---------|
| Entries | MapIconThemeEntry[] | Per-IconType visual config |

```csharp
[Serializable]
public struct MapIconThemeEntry
{
    public MapIconType IconType;
    public Sprite Icon;               // Minimap + world map sprite
    public Sprite CompassIcon;        // Compass variant (may be smaller)
    public Color DefaultColor;        // Default tint (Enemy=red, NPC=yellow, etc.)
    public float ScaleMultiplier;     // Per-type scale (Boss=1.5, Loot=0.8)
    public bool ShowOnCompass;        // Whether this type appears on compass
    public bool ShowOnWorldMap;       // Whether this type appears on world map
    public bool ShowDistance;         // Show distance text on compass
    public int SortOrder;             // Z-order for overlap (higher = on top)
}
```

Default theme colors:
- Player: green (#00FF00)
- PartyMember: blue (#4488FF)
- Enemy: red (#FF3333)
- NPC: yellow (#FFDD00)
- QuestObjective: gold (#FFD700)
- QuestGiver: gold with exclamation mark
- Vendor: yellow with coin
- CraftStation: orange (#FF8800)
- Loot: white (#FFFFFF)
- POI: silver (#CCCCCC)
- FastTravel: cyan (#00DDFF)
- Danger: dark red (#880000)
- Boss: red with skull, ScaleMultiplier=1.5

### POIRegistrySO

**File:** `Assets/Scripts/Map/Definitions/POIRegistrySO.cs`

```
[CreateAssetMenu(menuName = "DIG/Map/POI Registry")]
```

| Field | Type | Purpose |
|-------|------|---------|
| POIs | POIDefinition[] | All permanent points of interest |
| AutoDiscoverRadius | float | Distance at which POIs are auto-discovered (default 30) |
| DiscoverXPReward | float | Exploration XP per discovery (ties to EPIC 16.14 XPGrantAPI) |

```csharp
[Serializable]
public struct POIDefinition
{
    public int POIId;                  // Stable unique ID (never changes)
    public string Label;               // Display name
    public POIType Type;               // Town/Dungeon/etc.
    public Vector3 WorldPosition;      // Used for compass + world map (authoring may override)
    public bool IsFastTravel;          // Enables fast travel from world map
    public bool RequiresDiscovery;     // Must be visited before showing on world map
    public string Description;         // Tooltip text on world map hover
    public Sprite OverrideIcon;        // null = use theme default for POIType
}
```

---

## System Execution Order

```
InitializationSystemGroup (Client|Local):
  MinimapBootstrapSystem                    -- loads SOs, creates singletons, spawns camera (runs once)

SimulationSystemGroup (Client|Local):
  MapIconUpdateSystem                       -- frame-spread entity → icon buffer projection
    [UpdateAfter DeathTransitionSystem]     -- ensures DeathState is current

PresentationSystemGroup (Client|Local):
  MinimapCameraSystem                       -- positions orthographic camera on local player
  FogOfWarSystem (after MinimapCamera)      -- reveals fog texture at player position
  CompassSystem (after FogOfWar)            -- computes POI angles/distances
  MapUIBridgeSystem (after Compass)         -- pushes all data to UI registry, managed SystemBase
```

---

## ECS Systems

### MinimapBootstrapSystem

**File:** `Assets/Scripts/Map/Systems/MinimapBootstrapSystem.cs`

- Managed SystemBase, `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`, InitializationSystemGroup
- Runs once, self-disables (`Enabled = false`)
- Loads `MinimapConfigSO` from `Resources/MinimapConfig`
- Loads `MapIconThemeSO` from `Resources/MapIconTheme`
- Loads `POIRegistrySO` from `Resources/POIRegistry`
- Creates `MinimapConfig` singleton entity from SO values
- Creates `MapRevealState` singleton with fog texture dimensions + world bounds
- Creates `MapManagedState` managed singleton:
  - Instantiates `RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)` for minimap
  - Instantiates `RenderTexture(fogW, fogH, 0, RenderTextureFormat.R8)` for fog (initialized to 0 = unexplored)
  - Spawns orthographic `Camera` via `new GameObject("MinimapCamera")`, assigns RT, sets `cullingMask` to exclude UI layers
  - Initializes `IconBuffer` and `CompassBuffer` NativeLists
- Registers all data with `MapUIRegistry` static class

### MapIconUpdateSystem

**File:** `Assets/Scripts/Map/Systems/MapIconUpdateSystem.cs`

- Managed SystemBase, `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`, SimulationSystemGroup
- `[UpdateAfter(typeof(DeathTransitionSystem))]`
- Implements frame-spread pattern from DetectionSystem:

```
Core logic (pseudocode):
  frameSlot = SystemAPI.Time.ElapsedTime (cast to int, modulo UpdateFrameSpread)
  IconBuffer.Clear() on frame 0 of spread cycle

  foreach (entity, mapIcon, ltw) in SystemAPI.Query<MapIcon, LocalToWorld>()
      .WithNone<DeathState>()          // skip dead entities (DeathState only added on death)
      .WithDisabled<CorpseState>()     // skip corpses (CorpseState enabled on death)
  {
      entityIndex = entity.Index;
      if (entityIndex % UpdateFrameSpread != frameSlot) continue;  // frame-spread

      worldPos = ltw.Position;
      distSq = math.distancesq(worldPos.xz, localPlayerPos.xz);
      if (distSq > MaxIconRange * MaxIconRange) continue;           // range cull

      if (!mapIcon.VisibleOnMinimap) continue;                      // visibility flag

      IconBuffer.Add(new MapIconEntry {
          WorldPos2D = worldPos.xz,
          IconType = mapIcon.IconType,
          Priority = mapIcon.Priority,
          ColorPacked = mapIcon.CustomColorPacked,
          SourceEntity = entity
      });
  }

  // Add quest objective waypoints (always updated, not frame-spread)
  foreach (questProgress, objBuffer) with local player:
      foreach objective where !IsComplete:
          IconBuffer.Add(QuestObjective entry at objective.WorldPosition)

  // Add party member positions (always updated)
  foreach player with PlayerTag, !GhostOwnerIsLocal:
      IconBuffer.Add(PartyMember entry at player LocalToWorld.Position)

  // Add local player icon (always, highest priority)
  IconBuffer.Add(Player entry at localPlayerPos)
```

**Entity queries:**
- Main query: `MapIcon` + `LocalToWorld`, `WithNone<DeathState>`, `WithDisabled<CorpseState>`
- Enemy filter: `HasHitboxes` required to avoid phantom ghost duplicates
- Boss detection: checks if `MapIcon.IconType == Boss` OR if entity has a boss marker component
- Local player: `GhostOwnerIsLocal` + `PlayerTag` + `LocalToWorld`
- Party members: `PlayerTag` + `LocalToWorld`, `WithNone<GhostOwnerIsLocal>`
- Quest objectives: local player's `ObjectiveProgress` buffer

### MinimapCameraSystem

**File:** `Assets/Scripts/Map/Systems/MinimapCameraSystem.cs`

- Managed SystemBase, `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`, PresentationSystemGroup
- Reads local player `LocalToWorld` position + rotation
- Positions minimap camera:

```
camera.transform.position = new Vector3(playerPos.x, playerPos.y + cameraHeight, playerPos.z);
camera.transform.rotation = Quaternion.Euler(90, 0, 0);  // Look straight down
camera.orthographicSize = minimapConfig.Zoom;

if (minimapConfig.RotateWithPlayer)
    camera.transform.rotation = Quaternion.Euler(90, playerYaw, 0);
```

- Handles zoom input: scroll wheel adjusts `MinimapConfig.Zoom` clamped to `[MinZoom, MaxZoom]`
- Camera culling mask excludes: UI layer, minimap icons layer (icons rendered as UI overlays, not in 3D)
- Camera depth set below main camera to render first

### FogOfWarSystem

**File:** `Assets/Scripts/Map/Systems/FogOfWarSystem.cs`

- Managed SystemBase, `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`, PresentationSystemGroup
- `[UpdateAfter(typeof(MinimapCameraSystem))]`
- Reads local player world position, compares to `MapRevealState.LastRevealX/Z`
- If player moved beyond `RevealMoveThreshold`:

```
Core logic:
  fogU = (playerX - WorldMinX) / (WorldMaxX - WorldMinX) * FogTextureWidth
  fogV = (playerZ - WorldMinZ) / (WorldMaxZ - WorldMinZ) * FogTextureHeight

  // Draw filled circle on fog texture (GPU path)
  Graphics.Blit(null, FogOfWarTexture, FogRevealMaterial, pass=0)
  // FogRevealMaterial sets _Center = (fogU, fogV), _Radius = RevealRadius in texels
  // Shader writes 1.0 to R channel within radius (max blend, never re-fogs)

  MapRevealState.LastRevealX = playerX;
  MapRevealState.LastRevealZ = playerZ;
  MapRevealState.TotalRevealed += (approximate new pixels revealed);
```

- **POI auto-discovery**: after fog reveal, checks all `PointOfInterest` entities within `AutoDiscoverRadius`:
  - If `!DiscoveredByPlayer` and distance < `AutoDiscoverRadius`:
    - Sets `DiscoveredByPlayer = true`
    - Enqueues discovery event to `MapUIRegistry` (toast notification)
    - Calls `XPGrantAPI.GrantXP(player, DiscoverXPReward, XPSourceType.Exploration)` if EPIC 16.14 present
    - Marks fog save as dirty

- **Fog texture format**: `RenderTextureFormat.R8` -- single channel, 0 = unexplored, 255 = explored. 1024x1024 = 1 MB VRAM. Applied as overlay on minimap and world map via shader multiply.

### CompassSystem

**File:** `Assets/Scripts/Map/Systems/CompassSystem.cs`

- Managed SystemBase, `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`, PresentationSystemGroup
- `[UpdateAfter(typeof(FogOfWarSystem))]`
- Reads local player position + forward direction (yaw angle)
- Iterates all `PointOfInterest` entities where `DiscoveredByPlayer == true`:

```
Core logic:
  CompassBuffer.Clear();

  foreach (poi, ltw) in PointOfInterest + LocalToWorld:
      if (!poi.DiscoveredByPlayer) continue;
      delta = ltw.Position.xz - playerPos.xz;
      dist = math.length(delta);
      if (dist > CompassRange) continue;

      angle = math.atan2(delta.y, delta.x) - playerYawRadians;  // relative to player facing
      CompassBuffer.Add(new CompassEntry {
          Angle = angle,
          Distance = dist,
          IconType = MapIconType.POI,
          Label = poi.Label,
          IsQuestWaypoint = false
      });

  // Add active quest objective waypoints to compass
  foreach objective in local player ObjectiveProgress where !IsComplete:
      delta = objective.WorldPosition.xz - playerPos.xz;
      dist = math.length(delta);
      angle = math.atan2(delta.y, delta.x) - playerYawRadians;
      CompassBuffer.Add(new CompassEntry {
          Angle = angle,
          Distance = dist,
          IconType = MapIconType.QuestObjective,
          Label = objective short name,
          IsQuestWaypoint = true
      });
```

### MapUIBridgeSystem

**File:** `Assets/Scripts/Map/Systems/MapUIBridgeSystem.cs`

- Managed SystemBase, `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`, PresentationSystemGroup
- `[UpdateAfter(typeof(CompassSystem))]`
- Reads `MapManagedState.IconBuffer` and `CompassBuffer`
- Reads `MapRevealState` for fog stats
- Reads local player position + rotation for world map player marker
- Dispatches to `MapUIRegistry`:

```
MapUIRegistry.UpdateMinimapIcons(iconBuffer, playerPos, playerYaw, zoom);
MapUIRegistry.UpdateCompass(compassBuffer);
MapUIRegistry.UpdateFogStats(totalRevealed, totalPixels);
MapUIRegistry.UpdatePlayerMarker(playerPos, playerYaw);
```

---

## Authoring

### MapIconAuthoring

**File:** `Assets/Scripts/Map/Authoring/MapIconAuthoring.cs`

```
[AddComponentMenu("DIG/Map/Map Icon")]
```

| Field | Type | Purpose |
|-------|------|---------|
| IconType | MapIconType | Dropdown: Enemy, NPC, Vendor, etc. |
| Priority | byte | Overlap priority (higher = on top) |
| VisibleOnMinimap | bool | Show on minimap (default true) |
| VisibleOnWorldMap | bool | Show on world map (default true) |
| UseCustomColor | bool | Override theme default color |
| CustomColor | Color | Custom RGBA (shown if UseCustomColor) |

Baker adds `MapIcon` IComponentData to the entity. 8 bytes, no ghost replication.

**Placement:** Add to enemy prefabs (BoxingJoe, etc.), NPC prefabs (dialogue speakers), crafting station prefabs, loot container prefabs, vendor NPCs, boss prefabs.

### PointOfInterestAuthoring

**File:** `Assets/Scripts/Map/Authoring/PointOfInterestAuthoring.cs`

```
[AddComponentMenu("DIG/Map/Point of Interest")]
```

| Field | Type | Purpose |
|-------|------|---------|
| POIId | int | Stable unique ID (matches POIRegistrySO entry) |
| Type | POIType | Town, Dungeon, Boss, FastTravel, etc. |
| Label | string | Display name (max 29 chars for FixedString32) |
| DiscoveredByDefault | bool | Start discovered (towns = true, dungeons = false) |

Baker adds `PointOfInterest` + `MapIcon(IconType=POI or FastTravel)` to the entity.

**Placement:** Add to landmark GameObjects in subscenes -- town centers, dungeon entrances, boss arena markers, fast travel shrines.

---

## UI Bridge

### MapUIRegistry

**File:** `Assets/Scripts/Map/Bridges/MapUIRegistry.cs`

Static registry (same pattern as `CombatUIRegistry`, `ProgressionUIRegistry`):

```csharp
public static class MapUIRegistry
{
    private static IMinimapProvider _minimapProvider;
    private static IWorldMapProvider _worldMapProvider;
    private static ICompassProvider _compassProvider;
    private static IMapNotificationProvider _notificationProvider;

    public static void RegisterMinimap(IMinimapProvider p) => _minimapProvider = p;
    public static void UnregisterMinimap(IMinimapProvider p) { if (_minimapProvider == p) _minimapProvider = null; }
    public static void RegisterWorldMap(IWorldMapProvider p) => _worldMapProvider = p;
    public static void UnregisterWorldMap(IWorldMapProvider p) { if (_worldMapProvider == p) _worldMapProvider = null; }
    public static void RegisterCompass(ICompassProvider p) => _compassProvider = p;
    public static void UnregisterCompass(ICompassProvider p) { if (_compassProvider == p) _compassProvider = null; }
    public static void RegisterNotification(IMapNotificationProvider p) => _notificationProvider = p;
    public static void UnregisterNotification(IMapNotificationProvider p) { if (_notificationProvider == p) _notificationProvider = null; }

    // Called by MapUIBridgeSystem
    public static void UpdateMinimapIcons(NativeList<MapIconEntry> icons, float3 playerPos, float playerYaw, float zoom)
        => _minimapProvider?.UpdateIcons(icons, playerPos, playerYaw, zoom);
    public static void UpdateCompass(NativeList<CompassEntry> entries)
        => _compassProvider?.UpdateEntries(entries);
    public static void UpdateFogStats(int revealed, int total)
        => _minimapProvider?.UpdateFogProgress(revealed, total);
    public static void UpdatePlayerMarker(float3 pos, float yaw)
        => _worldMapProvider?.UpdatePlayerMarker(pos, yaw);
    public static void NotifyPOIDiscovered(string label, POIType type)
        => _notificationProvider?.OnPOIDiscovered(label, type);
}
```

### Provider Interfaces

**File:** `Assets/Scripts/Map/Bridges/IMapUIProviders.cs`

```csharp
public interface IMinimapProvider
{
    void UpdateIcons(NativeList<MapIconEntry> icons, float3 playerPos, float playerYaw, float zoom);
    void UpdateFogProgress(int revealed, int total);
    void SetRenderTexture(RenderTexture minimapRT, RenderTexture fogRT);
    void SetZoom(float zoom);
}

public interface IWorldMapProvider
{
    void UpdatePlayerMarker(float3 worldPos, float yaw);
    void SetFogTexture(RenderTexture fogRT);
    void ShowWorldMap(bool show);
    void SetZoneLabels(POIDefinition[] pois);
    void HighlightFastTravel(int poiId);
}

public interface ICompassProvider
{
    void UpdateEntries(NativeList<CompassEntry> entries);
    void SetVisible(bool visible);
}

public interface IMapNotificationProvider
{
    void OnPOIDiscovered(string label, POIType type);
    void OnZoneEntered(string zoneName);
}
```

### MonoBehaviour UI Implementations

**File:** `Assets/Scripts/Map/UI/MinimapView.cs`
- Renders circular/square minimap overlay using `RawImage` + mask
- Positions icon Image elements over minimap based on world-to-screen projection
- Registers as `IMinimapProvider` on `OnEnable`, unregisters on `OnDisable`
- Manages icon pooling (pre-allocates 64 Image objects, recycles per frame)

**File:** `Assets/Scripts/Map/UI/WorldMapView.cs`
- Fullscreen UI panel (toggle via M key or configurable binding)
- Displays fog-of-war texture as background overlay
- Player position marker (arrow icon, rotates with yaw)
- Zone labels positioned at POI world coordinates mapped to panel UV
- Fast travel points: clickable buttons, triggers fast travel RPC if discovered
- Zoom + pan via scroll wheel and drag

**File:** `Assets/Scripts/Map/UI/CompassView.cs`
- Horizontal bar at screen top edge
- Cardinal directions (N/S/E/W) at fixed angles
- POI and quest icons slide along bar based on relative angle
- Distance text below icons when within threshold
- Registers as `ICompassProvider`

**File:** `Assets/Scripts/Map/UI/MapNotificationView.cs`
- Toast popup: "Discovered: Whispering Ruins" with fade animation
- Registers as `IMapNotificationProvider`

---

## Save Integration

### MapSaveModule (ISaveModule TypeId=13)

**File:** `Assets/Scripts/Persistence/Modules/MapSaveModule.cs`

```csharp
public class MapSaveModule : ISaveModule
{
    public int TypeId => 13;
    public string DisplayName => "Map";
    public int ModuleVersion => 1;
}
```

**Serialization format:**

```
FogTextureWidth    : int32
FogTextureHeight   : int32
CompressionFlag    : byte (0x00=raw, 0x01=RLE)
FogPixelData       : byte[] (R8 texture pixels, RLE compressed)

DiscoveredPOICount : int16
foreach:
  POIId            : int32
  DiscoverTimestamp : float32

TotalRevealed      : int32
```

**Fog texture serialization:**
1. `AsyncGPUReadback.Request(FogOfWarTexture)` → `NativeArray<byte>` (R8 pixels)
2. RLE compress (fog data is mostly 0x00 with clusters of 0xFF, highly compressible)
3. Write compressed bytes to save stream

**Fog texture deserialization:**
1. Read RLE bytes, decompress to `byte[]`
2. `fogTexture.LoadRawTextureData(bytes)` → `fogTexture.Apply()`
3. Restore `MapRevealState.TotalRevealed`
4. Iterate discovered POIs, set `PointOfInterest.DiscoveredByPlayer = true` on matching entities

**Size budget:** 1024x1024 R8 = 1 MB raw. RLE compression achieves 10-50x on typical fog (90%+ unexplored early game). Expected save size: 20-100 KB for fog + ~100 bytes for discovered POIs.

**Dirty check:** `IsDirty()` returns true when `MapRevealState.TotalRevealed` has changed since last save.

---

## Editor Tooling

### MapWorkstationWindow

**File:** `Assets/Editor/MapWorkstation/MapWorkstationWindow.cs`
- Menu: `DIG/Map Workstation`
- Sidebar + `IMapWorkstationModule` pattern (matches existing Workstation windows)

### Modules (5 Tabs)

| Tab | File | Purpose |
|-----|------|---------|
| Config Editor | `Modules/ConfigEditorModule.cs` | Edit `MinimapConfigSO` fields: zoom range, icon scale, frame spread, fog resolution. Live preview slider for zoom. RT size dropdown. |
| Icon Theme | `Modules/IconThemeModule.cs` | Edit `MapIconThemeSO`: per-type color pickers, sprite fields, compass toggle, sort order. Preview grid showing all 13 icon types with current theme. |
| POI Manager | `Modules/POIManagerModule.cs` | List all `POIRegistrySO` entries. Add/remove/edit POIs. "Select in Scene" button. Duplicate ID validator. Discovery radius visualizer. |
| Fog Preview | `Modules/FogPreviewModule.cs` | Play-mode only: live fog texture display. Stats: total revealed/total pixels, percentage. "Reveal All" and "Reset Fog" debug buttons. Save/load fog to/from file. |
| Live Inspector | `Modules/LiveInspectorModule.cs` | Play-mode only: icon buffer count, compass entry count, minimap camera position/zoom, local player position, discovered POI list. Toggle compass/minimap visibility. Manual zoom override. |

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `MinimapBootstrapSystem` | N/A | No | Runs once at startup |
| `MapIconUpdateSystem` | < 0.3ms | No | Frame-spread K=4 → 25% of entities per frame. 200 total entities = 50 per frame |
| `MinimapCameraSystem` | < 0.05ms | No | Single camera position + zoom. Unity renders RT via standard pipeline |
| `FogOfWarSystem` | < 0.1ms | No | GPU blit (1 draw call), CPU checks only player movement threshold |
| `CompassSystem` | < 0.05ms | No | Typically 5-20 POIs + 1-5 quest objectives. Trivial trig |
| `MapUIBridgeSystem` | < 0.05ms | No | Managed, dispatches NativeLists to UI providers |
| Minimap RT render | < 0.5ms GPU | N/A | Orthographic camera, reduced draw distance, simplified LOD |
| Fog reveal blit | < 0.02ms GPU | N/A | Single fullscreen pass on R8 texture |
| **Total CPU** | **< 0.55ms** | | All systems combined (client only, zero server cost) |
| **Total GPU** | **< 0.52ms** | | RT render + fog blit |
| **VRAM** | **< 2.5 MB** | | Minimap RT (512x512 ARGB=1MB) + Fog RT (1024x1024 R8=1MB) + staging (1MB) |

### Scaling Considerations

| Entity Count | Frame-Spread K=4 | Per-Frame Icons | Budget |
|-------------|-------------------|-----------------|--------|
| 100 entities | 25 per frame | < 0.15ms | Well under budget |
| 500 entities | 125 per frame | < 0.4ms | At budget limit |
| 1000 entities | 250 per frame | ~0.8ms | Increase K to 8 (< 0.4ms) |

If entity count exceeds 500, `MinimapConfigSO.UpdateFrameSpread` should be increased to 8 or 16. The system dynamically reads this value -- no code change needed.

---

## File Summary

### New Files (28)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Map/Components/MapIconComponents.cs` | IComponentData + enum |
| 2 | `Assets/Scripts/Map/Components/PointOfInterestComponents.cs` | IComponentData + enum |
| 3 | `Assets/Scripts/Map/Components/MapSingletons.cs` | IComponentData singletons + managed singleton |
| 4 | `Assets/Scripts/Map/Components/MapDataStructs.cs` | Shared structs (MapIconEntry, CompassEntry, DiscoveredPOIRecord) |
| 5 | `Assets/Scripts/Map/Definitions/MinimapConfigSO.cs` | ScriptableObject |
| 6 | `Assets/Scripts/Map/Definitions/MapIconThemeSO.cs` | ScriptableObject |
| 7 | `Assets/Scripts/Map/Definitions/POIRegistrySO.cs` | ScriptableObject |
| 8 | `Assets/Scripts/Map/Systems/MinimapBootstrapSystem.cs` | SystemBase (Client|Local, runs once) |
| 9 | `Assets/Scripts/Map/Systems/MapIconUpdateSystem.cs` | SystemBase (Client|Local, SimulationSystemGroup) |
| 10 | `Assets/Scripts/Map/Systems/MinimapCameraSystem.cs` | SystemBase (Client|Local, PresentationSystemGroup) |
| 11 | `Assets/Scripts/Map/Systems/FogOfWarSystem.cs` | SystemBase (Client|Local, PresentationSystemGroup) |
| 12 | `Assets/Scripts/Map/Systems/CompassSystem.cs` | SystemBase (Client|Local, PresentationSystemGroup) |
| 13 | `Assets/Scripts/Map/Systems/MapUIBridgeSystem.cs` | SystemBase (Client|Local, PresentationSystemGroup) |
| 14 | `Assets/Scripts/Map/Bridges/MapUIRegistry.cs` | Static registry |
| 15 | `Assets/Scripts/Map/Bridges/IMapUIProviders.cs` | Provider interfaces (4 interfaces) |
| 16 | `Assets/Scripts/Map/UI/MinimapView.cs` | MonoBehaviour (IMinimapProvider) |
| 17 | `Assets/Scripts/Map/UI/WorldMapView.cs` | MonoBehaviour (IWorldMapProvider) |
| 18 | `Assets/Scripts/Map/UI/CompassView.cs` | MonoBehaviour (ICompassProvider) |
| 19 | `Assets/Scripts/Map/UI/MapNotificationView.cs` | MonoBehaviour (IMapNotificationProvider) |
| 20 | `Assets/Scripts/Map/Authoring/MapIconAuthoring.cs` | Baker |
| 21 | `Assets/Scripts/Map/Authoring/PointOfInterestAuthoring.cs` | Baker |
| 22 | `Assets/Scripts/Map/Shaders/FogReveal.shader` | Fog circle stamp shader (single pass, R8 max blend) |
| 23 | `Assets/Scripts/Map/Shaders/MinimapFogOverlay.shader` | Minimap fog composite (fog texture * minimap RT) |
| 24 | `Assets/Scripts/Map/DIG.Map.asmdef` | Assembly definition |
| 25 | `Assets/Scripts/Persistence/Modules/MapSaveModule.cs` | ISaveModule (TypeId=13) |
| 26 | `Assets/Editor/MapWorkstation/MapWorkstationWindow.cs` | EditorWindow (5 tabs) |
| 27 | `Assets/Editor/MapWorkstation/IMapWorkstationModule.cs` | Module interface |
| 28 | `Assets/Editor/MapWorkstation/Modules/ConfigEditorModule.cs` | Editor module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | Enemy prefabs (BoxingJoe, etc.) | Add `MapIconAuthoring(IconType=Enemy)` |
| 2 | NPC prefabs (dialogue speakers) | Add `MapIconAuthoring(IconType=NPC)` |
| 3 | Boss prefabs | Add `MapIconAuthoring(IconType=Boss)` |
| 4 | Crafting station prefabs | Add `MapIconAuthoring(IconType=CraftStation)` |
| 5 | Vendor NPC prefabs | Add `MapIconAuthoring(IconType=Vendor)` |
| 6 | Loot container prefabs | Add `MapIconAuthoring(IconType=Loot)` |
| 7 | `Assets/Scripts/Persistence/Core/SaveModuleTypeIds.cs` | Add `Map = 13` constant |
| 8 | `Assets/Scripts/Persistence/Systems/PersistenceBootstrapSystem.cs` | Register `MapSaveModule` in module list |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/MinimapConfig.asset` |
| 2 | `Resources/MapIconTheme.asset` |
| 3 | `Resources/POIRegistry.asset` |

---

## Cross-EPIC Integration

| Source | EPIC | Integration |
|--------|------|-------------|
| Quest System | 16.12 | `ObjectiveProgress.WorldPosition` read by MapIconUpdateSystem + CompassSystem for quest waypoint markers |
| Progression | 16.14 | `XPGrantAPI.GrantXP(XPSourceType.Exploration)` called on POI discovery |
| Save/Load | 16.15 | `MapSaveModule` (TypeId=13) persists fog texture + discovered POIs via ISaveModule pattern |
| Dialogue | 16.16 | `DialogueSpeakerData` entities get `MapIconAuthoring(IconType=NPC)` for yellow minimap icons |
| Crafting | 16.13 | Crafting station entities get `MapIconAuthoring(IconType=CraftStation)` |
| Loot/Items | 16.6 | Loot container entities get `MapIconAuthoring(IconType=Loot)`, hidden when looted |
| Corpse Lifecycle | 16.3 | `CorpseState` enabled entities filtered out by MapIconUpdateSystem (no dead enemy dots) |
| Aggro / Detection | 15.33 | Enemies in COMBAT alert level could pulse red on minimap (future enhancement) |
| Skill Trees | 17.1 | Future: talent that increases minimap range or reveals enemies through fog |
| Party System | 17.2 | `PartyLink` buffer provides party member entity references for blue icons |
| VFX Pipeline | 16.7 | POI discovery triggers VFXRequest (discovery sparkle effect at POI location) |
| Knockback | 16.9 | No direct integration |
| Surface Material | 16.10 | No direct integration |

---

## Multiplayer Considerations

- **All map systems are Client|Local only** -- the server never runs minimap, fog, compass, or icon update logic
- **MapIcon is NOT ghost-replicated** -- baked at authoring time on each client independently
- **PointOfInterest is NOT ghost-replicated** -- baked at authoring time, `DiscoveredByPlayer` is client-local state
- **Fog-of-war is per-player** -- each client has its own fog texture. Saved via MapSaveModule per-player file
- **Party member positions** use existing ghost-replicated `LocalToWorld` on `PlayerTag` entities -- no additional ghost components needed
- **Enemy positions** use ghost-replicated `LocalToWorld` on interpolated ghosts -- MapIconUpdateSystem reads these directly
- **Listen server**: host sees their own fog state. Dedicated server: no map systems loaded (server world has no Client filter)
- **Fast travel** (world map): client sends `FastTravelRpc(poiId)` to server. Server validates POI is discovered + player is not in combat, then teleports. Future EPIC scope.

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `MapIcon` | 8 bytes | Enemy/NPC/POI entities (NOT player) |
| `PointOfInterest` | 20 bytes | Landmark entities (NOT player) |
| **Total on player entity** | **0 bytes** | |

The local player's map presence is determined by querying `GhostOwnerIsLocal` + `LocalToWorld` -- no component is added to the player archetype. Party members are identified by `PlayerTag` without `GhostOwnerIsLocal`. This design adds zero bytes to the player entity, staying well clear of the 16KB archetype limit.

---

## Extensibility

- **Dynamic icons**: Loot entities spawned at runtime already have `MapIcon` from authoring. Destroyed entities automatically disappear from icon buffer next frame-spread cycle.
- **Custom icon colors**: `MapIcon.CustomColorPacked` allows per-entity color overrides (e.g., elite enemies = orange, quest-critical NPCs = gold).
- **Multiple fog layers**: Future support for multi-floor dungeons by adding a `FogLayerIndex` to `MapRevealState` and switching textures per floor.
- **Minimap ping**: Party members could send ping events that flash a temporary icon on all party minimaps (requires RPC, future EPIC).
- **Danger zones**: `MapIconType.Danger` for environmental hazards, radiation zones, or boss aggro ranges displayed as colored circles on the minimap.
- **Real-time enemy tracking**: Toggle in settings to show enemy icons only when they are in combat with the player (uses `AlertLevel >= COMBAT` from EPIC 15.33 aggro system).
- **Objective trail**: Render a dotted line from player to active quest objective on the minimap (requires pathfinding integration, future scope).

---

## Verification Checklist

### Bootstrap & Initialization
- [ ] `MinimapBootstrapSystem` creates `MinimapConfig` singleton from `MinimapConfigSO`
- [ ] `MinimapBootstrapSystem` creates `MapRevealState` singleton with correct world bounds
- [ ] `MapManagedState` initialized: minimap RT, fog RT, camera spawned
- [ ] Minimap camera is orthographic, looks straight down, correct culling mask
- [ ] System self-disables after first run (`Enabled = false`)
- [ ] Missing `Resources/MinimapConfig` logs error and uses safe defaults

### Minimap Icons
- [ ] Enemy entities with `MapIconAuthoring(Enemy)` show red dots on minimap
- [ ] NPC entities with `MapIconAuthoring(NPC)` show yellow dots on minimap
- [ ] Boss entities show larger red icon with skull
- [ ] Dead enemies (DeathState) disappear from minimap
- [ ] Corpse entities (CorpseState enabled) disappear from minimap
- [ ] Phantom ghost duplicates (no HasHitboxes) do NOT appear on minimap
- [ ] Local player shows green arrow icon at minimap center
- [ ] Party member players show blue dots at correct positions
- [ ] Quest objective waypoints show gold star icons
- [ ] Icons beyond `MaxIconRange` are culled (not visible)
- [ ] Frame-spread: with K=4, only 25% of entities update per frame (verify in Profiler)

### Minimap Camera
- [ ] Camera follows local player XZ position each frame
- [ ] Zoom in (scroll up): orthographic size decreases, more detail
- [ ] Zoom out (scroll down): orthographic size increases, wider view
- [ ] Zoom clamped to [MinZoom, MaxZoom]
- [ ] RotateWithPlayer=true: minimap rotates with player facing
- [ ] RotateWithPlayer=false: minimap stays north-up
- [ ] Minimap render texture updates in real-time (terrain, structures visible)

### Fog of War
- [ ] Fog starts fully black (unexplored) at session start
- [ ] Moving player reveals circular area around player position
- [ ] Reveal radius matches `MinimapConfigSO.RevealRadius`
- [ ] Standing still does NOT repeatedly reveal (move threshold check)
- [ ] Fog overlay visible on both minimap and world map
- [ ] `MapRevealState.TotalRevealed` increments as new areas explored
- [ ] Fog is persistent within session (revisiting area stays revealed)
- [ ] Fog save: quit and reload, fog state restored from save file

### Points of Interest
- [ ] POI with `DiscoveredByDefault=true` visible on world map from start
- [ ] POI with `RequiresDiscovery=true` hidden until player approaches within `AutoDiscoverRadius`
- [ ] Discovery triggers toast notification ("Discovered: Whispering Ruins")
- [ ] Discovery grants exploration XP via `XPGrantAPI` (if EPIC 16.14 present)
- [ ] Discovered POIs persist across save/load
- [ ] Fast travel POIs marked distinctly on world map (cyan icon)

### Compass
- [ ] Compass bar visible at screen top
- [ ] Cardinal directions (N/S/E/W) at correct angles
- [ ] Discovered POIs appear on compass with correct relative angle
- [ ] Quest objectives appear on compass with gold highlight
- [ ] Icons slide smoothly as player rotates
- [ ] Distance text shown for nearby POIs
- [ ] POIs beyond `CompassRange` not shown on compass

### World Map
- [ ] Toggle world map with M key (or configured binding)
- [ ] Full-screen panel shows fog-of-war texture overlay
- [ ] Player position marker (arrow) at correct world position
- [ ] Player marker rotates with player yaw
- [ ] Discovered POIs shown with labels
- [ ] Undiscovered POIs hidden (or shown as ? if partially explored)
- [ ] Zoom and pan via scroll wheel and drag
- [ ] Fast travel points clickable (future: triggers teleport RPC)

### Save/Load (MapSaveModule TypeId=13)
- [ ] Fog texture serialized with RLE compression
- [ ] Discovered POI list serialized with stable POIIds
- [ ] Load restores fog texture pixel-perfect
- [ ] Load sets `DiscoveredByPlayer=true` on matching POI entities
- [ ] Missing save data (new player): starts with fresh fog (all unexplored)
- [ ] `IsDirty()` returns false when no new exploration since last save
- [ ] Save size reasonable: <100 KB for typical fog + POI data

### Performance
- [ ] `MapIconUpdateSystem` < 0.3ms with 200 entities (frame-spread K=4)
- [ ] Zero systems running on `ServerSimulation` world
- [ ] Minimap RT render < 0.5ms GPU
- [ ] Fog reveal blit < 0.02ms GPU
- [ ] Total VRAM usage < 2.5 MB

### Multiplayer
- [ ] Remote client: sees own fog-of-war (not shared with host)
- [ ] Remote client: sees enemy and NPC icons from local entity positions
- [ ] Remote client: sees party member icons via ghost-replicated PlayerTag entities
- [ ] Listen server host: map systems run in client world only, not server world
- [ ] Dedicated server: no map systems loaded (no ClientSimulation world)

### Editor Tooling
- [ ] Map Workstation: Config Editor shows all MinimapConfigSO fields with live preview
- [ ] Map Workstation: Icon Theme shows all 13 icon types with color pickers
- [ ] Map Workstation: POI Manager lists all POIs, validates unique IDs
- [ ] Map Workstation: Fog Preview shows live fog texture in play mode
- [ ] Map Workstation: Live Inspector shows icon count, compass entries, camera state
