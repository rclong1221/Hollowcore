# EPIC 3.4: Ship Storage (Cargo) and Resource Transfer

**Priority**: MEDIUM  
**Goal**: Deposit/withdraw resources between player inventory and ship cargo with authoritative sync and clear UI.  
**Dependencies**: Epic 2.6 (`InventoryItem`), Epic 3.2 (station interaction), NetCode predicted inventory ghosts  
**Status**: ✅ IMPLEMENTED

## Design Notes (Match EPIC7 Level of Detail)
- **Atomicity**: transfers must be applied as a single server-authoritative transaction (avoid "player lost items but ship didn't gain").
- **Request buffer pattern**: UI emits `CargoTransferRequest`; server validates and applies.
- **Rate limiting**: clamp transfer frequency and quantity to prevent buffer spam and accidental overflows.
- **Replication**: cargo buffer can be replicated (compact) or server-only with periodic "UI snapshot" events; MVP can replicate for simplicity.
- **Capacity model**: optional; can mirror player inventory weight logic (Epic 2.6) for consistency.

## Components

**ShipCargoItem** (IBufferElementData, on ship; server-authoritative ghost)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `ResourceType` | ResourceType | Yes | Matches `DIG.Survival.Resources.ResourceType` |
| `Quantity` | int | Yes | Amount stored |

**ShipCargoCapacity** (IComponentData, on ship)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `MaxWeight` | float | No | Optional capacity limit |
| `CurrentWeight` | float | Quantization=100 | Derived/synced |
| `IsOverCapacity` | bool | Yes | True if at or over capacity |

**CargoTransferRequest** (IBufferElementData, on player; server-consumed)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `ShipEntity` | Entity | Yes | Ship cargo to modify |
| `ResourceType` | ResourceType | Yes | What to move |
| `Quantity` | int | Yes | Positive = deposit, negative = withdraw |
| `ClientTick` | uint | Yes | Ordering/anti-spam |

**CargoTerminal** (IComponentData, on interactable entity)
| Field | Type | Description |
|---|---|---|
| `ShipEntity` | Entity | Which ship this terminal belongs to |
| `Range` | float | Interaction range |
| `StableId` | int | Unique ID for network resolution |

**InteractingWithCargo** (IComponentData, on player)
| Field | Type | Description |
|---|---|---|
| `TerminalEntity` | Entity | Terminal being interacted with |
| `ShipEntity` | Entity | Ship whose cargo is accessible |

## Systems

**CargoTerminalInteractionSystem** (SimulationSystemGroup, Client+Server)
- Detects nearby cargo terminals for each player
- Adds/removes `InteractingWithCargo` component based on proximity
- Uses world-space distance calculation

**CargoTransferSystem** (SimulationSystemGroup, ServerWorld)
- Validates:
  - player is in ship / near terminal
  - sufficient inventory/cargo quantity
  - capacity limits (optional)
- Applies inventory buffer diffs atomically (player + ship)

**CargoWeightSystem** (SimulationSystemGroup, ServerWorld)
- Recalculates `ShipCargoCapacity.CurrentWeight` after transfers
- Uses `ResourceWeights` singleton for weight values

**CargoPlayerInitSystem** (SimulationSystemGroup, Client+Server)
- Adds `CargoTransferRequest` buffer to players on spawn

## File Locations

```
Assets/Scripts/Runtime/Ship/Cargo/
├── Components/
│   └── CargoComponents.cs          # ShipCargoItem, ShipCargoCapacity, CargoTransferRequest, etc.
├── Authoring/
│   ├── ShipCargoAuthoring.cs       # Ship root cargo capacity authoring
│   └── CargoTerminalAuthoring.cs   # Cargo terminal interaction authoring
├── Systems/
│   ├── CargoTransferSystem.cs              # Server-side transfer validation
│   ├── CargoTerminalInteractionSystem.cs   # Client interaction detection + weight calc
│   └── CargoDebugSystem.cs                 # Debug T key logging
└── UI/
    ├── CargoUIPanel.cs             # Main UI panel with two-column layout
    ├── CargoItemRow.cs             # Individual item row with transfer button
    ├── CargoUIDataBridge.cs        # Bridge between ECS and Unity UI
    └── CargoUIBuilder.cs           # Runtime UI creation (no prefab needed)
```

## Integration Guide

### Step 1: Add Cargo to Existing Ships

For ships created via the editor tool (`GameObject > DIG - Test Objects > Ships > Complete Test Ship`), cargo is automatically added.

For existing ship prefabs, add these components:

1. **On Ship Root** (same object as `ShipRootAuthoring`):
   - Add `ShipCargoAuthoring` component
   - Set `MaxWeight` (default: 1000kg)
   - Optionally set `InitialCargo` for testing

2. **On Cargo Console** (child of ship interior):
   - Create a new child GameObject
   - Add `CargoTerminalAuthoring` component
   - Set `InteractionRange` (default: 2.5m)
   - Set unique `StableId` (use 10+ to avoid conflicts with stations)

### Step 2: Player Authoring

The system automatically adds `CargoTransferRequest` buffer to players with `PlayerState`. No manual setup required.

### Step 3: Triggering Transfers (UI Integration)

To transfer resources, append to the player's `CargoTransferRequest` buffer:

