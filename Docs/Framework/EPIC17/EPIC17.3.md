# EPIC 17.3: Player Trading System

**Status:** PLANNED
**Priority:** Medium (Social Economy Feature)
**Dependencies:**
- `InventoryItem` IBufferElementData (existing -- `DIG.Shared.InventoryComponents.cs`, Ghost:AllPredicted, InternalBufferCapacity=8, ResourceType+Quantity)
- `InventoryCapacity` IComponentData (existing -- `DIG.Shared.InventoryComponents.cs`, Ghost:AllPredicted, MaxWeight/CurrentWeight/IsOverencumbered)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy.Components.CurrencyInventory.cs`, Ghost:AllPredicted, Gold/Premium/Crafting, 12 bytes)
- `CurrencyTransaction` IBufferElementData (existing -- `DIG.Economy.Components.CurrencyTransaction.cs`, InternalBufferCapacity=4, NOT ghost-replicated, processed by CurrencyTransactionSystem)
- `CurrencyType` enum (existing -- `DIG.Economy.Components.CurrencyType.cs`, Gold=0/Premium=1/Crafting=2)
- `CurrencyTransactionSystem` (existing -- `DIG.Economy.Systems.CurrencyTransactionSystem.cs`, Server|Local, validates balance >= 0, clears buffer each frame)
- `ResourceType` enum (existing -- `DIG.Shared.ResourceTypes.cs`, None=0 through Isotope=7)
- `InteractionVerb` enum (existing -- `DIG.Interaction.InteractableComponents.cs`, values 0-14, need to add Trade=15)
- `InteractableContext` IComponentData (existing -- `DIG.Interaction.InteractableComponents.cs`, Verb+ActionNameKey+RequireLineOfSight)
- `InteractionCompleteEvent` IComponentData (existing -- `DIG.Interaction.InteractableComponents.cs`, InteractableEntity+InteractorEntity)
- `CommandTarget` IComponentData (existing -- `Unity.NetCode`, maps connection entity to player entity)
- `ReceiveRpcCommandRequest` IComponentData (existing -- `Unity.NetCode`, RPC source connection)
- `GhostOwnerIsLocal` tag (existing -- `Unity.NetCode`, identifies local player entity on client)
- `CombatState.IsInCombat` (existing -- `DIG.Combat.Components.CombatStateComponents.cs`, Ghost:All, blocks trading)
- `StatAllocationRpcReceiveSystem` pattern (existing -- `DIG.Progression.Systems.StatAllocationRpcReceiveSystem.cs`, RPC receive + CommandTarget resolution + validation)
- `CombatUIRegistry` / `CombatUIBridgeSystem` pattern (existing -- `DIG.Combat.UI`, static registry + provider interface + managed bridge system)
- `SaveStateLink` child entity pattern (existing -- `DIG.Persistence.Components.SaveStateComponents.cs`, 8 bytes on player)

**Feature:** A server-authoritative player-to-player trading system enabling item and currency exchange between two players via an atomic swap model. Trade sessions are separate entities (zero new components on player archetype). Both players must confirm before the server validates ownership, balance, capacity, and executes the swap in a single ECB pass. Includes anti-exploit protections (rate limiting, proximity checks, combat blocking, self-trade prevention), a complete trade audit log, and editor tooling for monitoring live trades.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `InventoryItem` buffer (ResourceType, Quantity) | `InventoryComponents.cs` | Ghost:AllPredicted, Cap=8 | Player items, already replicated to owning client |
| `InventoryCapacity` (MaxWeight, CurrentWeight) | `InventoryComponents.cs` | Ghost:AllPredicted | Weight tracking, IsOverencumbered flag |
| `CurrencyInventory` (Gold/Premium/Crafting) | `CurrencyInventory.cs` | Ghost:AllPredicted, 12 bytes | Readable by owning client for UI |
| `CurrencyTransaction` buffer | `CurrencyTransaction.cs` | NOT ghost, Cap=4 | Transient, processed same frame by CurrencyTransactionSystem |
| `CurrencyTransactionSystem` | `CurrencyTransactionSystem.cs` | Server\|Local | Validates balance >= 0, clears buffer each frame |
| `InteractionVerb` enum | `InteractableComponents.cs` | Values 0-14 | Trade=15 slot available |
| `InteractableContext` | `InteractableComponents.cs` | IComponentData | Verb + localization key for UI prompts |
| `CommandTarget` → player entity resolution | `StatAllocationRpcReceiveSystem.cs` | Pattern established | Connection → CommandTarget.targetEntity → player |
| `CombatState.IsInCombat` | `CombatStateComponents.cs` | Ghost:All | Can block trading when true |
| `ResourceWeights` singleton | `InventoryComponents.cs` | IComponentData | Per-resource-type weight values |

### What's Missing

- **No trade initiation** -- no way for one player to request a trade with another
- **No trade session entity** -- no container for tracking a two-party exchange
- **No trade offer model** -- no data structure for items/currency being offered by each side
- **No confirmation protocol** -- no two-phase confirm with mutual acknowledgment
- **No atomic swap** -- no system to validate and execute the exchange atomically
- **No trade RPCs** -- no network messages for trade request/offer/confirm/cancel
- **No anti-exploit checks** -- no rate limiting, proximity, self-trade, or combat blocking
- **No trade UI** -- no trade window, offer panels, or confirmation dialogs
- **No trade logging** -- no audit trail for anti-cheat investigation
- **No editor tooling** -- no live trade monitor or trade history viewer

---

## Problem

DIG has a functional economy (currency, inventory, item pickups, crafting) but no mechanism for players to exchange items or currency directly. The only way to transfer wealth is dropping items on the ground (lossy, exploitable, no confirmation). Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `InventoryItem` buffer on player entity (ghost-replicated) | No system reads two players' inventories to execute a swap |
| `CurrencyInventory` with Gold/Premium/Crafting | No currency transfer between players |
| `CurrencyTransaction` buffer for atomic currency modifications | No two-party atomic transaction (both sides in one pass) |
| `InteractionVerb` enum with 15 values | No `Trade = 15` value for player-to-player interaction |
| `CombatState.IsInCombat` on player entity | No system checks combat state before allowing trade |
| `CommandTarget` for RPC → player entity resolution | No trade-specific RPCs |
| `CombatUIRegistry` static registry pattern | No trade UI registry or provider interface |

**The gap:** Two players standing next to each other have no way to exchange items or currency. There is no economy-level social interaction. Guild leaders cannot distribute loot. Crafters cannot sell their output. The entire player economy is isolated per-player.

---

## Architecture Overview

```
                    TRADE SESSION ENTITY (Server-Only Lifecycle)
                              |
  TradeSessionState           TradeOffer buffer          TradeConfirmState
  (Initiator, Target,        (Side, SlotIndex,           (InitiatorConfirmed,
   State enum, CreationTick,  Quantity, CurrencyType,     TargetConfirmed)
   LastModifiedTick)          CurrencyAmount)
                              |
                    RPC LAYER (Client → Server)
                              |
  TradeRequestRpc             TradeOfferUpdateRpc        TradeConfirmRpc     TradeCancelRpc
  (TargetGhostId)            (Action, SlotIndex,          (no fields)         (Reason)
                              Quantity, CurrencyType,
                              CurrencyAmount)
                              |
                    SYSTEM PIPELINE (SimulationSystemGroup, Server|Local)
                              |
  TradeRequestReceiveSystem ─── validates proximity, combat, rate limit
  (creates TradeSession entity via ECB)
           |
  TradeOfferReceiveSystem ─── validates ownership, updates TradeOffer buffer
  (resets other player's confirmation on any change)
           |
  TradeConfirmReceiveSystem ─── sets confirm flag, checks both confirmed
           |
  TradeExecutionSystem ─── ATOMIC: validates ALL, swaps items+currency in one ECB pass
  (destroys session on success or rollback)
           |
  TradeTimeoutSystem ─── cancels stale sessions past TimeoutTicks
           |
  TradeCancelReceiveSystem ─── handles voluntary cancellation
           |
  TradeAuditSystem ─── writes TradeLogEntry to ring buffer singleton
                              |
                    PRESENTATION LAYER (PresentationSystemGroup)
                              |
  TradeUIBridgeSystem → TradeUIRegistry → ITradeUIProvider
  (managed, reads local player's active session, pushes offer state to UI)
           |
  TradeWindowView / TradeOfferPanel / TradeConfirmDialog (MonoBehaviours)
```

### Data Flow (Player A Trades with Player B)

```
Frame N (Client A):
  1. Player A targets Player B, presses interaction key
  2. InteractionVerb.Trade detected → client sends TradeRequestRpc(TargetGhostId)

Frame N+1 (Server):
  3. TradeRequestReceiveSystem:
     - Resolve connection → CommandTarget → Player A entity
     - Resolve TargetGhostId → Player B entity
     - Validate: A != B, both alive, neither in combat, proximity < ProximityRange
     - Validate: neither already in a trade session, rate limit not exceeded
     - Create TradeSession entity via ECB:
       - TradeSessionState{Initiator=A, Target=B, State=Pending, CreationTick=currentTick}
       - TradeOffer buffer (empty, capacity=16)
       - TradeConfirmState{false, false}
     - Send TradeSessionNotifyRpc to Player B (trade request popup)

Frame N+2 (Client B accepts):
  4. Player B sees popup, accepts → sends TradeConfirmRpc (or TradeAcceptRpc)
  5. Server sets TradeSessionState.State = Active

Frame N+3..M (Both players modify offers):
  6. Client sends TradeOfferUpdateRpc(Action=Add, SlotIndex=2, Quantity=5)
  7. TradeOfferReceiveSystem:
     - Validate: session active, player is participant, item exists in inventory, quantity valid
     - Add/Remove/Update TradeOffer buffer entry
     - Reset OTHER player's confirmed flag (any change invalidates confirmation)
     - Send TradeOfferSyncRpc to both clients (full offer state)

Frame M+1 (Player A confirms):
  8. Client A sends TradeConfirmRpc
  9. TradeConfirmReceiveSystem: sets InitiatorConfirmed = true

Frame M+2 (Player B confirms):
  10. Client B sends TradeConfirmRpc
  11. TradeConfirmReceiveSystem: sets TargetConfirmed = true
      Both confirmed → TradeSessionState.State = Executing

Frame M+3 (Server executes):
  12. TradeExecutionSystem (ATOMIC):
      a. Snapshot both players' InventoryItem buffers + CurrencyInventory
      b. Validate EVERY offered item: ownership (still in inventory), quantity (still sufficient)
      c. Validate EVERY offered currency: balance (still sufficient)
      d. Validate receiver capacity: weight check, slot check
      e. Validate no duplicate offers (same item offered twice)
      f. If ANY validation fails → State = Failed, send TradeCancelNotifyRpc(reason)
      g. If ALL pass → single ECB pass:
         - Remove items from A's InventoryItem buffer, add to B's
         - Remove items from B's InventoryItem buffer, add to A's
         - Write CurrencyTransaction entries for A (debit offered, credit received)
         - Write CurrencyTransaction entries for B (debit offered, credit received)
      h. State = Completed, send TradeCompleteNotifyRpc to both
      i. TradeAuditSystem logs the trade

Frame M+4 (Cleanup):
  13. TradeSession entity destroyed by TradeCleanupSystem
  14. Both clients close trade UI
```

### Critical System Ordering Chain

```
SimulationSystemGroup (Server|Local):
  TradeRequestReceiveSystem
      |
  TradeCancelReceiveSystem [UpdateAfter(TradeRequestReceiveSystem)]
      |
  TradeOfferReceiveSystem [UpdateAfter(TradeCancelReceiveSystem)]
      |
  TradeConfirmReceiveSystem [UpdateAfter(TradeOfferReceiveSystem)]
      |
  TradeExecutionSystem [UpdateAfter(TradeConfirmReceiveSystem)]
      |
  TradeTimeoutSystem [UpdateAfter(TradeExecutionSystem)]
      |
  TradeAuditSystem [UpdateAfter(TradeExecutionSystem)]
      |
  TradeCleanupSystem [UpdateAfter(TradeAuditSystem)]
      |
  [existing] CurrencyTransactionSystem — processes CurrencyTransaction entries from trade
```

---

## ECS Components

### Trade Session Entity (NOT on player -- separate entity)

**File:** `Assets/Scripts/Trading/Components/TradeSessionComponents.cs`

```
TradeSessionState (IComponentData, NOT ghost-replicated -- server-only entity)
  InitiatorEntity  : Entity    // Player A (trade requester)              8 bytes
  TargetEntity     : Entity    // Player B (trade recipient)              8 bytes
  State            : TradeState // enum: Pending/Active/Executing/
                               //        Completed/Failed/Cancelled       1 byte
  CreationTick     : uint      // NetworkTick when session was created    4 bytes
  LastModifiedTick : uint      // NetworkTick of last offer change        4 bytes
  InitiatorConnection : Entity // Connection entity for A (for RPCs)     8 bytes
  TargetConnection    : Entity // Connection entity for B (for RPCs)     8 bytes
                               // TOTAL:                                 41 bytes (padded to 44)
```

```
TradeState enum : byte
  Pending   = 0    // Waiting for target to accept
  Active    = 1    // Both players can modify offers
  Executing = 2    // Both confirmed, server validating
  Completed = 3    // Trade executed successfully (pending cleanup)
  Failed    = 4    // Validation failed (pending cleanup)
  Cancelled = 5    // One player cancelled (pending cleanup)
```

**File:** `Assets/Scripts/Trading/Components/TradeOffer.cs`

```
TradeOffer (IBufferElementData, InternalBufferCapacity=16, NOT ghost-replicated)
  OfferSide      : byte         // 0 = initiator, 1 = target              1 byte
  OfferType      : TradeOfferType // enum: Item=0, Currency=1             1 byte
  ItemSlotIndex  : byte         // Index into player's InventoryItem       1 byte
  ItemType       : ResourceType // Which resource (for validation)         1 byte
  Quantity       : int          // Item quantity offered                    4 bytes
  CurrencyType   : CurrencyType // Gold/Premium/Crafting                   1 byte
  CurrencyAmount : int          // Currency amount offered                 4 bytes
                                // TOTAL per element:                      13 bytes (padded to 16)
```

```
TradeOfferType enum : byte
  Item     = 0
  Currency = 1
```

**File:** `Assets/Scripts/Trading/Components/TradeConfirmState.cs`

```
TradeConfirmState (IComponentData, NOT ghost-replicated)
  InitiatorConfirmed : bool     //                                        1 byte
  TargetConfirmed    : bool     //                                        1 byte
                                // TOTAL:                                  2 bytes (padded to 4)
```

### On Player Entity -- NOTHING

**Zero new components on player entity.** Trade session is a standalone entity referencing players by Entity. The only player-entity touch point is reading existing `InventoryItem`, `CurrencyInventory`, `CombatState`, and `InventoryCapacity` components (all already present). Writing is done through existing `CurrencyTransaction` buffer and direct `InventoryItem` buffer manipulation.

### Singleton

**File:** `Assets/Scripts/Trading/Components/TradeConfig.cs`

```
TradeConfig (IComponentData -- singleton)
  MaxItemsPerOffer    : int    // Max distinct item entries per side       4 bytes
  MaxCurrencyPerOffer : int    // Max currency entries per side            4 bytes
  ProximityRange      : float  // Max distance between traders (meters)   4 bytes
  TimeoutTicks        : uint   // Ticks before session auto-cancels       4 bytes
  CooldownTicks       : uint   // Min ticks between trade requests        4 bytes
  MaxActiveTradesPerPlayer : int // Should be 1 (only one trade at a time) 4 bytes
  AllowPremiumCurrencyTrade : bool // Whether Premium can be traded       1 byte
                                   // TOTAL:                              25 bytes (padded to 28)
```

**File:** `Assets/Scripts/Trading/Components/TradeAuditLog.cs`

```
TradeAuditLog (IBufferElementData on singleton, InternalBufferCapacity=0)
  InitiatorGhostId : int       // Ghost ID of Player A                    4 bytes
  TargetGhostId    : int       // Ghost ID of Player B                    4 bytes
  Timestamp        : uint      // NetworkTick at completion               4 bytes
  ItemCount        : byte      // Total items exchanged                   1 byte
  GoldDelta        : int       // Net gold from A's perspective (+/-)     4 bytes
  PremiumDelta     : int       // Net premium from A's perspective        4 bytes
  CraftingDelta    : int       // Net crafting from A's perspective       4 bytes
  ResultCode       : byte      // 0=success, 1+=failure reason            1 byte
                               // TOTAL per element:                      26 bytes (padded to 28)
```

### RPCs

**File:** `Assets/Scripts/Trading/Components/TradeRpcs.cs`

```
TradeRequestRpc (IRpcCommand)
  TargetGhostId : int          // Ghost ID of target player               4 bytes

TradeAcceptRpc (IRpcCommand)
  SessionEntity : Entity       // Which session to accept                 8 bytes
                               // (only needed if multiple pending)

TradeOfferUpdateRpc (IRpcCommand)
  Action        : TradeOfferAction // Add=0, Remove=1, UpdateQty=2        1 byte
  OfferType     : TradeOfferType   // Item=0, Currency=1                  1 byte
  ItemSlotIndex : byte         // Index into local InventoryItem           1 byte
  Quantity      : int          // Quantity to add/set                      4 bytes
  CurrencyType  : CurrencyType // Which currency (for currency offers)    1 byte
  CurrencyAmount: int          // Amount (for currency offers)            4 bytes

TradeConfirmRpc (IRpcCommand)
  // Empty -- server resolves player from connection

TradeCancelRpc (IRpcCommand)
  Reason : TradeCancelReason   // Voluntary=0, Disconnect=1               1 byte

TradeOfferAction enum : byte
  Add       = 0
  Remove    = 1
  UpdateQty = 2
```

**Server-to-Client notification RPCs:**

```
TradeSessionNotifyRpc (IRpcCommand)
  InitiatorGhostId : int       // Who is requesting the trade             4 bytes
  SessionEntity     : Entity   // Session entity reference                8 bytes

TradeOfferSyncRpc (IRpcCommand)
  OfferSide      : byte        // Whose offer changed                     1 byte
  OfferType      : TradeOfferType                                         1 byte
  ItemSlotIndex  : byte                                                   1 byte
  ItemType       : ResourceType                                           1 byte
  Quantity       : int                                                    4 bytes
  CurrencyType   : CurrencyType                                          1 byte
  CurrencyAmount : int                                                    4 bytes
  Action         : TradeOfferAction                                       1 byte

TradeStateNotifyRpc (IRpcCommand)
  NewState       : TradeState  // Session state change                    1 byte
  FailReason     : byte        // 0=none, 1+=specific failure             1 byte

TradeCompleteNotifyRpc (IRpcCommand)
  Success : bool               // Whether trade succeeded                 1 byte

TradeCancelReason enum : byte
  Voluntary    = 0
  Disconnect   = 1
  Timeout      = 2
  TooFar       = 3
  EnteredCombat = 4
  InvalidSession = 5
```

---

## ScriptableObjects

### TradeConfigSO

**File:** `Assets/Scripts/Trading/Config/TradeConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Trading/Trade Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| MaxItemsPerOffer | int [Min(1)] | 8 | Max distinct item slots per side |
| MaxCurrencyPerOffer | int [Min(0)] | 3 | Max currency types per side (Gold+Premium+Crafting) |
| ProximityRange | float [Min(1)] | 10f | Max meters between traders |
| TimeoutSeconds | float [Min(10)] | 120f | Seconds before auto-cancel (converted to ticks at bootstrap) |
| CooldownSeconds | float [Min(1)] | 5f | Seconds between trade requests per player |
| MaxActiveTradesPerPlayer | int | 1 | Only one trade at a time |
| AllowPremiumCurrencyTrade | bool | false | Whether Premium currency can be traded |

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Client|Local):
  TradeBootstrapSystem                      -- loads TradeConfigSO from Resources/, creates singleton (runs once)

SimulationSystemGroup (Server|Local):
  TradeRequestReceiveSystem                 -- receives TradeRequestRpc, validates, creates session entity
  TradeCancelReceiveSystem                  -- receives TradeCancelRpc, destroys session
  TradeOfferReceiveSystem                   -- receives TradeOfferUpdateRpc, validates, updates buffer
  TradeConfirmReceiveSystem                 -- receives TradeConfirmRpc, sets flags, triggers execution
  TradeExecutionSystem                      -- atomic validation + swap (runs only when State==Executing)
  TradeTimeoutSystem                        -- cancels expired sessions
  TradeProximityCheckSystem                 -- cancels sessions where players moved too far apart
  TradeCombatCheckSystem                    -- cancels sessions where a player entered combat
  TradeAuditSystem                          -- logs completed/failed trades to ring buffer
  TradeCleanupSystem                        -- destroys Completed/Failed/Cancelled sessions (1-frame delay)
  [existing] CurrencyTransactionSystem      -- processes CurrencyTransaction entries written by TradeExecutionSystem

PresentationSystemGroup (Client|Local):
  TradeUIBridgeSystem                       -- managed bridge to UI registry
```

### TradeRequestReceiveSystem

**File:** `Assets/Scripts/Trading/Systems/TradeRequestReceiveSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
```

Validation chain:
1. Resolve `ReceiveRpcCommandRequest.SourceConnection` -> `CommandTarget.targetEntity` -> Player A
2. Resolve `TradeRequestRpc.TargetGhostId` -> Player B entity via ghost mapping
3. Reject if `A == B` (self-trade)
4. Reject if either entity has `CombatState.IsInCombat == true`
5. Reject if distance between `LocalTransform.Position` of A and B > `TradeConfig.ProximityRange`
6. Reject if either player already has an active `TradeSessionState` (query all sessions)
7. Reject if cooldown not elapsed (`LastTradeTick` tracked per-connection via `TradePlayerCooldown` cleanup component)
8. Create `TradeSession` entity via ECB with `TradeSessionState`, `TradeOffer` buffer, `TradeConfirmState`
9. Send `TradeSessionNotifyRpc` to Player B's connection

### TradeOfferReceiveSystem

**File:** `Assets/Scripts/Trading/Systems/TradeOfferReceiveSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TradeCancelReceiveSystem))]
```

Validation chain:
1. Resolve connection -> player entity
2. Find active `TradeSessionState` where player is initiator or target
3. Reject if `State != Active`
4. Determine `OfferSide` (0 if initiator, 1 if target)
5. For `Action=Add` (item): validate `ItemSlotIndex` < player's `InventoryItem` buffer length, validate `Quantity` <= buffer[index].Quantity, check `MaxItemsPerOffer` not exceeded, check no duplicate slot index in offers
6. For `Action=Add` (currency): validate `CurrencyAmount` <= player's balance, check `AllowPremiumCurrencyTrade` if Premium, check total offered currency of that type doesn't exceed balance
7. For `Action=Remove`: find matching offer entry, remove it
8. For `Action=UpdateQty`: find matching offer entry, validate new quantity
9. Update `TradeOffer` buffer
10. **Reset OTHER player's confirmed flag** (`TradeConfirmState.InitiatorConfirmed` if target modified, vice versa)
11. Update `TradeSessionState.LastModifiedTick`
12. Send `TradeOfferSyncRpc` to both connections

### TradeConfirmReceiveSystem

**File:** `Assets/Scripts/Trading/Systems/TradeConfirmReceiveSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TradeOfferReceiveSystem))]
```

1. Resolve connection -> player entity
2. Find active session
3. Set `InitiatorConfirmed` or `TargetConfirmed` based on which player sent RPC
4. If both `InitiatorConfirmed && TargetConfirmed`:
   - Set `TradeSessionState.State = Executing`

### TradeExecutionSystem (ATOMIC)

**File:** `Assets/Scripts/Trading/Systems/TradeExecutionSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TradeConfirmReceiveSystem))]
```

This is the critical system. It runs ONLY when `TradeSessionState.State == Executing`.

**Atomic validation pass (ALL must succeed or entire trade fails):**

```
1. Re-validate proximity (players may have moved since confirm)
2. Re-validate neither player is in combat
3. For EACH item offer from initiator:
   a. Verify item still exists at SlotIndex in initiator's InventoryItem buffer
   b. Verify item type matches (ResourceType)
   c. Verify quantity still sufficient
   d. Calculate weight for target's capacity check
