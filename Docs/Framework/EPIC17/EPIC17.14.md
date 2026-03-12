# EPIC 17.14: Platform Identity & Profile Provider System

**Status:** PLANNED
**Priority:** High (Multiplayer Foundation)
**Dependencies:**
- `PlayerIdentity` class (existing -- `Assets/Scripts/Lobby/LobbyState.cs`, GUID-based local identity, PlayerPrefs persistence)
- `LobbyPlayerSlot` class (existing -- `Assets/Scripts/Lobby/LobbyState.cs`, PlayerId/DisplayName/Level/ClassId per slot)
- `LobbyManager` MonoBehaviour singleton (existing -- `Assets/Scripts/Lobby/LobbyManager.cs`, EPIC 17.4, lobby lifecycle)
- `JoinRequestMessage` (existing -- `Assets/Scripts/Lobby/LobbyMessages.cs`, carries PlayerId/DisplayName)
- `LobbySpawnData` IComponentData singleton (existing -- `Assets/Scripts/Lobby/LobbySpawnData.cs`, bridges NetworkId→PlayerId)
- `SaveIdAssignmentSystem` (existing -- `Assets/Scripts/Persistence/Systems/SaveIdAssignmentSystem.cs`, EPIC 16.15, maps PlayerId to save files)
- `PlayerSaveId` IComponentData (existing -- `Assets/Scripts/Persistence/Components/SaveStateComponents.cs`, FixedString64Bytes)
- `ServiceLocator` (existing -- `Assets/Scripts/Core/Services/ServiceLocator.cs`, generic static service registry)
- `CombatUIRegistry` + `ICombatUIProviders` pattern (existing -- `Assets/Scripts/Combat/UI/`, interface-based provider registration)
- `LobbyConfigSO` ScriptableObject (existing -- `Assets/Scripts/Lobby/Config/LobbyConfigSO.cs`, designer config pattern)
- `LobbyWorkstationWindow` + `ILobbyWorkstationModule` (existing -- `Assets/Editor/LobbyWorkstation/`, editor tooling pattern)

**Feature:** A modular, zero-allocation platform identity abstraction layer that decouples player identity from any single platform SDK. Supports Steam, Epic Online Services (EOS), GOG Galaxy, Discord, and offline/local fallback through a single `IIdentityProvider` interface. Provides async avatar loading with LRU texture caching, rich profile data (display name, avatar, platform UID, online status, friend list), and a configuration-driven provider selection system using ScriptableObjects. Integrates seamlessly with the existing lobby system (EPIC 17.4) and persistence pipeline (EPIC 16.15) without modifying any ECS components or ghost archetypes. Includes AAA-quality editor tooling for mocking providers, inspecting live state, and rapid iteration.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `PlayerIdentity` class | `LobbyState.cs:46-99` | Functional, minimal | GUID from `Guid.NewGuid()`, DisplayName from PlayerPrefs, no platform SDK |
| `PlayerIdentity.Local` singleton | `LobbyState.cs:61-83` | Lazy-loaded | Reads `DIG_PlayerId` / `DIG_DisplayName` from PlayerPrefs |
| `LobbyPlayerSlot` | `LobbyState.cs:104-131` | Functional | PlayerId, DisplayName, Level, ClassId, PingMs — no avatar, no platform ID |
| `JoinRequestMessage` | `LobbyMessages.cs:41-67` | Binary serialized | Carries PlayerId + DisplayName + Level + ClassId + GameVersion |
| `LobbyManager` | `LobbyManager.cs` | Functional | Uses `PlayerIdentity.Local` for host slot + join requests |
| `LobbySpawnData` | `LobbySpawnData.cs` | Transient singleton | Maps NetworkId→PlayerId via `GetPersistentIdForNetworkId()` |
| `SaveIdAssignmentSystem` | `SaveIdAssignmentSystem.cs` | Server/Local | Reads LobbySpawnData for persistent PlayerId, falls back to `player_{NetworkId}` |
| `ServiceLocator` | `ServiceLocator.cs` | Generic container | `Register<T>`, `Get<T>`, `TryGet<T>`, `Clear()` |
| `CombatUIRegistry` pattern | `CombatUIRegistry.cs` | Static registry | Interface-typed providers with register/unregister/nullability checks |
| `LobbyConfigSO` | `LobbyConfigSO.cs` | `[CreateAssetMenu]` | Designer-facing config loaded from Resources/ |
| Unity Relay SDK | `manifest.json` | `com.unity.services.multiplayer:2.1.1` | Relay available, no auth package installed |

### What's Missing

