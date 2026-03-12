using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;
using DIG.Combat.Components;
using DIG.Combat.Systems;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19 + 15.33: Adds threat when AI takes damage via CombatResultEvent.
    /// Reads CombatResultEvent entities to find damage dealt to entities with ThreatEntry buffers.
    ///
    /// EPIC 15.33 fix: CRE.TargetEntity may point to CHILD entity (HitboxOwnerLink redirect).
    /// This system now walks DamageableLink → ROOT to find the ThreatEntry buffer.
    ///
    /// Runs after CombatResolutionSystem to consume fresh combat events.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatResolutionSystem))]
    [BurstCompile]
    public partial struct ThreatFromDamageSystem : ISystem
    {
        private ComponentLookup<DamageableLink> _damageableLinkLookup;
        private ComponentLookup<Unity.Transforms.Parent> _parentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatResultEvent>();
            _damageableLinkLookup = state.GetComponentLookup<DamageableLink>(true);
            _parentLookup = state.GetComponentLookup<Unity.Transforms.Parent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var threatBufferLookup = SystemAPI.GetBufferLookup<ThreatEntry>(false);
            var configLookup = SystemAPI.GetComponentLookup<AggroConfig>(true);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            _damageableLinkLookup.Update(ref state);
            _parentLookup.Update(ref state);

            foreach (var (combatResult, eventEntity) in
                SystemAPI.Query<RefRO<CombatResultEvent>>()
                .WithEntityAccess())
            {
                var result = combatResult.ValueRO;

                if (!result.DidHit || result.FinalDamage <= 0)
                    continue;

                Entity targetEntity = result.TargetEntity;
                Entity attackerEntity = result.AttackerEntity;
                float damage = result.FinalDamage;

                // Resolve CHILD → ROOT: CRE.TargetEntity may be a CHILD entity
                // that lacks ThreatEntry. Walk DamageableLink/Parent to find ROOT.
                Entity resolvedTarget = ResolveToThreatHolder(targetEntity, ref threatBufferLookup);

                if (resolvedTarget == Entity.Null)
                    continue;

                if (!configLookup.HasComponent(resolvedTarget))
                    continue;

                var config = configLookup[resolvedTarget];
                var threatBuffer = threatBufferLookup[resolvedTarget];

                float threatToAdd = damage * config.DamageThreatMultiplier;

                float3 attackerPos = float3.zero;
                if (transformLookup.HasComponent(attackerEntity))
                {
                    attackerPos = transformLookup[attackerEntity].Position;
                }

                int existingIndex = -1;
                for (int t = 0; t < threatBuffer.Length; t++)
                {
                    if (threatBuffer[t].SourceEntity == attackerEntity)
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
                else
                {
                    if (threatBuffer.Length < config.MaxTrackedTargets)
                    {
                        threatBuffer.Add(new ThreatEntry
                        {
                            SourceEntity = attackerEntity,
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

        /// <summary>
        /// Resolves an entity to the one holding a ThreatEntry buffer.
        /// Tries: entity directly, DamageableLink.DamageableRoot, Parent chain (up to 3 levels).
        /// </summary>
        private Entity ResolveToThreatHolder(Entity entity, ref BufferLookup<ThreatEntry> threatLookup)
        {
            // Direct check
            if (threatLookup.HasBuffer(entity))
                return entity;

            // DamageableLink → ROOT
            if (_damageableLinkLookup.HasComponent(entity))
            {
                Entity root = _damageableLinkLookup[entity].DamageableRoot;
                if (threatLookup.HasBuffer(root))
                    return root;
            }

            // Parent walk (up to 3 levels)
            Entity current = entity;
            for (int i = 0; i < 3; i++)
            {
                if (!_parentLookup.HasComponent(current))
                    break;
                current = _parentLookup[current].Value;
                if (threatLookup.HasBuffer(current))
                    return current;
            }

            return Entity.Null;
        }
    }
}
