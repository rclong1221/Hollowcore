using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Applies mitigation rules (Resistance, Cooldowns, I-Frames) to DamageEvents
    /// before they are physically applied to Health.
    /// server-authoritative.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [UpdateBefore(typeof(DamageApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct DamageMitigationSystem : ISystem
    {
        private ComponentLookup<DamageResistance> _resistanceLookup;
        private ComponentLookup<DamageCooldown> _cooldownLookup;
        private ComponentLookup<DamageInvulnerabilityWindow> _immunityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            
            _resistanceLookup = state.GetComponentLookup<DamageResistance>(true);
            _cooldownLookup = state.GetComponentLookup<DamageCooldown>(false); // ReadWrite
            _immunityLookup = state.GetComponentLookup<DamageInvulnerabilityWindow>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _resistanceLookup.Update(ref state);
            _cooldownLookup.Update(ref state);
            _immunityLookup.Update(ref state);

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            float currentTime = (float)SystemAPI.Time.ElapsedTime; // Use standard elapsed time

            DamagePolicy policy = default;
            if (SystemAPI.HasSingleton<DamagePolicy>())
            {
                policy = SystemAPI.GetSingleton<DamagePolicy>();
            }

            new MitigationJob
            {
                CurrentTime = currentTime,
                Policy = policy,
                ResistanceLookup = _resistanceLookup,
                CooldownLookup = _cooldownLookup,
                ImmunityLookup = _immunityLookup
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct MitigationJob : IJobEntity
        {
            public float CurrentTime;
            public DamagePolicy Policy;
            
            [ReadOnly] public ComponentLookup<DamageResistance> ResistanceLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<DamageCooldown> CooldownLookup;
            [ReadOnly] public ComponentLookup<DamageInvulnerabilityWindow> ImmunityLookup;

            void Execute(Entity entity, ref DynamicBuffer<DamageEvent> buffer)
            {
                if (buffer.IsEmpty) return;

                bool hasResistance = ResistanceLookup.HasComponent(entity);
                bool hasCooldown = CooldownLookup.HasComponent(entity);
                bool hasImmunity = ImmunityLookup.HasComponent(entity);

                DamageResistance resistance = hasResistance ? ResistanceLookup[entity] : DamageResistance.Default;
                DamageCooldown cooldown = hasCooldown ? CooldownLookup[entity] : default;
                DamageInvulnerabilityWindow immunity = hasImmunity ? ImmunityLookup[entity] : default;

                for (int i = 0; i < buffer.Length; i++)
                {
                    // Struct copy (buffer element access returns copy)
                    var evt = buffer[i];

                    // 1. Check Immunity (I-Frames)
                    if (hasImmunity && immunity.IsImmune(evt.Type, CurrentTime))
                    {
                        evt.Amount = 0f;
                        buffer[i] = evt;
                        continue;
                    }

                    // 2. Check Cooldowns
                    if (hasCooldown)
                    {
                        if (cooldown.IsCooldownActive(evt.Type, CurrentTime))
                        {
                            // Throttled
                            evt.Amount = 0f;
                            buffer[i] = evt;
                            continue;
                        }
                        else
                        {
                            // Allow, and set cooldown
                            // Note: If multiple events of same type processed in loop, first one passes,
                            // sets cooldown, subsequent ones fail IsCooldownActive check.
                            float duration = Policy.GetCooldownDuration(evt.Type);
                            if (duration > 0f)
                            {
                                cooldown.SetCooldown(evt.Type, CurrentTime, duration);
                            }
                        }
                    }

                    // 3. Apply Resistance
                    if (hasResistance)
                    {
                        float mult = resistance.GetMultiplier(evt.Type);
                        evt.Amount *= mult;
                    }

                    // Update buffer
                    buffer[i] = evt;
                }
                
                // Write back cooldowns
                if (hasCooldown)
                {
                    CooldownLookup[entity] = cooldown;
                }
            }
        }
    }
}
