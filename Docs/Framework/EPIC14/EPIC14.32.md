# EPIC 14.32: Throwable Hand Position Replication

## Overview
Implements accurate throwable projectile spawning from the character's animated hand socket position on both client and server.

**Problem Solved**: Throwable projectiles (grenades) were spawning from a fixed offset based on player position, not from the actual animated hand socket. This caused a visible discrepancy between where the hand appeared to be and where the projectile spawned.

---

## Architecture

### The Challenge

The animated skeleton exists only on the **client** (MonoBehaviour visual prefab). The server has no knowledge of where the character's hand is in world space - it only knows the player entity's root position.

**Previous behavior**: Server would calculate spawn position as `PlayerPosition + HeightOffset` (approximately 1.5m up), which didn't match the hand's actual position during the throw animation.

### The Solution: Input Stream Replication

We leverage Unity NetCode's `IInputComponentData` to send the hand socket position from client to server:

```
Client Side:
  SocketPositionSyncBridge (MonoBehaviour)
       │
       │ LateUpdate() - reads animated skeleton transforms
       ▼
  SocketPositionData (ECS Component on Player Entity)
       │
       │ GhostInputSystemGroup
       ▼
  PlayerInputSystem
       │
       │ writes to IInputComponentData
       ▼
  PlayerInput.MainHandPosition (replicated client → server)

Server Side:
  ThrowableActionSystem
       │
       │ reads from PlayerInput
       ▼
  Uses MainHandPosition for spawn position
```

### Key Insight

- **GhostFields** replicate SERVER → CLIENT (not useful here)
- **IInputComponentData** replicates CLIENT → SERVER (what we need)

By putting `MainHandPosition` in `PlayerInput`, the hand position is automatically sent to the server along with other input data every tick.

---

## Implementation Details

### 1. PlayerInput_Global.cs

Added fields to the `PlayerInput` struct (IInputComponentData):

```csharp
// ===== SOCKET POSITIONS (for accurate throwable spawn) =====
/// <summary>
/// World position of the main hand socket (from animated skeleton).
/// Used by server for accurate throwable spawn position.
/// </summary>
public float3 MainHandPosition;

/// <summary>0 = invalid/not available, 1 = valid position</summary>
public byte MainHandPositionValid;
```

**Location**: `Assets/Scripts/Shared/Player/PlayerInput_Global.cs`

### 2. PlayerInputSystem.cs

Captures hand socket position before sending input to server:

```csharp
// Capture hand socket position for accurate throwable spawning (client → server)
if (EntityManager.HasComponent<SocketPositionData>(entity))
{
    var socketData = EntityManager.GetComponentData<SocketPositionData>(entity);
    if (socketData.IsValid)
    {
        playerInput.ValueRW.MainHandPosition = socketData.MainHandPosition;
        playerInput.ValueRW.MainHandPositionValid = 1;
    }
}
```

**Location**: `Assets/Scripts/Player/Systems/PlayerInputSystem.cs`
**Update Group**: `GhostInputSystemGroup` (runs before input is sent to server)

### 3. ThrowableActionSystem.cs

Uses replicated hand position with fallback priority:

```csharp
// PRIORITY 1: Use MainHandPosition from PlayerInput (replicated from client to server)
var playerInputLookup = SystemAPI.GetComponentLookup<PlayerInput>(true);
if (playerInputLookup.HasComponent(ownerEntity))
{
    var playerInput = playerInputLookup[ownerEntity];
    if (playerInput.MainHandPositionValid == 1)
    {
        spawnPos = playerInput.MainHandPosition;
        useHandPosition = true;
    }
}

// PRIORITY 2: Fallback to SocketPositionData (only available on client)
// PRIORITY 3: Fallback to player position + height offset
```

**Location**: `Assets/Scripts/Weapons/Systems/ThrowableActionSystem.cs`

### 4. SocketPositionSyncBridge.cs

MonoBehaviour that syncs socket transforms from animated skeleton to ECS:

```csharp
private void LateUpdate()
{
    // LateUpdate ensures we read positions after animation has been applied
    SyncSocketPositions();
}

private void SyncSocketPositions()
{
    em.SetComponentData(playerEntity, new SocketPositionData
    {
        MainHandPosition = _mainHandSocket.position,
        IsValid = _mainHandSocket != null
    });
}
```

**Location**: `Assets/Scripts/Items/Bridges/SocketPositionSyncBridge.cs`
**Prefab**: Attached to the client visual character prefab (Atlas_Client)

---

## Data Flow Summary

```
Frame N (Client):
1. Animation updates hand bone position
2. SocketPositionSyncBridge.LateUpdate() reads _mainHandSocket.position
3. Writes to SocketPositionData component on player entity
4. PlayerInputSystem reads SocketPositionData
5. Writes to PlayerInput.MainHandPosition
6. NetCode sends PlayerInput to server

Frame N (Server):
7. Server receives PlayerInput with MainHandPosition
8. ThrowableActionSystem reads PlayerInput.MainHandPosition
9. Spawns projectile at correct hand position
```

---

## Files Modified

| File | Changes |
|------|---------|
| `PlayerInput_Global.cs` | Added `MainHandPosition` and `MainHandPositionValid` fields |
| `PlayerInputSystem.cs` | Read SocketPositionData and populate PlayerInput |
| `ThrowableActionSystem.cs` | Priority-based spawn position lookup from PlayerInput |
| `SocketPositionSyncBridge.cs` | Syncs animated skeleton to ECS (existing, cleaned up) |

---

## Related Components

### SocketPositionData (ECS Component)

```csharp
public struct SocketPositionData : IComponentData
{
    public float3 MainHandPosition;
    public float3 OffHandPosition;
    public bool IsValid;
}
```

**Location**: `Assets/Scripts/Shared/SocketPositionData.cs`

### ThrowableTrajectoryVisuals (Client-only)

Client-side trajectory visualization also uses SocketPositionData directly for accurate preview line rendering.

**Location**: `Assets/Scripts/Weapons/Visuals/ThrowableTrajectoryVisuals.cs`

---

## Performance Considerations

- **Bandwidth**: Adds 13 bytes per input tick (float3 + byte)
- **Latency**: Hand position is 1 tick behind (same as all input data)
- **CPU**: Negligible - just a component lookup

---

## Limitations

1. **Latency**: Position is 1 network tick old by the time server uses it
2. **Animation Sync**: Assumes client and server animations are in sync (standard for predicted ghosts)
3. **No Off-Hand**: Currently only tracks main hand (right hand for most characters)

---

## Future Improvements

1. **Dual Hand Support**: Add `OffHandPosition` to PlayerInput for two-handed throws
2. **Interpolation**: Could smooth hand position on server if needed
3. **Compression**: Could quantize position to reduce bandwidth (but probably not worth it)

---

## Testing

To verify implementation:

1. Equip a throwable weapon (grenade)
2. Hold throw button to charge
3. Observe trajectory line starts from hand
4. Release to throw
5. Verify projectile spawns from hand position (not center of body)
6. In multiplayer, verify other players see correct spawn position
