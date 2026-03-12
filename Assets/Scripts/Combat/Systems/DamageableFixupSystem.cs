using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Player.Components;
using DIG.Combat.Components;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Burst-compiled runtime fixup system that patches split-entity bake results:
    /// 1. Adds missing Health/DamageEvent/DeathState/StatusEffect to entities with HasHitboxes.
    ///    Uses DamageableLink to read MaxHealth from the root DamageableAuthoring entity.
    /// 2. Adds missing StatusEffect/StatusEffectRequest buffers to pre-existing damageable entities.
    /// Self-disables after 30 consecutive idle frames.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct DamageableFixupSystem : ISystem
    {
        private int _idleFrames;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HasHitboxes>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool didWork = false;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Pass 1: Add missing components to entities with HasHitboxes but no Health.
            // Handles the split-entity bake pattern: DamageableAuthoring bakes Health onto
            // the ROOT entity, HitboxOwnerMarker bakes HasHitboxes onto the CHILD entity.
            // DamageableLink (baked by HitboxOwnerMarker) bridges the gap.
            foreach (var (_, entity) in SystemAPI.Query<RefRO<HasHitboxes>>()
                         .WithNone<Health>()
                         .WithEntityAccess())
            {
                float maxHealth = 0f;

                if (SystemAPI.HasComponent<DamageableLink>(entity))
                {
                    var root = SystemAPI.GetComponent<DamageableLink>(entity).DamageableRoot;
                    if (root != Entity.Null && SystemAPI.HasComponent<Health>(root))
                        maxHealth = SystemAPI.GetComponent<Health>(root).Max;
                }

                if (maxHealth <= 0f && SystemAPI.HasComponent<DamageableTag>(entity))
                    maxHealth = SystemAPI.GetComponent<DamageableTag>(entity).MaxHealth;

                if (maxHealth <= 0f) maxHealth = 100f;

                ecb.AddComponent(entity, new Health { Current = maxHealth, Max = maxHealth });
                ecb.AddBuffer<DamageEvent>(entity);

                ecb.AddComponent(entity, new DeathState
                {
                    Phase = DeathPhase.Alive,
                    RespawnDelay = 0f,
                    InvulnerabilityDuration = 1f,
                    StateStartTime = 0,
                    InvulnerabilityEndTime = 0
                });

                ecb.AddComponent(entity, new DamageResistance
                {
                    PhysicalMult = 1f,
                    HeatMult = 1f,
                    RadiationMult = 1f,
                    SuffocationMult = 1f,
                    ExplosionMult = 1f,
                    ToxicMult = 1f
                });

                ecb.AddComponent(entity, default(WillDieEvent));
                ecb.SetComponentEnabled<WillDieEvent>(entity, false);
                ecb.AddComponent(entity, default(DiedEvent));
                ecb.SetComponentEnabled<DiedEvent>(entity, false);

                ecb.AddComponent(entity, new DamageableTag { MaxHealth = maxHealth });
                ecb.AddComponent(entity, new ShowHealthBarTag());
                ecb.AddComponent(entity, new ShowDamageNumbersTag());

                // StatusEffect buffers added here directly (avoids one-frame delay vs separate pass)
                ecb.AddBuffer<StatusEffect>(entity);
                ecb.AddBuffer<StatusEffectRequest>(entity);

                didWork = true;
            }

            // Pass 2: Add missing StatusEffect/StatusEffectRequest buffers to pre-existing entities
            // that were baked before StatusEffect support was added to DamageableAuthoring.
            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<DamageEvent>>()
                         .WithNone<StatusEffectRequest>()
                         .WithEntityAccess())
            {
                ecb.AddBuffer<StatusEffect>(entity);
                ecb.AddBuffer<StatusEffectRequest>(entity);
                didWork = true;
            }

            // Pass 3: Add missing HitboxOwnerLink on ROOT entities.
            // Runtime fixup for pre-baked subscenes that don't have HitboxOwnerLink yet.
            // Once subscene is reimported, the baker handles this and this pass is a no-op.
            foreach (var (link, entity) in SystemAPI.Query<RefRO<DamageableLink>>()
                         .WithAll<HasHitboxes>()
                         .WithEntityAccess())
            {
                var root = link.ValueRO.DamageableRoot;
                if (root != Entity.Null && !SystemAPI.HasComponent<HitboxOwnerLink>(root))
                {
                    ecb.AddComponent(root, new HitboxOwnerLink { HitboxOwner = entity });
                    didWork = true;
                }
            }

            // NOTE: Old Pass 3 (phantom ghost disabling) was removed.
            // It incorrectly disabled ROOT entities (DamageableAuthoring) that also lacked HasHitboxes,
            // removing their compound PhysicsCollider (Head/Torso hitboxes) from the physics world
            // and breaking all physics-based damage (hitscan, projectiles, explosions).
            // Phantom ghosts are harmless: they have empty DamageEvent buffers and don't match
            // health bar queries (which require HasHitboxes).

            if (didWork)
            {
                ecb.Playback(state.EntityManager);
                _idleFrames = 0;
            }
            else
            {
                _idleFrames++;
                if (_idleFrames >= 30)
                    state.Enabled = false;
            }

            ecb.Dispose();
        }
    }
}
