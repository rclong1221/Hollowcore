# SETUP GUIDE 17.3: Player Trading System

**Status:** Implemented
**Last Updated:** February 23, 2026
**Requires:** Inventory System (InventoryItem buffer), Economy System (CurrencyInventory/CurrencyTransaction), Combat State (CombatState.IsInCombat), Interaction System (InteractionVerb.Trade)

This guide covers Unity Editor setup for the player-to-player trading system. After setup, two players can exchange items and currency through a server-authoritative trade window with anti-exploit protections.

---

## What Changed

Previously, the only way to transfer items between players was dropping them on the ground (lossy, no confirmation, exploitable).

Now:

- **Trade initiation** — Player A targets Player B and uses InteractionVerb.Trade to start a trade
- **Trade request** — Target player receives an accept/decline popup notification
- **Offer management** — Both players add/remove/modify item and currency offers in real time
- **Confirmation protocol** — Both players must confirm; any offer change resets the other player's confirmation
- **Atomic swap** — Server validates all items, currency, weight, and proximity before executing the trade in a single pass
- **Anti-exploit protections** — Self-trade prevention, proximity checks, combat blocking, rate limiting, duplicate session blocking, audit logging
- **Trade Workstation** — Editor window with live trade monitor and audit log viewer

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Config bootstrap | Loads `TradeConfigSO` from `Resources/TradeConfig` on initialization, creates ECS singleton |
| Atomic execution | TradeExecutionSystem re-validates all items/currency/weight before any swap |
| Confirmation reset | Any offer change automatically resets the other player's confirmation |
| Proximity monitoring | TradeProximityCheckSystem cancels trades where players move too far apart |
| Combat monitoring | TradeCombatCheckSystem cancels trades when either player enters combat |
| Timeout | TradeTimeoutSystem auto-cancels inactive sessions after configurable duration |
| Disconnect handling | TradeCancelReceiveSystem detects destroyed connections and cancels sessions |
| Currency pipeline | Trade uses existing CurrencyTransaction buffer — CurrencyTransactionSystem processes entries automatically |
| Weight tracking | InventoryCapacity.CurrentWeight updated during swap; IsOverencumbered recalculated |
| Audit logging | Every completed/failed trade logged to a 256-entry ring buffer on the config singleton |
| Session cleanup | Terminal sessions destroyed automatically after 1 frame (allows audit + UI to read final state) |

---

## 1. ScriptableObject Configuration

The trading system requires **one** ScriptableObject asset placed in `Assets/Resources/`.

### TradeConfigSO

**Create:** Right-click in Project → `Create > DIG > Trading > Trade Config`
**Save as:** `Assets/Resources/TradeConfig.asset`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Max Items Per Offer | int | 8 | Max distinct item entries per side in a single trade |
| Max Currency Per Offer | int | 3 | Max currency types per side (Gold + Premium + Crafting) |
| Proximity Range | float | 10 | Max meters between traders to initiate or maintain a trade |
| Timeout Seconds | float | 120 | Seconds before an inactive trade session auto-cancels |
| Cooldown Seconds | float | 5 | Minimum seconds between trade requests from the same player |
| Max Active Trades Per Player | int | 1 | Max concurrent trades per player (should be 1) |
| Allow Premium Currency Trade | bool | false | Whether Premium currency can be traded between players |

**Important:** Tick rate conversion happens at bootstrap. The SO stores seconds; the ECS singleton stores ticks. No manual tick math needed.

---

## 2. Player Prefab Setup

**No prefab changes required.** The trading system adds zero components to the player entity. Trade sessions are separate ephemeral entities that reference players by Entity.

The system reads existing components already on the player:
- `InventoryItem` buffer (from Inventory system)
- `InventoryCapacity` (from Inventory system)
- `CurrencyInventory` (from Economy system)
- `CurrencyTransaction` buffer (from Economy system)
- `CombatState` (from Combat system)
- `LocalTransform` (from Unity Transforms)

---

## 3. Trade Flow Overview

