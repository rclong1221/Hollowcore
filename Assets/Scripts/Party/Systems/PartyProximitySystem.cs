using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Updates PartyProximityState for each party.
    /// Uses frame spreading — each party updates every SpreadFrames ticks (default 5).
    /// Max 6 members * 5 distance checks = 30 distancesq per party per update.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PartyKillCreditSystem))]
    public partial class PartyProximitySystem : SystemBase
    {
        private const int SpreadFrames = 5;
        private uint _frameCount;

        protected override void OnCreate()
        {
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<PartyConfigSingleton>();
            var transformLookup = GetComponentLookup<LocalTransform>(true);

            float xpRangeSq = config.XPShareRange * config.XPShareRange;
            float lootRangeSq = config.LootRange * config.LootRange;
            float killRangeSq = config.KillCreditRange * config.KillCreditRange;

            uint frame = _frameCount++;

            foreach (var (members, partyEntity) in
                     SystemAPI.Query<DynamicBuffer<PartyMemberElement>>()
                         .WithAll<PartyTag, PartyProximityState>()
                         .WithEntityAccess())
            {
                var proximity = EntityManager.GetBuffer<PartyProximityState>(partyEntity);

                // Ensure proximity buffer matches member count (always, regardless of spread)
                while (proximity.Length < members.Length)
                {
                    proximity.Add(new PartyProximityState
                    {
                        PlayerEntity = members[proximity.Length].PlayerEntity,
                        InXPRange = true,
                        InLootRange = true,
                        InKillCreditRange = true
                    });
                }
                while (proximity.Length > members.Length)
                    proximity.RemoveAt(proximity.Length - 1);

                // Frame spreading: only recompute distances on this party's assigned frame
                uint slot = (uint)partyEntity.Index % SpreadFrames;
                if (slot != frame % SpreadFrames)
                    continue;

                // For each member, check distance to all other members
                for (int a = 0; a < members.Length; a++)
                {
                    var memberA = members[a].PlayerEntity;
                    if (!transformLookup.HasComponent(memberA))
                    {
                        proximity[a] = new PartyProximityState
                        {
                            PlayerEntity = memberA,
                            InXPRange = false,
                            InLootRange = false,
                            InKillCreditRange = false
                        };
                        continue;
                    }

                    float3 posA = transformLookup[memberA].Position;
                    bool inXP = false, inLoot = false, inKill = false;

                    // A member is "in range" if they're within range of ANY other party member
                    for (int b = 0; b < members.Length; b++)
                    {
                        if (a == b) continue;
                        var memberB = members[b].PlayerEntity;
                        if (!transformLookup.HasComponent(memberB)) continue;

                        float distSq = math.distancesq(posA, transformLookup[memberB].Position);
                        if (distSq <= xpRangeSq) inXP = true;
                        if (distSq <= lootRangeSq) inLoot = true;
                        if (distSq <= killRangeSq) inKill = true;

                        if (inXP && inLoot && inKill) break;
                    }

                    proximity[a] = new PartyProximityState
                    {
                        PlayerEntity = memberA,
                        InXPRange = inXP,
                        InLootRange = inLoot,
                        InKillCreditRange = inKill
                    };
                }
            }
        }
    }
}
