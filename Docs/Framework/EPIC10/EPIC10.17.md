# EPIC 10.17: Physics & Collision Bug Fixes

**Status**: ✅ COMPLETE
**Priority**: CRITICAL  
**Dependencies**: EPIC 10.11 (Chunk Physics)

---

## Part A: Player-Terrain Collision Bug

### Problem Statement

Players are falling through terrain/voxels. The bug manifests as:
- **Only the first chunk (spawn chunk) has working collision**
- All other chunks appear visually but have no physics interaction
- Player falls through newly loaded chunks

### Root Cause Analysis

The voxel system has multiple systems responsible for physics colliders:

1. **`ChunkMeshingSystem`** (PresentationSystemGroup)
   - Creates visual meshes
   - Tags chunks with `ChunkNeedsCollider`
   - **NO WorldSystemFilter** - runs in default world only

2. **`ChunkColliderBuildSystem`** (PresentationSystemGroup, after ChunkMeshingSystem)
   - Processes `ChunkNeedsCollider` tags
   - Creates `PhysicsCollider` and `PhysicsWorldIndex` components
   - **NO WorldSystemFilter** - runs in default world only

3. **`ChunkPhysicsColliderSystem`** (SimulationSystemGroup)
   - Alternative collider creation for SERVER
   - **`WorldSystemFilter(ServerSimulation)`** - Server only

4. **`CharacterControllerSystem`** (PredictedFixedStepSimulationSystemGroup)
   - Uses `PhysicsWorldSingleton` for collision detection
   - **`WorldSystemFilter(ClientSimulation | ServerSimulation)`**

### Tasks

#### Task 10.17.1: Verify PhysicsWorldIndex Assignment ✅ FIXED
- [x] Confirm all chunk colliders use `PhysicsWorldIndex { Value = 0 }`
- [x] Verify player entity uses same `PhysicsWorldIndex`
- [x] Check if ClientWorld and ServerWorld have separate physics worlds

**Root Cause**: `ChunkColliderBuildSystem.UpdatePhysicsCollider()` only added `PhysicsWorldIndex` on first collider creation (in the `else` branch). Subsequent collider updates (e.g., after digging) would have `PhysicsCollider` but **no `PhysicsWorldIndex`**, causing them to be excluded from the physics world.

**Fix**: Moved `PhysicsWorldIndex` check outside the if/else to **always** ensure it's set after updating a collider.

**Files**: `ChunkColliderBuildSystem.cs`, `CharacterControllerSystem.cs`

---

#### Task 10.17.2: Add WorldSystemFilter to Chunk Systems ⏭️ NOT NEEDED
- [~] Add `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]` to `ChunkMeshingSystem`
- [~] Add same filter to `ChunkColliderBuildSystem`
- [~] Verify chunk streaming runs in correct worlds

**Status**: SKIPPED - Current architecture (Client=visuals, Server=physics) is intentional and working correctly after 10.17.1 fix.

**Files**: `ChunkMeshingSystem.cs`, `ChunkColliderBuildSystem.cs`, `ChunkStreamingSystem.cs`

---

#### Task 10.17.3: Force Physics World Rebuild ⏭️ NOT NEEDED
- [~] After adding PhysicsCollider, ensure physics world includes new body 
- [~] Consider using `BuildPhysicsWorld` system dependency
- [~] Verify colliders appear in next physics step

**Status**: SKIPPED - Unity Physics `BuildPhysicsWorld` system automatically rebuilds each frame. Issue was missing `PhysicsWorldIndex`, fixed in 10.17.1.

**Files**: `ChunkColliderBuildSystem.cs`

---

#### Task 10.17.4: Debug Logging for Collision
- [x] Add debug mode to `CharacterControllerSystem` to log collision hits
- [x] Add debug mode to `ChunkColliderBuildSystem` to log when colliders are created

**Files**: `CharacterControllerSystem.cs`, `ChunkColliderBuildSystem.cs`

---

#### Task 10.17.5: Immediate Collider Mode (Fallback) ⏭️ NOT NEEDED
- [~] Option to create Unity MeshCollider (not ECS PhysicsCollider) for immediate effect
- [~] Verify MeshCollider is enabled in `ChunkMeshingSystem`

**Status**: SKIPPED - ECS `PhysicsCollider` approach working correctly after 10.17.1 fix. Hybrid MeshCollider fallback not required.

**Files**: `ChunkMeshingSystem.cs`

---

## Part B: Ragdoll Death/Revival Visual Presentation ✅ COMPLETE

### Architecture Overview