4. For EACH item offer from target:
   a. Same checks as above for target's inventory
   b. Calculate weight for initiator's capacity check
5. For EACH currency offer:
   a. Verify balance still sufficient (CurrencyInventory)
6. Verify target can receive items: CurrentWeight + incoming weight <= MaxWeight
7. Verify initiator can receive items: same check
8. Check no duplicate items (same slot offered twice across separate Add actions)
```

**Execution pass (single ECB, all-or-nothing):**

```
If all validations pass:
  // Items: Initiator → Target
  For each initiator item offer:
    initiatorBuffer[slotIndex].Quantity -= offeredQuantity
    if Quantity == 0: remove entry (compact buffer)
    targetBuffer.Add(new InventoryItem{ResourceType, offeredQuantity})
    // OR if target already has that ResourceType: targetBuffer[match].Quantity += offeredQuantity

  // Items: Target → Initiator
  Same logic, reversed

  // Currency: via CurrencyTransaction buffer (existing system processes these)
  For each initiator currency offer:
    initiatorTransactions.Add(new CurrencyTransaction{Type, Amount=-offered, Source=sessionEntity})
    targetTransactions.Add(new CurrencyTransaction{Type, Amount=+offered, Source=sessionEntity})

  For each target currency offer:
    targetTransactions.Add(new CurrencyTransaction{Type, Amount=-offered, Source=sessionEntity})
    initiatorTransactions.Add(new CurrencyTransaction{Type, Amount=+offered, Source=sessionEntity})

  State = Completed
  Send TradeCompleteNotifyRpc(Success=true) to both

