using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Items;
using DIG.Diagnostics;
using DIG.Combat.Systems;
using DIG.Combat.Resolvers;
using Player.Components;
using DIG.Combat.Utility;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// EPIC 15.5: Swept Melee Hitbox System.
    /// Performs CapsuleCasts from previous to current frame positions to prevent tunneling.
    /// Fast-moving weapons no longer pass through enemies between frames.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MeleeActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct SweptMeleeHitboxSystem : ISystem
    {
        // Debug logging: define SWEPT_MELEE_DEBUG scripting symbol to enable

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            bool isServer = state.WorldUnmanaged.IsServer();
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            // Lookups for damage application
            var hitboxLookup = SystemAPI.GetComponentLookup<Hitbox>(true);
            var hasHitboxesLookup = SystemAPI.GetComponentLookup<HasHitboxes>(true);
            var damageBufferLookup = SystemAPI.GetBufferLookup<DamageEvent>(false);
            var comboBufferLookup = SystemAPI.GetBufferLookup<ComboData>(true);
            var ownerTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
            var damageProfileLookup = SystemAPI.GetComponentLookup<DamageProfile>(true);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (definition, sweptState, meleeState, meleeAction, transform, charItem, entity) in
                     SystemAPI.Query<RefRO<MeleeHitboxDefinition>, RefRW<SweptMeleeState>,
                                    RefRO<MeleeState>, RefRO<MeleeAction>, RefRO<LocalTransform>, RefRO<CharacterItem>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var swept = ref sweptState.ValueRW;
                var config = definition.ValueRO;
                var melee = meleeAction.ValueRO;
                var mState = meleeState.ValueRO;
                Entity owner = charItem.ValueRO.OwnerEntity;

                // EPIC 15.29: Read base damage element from weapon's DamageProfile
                var themeElement = damageProfileLookup.HasComponent(entity)
                    ? damageProfileLookup[entity].Element
                    : DIG.Targeting.Theming.DamageType.Physical;
                var survivalElement = DamageTypeConverter.ToSurvival(themeElement);

                // Per-combo damage multiplier from ComboData buffer
                float comboMultiplier = 1.0f;
                if (comboBufferLookup.HasBuffer(entity))
                {
                    var comboBuffer = comboBufferLookup[entity];
                    int comboIndex = mState.CurrentCombo;
                    if (comboIndex >= 0 && comboIndex < comboBuffer.Length)
                        comboMultiplier = comboBuffer[comboIndex].DamageMultiplier;
                }

                // Calculate current world positions
                // Weapon entity transform may be static (not synced from visual MonoBehaviour).
                // Use owner's (player character) transform as the sweep origin — it tracks properly.
                float3 worldPos = transform.ValueRO.Position;
                quaternion worldRot = transform.ValueRO.Rotation;
                if (owner != Entity.Null && ownerTransformLookup.HasComponent(owner))
                {
                    var ownerTx = ownerTransformLookup[owner];
                    worldPos = ownerTx.Position;
                    worldRot = ownerTx.Rotation;
                }
                float3 currentTip = worldPos + math.mul(worldRot, config.TipOffset);
                float3 currentHandle = worldPos + math.mul(worldRot, config.HandleOffset);

                // Initialize on first frame
                if (!swept.IsInitialized)
                {
                    swept.PreviousTipPosition = currentTip;
                    swept.PreviousHandlePosition = currentHandle;
                    swept.IsInitialized = true;
                    continue;
                }

                // Reset hit tracking when attack starts
                if (mState.IsAttacking && mState.AttackTime < deltaTime * 2f)
                {
                    swept.ResetHits();
                }

                // Only perform swept detection when hitbox is active
                if (mState.HitboxActive && config.UseSweptDetection && swept.CanHitMore)
                {
                    // Perform swept CapsuleCast from previous to current positions
                    PerformSweptDetection(
                        ref state,
                        ref swept,
                        ref ecb,
                        in physicsWorld,
                        in hitboxLookup,
                        in hasHitboxesLookup,
                        in damageBufferLookup,
                        in parentLookup,
                        currentTip,
                        currentHandle,
                        config.CapsuleRadius,
                        config.CollisionMask,
                        melee.Damage * comboMultiplier,
                        owner,
                        entity,
                        currentTick,
                        isServer,
                        worldRot,
                        survivalElement,
                        themeElement
                    );
                }

                // Store positions for next frame
                swept.PreviousTipPosition = currentTip;
                swept.PreviousHandlePosition = currentHandle;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void PerformSweptDetection(
            ref SystemState state,
            ref SweptMeleeState swept,
            ref EntityCommandBuffer ecb,
            in PhysicsWorldSingleton physicsWorld,
            in ComponentLookup<Hitbox> hitboxLookup,
            in ComponentLookup<HasHitboxes> hasHitboxesLookup,
            in BufferLookup<DamageEvent> damageBufferLookup,
            in ComponentLookup<Parent> parentLookup,
            float3 currentTip,
            float3 currentHandle,
            float capsuleRadius,
            uint collisionMask,
            float baseDamage,
            Entity owner,
            Entity weaponEntity,
            uint serverTick,
            bool isServer,
            quaternion ownerRotation,
            global::Player.Components.DamageType survivalElement,
            DIG.Targeting.Theming.DamageType themeElement)
        {
            // Calculate swept volume: We create a capsule that encompasses the blade's
            // motion from previous frame to current frame.

            // For a proper swept test, we cast from the midpoint of the previous blade
            // to the midpoint of the current blade
            float3 prevMid = (swept.PreviousTipPosition + swept.PreviousHandlePosition) * 0.5f;
            float3 currMid = (currentTip + currentHandle) * 0.5f;

            // Direction and distance of sweep
            float3 sweepDir = currMid - prevMid;
            float sweepDist = math.length(sweepDir);

            // Blade length for capsule
            float bladeLength = math.distance(currentTip, currentHandle);

            // Create collision filter
            var filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = collisionMask,
                GroupIndex = 0
            };

            float effectiveRadius = math.max(capsuleRadius, bladeLength * 0.5f);

            // Static overlap fallback when player isn't moving (common for melee)
            if (sweepDist < 0.001f)
            {
                var overlapAabb = new OverlapAabbInput
                {
                    Aabb = new Aabb
                    {
                        Min = currMid - new float3(effectiveRadius),
                        Max = currMid + new float3(effectiveRadius)
                    },
                    Filter = filter
                };

                var bodyIndices = new NativeList<int>(Allocator.Temp);
                if (physicsWorld.OverlapAabb(overlapAabb, ref bodyIndices))
                {
                    var bodies = physicsWorld.CollisionWorld.Bodies;
                    for (int i = 0; i < bodyIndices.Length && swept.CanHitMore; i++)
                    {
                        int bodyIndex = bodyIndices[i];
                        if (bodyIndex < 0 || bodyIndex >= bodies.Length) continue;
                        Entity hitEntity = bodies[bodyIndex].Entity;

                        if (hitEntity == weaponEntity || hitEntity == owner) continue;
                        if (swept.WasEntityHit(hitEntity)) continue;

                        Entity targetEntity = hitEntity;
                        float damageMultiplier = 1.0f;
                        bool isCritical = false;

                        if (hitboxLookup.HasComponent(hitEntity))
                        {
                            var hitbox = hitboxLookup[hitEntity];
                            targetEntity = hitbox.OwnerEntity;
                            damageMultiplier = hitbox.DamageMultiplier;
                            isCritical = hitbox.Region == HitboxRegion.Head;
                        }
                        else if (hasHitboxesLookup.HasComponent(hitEntity))
                        {
                            targetEntity = hitEntity;
                        }
                        else if (damageBufferLookup.HasBuffer(hitEntity))
                        {
                            targetEntity = hitEntity;
                        }
                        else
                        {
                            // Physics body may be on a child entity — traverse parents to find damageable root
                            bool found = false;
                            Entity cur = hitEntity;
                            for (int d = 0; d < 5 && parentLookup.HasComponent(cur); d++)
                            {
                                cur = parentLookup[cur].Value;
                                if (damageBufferLookup.HasBuffer(cur) || hasHitboxesLookup.HasComponent(cur))
                                {
                                    targetEntity = cur;
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) continue;
                        }

                        if (swept.WasEntityHit(targetEntity)) continue;
                        if (!swept.RegisterHit(targetEntity)) continue;

                        float finalDamage = baseDamage * damageMultiplier;

                        bool damageApplied = false;
                        if (isServer && finalDamage > 0 && damageBufferLookup.HasBuffer(targetEntity))
                        {
                            ecb.AppendToBuffer(targetEntity, new DamageEvent
                            {
                                Amount = finalDamage,
                                SourceEntity = owner,
                                HitPosition = currMid,
                                ServerTick = serverTick,
                                Type = survivalElement
                            });
                            damageApplied = true;

                            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                            CombatDiagnostics.LogDamageEventCreated("MeleeOverlap", targetEntity, finalDamage, serverTick, "ServerWorld");
                            #endif
                        }

                        if (isServer)
                        {
                            var hitRegion = hitboxLookup.HasComponent(hitEntity) ? hitboxLookup[hitEntity].Region : HitboxRegion.Torso;
                            var combatHitEntity = ecb.CreateEntity();
                            ecb.AddComponent(combatHitEntity, new PendingCombatHit
                            {
                                AttackerEntity = owner,
                                TargetEntity = targetEntity,
                                WeaponEntity = weaponEntity,
                                HitPoint = currMid,
                                HitNormal = new float3(0, 1, 0),
                                HitDistance = 0f,
                                WasPhysicsHit = true,
                                ResolverType = CombatResolverType.Hybrid,
                                HitRegion = hitRegion,
                                HitboxMultiplier = damageMultiplier,
                                DamagePreApplied = damageApplied,
                                AttackDirection = math.forward(ownerRotation),
                                WeaponData = new WeaponStats
                                {
                                    BaseDamage = baseDamage,
                                    DamageMin = baseDamage,
                                    DamageMax = baseDamage,
                                    DamageType = themeElement,
                                    CanCrit = true
                                }
                            });
                        }

                        if (SystemAPI.HasBuffer<SweptMeleeHitEvent>(weaponEntity))
                        {
                            ecb.AppendToBuffer(weaponEntity, new SweptMeleeHitEvent
                            {
                                HitEntity = targetEntity,
                                HitPosition = currMid,
                                HitNormal = new float3(0, 1, 0),
                                Damage = finalDamage,
                                IsCritical = isCritical,
                                ServerTick = serverTick
                            });
                        }
                    }
                }
                bodyIndices.Dispose();
                return;
            }

            sweepDir = math.normalize(sweepDir);
            float3 bladeDir = math.normalize(currentTip - currentHandle);

            var collectorHits = new NativeList<ColliderCastHit>(Allocator.Temp);

            var sphereGeometry = new SphereGeometry { Center = float3.zero, Radius = effectiveRadius };
            var sphereCollider = SphereCollider.Create(sphereGeometry, filter);

            unsafe
            {
                var colliderCastInput = new ColliderCastInput
                {
                    Collider = (Collider*)sphereCollider.GetUnsafePtr(),
                    Start = prevMid,
                    End = currMid,
                    Orientation = quaternion.identity
                };

                if (physicsWorld.CastCollider(colliderCastInput, ref collectorHits))
                {
                    // Process hits
                    for (int i = 0; i < collectorHits.Length && swept.CanHitMore; i++)
                    {
                        var hit = collectorHits[i];
                        Entity hitEntity = hit.Entity;

                        // Skip self and weapon
                        if (hitEntity == weaponEntity || hitEntity == owner) continue;

                        // Skip already hit entities
                        if (swept.WasEntityHit(hitEntity)) continue;

                        // Determine target entity and damage multiplier
                        Entity targetEntity = hitEntity;
                        float damageMultiplier = 1.0f;
                        bool isCritical = false;

                        // Check for hitbox component
                        if (hitboxLookup.HasComponent(hitEntity))
                        {
                            var hitbox = hitboxLookup[hitEntity];
                            targetEntity = hitbox.OwnerEntity;
                            damageMultiplier = hitbox.DamageMultiplier;
                            isCritical = hitbox.Region == HitboxRegion.Head;
                        }
                        else if (hasHitboxesLookup.HasComponent(hitEntity))
                        {
                            // Hit the root collider, use base multiplier
                            targetEntity = hitEntity;
                        }
                        else if (damageBufferLookup.HasBuffer(hitEntity))
                        {
                            targetEntity = hitEntity;
                        }
                        else
                        {
                            // Physics body may be on a child entity — traverse parents to find damageable root
                            bool found = false;
                            Entity cur = hitEntity;
                            for (int d = 0; d < 5 && parentLookup.HasComponent(cur); d++)
                            {
                                cur = parentLookup[cur].Value;
                                if (damageBufferLookup.HasBuffer(cur) || hasHitboxesLookup.HasComponent(cur))
                                {
                                    targetEntity = cur;
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) continue;
                        }

                        // Skip if already hit the same target entity
                        if (swept.WasEntityHit(targetEntity)) continue;

                        // Register the hit
                        if (!swept.RegisterHit(targetEntity)) continue;

                        // Calculate hit position (interpolated)
                        float3 hitPos = math.lerp(prevMid, currMid, hit.Fraction);

                        // Calculate final damage
                        float finalDamage = baseDamage * damageMultiplier;

                        // Apply damage on server
                        bool damageApplied = false;
                        if (isServer && finalDamage > 0 && damageBufferLookup.HasBuffer(targetEntity))
                        {
                            ecb.AppendToBuffer(targetEntity, new DamageEvent
                            {
                                Amount = finalDamage,
                                SourceEntity = owner,
                                HitPosition = hitPos,
                                ServerTick = serverTick,
                                Type = survivalElement
                            });
                            damageApplied = true;

                            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                            CombatDiagnostics.LogDamageEventCreated(
                                "SweptMelee",
                                targetEntity,
                                finalDamage,
                                serverTick,
                                "ServerWorld");
                            #endif
                        }

                        // EPIC 15.28: Create PendingCombatHit for combat resolution (server only)
                        if (isServer)
                        {
                            var hitRegion = hitboxLookup.HasComponent(hitEntity)
                                ? hitboxLookup[hitEntity].Region
                                : HitboxRegion.Torso;

                            var combatHitEntity = ecb.CreateEntity();
                            ecb.AddComponent(combatHitEntity, new PendingCombatHit
                            {
                                AttackerEntity = owner,
                                TargetEntity = targetEntity,
                                WeaponEntity = weaponEntity,
                                HitPoint = hitPos,
                                HitNormal = hit.SurfaceNormal,
                                HitDistance = sweepDist,
                                WasPhysicsHit = true,
                                ResolverType = CombatResolverType.Hybrid,
                                HitRegion = hitRegion,
                                HitboxMultiplier = damageMultiplier,
                                DamagePreApplied = damageApplied,
                                AttackDirection = sweepDir,
                                WeaponData = new WeaponStats
                                {
                                    BaseDamage = baseDamage,
                                    DamageMin = baseDamage,
                                    DamageMax = baseDamage,
                                    DamageType = themeElement,
                                    CanCrit = true
                                }
                            });
                        }

                        // Add hit event for feedback systems
                        if (SystemAPI.HasBuffer<SweptMeleeHitEvent>(weaponEntity))
                        {
                            ecb.AppendToBuffer(weaponEntity, new SweptMeleeHitEvent
                            {
                                HitEntity = targetEntity,
                                HitPosition = hitPos,
                                HitNormal = hit.SurfaceNormal,
                                Damage = finalDamage,
                                IsCritical = isCritical,
                                ServerTick = serverTick
                            });
                        }

#if SWEPT_MELEE_DEBUG
                        UnityEngine.Debug.Log($"[SWEPT_MELEE] Hit {targetEntity.Index} at {hitPos} " +
                            $"Damage={finalDamage} Critical={isCritical} " +
                            $"SweepDist={sweepDist:F3}");
#endif
                    }
                }
            }

            sphereCollider.Dispose();
            collectorHits.Dispose();
        }
    }
}
