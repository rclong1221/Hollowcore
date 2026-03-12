# EPIC 4.4: Healing, Medical Items, and Recovery

**Priority**: MEDIUM  
**Goal**: Provide clear ways to recover from damage and status effects (medkits, stims, ship healing stations) with server authority.  
**Dependencies**: Epic 2.3 (tool framework), Epic 3.2 (stations), Epic 4.1/4.3

## Design Notes (Match EPIC7 Level of Detail)
- **Heal is symmetric to damage**: healing should also be event/request driven and server-authoritative.
- **Resource consumption**: med items/stations should consume inventory/power/resources via the same atomic validation patterns as cargo transfer (Epic 3.4).
- **Recovery is more than HP**: some heals should also reduce status effects (bleed/burn/hypoxia) by policy, not hard-coded.
- **Anti-exploit**: prevent client from repeatedly healing without owning/consuming an item.

## Components

**HealRequest** (IBufferElementData, on player; server-consumed)
| Field | Type | GhostField | Description |
|---|---|---:|---|
| `Amount` | float | Quantization=100 | HP to restore |
| `SourceEntity` | Entity | Yes | Medkit/station/tool |
| `ClientTick` | uint | Yes | Ordering |

**HealingStation** (IComponentData, on station)
| Field | Type | Description |
|---|---|---|
| `HealPerSecond` | float | Heal rate |
| `Range` | float | Interaction radius |

## Systems

**HealApplySystem** (SimulationSystemGroup, ServerWorld)
- Consumes `HealRequest`, clamps health to max
- Optional: clears or reduces certain `StatusEffect` types on heal

## Acceptance Criteria
- [x] Healing is server-authoritative, no client-side “free heal”
- [x] Stations/med items use the same request pipeline (no special cases)

## Sub-Epics / Tasks

### Sub-Epic 4.4.1: Medical Items (Inventory + Requests)
**Tasks**:
- [x] Define a minimal medical item model (even if temporary):
  - [x] Medkit (instant heal)
  - [x] Stim (small heal + temporary buff)
- [x] Use tool/request patterns:
  - [x] client requests “use med item”
  - [x] server validates item exists, consumes it, enqueues `HealRequest`

### Sub-Epic 4.4.2: Healing Stations (Ship)
**Tasks**:
- [x] Station interaction requires being in ship and within range
- [x] Heal is applied per second while interacting
- [ ] Optional power dependency (ties into Epic 3.5)

### Sub-Epic 4.4.3: Status Effect Recovery Policy
**Tasks**:
- [x] Define which heals affect which status types:
  - [x] Medkit reduces bleed/burn
  - [x] Oxygen refill clears hypoxia over time (future)
- [x] Implement recovery as:
  - [x] edits to `StatusEffect` buffer (server)
  - [x] or “recovery events” consumed by status system (cleaner) -- **Choice: StatusEffectRequest with negative severity**

### Sub-Epic 4.4.4: QA Checklist
**Tasks**:
- [x] Healing clamps at `Health.Max`
- [x] Healing request without item/station is rejected (handled by requester logic)
- [x] Healing correctly reduces configured status effects

---

## Integration & Usage Guide

### 1. Requesting a Heal (HP)
To restore Health, add a `HealRequest` to the player's buffer. The `HealApplySystem` processes this (Server) and clamps to Max HP.

```csharp
public void ApplyMedkit(Entity player, float healAmount)
{
    if (SystemAPI.HasBuffer<HealRequest>(player))
    {
        var buffer = SystemAPI.GetBuffer<HealRequest>(player);
        buffer.Add(new HealRequest
        {
            Amount = healAmount,
            SourceEntity = Entity.Null, // Or the Item Entity
            CureStatusType = 0
        });
    }
}
```

### 2. Curing Status Effects
To reduce or remove status effects (e.g., using a bandage to reduce Bleed, or oxygen to reduce Hypoxia), use `StatusEffectRequest` with **negative severity** and `Additive = true`.

```csharp
public void ApplyBandage(Entity player)
{
    var statusBuffer = SystemAPI.GetBuffer<StatusEffectRequest>(player);
    statusBuffer.Add(new StatusEffectRequest
    {
        Type = StatusEffectType.Bleed,
        Severity = -0.5f, // Removes 0.5 severity
        Duration = 0f,
        Additive = true
    });
}
```

### 3. Testing Instructions (Debug)
The `StatusEffectDebugSystem` has been updated:
- **Apply Effect**:
  - `[` : Hypoxia
  - `]` : Radiation
  - `\` : Burn
- **Heal HP**:
  - `H` : Heal 25 HP
- **Cure Effect** (Hold **Shift**):
  - `Shift + [` : Cure Hypoxia (Remove 1.0)
  - `Shift + ]` : Cure Radiation (Remove 1.0)
  - `Shift + \` : Cure Burn (Remove 1.0)


