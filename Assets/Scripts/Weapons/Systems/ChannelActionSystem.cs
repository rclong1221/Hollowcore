using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Items;
using DIG.Targeting;
using DIG.Combat.Utility;
using DIG.Combat.Components;
using DIG.Combat.Resources;
using Player.Components;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// EPIC 15.7: Handles channeled abilities (healing beam, drain life, etc.).
    /// Reads UseRequest (left-click held) to maintain channel state.
    /// Applies effects at TickInterval while channeling.
    /// Server-authoritative: raycast for target, apply heal/damage per tick.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerToItemInputSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ChannelActionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            bool isServer = state.WorldUnmanaged.IsServer();

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            bool isFirstPrediction = networkTime.IsFirstTimeFullyPredictingTick;
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            // Lookups for server-side effect application
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var ownerTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var hitboxLookup = SystemAPI.GetComponentLookup<Hitbox>(true);
            var hitboxOwnerLinkLookup = SystemAPI.GetComponentLookup<HitboxOwnerLink>(true);
            var damageableLinkLookup = SystemAPI.GetComponentLookup<DamageableLink>(true);
            var damageBufferLookup = SystemAPI.GetBufferLookup<DamageEvent>(false);
            var healBufferLookup = SystemAPI.GetBufferLookup<HealEvent>(false);
            var healthLookup = SystemAPI.GetComponentLookup<Health>(false);
            var resourcePoolLookup = SystemAPI.GetComponentLookup<ResourcePool>(false);
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (channelAction, channelState, request, charItem, entity) in
                     SystemAPI.Query<RefRO<ChannelAction>, RefRW<ChannelState>,
                                    RefRO<UseRequest>, RefRO<CharacterItem>>()
                     .WithEntityAccess())
            {
                // Client-side: Only process weapons owned by the local player
                if (!isServer)
                {
                    Entity owner = charItem.ValueRO.OwnerEntity;
                    if (owner == Entity.Null ||
                        !SystemAPI.HasComponent<GhostOwnerIsLocal>(owner) ||
                        !SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(owner))
                        continue;
                }

                ref var stateRef = ref channelState.ValueRW;
                var config = channelAction.ValueRO;
                var useRequest = request.ValueRO;
                Entity ownerEntity = charItem.ValueRO.OwnerEntity;

                // Track previous state
                bool wasChanneling = stateRef.IsChanneling;

                // Clear one-frame flags
                stateRef.JustStarted = false;
                stateRef.JustEnded = false;

                if (useRequest.StartUse)
                {
                    // Check max channel time (0 = unlimited)
                    bool canContinue = config.MaxChannelTime <= 0f || stateRef.ChannelTime < config.MaxChannelTime;

                    if (canContinue)
                    {
                        if (!stateRef.IsChanneling)
                        {
                            // Start channeling
                            stateRef.IsChanneling = true;
                            stateRef.JustStarted = true;
                            stateRef.ChannelTime = 0f;
                            stateRef.TimeSinceTick = 0f;
                            stateRef.TickCount = 0;
                            stateRef.CurrentTarget = Entity.Null;
                        }

                        // Update channel time
                        stateRef.ChannelTime += deltaTime;
                        stateRef.TimeSinceTick += deltaTime;

                        // Check if it's time to apply a tick
                        if (stateRef.TimeSinceTick >= config.TickInterval)
                        {
                            // EPIC 16.8: Resource drain per tick
                            if (config.ChannelResourceType != ResourceType.None &&
                                config.ResourcePerTick > 0f && ownerEntity != Entity.Null &&
                                resourcePoolLookup.HasComponent(ownerEntity))
                            {
                                var pool = resourcePoolLookup[ownerEntity];
                                if (!pool.TryDeduct(config.ChannelResourceType, config.ResourcePerTick, currentTime))
                                {
                                    // Resource depleted — force stop channel
                                    stateRef.IsChanneling = false;
                                    stateRef.JustEnded = true;
                                    stateRef.TimeSinceTick = 0f;
                                    stateRef.CurrentTarget = Entity.Null;
                                    resourcePoolLookup[ownerEntity] = pool;
                                    continue;
                                }
                                resourcePoolLookup[ownerEntity] = pool;
                            }

                            stateRef.TimeSinceTick -= config.TickInterval;
                            stateRef.TickCount++;

                            // Apply effect on server only (authoritative)
                            if (isServer)
                            {
                                stateRef.CurrentTarget = ApplyChannelTick(
                                    ref physicsWorld, ownerEntity, entity, config,
                                    useRequest, currentTick,
                                    ref ownerTransformLookup, ref hitboxLookup,
                                    ref hitboxOwnerLinkLookup, ref damageableLinkLookup,
                                    ref damageBufferLookup, ref healBufferLookup,
                                    ref healthLookup);
                            }
                        }
                    }
                    else
                    {
                        // Max time reached - force stop
                        stateRef.IsChanneling = false;
                        stateRef.JustEnded = true;
                        stateRef.TimeSinceTick = 0f;
                        stateRef.CurrentTarget = Entity.Null;
                    }
                }
                else if (wasChanneling)
                {
                    // Input released - stop channeling
                    stateRef.IsChanneling = false;
                    stateRef.JustEnded = true;
                    stateRef.TimeSinceTick = 0f;
                    stateRef.CurrentTarget = Entity.Null;
                }
            }
        }

        private static Entity ApplyChannelTick(
            ref PhysicsWorldSingleton physicsWorld,
            Entity ownerEntity, Entity weaponEntity,
            ChannelAction config, UseRequest useRequest, uint currentTick,
            ref ComponentLookup<LocalTransform> ownerTransformLookup,
            ref ComponentLookup<Hitbox> hitboxLookup,
            ref ComponentLookup<HitboxOwnerLink> hitboxOwnerLinkLookup,
            ref ComponentLookup<DamageableLink> damageableLinkLookup,
            ref BufferLookup<DamageEvent> damageBufferLookup,
            ref BufferLookup<HealEvent> healBufferLookup,
            ref ComponentLookup<Health> healthLookup)
        {
            if (ownerEntity == Entity.Null || !ownerTransformLookup.HasComponent(ownerEntity))
                return Entity.Null;

            // Get owner position and aim direction
            float3 ownerPos = ownerTransformLookup[ownerEntity].Position;
            float3 eyePos = ownerPos + math.up() * 1.5f;

            float3 aimDir = math.forward();
            if (math.lengthsq(useRequest.AimDirection) > 0.01f)
                aimDir = math.normalize(useRequest.AimDirection);

            // Raycast to find target
            var rayInput = new RaycastInput
            {
                Start = eyePos,
                End = eyePos + aimDir * config.Range,
                Filter = CollisionFilter.Default
            };

            Entity targetEntity = Entity.Null;
            float3 hitPoint = eyePos;

            if (physicsWorld.CastRay(rayInput, out var hit) && hit.Entity != Entity.Null && hit.Entity != ownerEntity)
            {
                targetEntity = hit.Entity;
                hitPoint = hit.Position;

                // Resolve hitbox → owner
                if (hitboxLookup.HasComponent(hit.Entity))
                {
                    targetEntity = hitboxLookup[hit.Entity].OwnerEntity;
                }

                // HitboxOwnerLink redirect (ROOT→CHILD)
                if (hitboxOwnerLinkLookup.HasComponent(targetEntity))
                    targetEntity = hitboxOwnerLinkLookup[targetEntity].HitboxOwner;

                // DamageableLink resolve (CHILD→ROOT)
                if (!damageBufferLookup.HasBuffer(targetEntity) && !healBufferLookup.HasBuffer(targetEntity))
                {
                    if (damageableLinkLookup.HasComponent(targetEntity))
                    {
                        Entity root = damageableLinkLookup[targetEntity].DamageableRoot;
                        if (root != Entity.Null) targetEntity = root;
                    }
                }
            }

            // Apply effect
            if (config.IsHealing)
            {
                // Healing: heal target if hit, otherwise heal self
                Entity healTarget = targetEntity != Entity.Null ? targetEntity : ownerEntity;
                if (healBufferLookup.HasBuffer(healTarget))
                {
                    healBufferLookup[healTarget].Add(new HealEvent
                    {
                        Amount = config.EffectPerTick,
                        SourceEntity = ownerEntity,
                        Position = hitPoint,
                        ServerTick = currentTick,
                        Type = HealType.Ability
                    });
                }
            }
            else
            {
                // Damage: only apply if we hit a valid target
                if (targetEntity != Entity.Null && damageBufferLookup.HasBuffer(targetEntity))
                {
                    damageBufferLookup[targetEntity].Add(new DamageEvent
                    {
                        Amount = config.EffectPerTick,
                        SourceEntity = ownerEntity,
                        HitPosition = hitPoint,
                        ServerTick = currentTick,
                        Type = global::Player.Components.DamageType.Physical
                    });
                }
                // Fallback: direct Health reduction for entities without DamageEvent buffer
                else if (targetEntity != Entity.Null && healthLookup.HasComponent(targetEntity))
                {
                    var hp = healthLookup[targetEntity];
                    hp.Current = math.max(0f, hp.Current - config.EffectPerTick);
                    healthLookup[targetEntity] = hp;
                }
            }

            return targetEntity;
        }
    }
}
