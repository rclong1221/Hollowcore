using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;
using DIG.Combat.Components;
using DIG.Combat.Systems;
using Player.Components;
using Player.Systems;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Generates threat directly from DamageEvent buffers on NPC entities,
    /// BEFORE SimpleDamageApplySystem clears them. This is the primary damage→threat
    /// path for hitscan/projectile/explosion damage that bypasses CombatResultEvent.
    ///
    /// Handles CHILD→ROOT resolution: DamageEvent buffer lives on CHILD entities
    /// (HitboxOwnerLink redirect), but ThreatEntry buffer lives on ROOT (AggroAuthoring).
    /// Uses DamageableLink and Parent walk to resolve.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [UpdateBefore(typeof(SimpleDamageApplySystem))]
    [BurstCompile]
    public partial struct ThreatFromDamageEventSystem : ISystem
    {
        private ComponentLookup<DamageableLink> _damageableLinkLookup;
        private ComponentLookup<Parent> _parentLookup;
        private BufferLookup<ThreatEntry> _threatBufferLookup;
        private ComponentLookup<AggroConfig> _configLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggroConfig>();
            _damageableLinkLookup = state.GetComponentLookup<DamageableLink>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _threatBufferLookup = state.GetBufferLookup<ThreatEntry>(false);
            _configLookup = state.GetComponentLookup<AggroConfig>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            _damageableLinkLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _threatBufferLookup.Update(ref state);
            _configLookup.Update(ref state);
            _transformLookup.Update(ref state);

            foreach (var (damageBuffer, entity) in
                SystemAPI.Query<DynamicBuffer<DamageEvent>>()
                .WithNone<ShieldComponent>()
                .WithNone<PlayerBlockingState>()
                .WithEntityAccess())
            {
                if (damageBuffer.Length == 0)
                    continue;

                // Resolve this entity (possibly CHILD) → ROOT with ThreatEntry
                Entity threatHolder = ResolveToThreatHolder(entity);
                if (threatHolder == Entity.Null)
                    continue;

                if (!_configLookup.HasComponent(threatHolder))
                    continue;

                var config = _configLookup[threatHolder];
                var threatBuffer = _threatBufferLookup[threatHolder];

                int eventsToProcess = math.min(damageBuffer.Length, 16);

                for (int i = 0; i < eventsToProcess; i++)
                {
                    var dmg = damageBuffer[i];
                    if (dmg.SourceEntity == Entity.Null)
                        continue;
                    if (dmg.Amount <= 0f || math.isnan(dmg.Amount) || math.isinf(dmg.Amount))
                        continue;

                    float threatToAdd = dmg.Amount * config.DamageThreatMultiplier;

                    float3 attackerPos = float3.zero;
                    if (_transformLookup.HasComponent(dmg.SourceEntity))
                    {
                        attackerPos = _transformLookup[dmg.SourceEntity].Position;
                    }

                    // Find or create threat entry
                    int existingIndex = -1;
                    for (int t = 0; t < threatBuffer.Length; t++)
                    {
                        if (threatBuffer[t].SourceEntity == dmg.SourceEntity)
                        {
                            existingIndex = t;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                    {
                        var entry = threatBuffer[existingIndex];
                        entry.ThreatValue += threatToAdd;
                        entry.DamageThreat += threatToAdd;
                        entry.LastKnownPosition = attackerPos;
                        entry.TimeSinceVisible = 0f;
                        entry.IsCurrentlyVisible = true;
                        entry.SourceFlags |= ThreatSourceFlags.Damage;
                        threatBuffer[existingIndex] = entry;
                    }
                    else if (threatBuffer.Length < config.MaxTrackedTargets)
                    {
                        threatBuffer.Add(new ThreatEntry
                        {
                            SourceEntity = dmg.SourceEntity,
                            ThreatValue = threatToAdd,
                            DamageThreat = threatToAdd,
                            LastKnownPosition = attackerPos,
                            TimeSinceVisible = 0f,
                            IsCurrentlyVisible = true,
                            SourceFlags = ThreatSourceFlags.Damage
                        });
                    }
                }
            }
        }

        private Entity ResolveToThreatHolder(Entity entity)
        {
            if (_threatBufferLookup.HasBuffer(entity))
                return entity;

            if (_damageableLinkLookup.HasComponent(entity))
            {
                Entity root = _damageableLinkLookup[entity].DamageableRoot;
                if (_threatBufferLookup.HasBuffer(root))
                    return root;
            }

            Entity current = entity;
            for (int i = 0; i < 3; i++)
            {
                if (!_parentLookup.HasComponent(current))
                    break;
                current = _parentLookup[current].Value;
                if (_threatBufferLookup.HasBuffer(current))
                    return current;
            }

            return Entity.Null;
        }
    }
}
