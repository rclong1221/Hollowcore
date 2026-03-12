# Ship Piloting Mode with Player Synchronization

## Overview
This PR implements a complete ship piloting system with proper player-server synchronization. When a player operates a ship's helm station, their character now correctly remains stationary while the ship responds to movement input (WASD).

## Problem Statement
Previously, when players attempted to pilot ships:
1. **Player character moved independently** - WASD input moved the player instead of the ship
2. **PlayerMode desynchronization** - Client and server had different PlayerMode states
3. **Ship didn't respond to input** - Input routing and ship movement were not connected
4. **Airlock component errors** - ReadOnly ComponentLookups prevented necessary writes

## Solution

### Core Architecture Changes

#### 1. Player Mode Synchronization
- **StationOccupancySystem** now runs on both client and server (`WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation`)
- Enables client-side prediction of `PlayerState.Mode` changes
- Eliminates the lag between entering a station and mode change taking effect

#### 2. Player Movement Suppression
Added `PlayerMode.Piloting` checks in two critical systems:

**PlayerMovementSystem:**
```csharp
if (pState.Mode == PlayerMode.Piloting)
{
    vel.Linear.x = 0;  // Zero horizontal movement
    vel.Linear.z = 0;
    // Gravity still applies for proper physics
}
```

**CharacterControllerSystem:**
```csharp
if (pState.Mode == PlayerMode.Piloting)
    return;  // Skip movement processing entirely
```

This dual-layer approach ensures the player character remains stationary from both velocity calculation and physics integration perspectives.

#### 3. Input Routing
**StationInputRoutingSystem** properly routes player input to ship controls:
- Maps WASD to `StationInput.Move` for helm stations
- Applies appropriate deadzones (0.1 for movement, 0.5 for look)
- Runs in `PredictedSimulationSystemGroup` for responsive input

#### 4. Ship Movement
**ShipMovementSystem** processes helm input and updates ship position:
- Applies thrust based on input to `ShipKinematics.LinearVelocity`
- Updates ship `LocalTransform.Position` each frame
- Includes drag/friction for realistic physics feel
- Runs in `PredictedFixedStepSimulationSystemGroup` for consistent physics

#### 5. Visual Synchronization
**New: ShipVisualSyncSystem**
- Syncs ECS entity `LocalTransform` to visual GameObject
- Uses `GhostPresentationGameObjectSystem` for entity-to-GameObject mapping
- Runs in `PresentationSystemGroup` on client only
- **Note:** Requires `GhostPresentationGameObjectAuthoring` on ship prefab

### Bug Fixes

#### Airlock System
Fixed `InvalidOperationException` in `AirlockCycleSystem`:
```csharp
// Before: ReadOnly (true)
_transformLookup = state.GetComponentLookup<LocalTransform>(false);  // Now writable
_playerStateLookup = state.GetComponentLookup<PlayerState>(false);   // Now writable
```

This allows the airlock system to properly teleport players and update their mode.

## Technical Details

### Player-Ship Relationship
When piloting:
- **Player position** tracks ship position with fixed offset (~2.3 units at helm)
- **Player velocity** is zeroed (no independent movement)
- **Ship position** updates based on input
- **Both move together** in world space

### Network Synchronization
- `PlayerState.Mode` is a `[GhostField]` - replicated to all clients
- Client-side prediction prevents input lag
- Server remains authoritative for final state
- Debounce system prevents rapid state changes

### Debug Logging
Added comprehensive logging for troubleshooting:
- `[PlayerMove] PILOTING` - Shows player position when piloting
- `[ShipMovement] Applied thrust` - Confirms ship velocity changes
- `[ShipMovement] Ship moved from X to Y` - Tracks actual position updates
- `[StationInputRouting] WROTE StationInput` - Verifies input routing

## Testing Performed

### Functional Testing
✅ Player enters ship via airlock  
✅ Player approaches helm and presses 'T'  
✅ PlayerMode changes to `Piloting`  
✅ WASD input routes to ship (not player)  
✅ Player character remains stationary  
✅ Ship entity position updates correctly  
✅ Player exits helm with 'T'  
✅ Player exits ship via airlock  

### Synchronization Testing
✅ Client-side prediction works (no input lag)  
✅ Server-client mode sync verified via logs  
✅ No "debounce" errors after fixes  
✅ Multiple enter/exit cycles work correctly  

### Edge Cases
✅ Gravity still applies to player when piloting  
✅ Input deadzone prevents drift  
✅ Ship velocity clamps to max speed  
✅ Airlock teleport works with new ComponentLookups  

## Known Limitations

### Visual Sync Requirement
The ship's visual GameObject may not update without proper setup:

**Required on Ship Prefab:**
1. `GhostAuthoringComponent` - Makes ship a networked entity
2. `GhostPresentationGameObjectAuthoring` - Links entity to visual

**Recommendation:** Add these components to `Assets/Prefabs/TestShip.prefab`

### Future Enhancements
- [ ] Add ship rotation (yaw/pitch/roll) based on look input
- [ ] Implement boost/brake functionality (Sprint/Crouch keys)
- [ ] Add vertical thrust (Jump key)
- [ ] Create ship camera system (separate from player camera)
- [ ] Add ship HUD/UI when piloting
- [ ] Implement ship-to-ship collision
- [ ] Add ship physics body for proper collision response

## Files Changed

### Modified Systems
- `PlayerMovementSystem.cs` - Added Piloting mode check
- `CharacterControllerSystem.cs` - Skip movement when piloting
- `StationOccupancySystem.cs` - Client-side prediction enabled
- `StationInputRoutingSystem.cs` - Input routing and debug logs
- `ShipMovementSystem.cs` - Ship physics and position updates
- `AirlockCycleSystem.cs` - Fixed ComponentLookup declarations

### New Systems
- `ShipVisualSyncSystem.cs` - Entity-to-GameObject transform sync

## Migration Guide

### For Existing Ships
If you have existing ship prefabs:

1. **Add Required Components:**
   ```
   - GhostAuthoringComponent
   - GhostPresentationGameObjectAuthoring
   ```

2. **Configure GhostPresentationGameObjectAuthoring:**
   - Set `Presentation` field to the visual mesh GameObject
   - This enables visual sync via `ShipVisualSyncSystem`

3. **Verify Station Setup:**
   - Ensure helm station has `StationType.Helm`
   - Confirm station is child of ship with `ShipRoot`

### For New Ships
Follow the ship creation workflow:
1. Create ship GameObject with `ShipRootAuthoring`
2. Add `GhostAuthoringComponent` and `GhostPresentationGameObjectAuthoring`
3. Add child stations with `StationAuthoring`
4. Add airlocks with `AirlockAuthoring`

## Performance Impact

### Positive
- Client-side prediction reduces perceived latency
- Efficient ECS queries (no managed allocations in hot path)
- Debug logs can be disabled for production

### Neutral
- `ShipVisualSyncSystem` runs per-frame but only on client
- Minimal overhead (single foreach loop per ship)

## Breaking Changes
None - This is purely additive functionality.

## Dependencies
- Unity Entities 1.0+
- Unity NetCode 1.0+
- Existing player and ship systems

## Reviewers
Please verify:
- [ ] Player movement suppression works correctly
- [ ] Ship responds to WASD input
- [ ] No regressions in airlock functionality
- [ ] Network synchronization is stable
- [ ] Debug logs are helpful (can be removed later)

## Related Issues
Closes #[issue-number] (if applicable)

## Screenshots/Videos
[Add gameplay footage showing ship piloting in action]

---

**Ready for Review** ✅
