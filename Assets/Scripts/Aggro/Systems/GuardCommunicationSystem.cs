using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Propagates alert levels between guards with GuardCommunication flag.
    ///
    /// Rules:
    /// - Guard at COMBAT → nearby guards become SEARCHING
    /// - Guard at SEARCHING → nearby guards become SUSPICIOUS
    /// - Guard at SUSPICIOUS → nearby guards become CURIOUS
    /// Communication range = CallForHelpRadius from SocialAggroConfig.
    ///
    /// Creates a cascading alert wave across a guard network.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AlertStateSystem))]
    [BurstCompile]
    public partial struct GuardCommunicationSystem : ISystem
    {
        EntityQuery _guardQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _guardQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<AlertState>()
                .WithAll<SocialAggroConfig, LocalTransform>()
                .Build(ref state);
            state.RequireForUpdate(_guardQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            var entities = _guardQuery.ToEntityArray(Allocator.Temp);
            var socialConfigs = _guardQuery.ToComponentDataArray<SocialAggroConfig>(Allocator.Temp);
            var alerts = _guardQuery.ToComponentDataArray<AlertState>(Allocator.Temp);
            var transforms = _guardQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            int count = entities.Length;

            // Propagate alert from high-alert guards to nearby lower-alert guards
            for (int i = 0; i < count; i++)
            {
                if ((socialConfigs[i].Flags & SocialAggroFlags.GuardCommunication) == 0) continue;
                if (alerts[i].AlertLevel < AlertState.SUSPICIOUS) continue;

                int propagateLevel = alerts[i].AlertLevel - 1;
                float range = socialConfigs[i].CallForHelpRadius;
                float rangeSq = range * range;
                float3 srcPos = transforms[i].Position;

                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    if ((socialConfigs[j].Flags & SocialAggroFlags.GuardCommunication) == 0) continue;
                    if (alerts[j].AlertLevel >= propagateLevel) continue;

                    float distSq = math.distancesq(srcPos, transforms[j].Position);
                    if (distSq > rangeSq) continue;

                    var a = alerts[j];
                    a.AlertLevel = propagateLevel;
                    a.AlertTimer = GetAlertDuration(propagateLevel);
                    a.AlertSource = alerts[i].AlertSource;
                    a.AlertPosition = alerts[i].AlertPosition;

                    if (propagateLevel >= AlertState.SEARCHING)
                    {
                        a.InvestigatePosition = alerts[i].AlertPosition;
                        a.SearchTimer = 0f;
                        a.HasInvestigated = false;
                    }

                    alerts[j] = a;
                }
            }

            // Bulk write-back (single memcpy vs N individual SetComponentData calls)
            _guardQuery.CopyFromComponentDataArray(alerts);

            entities.Dispose();
            socialConfigs.Dispose();
            alerts.Dispose();
            transforms.Dispose();
        }

        private static float GetAlertDuration(int alertLevel)
        {
            return alertLevel switch
            {
                AlertState.COMBAT => 3.0f,
                AlertState.SEARCHING => 10.0f,
                AlertState.SUSPICIOUS => 8.0f,
                AlertState.CURIOUS => 5.0f,
                _ => 0f
            };
        }
    }
}
