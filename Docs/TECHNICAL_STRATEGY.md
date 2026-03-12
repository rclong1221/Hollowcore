# Technical Strategy: High-Performance ECS/DOTS Architecture

To achieve the "hyper-performant" goal for DIG (Ship-Drill-Voxel Horror), we will leverage the full DOTS stack. This document outlines how we apply specific technologies to our core gameplay pillars.

## 1. Core Architecture Pattern: "Systems over Objects"

We strictly follow **Data-Oriented Design**. We do not think in "Classes" or "Managers", but in **Data Streams**.

*   **Archetypes**: We group entities by their component combinations (e.g., `Monster` + `Health` + `Translation`).
*   **Burst Compiler**: All gameplay logic (Systems) must be `[BurstCompile]`. We avoid managed types (strings, classes) in hot paths.
*   **Job System**: Heavy calculations (Voxel meshing, AI pathing) run on worker threads, leaving the Main Thread free for Unity API calls.

## 2. Domain-Specific Implementation

### A. The Voxel World (Generation & Rendering)
*Challenge: Generating and rendering infinite caves without lag.*

*   **Data**: `BlobAsset` or `NativeArray` for voxel density data.
*   **Generation (Jobs)**:
    *   **Noise**: Use `Unity.Mathematics` noise in parallel Jobs to calculate density.
    *   **Meshing**: Marching Cubes algorithm running in `IJobChunk` or `IJobParallelFor`.
*   **Rendering**:
    *   **Entities Graphics**: Instead of creating GameObjects for chunks, we generate `Mesh` data in jobs and assign it to Entities with `RenderMesh`.
    *   **LOD**: Use `LODGroup` components on chunks to lower vertex count for distant cave sections.

### B. The Ship (Modular & Physics)
*Challenge: A moving platform containing players, modules, and physics interactions.*

*   **Structure**: The Ship is a root Entity. Modules (Drills, Thrusters) are child entities linked via `LinkedEntityGroup`.
*   **Physics**:
    *   **Unity Physics**: The ship uses `PhysicsBody` and `PhysicsShape`.
    *   **Internal Gravity**: We don't simulate internal physics. Players are "parented" (logically) to the ship relative to its transform, or we use a custom "Relative Motion" system if they are free-walking.
*   **Damage**: A `DynamicBuffer<DamageEvent>` on the ship entity records hits. A generic `DamageSystem` processes these in parallel.

### C. Swarm AI (Bugs & Worms)
*Challenge: Hundreds of enemies attacking simultaneously.*

*   **Spatial Query**: Use `Unity.Physics.CollisionWorld` or a custom `NativeMultiHashMap` spatial grid for finding targets.
*   **Behavior**:
    *   **Flow Fields**: Instead of A* for every bug, calculate a "Flow Field" (vector grid) towards the ship in a Job. Bugs just look up the vector at their position.
    *   **Steering**: Boids simulation (Separation, Alignment, Cohesion) running in a single parallel job for 1000+ units.
*   **Animation**: Vertex Animation Textures (VAT) or `GhostAnimationController` (Netcode) to avoid Animator overhead.

## 3. Netcode & Networking
*Challenge: Syncing a deformable world and high-speed ship.*

*   **Netcode for Entities**:
    *   **Prediction**: The local player and the ship (for the pilot) are predicted.
    *   **Interpolation**: Other players and monsters are interpolated.
*   **Snapshotting**: The Voxel world modification is too big for snapshots. We sync "Modification Events" (e.g., "Sphere removed at (x,y,z)") and re-simulate the voxel change deterministically on clients.
*   **Transport**: Use `Unity Transport` (UDP) for raw speed.

## 4. Tech Stack Mapping

| Feature | Technology | Usage |
| :--- | :--- | :--- |
| **Math** | `Unity.Mathematics` | Use `float3`, `quaternion` everywhere. SIMD optimized. |
| **Collections** | `Unity.Collections` | `NativeArray`, `NativeList` for passing data between Jobs. |
| **Physics** | `Unity Physics` | Stateless collision detection for ship/drills. |
| **Rendering** | `Entities Graphics` | Batch rendering of thousands of bullets/bugs. |
| **Particles** | `VFX Graph` | GPU particles driven by ECS Events (e.g., Drill sparks). |
| **Loading** | `Async Scenes` | Stream in "Zone" subscenes as the ship descends. |

## 5. Performance Rules (The "Golden Path")
1.  **No Main Thread Logic**: If it involves a loop, it goes in a Job.
2.  **Zero GC in Update**: No `new` allocations in OnUpdate. Use pre-allocated NativeContainers.
3.  **Structure of Arrays (SoA)**: ECS does this by default. Keep components small and focused.
4.  **Cache Locality**: Iterate over contiguous arrays of component data (ArchetypeChunks).