If any validation fails:
  State = Failed
  Send TradeCancelNotifyRpc(reason) to both
  // NO partial modifications -- nothing was written yet
```

### TradeTimeoutSystem

**File:** `Assets/Scripts/Trading/Systems/TradeTimeoutSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TradeExecutionSystem))]
```

Checks `currentTick - TradeSessionState.CreationTick > TradeConfig.TimeoutTicks`. Cancels session, notifies both players with `TradeCancelReason.Timeout`.

### TradeProximityCheckSystem

**File:** `Assets/Scripts/Trading/Systems/TradeProximityCheckSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TradeExecutionSystem))]
```

Every N frames (configurable, default every 30 ticks), checks distance between both players. If > `ProximityRange * 1.5` (hysteresis to avoid flicker), cancels with `TradeCancelReason.TooFar`.

### TradeCombatCheckSystem

**File:** `Assets/Scripts/Trading/Systems/TradeCombatCheckSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TradeExecutionSystem))]
```

Checks `CombatState.IsInCombat` on both participants each frame. If either enters combat, cancels with `TradeCancelReason.EnteredCombat`.

### TradeCleanupSystem

**File:** `Assets/Scripts/Trading/Systems/TradeCleanupSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TradeAuditSystem))]
```

Destroys session entities where `State` is `Completed`, `Failed`, or `Cancelled`. Runs 1 frame after terminal state to allow `TradeAuditSystem` and `TradeUIBridgeSystem` to read the final state.

---

## Authoring

No authoring component needed on player prefab. Trade sessions are runtime-created entities, not baked. The `TradeConfig` singleton is bootstrapped from a ScriptableObject via `TradeBootstrapSystem`.

### TradeBootstrapSystem

**File:** `Assets/Scripts/Trading/Systems/TradeBootstrapSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation |
                    WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
