# EPIC 8.4: Blocky Collision

**Status**: 🔲 NOT STARTED  
**Priority**: CRITICAL ⭐ MOST IMPORTANT EPIC  
**Dependencies**: EPIC 8.3 (Blocky Meshing)
**Estimated Time**: 1 day

---

## Goal

**THE SINGLE MOST IMPORTANT EPIC.**

Player MUST collide with voxel terrain. They CANNOT fall through.

---

## Previous Failure Modes

| Symptom | Cause | Our Fix |
|---------|-------|---------|
| Falls through | Layer matrix disabled | Check Physics settings first |
| Falls through | Empty mesh | Validate mesh.vertexCount > 0 |
| Falls through | MeshCollider disabled | Enable after mesh assignment |
| Falls through | Wrong layer | Set layer on GameObject |
| Falls through | Mesh not readable | Use UploadMeshData(false) |
| Falls through | Collider not baked | Assign sharedMesh properly |

---

## Pre-Requisite Checklist

Before writing any code, verify in Unity Editor:

### Physics Layer Setup

1. Open **Edit → Project Settings → Tags and Layers**
2. Verify or create:
   - Layer 8: "Player" (or your player layer)
   - Layer 9: "Voxel"

3. Open **Edit → Project Settings → Physics**
4. In the Layer Collision Matrix:
   - ✅ Player ↔ Voxel = ENABLED
   - ✅ Player ↔ Default = ENABLED

### Verify Player Setup

1. Open player prefab
2. Verify player has collider (Capsule/CharacterController)
3. Verify player layer is "Player"
4. Verify player has Rigidbody or uses CharacterController

---

## Tasks

### Task 8.4.1: Update ChunkMeshPool for Collision

**File**: Update `ChunkMeshPool.cs`

```csharp
private GameObject CreateChunkGameObject()
{
    _createdCount++;
    var go = new GameObject($"Chunk_{_createdCount}");
    go.transform.SetParent(transform);
    
    // Add components
    go.AddComponent<MeshFilter>();
    var renderer = go.AddComponent<MeshRenderer>();
    renderer.sharedMaterial = _voxelMaterial;
    
    // COLLISION SETUP
    var collider = go.AddComponent<MeshCollider>();
    collider.convex = false;  // MUST be false for terrain
    collider.cookingOptions = 
        MeshColliderCookingOptions.CookForFasterSimulation |
        MeshColliderCookingOptions.EnableMeshCleaning |
        MeshColliderCookingOptions.WeldColocatedVertices;
    collider.enabled = false; // Will enable after mesh is assigned
    
    // CRITICAL: Set correct layer
    go.layer = LayerMask.NameToLayer("Voxel");
    if (go.layer == -1)
    {
        Debug.LogError("[Voxel] 'Voxel' layer not found! Create it in Project Settings.");
        go.layer = 0; // Fallback to Default
    }
    
    return go;
}
```

---

### Task 8.4.2: Create ChunkCollisionSystem

