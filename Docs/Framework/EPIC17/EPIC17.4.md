# EPIC 17.4: Lobby & Matchmaking System

**Status:** PLANNED
**Priority:** High (Multiplayer Infrastructure Foundation)
**Dependencies:**
- `GoInGameRequest` IRpcCommand (existing -- `Assets/Scripts/Systems/Network/GoInGameSystem.cs`, client-to-server "ready to receive ghosts")
- `GoInGameClientSystem` ISystem (existing -- `Assets/Scripts/Systems/Network/GoInGameSystem.cs`, sends GoInGameRequest when NetworkId present, no NetworkStreamInGame)
- `GoInGameServerSystem` ISystem (existing -- `Assets/Scripts/Systems/Network/GoInGameSystem.cs`, receives GoInGameRequest, instantiates `PlayerSpawner.Player` prefab, sets GhostOwner.NetworkId)
- `PlayerSpawner` IComponentData singleton (existing -- loaded from subscene, holds ghost prefab reference for server-side player instantiation)
- `SetRpcSystemDynamicAssemblyListSystem` ISystem (existing -- `Assets/Scripts/Systems/Network/GoInGameSystem.cs`, enables dynamic RPC assembly list)
- `SaveIdAssignmentSystem` SystemBase (existing -- `Assets/Scripts/Persistence/Systems/SaveIdAssignmentSystem.cs`, populates PlayerSaveId from GhostOwner.NetworkId, server-only)
- `SaveStateLink` / `PlayerSaveId` (existing -- `Assets/Scripts/Persistence/Components/SaveStateComponents.cs`, 8 bytes on player, child entity pattern)
- `PlayerReconnectSaveSystem` (existing -- `Assets/Scripts/Persistence/Systems/PlayerReconnectSaveSystem.cs`, handles reconnection save state)
- `CollisionGracePeriod` IComponentData (existing -- added to spawned player for post-spawn collision immunity)
- Unity Transport Package (`com.unity.transport`) -- existing transport layer used by NetCode
- Unity Relay SDK (`com.unity.services.relay`) -- required package addition for NAT traversal

**Feature:** A pre-NetCode lobby system using MonoBehaviours and Unity Transport for player gathering, party formation, and match configuration before ECS world creation. Supports public lobby browsing, private join codes, quick match, and cooperative PvE (with PvP Arena reserved for EPIC 17.10). Uses Unity Relay for NAT traversal so no dedicated server is required during the lobby phase. On game start, the host transitions from lobby transport to NetCode ServerWorld creation, and all clients connect to the host's NetCode endpoint.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `GoInGameClientSystem` | `GoInGameSystem.cs` | Fully implemented | Sends GoInGameRequest when client has NetworkId but no NetworkStreamInGame |
| `GoInGameServerSystem` | `GoInGameSystem.cs` | Fully implemented | Spawns player prefab, sets GhostOwner, appends to LinkedEntityGroup |
| `PlayerSpawner` singleton | Subscene baked | Fully implemented | Holds `Player` ghost prefab Entity reference |
| `SaveIdAssignmentSystem` | `SaveIdAssignmentSystem.cs` | Fully implemented | Assigns `player_{NetworkId}` to SaveStateLink child. Server|Local only |
| `PlayerReconnectSaveSystem` | `PlayerReconnectSaveSystem.cs` | Fully implemented | Handles save state on reconnect |
| `CollisionGracePeriod` | `GoInGameServerSystem` line 107 | Applied at spawn | `SpawnDefault` prevents collision spam on fresh players |
| Unity Transport (UTP) | Package dependency | Installed | Used internally by NetCode, not wrapped for lobby use |
| Unity Relay SDK | **NOT INSTALLED** | Missing | Required for NAT traversal in peer-hosted lobbies |

### What's Missing

- **No lobby state management** -- no MonoBehaviour or data structure for lobby rooms, player slots, or readiness
- **No lobby transport** -- Unity Transport exists for NetCode but no standalone transport wrapper for pre-ECS lobby communication
- **No lobby discovery** -- no mechanism for browsing, filtering, or listing public lobbies
- **No join code system** -- no private lobby invite mechanism
- **No quick match** -- no automated lobby selection based on player preferences
- **No lobby-to-game transition** -- no orchestration from lobby phase to NetCode world creation and scene loading
- **No lobby UI** -- no browser panel, room panel, player slots, or ready-up interface
- **No map/difficulty selection** -- no ScriptableObjects for map definitions or difficulty presets
- **No reconnect from lobby** -- `PlayerReconnectSaveSystem` handles in-game reconnect but nothing restores lobby membership
- **No player identity persistence** -- no mechanism to remember PlayerId across sessions for reconnection

---

## Problem

DIG has a fully functional NetCode pipeline (`GoInGameServerSystem` spawns players, `SaveIdAssignmentSystem` assigns save IDs, ghost replication works) but there is no way for players to find each other before the game starts. The current flow assumes all clients hard-connect to a known server endpoint -- there is no lobby room, no ready-up, no map selection, and no NAT traversal for peer-hosted sessions. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `GoInGameServerSystem` spawns players on connect | No pre-game gathering phase -- players spawn immediately |
| `PlayerSpawner` singleton with ghost prefab | No map selection or difficulty choice before spawning |
| `SaveIdAssignmentSystem` assigns `player_{NetworkId}` | No persistent PlayerId across sessions for reconnect |
| Unity Transport package installed | No standalone transport wrapper for pre-ECS lobby communication |
| `PlayerReconnectSaveSystem` handles in-game reconnect | No lobby-phase reconnect or rejoin |
| `CollisionGracePeriod` on spawn | No staggered spawn positions based on lobby slot |

**The gap:** A player launches DIG and must manually enter an IP address to join a session. There is no lobby browser, no join code, no friend invite, and no way to configure the session before loading. Players cannot form parties, pick maps, or set difficulty. Without Unity Relay, NAT-blocked players cannot host for others.

---

## Architecture Overview

```
                    PRE-ECS LAYER (MonoBehaviour, runs in bootstrap scene)
  LobbyConfigSO          MapDefinitionSO[]         DifficultyDefinitionSO[]
  (MaxPlayers, timeout,   (MapId, DisplayName,      (DifficultyId, DisplayName,
   heartbeat interval)     ScenePath, Thumbnail)      EnemyScaling, LootBonus)
           |                      |                          |
           +---------- LobbyManager (MonoBehaviour singleton) ----------+
           |           +-- CreateLobby()                                |
           |           +-- JoinLobby(joinCode) / JoinLobbyById(id)     |
           |           +-- LeaveLobby()                                 |
           |           +-- SetReady(bool)                               |
           |           +-- StartGame() [host only]                      |
           |           +-- KickPlayer(playerId) [host only]             |
           |                      |                                     |
           |           LobbyNetworkTransport                            |
           |           (wraps Unity Transport for lobby messages,       |
           |            Unity Relay allocation for NAT traversal)       |
           |                      |                                     |
           |           LobbyState (in-memory)                           |
           |           +-- HostPlayerId, MapId, Difficulty              |
           |           +-- MaxPlayers, GameMode, IsPrivate              |
           |           +-- LobbyPlayerSlot[] (per-player data)          |
           |           +-- JoinCode (6-char alphanumeric)               |
           |                      |                                     |
  LobbyDiscoveryService          |          LobbyJoinCodeService        |
  (queries available lobbies,    |          (generates/validates        |
   filters by map/difficulty/    |           6-char join codes,         |
   player count, pagination)     |           maps code -> Relay        |
                                 |           allocation)                |
                                 |                                     |
                    TRANSITION LAYER                                    |
                                 |                                     |
           LobbyToGameTransition                                       |
           +-- All players ready + host clicks Start                   |
           +-- Host: create NetCode ServerWorld + ClientWorld           |
           +-- Host: configure transport with Relay host allocation    |
           +-- Clients: create NetCode ClientWorld                     |
           +-- Clients: connect to host via Relay join allocation      |
           +-- Scene load via NetworkStreamRequestListen / Connect     |
           +-- GoInGameClientSystem / GoInGameServerSystem take over   |
                                 |                                     |
                    ECS LAYER (existing, unchanged)                     |
                                 |                                     |
           GoInGameServerSystem: spawns PlayerSpawner.Player            |
           SaveIdAssignmentSystem: assigns PlayerSaveId                 |
           (all existing systems function as-is)                        |
```