```

- Loads `TradeConfigSO` from `Resources/TradeConfig`
- Creates `TradeConfig` singleton entity
- Converts `TimeoutSeconds` / `CooldownSeconds` to tick counts using `NetworkTime.ServerTick` tick rate
- Creates `TradeAuditLog` buffer singleton (ring buffer, capacity 256)
- Self-disables after first run (same pattern as `SurfaceGameplayConfigSystem`)

---

## UI Bridge

### Static Registry

**File:** `Assets/Scripts/Trading/UI/TradeUIRegistry.cs`

```csharp
public static class TradeUIRegistry
{
    private static ITradeUIProvider _tradeUI;

    public static ITradeUIProvider TradeUI => _tradeUI;
    public static bool HasTradeUI => _tradeUI != null;

    public static void Register(ITradeUIProvider provider) { ... }
    public static void Unregister(ITradeUIProvider provider) { ... }
    public static void UnregisterAll() { _tradeUI = null; }
}
```

### Provider Interface

**File:** `Assets/Scripts/Trading/UI/ITradeUIProvider.cs`

```csharp
public interface ITradeUIProvider
{
    void OnTradeRequested(int requesterGhostId, string requesterName);
    void OnTradeSessionStarted();
    void OnTradeSessionCancelled(TradeCancelReason reason);
    void OnTradeCompleted(bool success);
    void OnOfferUpdated(TradeOfferSnapshot[] myOffers, TradeOfferSnapshot[] theirOffers);
    void OnConfirmStateChanged(bool iConfirmed, bool theyConfirmed);
}
```

### Bridge System

**File:** `Assets/Scripts/Trading/UI/TradeUIBridgeSystem.cs`

```
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
```

- Managed `SystemBase` (needs access to `TradeUIRegistry` static class)
- Reads incoming notification RPCs (`TradeSessionNotifyRpc`, `TradeOfferSyncRpc`, `TradeStateNotifyRpc`, `TradeCompleteNotifyRpc`)
- Maintains client-side trade state mirror (`TradeClientState` -- local struct, NOT an ECS component)
- Pushes updates to `ITradeUIProvider` via `TradeUIRegistry`
- Handles UI open/close lifecycle

### UI Views

**File:** `Assets/Scripts/Trading/UI/TradeWindowView.cs`
- Main trade window MonoBehaviour
- Two panels: "My Offer" and "Their Offer"
- Item slots populated from local `InventoryItem` buffer (read via `GhostOwnerIsLocal` query)
- Drag-and-drop items from inventory to offer panel
- Currency input fields (Gold, Crafting; Premium conditionally shown)

**File:** `Assets/Scripts/Trading/UI/TradeOfferPanel.cs`
- Displays one side's offer (items + currencies)
- Read-only for "Their Offer" panel
- Editable for "My Offer" panel (sends `TradeOfferUpdateRpc` on change)

**File:** `Assets/Scripts/Trading/UI/TradeConfirmButton.cs`
- Confirm/Cancel buttons
- Visual state: "Confirm" (default), "Waiting..." (confirmed, waiting for other), "Trading..." (both confirmed)
- Cancel sends `TradeCancelRpc`
- Confirm sends `TradeConfirmRpc`

---

## Editor Tooling

### TradeWorkstationWindow

**File:** `Assets/Editor/TradeWorkstation/TradeWorkstationWindow.cs`
- Menu: `DIG/Trade Workstation`
- Sidebar + `ITradeWorkstationModule` pattern

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Active Trades | `Modules/ActiveTradesModule.cs` | Live list of all TradeSession entities: participants, state, offers, time elapsed |
| Trade History | `Modules/TradeHistoryModule.cs` | Ring buffer viewer for TradeAuditLog: search by player, filter by result, sort by timestamp |
| Trade Simulator | `Modules/TradeSimulatorModule.cs` | Create mock trade between two entities in Play mode, test execution path |
| Config Editor | `Modules/TradeConfigModule.cs` | Inline editor for TradeConfigSO with live singleton update |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| **Player entity** | **0 bytes** | No new components on player |
| `TradeSessionState` | 44 bytes | Trade session entity (separate) |
| `TradeOffer` buffer header + capacity=16 | ~16 + 16*16 = 272 bytes | Trade session entity |
| `TradeConfirmState` | 4 bytes | Trade session entity |
| `TradeConfig` singleton | 28 bytes | Singleton entity |
| `TradeAuditLog` buffer | ~28 * N bytes | Singleton entity |
| **Total on player** | **0 bytes** | |

Trade session entities are ephemeral (created on trade start, destroyed on completion/cancel). They exist for seconds, not permanently. Zero impact on the player ghost prefab's 16KB archetype budget.

---

## Anti-Exploit Protections

| Protection | Implementation | System |
|------------|----------------|--------|
| **Self-trade** | Reject if `InitiatorEntity == TargetEntity` | TradeRequestReceiveSystem |
| **Proximity** | Reject if distance > ProximityRange; continuous check during session | TradeRequestReceiveSystem, TradeProximityCheckSystem |
| **Combat blocking** | Reject if `CombatState.IsInCombat` on either player; continuous check | TradeRequestReceiveSystem, TradeCombatCheckSystem |
| **Rate limiting** | Cooldown per-connection (CooldownTicks between requests) | TradeRequestReceiveSystem |
| **Duplicate sessions** | Reject if player already in active session | TradeRequestReceiveSystem |
| **Item duplication** | Final validation re-checks ownership/quantity before swap | TradeExecutionSystem |
| **Currency duplication** | Final validation re-checks balance before swap | TradeExecutionSystem |
| **Offer tampering** | All offer state is server-side; client sends RPCs, server validates | TradeOfferReceiveSystem |
| **Confirm reset** | Any offer change resets other player's confirmation | TradeOfferReceiveSystem |
| **Timeout** | Sessions auto-cancel after TimeoutTicks | TradeTimeoutSystem |
| **Disconnect** | Connection destroy event triggers session cancel | TradeCancelReceiveSystem |
| **Audit trail** | Every completed/failed trade logged to ring buffer | TradeAuditSystem |

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `TradeBootstrapSystem` | N/A | No | Runs once at startup |
| `TradeRequestReceiveSystem` | < 0.01ms | No | Only on trade request (very rare, ~0-1 per second) |
| `TradeOfferReceiveSystem` | < 0.01ms | No | Only on offer change (human input speed) |
| `TradeConfirmReceiveSystem` | < 0.01ms | No | At most 2 per trade session |
| `TradeExecutionSystem` | < 0.02ms | No | Runs once per trade (rare), iterates small buffers |
| `TradeTimeoutSystem` | < 0.01ms | No | Iterates active sessions (expect 0-5 concurrent) |
| `TradeProximityCheckSystem` | < 0.01ms | No | Distance check on 0-5 sessions, spread across frames |
| `TradeCombatCheckSystem` | < 0.01ms | No | Component read on 0-10 players |
| `TradeAuditSystem` | < 0.01ms | No | Append to ring buffer (rare) |
| `TradeCleanupSystem` | < 0.01ms | No | Destroy 0-5 entities |
| `TradeUIBridgeSystem` | < 0.02ms | No | Managed, RPC drain + registry push |
| **Total** | **< 0.10ms** | | All systems combined, worst case |

All trade systems operate on tiny data sets (0-5 active sessions, 0-16 offers per session). The bottleneck is human input rate, not computation. No Burst compilation needed -- managed SystemBase is sufficient for the data volumes involved.

---

## Backward Compatibility

| Existing System | Impact | Risk |
|-----------------|--------|------|
| `InventoryItem` buffer | READ + WRITE by TradeExecutionSystem | Low -- same buffer manipulation as ItemPickupSystem |
| `CurrencyInventory` | READ by validation, WRITE via CurrencyTransaction | Low -- uses existing CurrencyTransaction pipeline |
| `CurrencyTransaction` buffer | WRITE by TradeExecutionSystem | None -- appends entries, CurrencyTransactionSystem processes as normal |
| `CurrencyTransactionSystem` | Unchanged | None -- already handles arbitrary transaction sources |
| `InteractionVerb` enum | Add `Trade = 15` | None -- additive enum value, no existing code reads value 15 |
| `InteractableContext` | Unchanged (used if player entities get InteractableContext) | None |
| `CombatState` | READ-ONLY | None -- no modifications |
| `CommandTarget` | READ-ONLY | None -- standard RPC resolution pattern |
| Player ghost prefab | Unchanged | None -- zero new components |
| Persistence (EPIC 16.15) | No save module needed for trade sessions (ephemeral) | None |

---

## File Summary

### New Files (28)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Trading/Components/TradeSessionComponents.cs` | TradeSessionState + TradeState enum |
| 2 | `Assets/Scripts/Trading/Components/TradeOffer.cs` | IBufferElementData + TradeOfferType enum |
| 3 | `Assets/Scripts/Trading/Components/TradeConfirmState.cs` | IComponentData |
| 4 | `Assets/Scripts/Trading/Components/TradeConfig.cs` | IComponentData (singleton) |
| 5 | `Assets/Scripts/Trading/Components/TradeAuditLog.cs` | IBufferElementData (ring buffer) |
| 6 | `Assets/Scripts/Trading/Components/TradeRpcs.cs` | All IRpcCommand structs + enums |
| 7 | `Assets/Scripts/Trading/Components/TradePlayerCooldown.cs` | ICleanupComponentData on connection |
| 8 | `Assets/Scripts/Trading/Config/TradeConfigSO.cs` | ScriptableObject |
| 9 | `Assets/Scripts/Trading/Systems/TradeBootstrapSystem.cs` | SystemBase (init) |
| 10 | `Assets/Scripts/Trading/Systems/TradeRequestReceiveSystem.cs` | SystemBase (server) |
| 11 | `Assets/Scripts/Trading/Systems/TradeOfferReceiveSystem.cs` | SystemBase (server) |
| 12 | `Assets/Scripts/Trading/Systems/TradeConfirmReceiveSystem.cs` | SystemBase (server) |
| 13 | `Assets/Scripts/Trading/Systems/TradeExecutionSystem.cs` | SystemBase (server, atomic swap) |
| 14 | `Assets/Scripts/Trading/Systems/TradeCancelReceiveSystem.cs` | SystemBase (server) |
| 15 | `Assets/Scripts/Trading/Systems/TradeTimeoutSystem.cs` | SystemBase (server) |
| 16 | `Assets/Scripts/Trading/Systems/TradeProximityCheckSystem.cs` | SystemBase (server) |
| 17 | `Assets/Scripts/Trading/Systems/TradeCombatCheckSystem.cs` | SystemBase (server) |
| 18 | `Assets/Scripts/Trading/Systems/TradeAuditSystem.cs` | SystemBase (server) |
| 19 | `Assets/Scripts/Trading/Systems/TradeCleanupSystem.cs` | SystemBase (server) |
| 20 | `Assets/Scripts/Trading/UI/TradeUIRegistry.cs` | Static singleton registry |
| 21 | `Assets/Scripts/Trading/UI/ITradeUIProvider.cs` | Interface |
| 22 | `Assets/Scripts/Trading/UI/TradeUIBridgeSystem.cs` | SystemBase (client, managed) |
| 23 | `Assets/Scripts/Trading/UI/TradeWindowView.cs` | MonoBehaviour |
| 24 | `Assets/Scripts/Trading/UI/TradeOfferPanel.cs` | MonoBehaviour |
| 25 | `Assets/Scripts/Trading/UI/TradeConfirmButton.cs` | MonoBehaviour |
| 26 | `Assets/Scripts/Trading/DIG.Trading.asmdef` | Assembly definition |
| 27 | `Assets/Editor/TradeWorkstation/TradeWorkstationWindow.cs` | EditorWindow |
| 28 | `Assets/Editor/TradeWorkstation/ITradeWorkstationModule.cs` | Interface |

