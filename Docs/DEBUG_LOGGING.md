# Debug Logging System

This project uses a centralized conditional logging system via `DebugLog.cs` that allows you to enable/disable specific logging categories at compile-time.

## How to Enable Logging

### Option 1: Enable All Logging
In Unity Editor, go to:
1. **Edit → Project Settings → Player → Other Settings → Scripting Define Symbols**
2. Add: `DEBUG_LOG_ALL`
3. Click **Apply**

### Option 2: Enable Specific Categories
Add any combination of these symbols to enable specific logging:
- `DEBUG_LOG_INPUT` - Player input detection and processing
- `DEBUG_LOG_MOVEMENT` - Player movement, velocity, position
- `DEBUG_LOG_GROUND_CHECK` - Ground detection raycasts
- `DEBUG_LOG_STATE` - Player state transitions
- `DEBUG_LOG_STANCE` - Stance changes (standing, crouching, prone)
- `DEBUG_LOG_STAMINA` - Stamina drain/regen
- `DEBUG_LOG_CAMERA` - Camera world/entity detection
- `DEBUG_LOG_NETWORK` - Network connection events
- `DEBUG_LOG_PHYSICS` - Physics simulation
- `DEBUG_LOG_SPAWNING` - Entity spawning
- `DEBUG_LOG_SYSTEMS` - General ECS system lifecycle
- `DEBUG_LOG_ENTITIES` - Entity creation/destruction

## Performance Impact

✅ **ZERO performance impact when disabled!**
- Uses `[System.Diagnostics.Conditional]` attribute
- Calls are completely removed by the compiler when symbols are not defined
- No runtime checks, no string allocations, no performance cost

## Logging Categories Implemented

### Player Systems
- ✅ **PlayerInputDebugSystem** → `DebugLog.LogInput()`
  - Can be disabled: ✅ (via `DEBUG_LOG_INPUT`)
  - Logs: Movement input, look delta, button states

- ✅ **PlayerMovementSystem** → `DebugLog.LogMovement()` / `LogMovementWarning()`
  - Can be disabled: ✅ (via `DEBUG_LOG_MOVEMENT`)
  - Logs: Input values, grounded state, velocity, position, entity count

- ⚠️ **PlayerGroundCheckSystem** → No logging implemented yet
  - Can be disabled: N/A
  - Reserved: `DebugLog.LogGroundCheck()` available

- ⚠️ **PlayerStateSystem** → No logging implemented yet
  - Can be disabled: N/A
  - Reserved: `DebugLog.LogState()` available

- ⚠️ **PlayerStanceSystem** → No logging implemented yet
  - Can be disabled: N/A
  - Reserved: `DebugLog.LogStance()` available

- ⚠️ **PlayerStaminaSystem** → No logging implemented yet
  - Can be disabled: N/A
  - Reserved: `DebugLog.LogStamina()` available

### Camera Systems
- ✅ **CameraManager** → `DebugLog.LogCamera()` / `LogCameraWarning()`
  - Can be disabled: ✅ (via `DEBUG_LOG_CAMERA`)
  - Logs: World search, client world detection, player entity detection

### Network Systems
- ❌ **GameBootstrap** → Still uses `Debug.Log()` directly
  - Can be disabled: ❌ (always on)
  - Logs: Host/join events, world creation

- ❌ **NetworkUI** → Still uses `Debug.Log()` directly
  - Can be disabled: ❌ (always on)
  - Logs: UI events

- ❌ **GoInGameSystem** → Still uses Burst logging directly
  - Can be disabled: ❌ (always on)
  - Logs: Player spawning events

### Other Systems
- No other systems currently have debug logging

## Migration Status

| System | Migrated to DebugLog | Can Disable |
|--------|---------------------|-------------|
| PlayerInputDebugSystem | ✅ | ✅ |
| PlayerMovementSystem | ✅ | ✅ |
| CameraManager | ✅ | ✅ |
| GameBootstrap | ❌ | ❌ |
| NetworkUI | ❌ | ❌ |
| GoInGameSystem | ❌ | ❌ |
| PlayerGroundCheckSystem | N/A | - |
| PlayerStateSystem | N/A | - |
| PlayerStanceSystem | N/A | - |
| PlayerStaminaSystem | N/A | - |

## Notes on Non-Migratable Logging

### Burst-Compiled Systems
Systems compiled with `[BurstCompile]` **cannot** call `DebugLog` methods because:
- Burst doesn't support `[Conditional]` attributes
- Burst has its own logging mechanism
- Example: `GoInGameSystem` uses burst logging which cannot be disabled

### Solution for Burst Systems
Remove or comment out the logging statements manually in burst-compiled code.

### CharacterControllerSystem
**Special Case: Runtime-Toggleable Diagnostics**

`CharacterControllerSystem` uses a different approach with a runtime-toggleable flag:

```csharp
// Enable/disable at runtime (default: false)
CharacterControllerSystem.DiagnosticsEnabled = true;
```

**Features:**
- ✅ Can be toggled at runtime without recompilation
- ✅ Works with Burst-compiled jobs (uses static bool check)
- ✅ Default is **false** (logs disabled for performance)
- ✅ Logs player movement, overlap checks, collision resolution
- ⚠️ Small runtime cost when enabled (bool check per log statement)

**Usage:**
- Set to `true` from editor script, debug UI, or console command
- Useful for debugging player-player collisions and character controller physics
- Logs are gated behind `DLog()`, `LogIfEnabled()`, `DLogWarning()`, `DLogError()` helpers

### PlayerCollisionResponseSystem
**Burst-Compiled: All Debug Logs Removed**

`PlayerCollisionResponseSystem` is fully Burst-compiled with all debug logging removed for optimal performance:

```csharp
[BurstCompile]
public partial struct PlayerCollisionResponseSystem : ISystem
```

**Features:**
- ✅ Fully Burst-compiled (system and job)
- ✅ No runtime logging overhead
- ✅ All debug logs removed for production performance
- ✅ Processes Unity Physics collision events in parallel

**Note:** If you need to debug collision response, temporarily remove `[BurstCompile]` attributes and add back `UnityEngine.Debug.Log()` calls as needed. Remember to restore Burst compilation before committing.

## Future Enhancements
- Add `DEBUG_LOG_GAMEPLAY` for gameplay events
- Add `DEBUG_LOG_AI` for AI behavior
- Add `DEBUG_LOG_VOXEL` for voxel operations
- Add `DEBUG_LOG_TOOLS` for player tool usage
- Add `DEBUG_LOG_INVENTORY` for inventory management
