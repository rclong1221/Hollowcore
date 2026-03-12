using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace DIG.VFX.Systems
{
    /// <summary>
    /// EPIC 16.7: Computes distance to camera per VFX request, assigns LOD tier,
    /// creates VFXResolvedLOD, and culls requests beyond MinimalDistance.
    /// Managed — Camera.main access prevents Burst compilation.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(VFXBudgetSystem))]
    public partial struct VFXLODSystem : ISystem
    {
        private EntityQuery _requestQuery;
        static readonly ProfilerMarker k_Marker = new("VFXLODSystem.Assign");

        public void OnCreate(ref SystemState state)
        {
            _requestQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<VFXRequest>(),
                ComponentType.ReadWrite<VFXCulled>()
            );
            state.RequireForUpdate<VFXLODConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int entityCount = _requestQuery.CalculateEntityCount();
            if (entityCount == 0) return;

            k_Marker.Begin();

            // Cache camera position once per frame
            var cam = Camera.main;
            if (cam == null)
            {
                k_Marker.End();
                return;
            }

            float3 cameraPos = cam.transform.position;
            var lodConfig = SystemAPI.GetSingleton<VFXLODConfig>();

            var entities = _requestQuery.ToEntityArray(Allocator.Temp);
            var requests = _requestQuery.ToComponentDataArray<VFXRequest>(Allocator.Temp);

            // Use ECB for structural changes instead of EntityManager in-loop
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                // Skip already culled by budget
                if (state.EntityManager.IsComponentEnabled<VFXCulled>(entities[i]))
                    continue;

                float dist = math.distance(requests[i].Position, cameraPos);
                DIG.Surface.EffectLODTier tier;

                if (dist < lodConfig.FullDistance)
                    tier = DIG.Surface.EffectLODTier.Full;
                else if (dist < lodConfig.ReducedDistance)
                    tier = DIG.Surface.EffectLODTier.Reduced;
                else if (dist < lodConfig.MinimalDistance)
                    tier = DIG.Surface.EffectLODTier.Minimal;
                else
                {
                    // Beyond minimal distance — cull
                    state.EntityManager.SetComponentEnabled<VFXCulled>(entities[i], true);
                    continue;
                }

                // Add/set resolved LOD via ECB to avoid structural changes in-loop
                if (!state.EntityManager.HasComponent<VFXResolvedLOD>(entities[i]))
                {
                    ecb.AddComponent(entities[i], new VFXResolvedLOD
                    {
                        Tier = tier,
                        DistanceToCamera = dist
                    });
                }
                else
                {
                    ecb.SetComponent(entities[i], new VFXResolvedLOD
                    {
                        Tier = tier,
                        DistanceToCamera = dist
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            entities.Dispose();
            requests.Dispose();

            k_Marker.End();
        }
    }
}