### Editor Modules (4)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Editor/TradeWorkstation/Modules/ActiveTradesModule.cs` | Module |
| 2 | `Assets/Editor/TradeWorkstation/Modules/TradeHistoryModule.cs` | Module |
| 3 | `Assets/Editor/TradeWorkstation/Modules/TradeSimulatorModule.cs` | Module |
| 4 | `Assets/Editor/TradeWorkstation/Modules/TradeConfigModule.cs` | Module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/Interaction/Components/InteractableComponents.cs` | Add `Trade = 15` to `InteractionVerb` enum (~1 line) |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/TradeConfig.asset` |

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `InventoryItem` buffer | Shared | Trade reads/writes item buffers directly |
| `CurrencyInventory` + `CurrencyTransaction` | 16.6 | Trade uses existing currency pipeline for atomic currency transfer |
| `CurrencyTransactionSystem` | 16.6 | Processes trade currency entries same frame (unchanged) |
| `InteractionVerb.Trade` | 16.1 | New verb value triggers trade initiation via interaction system |
| `CombatState.IsInCombat` | Combat | Blocks trade initiation and cancels active trades |
| `InventoryCapacity` + weight system | Survival | Trade validates receiver weight capacity before swap |
| `PersistenceSaveModule` (InventorySaveModule) | 16.15 | Post-trade inventory changes are saved by existing dirty tracking |
| `SaveDirtyTrackingSystem` | 16.15 | Inventory/currency modifications trigger save dirty flags automatically |
| `QuestObjectiveType.TradeWithPlayer` | 16.12 (future) | Quest system can add trade-based objectives |
| `SkillTreeSO` passive bonuses | 17.1 (future) | Talent passives could modify trade fees or capacity |
| `GuildSystem` (future) | TBD | Guild members could have reduced trade cooldowns or no proximity restriction |