### Data Flow (Create Lobby -> Play Game)

```
Phase 1: Lobby Creation (Host)
  1. Player clicks "Create Game" in main menu
  2. LobbyUIManager -> LobbyManager.CreateLobby(config)
  3. LobbyManager -> LobbyNetworkTransport.AllocateRelay(maxPlayers)
     - Unity Relay SDK: Allocations.CreateAllocation() -> AllocationId + JoinCode
     - LobbyNetworkTransport: bind Unity Transport to Relay host endpoint
  4. LobbyManager creates LobbyState:
     - HostPlayerId = local PlayerIdentity.PlayerId
     - MapId = selected map
     - Difficulty = selected difficulty
     - JoinCode = Relay-provided 6-char code
     - LobbyPlayerSlot[0] = host player
  5. If public: LobbyDiscoveryService.RegisterLobby(lobbyState) -> broadcast
  6. LobbyRoomPanel activates, showing host in slot 0

Phase 2: Lobby Join (Client)
  7a. Public: LobbyBrowserPanel -> LobbyDiscoveryService.QueryLobbies(filter)
      - Returns LobbyInfo[] (id, host name, map, difficulty, playerCount, maxPlayers)
      - Player selects lobby -> LobbyManager.JoinLobbyById(lobbyId)
  7b. Private: Player enters 6-char code -> LobbyManager.JoinLobby(joinCode)
  7c. Quick Match: LobbyManager.QuickMatch(preferences)
      - LobbyDiscoveryService.QueryLobbies(preferences) -> pick best match
      - If none found: auto-create new lobby
  8. LobbyNetworkTransport.JoinRelay(joinCode)
     - Unity Relay SDK: Allocations.JoinAllocation(joinCode) -> AllocationId
     - Connect Unity Transport to Relay join endpoint
  9. Client sends LobbyJoinMessage to host transport
  10. Host validates (not full, not banned) -> adds LobbyPlayerSlot
  11. Host broadcasts updated LobbyState to all clients
  12. All clients' LobbyRoomPanel updates player list

Phase 3: Ready Up
  13. Each player clicks "Ready" -> LobbyManager.SetReady(true)
  14. Client sends LobbyReadyMessage to host
  15. Host updates LobbyPlayerSlot.IsReady, broadcasts state
  16. LobbyRoomPanel shows green checkmarks for ready players
  17. Host sees "Start Game" button enable when ALL non-host players ready

Phase 4: Game Start Transition
  18. Host clicks "Start Game" -> LobbyManager.StartGame()
  19. LobbyToGameTransition.BeginTransition(lobbyState):
      a. Host: broadcasts LobbyStartGameMessage to all clients
      b. Host: LobbyNetworkTransport.Shutdown() (lobby transport)
      c. Host: creates NetCode ServerWorld + ClientWorld
      d. Host: configures ServerWorld transport with Relay host allocation
      e. Host: ServerWorld starts listening (NetworkStreamRequestListen)
      f. Clients: receive LobbyStartGameMessage
      g. Clients: LobbyNetworkTransport.Shutdown() (lobby transport)
      h. Clients: create NetCode ClientWorld
      i. Clients: configure ClientWorld transport with Relay join allocation
      j. Clients: ClientWorld connects (NetworkStreamRequestConnect)
  20. Loading screen shown during world creation + scene load
  21. GoInGameClientSystem detects NetworkId -> sends GoInGameRequest
  22. GoInGameServerSystem spawns player, sets GhostOwner.NetworkId
  23. SaveIdAssignmentSystem assigns PlayerSaveId
  24. All existing systems take over -- lobby code dormant

Phase 5: Return to Lobby (Post-Game)
  25. Game ends or host returns to menu
  26. LobbyToGameTransition.ReturnToLobby():
      a. Destroy NetCode worlds (ServerWorld + ClientWorld)
      b. Re-initialize LobbyNetworkTransport
      c. Restore LobbyState from cached data
      d. Reconnect lobby transport via Relay
      e. LobbyRoomPanel reactivates with preserved player list
```

### Critical Design: Why MonoBehaviour, Not ECS

The lobby runs BEFORE ECS worlds exist. NetCode's `ServerWorld` and `ClientWorld` are created only when the host starts the game. This means:
- **No ECS entities** during lobby phase -- `SystemAPI`, `EntityManager`, `IComponentData` are unavailable
- **No ghost replication** -- lobby state is replicated via raw Unity Transport messages, not NetCode snapshots
- **No prediction** -- lobby state is authoritative on the host, clients receive full state broadcasts
- **MonoBehaviour is correct** -- `LobbyManager` is a `DontDestroyOnLoad` singleton that survives scene transitions
- **Unity Transport is reusable** -- the same transport driver allocated for lobby can transfer its Relay allocation to NetCode on game start

This is the same pattern used by Unity's official Multiplayer Samples (Boss Room): lobby is MonoBehaviour-based, ECS/NetCode activates on game start.

---

## Data Structures

### LobbyState

**File:** `Assets/Scripts/Lobby/LobbyState.cs`

```
[Serializable]
LobbyState
  LobbyId          : string         // Unique lobby identifier (GUID)
  HostPlayerId     : string         // PlayerId of the host
  MapId            : int            // Index into MapDefinitionSO registry
  DifficultyId     : int            // Index into DifficultyDefinitionSO registry
  MaxPlayers       : int            // 1-4 (from LobbyConfigSO)
  GameMode         : GameMode       // Cooperative(0), PvPArena(1)
  IsPrivate        : bool           // If true, not listed in browser
  JoinCode         : string         // 6-char alphanumeric (from Relay or generated)
  CreatedAtUtc     : long           // Unix timestamp
  Players          : List<LobbyPlayerSlot>
```

### LobbyPlayerSlot

**File:** `Assets/Scripts/Lobby/LobbyState.cs`

```
[Serializable]
LobbyPlayerSlot
  PlayerId         : string         // Persistent player identity
  DisplayName      : string         // Player-chosen display name (max 24 chars)
  Level            : int            // Character level (from save data, for display)
  ClassId          : int            // Character class/archetype (for display icon)
  IsReady          : bool           // Ready-up status
  IsHost           : bool           // True for lobby creator
  SlotIndex        : int            // 0-based position in lobby (determines spawn order)
  ConnectionId     : int            // Transport connection ID (-1 for local host)
  PingMs           : int            // Last measured round-trip time
```

### GameMode

**File:** `Assets/Scripts/Lobby/LobbyState.cs`

```
enum GameMode : byte
  Cooperative = 0    // PvE co-op (default, EPIC 17.4)
  PvPArena    = 1    // Competitive PvP (EPIC 17.10, not implemented here)
```

### PlayerIdentity

**File:** `Assets/Scripts/Lobby/LobbyState.cs`

```
[Serializable]
PlayerIdentity
  PlayerId         : string         // Persisted in PlayerPrefs ("DIG_PlayerId"), GUID if first launch
  DisplayName      : string         // Persisted in PlayerPrefs ("DIG_DisplayName")
  LastLevel        : int            // Cached from most recent save (for lobby display)
  LastClassId      : int            // Cached from most recent save
```

Loaded at application startup by `PlayerIdentityProvider`. If `PlayerPrefs` has no `DIG_PlayerId`, a new GUID is generated and saved. This ensures reconnection works across sessions without requiring an account system.

### LobbyInfo (Discovery Query Result)

**File:** `Assets/Scripts/Lobby/LobbyDiscoveryService.cs`

```
LobbyInfo
  LobbyId          : string
  HostDisplayName  : string
  MapId            : int
  MapDisplayName   : string
  DifficultyId     : int
  DifficultyName   : string
  CurrentPlayers   : int
  MaxPlayers       : int
  GameMode         : GameMode
  PingMs           : int            // Estimated ping to host
  CreatedAtUtc     : long
```

### Lobby Messages (Transport Protocol)

**File:** `Assets/Scripts/Lobby/LobbyNetworkTransport.cs`

All lobby messages are serialized as: `[MessageType:byte][PayloadLength:ushort][Payload:bytes]`