- **No platform SDK packages** — Steamworks, EOS, GOG Galaxy, Discord Game SDK not in manifest
- **No identity abstraction** — `PlayerIdentity` is hardcoded to PlayerPrefs, no interface, no swappable providers
- **No avatar/profile picture system** — no Texture2D loading, no caching, no display
- **No platform-specific user ID** — only local GUID, no Steam ID / Epic Account ID mapping
- **No authentication flow** — no login screen, no token management, no session validation
- **No rich profile data** — no bio, status, playtime, achievement count, friend list
- **No cross-platform identity mapping** — no linking Steam account to Epic account to local GUID
- **No platform presence** — no "In Lobby" / "In Game" status pushed to Steam/Discord/etc.
- **No friend list integration** — no "invite friend" or "join friend's game"
- **No platform overlay hooks** — no Steam overlay invite, no Discord Rich Presence
- **No avatar in lobby UI** — `LobbyPlayerSlotUI` shows name/level/ping but no profile picture
- **No identity config SO** — no designer-facing configuration for platform selection
- **No editor tooling for identity** — no mock provider, no live state inspector

---

## Problem

DIG's multiplayer identity is a locally-generated GUID stored in PlayerPrefs. This means:

1. **No platform integration** — Players see "Player_a1b2c3" instead of their Steam/Epic/GOG username and avatar
2. **No persistence across devices** — Reinstalling the game generates a new GUID, orphaning save data
3. **No social features** — Can't invite friends, can't see who's online, can't join via platform overlay
4. **No anti-impersonation** — Anyone can set any DisplayName via PlayerPrefs
5. **Tightly coupled** — `PlayerIdentity` is a concrete class referenced directly by LobbyManager, LobbyMessages, and SaveIdAssignment — swapping to a platform SDK requires touching every callsite

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| Local GUID (`DIG_PlayerId`) | Platform-verified user ID (Steam ID, Epic Account) |
| PlayerPrefs display name | Platform display name + avatar texture |
| Direct `PlayerIdentity.Local` calls | Abstracted `IIdentityProvider` interface |
| Binary lobby messages with PlayerId | Rich profile data (avatar URL, status, friends) |
| LobbyPlayerSlotUI with name/level | Avatar thumbnail in player slot |
| SaveIdAssignmentSystem reads PlayerId | Cross-device persistent identity from platform |
| No configuration | ScriptableObject-driven provider selection |

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                    CONFIGURATION LAYER                               │
│  PlatformIdentityConfigSO          ProviderPriorityList             │
│  ├── ActiveProviderType (enum)     ├── Steam > Epic > GOG > Local   │
│  ├── AllowFallback (bool)          └── Auto-detect from installed   │
│  ├── AvatarCacheSize (int)              SDK packages                │
│  └── AvatarResolution (enum)                                        │
└────────────────────────────┬────────────────────────────────────────┘
                             │ Loaded from Resources/
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    PROVIDER LAYER (MonoBehaviour)                     │
│                                                                      │
│  IdentityManager (singleton, DontDestroyOnLoad)                      │
│  ├── InitializeAsync() — detects & initializes active provider       │
│  ├── IIdentityProvider ActiveProvider (current platform)              │
│  ├── IdentityState { Uninitialized, Initializing, Ready, Failed }    │
│  └── Events: OnIdentityReady, OnIdentityFailed, OnAvatarLoaded      │
│                                                                      │
│  IIdentityProvider ──────────────────────────────────────────────┐   │
│  │  GetPlatformId() → string         GetDisplayName() → string  │   │
│  │  GetAvatarAsync() → Task<Tex2D>   GetFriends() → FriendInfo[]│   │
│  │  SetRichPresence(string)           IsReady → bool             │   │
│  │  SupportsAvatars → bool            SupportsPresence → bool    │   │
│  │  SupportsInvites → bool            SupportsFriends → bool     │   │
│  └───────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  Implementations:                                                    │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌────────────┐ │
│  │ SteamProvider │ │ EpicProvider │ │ GogProvider  │ │LocalProvider│ │
│  │ Steamworks.NET│ │ EOS SDK     │ │ Galaxy SDK   │ │ PlayerPrefs │ │
│  │ #if HAS_STEAM │ │ #if HAS_EOS │ │ #if HAS_GOG  │ │ (fallback)  │ │
│  └──────────────┘ └──────────────┘ └──────────────┘ └────────────┘ │
└────────────────────────────┬────────────────────────────────────────┘
                             │ IdentityManager.ActiveProvider
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    INTEGRATION LAYER                                  │
│                                                                      │
│  PlayerIdentity.Local ← populated from ActiveProvider                │
│  ├── PlayerId = provider.GetPlatformId() ?? GUID fallback            │
│  ├── DisplayName = provider.GetDisplayName()                         │
│  └── AvatarTexture = provider.GetAvatarAsync() result (cached)       │
│                                                                      │
│  LobbyManager ← uses PlayerIdentity.Local (unchanged API)           │
│  LobbyPlayerSlot ← adds optional AvatarSmallHash for UI             │
│  LobbyPlayerSlotUI ← shows avatar thumbnail if available            │
│  SaveIdAssignmentSystem ← uses PlayerIdentity.PlayerId (unchanged)  │
│                                                                      │
│  AvatarCache (static)                                                │
│  ├── LRU eviction, configurable max entries                          │
│  ├── GetOrLoadAsync(platformId) → Texture2D                         │
│  └── Memory-aware: Texture2D.Compress() + mipmap stripping          │
└─────────────────────────────────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                                 │
│                                                                      │
│  LobbyPlayerSlotUI ← RawImage for avatar thumbnail                  │
│  LobbyBrowserPanel ← host avatar in lobby list entries               │
│  ProfilePopup ← click player name → full profile card               │
│  Rich Presence ← "In Lobby (2/4)" / "In Game - Default Map"         │
└─────────────────────────────────────────────────────────────────────┘
```

### Initialization Flow

```
1. Game Launch
   IdentityManager.Awake()
     → Load PlatformIdentityConfigSO from Resources/
     → DontDestroyOnLoad

