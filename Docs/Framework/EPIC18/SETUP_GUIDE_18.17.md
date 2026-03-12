# EPIC 18.17: Multiplayer Damage Number Visibility — Setup Guide

**Status:** Implemented
**Last Updated:** March 5, 2026
**Requires:** EPIC 15.22 (DamageFeedbackProfile), EPIC 15.30 (DamageVisualQueue), EPIC 18.2 (GameplaySettingsPage), DamageNumbersPro (third-party)

---

## Overview

This system ensures all players see damage numbers in multiplayer (not just the host), and provides configurable visibility modes so designers can choose whether players see all damage or only their own.

| Component | Purpose |
|-----------|---------|
| **DamageVisibilityConfig** | Designer-configurable visibility policy (Resources/ SO) |
| **DamageVisibilityServerFilter** | Server-side RPC targeting per visibility mode |
| **DamageVisualRpcReceiveSystem** | Initializes DamageVisualQueue on remote clients; receives damage RPCs |
| **DamageApplicationSystem** | CRE → health + filtered visual RPCs via server filter |
| **DamageVisualRpcSendSystem** | Relay: creates RPCs outside prediction loop (prevents rollback destruction) |
| **DamageNumberVisibilitySettings** | Resolves effective visibility (config default vs player override, cached per-frame) |
| **CombatUIBridgeSystem** | Client-side defense-in-depth filter for all 5 modes |
| **GameplaySettingsPage** | Player-facing dropdown for visibility preference |

Key behaviors:
- **Zero setup required for basic multiplayer** — damage numbers work on all clients out of the box
- **Server-side enforcement** — server filters RPCs per-connection based on visibility mode
- **Client defense-in-depth** — client filter handles listen server + player overrides
- **5 visibility modes** — All, SelfOnly, Nearby, Party, None
- **Designer-configurable** — default visibility mode set on `DamageVisibilityConfig` ScriptableObject
- **Player-overridable** — optional settings dropdown (controlled by designer toggle)
- **No duplication** — listen server, remote client, and single player all display correctly

---

## Prerequisites

Before damage numbers display in multiplayer, you must have:

1. **DamageNumbersProAdapter** (or custom `DamageNumberAdapterBase` subclass) on a Canvas in your scene
2. **DamageFeedbackProfile** asset assigned to the adapter's `Feedback Profile` field
3. **CombatUIBootstrap** in your scene (for hitmarkers, combo, directional damage)
4. **CombatUIPlayerBindingSystem** calling `CombatUIBridgeSystem.SetPlayerEntity()` when the local player spawns

If these are already set up (from EPIC 15.22 / 15.30), damage numbers will work in multiplayer with zero additional configuration.

---

## Step 1: Verify Existing Setup

Before configuring visibility, confirm that the base damage number system is working.

### Checklist

- [ ] Scene has a Canvas with `DamageNumbersProAdapter` (or subclass) component
- [ ] `DamageNumbersProAdapter.Feedback Profile` field is assigned (not null)
- [ ] Scene has `CombatUIBootstrap` component
- [ ] `CombatUIPlayerBindingSystem` exists and calls `SetPlayerEntity()` on player spawn
- [ ] No warnings in console at startup:
  - `[CombatUI] No IDamageNumberProvider registered` = missing adapter
  - `[CombatUI] CombatUIBootstrap.Instance is null` = missing bootstrap
  - `[CombatUI] _playerEntity is Entity.Null` = player binding not working

> **Note:** These diagnostics appear after ~60 frames (~1 second) to allow MonoBehaviour registration and player spawn.

---

## Step 2: Create DamageVisibilityConfig Asset

Visibility policy is configured on a **DamageVisibilityConfig** ScriptableObject in `Resources/`.

### Create the Config

1. Right-click in `Assets/Resources/` → **Create > DIG > Combat > Damage Visibility Config**
2. Name it `DamageVisibilityConfig` (must match for `Resources.Load`)

### Configure Visibility Policy

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Default Visibility** | `DamageNumberVisibility` enum | `All` | What players see by default |
| **Allow Player Visibility Override** | `bool` | `true` | Whether the player settings dropdown is shown |
| **Nearby Distance** | `float` | `50` | Max distance (meters) for Nearby visibility mode |

### Visibility Modes

| Mode | Server Behavior | Client Behavior | Use Case |
|------|----------------|-----------------|----------|
| **All** | Broadcast RPCs to all | Show everything | Co-op PvE (Diablo 4, Lost Ark) |
| **Self Only** | Targeted RPC to attacker only | Filter to own damage | Competitive PvP (Destiny 2) |
| **Nearby** | Targeted RPCs to players within NearbyDistance | Distance check | Large-scale PvE (MMO) |
| **Party** | Targeted RPCs to party members | Party membership check | Group content |
| **None** | No RPCs sent | Block all damage numbers | Accessibility / minimalist |

