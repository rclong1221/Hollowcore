# EPIC 17.14: Platform Identity & Profile Provider — Setup Guide

## Overview

The Platform Identity system is a modular abstraction layer that decouples player identity from any single platform SDK. It supports Steam, Epic Online Services, GOG Galaxy, Discord, and an offline/local fallback through a single swappable provider interface. The system provides platform display names, avatar textures with an LRU cache, friend lists, rich presence, and lobby invites — all driven by a single ScriptableObject config.

The entire system is **opt-in**. Without any setup, the game runs identically to before — `PlayerIdentity.Local` uses PlayerPrefs GUIDs. No ECS components, no ghost archetypes, no network traffic. The identity layer is purely MonoBehaviour-based and integrates with the existing lobby system (EPIC 17.4) and persistence pipeline (EPIC 16.15) without modifying any existing APIs.

---

## Table of Contents

1. [Quick Start (Local/Offline)](#1-quick-start-localoffline)
2. [Creating the Config Asset](#2-creating-the-config-asset)
3. [Adding IdentityManager to the Scene](#3-adding-identitymanager-to-the-scene)
4. [Provider Selection](#4-provider-selection)
5. [Installing Platform SDKs](#5-installing-platform-sdks)
6. [Avatar Setup in Lobby UI](#6-avatar-setup-in-lobby-ui)
7. [Rich Presence](#7-rich-presence)
8. [Using Identity from Gameplay Code](#8-using-identity-from-gameplay-code)
9. [Editor Tooling — Identity Inspector](#9-editor-tooling--identity-inspector)
10. [Resource Asset Checklist](#10-resource-asset-checklist)
11. [Performance Budget](#11-performance-budget)
12. [Multiplayer Considerations](#12-multiplayer-considerations)
13. [Backward Compatibility](#13-backward-compatibility)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. Quick Start (Local/Offline)

To enable the identity system with the local fallback provider (no platform SDK needed):

1. Create a config asset (Section 2)
2. Add `IdentityManager` to the scene (Section 3)
3. Enter Play Mode — the Local provider activates automatically

Without these steps, the game runs identically to before. The identity system never interferes with existing functionality.

---

## 2. Creating the Config Asset

1. In the Project window, navigate to `Assets/Resources/` (create the folder if it doesn't exist).
2. Right-click → **Create → DIG → Identity → Platform Identity Config**.
3. Name the asset **`PlatformIdentityConfig`** (exact name required — loaded via `Resources.Load`).

### Config Fields

| Field | Default | Description |
|-------|---------|-------------|
| **Active Provider Type** | Auto | Which platform to use. `Auto` detects from installed SDKs based on scripting defines. `Local` forces PlayerPrefs fallback. Set to a specific platform to bypass detection. |
| **Allow Fallback** | true | If the selected provider fails to initialize, fall back to Local. Recommended: always keep enabled for development. |

#### Avatar Settings

| Field | Default | Description |
|-------|---------|-------------|
| **Avatar Cache Size** | 32 | Max cached avatar textures in memory (range: 8–128). 32 entries at Medium ≈ 512 KB uncompressed, ~128 KB compressed. |
| **Avatar Resolution** | Medium | Default request size: Small (32×32), Medium (64×64), Large (128×128). Medium is recommended for lobby slots. |
| **Default Avatar Sprite** | None | Fallback sprite shown when no avatar is available or the provider doesn't support avatars. Assign a placeholder image here. |

#### Platform Credentials

| Field | Default | When Required |
|-------|---------|---------------|
| **Steam App Id** | 0 | When Active Provider is Steam or Auto with `HAS_STEAMWORKS` defined |
| **Epic Product Id** | "" | When Active Provider is Epic or Auto with `HAS_EOS` defined |
| **Epic Sandbox Id** | "" | Same as above |
| **Epic Deployment Id** | "" | Same as above |
| **Epic Client Id** | "" | Same as above |
| **Epic Client Secret** | "" | Same as above |
| **GOG Client Id** | "" | When Active Provider is GOG or Auto with `HAS_GOG_GALAXY` defined |
| **GOG Client Secret** | "" | Same as above |
| **Discord Application Id** | 0 | When Active Provider is Discord or Auto with `HAS_DISCORD_SDK` defined |

> **Security Note:** Client secrets in ScriptableObjects are visible in the build. For production, consider encrypting or obfuscating these values. For development and playtesting, plain text is acceptable.

#### Rich Presence Settings

| Field | Default | Description |
|-------|---------|-------------|
| **Rich Presence Enabled** | true | Push presence status to the platform overlay (Steam, Discord, etc.). |
| **Rich Presence In Lobby** | "In Lobby ({0}/{1})" | Format string — `{0}` = current player count, `{1}` = max players. |
| **Rich Presence In Game** | "In Game - {0}" | Format string — `{0}` = map name (for future use). |

---

## 3. Adding IdentityManager to the Scene

1. Open your persistent/bootstrap scene (the one that contains `LobbyManager`).
2. Create an empty GameObject and name it **`IdentityManager`**.
3. Add the **`IdentityManager`** component (namespace: `DIG.Identity`).
4. Save the scene.

That's it. No inspector fields need to be configured — the manager loads `PlatformIdentityConfig` from Resources at runtime.

### Execution Order

The `IdentityManager` script has `[DefaultExecutionOrder(-200)]`, so it always runs **before** `LobbyManager` (which uses the default order). This ensures `PlayerIdentity.Local` is populated from the platform provider before the lobby system reads it.

### Lifecycle

- `Awake()` — Singleton setup, `DontDestroyOnLoad`, loads config from Resources.
- `Start()` — Initializes the provider asynchronously. On success, populates `PlayerIdentity.Local`.
- `Update()` — Calls `provider.Tick()` for SDK callbacks (e.g., Steam requires `SteamAPI.RunCallbacks()` every frame).
- `OnDestroy()` — Shuts down the provider and clears the avatar cache.

---

## 4. Provider Selection

### Auto Detection (Recommended)

Set **Active Provider Type** to `Auto` in the config asset. The system selects the first available platform based on scripting defines:

| Priority | Scripting Define | Provider Selected |
|----------|-----------------|-------------------|
| 1 | `HAS_STEAMWORKS` | Steam |
| 2 | `HAS_EOS` | Epic Online Services |
| 3 | `HAS_GOG_GALAXY` | GOG Galaxy |
| 4 | `HAS_DISCORD_SDK` | Discord |
| 5 | (none defined) | Local (PlayerPrefs GUID) |

### Manual Selection

Set **Active Provider Type** to a specific platform to bypass auto-detection. This is useful for:
- Builds targeting a single storefront
- Testing a specific provider in the editor
- Forcing Local mode during development

### Fallback Behavior

When **Allow Fallback** is enabled and the selected provider fails to initialize (e.g., Steam client not running, EOS credentials invalid), the system automatically falls back to `Local`. A warning is logged to the console.

When **Allow Fallback** is disabled and the provider fails, the identity state is set to `Failed` and `PlayerIdentity.Local` uses the PlayerPrefs path as a last resort.

### Provider Capabilities

Not all providers support all features:

| Provider | Display Name | Avatar | Friends | Presence | Invites |
|----------|-------------|--------|---------|----------|---------|
| Local | PlayerPrefs | No | No | No | No |
| Steam | Steam persona | Yes | Yes | Yes | Yes |
| Epic | EOS display name | No | Yes | Yes | No |
| GOG | Galaxy persona | Yes | Yes | Yes | No |
| Discord | Discord username | Yes | Yes | Yes | No |

Code that uses optional features (avatars, friends, etc.) should check the capability flags first. See Section 8.

---

## 5. Installing Platform SDKs

Each provider is behind a `#if` compile guard. To enable a provider:

1. **Install the SDK package** into your Unity project.
2. **Add the scripting define** to Player Settings.

### Step-by-Step: Adding Scripting Defines

1. Open **Edit → Project Settings → Player**.
2. Expand **Other Settings**.
3. Find **Scripting Define Symbols**.
4. Add the appropriate define (semicolon-separated if adding multiple).
5. Click **Apply**. Unity will recompile.

### SDK Reference

| Platform | Define to Add | SDK Source | Install Method |
|----------|---------------|------------|----------------|
| Steam | `HAS_STEAMWORKS` | [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) | Import `.unitypackage` into `Assets/Plugins/` |
| Epic | `HAS_EOS` | [Epic Online Services](https://dev.epicgames.com/docs/game-services/eos-platform-interface) | Import EOS Unity plugin |
| GOG | `HAS_GOG_GALAXY` | [GOG Galaxy SDK](https://docs.gog.com/galaxyapi/) | Import native DLLs + managed wrapper |
| Discord | `HAS_DISCORD_SDK` | [Discord Game SDK](https://discord.com/developers/docs/game-sdk/sdk-starter-guide) | Import native DLLs + C# wrapper |

### Steam Setup Walkthrough

1. Download Steamworks.NET from the GitHub releases page.
2. Import the `.unitypackage` into `Assets/Plugins/Steamworks.NET/`.
3. Add `HAS_STEAMWORKS` to **Scripting Define Symbols** (Project Settings → Player).
4. In the `PlatformIdentityConfig` asset, set **Steam App Id** to your app's Steam ID (from Steamworks partner site).
5. Create a `steam_appid.txt` file in the **project root** (same level as `Assets/`) containing only your App ID number. This is required for editor testing without launching through Steam.
6. Ensure the Steam client is running when entering Play Mode.

### No SDK Installed

If no SDK is installed and no defines are set, only the `Local` provider compiles. The system degrades gracefully to the existing PlayerPrefs behavior. Zero additional dependencies in the build, zero code from platform SDKs.

---

## 6. Avatar Setup in Lobby UI

The `LobbyPlayerSlotUI` component has a new optional field under the **Avatar (EPIC 17.14)** header:

| Field | Type | Description |
|-------|------|-------------|
| **Avatar** | RawImage | Displays the player's platform avatar texture. Leave unassigned to skip avatars entirely. |

### Wiring the Avatar in the Prefab

1. Open your lobby player slot prefab (the one with the `LobbyPlayerSlotUI` component).
2. Add a **RawImage** child element where you want the avatar displayed (e.g., 64×64 pixels, positioned to the left of the player name).
3. On the `LobbyPlayerSlotUI` component in the Inspector, drag the new RawImage into the **Avatar** field.
4. Save the prefab.

### How It Works

- When a slot is populated, the system checks the `AvatarCache` for a cached texture.
- **Cache hit**: The avatar displays immediately.
- **Cache miss**: The RawImage is hidden. An async load fires in the background. Once the texture arrives, the RawImage is shown with the avatar.
- The avatar loads once per player and is cached for the session. Subsequent `SetSlot` calls for the same player show the cached texture instantly.

### If the Avatar Field Is Not Assigned

If the **Avatar** field is left empty (null), no avatar code runs at all. The lobby slot behaves identically to pre-17.14. No performance cost, no visual change.

---

## 7. Rich Presence

Rich presence automatically pushes the player's current status to the platform overlay (e.g., Steam friends list, Discord activity).

### Automatic Updates

| Event | Presence Text |
|-------|--------------|
| Create or join a lobby | `"In Lobby (2/4)"` (using the format string from config) |
| Start game / transition | Cleared |
| Leave lobby | Cleared |

### Configuration

- To customize the format strings, edit **Rich Presence In Lobby** and **Rich Presence In Game** on the config asset.
- To disable entirely, uncheck **Rich Presence Enabled**.
- Rich presence only fires if the active provider supports it (`SupportsPresence = true`). The Local provider does not.

### No Additional Setup Required

Rich presence hooks are built into `LobbyManager` phase transitions. No prefab wiring or scene changes needed beyond creating the config asset and adding `IdentityManager`.

---

## 8. Using Identity from Gameplay Code

These are the APIs available to gameplay scripters and designers who need to read identity data or use platform features from their own code.

### Checking If Identity Is Ready

```csharp
var mgr = DIG.Identity.IdentityManager.Instance;
if (mgr != null && mgr.IsReady)
{
    string name = mgr.ActiveProvider.DisplayName;
    string id = mgr.ActiveProvider.PlatformId;
}
```

### Listening for Identity Events

```csharp
DIG.Identity.IdentityManager.Instance.OnIdentityReady += () =>
{
    // Identity is now available — safe to read DisplayName, PlatformId, etc.
};

DIG.Identity.IdentityManager.Instance.OnIdentityFailed += (reason) =>
{
    // All providers failed. Handle gracefully.
};
```

### Checking Provider Capabilities Before Use

```csharp
var provider = DIG.Identity.IdentityManager.Instance.ActiveProvider;

if (provider.SupportsAvatars)
{
    // Safe to request avatars
}

if (provider.SupportsFriends)
{
    var friends = provider.GetFriends();
    var onlineFriends = provider.GetOnlineFriends();
}

if (provider.SupportsInvites)
{
    provider.InviteToLobby(friendPlatformId, lobbyJoinCode);
}
```

### Loading an Avatar Manually

```csharp
DIG.Identity.IdentityManager.Instance.OnAvatarLoaded += (platformId, texture) =>
{
    // Use the loaded Texture2D
};
DIG.Identity.IdentityManager.Instance.LoadAvatarForPlayer(somePlatformId);
```

Or use the cache directly:

```csharp
if (DIG.Identity.AvatarCache.TryGet(platformId, out var texture))
{
    // Immediate cache hit
}
```

> **Note:** All identity APIs are null-safe. If `IdentityManager.Instance` is null (not in scene), calls simply do nothing. If the provider doesn't support a feature, methods return empty arrays or null.

---

## 9. Editor Tooling — Identity Inspector

### Opening

**Menu → DIG → Lobby Workstation**, then click the **Identity** tab in the sidebar.

### Play Mode — Provider State

| Display | Description |
|---------|-------------|
| **State** | Current lifecycle state: Uninitialized, Initializing, Ready, or Failed |
| **Provider** | Active provider type (Local, Steam, Epic, GOG, Discord) |
| **Platform ID** | The platform-specific user ID |
| **Display Name** | The platform display name |
| **Capabilities** | Which features the provider supports (Avatars, Friends, Presence, Invites) |

### Play Mode — Friend List

If the active provider supports friends, the inspector displays up to 20 friends with status indicators:
- **[In Game]** — playing DIG specifically
- **[Online]** — on the platform but not in DIG
- **[Offline]** — not online

The friend list refreshes every 2 seconds to avoid performance impact.

### Play Mode — Avatar Cache

| Stat | Description |
|------|-------------|
| **Cached Entries** | Number of textures currently in the LRU cache |
| **Hit Rate** | Percentage of cache hits vs misses |
| **Approx Memory** | Estimated memory usage (uncompressed) |

The **Clear Cache** button destroys all cached textures and resets stats. Useful for testing avatar loading behavior.

### Edit Mode — Config Validation

The inspector automatically checks the `PlatformIdentityConfig` asset and warns about:

| Check | Severity | Condition |
|-------|----------|-----------|
| Missing config asset | Warning | No `PlatformIdentityConfig.asset` in Resources/ |
| Steam App ID = 0 | Warning | Steam or Auto provider selected |
| Empty EOS credentials | Warning | Epic or Auto provider selected |
| Empty GOG Client ID | Warning | GOG or Auto provider selected |
| Discord App ID = 0 | Warning | Discord or Auto provider selected |
| Default Avatar Sprite not assigned | Info | No fallback avatar configured |
| Avatar cache size > 64 | Info | High memory usage for target platforms |

### Edit Mode — Scripting Defines

Shows which `HAS_*` defines are currently active in the project:

```
HAS_STEAMWORKS    — YES / no
HAS_EOS           — YES / no
HAS_GOG_GALAXY    — YES / no
HAS_DISCORD_SDK   — YES / no
```

This helps verify that the correct SDK packages are installed and defines are set.

### Edit Mode — Mock Provider

Preview fields for display name and platform ID. These are informational only and do not affect runtime behavior. To test fallback behavior, set the provider type to one without the SDK installed and enable **Allow Fallback** in the config.

---

## 10. Resource Asset Checklist

| # | Path | Required | Notes |
|---|------|----------|-------|
| 1 | `Assets/Resources/PlatformIdentityConfig.asset` | Recommended | Without it, `IdentityManager` logs a warning and uses Local provider. |

No other resource assets are required. The config asset is the only one loaded via `Resources.Load`. Platform SDK credentials are stored directly on this asset.

> **Tip:** The config asset name must be exactly `PlatformIdentityConfig`. If renamed, the system will not find it and will fall back to Local.

---

## 11. Performance Budget

| Operation | Cost | Frequency | Allocation |
|-----------|------|-----------|------------|
| `IdentityManager.Update()` (provider Tick) | < 0.05ms | Every frame | 0 B |
| `AvatarCache.TryGet()` (cache hit) | < 0.001ms | On lobby UI refresh | 0 B |
| `AvatarCache.GetOrLoadAsync()` (cache miss) | < 200ms | Once per player, async | 1 Texture2D |
| `Texture2D.Compress()` after avatar load | 1–5ms | Once per avatar load | 0 B |
| `PlayerIdentity.Local` property access | < 0.001ms | Cached after first call | 0 B |
| Rich presence update | < 0.1ms | On lobby phase change only | 1 string |
| Avatar cache memory (32 entries, Medium) | — | Constant | ~512 KB uncompressed, ~128 KB compressed |

**Zero per-frame allocations.** All async operations use `Task` (no coroutine GC). Avatar textures are pooled via the LRU cache with automatic eviction. Platform SDK callbacks are processed in the provider's `Tick()` with no managed allocations.

---

## 12. Multiplayer Considerations

| Aspect | Behavior |
|--------|----------|
| **Network impact** | **Zero.** No RPCs, no ghost components, no network traffic. Identity is resolved locally on each machine. |
| **ECS impact** | **Zero.** No `IComponentData`, no `IBufferElementData`, no ghost components. No change to any entity archetype. |
| **Save compatibility** | `SaveIdAssignmentSystem` reads `PlayerIdentity.Local.PlayerId`. This string is now populated from the platform provider instead of PlayerPrefs. Save file naming uses the same `PlayerSaveId` field — no migration needed. |
| **Cross-platform lobbies** | Each player resolves their own identity locally. The lobby system transmits `PlayerId` and `DisplayName` via `JoinRequestMessage` as before — these strings are now platform-sourced. |
| **Platform ID length** | Steam64 IDs are 17 digits, Epic Account IDs are 32 hex chars, GUIDs are 32 hex chars. All fit within `FixedString64Bytes` used by `PlayerSaveId`. |

---

## 13. Backward Compatibility

| Scenario | Behavior |
|----------|----------|
| No `PlatformIdentityConfig.asset` in Resources | `IdentityManager` logs a warning, uses Local provider |
| No `IdentityManager` component in scene | `PlayerIdentity.Local` uses PlayerPrefs path (identical to pre-17.14) |
| No platform SDK installed / no defines set | Auto-detect picks Local; behaves exactly like the current GUID system |
| `Avatar` field not wired on `LobbyPlayerSlotUI` | Null check skips all avatar code; no visual change |
| Rich presence disabled in config | No platform API calls; zero overhead |
| Existing `LobbyManager` calls unchanged | `CreateLobby`, `JoinLobby`, `SendChat`, etc. work identically |
| Existing `SaveIdAssignmentSystem` unchanged | Reads same `PlayerIdentity.Local.PlayerId` string property |
| Existing `JoinRequestMessage` serialization | Same fields (`PlayerId`, `DisplayName`) — just populated from platform instead of PlayerPrefs |

The entire system is opt-in. Nothing changes for existing setups until you explicitly add the `IdentityManager` to the scene and create the config asset.

---

## 14. Troubleshooting

### Console: "[Identity] No PlatformIdentityConfig found in Resources/"

**Cause:** The config asset is missing or misnamed.
**Fix:** Create the asset via **Create → DIG → Identity → Platform Identity Config** in `Assets/Resources/`. The file must be named exactly `PlatformIdentityConfig`.

### Console: "[Identity:Steam] SteamAPI.Init() failed. Is Steam running?"

**Cause:** The Steam client is not running, or the App ID is incorrect.
**Fix:**
1. Launch the Steam client and log in.
2. Verify the **Steam App Id** on the config asset matches your Steamworks partner portal.
3. Ensure `steam_appid.txt` exists in the project root (next to `Assets/`) containing your App ID.

### Provider falls back to Local unexpectedly

**Cause:** The selected provider failed to initialize and **Allow Fallback** is enabled.
**Fix:** Check the Console for `[Identity]` warnings. Common causes:
- SDK not installed (check Scripting Defines in the Identity Inspector).
- Credentials empty or incorrect on the config asset.
- Platform client not running (Steam, Discord, GOG Galaxy).

### Avatars not showing in lobby

**Cause:** Multiple possible reasons.
**Fix — check in order:**
1. Is the **Avatar** `RawImage` field assigned on `LobbyPlayerSlotUI`? (Inspector check)
2. Does the active provider support avatars? (Local does not — check Identity Inspector capabilities)
3. Check the Avatar Cache stats in the Identity Inspector for hit/miss data.
4. Check Console for `[AvatarCache]` warnings indicating load failures.

### Display name shows a GUID like "Player_a1b2c3" instead of platform name

**Cause:** `PlayerIdentity.Local` was accessed before `IdentityManager` finished initializing.
**Fix:**
1. Verify `IdentityManager` is in the scene. It must exist in the same bootstrap scene as `LobbyManager`.
2. The script execution order is `-200` by default — this ensures it runs before `LobbyManager`. Do not change this.
3. If the issue persists, it may be a timing edge case on first launch. Exiting and re-entering Play Mode resolves it (the singleton caches correctly on the second access).

### Rich presence not updating on Steam/Discord

**Cause:** Either rich presence is disabled or the provider doesn't support it.
**Fix:**
1. Confirm **Rich Presence Enabled** is checked on the config asset.
2. Confirm the active provider supports presence (check Identity Inspector → Capabilities → Presence).
3. For Steam: `SteamAPI.RunCallbacks()` must run every frame. `IdentityManager.Update()` handles this automatically — verify the component is active and not disabled.

### Compile errors after adding a scripting define

**Cause:** The define was added but the SDK package is not installed.
**Fix:** The `#if HAS_*` guards compile the provider code, which references SDK types. If those types don't exist (no SDK imported), compilation fails. Either:
- Import the correct SDK package, or
- Remove the scripting define from Player Settings.

### "All identity providers failed" error

**Cause:** The selected provider failed AND **Allow Fallback** was disabled (or the Local fallback itself failed, which should never happen).
**Fix:** Enable **Allow Fallback** on the config asset. The Local provider always succeeds — it only reads/writes PlayerPrefs.