```csharp
// Example: Deposit 10 Metal to ship
var requestBuffer = SystemAPI.GetBuffer<CargoTransferRequest>(playerEntity);
requestBuffer.Add(new CargoTransferRequest
{
    ShipEntity = shipEntity,
    ResourceType = ResourceType.Metal,
    Quantity = 10,  // Positive = deposit
    ClientTick = networkTime.ServerTick.TickIndexForValidTick
});

// Example: Withdraw 5 Stone from ship
requestBuffer.Add(new CargoTransferRequest
{
    ShipEntity = shipEntity,
    ResourceType = ResourceType.Stone,
    Quantity = -5,  // Negative = withdraw
    ClientTick = networkTime.ServerTick.TickIndexForValidTick
});
```

### Step 4: Reading Cargo Contents (UI Display)

To display ship cargo in UI:

```csharp
// Check if player is near a cargo terminal
if (SystemAPI.HasComponent<InteractingWithCargo>(playerEntity))
{
    var interaction = SystemAPI.GetComponent<InteractingWithCargo>(playerEntity);
    Entity shipEntity = interaction.ShipEntity;
    
    // Get cargo buffer
    if (SystemAPI.HasBuffer<ShipCargoItem>(shipEntity))
    {
        var cargo = SystemAPI.GetBuffer<ShipCargoItem>(shipEntity);
        foreach (var item in cargo)
        {
            Debug.Log($"{item.ResourceType}: {item.Quantity}");
        }
    }
    
    // Get capacity
    if (SystemAPI.HasComponent<ShipCargoCapacity>(shipEntity))
    {
        var capacity = SystemAPI.GetComponent<ShipCargoCapacity>(shipEntity);
        Debug.Log($"Weight: {capacity.CurrentWeight}/{capacity.MaxWeight}");
    }
}
```

## Testing Guide

### Manual Testing

1. **Create Test Ship**: 
   - `GameObject > DIG - Test Objects > Ships > Complete Test Ship`
   - This creates a ship with cargo terminal and 1000kg capacity

2. **Add Test Resources to Player**:
   - Use debug console or modify player spawn to add `InventoryItem` entries

3. **Test Deposit**:
   - Walk to cargo terminal (orange console with boxes)
   - Trigger deposit (via UI or debug command)
   - Verify player inventory decreased
   - Verify ship cargo increased

4. **Test Withdraw**:
   - At cargo terminal, trigger withdraw
   - Verify ship cargo decreased
   - Verify player inventory increased

5. **Test Capacity Limits**:
   - Fill cargo to near max weight
   - Try to deposit more than capacity allows
   - Verify partial transfer or rejection

### Automated Testing Checklist

- [ ] Deposit updates both player inventory and ship cargo deterministically
- [ ] Withdraw fails gracefully when cargo is insufficient (no desync)
- [ ] Capacity limits prevent overflow
- [ ] Player must be near terminal or in ship to transfer
- [ ] Multiplayer: Two players deposit simultaneously; totals add up correctly
- [ ] Negative/overflow quantities are rejected

## Acceptance Criteria
- [x] Depositing updates both player inventory and ship cargo deterministically
- [x] Withdrawing fails gracefully when cargo is insufficient (no desync)
- [x] UI shows current ship cargo and player inventory counts (local player only)

## Sub-Epics / Tasks

### Sub-Epic 3.4.1: Ship Cargo Buffer + Capacity ✅
**Tasks**:
- [x] Define `ShipCargoItem` buffer + helper methods (find/add/remove by `ResourceType`)
- [x] Optional `ShipCargoCapacity` using `ResourceWeights` (same weights as player inventory)
- [x] Decide replication strategy:
  - [x] replicate cargo for all clients (simple, potentially more bandwidth)

### Sub-Epic 3.4.2: Transfer Validation + Atomic Apply (Server) ✅
**Tasks**:
- [x] Validate player is:
  - [x] alive
  - [x] in ship (or near `CargoTerminal`)
  - [x] interacting with correct ship
- [x] Validate quantities:
  - [x] deposit: player has enough in `InventoryItem`
  - [x] withdraw: ship has enough in `ShipCargoItem`
- [x] Apply as one transaction (update player + ship or no-op)

### Sub-Epic 3.4.3: UI + UX (Client) ✅
**Tasks**:
- [x] Minimal cargo UI: two columns (player inventory, ship cargo) + transfer buttons
- [x] Show capacity/weight warnings (if enabled)
- [x] Prevent repeated spam clicks (cooldown / hold-to-transfer)

### Sub-Epic 3.4.4: QA Checklist
**Tasks**:
- [ ] Deposit/withdraw while latency is high; counts remain consistent
- [ ] Two players deposit simultaneously; totals add up and no duplication occurs
- [ ] Negative/overflow quantities are rejected

## Known Limitations

1. **Runtime UI**: The UI is created at runtime via `CargoUIBuilder`. No prefab is provided.

2. **Weight Calculation**: Weight is recalculated after each transfer. For very frequent transfers, consider batching.

3. **T Key Conflict**: The cargo UI uses T key to open. The debug system also uses T, so opening the UI will also log cargo contents.

## Future Improvements

1. **Cargo Categories**: Add categories (raw materials, processed goods, equipment)
2. **Cargo Sorting**: Sort by type, quantity, or weight
3. **Quick Transfer**: "Transfer All" button for efficiency
4. **Cargo History**: Log of recent transfers for auditing
5. **Cargo Permissions**: Per-player access control