```
LobbyMessageType : byte
  JoinRequest       = 1    // Client -> Host: PlayerIdentity
  JoinAccepted      = 2    // Host -> Client: full LobbyState
  JoinDenied        = 3    // Host -> Client: DenyReason (byte)
  StateUpdate       = 4    // Host -> All: full LobbyState (broadcast on any change)
  ReadyChanged      = 5    // Client -> Host: bool isReady
  ChatMessage       = 6    // Client -> Host -> All: string message
  KickPlayer        = 7    // Host -> Target: DenyReason
  StartGame         = 8    // Host -> All: GameStartPayload (Relay allocation data)
  Heartbeat         = 9    // Bidirectional: keepalive
  LeaveNotify       = 10   // Client -> Host: graceful disconnect
  DiscoveryQuery    = 11   // Client -> Broadcast: LobbyFilter
  DiscoveryResponse = 12   // Host -> Client: LobbyInfo
  ReturnToLobby     = 13   // Host -> All: game ended, restore lobby
  PingRequest       = 14   // Bidirectional: timestamp for RTT measurement
  PingResponse      = 15   // Bidirectional: echo timestamp

DenyReason : byte
  LobbyFull         = 0
  Kicked             = 1
  LobbyClosing       = 2
  InvalidCode        = 3
  VersionMismatch    = 4
  Banned             = 5
```

### GameStartPayload

**File:** `Assets/Scripts/Lobby/LobbyToGameTransition.cs`

```
GameStartPayload
  RelayHostAllocation   : byte[]   // Serialized RelayServerData for ServerWorld transport
  RelayJoinAllocation   : byte[]   // Serialized RelayServerData for ClientWorld transport
  MapId                 : int      // Confirmed map (host-authoritative)
  DifficultyId          : int      // Confirmed difficulty
  GameMode              : byte     // Confirmed game mode
  PlayerSlotAssignments : LobbyPlayerSlot[]  // Final slot order (determines spawn positions)
  RandomSeed            : uint     // Shared seed for deterministic world generation
```

---

## ScriptableObjects

### LobbyConfigSO

**File:** `Assets/Scripts/Lobby/Config/LobbyConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Lobby/Lobby Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| MaxPlayersPerLobby | int [1-8] | 4 | Maximum players in one lobby |
| DefaultMaxPlayers | int | 4 | Pre-filled max when creating lobby |
| HeartbeatIntervalMs | int | 2000 | Keepalive message interval |
| HeartbeatTimeoutMs | int | 8000 | Disconnect if no heartbeat received |
| LobbyTimeoutMinutes | float | 30 | Auto-close empty lobby after this duration |
| JoinCodeLength | int | 6 | Length of private join code |
| MaxDisplayNameLength | int | 24 | Character limit for player names |
| MaxChatMessageLength | int | 128 | Character limit for lobby chat |
| DiscoveryRefreshIntervalMs | int | 3000 | How often lobby browser polls for updates |
| DiscoveryMaxResults | int | 50 | Max lobbies returned per query |
| QuickMatchTimeoutSeconds | float | 10 | Time before quick match creates a new lobby |
| TransitionTimeoutSeconds | float | 15 | Max time for lobby-to-game transition before abort |
| AllowMidGameJoin | bool | false | Whether players can join after game starts (future) |
| MinPlayersToStart | int | 1 | Minimum players before host can start (1 = solo allowed) |
| DefaultGameMode | GameMode | Cooperative | Pre-selected game mode |
| ShowPingInBrowser | bool | true | Display ping column in lobby browser |
| RelayRegion | string | "" | Preferred Relay region (empty = auto-select closest) |

### MapDefinitionSO

**File:** `Assets/Scripts/Lobby/Config/MapDefinitionSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Lobby/Map Definition")]
```

| Field | Type | Purpose |
|-------|------|---------|
| MapId | int | Stable numeric identifier (must not change across versions) |
| DisplayName | string | Shown in lobby UI ("Forsaken Mines", "Crimson Wastes") |
| Description | string | Multi-line description for lobby details panel |
| Thumbnail | Sprite | Preview image shown in map selection and lobby browser |
| ScenePath | string | Addressable scene path for NetworkSceneManager |
| SubscenePaths | string[] | Additional subscene paths to load (enemy spawns, environment) |
| MinPlayers | int | Minimum players recommended (informational) |
| MaxPlayers | int | Hard cap for this map (overrides LobbyConfig if lower) |
| EstimatedMinutes | int | Estimated play time (shown in browser) |
| SupportedGameModes | GameMode[] | Which game modes this map supports |
| SpawnPositions | Vector3[] | Per-slot spawn positions (index = SlotIndex from LobbyPlayerSlot) |
| SpawnRotations | Quaternion[] | Per-slot spawn rotations |
| UnlockRequirement | int | Minimum player level to select this map (0 = always available) |

### DifficultyDefinitionSO

**File:** `Assets/Scripts/Lobby/Config/DifficultyDefinitionSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Lobby/Difficulty Definition")]
```

| Field | Type | Purpose |
|-------|------|---------|
| DifficultyId | int | Stable numeric identifier |
| DisplayName | string | Shown in UI ("Normal", "Hard", "Nightmare") |
| Description | string | Tooltip description of modifiers |
| Icon | Sprite | Difficulty icon/badge |
| EnemyHealthScale | float | Multiplier on enemy Health.Max (1.0 = normal) |
| EnemyDamageScale | float | Multiplier on enemy AttackStats (1.0 = normal) |
| EnemySpawnRateScale | float | Multiplier on spawn frequency (1.0 = normal) |
| LootQuantityScale | float | Multiplier on loot drop count (1.0 = normal) |
| LootQualityBonus | float | Added to loot quality roll (0.0 = normal) |
| XPMultiplier | float | Multiplier on XP awards (1.0 = normal) |
| CurrencyMultiplier | float | Multiplier on currency drops (1.0 = normal) |
| UnlockRequirement | int | Minimum player level to select (0 = always available) |
| Color | Color | UI accent color for difficulty badge |

---

## Core Systems

### LobbyManager

**File:** `Assets/Scripts/Lobby/LobbyManager.cs`

MonoBehaviour singleton (`DontDestroyOnLoad`). Central orchestrator for all lobby operations.

```
LobbyManager : MonoBehaviour
  // Singleton
  static Instance : LobbyManager

  // Configuration
  [SerializeField] LobbyConfigSO Config
  [SerializeField] MapDefinitionSO[] Maps
  [SerializeField] DifficultyDefinitionSO[] Difficulties

  // Runtime state
  CurrentLobby     : LobbyState         // null when not in a lobby
  LocalIdentity    : PlayerIdentity     // loaded from PlayerPrefs on Awake
  IsHost           : bool               // true if we created the lobby
  ConnectionState  : LobbyConnectionState  // Disconnected, Connecting, InLobby, Transitioning, InGame

  // Events (C# events for UI binding)
  event Action<LobbyState> OnLobbyStateChanged
  event Action<LobbyPlayerSlot> OnPlayerJoined
  event Action<LobbyPlayerSlot> OnPlayerLeft
  event Action<string> OnChatMessageReceived
  event Action<DenyReason> OnJoinDenied
  event Action OnGameStarting
  event Action<string> OnError

  // Host operations
  CreateLobby(mapId, difficultyId, maxPlayers, isPrivate, gameMode) : async Task<bool>
  StartGame() : async Task<bool>
  KickPlayer(playerId) : void
  SetMap(mapId) : void
  SetDifficulty(difficultyId) : void

  // Client operations
  JoinLobby(joinCode) : async Task<bool>
  JoinLobbyById(lobbyId) : async Task<bool>
  QuickMatch(preferences) : async Task<bool>
  LeaveLobby() : void
  SetReady(isReady) : void
  SendChatMessage(message) : void

  // Internal
  Update() : void  // pumps transport, checks heartbeats, handles timeouts