---

## Multiplayer Considerations

- **Server-authoritative:** ALL trade logic runs on server. Clients send RPCs, receive notifications. No client prediction of trade state.
- **No ghost replication of trade sessions:** Session entities are server-only. Clients maintain a local mirror via notification RPCs. This avoids adding ghost bandwidth for ephemeral trade data.
- **Atomic execution:** The `TradeExecutionSystem` reads and writes in a single system update. No ECB deferred playback between validation and execution -- both happen in the same `OnUpdate` call using direct buffer access (`SystemAPI.GetBuffer<T>`), not ECB. The only ECB usage is for session entity destruction in `TradeCleanupSystem`.
- **Confirmation reset on offer change:** Prevents the exploit where Player A confirms, Player B modifies their offer (removing items), and the trade executes with the modified offer. The reset forces Player A to re-inspect and re-confirm.
- **Connection disconnect handling:** `TradeCancelReceiveSystem` monitors for destroyed connection entities and auto-cancels their active sessions.
- **Listen server:** Works identically -- server world processes trades, client world receives notifications. Host player's trade RPCs go through the same server-side validation as remote players.

---

## Extensibility

- **Trade fees:** Add `float TradeFeePercent` to `TradeConfig`. `TradeExecutionSystem` deducts fee from currency transfers (gold sink for economy health).
- **Trade history UI:** `TradeAuditLog` ring buffer can be exposed to clients via a query RPC for personal trade history.
- **Mail system:** Extend `TradeSessionState` with `TradeMode` enum (Direct=0, Mail=1). Mail mode skips proximity checks and allows offline recipients.
- **Auction house:** Separate system, but shares `TradeOffer` data model for item/currency descriptions.
- **Trade restrictions by item rarity:** Add `bool IsTradeable` to item definitions. `TradeOfferReceiveSystem` rejects non-tradeable items.
- **Guild bank deposits:** Reuse `TradeExecutionSystem` pattern with one side being a guild bank entity instead of a player.