2. IdentityManager.Start()
   → InitializeAsync():
     a. Read ActiveProviderType from config
     b. If Auto: detect installed SDKs via #if defines
     c. Instantiate provider: new SteamIdentityProvider() / etc.
     d. await provider.InitializeAsync()
     e. If success: State = Ready, fire OnIdentityReady
     f. If fail + AllowFallback: try LocalIdentityProvider
     g. Populate PlayerIdentity.Local from provider

3. Lobby Phase (EPIC 17.4 — unchanged)
   LobbyManager.CreateLobby()
     → PlayerIdentity.Local.PlayerId  ← now platform ID
     → PlayerIdentity.Local.DisplayName ← now platform name

4. Avatar Loading (async, non-blocking)
   LobbyRoomPanel.RefreshState()
     → For each occupied slot:
       AvatarCache.GetOrLoadAsync(slot.PlayerId)
       → Cache hit: immediate Texture2D
       → Cache miss: provider.GetAvatarAsync(platformId)
         → Download/decode → cache → callback → UI updates
```

---

## Core Interface

### IIdentityProvider

**File:** `Assets/Scripts/Identity/IIdentityProvider.cs`

```csharp
namespace DIG.Identity
{
    public enum IdentityProviderType : byte
    {
        Local = 0,      // PlayerPrefs GUID fallback
        Steam = 1,      // Steamworks.NET
        Epic = 2,       // Epic Online Services
        GOG = 3,        // GOG Galaxy SDK
        Discord = 4     // Discord Game SDK
    }

    public enum IdentityState : byte
    {
        Uninitialized = 0,
        Initializing = 1,
        Ready = 2,
        Failed = 3
    }

    /// <summary>
    /// Platform-agnostic identity provider contract.
    /// Implementations wrap platform SDKs (Steam, Epic, GOG, etc.).
    /// </summary>
    public interface IIdentityProvider
    {
        IdentityProviderType ProviderType { get; }
        IdentityState State { get; }

        // ── Core Identity ──
        /// <summary>Platform-unique user ID (Steam64, Epic Account ID, etc.).</summary>
        string PlatformId { get; }
        /// <summary>Platform display name.</summary>
        string DisplayName { get; }

        // ── Capabilities (checked before calling optional methods) ──
        bool SupportsAvatars { get; }
        bool SupportsFriends { get; }
        bool SupportsPresence { get; }
        bool SupportsInvites { get; }

        // ── Lifecycle ──
        System.Threading.Tasks.Task<bool> InitializeAsync();
        void Shutdown();
        void Tick(); // Called every frame for SDK callbacks (Steam, etc.)

        // ── Avatar ──
        /// <summary>Returns cached avatar or loads async. Null if unsupported.</summary>
        System.Threading.Tasks.Task<UnityEngine.Texture2D> GetAvatarAsync(
            string platformId, AvatarSize size = AvatarSize.Medium);
        /// <summary>Get avatar for the local user.</summary>
        System.Threading.Tasks.Task<UnityEngine.Texture2D> GetLocalAvatarAsync(
            AvatarSize size = AvatarSize.Medium);

        // ── Friends ──
        FriendInfo[] GetFriends();
        FriendInfo[] GetOnlineFriends();

        // ── Presence ──
        void SetRichPresence(string key, string value);
        void ClearRichPresence();

        // ── Invites ──
        void InviteToLobby(string friendPlatformId, string joinCode);
    }

    public enum AvatarSize : byte
    {
        Small = 0,   // 32x32
        Medium = 1,  // 64x64
        Large = 2    // 128x128 (profile popup)
    }