```

**Lifecycle:**
1. `Awake()`: Load `PlayerIdentity` from `PlayerPrefs`. Generate GUID if first launch.
2. `CreateLobby()`: Allocate Unity Relay -> bind transport -> create `LobbyState` -> register for discovery if public.
3. `JoinLobby()`: Resolve join code -> join Relay allocation -> connect transport -> send `JoinRequest`.
4. `Update()`: Pump transport events (`DataReceived`, `Connected`, `Disconnected`). Process lobby messages. Send heartbeats.
5. `StartGame()`: Validate all ready -> broadcast `StartGame` message -> invoke `LobbyToGameTransition`.
6. `OnDestroy()`: Graceful disconnect, release Relay allocation.

### LobbyConnectionState

**File:** `Assets/Scripts/Lobby/LobbyState.cs`

```
enum LobbyConnectionState : byte
  Disconnected   = 0   // Not in any lobby
  Connecting     = 1   // Transport connecting to host
  InLobby        = 2   // Connected, lobby phase active
  Transitioning  = 3   // Lobby -> Game world creation in progress
  InGame         = 4   // NetCode worlds active, lobby dormant
  Reconnecting   = 5   // Attempting to rejoin after disconnect
```

### LobbyNetworkTransport

**File:** `Assets/Scripts/Lobby/LobbyNetworkTransport.cs`

Wraps Unity Transport (`NetworkDriver`) for lobby-phase communication. Handles Relay allocation, connection management, and message serialization.

```
LobbyNetworkTransport
  // Relay
  AllocateRelay(maxPlayers) : async Task<(string joinCode, RelayServerData hostData)>
  JoinRelay(joinCode) : async Task<RelayServerData>
  GetRelayAllocationData() : RelayServerData   // cached, for handoff to NetCode

  // Transport
  Initialize(isHost) : void
  Shutdown() : void
  Send(connectionId, messageType, payload) : void
  Broadcast(messageType, payload) : void     // host -> all clients
  PumpEvents() : LobbyTransportEvent[]      // called each frame from LobbyManager.Update

  // Connection tracking
  Connections : Dictionary<int, LobbyPlayerSlot>
  IsListening : bool

  // Serialization helpers
  static SerializeLobbyState(LobbyState) : byte[]
  static DeserializeLobbyState(byte[]) : LobbyState
  static SerializePlayerIdentity(PlayerIdentity) : byte[]
  static DeserializePlayerIdentity(byte[]) : PlayerIdentity
```

**Key design:** The `RelayServerData` obtained during lobby allocation is cached and later passed to `NetworkStreamReceiveSystem` (host) or `NetworkStreamReceiveSystem` (client) during game start. This avoids a second Relay allocation -- the same Relay session bridges lobby and game.

### LobbyDiscoveryService

**File:** `Assets/Scripts/Lobby/LobbyDiscoveryService.cs`

Handles lobby browsing for public lobbies. Uses a lightweight broadcast/response protocol over Unity Transport.

```
LobbyDiscoveryService
  // Query
  QueryLobbies(filter) : async Task<LobbyInfo[]>
  RefreshLobbies() : async Task<LobbyInfo[]>   // re-query with last filter
  CancelQuery() : void

  // Filter
  LobbyFilter
    MapId          : int?          // null = any map
    DifficultyId   : int?          // null = any difficulty
    GameMode       : GameMode?     // null = any mode
    HasOpenSlots   : bool          // true = only show lobbies with room
    MaxPing        : int?          // null = no ping filter
    SearchText     : string        // partial match on host name or lobby id

  // Registration (host only)
  RegisterLobby(lobbyState) : void
  UnregisterLobby() : void
  UpdateRegistration(lobbyState) : void

  // Events
  event Action<LobbyInfo[]> OnLobbiesReceived
```

**Implementation strategy:** For initial implementation, discovery uses the same Relay-based transport with a well-known "discovery relay" join code that all hosts register with. Hosts periodically broadcast their `LobbyInfo` to the discovery relay. Querying clients receive all broadcasts and filter locally. This avoids requiring a dedicated matchmaking server.

**Future (EPIC 17.10+):** Replace with Unity Lobby Service or a dedicated matchmaking backend for scalability beyond ~100 concurrent lobbies.

### LobbyJoinCodeService

**File:** `Assets/Scripts/Lobby/LobbyJoinCodeService.cs`

Generates and validates join codes for private lobbies.

```
LobbyJoinCodeService
  GenerateJoinCode(relayJoinCode) : string
    // Uses Relay's own join code (already 6-char alphanumeric)
    // If Relay unavailable: generates random 6-char from [A-Z0-9], excluding ambiguous (0/O, 1/I/L)

  ValidateJoinCode(code) : bool
    // Length check, character set check, checksum (last char)

  static AllowedChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"  // 30 chars, no ambiguous
```

### LobbyToGameTransition

**File:** `Assets/Scripts/Lobby/LobbyToGameTransition.cs`

Orchestrates the transition from lobby MonoBehaviour world to NetCode ECS world. This is the most critical class -- it bridges the two architectures.

```
LobbyToGameTransition
  // Transition
  BeginTransition(lobbyState) : async Task<bool>
  AbortTransition(reason) : void
  ReturnToLobby() : async Task<bool>

  // State
  TransitionPhase : TransitionPhase enum
    Idle             = 0
    BroadcastingStart= 1   // Host: sending StartGame to clients
    CreatingWorlds   = 2   // Creating ServerWorld/ClientWorld
    ConfiguringTransport = 3  // Setting Relay data on NetCode transport
    LoadingScene     = 4   // Streaming scene + subscenes
    WaitingForPlayers= 5   // Waiting for all clients to connect + GoInGame
    Complete         = 6   // All players in game
    Aborting         = 7   // Transition failed, rolling back

  // Events
  event Action<TransitionPhase> OnPhaseChanged
  event Action<float> OnProgressUpdated       // 0.0 - 1.0 for loading bar
  event Action<string> OnTransitionFailed
```

**Host-side transition sequence:**
```
1. BroadcastingStart:
   - Serialize GameStartPayload (Relay data, map, difficulty, slots)
   - Send LobbyMessageType.StartGame to all clients
   - Wait for ack from all clients (timeout: TransitionTimeoutSeconds)

2. CreatingWorlds:
   - Call ClientServerBootstrap.CreateServerWorld("ServerWorld")
   - Call ClientServerBootstrap.CreateClientWorld("ClientWorld")
   - Wait for worlds to initialize

3. ConfiguringTransport:
   - Get NetworkStreamReceiveSystem from ServerWorld
   - Set RelayServerData on server transport driver
   - Call NetworkStreamRequestListen on server
   - Get NetworkStreamReceiveSystem from ClientWorld
   - Set RelayServerData on client transport driver
   - Call NetworkStreamRequestConnect on client (loopback to own server)

4. LoadingScene:
   - Load map scene via NetworkSceneManager
   - Load subscenes (enemy spawns, environment chunks)

5. WaitingForPlayers:
   - Monitor NetworkStreamConnection entities in ServerWorld
   - Wait until count == lobby player count
   - GoInGameServerSystem handles player spawning automatically
   - Apply per-slot spawn positions from MapDefinitionSO.SpawnPositions

6. Complete:
   - Hide loading screen
   - LobbyManager.ConnectionState = InGame
```

**Client-side transition sequence:**
```
1. Receive LobbyMessageType.StartGame with GameStartPayload
2. Send ack to host
3. CreatingWorlds: Call ClientServerBootstrap.CreateClientWorld("ClientWorld")
4. ConfiguringTransport:
   - Deserialize RelayServerData from GameStartPayload
   - Set on client transport driver
   - Call NetworkStreamRequestConnect
5. LoadingScene: Load map scene (same ScenePath from payload)
6. WaitingForPlayers: Wait for NetworkStreamInGame
7. Complete: GoInGameClientSystem sends GoInGameRequest automatically
```

### PlayerIdentityProvider

**File:** `Assets/Scripts/Lobby/LobbyManager.cs` (inner class or same file)

```
static PlayerIdentityProvider
  static Load() : PlayerIdentity
    PlayerId = PlayerPrefs.GetString("DIG_PlayerId", "")
    if empty: PlayerId = Guid.NewGuid().ToString("N")[..16]
              PlayerPrefs.SetString("DIG_PlayerId", PlayerId)
    DisplayName = PlayerPrefs.GetString("DIG_DisplayName", "Player")
    LastLevel = PlayerPrefs.GetInt("DIG_LastLevel", 1)
    LastClassId = PlayerPrefs.GetInt("DIG_LastClassId", 0)

  static Save(identity) : void
    PlayerPrefs.SetString("DIG_PlayerId", identity.PlayerId)
    PlayerPrefs.SetString("DIG_DisplayName", identity.DisplayName)
    PlayerPrefs.SetInt("DIG_LastLevel", identity.LastLevel)
    PlayerPrefs.SetInt("DIG_LastClassId", identity.LastClassId)
    PlayerPrefs.Save()
