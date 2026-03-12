# EPIC 17.4: Pre-NetCode Lobby System — Setup Guide

## Overview

The Lobby System provides a player-gathering phase **before** ECS worlds are created. Players can browse lobbies, create private/public games, select maps & difficulty, ready up, and transition into the existing NetCode game flow.

**Key design**: All lobby code is MonoBehaviour-based. No ECS entities, ghosts, or prediction during the lobby phase. The only ECS touch point is a transient `LobbySpawnData` singleton injected during the lobby→game transition.

---

## Quick Start

### 1. Add LobbyManager to Scene

1. Create an empty GameObject in your boot/main scene
2. Add the `LobbyManager` component
3. It will auto-persist via `DontDestroyOnLoad`

### 2. Create Lobby Config

1. **Right-click** in Project → **Create → DIG → Lobby → Lobby Config**
2. Name it `LobbyConfig`
3. Move it to `Assets/Resources/LobbyConfig.asset`
4. Configure values:

| Field | Default | Description |
|-------|---------|-------------|
| MaxPlayersPerLobby | 4 | Maximum players per lobby (2-6) |
| HeartbeatIntervalMs | 2000 | Keepalive interval |
| HeartbeatTimeoutMs | 8000 | Disconnect detection threshold |
| TransitionTimeoutSeconds | 15 | Lobby→game timeout |
| MinPlayersToStart | 1 | Minimum to enable Start button |
| JoinCodeLength | 6 | Characters in join code |

### 3. Create Map Definitions

1. **Right-click** → **Create → DIG → Lobby → Map Definition**
2. Fill in fields:
   - **MapId**: Unique integer (0, 1, 2...)
   - **DisplayName**: Shown in UI
   - **SpawnPositions**: One Vector3 per player slot
   - **SpawnRotations**: Matching rotations
   - **MaxPlayers**: Must have `SpawnPositions.Length >= MaxPlayers`
   - **ScenePath**: Addressable scene path for this map
3. Save anywhere under `Assets/Resources/` (or a subfolder)

### 4. Create Difficulty Definitions

1. **Right-click** → **Create → DIG → Lobby → Difficulty Definition**
2. Fill in fields:
   - **DifficultyId**: Unique integer (0=Easy, 1=Normal, 2=Hard...)
   - **EnemyHealthScale**: 1.0 = normal, 2.0 = double HP
   - **XPMultiplier**: Reward scaling
3. Save anywhere under `Assets/Resources/`

### 5. Set Up Lobby UI

1. Create a Canvas for the lobby UI
2. Add `LobbyUIManager` component
3. Create child panels:
   - **Browser Panel**: Add `LobbyBrowserPanel` — shows lobby list + join-by-code
   - **Room Panel**: Add `LobbyRoomPanel` — shows player slots + chat + start
   - **Transition Panel**: Add `TransitionLoadingPanel` — loading screen
4. Wire the panel references in `LobbyUIManager` inspector
5. Each `LobbyRoomPanel` needs `LobbyPlayerSlotUI` children (one per max player)

---

## Architecture

```
LOBBY PHASE (MonoBehaviour only)
├── LobbyManager (singleton, DontDestroyOnLoad)
│   ├── LobbyNetworkTransport (Unity Transport, port 7980)
│   ├── LobbyState (full lobby data)
│   └── LobbyDiscoveryService (browser queries)
├── UI
│   ├── LobbyUIManager (panel switcher)
│   ├── LobbyBrowserPanel (browse/join)
│   ├── LobbyRoomPanel (lobby room)
│   └── TransitionLoadingPanel (loading)

TRANSITION
└── LobbyToGameTransition
    ├── Calls GameBootstrap.CreateHost()/CreateClient()
    ├── Injects LobbySpawnData singleton into ServerWorld
    └── Existing SubsceneLoadHelper takes over

GAME PHASE (standard NetCode ECS)
├── GoInGameServerSystem reads LobbySpawnData → spawn positions
├── SaveIdAssignmentSystem reads LobbySpawnData → persistent PlayerId
└── LobbySpawnData destroyed after all players spawn
```

---

## Lobby Flow

1. **Create**: Host calls `LobbyManager.CreateLobby(map, difficulty, isPrivate)`
   - Transport binds on port 7980
   - Join code generated (6 chars, no confusing characters)

2. **Join**: Client calls `LobbyManager.JoinLobby(joinCode)`
   - Connects to host, sends `JoinRequest` with `PlayerIdentity`
   - Receives `JoinAccepted` with full lobby state