    public struct FriendInfo
    {
        public string PlatformId;
        public string DisplayName;
        public bool IsOnline;
        public bool IsInGame;    // Playing DIG specifically
        public string LobbyCode; // If in a joinable lobby, null otherwise
    }
}
```

---

## Implementations

### LocalIdentityProvider (Fallback — Always Available)

**File:** `Assets/Scripts/Identity/Providers/LocalIdentityProvider.cs`

The current `PlayerIdentity` logic extracted into the provider interface. Zero platform dependencies. Uses PlayerPrefs for GUID + display name. No avatar support.

- `PlatformId` → `PlayerPrefs.GetString("DIG_PlayerId")` (GUID, generated on first launch)
- `DisplayName` → `PlayerPrefs.GetString("DIG_DisplayName")` (editable)
- `SupportsAvatars` → false
- `SupportsFriends` → false
- `SupportsPresence` → false
- `SupportsInvites` → false
- `InitializeAsync()` → synchronous, always succeeds
- `GetAvatarAsync()` → returns null

### SteamIdentityProvider

**File:** `Assets/Scripts/Identity/Providers/SteamIdentityProvider.cs`

Wrapped in `#if HAS_STEAMWORKS` define. Requires `Steamworks.NET` package (user-installed).

- `PlatformId` → `SteamUser.GetSteamID().m_SteamID.ToString()`
- `DisplayName` → `SteamFriends.GetPersonaName()`
- `GetAvatarAsync()` → `SteamFriends.GetMediumFriendAvatar()` → `SteamUtils.GetImageRGBA()` → `Texture2D.LoadRawTextureData()`
- `GetFriends()` → `SteamFriends.GetFriendCount()` + iterate
- `SetRichPresence()` → `SteamFriends.SetRichPresence(key, value)`
- `InviteToLobby()` → `SteamFriends.InviteUserToGame(steamId, connectString)`
- `Tick()` → `SteamAPI.RunCallbacks()` (required every frame)

### EpicIdentityProvider

**File:** `Assets/Scripts/Identity/Providers/EpicIdentityProvider.cs`

Wrapped in `#if HAS_EOS` define. Requires Epic Online Services SDK.

- `PlatformId` → `ProductUserId.ToString()`
- `DisplayName` → `UserInfoInterface.QueryUserInfo()` → `DisplayName`
- `GetAvatarAsync()` → Not natively supported by EOS (returns null, falls back to generated initials)
- `GetFriends()` → `FriendsInterface.QueryFriends()` + `GetFriendAtIndex()`
- `SetRichPresence()` → `PresenceInterface.SetPresence()`

### GogIdentityProvider

**File:** `Assets/Scripts/Identity/Providers/GogIdentityProvider.cs`

Wrapped in `#if HAS_GOG_GALAXY` define. Requires GOG Galaxy SDK.

- `PlatformId` → `GalaxyInstance.User().GetGalaxyID().ToUint64().ToString()`
- `DisplayName` → `GalaxyInstance.Friends().GetPersonaName()`
- `GetAvatarAsync()` → `Friends().GetFriendAvatarUrl()` → HTTP fetch → Texture2D
- `GetFriends()` → `Friends().GetFriendCount()` + iterate
- `SetRichPresence()` → `Friends().SetRichPresence(key, value)`

### DiscordIdentityProvider

**File:** `Assets/Scripts/Identity/Providers/DiscordIdentityProvider.cs`

Wrapped in `#if HAS_DISCORD_SDK` define. Requires Discord Game SDK.

- `PlatformId` → `UserManager.GetCurrentUser().Id.ToString()`
- `DisplayName` → `UserManager.GetCurrentUser().Username`
- `GetAvatarAsync()` → `ImageManager.Fetch()` with user ID + size
- `SetRichPresence()` → `ActivityManager.UpdateActivity()` (Discord Rich Presence)

---

## IdentityManager (Runtime Orchestrator)

**File:** `Assets/Scripts/Identity/IdentityManager.cs`