```

---

## System Flow Diagrams

### Create & Join Flow

```
 HOST                              RELAY SERVICE                         CLIENT
  |                                     |                                   |
  |-- AllocateRelay(maxPlayers) ------->|                                   |
  |<---- AllocationId + JoinCode -------|                                   |
  |                                     |                                   |
  |-- Bind transport to Relay --------->|                                   |
  |-- RegisterLobby() (if public) ----->|                                   |
  |                                     |                                   |
  |  [UI: Show lobby room, slot 0]      |     [UI: Show lobby browser or    |
  |                                     |           enter join code]        |
  |                                     |                                   |
  |                                     |<---- JoinRelay(joinCode) ---------|
  |                                     |---- AllocationId --------------->|
  |                                     |                                   |
  |<---------- Transport Connect -------|------------ Transport Connect --->|
  |                                     |                                   |
  |<---------- JoinRequest (PlayerIdentity) --------------------------------|
  |                                     |                                   |
  |-- Validate (not full, not banned)   |                                   |
  |-- Add LobbyPlayerSlot              |                                   |
  |                                     |                                   |
  |---------- JoinAccepted (LobbyState) ---------------------------------->|
  |---------- StateUpdate (broadcast) --------- to all clients ----------->|
  |                                     |                                   |
  |  [UI: New player in slot]           |     [UI: LobbyRoomPanel shows]   |
```

### Ready & Start Flow

```
 HOST                              CLIENT A                          CLIENT B
  |                                   |                                   |
  |<----- ReadyChanged(true) ---------|                                   |
  |-- Update slot A IsReady=true      |                                   |
  |-- Broadcast StateUpdate --------->|                                   |
  |------- StateUpdate --------------------------------------------->|
  |                                   |                                   |
  |                                   |<----- ReadyChanged(true) ---------|
  |<------- ReadyChanged(true) -------|-------- (forwarded) ------------->|
  |-- Update slot B IsReady=true      |                                   |
  |-- Broadcast StateUpdate --------->|                                   |
  |------- StateUpdate --------------------------------------------->|
  |                                   |                                   |
  | [All ready: "Start Game" enabled] |  [UI: All checkmarks green]       |
  |                                   |                                   |
  |== Host clicks "Start Game" =====================================>|
  |                                   |                                   |
  |-- Broadcast StartGame(payload) -->|                                   |
  |------- StartGame(payload) ------------------------------------------------>|
  |                                   |                                   |
  |<---------- Ack -------------------|                                   |
  |<---------- Ack --------------------------------------------------|
  |                                   |                                   |
  | [Create ServerWorld+ClientWorld]  | [Create ClientWorld]              | [Create ClientWorld]
  | [Configure Relay on Server]       | [Configure Relay on Client]       | [Configure Relay on Client]
  | [Listen]                          | [Connect]                         | [Connect]
  |                                   |                                   |
  | GoInGameServerSystem spawns all   | GoInGameClientSystem sends RPC    | GoInGameClientSystem sends RPC
  |                                   |                                   |
  | ============== GAME RUNNING (ECS/NetCode) ========================== |
```

### Disconnect & Reconnect Flow

```
 HOST                              CLIENT (disconnected)
  |                                   |
  | [Heartbeat timeout detected]      | [Transport disconnect detected]
  |                                   |
  |-- Remove LobbyPlayerSlot          |-- ConnectionState = Reconnecting
  |-- Broadcast StateUpdate           |-- Cache lobby JoinCode
  |                                   |
  |  [UI: Player slot grayed out]     |  [UI: "Reconnecting..." overlay]
  |  [Keep slot reserved for          |
  |   ReconnectWindowSeconds]         |
  |                                   |
  |                                   |-- Retry JoinRelay(cachedJoinCode)
  |                                   |-- Send JoinRequest with same PlayerId
  |                                   |
  |<----- JoinRequest (same PlayerId) |
  |-- Match by PlayerId               |
  |-- Restore to original slot        |
  |-- Broadcast StateUpdate --------->|
  |                                   |
  |  [UI: Player slot restored]       |  [UI: Back in lobby]
```

---

## UI Bridge

### LobbyUIManager

**File:** `Assets/Scripts/Lobby/UI/LobbyUIManager.cs`

MonoBehaviour that manages lobby UI panels. Unlike ECS systems, this uses standard Unity UI binding since there is no ECS world during lobby phase.

```
LobbyUIManager : MonoBehaviour
  [SerializeField] LobbyBrowserPanel BrowserPanel
  [SerializeField] LobbyRoomPanel RoomPanel
  [SerializeField] LobbyCreatePanel CreatePanel
  [SerializeField] TransitionLoadingPanel TransitionPanel
  [SerializeField] QuickMatchPanel QuickMatchPanel

  // Panel switching
  ShowBrowser() : void
  ShowCreateLobby() : void
  ShowRoom(LobbyState) : void
  ShowTransition() : void
  ShowQuickMatch() : void
  ReturnToMainMenu() : void

  // Event handlers (bound to LobbyManager events)
  OnLobbyStateChanged(LobbyState) : void
  OnPlayerJoined(LobbyPlayerSlot) : void
  OnPlayerLeft(LobbyPlayerSlot) : void
  OnJoinDenied(DenyReason) : void
  OnGameStarting() : void
  OnError(string) : void
```

### LobbyBrowserPanel

**File:** `Assets/Scripts/Lobby/UI/LobbyBrowserPanel.cs`

Displays available public lobbies with filtering, sorting, and pagination.

```
LobbyBrowserPanel : MonoBehaviour
  // UI references
  [SerializeField] Transform LobbyListContent       // ScrollView content parent
  [SerializeField] LobbyListEntryUI EntryPrefab     // Pooled list entry
  [SerializeField] TMP_InputField SearchField
  [SerializeField] TMP_Dropdown MapFilterDropdown
  [SerializeField] TMP_Dropdown DifficultyFilterDropdown
  [SerializeField] Toggle ShowFullLobbiesToggle
  [SerializeField] Button RefreshButton
  [SerializeField] Button JoinCodeButton
  [SerializeField] TMP_InputField JoinCodeField
  [SerializeField] Button CreateLobbyButton
  [SerializeField] Button QuickMatchButton
  [SerializeField] TextMeshProUGUI StatusText        // "Found 12 lobbies" / "Searching..."

  // Sorting
  SortColumn : LobbyBrowserSortColumn enum (HostName, Map, Difficulty, Players, Ping)
  SortAscending : bool

  // Data
  CachedLobbies : List<LobbyInfo>
  FilteredLobbies : List<LobbyInfo>

  // Methods
  Refresh() : void                    // Query LobbyDiscoveryService
  ApplyFilter() : void               // Filter + sort CachedLobbies -> FilteredLobbies
  OnEntryClicked(LobbyInfo) : void   // Join selected lobby
  OnJoinCodeSubmit() : void          // Join by code
  OnCreateClicked() : void           // Switch to create panel
  OnQuickMatchClicked() : void       // Invoke LobbyManager.QuickMatch
```

**Layout:**
```
+---------------------------------------------------------------+
| LOBBY BROWSER                                    [Create Game] |
+---------------------------------------------------------------+
| Search: [____________]  Map: [Any v]  Difficulty: [Any v]     |
| [x] Hide Full Lobbies                          [Quick Match]  |
+---------------------------------------------------------------+
| Host          | Map             | Diff   | Players | Ping     |
|---------------|-----------------|--------|---------|----------|
| PlayerOne     | Forsaken Mines  | Normal | 2/4     | 45ms     |
| xDarkLord     | Crimson Wastes  | Hard   | 1/4     | 78ms     |
| CoolPlayer99  | Frozen Depths   | Normal | 3/4     | 23ms     |
|               |                 |        |         |          |
+---------------------------------------------------------------+
| Found 3 lobbies                              [Refresh] [Join] |
+---------------------------------------------------------------+
| Join Code: [______] [Join]                                     |
+---------------------------------------------------------------+
```

### LobbyCreatePanel

**File:** `Assets/Scripts/Lobby/UI/LobbyBrowserPanel.cs` (inner panel or separate file)

```
LobbyCreatePanel : MonoBehaviour
  [SerializeField] TMP_Dropdown MapDropdown
  [SerializeField] TMP_Dropdown DifficultyDropdown
  [SerializeField] TMP_Dropdown MaxPlayersDropdown
  [SerializeField] TMP_Dropdown GameModeDropdown
  [SerializeField] Toggle PrivateLobbyToggle
  [SerializeField] Image MapThumbnail
  [SerializeField] TextMeshProUGUI MapDescription
  [SerializeField] TextMeshProUGUI DifficultyDescription
  [SerializeField] Button CreateButton
  [SerializeField] Button BackButton
