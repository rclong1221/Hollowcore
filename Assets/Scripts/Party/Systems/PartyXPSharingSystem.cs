using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Player.Components;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Writes PartyXPModifier on party members with KillCredited.
    /// XPMultiplier = (1/memberCount) * (1 + XPShareBonusPerMember * (memberCount - 1))
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DIG.Progression.XPAwardSystem))]
    public partial class PartyXPSharingSystem : SystemBase
    {
        private EntityQuery _killQuery;

        protected override void OnCreate()
        {
            _killQuery = GetEntityQuery(
                ComponentType.ReadOnly<KillCredited>(),
                ComponentType.ReadOnly<PartyLink>(),
                ComponentType.Exclude<PartyXPModifier>());
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            if (_killQuery.IsEmpty) return;

            var config = SystemAPI.GetSingleton<PartyConfigSingleton>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var proximityBufferLookup = GetBufferLookup<PartyProximityState>(true);

            var entities = _killQuery.ToEntityArray(Allocator.Temp);
            var links = _killQuery.ToComponentDataArray<PartyLink>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var partyEntity = links[i].PartyEntity;
                if (partyEntity == Entity.Null || !proximityBufferLookup.HasBuffer(partyEntity))
                    continue;

                // Count members in XP range
                var proximity = proximityBufferLookup[partyEntity];
                int inRangeCount = 0;
                for (int p = 0; p < proximity.Length; p++)
                {
                    if (proximity[p].InXPRange)
                        inRangeCount++;
                }

                if (inRangeCount <= 1) continue; // Solo, full XP

                // Compute multiplier
                float shareBase = 1f / inRangeCount;
                float groupBonus = config.XPShareBonusPerMember * (inRangeCount - 1);
                float multiplier = shareBase * (1f + groupBonus);

                ecb.AddComponent(entities[i], new PartyXPModifier
                {
                    XPMultiplier = multiplier
                });
            }

            entities.Dispose();
            links.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