```csharp
/// <summary>
/// EPIC 17.14: Singleton MonoBehaviour that manages the active identity provider.
/// DontDestroyOnLoad. Initializes before LobbyManager.
/// </summary>
public class IdentityManager : MonoBehaviour
{
    public static IdentityManager Instance { get; private set; }

    public IIdentityProvider ActiveProvider { get; private set; }
    public IdentityState State { get; private set; }
    public bool IsReady => State == IdentityState.Ready;

    public event Action OnIdentityReady;
    public event Action<string> OnIdentityFailed;
    public event Action<string, Texture2D> OnAvatarLoaded; // platformId, texture

    private PlatformIdentityConfigSO _config;

    private void Awake()
    {
        // Singleton + DontDestroyOnLoad
        // Load PlatformIdentityConfigSO from Resources/
        // Script execution order: before LobbyManager (-200)
    }

    private async void Start()
    {
        await InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        State = IdentityState.Initializing;

        // 1. Determine provider type from config (or auto-detect)
        var type = _config.ActiveProviderType;
        if (type == IdentityProviderType.Auto)
            type = DetectAvailablePlatform();

        // 2. Create and initialize provider
        ActiveProvider = CreateProvider(type);
        bool success = await ActiveProvider.InitializeAsync();

        if (!success && _config.AllowFallback && type != IdentityProviderType.Local)
        {
            Debug.LogWarning($"[Identity] {type} failed, falling back to Local");
            ActiveProvider = CreateProvider(IdentityProviderType.Local);
            success = await ActiveProvider.InitializeAsync();
        }

        // 3. Populate PlayerIdentity.Local from provider
        if (success)
        {
            PlayerIdentity.Local.PlayerId = ActiveProvider.PlatformId;
            PlayerIdentity.Local.DisplayName = ActiveProvider.DisplayName;
            PlayerIdentity.Local.Save();
            State = IdentityState.Ready;
            OnIdentityReady?.Invoke();
        }
        else
        {
            State = IdentityState.Failed;
            OnIdentityFailed?.Invoke("All identity providers failed");
        }
    }

    private void Update()
    {
        // Tick SDK callbacks (Steam requires this every frame)
        ActiveProvider?.Tick();
    }

    private void OnDestroy()
    {
        ActiveProvider?.Shutdown();
    }
}
```

**Script Execution Order:** `-200` (before LobbyManager at default, before any system that reads `PlayerIdentity.Local`)

**DetectAvailablePlatform logic:**
```
#if HAS_STEAMWORKS → Steam
#elif HAS_EOS → Epic
#elif HAS_GOG_GALAXY → GOG
#elif HAS_DISCORD_SDK → Discord
#else → Local
```

---

## Avatar Cache

**File:** `Assets/Scripts/Identity/AvatarCache.cs`

```csharp
/// <summary>
/// EPIC 17.14: LRU texture cache for player avatars.
/// Thread-safe, memory-aware, configurable capacity.
/// </summary>
public static class AvatarCache
{
    // LRU dictionary: platformId → (Texture2D, lastAccessFrame)
    // Max entries from PlatformIdentityConfigSO.AvatarCacheSize (default 32)
    // On eviction: Texture2D.Destroy() to free GPU memory
    // On cache hit: update lastAccessFrame, return immediately
    // On cache miss: provider.GetAvatarAsync() → compress → store → return

    public static bool TryGet(string platformId, out Texture2D avatar);
    public static async Task<Texture2D> GetOrLoadAsync(
        string platformId, AvatarSize size = AvatarSize.Medium);
    public static void Invalidate(string platformId);
    public static void Clear(); // On shutdown
}
```

**Memory budget:** 32 entries × 64×64×4 bytes = ~512 KB (Medium). Configurable.

**Compression:** `Texture2D.Compress(true)` after load → ~128 KB for 32 entries at Medium.

---

## Configuration

### PlatformIdentityConfigSO

**File:** `Assets/Scripts/Identity/Config/PlatformIdentityConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Identity/Platform Identity Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| ActiveProviderType | IdentityProviderType | Auto | Which provider to use (Auto = detect from defines) |
| AllowFallback | bool | true | Fall back to Local if platform SDK fails |
| AvatarCacheSize | int (8-128) | 32 | Max cached avatar textures |
| AvatarResolution | AvatarSize | Medium | Default avatar request size |
| DefaultAvatarSprite | Sprite | null | Fallback avatar when none available |
| SteamAppId | uint | 0 | Steam App ID (required for Steamworks init) |
| EpicProductId | string | "" | EOS Product ID |
| EpicSandboxId | string | "" | EOS Sandbox ID |
| EpicDeploymentId | string | "" | EOS Deployment ID |
| EpicClientId | string | "" | EOS Client credentials |
| EpicClientSecret | string | "" | EOS Client secret |
| GogClientId | string | "" | GOG Galaxy Client ID |
| GogClientSecret | string | "" | GOG Galaxy Client secret |
| DiscordApplicationId | long | 0 | Discord Application ID |
| RichPresenceEnabled | bool | true | Push presence to platform |
| RichPresenceInLobby | string | "In Lobby ({0}/{1})" | Format string |
| RichPresenceInGame | string | "In Game - {0}" | Format string (map name) |

**Loaded from:** `Resources/PlatformIdentityConfig.asset`

---

## Lobby Integration (Minimal Changes)

### Modified: PlayerIdentity

**File:** `Assets/Scripts/Lobby/LobbyState.cs` (~15 lines changed)

`PlayerIdentity.Local` becomes a **pass-through** to `IdentityManager.ActiveProvider`:

```csharp
public static PlayerIdentity Local
{
    get
    {
        if (_instance != null) return _instance;
        _instance = new PlayerIdentity();

        // EPIC 17.14: Use IdentityManager if available, otherwise PlayerPrefs fallback
        var mgr = IdentityManager.Instance;
        if (mgr != null && mgr.IsReady)
        {
            _instance.PlayerId = mgr.ActiveProvider.PlatformId;
            _instance.DisplayName = mgr.ActiveProvider.DisplayName;
        }
        else
        {
            // Original fallback (backward compatible)
            _instance.PlayerId = PlayerPrefs.GetString(PlayerIdKey, "");
            if (string.IsNullOrEmpty(_instance.PlayerId))
            {
                _instance.PlayerId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PlayerIdKey, _instance.PlayerId);
                PlayerPrefs.Save();
            }
            _instance.DisplayName = PlayerPrefs.GetString(DisplayNameKey,
                $"Player_{_instance.PlayerId[..6]}");
        }

        _instance.LastLevel = PlayerPrefs.GetInt(LastLevelKey, 1);
        _instance.LastClassId = PlayerPrefs.GetInt(LastClassIdKey, 0);
        return _instance;
    }
}
```

**Backward compatible:** If `IdentityManager` doesn't exist (hasn't been set up), the original PlayerPrefs path runs unchanged.

### Modified: LobbyPlayerSlotUI

**File:** `Assets/Scripts/Lobby/UI/LobbyPlayerSlotUI.cs` (~12 lines added)

Add optional `RawImage _avatar` field. In `SetSlot()`, request avatar from cache:

```csharp
[SerializeField] private RawImage _avatar; // Optional — null if not wired