```

### LobbyRoomPanel

**File:** `Assets/Scripts/Lobby/UI/LobbyRoomPanel.cs`

The main lobby room view showing all players, their status, and game settings.

```
LobbyRoomPanel : MonoBehaviour
  [SerializeField] LobbyPlayerSlotUI[] PlayerSlots    // Fixed array, size = MaxPlayersPerLobby
  [SerializeField] TextMeshProUGUI MapNameText
  [SerializeField] Image MapThumbnailImage
  [SerializeField] TextMeshProUGUI DifficultyText
  [SerializeField] Image DifficultyIcon
  [SerializeField] TextMeshProUGUI JoinCodeText
  [SerializeField] Button CopyCodeButton
  [SerializeField] Button ReadyButton
  [SerializeField] TextMeshProUGUI ReadyButtonText     // "Ready" / "Not Ready"
  [SerializeField] Button StartGameButton              // Host only, enabled when all ready
  [SerializeField] Button LeaveButton
  [SerializeField] ScrollRect ChatScrollRect
  [SerializeField] TextMeshProUGUI ChatContent
  [SerializeField] TMP_InputField ChatInput
  [SerializeField] Button SendChatButton

  // Host-only controls
  [SerializeField] GameObject HostSettingsPanel
  [SerializeField] TMP_Dropdown MapChangeDropdown      // Host can change map
  [SerializeField] TMP_Dropdown DifficultyChangeDropdown

  // Methods
  UpdateFromState(LobbyState) : void
  SetHostMode(bool) : void        // Show/hide host controls
  OnReadyClicked() : void
  OnStartClicked() : void
  OnLeaveClicked() : void
  OnCopyCodeClicked() : void      // Copy join code to clipboard
  OnChatSend() : void
  AppendChatMessage(playerName, message) : void
```

**Layout:**
```
+---------------------------------------------------------------+
| LOBBY ROOM                          Join Code: ABC123 [Copy]  |
+---------------------------------------------------------------+
| Map: Forsaken Mines  [Change v]    Difficulty: Hard [Change v] |
| [Map Thumbnail]                    Est. Time: ~20 min         |
+---------------------------------------------------------------+
| PLAYERS (3/4)                                                  |
| +-----------------------------------------------------------+ |
| | [Crown] PlayerOne    Lv.25 Warrior    [Ready checkmark]   | |
| | [ --- ] xDarkLord    Lv.18 Mage       [Ready checkmark]   | |
| | [ --- ] CoolPlayer99 Lv.12 Ranger     [Not ready]         | |
| | [Empty Slot]                                               | |
| +-----------------------------------------------------------+ |
+---------------------------------------------------------------+
| Chat:                                                          |
| PlayerOne: everyone ready?                                     |
| xDarkLord: one sec                                             |
| [Type message...                              ] [Send]         |
+---------------------------------------------------------------+
| [Leave]                                [Ready] [Start Game]   |
+---------------------------------------------------------------+
```

### LobbyPlayerSlotUI

**File:** `Assets/Scripts/Lobby/UI/LobbyPlayerSlotUI.cs`

Individual player slot within the lobby room.

```
LobbyPlayerSlotUI : MonoBehaviour
  [SerializeField] GameObject OccupiedRoot       // Active when slot has player
  [SerializeField] GameObject EmptyRoot          // Active when slot is empty
  [SerializeField] GameObject HostCrown          // Crown icon for host
  [SerializeField] TextMeshProUGUI PlayerNameText
  [SerializeField] TextMeshProUGUI LevelText     // "Lv.25"
  [SerializeField] TextMeshProUGUI ClassText     // "Warrior"
  [SerializeField] Image ClassIcon
  [SerializeField] Image ReadyIndicator          // Green check / Red X
  [SerializeField] TextMeshProUGUI PingText      // "45ms"
  [SerializeField] Button KickButton             // Host only, hidden for non-host

  // Methods
  SetSlotData(LobbyPlayerSlot) : void
  SetEmpty() : void
  SetHostControls(bool isLocalHost) : void
  OnKickClicked() : void
```

### TransitionLoadingPanel

**File:** `Assets/Scripts/Lobby/UI/LobbyUIManager.cs` (inner panel)

```
TransitionLoadingPanel : MonoBehaviour
  [SerializeField] TextMeshProUGUI PhaseText       // "Creating game world..."
  [SerializeField] Slider ProgressBar
  [SerializeField] TextMeshProUGUI ProgressPercent  // "67%"
  [SerializeField] Button CancelButton             // Only during WaitingForPlayers
  [SerializeField] TextMeshProUGUI TipText          // Random gameplay tip

  UpdatePhase(TransitionPhase, float progress) : void
```

---

## Editor Tooling

### LobbyWorkstationWindow

**File:** `Assets/Editor/LobbyWorkstation/LobbyWorkstationWindow.cs`

```
Menu: DIG/Lobby Workstation
```

Sidebar + `ILobbyWorkstationModule` pattern (matches existing workstation conventions).

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Lobby Inspector | `Modules/LobbyInspectorModule.cs` | Live lobby state: players, ready status, connection IDs, ping, Relay allocation info. Play-mode only |
| Transport Debug | `Modules/TransportDebugModule.cs` | Message log (type, size, direction, timestamp), bandwidth graph, connection state timeline |
| Map Registry | `Modules/MapRegistryModule.cs` | List all MapDefinitionSO assets, validate ScenePaths exist, check spawn position count >= MaxPlayers |
| Difficulty Registry | `Modules/DifficultyRegistryModule.cs` | List all DifficultyDefinitionSO assets, preview scaling values, validate field ranges |
| Transition Debugger | `Modules/TransitionDebuggerModule.cs` | Step-by-step transition phase viewer, manual phase override for testing, world creation status |
| Simulated Lobby | `Modules/SimulatedLobbyModule.cs` | Create a fake lobby with N simulated players for UI testing without networking. Toggle ready states, trigger start |

---

## Performance Considerations

### Lobby Phase (MonoBehaviour)

| System | Target | Notes |
|--------|--------|-------|
| `LobbyManager.Update()` | < 0.1ms | Message pump + heartbeat check. No per-frame allocations after initialization |
| `LobbyNetworkTransport.PumpEvents()` | < 0.05ms | Unity Transport event loop. Typically 0-2 events per frame |
| `LobbyDiscoveryService.QueryLobbies()` | < 5ms one-shot | Async, does not block main thread. Results cached |
| `LobbyBrowserPanel.ApplyFilter()` | < 0.1ms | Sort + filter over max 50 entries. No allocations (pooled list) |
| Lobby state broadcast | ~200-500 bytes | Full LobbyState serialized. Sent only on change, not per-frame |
| Heartbeat messages | 1 byte payload | Sent every 2s per connection. Negligible bandwidth |

### Transition Phase

| Operation | Target | Notes |
|-----------|--------|-------|
| Relay allocation | 500-2000ms | Async, shown behind loading screen. One-time cost |
| World creation | 200-500ms | `ClientServerBootstrap.CreateServerWorld()` + `CreateClientWorld()`. One-time |
| Scene load | 1000-5000ms | Depends on map size. Streamed via NetworkSceneManager |
| Player spawn | < 1ms per player | `GoInGameServerSystem` is Burst-compiled |
| Total transition | 2-8s | Covered by loading screen with progress bar |

### Memory

| Allocation | Size | Lifetime |
|------------|------|----------|
| `LobbyState` | ~2 KB | Lobby phase only, GC'd on transition |
| `LobbyNetworkTransport` (driver) | ~64 KB | Lobby phase, driver disposed on game start |
| `LobbyInfo[]` (discovery cache) | ~5 KB (50 entries) | Refreshed every 3s, pooled |
| Relay allocation token | ~1 KB | Held until game world is running |
| Total lobby overhead | ~72 KB | Released when transitioning to game |

### Bandwidth Budget (Lobby Phase)

| Message | Frequency | Size | Bandwidth |
|---------|-----------|------|-----------|
| StateUpdate broadcast | On change (~1/s) | 500 bytes | 500 B/s |
| Heartbeat | Every 2s per conn | 3 bytes | ~6 B/s per player |
| DiscoveryResponse | On query (~1/3s) | 100 bytes per lobby | 1.5 KB/s while browsing |
| ChatMessage | On send | 130 bytes max | Negligible |
| Total (4-player lobby) | -- | -- | < 2 KB/s |

---

## Integration with Existing Systems

### GoInGameServerSystem Modification

**File:** `Assets/Scripts/Systems/Network/GoInGameSystem.cs` (MODIFY -- ~15 lines)

Currently, `GoInGameServerSystem` spawns players at a fixed offset (`float3(0, 1, networkId.Value * 2)`). After EPIC 17.4, the spawn position comes from `MapDefinitionSO.SpawnPositions[slotIndex]`.

Change:
```
// BEFORE: Simple NetworkId-based offset
var pos = new LocalTransform {
    Position = new float3(0, 1, networkId.Value * 2),
    ...
};

