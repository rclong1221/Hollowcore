# EPIC 13.5: Locomotion Abilities & Refactor

> **Status:** COMPLETED ✓
> **Priority:** CRITICAL
> **Dependencies:** EPIC 13.2 (Ability System)

> [!IMPORTANT]
> **Refactoring Goal:** This is the "Cleanup" Epic.
> We will verify new Ability System implementations for Jump, Crouch, and Sprint, and then **DELETE** the corresponding hardcoded logic from `PlayerMovementSystem.cs`.
> The goal is to shrink `PlayerMovementSystem.cs` to a simple velocity integrator.

## Overview
Port core locomotion features (Jump, Crouch, Sprint) from the monolithic `PlayerMovementSystem` to modular ECS Abilities.

## Implementation Status

### 13.5.1 Jump Ability
**Status:** COMPLETED ✓
**Files Created:** `JumpSystem.cs`, `LocomotionComponents.cs`

The JumpSystem handles jump input and state management within the AbilitySystemGroup.

### 13.5.2 Crouch Ability
**Status:** COMPLETED ✓
**Files Created:** `CrouchSystem.cs`, `LocomotionComponents.cs`

The CrouchSystem handles crouch toggle input and updates `PlayerState.Stance`.

### 13.5.3 Sprint Ability  
**Status:** COMPLETED ✓
**Files Created:** `SprintSystem.cs`, `LocomotionComponents.cs`

The SprintSystem handles sprint input and updates `PlayerState.MovementState`.

## Network Replication

### Animation State Replication
**Status:** COMPLETED ✓

- **PlayerAnimationStateSyncSystem**: Server-side system that syncs `PlayerState.Stance` to `PlayerAnimationState.IsCrouching`
- `PlayerAnimationState` has `[GhostField]` attributes for network serialization
- `PlayerState` and `PlayerAnimationState` use `[GhostComponent(PrefabType = GhostPrefabType.All)]`

### Critical Fix: Ghost Mode Configuration
**Status:** FIXED ✓

**Problem:** Remote player crouch animations were not replicating - hosts saw remote players stuck in standing pose.

**Root Cause:** The player ghost prefab (`Warrok_Server.prefab`) had `SupportedGhostModes: 3` (All) with `DefaultGhostMode: 1` (Predicted). This caused all clients to predict all player ghosts, including remote players. When `Simulate=True`, local systems overwrite replicated values.

**Solution:** Changed `Warrok_Server.prefab` GhostAuthoringComponent:
- **Supported Ghost Modes:** `Owner Predicted` (value 2)

This ensures:
- Owner client predicts their own ghost (`Simulate=True`)
- Other clients interpolate the ghost (`Simulate=False`) and receive server-replicated values

## Files Created
- `Assets/Scripts/Player/Abilities/LocomotionComponents.cs` - Jump/Crouch/Sprint components
- `Assets/Scripts/Player/Systems/Abilities/JumpSystem.cs`
- `Assets/Scripts/Player/Systems/Abilities/CrouchSystem.cs`
- `Assets/Scripts/Player/Systems/Abilities/SprintSystem.cs`
- `Assets/Scripts/Player/Authoring/Abilities/LocomotionAbilityAuthoring.cs`
- `Assets/Scripts/Player/Systems/PlayerAnimationStateSyncSystem.cs`

## Files Modified
- `Assets/Prefabs/Warrok_Server.prefab` - Changed SupportedGhostModes to OwnerPredicted
- `Assets/Scripts/Player/Components/PlayerAnimationStateComponent.cs` - Added GhostField attributes
- `Assets/Scripts/Player/Components/PlayerStateComponent.cs` - Added GhostComponent attribute
- `Assets/Scripts/Player/Systems/Abilities/AbilitySystemGroup.cs` - Updated system ordering
- `Assets/Scripts/Player/Systems/PlayerAnimatorBridgeSystem.cs` - Fixed local player detection
- `Assets/Scripts/Player/Bridges/AnimatorRigBridge.cs` - Animation state application

## Verification Checklist
- [x] Local player can crouch (toggle with C key)
- [x] Local player can sprint (hold Shift)
- [x] Local player can jump (Space key)
- [x] Remote player crouch animation replicates to other clients
- [x] Remote player stance changes are visible to host
- [x] Animation states properly sync via PlayerAnimationState component
