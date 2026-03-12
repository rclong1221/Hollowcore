using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Mathematics;
using UnityEngine;
using DIG.Voxel.Interaction;
using RaycastHit = Unity.Physics.RaycastHit;

namespace DIG.Voxel.Systems.Network
{
    /// <summary>
    /// Bridges ECS Physics (terrain) and GameObject loot (LootPhysicsSimulator).
    /// Performs ECS raycasts for active LootPhysicsSimulators to allow them to collide with Voxel terrain.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))] // Update with rendering/GOs
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class LootPhysicsProxySystem : SystemBase
    {
        private PhysicsWorldSingleton _physicsWorldSingleton;

        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsWorldSingleton>();
        }

        protected override void OnUpdate()
        {
             if (LootPhysicsSimulator.ActiveSimulators.Count == 0) return;
             
             _physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
             var collisionWorld = _physicsWorldSingleton.CollisionWorld;
             float dt = SystemAPI.Time.DeltaTime;
             
             // Iterate backwards to allow removal if enabled=false
             for (int i = LootPhysicsSimulator.ActiveSimulators.Count - 1; i >= 0; i--)
             {
                 var sim = LootPhysicsSimulator.ActiveSimulators[i];
                 if (sim == null || !sim.enabled) continue;
                 
                 // 1. Depenetration (Outward Push) for embedded loot
                 float3 pos = (float3)sim.transform.position;
                 var distInput = new PointDistanceInput
                 {
                     Position = pos,
                     MaxDistance = 0.5f,
                     Filter = CollisionFilter.Default
                 };
                 
                 // Check if we are inside or very close to geometry
                 if (collisionWorld.CalculateDistance(distInput, out DistanceHit distHit))
                 {
                     float dist = math.distance(pos, distHit.Position);
                     
                     // If too close (or inside), push out
                     // Note: CalculateDistance returns positive distance to closest surface point even if inside mesh
                     // We rely on simple "too close" logic to push away from surface normal
                     if (dist < 0.35f)
                     {
                         float3 push = distHit.SurfaceNormal * (0.35f - dist);
                         // Apply immediate depenetration
                         sim.transform.position += (Vector3)push;
                         pos += push; // Update local pos for Raycast
                     }
                 }
                 
                 // 2. Ground Raycast (Gravity)
                 // Reverted from 200m to 1m relative to allow Cave spawning (finding floor BELOW, not surface ABOVE)
                 float3 start = pos + new float3(0, 1.0f, 0); 
                 float3 direction = new float3(0, -1, 0); // Down
                 float distance = 2.0f; // Check 2m total vertical range
                 
                  
                 var input = new RaycastInput
                 {
                     Start = start,
                     End = start + (direction * distance),
                     Filter = CollisionFilter.Default 
                 };

                 bool hit = collisionWorld.CastRay(input, out RaycastHit raycastHit);
                 

                 


                 // Also check velocity direction if moving fast? 
                 // For now, stick to simple ground check as per original simulator logic
                 
                 Vector3 hitPoint = hit ? (Vector3)raycastHit.Position : Vector3.zero;
                 Vector3 hitNormal = hit ? (Vector3)raycastHit.SurfaceNormal : Vector3.up;
                 
                 sim.ManualUpdate(dt, hit, hitPoint, hitNormal);
             }
        }
    }
}
