using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;
using Player.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Guards with BodyDiscovery flag detect dead allies within range
    /// and raise their alert to SUSPICIOUS, setting InvestigatePosition to the corpse.
    ///
    /// Only triggers when the guard is at IDLE or CURIOUS level (already alerted guards skip).
    /// Chains with GuardCommunicationSystem to relay the alert across guard networks.
    /// Discovery range = CallForHelpRadius from SocialAggroConfig.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GuardCommunicationSystem))]
    [BurstCompile]
    public partial struct BodyDiscoverySystem : ISystem
    {
        EntityQuery _discovererQuery;
        EntityQuery _deadQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _discovererQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<AlertState>()
                .WithAll<SocialAggroConfig, LocalTransform>()
                .Build(ref state);

            _deadQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<DeathState, LocalTransform>()
                .Build(ref state);

            state.RequireForUpdate(_discovererQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            // Collect dead entities
            var deadEntities = _deadQuery.ToEntityArray(Allocator.Temp);
            var deadStates = _deadQuery.ToComponentDataArray<DeathState>(Allocator.Temp);
            var deadTransforms = _deadQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // Filter to only actually-dead entities
            var corpsePositions = new NativeList<float3>(deadEntities.Length, Allocator.Temp);
            for (int d = 0; d < deadEntities.Length; d++)
            {
                if (deadStates[d].Phase != DeathPhase.Alive && deadStates[d].Phase != DeathPhase.Respawning)
                {
                    corpsePositions.Add(deadTransforms[d].Position);
                }
            }

            deadEntities.Dispose();
            deadStates.Dispose();
            deadTransforms.Dispose();

            if (corpsePositions.Length == 0)
            {
                corpsePositions.Dispose();
                return;
            }

            // Check discoverers
            var discEntities = _discovererQuery.ToEntityArray(Allocator.Temp);
            var discSocials = _discovererQuery.ToComponentDataArray<SocialAggroConfig>(Allocator.Temp);
            var discAlerts = _discovererQuery.ToComponentDataArray<AlertState>(Allocator.Temp);
            var discTransforms = _discovererQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            int count = discEntities.Length;
            for (int i = 0; i < count; i++)
            {
                if ((discSocials[i].Flags & SocialAggroFlags.BodyDiscovery) == 0) continue;

                // Only discover when not already highly alerted
                if (discAlerts[i].AlertLevel >= AlertState.SUSPICIOUS) continue;

                float range = discSocials[i].CallForHelpRadius;
                float rangeSq = range * range;
                float3 myPos = discTransforms[i].Position;

                for (int c = 0; c < corpsePositions.Length; c++)
                {
                    float distSq = math.distancesq(myPos, corpsePositions[c]);
                    if (distSq > rangeSq) continue;

                    // Discovered a body — raise alert to SUSPICIOUS
                    var a = discAlerts[i];
                    a.AlertLevel = AlertState.SUSPICIOUS;
                    a.AlertTimer = 8.0f;
                    a.AlertPosition = corpsePositions[c];
                    a.InvestigatePosition = corpsePositions[c];
                    a.SearchTimer = 0f;
                    a.HasInvestigated = false;
                    discAlerts[i] = a;
                    break; // One discovery per frame is enough
                }
            }

            // Bulk write-back (single memcpy vs N individual SetComponentData calls)
            _discovererQuery.CopyFromComponentDataArray(discAlerts);

            discEntities.Dispose();
            discSocials.Dispose();
            discAlerts.Dispose();
            discTransforms.Dispose();
            corpsePositions.Dispose();
        }
    }
}