---

## Verification Checklist

- [ ] Player A targets Player B and uses InteractionVerb.Trade: trade request sent
- [ ] Player B receives trade request popup notification
- [ ] Player B accepts: trade session created, both players see trade UI
- [ ] Player B declines: session destroyed, Player A notified
- [ ] Player A adds 5 Stone to offer: server validates, both UIs update
- [ ] Player A adds more items than MaxItemsPerOffer: server rejects
- [ ] Player A offers more currency than their balance: server rejects
- [ ] Player A confirms: "Waiting for other player" state shown
- [ ] Player B modifies offer after Player A confirmed: Player A's confirm reset
- [ ] Player A must re-confirm after offer change
- [ ] Both players confirm: server enters Executing state
- [ ] Execution: items swapped correctly between both inventories
- [ ] Execution: currency transferred correctly via CurrencyTransaction pipeline
- [ ] Execution: weight capacity validated before swap (reject if overweight)
- [ ] Execution: item ownership re-validated at execution time (not stale from confirm time)
- [ ] Execution: currency balance re-validated at execution time
- [ ] Failed validation: entire trade rolled back (no partial execution)
- [ ] Failed validation: both players notified with specific reason
- [ ] Self-trade prevention: A targeting self rejected
- [ ] Combat blocking: trade request rejected if either player in combat
- [ ] Combat during trade: session cancelled if either player enters combat
- [ ] Proximity check: trade request rejected if players too far apart
- [ ] Proximity during trade: session cancelled if players move too far apart
- [ ] Rate limiting: rapid trade requests from same player rejected
- [ ] Duplicate session: reject trade request if player already in active trade
- [ ] Timeout: session auto-cancelled after TimeoutTicks
- [ ] Disconnect: session cancelled if either player disconnects
- [ ] Premium currency: blocked by default (AllowPremiumCurrencyTrade=false)
- [ ] Audit log: completed trade logged with both ghost IDs, items, currencies, result
- [ ] Audit log: failed trade logged with failure reason
- [ ] Trade UI: shows correct items from local InventoryItem buffer
- [ ] Trade UI: shows correct currency balances from CurrencyInventory
- [ ] Trade UI: confirm button state reflects current confirm status
- [ ] Trade UI: closes on trade completion/cancellation/failure
- [ ] Editor: Active Trades module shows live sessions in Play mode
- [ ] Editor: Trade History module reads audit log ring buffer
- [ ] Editor: Trade Simulator creates mock trade between entities
- [ ] Multiplayer: trade is server-authoritative (no client-side execution)
- [ ] Multiplayer: listen server host goes through same validation as remote players
- [ ] Multiplayer: no ghost components added to player entity
- [ ] Performance: < 0.10ms total for all trade systems combined
- [ ] Archetype: 0 bytes added to player entity
- [ ] Backward compatibility: existing inventory, currency, interaction systems unchanged
- [ ] No regression: ItemPickupSystem, CurrencyTransactionSystem, ResourceCollectionSystem unaffected