The game uses a split architecture:
- **Server**: ECS entities with physics, bones as transforms, NO Animator
- **Client**: Presentation GameObjects with Animator, NO physics authority

**Solution**: Client-side visual ragdoll on the presentation GameObject.

### Dual Collider System

Chunks now have two types of colliders:

| Collider Type | Purpose | Used By |
|--------------|---------|--------|
| **ECS `Unity.Physics.MeshCollider`** | Character controller, player movement, ECS raycasts | `CharacterControllerSystem`, ECS physics queries |
| **Unity `MeshCollider`** | Ragdoll bones, loot physics, legacy Unity physics | Ragdoll Rigidbodies, `Physics.Raycast()` |

> **Note**: Both colliders share the same mesh data (no memory duplication). The Unity `MeshCollider` is added by `ChunkMeshPool` when creating chunk GameObjects.

```
┌──────────────────────────────────────────────────────────────┐
│ SERVER (ECS)                                                 │
│                                                              │
│  DeathState.Phase ────────────────────────┐                  │
│     ↓ (sets Dead/Downed on damage)        │                  │
│     ↓ (sets Alive on respawn)             │                  │
│                                           │ [GhostField]     │
│                                           │ replicates       │
└───────────────────────────────────────────│──────────────────┘
                                            ↓
┌───────────────────────────────────────────│──────────────────┐
│ CLIENT                                    ↓                  │
│                                                              │
│  RagdollPresentationBridge (MonoBehaviour)                   │
│     ↓ reads DeathState.Phase from linked entity              │
│     ↓                                                        │
│  ┌─────────────────┐    ┌─────────────────┐                  │
│  │ Dead/Downed?    │    │ Alive?          │                  │
│  │ → Disable Anim  │    │ → Enable Anim   │                  │
│  │ → Enable Ragdoll│    │ → Disable Ragdl │                  │
│  └─────────────────┘    └─────────────────┘                  │
└──────────────────────────────────────────────────────────────┘
```

### Implementation

#### Task 10.17.6: Client-Side Ragdoll Presentation ✅ COMPLETE

Created `RagdollPresentationBridge.cs` MonoBehaviour that:
- Attaches to `Warrok_Client` presentation GameObject
- Reads `DeathState.Phase` from linked ECS entity (via `[GhostField]` replication)
- On `Dead/Downed`: Disables Animator, enables Rigidbody physics on visual bones
- On `Alive`: Disables ragdoll physics, re-enables Animator
- **Unparents ragdoll root** during death to prevent ECS transform sync from interfering
- **Reparents ragdoll root** on recovery to restore normal animation

**Files**:
- New: `Assets/Scripts/Player/Animation/RagdollPresentationBridge.cs`

---

#### Task 10.17.6a: Camera Follow During Ragdoll ✅ COMPLETE

Modified `CameraManager.cs` to:
- Detect when player is in ragdoll state via `RagdollPresentationBridge.IsRagdolled`
- Follow ragdoll hips position instead of ECS entity transform
- Smooth transition back to normal camera on recovery

**Files**:
- Modified: `Assets/Scripts/Systems/Camera/CameraManager.cs`

---

#### Task 10.17.6b: Ragdoll Terrain Collision ✅ COMPLETE

Fixed ragdoll falling through terrain by adding Unity `MeshCollider` to chunk GameObjects:
- Modified `ChunkMeshPool.cs` to add `MeshCollider` component on pool creation
- Modified `ChunkMeshingSystem.cs` to store `MeshCollider` reference in `ChunkGameObject`
- Ragdoll bones (Unity physics) now collide with chunk colliders (Unity physics)

**Root Cause**: Ragdoll uses Unity `Rigidbody` physics, but terrain only had ECS `Unity.Physics.MeshCollider`. These are separate physics systems that don't interact.

**Files**:
- Modified: `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshPool.cs`
- Modified: `Assets/Scripts/Voxel/Systems/Meshing/ChunkMeshingSystem.cs`

---

#### Task 10.17.7: Ragdoll Unity Setup on Warrok_Client ✅ COMPLETE

The `Warrok_Client.prefab` has Unity Ragdoll components on the visual model:

1. [x] Used Unity's **Ragdoll Wizard** on the visual skeleton
2. [x] All bone Rigidbodies set to **Is Kinematic = true** (disabled by default)
3. [x] `RagdollPresentationBridge` component attached to prefab root
4. [x] `RagdollRoot` (Hips) and `PlayerAnimator` assigned

**Files**:
- `Assets/Prefabs/Warrok_Client.prefab`

