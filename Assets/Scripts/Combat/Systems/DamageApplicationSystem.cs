using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Targeting.Theming;
using DIG.Combat.UI;

using Health = Player.Components.Health;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Applies damage from combat resolution events to target health.
    /// Runs managed on main thread — processes only a handful of CREs per frame.
    ///
    /// CombatEventCleanupSystem destroys CREs after PresentationSystemGroup
    /// so CombatUIBridgeSystem can read them for hitmarkers/combo/kill feed.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatResolutionSystem))]
    public partial class DamageApplicationSystem : SystemBase
    {
        private EntityQuery _resultsQuery;
        private bool _isServer;
        private ComponentLookup<GhostOwner> _ghostOwnerLookup;
        private DamageVisibilityServerFilter _visFilter;

        protected override void OnCreate()
        {
            _resultsQuery = GetEntityQuery(
                ComponentType.ReadOnly<CombatResultEvent>()
            );
            RequireForUpdate(_resultsQuery);
            _isServer = World.Name == "ServerWorld";
            if (_isServer)
            {
                _visFilter = World.GetExistingSystemManaged<DamageVisibilityServerFilter>();
            }
            _ghostOwnerLookup = GetComponentLookup<GhostOwner>(true);
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _ghostOwnerLookup.Update(this);

            var results = _resultsQuery.ToComponentDataArray<CombatResultEvent>(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int idx = 0; idx < results.Length; idx++)
            {
                var combat = results[idx];

                // Defensive results (Blocked/Parried/Immune) and misses:
                // enqueue to DamageVisualQueue + broadcast RPC so all clients see the text.
                // No health damage applied for these — skip to next CRE after enqueue.
                bool isDefensive = combat.HitType == HitType.Blocked ||
                                   combat.HitType == HitType.Parried ||
                                   combat.HitType == HitType.Immune;

                if (!combat.DidHit || isDefensive)
                {
                    int srcNetId = GetAttackerNetworkId(combat.AttackerEntity);
                    var visualData = new DamageVisualData
                    {
                        Damage = combat.FinalDamage,
                        HitPosition = combat.HitPoint,
                        HitType = combat.HitType,
                        DamageType = combat.DamageType,
                        Flags = combat.Flags,
                        IsDOT = false,
                        SourceNetworkId = srcNetId
                    };
                    DamageVisualQueue.Enqueue(visualData);

                    if (_isServer)
                    {
                        _visFilter.CreateFilteredRpcs(ecb, new DamageVisualRpc
                        {
                            Damage = visualData.Damage,
                            HitPosition = visualData.HitPosition,
                            HitType = (byte)visualData.HitType,
                            DamageType = (byte)visualData.DamageType,
                            Flags = (byte)visualData.Flags,
                            IsDOT = 0,
                            SourceNetworkId = srcNetId
                        }, srcNetId, visualData.HitPosition);
                    }
                    continue;
                }

                if (combat.DamagePreApplied) continue;
                if (combat.TargetEntity == Entity.Null) continue;

                bool applied = false;
                bool died = false;
                float finalHealth = 0f;

                if (EntityManager.HasComponent<Health>(combat.TargetEntity))
                {
                    var health = EntityManager.GetComponentData<Health>(combat.TargetEntity);
                    health.Current = math.max(0f, health.Current - combat.FinalDamage);
                    died = health.Current <= 0f;
                    finalHealth = health.Current;
                    EntityManager.SetComponentData(combat.TargetEntity, health);
                    applied = true;

                    if (EntityManager.HasComponent<HealthComponent>(combat.TargetEntity))
                    {
                        var hc = EntityManager.GetComponentData<HealthComponent>(combat.TargetEntity);
                        hc.CurrentHealth = health.Current;
                        EntityManager.SetComponentData(combat.TargetEntity, hc);
                    }
                }
                else if (EntityManager.HasComponent<HealthComponent>(combat.TargetEntity))
                {
                    var health = EntityManager.GetComponentData<HealthComponent>(combat.TargetEntity);
                    health.CurrentHealth = math.max(0f, health.CurrentHealth - combat.FinalDamage);
                    died = health.CurrentHealth <= 0f;
                    finalHealth = health.CurrentHealth;
                    EntityManager.SetComponentData(combat.TargetEntity, health);
                    applied = true;
                }

                if (!applied) continue;

                if (died)
                {
                    var deathEntity = ecb.CreateEntity();
                    ecb.AddComponent(deathEntity, new DeathEvent
                    {
                        DyingEntity = combat.TargetEntity,
                        KillerEntity = combat.AttackerEntity,
                        DamageType = combat.DamageType,
                        FinalBlow = combat.FinalDamage
                    });
                }

                int hitSrcNetId = GetAttackerNetworkId(combat.AttackerEntity);
                var hitVisualData = new DamageVisualData
                {
                    Damage = combat.FinalDamage,
                    HitPosition = combat.HitPoint,
                    HitType = combat.HitType,
                    DamageType = combat.DamageType,
                    Flags = combat.Flags,
                    IsDOT = false,
                    SourceNetworkId = hitSrcNetId
                };
                DamageVisualQueue.Enqueue(hitVisualData);

                if (_isServer)
                {
                    _visFilter.CreateFilteredRpcs(ecb, new DamageVisualRpc
                    {
                        Damage = hitVisualData.Damage,
                        HitPosition = hitVisualData.HitPosition,
                        HitType = (byte)hitVisualData.HitType,
                        DamageType = (byte)hitVisualData.DamageType,
                        Flags = (byte)hitVisualData.Flags,
                        IsDOT = 0,
                        SourceNetworkId = hitSrcNetId
                    }, hitSrcNetId, hitVisualData.HitPosition);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            results.Dispose();
        }

        private int GetAttackerNetworkId(Entity attacker)
        {
            if (attacker != Entity.Null && _ghostOwnerLookup.HasComponent(attacker))
                return _ghostOwnerLookup[attacker].NetworkId;
            return -1;
        }
    }

    /// <summary>
    /// Basic health component for entities that can take damage.
    /// </summary>
    public struct HealthComponent : IComponentData
    {
        public float MaxHealth;
        public float CurrentHealth;

        public float HealthPercent => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;
    }

    /// <summary>
    /// Event component for entity death.
    /// </summary>
    public struct DeathEvent : IComponentData
    {
        public Entity DyingEntity;
        public Entity KillerEntity;
        public DamageType DamageType;
        public float FinalBlow;
    }
}