public void SetSlot(LobbyPlayerSlot slot, bool showKick)
{
    // ... existing code unchanged ...

    // EPIC 17.14: Avatar (non-blocking, null-safe)
    if (_avatar != null && !string.IsNullOrEmpty(slot.PlayerId))
    {
        if (AvatarCache.TryGet(slot.PlayerId, out var tex))
        {
            _avatar.texture = tex;
            _avatar.gameObject.SetActive(true);
        }
        else
        {
            _avatar.gameObject.SetActive(false); // Hide until loaded
            LoadAvatarAsync(slot.PlayerId);
        }
    }
}

private async void LoadAvatarAsync(string platformId)
{
    var tex = await AvatarCache.GetOrLoadAsync(platformId);
    if (tex != null && _avatar != null)
    {
        _avatar.texture = tex;
        _avatar.gameObject.SetActive(true);
    }
}
```

### Modified: LobbyManager

**File:** `Assets/Scripts/Lobby/LobbyManager.cs` (~8 lines added)

Add rich presence updates at phase transitions:

```csharp
// In CreateLobby(), after Phase = InLobby:
UpdateRichPresence();

// In HandleJoinAccepted(), after Phase = InLobby:
UpdateRichPresence();

// In StartGame() / HandleStartGame(), after Phase = Transitioning:
ClearRichPresence();

// In Cleanup():
ClearRichPresence();

// New helper methods:
private void UpdateRichPresence()
{
    var mgr = IdentityManager.Instance;
    if (mgr == null || !mgr.IsReady || !mgr.ActiveProvider.SupportsPresence) return;
    var config = Resources.Load<PlatformIdentityConfigSO>("PlatformIdentityConfig");
    if (config == null || !config.RichPresenceEnabled) return;

    string status = string.Format(config.RichPresenceInLobby,
        CurrentLobby.PlayerCount, CurrentLobby.MaxPlayers);
    mgr.ActiveProvider.SetRichPresence("status", status);
}

private void ClearRichPresence()
{
    var mgr = IdentityManager.Instance;
    if (mgr != null && mgr.IsReady)
        mgr.ActiveProvider.ClearRichPresence();
}
```

---

## Editor Tooling

### IdentityWorkstation Module

**File:** `Assets/Editor/LobbyWorkstation/Modules/IdentityInspectorModule.cs`

Added as a new module to the existing `LobbyWorkstationWindow`:

**Live State Inspector (Play Mode):**
- Current provider type + state (Ready/Initializing/Failed)
- Platform ID + Display Name
- Avatar texture preview (if loaded)
- Avatar cache stats (entries, memory, hit rate)
- Friend list (if supported)
- Rich presence current value

**Mock Provider (Edit Mode + Play Mode):**
- Override provider type dropdown
- Custom display name field
- Custom platform ID field
- Load custom avatar texture from project
- Simulate init failure (test fallback path)
- Simulate slow init (test loading states)

**Provider Diagnostics:**
- SDK initialization time (ms)
- Avatar load time per request (ms)
- Cache hit/miss ratio
- Memory usage breakdown

### Identity Config Validator

Built into the `IdentityInspectorModule`:

- Warns if SteamAppId is 0 when provider is Steam
- Warns if EOS credentials are empty when provider is Epic
- Validates DefaultAvatarSprite is assigned
- Warns if AvatarCacheSize is too large for target platform memory budget
- Shows which `#if` defines are active in current build