**File**: `Assets/Scripts/Voxel/Systems/Collision/ChunkCollisionSystem.cs`

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel.Systems.Collision
{
    /// <summary>
    /// Assigns and manages MeshColliders for voxel chunks.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Meshing.ChunkMeshingSystem))]
    public partial class ChunkCollisionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (meshState, colliderState, entity) in 
                SystemAPI.Query<RefRO<ChunkMeshState>, RefRW<ChunkColliderState>>()
                    .WithEntityAccess())
            {
                // Skip if no mesh or already has collider
                if (!meshState.ValueRO.HasMesh) continue;
                if (colliderState.ValueRO.HasCollider && colliderState.ValueRO.IsActive) continue;
                
                // Get GameObject
                if (!EntityManager.HasComponent<ChunkGameObject>(entity)) continue;
                var chunkGO = EntityManager.GetComponentObject<ChunkGameObject>(entity);
                
                AssignCollider(chunkGO, ref colliderState.ValueRW);
            }
        }
        
        private void AssignCollider(ChunkGameObject chunkGO, ref ChunkColliderState state)
        {
            var meshFilter = chunkGO.MeshFilter;
            var meshCollider = chunkGO.MeshCollider;
            
            if (meshFilter == null || meshCollider == null) return;
            
            var mesh = meshFilter.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0)
            {
                Debug.LogWarning("[Voxel] Cannot assign collider: mesh is empty");
                return;
            }
            
            // CRITICAL SEQUENCE:
            // 1. Disable collider
            meshCollider.enabled = false;
            
            // 2. Clear existing mesh
            meshCollider.sharedMesh = null;
            
            // 3. Assign new mesh
            meshCollider.sharedMesh = mesh;
            
            // 4. Enable collider
            meshCollider.enabled = true;
            
            // 5. Validate
            ValidateCollider(chunkGO.Value, meshCollider, mesh);
            
            state.HasCollider = true;
            state.IsActive = true;
        }
        
        private void ValidateCollider(GameObject go, MeshCollider collider, Mesh mesh)
        {
            // Test 1: Mesh has data
            if (mesh.vertexCount == 0)
            {
                Debug.LogError($"[Voxel] {go.name}: Mesh has 0 vertices!");
                return;
            }
            
            // Test 2: Collider is enabled
            if (!collider.enabled)
            {
                Debug.LogError($"[Voxel] {go.name}: Collider is disabled!");
                return;
            }
            
            // Test 3: Collider has mesh
            if (collider.sharedMesh == null)
            {
                Debug.LogError($"[Voxel] {go.name}: Collider sharedMesh is null!");
                return;
            }
            
            // Test 4: Layer is correct
            int voxelLayer = LayerMask.NameToLayer("Voxel");
            if (go.layer != voxelLayer)
            {
                Debug.LogWarning($"[Voxel] {go.name}: Layer is {go.layer}, expected {voxelLayer}");
            }
            
            // Test 5: Raycast test
            Vector3 testPos = go.transform.position + mesh.bounds.center + Vector3.up * 10f;
            if (Physics.Raycast(testPos, Vector3.down, out RaycastHit hit, 20f))
            {
                if (hit.collider == collider)
                {
                    Debug.Log($"[Voxel] ✅ {go.name}: Collision validated at Y={hit.point.y:F1}");
                }
                else
                {
                    Debug.LogWarning($"[Voxel] {go.name}: Raycast hit different collider: {hit.collider.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[Voxel] {go.name}: Raycast from {testPos} hit nothing");
            }
        }
    }
}
```

---

### Task 8.4.3: Create Collision Debug Visualizer

**File**: `Assets/Scripts/Voxel/Debug/CollisionDebugVisualizer.cs`

```csharp
using UnityEngine;

namespace DIG.Voxel.Debug
{
    /// <summary>
    /// Debug tool to visualize and test voxel collision.
    /// Attach to any GameObject in scene.
    /// </summary>
    public class CollisionDebugVisualizer : MonoBehaviour
    {
        [Header("Raycast Test")]
        public bool testRaycastEveryFrame = true;
        public float raycastDistance = 100f;
        
        [Header("Layer Info")]
        public bool showLayerInfo = true;
        
        private Camera _cam;
        
        private void Start()
        {
            _cam = Camera.main;
            LogLayerMatrix();
        }
        
        private void Update()
        {
            if (testRaycastEveryFrame)
            {
                TestRaycast();
            }
        }
        
        private void TestRaycast()
        {
            if (_cam == null) return;
            
            Vector3 origin = _cam.transform.position;
            Vector3 direction = Vector3.down;
            
            int voxelLayerMask = 1 << LayerMask.NameToLayer("Voxel");
            
            if (Physics.Raycast(origin, direction, out RaycastHit hit, raycastDistance, voxelLayerMask))
            {
                UnityEngine.Debug.DrawLine(origin, hit.point, Color.green);
                UnityEngine.Debug.DrawRay(hit.point, hit.normal, Color.yellow);
            }
            else
            {
                UnityEngine.Debug.DrawRay(origin, direction * raycastDistance, Color.red);
            }
        }
        
        private void LogLayerMatrix()
        {
            if (!showLayerInfo) return;
            
            int playerLayer = LayerMask.NameToLayer("Player");
            int voxelLayer = LayerMask.NameToLayer("Voxel");
            
            UnityEngine.Debug.Log($"[Collision Debug] Player layer: {playerLayer}");
            UnityEngine.Debug.Log($"[Collision Debug] Voxel layer: {voxelLayer}");
            
            // Check if layers can collide
            bool canCollide = !Physics.GetIgnoreLayerCollision(playerLayer, voxelLayer);
            UnityEngine.Debug.Log($"[Collision Debug] Player↔Voxel collision: {(canCollide ? "ENABLED ✅" : "DISABLED ❌")}");
            
            if (!canCollide)
            {
                UnityEngine.Debug.LogError("[Collision Debug] Player and Voxel layers cannot collide! " +
                    "Check Edit → Project Settings → Physics → Layer Collision Matrix");
            }
        }
        
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("[Collision Debug]");
            
            if (GUILayout.Button("Test Raycast Down"))
            {
                TestRaycastManual();
            }
            
            if (GUILayout.Button("Find All Voxel Colliders"))
            {
                FindVoxelColliders();
            }
            
            GUILayout.EndArea();
        }
        