---

#### Task 10.17.8: Server-Side Systems (Deprecated)

Previous server-side ragdoll systems are no longer needed for gameplay:
- `RagdollTransitionSystem.cs` - Can be removed or kept for future server-auth ragdoll
- `RagdollRecoverySystem.cs` - Can be removed or kept for future server-auth ragdoll
- `RagdollAuthoring.cs` - No changes needed on server prefab

---

### Setup Guide: Warrok_Client Visual Ragdoll

#### Step 1: Create Unity Ragdoll on Client Model

1. Open `Warrok_Client.prefab`
2. Select the armature root (e.g., `Armature` or skeleton root)
3. `GameObject → 3D Object → Ragdoll...`
4. Assign bones:
   - Pelvis → Hips
   - Left/Right Hips → Upper legs
   - Left/Right Knee → Lower legs
   - Left/Right Arm → Upper arms
   - Left/Right Elbow → Lower arms
   - Middle Spine → Spine1/Spine2
   - Head → Head
5. Total Mass: 80 kg
6. Click **Create**

#### Step 2: Configure Ragdoll Bones

For EACH Rigidbody added by the wizard:
- **Is Kinematic**: ✅ **ENABLED** (starts disabled, script enables on death)
- **Use Gravity**: ✅ Enabled
- Collider: Keep as-is

#### Step 3: Add RagdollPresentationBridge

1. Select `Warrok_Client` root GameObject
2. Add Component → `RagdollPresentationBridge`
3. Assign:
   - **Player Animator**: The Animator component
   - **Ragdoll Root**: The Hips/Pelvis bone
4. Save prefab

#### Step 4: Test

1. Enter Play Mode as Host
2. Take damage until dead (use debug key if available)
3. Verify: Animator stops, ragdoll physics activates
4. Respawn (wait or use debug key)
5. Verify: Ragdoll deactivates, Animator resumes

---

## Part C: Mining Loot Physics

### Problem Statement

When voxels are mined and loot drops:
- Loot should have a **small explosion force** that spreads items outward
- Loot should have **proper physics weight** for satisfying movement
- Currently loot may not interact properly with terrain or player

### Current Implementation Analysis

#### `LootSpawnServerSystem.cs` / `VoxelLootSystem.cs`

Current velocity calculation:
```csharp
// Velocity: normalized random direction * 2f (weak impulse)
float3 vel = math.normalize(new float3(
    _random.NextFloat(-1f, 1f),
    2f,  // Slight upward bias
    _random.NextFloat(-1f, 1f)
)) * 2f;
```

**Issues:**
1. **Weak impulse** - Velocity magnitude of 2 is too low for satisfying "pop"
2. **No radial explosion** - Items don't scatter from a center point
3. **No material-based weight** - All loot uses same physics properties

#### Loot Prefab Setup (`VoxelQuickSetup.cs`, `BatchMaterialImporter.cs`)

```csharp
var rb = loot.AddComponent<Rigidbody>();
rb.mass = 0.5f;  // or 1f in some cases
// No drag configuration
// No angular drag configuration
// Uses Unity Rigidbody, not ECS PhysicsVelocity
```

**Issues:**
1. **Low mass** - 0.5-1kg feels floaty
2. **No drag tuning** - Items may slide indefinitely
3. **Hybrid physics** - Uses Unity Rigidbody while terrain uses ECS PhysicsCollider

### Tasks

#### Task 10.17.11: Implement Explosion Scatter Force
- [x] Calculate radial direction from mine point center
- [x] Add configurable `LootScatterForce` parameter (default ~5-8f)
- [x] Apply upward bias for arc trajectory
- [x] Randomize force slightly per item for natural spread

**Files**:
- `VoxelLootSystem.cs`
- `LootSpawnNetworkSystem.cs`

---

#### Task 10.17.12: Configure Loot Physics Weight
- [x] Increase Rigidbody mass to 2-5kg for "chunky" feel
- [x] Add drag (0.5-1.0) to prevent infinite sliding
- [x] Add angular drag (0.5-1.0) for realistic tumbling
- [x] Consider material-specific mass (ore heavier than dirt)

**Files**:
- `VoxelQuickSetup.cs`
- `BatchMaterialImporter.cs`
- `VoxelTestAssetCreator.cs`
- Existing loot prefabs in `Assets/Prefabs/Loot/`

---

#### Task 10.17.13: Add Loot Physics Settings ScriptableObject
- [x] Create `LootPhysicsSettings` ScriptableObject
- [x] Expose: `ScatterForce`, `UpwardBias`, `BaseMass`, `Drag`, `AngularDrag`
- [x] Per-material mass multiplier in `VoxelMaterialDefinition`
- [x] Load and apply settings in loot spawn systems