// AFTER: Read from LobbySpawnData singleton (created by LobbyToGameTransition)
if (SystemAPI.HasSingleton<LobbySpawnData>()) {
    var spawnData = SystemAPI.GetSingleton<LobbySpawnData>();
    var slotIndex = spawnData.GetSlotForNetworkId(networkId.Value);
    pos.Position = spawnData.SpawnPositions[slotIndex];
    pos.Rotation = spawnData.SpawnRotations[slotIndex];
}
```

### LobbySpawnData (ECS Singleton)

**File:** `Assets/Scripts/Lobby/LobbySpawnData.cs`

```
LobbySpawnData (IComponentData, NOT ghost-replicated)
  SpawnPositions : NativeArray<float3>    // Per-slot spawn positions from MapDefinitionSO
  SpawnRotations : NativeArray<quaternion> // Per-slot spawn rotations
  NetworkIdToSlot: NativeHashMap<int, int> // NetworkId -> SlotIndex mapping
```

Created by `LobbyToGameTransition` during the `CreatingWorlds` phase and placed into `ServerWorld` as a singleton entity. Consumed by `GoInGameServerSystem` to determine spawn positions. Destroyed after all players have spawned.

### SaveIdAssignmentSystem Integration

**File:** `Assets/Scripts/Persistence/Systems/SaveIdAssignmentSystem.cs` (MODIFY -- ~5 lines)

Currently generates `player_{NetworkId}` as the PlayerId. After EPIC 17.4, use the persistent `PlayerIdentity.PlayerId` from lobby data instead, so save files are tied to the player identity rather than an ephemeral network ID.

Change:
```
// BEFORE: Ephemeral NetworkId-based PlayerId
saveId.PlayerId = new FixedString64Bytes($"player_{ghost.NetworkId}");

