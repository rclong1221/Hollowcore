# EPIC 13.2 Setup Guide: Zone Prefab Setup, Topology Variants & NavMesh Config

**Status:** Planned
**Requires:** 13.1 (DistrictDefinitionSO), Framework: Roguelite/IZoneProvider, Voxel/

---

## Overview

Configure the district generation pipeline that turns a seed + zone definition into playable 3D space. This guide covers the four generation strategies (prefab assembly, voxel, hybrid, hand-crafted), zone room prefab authoring with connection sockets, topology variant creation, NavMesh baking, and the SpawnPointMarker / GateMarker workflow.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| DistrictDefinitionSO | EPIC 13.1 complete | Zone graph and topology data |
| Framework IZoneProvider | `DIG.Roguelite.IZoneProvider` | Lifecycle contract (Initialize/IsReady/Activate/Deactivate/Dispose) |
| Zone prefab pool | Room GameObjects with sockets | Building blocks for prefab assembly |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| DistrictGenerationEnums.cs | `Assets/Scripts/District/Generation/` | C# (enums) |
| DistrictGeneratorConfigSO.cs | `Assets/Scripts/District/Generation/` | C# (ScriptableObject) |
| ZoneRoomPoolSO.cs | `Assets/Scripts/District/Generation/` | C# (ScriptableObject) |
| ZoneBoundaryComponents.cs | `Assets/Scripts/District/Components/` | C# (ECS) |
| DistrictGeneratorBase.cs | `Assets/Scripts/District/Generation/` | C# (abstract class) |
| PrefabAssemblyGenerator.cs | `Assets/Scripts/District/Generation/` | C# (IZoneProvider) |
| SpawnPointMarker.cs | `Assets/Scripts/District/Generation/` | C# (MonoBehaviour) |
| GateMarker.cs | `Assets/Scripts/District/Generation/` | C# (MonoBehaviour) |
| Per-district DistrictGeneratorConfigSO | `Assets/Data/Districts/[Name]/` | ScriptableObject |
| Zone room prefabs | `Assets/Prefabs/Districts/[Name]/Rooms/` | Prefab |

---

## 1. Choose a Generation Mode
**Create:** `Assets > Create > Hollowcore/District/Generator Config`

### 1.1 DistrictGenerationMode Options
| Mode | Value | Best For | Effort |
|------|-------|----------|--------|
| PrefabAssembly | 0 | Most districts. Modular rooms connected by sockets | Medium |
| VoxelGeneration | 1 | Organic districts (Old Growth, Quarantine) | Low (procedural) |
| Hybrid | 2 | Prefab rooms + procedural corridors | High |
| HandCraftedProcedural | 3 | Complex set-piece topology (Necrospire, Chrome Cathedral) | Very High |

### 1.2 DistrictGeneratorConfigSO Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| Mode | Generation strategy | PrefabAssembly | Enum |
| RoomPool | ZoneRoomPoolSO for PrefabAssembly/Hybrid | null | Required for PrefabAssembly |
| ConnectorPool | Connector prefab pool for Hybrid mode | null | Required for Hybrid only |
| VoxelConfig | Voxel generation parameters | null | Required for VoxelGeneration |
| ScenePaths | Addressable scene paths per zone index | [] | Required for HandCrafted |
| NavMeshAgentTypeId | Agent type for pathfinding bake | 0 (Humanoid) | Match your agent config |
| GenerationTimeoutSeconds | Max async generation time | 10.0 | 5.0-30.0 |

---

## 2. Author Zone Room Prefabs (PrefabAssembly Mode)

### 2.1 Create a ZoneRoomPoolSO
**Create:** `Assets > Create > Hollowcore/District/Zone Room Pool`
**Recommended location:** `Assets/Data/Districts/[Name]/Rooms/[Name]_RoomPool.asset`

### 2.2 ZoneRoomEntry Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| RoomPrefab | Reference to the room GameObject prefab | null | Required |
| RoomName | Human-readable identifier | "" | For debug/editor |
| AllowedType | ZoneType this room can be used for | Combat | Enum |
| Size | Size hint (Small, Medium, Large) | Medium | Enum |
| ConnectionSockets | Socket tag names on this prefab | [] | At least 1 |
| Weight | Selection probability within type | 1.0 | 0.1-10.0 |

### 2.3 Room Prefab Structure
```
Room_CombatA (GameObject root)
  Geometry/
    Floor (MeshRenderer + MeshCollider)
    Walls (MeshRenderer + MeshCollider)
    Ceiling (MeshRenderer + MeshCollider)
  Sockets/
    Socket_North (empty GameObject, position at north door)
    Socket_South (empty GameObject, position at south door)
    Socket_East  (empty GameObject, position at east door)
  SpawnPoints/
    SpawnPoint_Enemy_0 (SpawnPointMarker, Type=Enemy)
    SpawnPoint_Enemy_1 (SpawnPointMarker, Type=Enemy)
    SpawnPoint_Loot_0  (SpawnPointMarker, Type=Loot)
    SpawnPoint_POI_0   (SpawnPointMarker, Type=POI)
  Gates/
    Gate_North (GateMarker, ConnectedZoneIndex set at generation time)
```

### 2.4 Socket Naming Convention
| Tag | Direction | Pairs With |
|-----|-----------|------------|
| Socket_North | +Z | Socket_South |
| Socket_South | -Z | Socket_North |
| Socket_East | +X | Socket_West |
| Socket_West | -X | Socket_East |
| Socket_Up | +Y | Socket_Down |
| Socket_Down | -Y | Socket_Up |

**Tuning tip:** Place socket GameObjects at the exact door/portal position. The generator aligns rooms by matching socket positions. Ensure socket positions are at consistent heights relative to the room floor.