---

## File Summary

### New Files (11)

| # | Path | Type | Phase |
|---|------|------|-------|
| 1 | `Assets/Scripts/Identity/IIdentityProvider.cs` | Interface + enums + structs | 0 |
| 2 | `Assets/Scripts/Identity/IdentityManager.cs` | MonoBehaviour singleton | 0 |
| 3 | `Assets/Scripts/Identity/AvatarCache.cs` | Static LRU cache | 0 |
| 4 | `Assets/Scripts/Identity/Config/PlatformIdentityConfigSO.cs` | ScriptableObject | 0 |
| 5 | `Assets/Scripts/Identity/Providers/LocalIdentityProvider.cs` | IIdentityProvider impl | 1 |
| 6 | `Assets/Scripts/Identity/Providers/SteamIdentityProvider.cs` | IIdentityProvider impl (`#if HAS_STEAMWORKS`) | 1 |
| 7 | `Assets/Scripts/Identity/Providers/EpicIdentityProvider.cs` | IIdentityProvider impl (`#if HAS_EOS`) | 1 |
| 8 | `Assets/Scripts/Identity/Providers/GogIdentityProvider.cs` | IIdentityProvider impl (`#if HAS_GOG_GALAXY`) | 1 |
| 9 | `Assets/Scripts/Identity/Providers/DiscordIdentityProvider.cs` | IIdentityProvider impl (`#if HAS_DISCORD_SDK`) | 1 |
| 10 | `Assets/Editor/LobbyWorkstation/Modules/IdentityInspectorModule.cs` | Editor module | 2 |
| 11 | `Docs/EPIC17/SETUP_GUIDE_17.14.md` | Setup guide | 3 |

### Modified Files (4)

| # | Path | Change | Lines |
|---|------|--------|-------|
| 1 | `Assets/Scripts/Lobby/LobbyState.cs` | `PlayerIdentity.Local` reads from IdentityManager when available | ~15 |
| 2 | `Assets/Scripts/Lobby/UI/LobbyPlayerSlotUI.cs` | Optional `RawImage _avatar` field + async avatar load in `SetSlot()` | ~12 |
| 3 | `Assets/Scripts/Lobby/LobbyManager.cs` | Rich presence updates at phase transitions | ~8 |
| 4 | `Assets/Editor/LobbyWorkstation/LobbyWorkstationWindow.cs` | Register IdentityInspectorModule in module list | ~2 |

### Resource Assets (1)

| # | Path |
|---|------|
| 1 | `Assets/Resources/PlatformIdentityConfig.asset` |

---

## 16KB Archetype Impact

**Zero bytes** on any ECS entity. The identity system is entirely MonoBehaviour-based. No IComponentData, no IBufferElementData, no ghost components. `PlayerIdentity` remains a plain C# class. `LobbyPlayerSlot` remains a `[Serializable]` class.

The only ECS touch point (`SaveIdAssignmentSystem` reading `PlayerIdentity.Local.PlayerId`) is unchanged — it reads the same string property, which is now populated from the platform provider instead of PlayerPrefs.

---

## Performance Budget

| Operation | Target | Allocation | Notes |
|-----------|--------|------------|-------|
| `IdentityManager.Update()` (Tick) | < 0.05ms | 0 B/frame | Steam `RunCallbacks()` is ~0.01ms |
| `AvatarCache.TryGet()` | < 0.001ms | 0 B | Dictionary lookup |
| `AvatarCache.GetOrLoadAsync()` (miss) | < 200ms | 1 Texture2D | Async, off main thread where possible |
| `PlayerIdentity.Local` property | < 0.001ms | 0 B | Cached singleton |
| Rich presence update | < 0.1ms | 0 B | Only on phase transition (not per-frame) |
| Avatar cache memory (32 entries) | — | ~512 KB | Compressed: ~128 KB |

**Zero per-frame allocations.** All async operations use `Task` (no coroutine GC). Avatar textures are pooled via the LRU cache. Platform SDK callbacks are processed in the provider's `Tick()` with no managed allocations.

---

## Conditional Compilation Strategy

Platform SDKs are **not** hard dependencies. Each provider is wrapped in `#if` defines:

```
HAS_STEAMWORKS     — defined when Steamworks.NET package is installed
HAS_EOS            — defined when EOS SDK package is installed
HAS_GOG_GALAXY     — defined when GOG Galaxy SDK is installed
HAS_DISCORD_SDK    — defined when Discord Game SDK is installed
```

**How defines get set:** Via `csc.rsp` file or Unity's Scripting Define Symbols in Player Settings. The setup guide (SETUP_GUIDE_17.14.md) documents this per platform.

