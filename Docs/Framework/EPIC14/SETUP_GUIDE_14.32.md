# Setup Guide: EPIC 14.32 - Throwable Hand Position Replication

## Prerequisites

- Character visual prefab (e.g., `Atlas_Client`) with animated skeleton
- Socket transforms configured via `SocketAuthoring` components
- Throwable weapon with `ThrowableAction` and `ThrowableState` components

---

## Setup Steps

### 1. Verify Socket Configuration on Character

The character visual prefab needs `SocketPositionSyncBridge` and properly configured sockets.

**On the character visual prefab (Atlas_Client):**

1. Ensure `SocketPositionSyncBridge` component is attached (same GameObject as `DIGEquipmentProvider`)
2. Verify socket transforms have `SocketAuthoring` component with correct `SocketType`:
   - `Socket_MainHand` → `SocketType.MainHand`
   - `Socket_OffHand` → `SocketType.OffHand`

```
Atlas_Client
├── SocketPositionSyncBridge ✓
├── DIGEquipmentProvider ✓
└── Armature
    └── ... (bone hierarchy)
        └── ORG-hand.R
            └── Socket_MainHand
                └── SocketAuthoring (Type = MainHand) ✓
```

### 2. Verify PlayerInput Has Required Fields

The `PlayerInput` struct should already have these fields (added in this EPIC):

```csharp
// In PlayerInput_Global.cs
public float3 MainHandPosition;
public byte MainHandPositionValid;
```

If missing, add them to the struct.

### 3. Verify Throwable Weapon Configuration

Throwable weapons need standard components:

- `ThrowableAction` - Configuration (min/max force, charge time, prefab)
- `ThrowableState` - Runtime state (charging, charge progress, aim direction)
- `UsableAction` - Ammo tracking
- `UseRequest` - Input state

---

## Verification

### In Editor (Play Mode)

1. Enter play mode with a character that has a throwable equipped
2. Hold the throw button to charge
3. Watch the trajectory line - it should start from the hand position
4. Release to throw
5. Projectile should spawn from hand, not body center

### Console Logs (If Debug Enabled)

If you need to debug, temporarily add logging to `ThrowableActionSystem.OnUpdate()`:

```csharp
if (Time.frameCount % 60 == 0)
{
    Debug.Log($"[GRENADE] Using hand position: {spawnPos} Valid={useHandPosition}");
}
```

### Multiplayer Verification

1. Run server + client
2. Have one player throw a grenade
3. On both client and server, projectile should spawn from hand position
4. Other connected clients should see correct spawn position

---

## Troubleshooting

### Issue: Projectile spawns from body center instead of hand

**Possible causes:**

1. **SocketPositionSyncBridge not attached** - Add it to character visual prefab
2. **SocketAuthoring missing/wrong type** - Verify `Socket_MainHand` has `SocketAuthoring` with `Type = MainHand`
3. **PlayerInputSystem not reading socket data** - Check `using DIG.Shared;` is present

**Debug steps:**

```csharp
// In PlayerInputSystem.OnUpdate(), add temporarily:
if (EntityManager.HasComponent<SocketPositionData>(entity))
{
    var sd = EntityManager.GetComponentData<SocketPositionData>(entity);
    Debug.Log($"Socket: Valid={sd.IsValid} Pos={sd.MainHandPosition}");
}
```

### Issue: Hand position is zero on server

**Possible causes:**

1. **PlayerInput not replicating** - Ensure `PlayerInput` is `IInputComponentData`
2. **MainHandPositionValid not set** - Check client is setting it to 1

### Issue: Trajectory line doesn't match spawn position

The trajectory visualization (`ThrowableTrajectoryVisuals`) reads `SocketPositionData` directly on the client. The spawn position (`ThrowableActionSystem`) reads from `PlayerInput`. Both should use the same source, but there's a 1-tick delay for the replicated version.

This is expected and the visual discrepancy should be imperceptible.

---

## File Locations

| File | Purpose |
|------|---------|
| `Assets/Scripts/Shared/Player/PlayerInput_Global.cs` | IInputComponentData with hand position fields |
| `Assets/Scripts/Player/Systems/PlayerInputSystem.cs` | Reads socket, writes to PlayerInput |
| `Assets/Scripts/Weapons/Systems/ThrowableActionSystem.cs` | Reads PlayerInput for spawn position |
| `Assets/Scripts/Items/Bridges/SocketPositionSyncBridge.cs` | MonoBehaviour → ECS bridge |
| `Assets/Scripts/Shared/SocketPositionData.cs` | ECS component for socket positions |

---

## Dependencies

- EPIC 14.16 (DIG Asset Pipeline) - Socket system
- EPIC 14.20 (Weapon Effects) - Throwable audio/effects