        private void TestRaycastManual()
        {
            Vector3 origin = _cam.transform.position;
            
            // Test all layers
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f))
            {
                UnityEngine.Debug.Log($"[Collision Debug] Hit: {hit.collider.name} at Y={hit.point.y:F2}, layer={hit.collider.gameObject.layer}");
            }
            else
            {
                UnityEngine.Debug.Log("[Collision Debug] Raycast hit nothing!");
            }
        }
        
        private void FindVoxelColliders()
        {
            var colliders = FindObjectsOfType<MeshCollider>();
            int voxelLayer = LayerMask.NameToLayer("Voxel");
            int voxelCount = 0;
            int enabledCount = 0;
            
            foreach (var col in colliders)
            {
                if (col.gameObject.layer == voxelLayer)
                {
                    voxelCount++;
                    if (col.enabled && col.sharedMesh != null && col.sharedMesh.vertexCount > 0)
                    {
                        enabledCount++;
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[Collision Debug] Broken collider: {col.name}, " +
                            $"enabled={col.enabled}, mesh={(col.sharedMesh != null ? col.sharedMesh.vertexCount.ToString() : "null")} verts");
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[Collision Debug] Found {voxelCount} voxel colliders, {enabledCount} working");
        }
    }
}
```

---

### Task 8.4.4: Update Player Collision Mask

If using KinematicCharacterController or custom physics:

```csharp
// Ensure your player queries include Voxel layer
int collisionMask = ~0; // All layers

// OR explicitly include
int collisionMask = 
    (1 << LayerMask.NameToLayer("Default")) |
    (1 << LayerMask.NameToLayer("Voxel"));

// Use in Physics queries
Physics.Raycast(origin, direction, out hit, distance, collisionMask);
Physics.CapsuleCast(..., collisionMask);
Physics.SphereCast(..., collisionMask);
```

---

## Testing Procedure

### Test 1: Static Raycast
```
1. Place camera above terrain at Y=20
2. Run CollisionDebugVisualizer
3. Click "Test Raycast Down"
4. Expected: "Hit: Chunk_X at Y=0.0, layer=9"
```

### Test 2: Player Landing
```
1. Place player at Y=10, above terrain
2. Enable gravity
3. Press Play
4. Expected: Player falls and STOPS at Y≈0
```

### Test 3: Player Walking
```
1. After landing, use WASD to walk
2. Expected: Player stays on terrain
3. Expected: Player cannot walk through walls
```

### Test 4: Boundary Test
```
1. Walk to chunk boundary
2. Continue walking
3. Expected: No falling through at boundaries
```

---

## Failure Debugging

### If Player Falls Through:

**Step 1**: Check layer matrix
```
Edit → Project Settings → Physics
Is Player ↔ Voxel enabled? ✅
```

**Step 2**: Check GameObject layer
```
Select chunk in Hierarchy
Inspector → Layer = Voxel? ✅
```

**Step 3**: Check collider state
```
Select chunk in Hierarchy
MeshCollider → enabled = true? ✅
MeshCollider → sharedMesh = (mesh)? ✅
MeshCollider → convex = false? ✅
```

**Step 4**: Check mesh data
```
MeshFilter → sharedMesh → vertexCount > 0? ✅
```

**Step 5**: Check player collision mask
```
Does your player code include Voxel layer in queries?
```

---

## Emergency Fallback

If MeshCollider continues to fail, use BoxColliders:

```csharp
// Instead of MeshCollider, generate BoxColliders for surface voxels
private void GenerateBoxColliders(GameObject go, NativeArray<byte> densities)
{
    for (int i = 0; i < densities.Length; i++)
    {
        if (!VoxelDensity.IsSolid(densities[i])) continue;
        
        int3 localPos = CoordinateUtils.IndexToVoxelPos(i);
        
        // Check if this voxel has any exposed face
        bool isExposed = HasExposedFace(localPos, densities);
        if (!isExposed) continue;
        
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(localPos.x + 0.5f, localPos.y + 0.5f, localPos.z + 0.5f);
        box.size = Vector3.one;
    }
}
```

This is slower but guaranteed to work.

---

## Acceptance Criteria

**ALL MUST PASS:**

- [ ] Player lands on terrain (not falling forever)
- [ ] Player walks on terrain without falling through
- [ ] Player cannot walk through solid voxels
- [ ] `Physics.Raycast` from camera hits terrain
- [ ] CollisionDebugVisualizer shows green rays (hitting)
- [ ] No console errors about missing colliders
- [ ] Works at chunk boundaries

---

## STOP GATE ⚠️

**DO NOT PROCEED TO 8.5 UNTIL:**

✅ Player can stand on terrain
✅ Player can walk on terrain
✅ Player cannot walk through solid voxels

This is the foundation. Everything else depends on this working.