3. **Ready Up**: Players click Ready → `LobbyManager.SetReady(true)`

4. **Start Game**: Host clicks Start (requires all players ready)
   - `LobbyToGameTransition.BeginTransition()` called
   - Creates ECS worlds via `GameBootstrap`
   - Injects `LobbySpawnData` singleton
   - Players spawn at map-defined positions

5. **In Game**: Standard NetCode flow, lobby dormant

---

## PlayerIdentity Persistence

- **PlayerId**: GUID stored in `PlayerPrefs["DIG_PlayerId"]`
- Generated once on first launch, survives game restarts
- Used for reconnection matching and save file naming
- Access via `PlayerIdentity.Local`

---

## 16KB Archetype Impact

**Zero bytes** on player entity. `LobbySpawnData` is a transient singleton on a separate entity, destroyed after spawn. No ghost components added.

---

## Backward Compatibility

- `LobbySpawnData` is **optional** — all modified systems fall back to existing behavior when absent
- Direct-connect via `GameBootstrap.CreateHost()`/`CreateClient()` still works unchanged
- Existing Host/Join buttons continue to function without lobby

---

## Editor Tooling

**Menu**: DIG → Lobby Workstation

Three modules:
1. **Lobby Inspector**: Live lobby state in Play Mode (players, ready, ping, Relay)
2. **Map Registry**: Validates MapDefinitionSO assets (spawn count checks)
3. **Difficulty Registry**: Validates DifficultyDefinitionSO scaling ranges

---

## Relay (NAT Traversal)

Uses `com.unity.services.multiplayer` (already at 2.1.1 in manifest).

To enable Relay:
1. Set up Unity Gaming Services in Project Settings
2. Enable Relay service in the Unity Dashboard
3. The lobby transport automatically allocates Relay on lobby creation
4. Same Relay allocation is reused for NetCode game transport

**Note**: Relay methods are compiled only when `UNITY_SERVICES_MULTIPLAYER` is defined. For local testing, the system falls back to direct connection on port 7980.

---

## File Manifest

### Scripts (16 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/Lobby/LobbyState.cs` | State classes, PlayerIdentity, enums |
| `Assets/Scripts/Lobby/LobbyMessages.cs` | 15 message types + serializer |
| `Assets/Scripts/Lobby/LobbySpawnData.cs` | ECS singleton bridge |
| `Assets/Scripts/Lobby/Config/LobbyConfigSO.cs` | Lobby configuration SO |
| `Assets/Scripts/Lobby/Config/MapDefinitionSO.cs` | Map definition SO |
| `Assets/Scripts/Lobby/Config/DifficultyDefinitionSO.cs` | Difficulty definition SO |
| `Assets/Scripts/Lobby/LobbyNetworkTransport.cs` | Unity Transport wrapper |
| `Assets/Scripts/Lobby/LobbyManager.cs` | Core singleton manager |
| `Assets/Scripts/Lobby/LobbyDiscoveryService.cs` | Lobby browsing service |
| `Assets/Scripts/Lobby/LobbyToGameTransition.cs` | Lobby→game transition |
| `Assets/Scripts/Lobby/UI/LobbyUIManager.cs` | Panel switcher |
| `Assets/Scripts/Lobby/UI/LobbyBrowserPanel.cs` | Browse/join panel |
| `Assets/Scripts/Lobby/UI/LobbyRoomPanel.cs` | Lobby room panel |
| `Assets/Scripts/Lobby/UI/LobbyPlayerSlotUI.cs` | Player slot widget |
| `Assets/Scripts/Lobby/UI/LobbyListEntryUI.cs` | Browser list entry |
| `Assets/Scripts/Lobby/UI/TransitionLoadingPanel.cs` | Loading screen |

### Editor (3 files)
| File | Description |
|------|-------------|
| `Assets/Editor/LobbyWorkstation/ILobbyWorkstationModule.cs` | Module interface |
| `Assets/Editor/LobbyWorkstation/LobbyWorkstationWindow.cs` | Editor window |
| `Assets/Editor/LobbyWorkstation/Modules/LobbyInspectorModule.cs` | Inspector + validators |

### Modified (2 files)
| File | Change |
|------|--------|
| `Assets/Scripts/Systems/Network/GoInGameSystem.cs` | Read LobbySpawnData for spawn positions |
| `Assets/Scripts/Persistence/Systems/SaveIdAssignmentSystem.cs` | Read LobbySpawnData for persistent PlayerId |