**Files**:
- New: `LootPhysicsSettings.cs` ✓
- `VoxelMaterialDefinition.cs` ✓

---

#### Task 10.17.14: Ensure Loot Collides With Terrain
- [x] Verify loot Rigidbody uses correct collision layer
- [x] Test that loot lands on and bounces off voxel terrain
- [x] Handle edge case where loot spawns inside terrain (push outward)
- [x] Use custom ECS Proxy System to allow GameObject loot to collide with ECS terrain (`LootPhysicsProxySystem`)
- [x] Implement manual physics simulator (`LootPhysicsSimulator`) for NetCode environments with `Physics.simulationMode = Script`

**Files**:
- Loot prefab layer settings
- May need overlap check in spawn logic
- New: `LootPhysicsProxySystem.cs`
- New: `LootPhysicsSimulator.cs`

---

#### Task 10.17.15: Loot Lifetime and Cleanup
- [x] Add lifetime component to loot GameObjects
- [x] Auto-destroy after configurable time (30-60s)
- [x] Optional: fade-out effect before destruction
- [x] Prevent memory leak from accumulated loot

**Files**:
- New: `LootLifetime.cs` MonoBehaviour ✓
- Attach to loot prefabs ✓

---

#### Task 10.17.16: Fix Voxel Collider Updates After Digging
- [x] Investigate why colliders don't update after digging (mesh updates but physics hole remains closed)
- [x] Ensure `ChunkNeedsCollider` is correctly re-added after mesh regeneration
- [x] Verify `ChunkColliderBuildSystem` removes old collider blob before adding new one
- [x] Confirm player can fall into dug holes (physics shape matches visual shape)

**Files**:
- `VoxelModificationSystems.cs`
- `ChunkMeshingSystem.cs`
- `ChunkColliderBuildSystem.cs`

---

## Acceptance Criteria

### Part A (Terrain Collision) ✅ COMPLETE
- [x] Player can walk on all loaded chunks without falling through
- [x] New chunks become collidable within 1-2 frames of mesh creation
- [x] Works in both Host and Client modes

### Part B (Ragdoll) ✅ COMPLETE
- [x] Ragdoll bones collide with terrain on death (via Unity `MeshCollider` on chunks)
- [x] Camera follows ragdoll hips position during death
- [x] Ragdoll unparents from ECS sync during death, reparents on recovery
- [x] Revival/respawn restores player physics state
- [x] Player has working collision after revival
- [x] `CharacterControllerSystem` works correctly post-revival

### Part C (Loot Physics)
- [x] Loot visibly "pops" outward when spawned
- [x] Loot has satisfying weight when pushed/moved
- [x] Loot lands and settles on terrain properly
- [x] No sliding forever on flat surfaces

---

## Files Summary

| File | Tasks |
|------|-------|
| `ChunkMeshingSystem.cs` | 10.17.2, 10.17.5, 10.17.6b |
| `ChunkMeshPool.cs` | 10.17.6b (Unity MeshCollider for ragdoll) |
| `ChunkColliderBuildSystem.cs` | 10.17.1, 10.17.2, 10.17.3, 10.17.4 |
| `ChunkPhysicsColliderSystem.cs` | 10.17.1 |
| `CharacterControllerSystem.cs` | 10.17.4 |
| `CameraManager.cs` | 10.17.6a (ragdoll camera follow) |
| `RagdollPresentationBridge.cs` | 10.17.6, 10.17.6a, 10.17.6b |
| `RagdollTransitionSystem.cs` | 10.17.7 |
| `RagdollComponents.cs` | 10.17.7, 10.17.9 |
| `RagdollAuthoringBaker.cs` | 10.17.8 |
| `RagdollRecoverySystem.cs` | 10.17.6, 10.17.10 |
| `DownedRulesSystem.cs` | Integration with 10.17.6 |
| `RespawnSystem.cs` | Integration with 10.17.6 |
| `VoxelLootSystem.cs` | 10.17.11 |
| `LootSpawnNetworkSystem.cs` | 10.17.11 |
| `VoxelQuickSetup.cs` | 10.17.12 |
| `BatchMaterialImporter.cs` | 10.17.12 |
| `VoxelMaterialDefinition.cs` | 10.17.13 |
| `LootPhysicsSettings.cs` | 10.17.13 |
| `LootLifetime.cs` | 10.17.15 |