```
Player A                    Server                    Player B
    |                         |                          |
    |--- TradeRequestRpc ---->|                          |
    |                         |-- TradeSessionNotifyRpc->|  (popup)
    |                         |                          |
    |                         |<--- TradeAcceptRpc ------|  (accept)
    |<-- StateNotify(Active) -|-- StateNotify(Active) -->|  (both open window)
    |                         |                          |
    |--- OfferUpdateRpc ----->|                          |
    |<-- OfferSyncRpc --------|---- OfferSyncRpc ------->|  (both see update)
    |                         |                          |
    |--- TradeConfirmRpc ---->|                          |  (A confirms)
    |                         |<--- TradeConfirmRpc -----|  (B confirms)
    |                         |                          |
    |                    [ATOMIC SWAP]                   |
    |                         |                          |
    |<-- StateNotify(Done) ---|--- StateNotify(Done) --->|  (both close)
```

---

## 4. UI Setup

### 4a. Trade Window (TradeWindowView)

Add `TradeWindowView` MonoBehaviour to a UI GameObject. It implements `ITradeUIProvider` and registers with `TradeUIRegistry` automatically.

| Inspector Field | Type | Purpose |
|----------------|------|---------|
| My Offer Panel | TradeOfferPanel | Panel showing local player's offers (editable) |
| Their Offer Panel | TradeOfferPanel | Panel showing other player's offers (read-only) |
| Confirm Button | TradeConfirmButton | Confirm/Cancel button component |
| Cancel Button | Button | Standard Unity Button for cancel |
| Request Popup | GameObject | Accept/decline popup (hidden by default) |
| Request Text | Text | Display text for incoming trade request |
| Accept Button | Button | Accept trade request |
| Decline Button | Button | Decline trade request |
| Trade Window | GameObject | Main trade window root (hidden by default) |

### 4b. Trade Offer Panel (TradeOfferPanel)

Add to two separate GameObjects — one for "My Offer" (editable), one for "Their Offer" (read-only).

| Inspector Field | Type | Purpose |
|----------------|------|---------|
| Is Editable | bool | True for "My Offer" panel, false for "Their Offer" |
| Item Slot Container | Transform | Parent transform for item slot UI elements |
| Gold Amount Text | Text | Displays gold amount offered |
| Premium Amount Text | Text | Displays premium amount offered |
| Crafting Amount Text | Text | Displays crafting currency amount offered |
| Empty Text | Text | Shown when no offers exist |

### 4c. Trade Confirm Button (TradeConfirmButton)

| Inspector Field | Type | Purpose |
|----------------|------|---------|
| Confirm Button | Button | The clickable button |
| Confirm Text | Text | Label text on the button |
| Ready Label | string | Text shown when ready to confirm ("Confirm") |
| Waiting Label | string | Text shown after confirming ("Waiting...") |
| Trading Label | string | Text shown when both confirmed ("Trading...") |

---

## 5. Custom UI Integration

If building a custom trade UI, implement `ITradeUIProvider` and register with `TradeUIRegistry`:

| Callback | When Called | What to Do |
|----------|-----------|------------|
| `OnTradeRequested(int ghostId)` | Incoming trade request | Show accept/decline popup |
| `OnTradeSessionStarted()` | Both players in session | Open trade window |
| `OnTradeSessionCancelled(reason)` | Session cancelled | Close window, show reason |
| `OnTradeCompleted(success)` | Trade finished | Close window, show result |
| `OnOfferUpdated(my[], their[])` | Offer changed | Refresh offer displays |
| `OnConfirmStateChanged(i, they)` | Confirm state changed | Update button states |

Use `TradeRpcHelper` static methods to send RPCs from MonoBehaviours:
- `TradeRpcHelper.SendAccept()` — Accept incoming trade request
- `TradeRpcHelper.SendConfirm()` — Confirm your side of the trade
- `TradeRpcHelper.SendCancel(reason)` — Cancel the trade
- `TradeRpcHelper.SendOfferUpdate(...)` — Modify an offer

---

## 6. Anti-Exploit Protections

| Protection | Behavior | Configurable |
|-----------|----------|-------------|
| Self-trade | Cannot trade with yourself | No (hardcoded) |
| Proximity | Must be within ProximityRange to start; cancelled at 1.5x range | ProximityRange in SO |
| Combat block | Cannot trade while in combat; cancelled if combat starts | No (uses CombatState) |
| Rate limiting | CooldownSeconds between trade requests | CooldownSeconds in SO |
| Duplicate session | Only 1 active trade per player | MaxActiveTradesPerPlayer |
| Item re-validation | Items re-checked at execution time (not stale from offer time) | No (hardcoded) |
| Currency re-validation | Balances re-checked at execution time | No (hardcoded) |
| Confirm reset | Any offer change resets the other player's confirmation | No (hardcoded) |
| Weight check | Receiver must have capacity for incoming items | No (uses InventoryCapacity) |
| Timeout | Sessions auto-cancel after TimeoutSeconds | TimeoutSeconds in SO |