> **Recommendation:** Use `All` for co-op/PvE. Use `SelfOnly` for competitive PvP. Environment damage (SourceNetworkId == -1) always broadcasts and always shows regardless of mode.

---

## Step 3: Player Override (Optional)

If `Allow Player Visibility Override` is `true` on the config, players will see a **"Damage Number Visibility"** dropdown in:

**Settings > Gameplay > HUD section**

| Dropdown Option | Maps To |
|-----------------|---------|
| All Players | `DamageNumberVisibility.All` |
| Self Only | `DamageNumberVisibility.SelfOnly` |
| Nearby | `DamageNumberVisibility.Nearby` |
| Party Only | `DamageNumberVisibility.Party` |
| None | `DamageNumberVisibility.None` |

The player's choice is stored in `PlayerPrefs` key `Settings_DmgNumVisibility` and persists across sessions.

> **Note:** Player override is client-side defense-in-depth. The server still uses the global `DamageVisibilityConfig.DefaultVisibility` for RPC filtering. A player choosing `SelfOnly` when the server is set to `All` will still receive all RPCs — the client simply filters them before display.

### Disabling Player Override

If you don't want players to change visibility (e.g., competitive game where everyone must see the same thing):

1. Open the `DamageVisibilityConfig` asset in `Assets/Resources/`
2. Uncheck **Allow Player Visibility Override**
3. The dropdown will no longer appear in gameplay settings

---

## Step 4: Verify Multiplayer Behavior

### Single Player (Editor Play Mode)

1. Enter Play Mode
2. Attack an enemy — damage numbers should appear
3. Get hit — defensive text (BLOCKED, PARRIED, IMMUNE) should appear
4. Miss — "Miss" text should appear
5. No duplicate numbers (each hit shows exactly one number)

### Listen Server (Two Editors or Build + Editor)

1. Start a listen server host
2. Connect a client
3. **Host attacks enemy** → both host and client see the damage number
4. **Client attacks enemy** → both host and client see the damage number
5. Defensive/miss text visible to both players
6. No duplicate numbers on host

### Remote Client (Dedicated Server or Build)

1. Connect to a remote server
2. Attack an enemy — damage numbers appear
3. Another player attacks same enemy — their damage numbers also appear (if visibility = All)
4. Switch to SelfOnly in settings — only your damage numbers appear; other players' damage hidden

---

## Step 5: Verify Source Attribution

Damage numbers carry `SourceNetworkId` to identify who dealt the damage:

| Source | SourceNetworkId Value | Behavior in SelfOnly Mode |
|--------|----------------------|---------------------------|
| Local player's weapons | Player's `GhostOwner.NetworkId` | **Shown** |
| Other player's weapons | Other player's NetworkId | **Hidden** |
| Environment / AOE / DOT | `-1` | **Always shown** |

> **Note:** The DamageEvent pipeline (grenades, AOE, hazards) currently sets `SourceNetworkId = -1` because `DamageEvent.SourceEntity` attribution is not available in the Burst-compiled jobs. This means environment damage is always visible regardless of visibility mode.

---

## Architecture Reference

### How Damage Numbers Reach Each Client

```
Server World:
  DamageVisibilityServerFilter → rebuilds NetworkId→Connection maps + reads config
  CombatResolutionSystem → CombatResultEvent
  DamageApplicationSystem → DamageVisualQueue (shared static) + FILTERED RPCs (direct)
  DamageEventVisualBridgeSystem (PredictedFixedStep)
    → DamageVisualQueue (shared static) + queues RPCs to PendingServerRpcs
  DamageVisualRpcSendSystem (SimulationSystemGroup, after prediction)
    → drains PendingServerRpcs → FILTERED RPCs via server filter
    (relay pattern: avoids prediction rollback destroying RPC entities)

Listen Server Host (ClientWorld):
  DamageVisualRpcReceiveSystem → skips enqueue (isListenServer = true, shared static already populated)
    → still destroys RPC entities (batch) to prevent age warnings
  CombatUIBridgeSystem → dequeues DamageVisualQueue → defense-in-depth filter → UI

Remote Client (ClientWorld only):
  DamageVisualRpcReceiveSystem (manual EntityQuery, SystemBase)
    → OnCreate: Initialize queue. OnUpdate: RPC → DamageVisualQueue (batch destroy)
  CombatUIBridgeSystem → dequeues DamageVisualQueue → defense-in-depth filter → UI
```