**When no SDK is installed:** Only `LocalIdentityProvider` compiles. Zero platform SDK code in the build. Zero additional package dependencies. The system degrades gracefully to the current PlayerPrefs behavior.

---

## Backward Compatibility

| Feature | Default (No Config) | Effect |
|---------|---------------------|--------|
| IdentityManager absent | `PlayerIdentity.Local` uses PlayerPrefs | Identical to pre-17.14 behavior |
| PlatformIdentityConfig absent | IdentityManager logs warning, uses Local | Falls back gracefully |
| No platform SDK installed | Auto-detect picks Local | Same as current GUID system |
| `_avatar` field not wired in UI | Null check skips avatar display | No visual change |
| Rich presence config disabled | `RichPresenceEnabled = false` | No platform API calls |

**The entire EPIC is opt-in.** Without `PlatformIdentityConfig.asset` in Resources and `IdentityManager` in the scene, the game runs identically to today.

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| Lobby System | 17.4 | PlayerIdentity.Local populated from provider; rich presence on phase change |
| Persistence | 16.15 | SaveIdAssignmentSystem uses same `PlayerId` string (now platform ID instead of GUID) |
| Party System | 17.2 | Party invites can use `InviteToLobby()` for platform overlay invites |
| PvP Arena | 17.10 | Ranking display uses platform avatar + name |
| Trading System | 17.3 | Trade partner shown with platform avatar |

---

## Future Extensions (Out of Scope for 17.14)

- **Account linking** — Map multiple platform IDs to a single DIG account (requires backend)
- **Backend authentication** — Server-side token validation (requires dedicated auth server)
- **Platform achievements** — Push DIG achievements to Steam/Epic/GOG achievement systems
- **Cloud saves** — Sync save files via platform cloud storage APIs
- **Voice chat identity** — Map Vivox participants to platform identities
- **Anti-cheat integration** — EAC/VAC tied to platform identity

---

## Verification Checklist

### Core Identity
- [ ] `IdentityManager` initializes before `LobbyManager` (script execution order -200)
- [ ] `PlayerIdentity.Local.PlayerId` returns platform ID when provider is ready
- [ ] `PlayerIdentity.Local.PlayerId` returns PlayerPrefs GUID when no provider exists
- [ ] `PlayerIdentity.Local.DisplayName` returns platform name when provider is ready
- [ ] Provider `Tick()` called every frame without allocation
- [ ] Provider `Shutdown()` called on `IdentityManager.OnDestroy()`

### Provider Fallback
- [ ] Auto-detect selects correct provider based on `#if` defines
- [ ] Failed Steam init falls back to Local when `AllowFallback = true`
- [ ] Failed Steam init fires `OnIdentityFailed` when `AllowFallback = false`
- [ ] No compile errors when zero platform SDKs are installed
- [ ] `LocalIdentityProvider` always succeeds initialization

### Avatar System
- [ ] `AvatarCache.TryGet()` returns true on cache hit, false on miss
- [ ] `AvatarCache.GetOrLoadAsync()` returns Texture2D from provider
- [ ] LRU eviction destroys oldest Texture2D when cache is full
- [ ] `AvatarCache.Clear()` destroys all cached textures
- [ ] Cache miss for `LocalIdentityProvider` returns null (no avatar support)
- [ ] Lobby player slots show avatar when available, hide when not
- [ ] Avatar load failure doesn't crash — slot shows without avatar

### Lobby Integration
- [ ] Creating a lobby sets rich presence "In Lobby (1/4)"
- [ ] Joining a lobby sets rich presence
- [ ] Starting game clears rich presence
- [ ] Leaving lobby clears rich presence
- [ ] Rich presence disabled when `RichPresenceEnabled = false`
- [ ] Lobby join still works identically without IdentityManager in scene

### Save Compatibility
- [ ] `SaveIdAssignmentSystem` uses platform ID for save file naming
- [ ] Existing save files with GUID-based IDs still load (no migration needed for local saves)
- [ ] `PlayerSaveId` FixedString64Bytes fits platform IDs (Steam64 = 17 digits, fits in 64 bytes)

### Editor Tooling
- [ ] Identity Inspector shows live provider state in play mode
- [ ] Mock provider overrides display name and avatar
- [ ] Config validator warns on missing SteamAppId
- [ ] Config validator warns on missing EOS credentials
- [ ] Avatar cache stats (count, memory, hit rate) displayed correctly

### Backward Compatibility
- [ ] Game runs without `PlatformIdentityConfig.asset` — no errors
- [ ] Game runs without `IdentityManager` in scene — no errors
- [ ] Existing multiplayer join flow works unchanged
- [ ] Existing save/load flow works unchanged
- [ ] No new ECS components on any entity
