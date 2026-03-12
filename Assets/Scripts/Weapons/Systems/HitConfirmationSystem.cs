using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;
using DIG.Weapons.Feedback;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// EPIC 15.5: Client-side predicted hit confirmation system.
    /// Performs local raycasts to predict hits and trigger immediate feedback.
    /// Reduces perceived lag for hitmarkers and hit sounds.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ShootableActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct HitConfirmationSystem : ISystem
    {
        private ComponentLookup<Hitbox> _hitboxLookup;
        private ComponentLookup<HasHitboxes> _hasHitboxesLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _hitboxLookup = state.GetComponentLookup<Hitbox>(true);
            _hasHitboxesLookup = state.GetComponentLookup<HasHitboxes>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only run on client
            if (!state.WorldUnmanaged.IsClient()) return;

            _hitboxLookup.Update(ref state);
            _hasHitboxesLookup.Update(ref state);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            // Process hitscan weapons using SystemAPI.Query
            foreach (var (fireState, fireConfig, transform, request, entity) in
                     SystemAPI.Query<RefRO<WeaponFireState>, RefRO<WeaponFireComponent>, 
                                    RefRO<LocalTransform>, RefRO<UseRequest>>()
                     .WithEntityAccess())
            {
                // Only process if just fired
                if (!fireState.ValueRO.IsFiring || fireState.ValueRO.TimeSinceLastShot > 0.02f)
                {
                    continue;
                }

                // Only for hitscan weapons
                if (!fireConfig.ValueRO.UseHitscan)
                {
                    continue;
                }

                // Perform predictive raycast
                float3 origin = transform.ValueRO.Position + new float3(0, 1.5f, 0); // Eye height
                float3 direction = math.normalize(request.ValueRO.AimDirection);

                var rayInput = new RaycastInput
                {
                    Start = origin,
                    End = origin + direction * fireConfig.ValueRO.Range,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                if (physicsWorld.CastRay(rayInput, out var hit))
                {
                    ProcessPredictedHit(hit, fireConfig.ValueRO.Damage, currentTick, 
                                       _hitboxLookup, _hasHitboxesLookup);
                }
            }

            // Process melee hit events using SystemAPI.Query
            foreach (var (hitEvents, entity) in
                     SystemAPI.Query<DynamicBuffer<SweptMeleeHitEvent>>()
                     .WithEntityAccess())
            {
                foreach (var hitEvent in hitEvents)
                {
                    // Enrich hit data with hitbox information if available
                    HitboxRegion region = hitEvent.IsCritical ? HitboxRegion.Head : HitboxRegion.Torso;
                    float damageMultiplier = 1.0f;
                    
                    if (_hitboxLookup.HasComponent(hitEvent.HitEntity))
                    {
                        var hitbox = _hitboxLookup[hitEvent.HitEntity];
                        region = hitbox.Region;
                        damageMultiplier = hitbox.DamageMultiplier;
                    }

                    var confirmation = new HitConfirmation
                    {
                        TargetEntity = hitEvent.HitEntity,
                        HitPosition = hitEvent.HitPosition,
                        HitNormal = hitEvent.HitNormal,
                        Damage = hitEvent.Damage * damageMultiplier,
                        IsCritical = hitEvent.IsCritical || region == HitboxRegion.Head,
                        IsKill = false, // Determined server-side
                        HitRegion = region,
                        ServerTick = hitEvent.ServerTick
                    };

                    TriggerHitFeedback(confirmation);
                }

                // Clear events after processing
                hitEvents.Clear();
            }
        }

        private void ProcessPredictedHit(
            Unity.Physics.RaycastHit hit, 
            float baseDamage, 
            uint serverTick,
            ComponentLookup<Hitbox> hitboxLookup,
            ComponentLookup<HasHitboxes> hasHitboxesLookup)
        {
            Entity hitEntity = hit.Entity;
            float damageMultiplier = 1.0f;
            bool isCritical = false;
            HitboxRegion region = HitboxRegion.Torso;

            // Check if we hit a hitbox
            if (hitboxLookup.HasComponent(hitEntity))
            {
                var hitbox = hitboxLookup[hitEntity];
                hitEntity = hitbox.OwnerEntity;
                damageMultiplier = hitbox.DamageMultiplier;
                region = hitbox.Region;
                isCritical = region == HitboxRegion.Head;
            }
            else if (hasHitboxesLookup.HasComponent(hitEntity))
            {
                // Hit root entity with hitboxes - use base damage
                region = HitboxRegion.Torso;
            }
            else
            {
                // Hit environment or non-damageable - no hitmarker
                return;
            }

            float finalDamage = baseDamage * damageMultiplier;

            var confirmation = new HitConfirmation
            {
                TargetEntity = hitEntity,
                HitPosition = hit.Position,
                HitNormal = hit.SurfaceNormal,
                Damage = finalDamage,
                IsCritical = isCritical,
                IsKill = false, // Kill confirmation comes from server
                HitRegion = region,
                ServerTick = serverTick
            };

            TriggerHitFeedback(confirmation);
        }

        private void TriggerHitFeedback(HitConfirmation confirmation)
        {
            // Send to managed feedback bridge
            if (HitmarkerFeedbackBridge.Instance != null)
            {
                HitmarkerFeedbackBridge.Instance.OnHitConfirmed(confirmation);
            }
        }
    }
}