### Settings Resolution Order

```
1. Player override (PlayerPrefs "Settings_DmgNumVisibility")
   ↓ (only if DamageVisibilityConfig.AllowPlayerVisibilityOverride = true)
2. Designer default (DamageVisibilityConfig.DefaultVisibility)
   ↓ (only if config exists in Resources/)
3. Fallback: DamageNumberVisibility.All
   (Result cached per-frame via Time.frameCount)
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| No damage numbers on remote client | DamageVisualQueue not initialized | Verify `DamageVisualRpcReceiveSystem` exists in ClientWorld (check system inspector) |
| Duplicate numbers on listen server host | CRE defensive/miss path not removed | Verify CombatUIBridgeSystem no longer has `ShowDamageNumber` / `ShowMiss` in `ProcessCombatResult` |
| Visibility dropdown not showing | `AllowPlayerVisibilityOverride` is false | Check `DamageVisibilityConfig` in `Assets/Resources/` |
| No `DamageVisibilityConfig` found | Asset not in Resources/ | Create via Assets > Create > DIG > Combat > Damage Visibility Config, name `DamageVisibilityConfig`, save in `Assets/Resources/` |
| SelfOnly mode still shows other players' damage | Server filter not running | Verify `DamageVisibilityServerFilter` exists in ServerWorld system inspector |
| Party mode not filtering | Player not in party or `PartyLink` missing | Check player entity has `PartyLink` component pointing to valid party entity |
| Nearby shows nothing | NearbyDistance too small | Increase `NearbyDistance` on `DamageVisibilityConfig` (default 50m) |
| Environment damage hidden | `SourceNetworkId` is 0 instead of -1 | Check `DamageEventVisualBridgeSystem` Burst jobs set `SourceNetworkId = -1` |
| Settings change doesn't take effect | Stale PlayerPrefs cache | `DamageNumberVisibilitySettings.ClearPlayerOverride()` or restart — cache invalidates on `SetPlayerOverride()` / `ClearPlayerOverride()` |
| RPCs created but never arrive on remote client | Prediction rollback destroying entities | Verify `DamageVisualRpcSendSystem` exists in ServerWorld (creates RPCs outside prediction loop). RPCs must NOT be created during `PredictedFixedStepSimulationSystemGroup` |
| RPC receive system runs but matches zero entities | SystemAPI.Query source-gen mismatch | Verify `DamageVisualRpcReceiveSystem` uses manual `EntityQuery` (not `SystemAPI.Query`). Source-gen is unreliable for transient RPC entities |
| RPC age warnings in console | RPC entities not being destroyed | Verify `DamageVisualRpcReceiveSystem` calls `EntityManager.DestroyEntity(_rpcQuery)` — listen server must also destroy even though it skips enqueue |

---

## File Locations

| File | Purpose |
|------|---------|
| `Assets/Scripts/Combat/UI/DamageNumberVisibility.cs` | Visibility enum (All, SelfOnly, Nearby, Party, None) |
| `Assets/Scripts/Combat/UI/DamageVisibilityConfig.cs` | Visibility policy SO (Resources/) |
| `Assets/Scripts/Combat/UI/DamageNumberVisibilitySettings.cs` | Static settings resolver (cached per-frame) |
| `Assets/Scripts/Combat/UI/DamageVisualQueue.cs` | Shared queue + DamageVisualData struct |
| `Assets/Scripts/Combat/Systems/DamageVisualRpc.cs` | Network RPC struct |
| `Assets/Scripts/Combat/Systems/DamageVisualRpcReceiveSystem.cs` | Client-side RPC receiver (SystemBase, manual EntityQuery) |
| `Assets/Scripts/Combat/Systems/DamageVisualRpcSendSystem.cs` | RPC relay — creates entities outside prediction loop |
| `Assets/Scripts/Combat/Systems/DamageVisibilityServerFilter.cs` | Server-side RPC filtering system |
| `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs` | CRE → health + filtered RPCs |
| `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs` | DamageEvent → visuals (Burst) + queues RPCs for relay |
| `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs` | Queue → defense-in-depth filter → UI dispatch |
| `Assets/Scripts/Combat/UI/Config/DamageFeedbackProfile.cs` | Visual config SO (hit profiles, colors, prefabs) |
| `Assets/Scripts/Combat/UI/Adapters/DamageNumberAdapterBase.cs` | Base adapter class |
| `Assets/Scripts/Settings/Pages/GameplaySettingsPage.cs` | Player settings page (5-option dropdown) |