---

## 7. Editor Tooling

### Trade Workstation

Open via **DIG > Trade Workstation** menu.

| Tab | Purpose | Requires |
|-----|---------|----------|
| Active Trades | Live list of all trade session entities: participants, state, offer count, tick | Play Mode |
| Trade History | Audit log ring buffer viewer: search by ghost ID, result codes, currency deltas | Play Mode |

---

## 8. Backward Compatibility

| Existing System | Impact |
|-----------------|--------|
| InventoryItem buffer | READ + WRITE by TradeExecutionSystem (same pattern as ItemPickupSystem) |
| CurrencyInventory | READ by validation |
| CurrencyTransaction buffer | WRITE by TradeExecutionSystem (processed by existing CurrencyTransactionSystem) |
| CurrencyTransactionSystem | Unchanged — handles trade entries like any other source |
| InteractionVerb enum | Trade=15 already exists (added by EPIC 17.2) |
| CombatState | READ-ONLY |
| Player ghost prefab | Zero new components — no archetype impact |

---

## 9. Verification Checklist

### Core Flow
- [ ] Enter Play Mode → console shows `[TradeBootstrap] Loaded config`
- [ ] Player A targets Player B with InteractionVerb.Trade → request sent
- [ ] Player B receives trade request popup → accepts → trade window opens for both
- [ ] Player A adds items to offer → both UIs update → B's confirm resets
- [ ] Both players confirm → items and currency swapped correctly
- [ ] Trade window closes on completion

### Anti-Exploit
- [ ] Self-trade → rejected
- [ ] Player in combat → trade request rejected
- [ ] Combat during trade → session cancelled
- [ ] Players too far apart → session cancelled (after 1.5x hysteresis)
- [ ] Rapid requests → rate limited
- [ ] Player already in trade → new request rejected
- [ ] Item removed from inventory before execution → trade fails safely
- [ ] Weight capacity exceeded → trade fails safely
- [ ] Premium currency with AllowPremiumCurrencyTrade=false → rejected

### Edge Cases
- [ ] Player disconnects during trade → session cancelled, other player notified
- [ ] Session exceeds timeout → auto-cancelled
- [ ] Empty trade (no offers) → both confirm → completes (no-op swap)

### Editor
- [ ] DIG > Trade Workstation opens
- [ ] Active Trades tab shows live sessions in Play Mode
- [ ] Trade History tab shows audit log entries

---

## 10. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `[TradeBootstrap] No TradeConfigSO found` | Missing ScriptableObject | Create via `Create > DIG > Trading > Trade Config`, save to `Resources/TradeConfig` |
| Trade request silently fails | Target ghost ID not resolved | Verify target player entity has `GhostInstance` component |
| No trade UI appears | No ITradeUIProvider registered | Add `TradeWindowView` to a UI GameObject in the scene |
| `[TradeUIBridge] No ITradeUIProvider registered after 120 frames` | UI not set up | Same as above |
| Trade cancelled immediately | Proximity or combat check failed | Check ProximityRange in config; verify neither player is in combat |
| "InvalidSession" failure at execution | Item/currency changed between confirm and execute | Normal anti-exploit behavior — player should retry |
| Offers not syncing to other client | TradeOfferSyncRpc not reaching client | Check network connection; verify TradeUIBridgeSystem is running in Client world |
| Weight check failing | Receiver inventory too full | Increase InventoryCapacity.MaxWeight or trade fewer items |

---

## 11. Relationship to Other EPICs

| EPIC | Integration |
|------|-------------|
| EPIC 16.6 (Economy) | Trade uses existing CurrencyTransaction pipeline for atomic currency transfer |
| EPIC 16.1 (Interaction) | InteractionVerb.Trade triggers trade initiation |
| EPIC 16.15 (Persistence) | Post-trade inventory/currency changes saved by existing dirty tracking |
| EPIC 17.2 (Party) | Party members could have reduced cooldowns (future extension) |
| EPIC 16.12 (Quests) | Quest objectives can target trade completion (future extension) |