// AFTER: Use lobby-assigned persistent PlayerId if available
if (SystemAPI.HasSingleton<LobbySpawnData>()) {
    var spawnData = SystemAPI.GetSingleton<LobbySpawnData>();
    var persistentId = spawnData.GetPlayerIdForNetworkId(ghost.NetworkId);
    saveId.PlayerId = persistentId;
} else {
    saveId.PlayerId = new FixedString64Bytes($"player_{ghost.NetworkId}");
}
```

---

## Error Handling & Edge Cases

### Host Disconnect During Lobby

If the host disconnects or crashes during the lobby phase:
1. Clients detect heartbeat timeout (`HeartbeatTimeoutMs`)
2. `LobbyManager` transitions to `Disconnected` state
3. UI shows "Host disconnected" dialog with options: "Return to Browser" / "Retry"
4. No host migration in lobby phase (client must find or create a new lobby)

### Host Disconnect During Transition

If the host disconnects during lobby-to-game transition:
1. `LobbyToGameTransition.AbortTransition("Host disconnected")`
2. Destroy any partially created NetCode worlds
3. Return to main menu with error message

### Client Disconnect During Game (Reconnect)

1. NetCode detects transport disconnect in `ClientWorld`
2. `LobbyToGameTransition.HandleDisconnect()`:
   - Cache `JoinCode` and `PlayerIdentity`
   - Destroy `ClientWorld`
   - Show "Reconnecting..." overlay
3. Attempt rejoin via `LobbyNetworkTransport.JoinRelay(cachedJoinCode)`
4. If successful: recreate `ClientWorld`, connect, `GoInGameClientSystem` fires
5. `PlayerReconnectSaveSystem` restores save state (existing system)
6. If timeout: show "Connection Lost" with "Return to Menu" button

### Lobby Full

Host validates player count before accepting `JoinRequest`. If full, sends `JoinDenied(LobbyFull)`. Client UI shows "Lobby is full" toast notification.

### Version Mismatch

`JoinRequest` includes a `uint ProtocolVersion` field. If host's version differs, sends `JoinDenied(VersionMismatch)`. This prevents desync from mismatched game builds.

---

## File Summary

### New Files (22)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Lobby/LobbyManager.cs` | MonoBehaviour singleton |
| 2 | `Assets/Scripts/Lobby/LobbyState.cs` | Data structs (LobbyState, LobbyPlayerSlot, GameMode, LobbyConnectionState, PlayerIdentity) |
| 3 | `Assets/Scripts/Lobby/LobbyNetworkTransport.cs` | Unity Transport wrapper + Relay allocation |
| 4 | `Assets/Scripts/Lobby/LobbyDiscoveryService.cs` | Lobby browsing service |
| 5 | `Assets/Scripts/Lobby/LobbyJoinCodeService.cs` | Join code generation/validation |
| 6 | `Assets/Scripts/Lobby/LobbyToGameTransition.cs` | Lobby -> NetCode world orchestrator |
| 7 | `Assets/Scripts/Lobby/LobbySpawnData.cs` | IComponentData singleton (ECS, spawn positions) |
| 8 | `Assets/Scripts/Lobby/LobbyMessages.cs` | Message types + serialization helpers |
| 9 | `Assets/Scripts/Lobby/UI/LobbyUIManager.cs` | UI panel manager |
| 10 | `Assets/Scripts/Lobby/UI/LobbyBrowserPanel.cs` | Lobby browser with filter/sort |
| 11 | `Assets/Scripts/Lobby/UI/LobbyRoomPanel.cs` | Lobby room view |
| 12 | `Assets/Scripts/Lobby/UI/LobbyPlayerSlotUI.cs` | Individual player slot widget |
| 13 | `Assets/Scripts/Lobby/UI/LobbyListEntryUI.cs` | Pooled list entry for browser |
| 14 | `Assets/Scripts/Lobby/UI/TransitionLoadingPanel.cs` | Loading screen during transition |
| 15 | `Assets/Scripts/Lobby/UI/QuickMatchPanel.cs` | Quick match searching overlay |
| 16 | `Assets/Scripts/Lobby/Config/LobbyConfigSO.cs` | ScriptableObject (lobby settings) |
| 17 | `Assets/Scripts/Lobby/Config/MapDefinitionSO.cs` | ScriptableObject (per-map config) |
| 18 | `Assets/Scripts/Lobby/Config/DifficultyDefinitionSO.cs` | ScriptableObject (per-difficulty scaling) |
| 19 | `Assets/Scripts/Lobby/DIG.Lobby.asmdef` | Assembly definition |
| 20 | `Assets/Editor/LobbyWorkstation/LobbyWorkstationWindow.cs` | EditorWindow |
| 21 | `Assets/Editor/LobbyWorkstation/ILobbyWorkstationModule.cs` | Module interface |
| 22 | `Assets/Editor/LobbyWorkstation/Modules/LobbyInspectorModule.cs` | Live lobby state inspector |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/Systems/Network/GoInGameSystem.cs` | Read spawn positions from `LobbySpawnData` singleton instead of fixed offset (~15 lines) |
| 2 | `Assets/Scripts/Persistence/Systems/SaveIdAssignmentSystem.cs` | Use persistent PlayerId from lobby data instead of `player_{NetworkId}` (~5 lines) |
| 3 | `Packages/manifest.json` | Add `com.unity.services.relay` package dependency |

### Resource/Config Assets

| # | Path |
|---|------|
| 1 | `Assets/Resources/LobbyConfig.asset` (LobbyConfigSO) |
| 2 | `Assets/Resources/Maps/ForsakenMines.asset` (MapDefinitionSO) |
| 3 | `Assets/Resources/Maps/CrimsonWastes.asset` (MapDefinitionSO) |
| 4 | `Assets/Resources/Difficulties/Normal.asset` (DifficultyDefinitionSO) |
| 5 | `Assets/Resources/Difficulties/Hard.asset` (DifficultyDefinitionSO) |
| 6 | `Assets/Resources/Difficulties/Nightmare.asset` (DifficultyDefinitionSO) |

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `GoInGameServerSystem` | Existing | Modified to read `LobbySpawnData` for per-slot spawn positions |
| `SaveIdAssignmentSystem` | 16.15 | Modified to use persistent `PlayerIdentity.PlayerId` from lobby |
| `PlayerReconnectSaveSystem` | 16.15 | Unchanged -- handles in-game reconnect after `LobbyToGameTransition` re-creates ClientWorld |
| `SaveSystem` / `LoadSystem` | 16.15 | Unchanged -- `PlayerSaveId` now maps to persistent PlayerId instead of `player_{NetworkId}` |
| `PlayerProgression.Level` | 16.14 | Read from save data for `LobbyPlayerSlot.Level` display in lobby |
| `CharacterAttributes` | Existing | Read from save data for `LobbyPlayerSlot.ClassId` display in lobby |
| `DifficultyDefinitionSO.XPMultiplier` | 16.14 | `XPAwardSystem` reads difficulty from `DifficultyConfig` singleton (created during transition) |
| `DifficultyDefinitionSO.LootQualityBonus` | 16.6 | `DeathLootSystem` reads difficulty for loot quality scaling |
| `DifficultyDefinitionSO.EnemyHealthScale` | Existing | `EnemySpawnSystem` applies health/damage scaling based on difficulty |
| PvP Arena game mode | 17.10 | `GameMode.PvPArena` enum value reserved, lobby infrastructure shared |
| Skill Trees | 17.1 | `LobbyPlayerSlot.ClassId` displays active talent spec in lobby |
| Party System | Future | Party members auto-join same lobby, lobby respects party leader |
| Server Browser | Future | `LobbyDiscoveryService` can be replaced by dedicated matchmaking backend |

---

## ECS Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `LobbySpawnData` singleton | ~128 bytes | Server world singleton entity (destroyed after all players spawn) |
| **Total on player** | **0 bytes** | No changes to player entity archetype |

The lobby system adds **zero bytes** to the player entity archetype. `LobbySpawnData` is a transient singleton on a dedicated entity in `ServerWorld`, destroyed after the spawn phase completes. All lobby state is MonoBehaviour-based and lives outside ECS.

---

## Extensibility

- **Dedicated Server:** Replace `LobbyToGameTransition` host-side logic with a server allocation service (e.g., Multiplay). Lobby sends match config to allocation service instead of creating local ServerWorld. Clients receive server IP instead of Relay join data.
- **Host Migration:** On host disconnect during game, elect new host from remaining players. New host creates ServerWorld, other clients reconnect. Requires serializing full ECS world state (complex, deferred to future EPIC).
- **Spectator Mode:** Add `LobbyPlayerSlot.IsSpectator` flag. Spectators connect to ServerWorld but `GoInGameServerSystem` skips player spawning, creates camera-only entity instead.
- **Party System:** `LobbyManager.CreateLobby()` accepts party member list. Party leader becomes host. Party members auto-join and cannot be kicked.
- **Ranked Matchmaking:** Extend `LobbyDiscoveryService` with skill-based rating (MMR). Quick Match prioritizes lobbies with similar MMR. Requires backend service.
- **Cross-Play:** Unity Relay is transport-agnostic. Add platform identifier to `PlayerIdentity` and `LobbyPlayerSlot` for cross-play filtering.
- **Map Voting:** Add `LobbyMessageType.MapVote`. Host collects votes, displays results, applies majority choice before game start.
- **Character Select:** Extend `LobbyRoomPanel` with character model preview. Each slot shows 3D character render. Lock duplicate class selections if desired.

---

## Verification Checklist

- [ ] Host creates lobby: `LobbyState` populated, Relay allocated, transport listening
- [ ] Host creates private lobby: `IsPrivate=true`, not listed in browser, join code displayed
- [ ] Host creates public lobby: listed in browser within one refresh cycle (3s)
- [ ] Client joins via join code: transport connects, `JoinRequest` sent, `JoinAccepted` received, slot populated
- [ ] Client joins via browser: lobby list displays, click joins, same flow as join code
- [ ] Quick match with available lobby: auto-joins best match within timeout
- [ ] Quick match with no lobbies: auto-creates new lobby after `QuickMatchTimeoutSeconds`
- [ ] Lobby full: `JoinDenied(LobbyFull)` received, UI shows toast notification
- [ ] Version mismatch: `JoinDenied(VersionMismatch)` received, UI shows version error
- [ ] Player ready: `ReadyChanged` sent to host, all clients see checkmark update
- [ ] All players ready: host "Start Game" button enables
- [ ] Not all ready: host "Start Game" button disabled
- [ ] Host changes map: all clients see updated map name and thumbnail
- [ ] Host changes difficulty: all clients see updated difficulty badge
- [ ] Host kicks player: target receives `KickPlayer`, disconnected, slot cleared for all
- [ ] Player leaves voluntarily: `LeaveNotify` sent, slot cleared, remaining players notified
- [ ] Host starts game: `StartGame` broadcast, all clients receive `GameStartPayload`
- [ ] Transition creates ServerWorld (host): `NetworkStreamRequestListen` succeeds
- [ ] Transition creates ClientWorld (all): `NetworkStreamRequestConnect` succeeds
- [ ] Scene loads via NetworkSceneManager: subscenes stream correctly
- [ ] `GoInGameServerSystem` spawns player at correct `MapDefinitionSO.SpawnPositions[slotIndex]`
- [ ] `SaveIdAssignmentSystem` uses persistent `PlayerIdentity.PlayerId` instead of `player_{NetworkId}`
- [ ] Player reconnect during lobby: same PlayerId recognized, original slot restored
- [ ] Player reconnect during game: `LobbyToGameTransition.HandleDisconnect()` -> rejoin -> `PlayerReconnectSaveSystem` restores state
- [ ] Host disconnect during lobby: clients detect timeout, show error dialog
- [ ] Host disconnect during transition: `AbortTransition()` cleans up partial worlds, returns to menu
- [ ] Heartbeat timeout: disconnected player removed from lobby after `HeartbeatTimeoutMs`
- [ ] Chat message: sent to host, broadcast to all, displayed in chat panel
- [ ] Join code copied to clipboard: `OnCopyCodeClicked()` uses `GUIUtility.systemCopyBuffer`
- [ ] Lobby browser filter by map: only matching lobbies shown
- [ ] Lobby browser filter by difficulty: only matching lobbies shown
- [ ] Lobby browser sort by ping: entries ordered by `PingMs` ascending
- [ ] Lobby browser pagination: max 50 results, no scroll performance issues
- [ ] Loading screen: phase text updates, progress bar fills, cancel available during `WaitingForPlayers`
- [ ] Return to lobby post-game: NetCode worlds destroyed, lobby transport re-initialized, players rejoin
- [ ] `PlayerIdentity` persisted in `PlayerPrefs`: same PlayerId across application restarts
- [ ] New player first launch: GUID generated, saved to `PlayerPrefs`, used as PlayerId
- [ ] Lobby Workstation: inspector shows live state in play mode
- [ ] Lobby Workstation: simulated lobby creates fake players for UI testing
- [ ] No regression: direct-connect flow (no lobby) still works when `LobbySpawnData` singleton absent
- [ ] NAT traversal: two players behind different NATs can connect via Relay
- [ ] Bandwidth: lobby phase < 2 KB/s with 4 players
- [ ] Memory: lobby overhead < 100 KB, fully released on game start
- [ ] Transition time: < 8 seconds from "Start Game" to all players in game