---

## 3. Author SpawnPointMarker and GateMarker

### 3.1 SpawnPointMarker MonoBehaviour
**Add to:** Empty child GameObjects inside room prefabs at desired spawn locations.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| SpawnPointType | 0=Enemy, 1=Loot, 2=POI, 3=Interactable | 0 | 0-3 |

**Recommended per room:**
| Room Type | Enemy Points | Loot Points | POI Points | Interactable |
|-----------|-------------|-------------|------------|-------------|
| Combat | 3-6 | 1-2 | 0-1 | 0 |
| Elite | 2-4 | 2-3 | 0-1 | 0 |
| Shop | 0 | 0 | 0 | 2-3 |
| Rest | 0 | 1 | 1 | 1-2 |
| Boss | 1 (boss) | 1 | 0 | 0 |

### 3.2 GateMarker MonoBehaviour
**Add to:** Door/portal GameObjects that connect to other zones.

| Field | Description | Default |
|-------|-------------|---------|
| ConnectedZoneIndex | Index of the zone this gate leads to | -1 (set at generation) |

The generator resolves ConnectedZoneIndex from the zone graph at room placement time.

---

## 4. Configure NavMesh

### 4.1 NavMesh Surface Setup
| Approach | When |
|----------|------|
| Runtime bake | Room prefabs generate diverse layouts. Add NavMeshSurface to each room root. Bake after all rooms placed |
| Pre-baked per room | HandCrafted mode. Each scene has pre-baked NavMesh |
| Agent-based | Configure in Navigation window: Agent Type ID matches DistrictGeneratorConfigSO.NavMeshAgentTypeId |

### 4.2 NavMesh Agent Settings
| Setting | Value | Notes |
|---------|-------|-------|
| Agent Type | Humanoid (0) | Default for most enemies |
| Radius | 0.5 | Standard capsule half-width |
| Height | 2.0 | Standard capsule height |
| Step Height | 0.4 | Stairs/small ledges |
| Max Slope | 45 | Navigable slope angle |

### 4.3 NavMesh Validation
After generation, the validator checks:
- >= 80% of zone floor area is navigable
- All spawn points are on NavMesh (within 1m sampling distance)
- All gate positions are on NavMesh

---

## 5. Configure Topology Variants

For each TopologyVariant in DistrictDefinitionSO.TopologyVariants:

### 5.1 Variant Differentiation Strategies
| Strategy | Description | Example |
|----------|-------------|---------|
| Connectivity change | Add/remove zone connections | Variant B: zone 4 unreachable until zone 7 cleared |
| Entry point shift | Different starting zone | Variant C: entry at zone 6 instead of zone 0 |
| Zone type swap | Change zone types | Variant B: zone 3 becomes Elite instead of Combat |
| Front origin shift | Move the Front starting zone | Variant C: Front starts from zone 3 instead of zone 9 |
| Room prefab override | Different room layouts per variant | Variant B: ZonePrefabs[1] = alternate room |

### 5.2 Assign ZonePrefabs per Variant
| Field | Description |
|-------|-------------|
| ZonePrefabs[i] | Room prefab for zone index i. If null, generator picks from ZoneRoomPoolSO |

---

## Scene & Subscene Checklist

- [ ] DistrictGeneratorConfigSO created per district with correct Mode
- [ ] ZoneRoomPoolSO created with 10-15 room entries for PrefabAssembly districts
- [ ] All room prefabs have at least 1 SpawnPointMarker and 1 socket
- [ ] Socket naming follows convention (North/South/East/West/Up/Down)
- [ ] GateMarker placed at all zone-connecting doors
- [ ] NavMesh agent type configured in Navigation settings
- [ ] NavMeshSurface added to room roots (for runtime bake) or scenes pre-baked (HandCrafted)
- [ ] TopologyVariants in DistrictDefinitionSO have 2-3 entries

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Missing sockets on room prefabs | PrefabAssemblyGenerator cannot connect rooms, rooms placed overlapping | Add Socket_[Direction] empty GameObjects at door positions |
| Socket pairs mismatched | Rooms connect but doors don't align | North pairs with South, East with West, Up with Down |
| No SpawnPointMarker in room | Zone generates with 0 spawn points, no enemies appear | Add at least 1 SpawnPointMarker (Type=Enemy) per combat room |
| Room AABBs overlap | Rooms clip through each other | Ensure room geometry fits within its bounds; generator retries 3 times |
| NavMesh not baked after generation | Enemies stand still, cannot pathfind | Call NavMeshSurface.BuildNavMesh() after all rooms instantiated |
| ScenePaths empty for HandCrafted mode | Generator timeout, no geometry loads | Populate ScenePaths[i] with valid Addressable scene paths per zone index |
| GenerationTimeoutSeconds too low | Generation fails on complex districts | Increase to 15-30s for VoxelGeneration or large PrefabAssembly |

---

## Verification

- [ ] DistrictGeneratorBase creates ZoneBoundary entities for all zones in graph
- [ ] ZoneSpawnPoint buffer populated with at least 1 point per zone
- [ ] ZoneGate buffer populated with connections matching zone graph
- [ ] FrontOriginMarker entity created at correct position
- [ ] PrefabAssemblyGenerator socket alignment produces connected rooms without overlap
- [ ] Topology variant selection produces different layouts for different seeds
- [ ] NavMesh data present after generation (AI agents can pathfind)
- [ ] ZoneActivationResult.PlayerSpawnPosition is navigable
- [ ] Generation completes within GenerationTimeoutSeconds
- [ ] Deterministic: same seed produces identical layout
