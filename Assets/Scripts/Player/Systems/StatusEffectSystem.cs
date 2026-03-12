using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Core system for Status Effects (Epic 4.3).
    /// Handles:
    /// 1. Merging requests (Stacking rules)
    /// 2. Updating duration/timers
    /// 3. Applying consequences (Damage events)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DamageSystemGroup))] // Before damage processing
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct StatusEffectSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<StatusEffectConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var config = SystemAPI.GetSingleton<StatusEffectConfig>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();

            // Valid tick for damage events
            uint serverTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;

            new StatusEffectJob
            {
                DeltaTime = deltaTime,
                Config = config,
                ServerTick = serverTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct StatusEffectJob : IJobEntity
        {
            public float DeltaTime;
            public StatusEffectConfig Config;
            public uint ServerTick;

            void Execute(
                ref DynamicBuffer<StatusEffect> effects,
                ref DynamicBuffer<StatusEffectRequest> requests,
                ref DynamicBuffer<DamageEvent> damageEvents)
            {
                // 1. Process Requests (Merge/Add)
                if (!requests.IsEmpty)
                {
                    for (int i = 0; i < requests.Length; i++)
                    {
                        var req = requests[i];
                        ProcessRequest(ref effects, req);
                    }
                    requests.Clear();
                }

                // 2. Update Effects & Apply Damage
                for (int i = effects.Length - 1; i >= 0; i--)
                {
                    var effect = effects[i];

                    // Decrement duration
                    effect.TimeRemaining -= DeltaTime;

                    // Expire (Time out OR Zero/Negative Severity)
                    if (effect.TimeRemaining <= 0f || effect.Severity <= 0f)
                    {
                        effects.RemoveAt(i);
                        continue;
                    }

                    // Ticking Damage
                    effect.TickTimer += DeltaTime;
                    if (effect.TickTimer >= Config.TickInterval)
                    {
                        effect.TickTimer -= Config.TickInterval;
                        
                        float damagePerTick = Config.GetDamage(effect.Type);
                        if (damagePerTick > 0f)
                        {
                            // Scale by severity
                            float amount = damagePerTick * effect.Severity;
                            
                            // Map to DamageType
                            DamageType dmgType = MapToDamageType(effect.Type);
                            
                            damageEvents.Add(new DamageEvent
                            {
                                Amount = amount,
                                Type = dmgType,
                                SourceEntity = Entity.Null, // Internal
                                HitPosition = float3.zero,
                                ServerTick = ServerTick
                            });
                        }
                    }

                    // Write back structural change
                    effects[i] = effect;
                }
            }

            private void ProcessRequest(ref DynamicBuffer<StatusEffect> effects, StatusEffectRequest req)
            {
                // Search for existing effect
                int index = -1;
                for (int i = 0; i < effects.Length; i++)
                {
                    if (effects[i].Type == req.Type)
                    {
                        index = i;
                        break;
                    }
                }

                if (index != -1)
                {
                    // Merge
                    var existing = effects[index];
                    
                    // Refresh duration (Max of remaining vs new)
                    existing.TimeRemaining = math.max(existing.TimeRemaining, req.Duration);
                    
                    // Update Severity
                    if (req.Additive)
                    {
                        existing.Severity = math.min(1.0f, existing.Severity + req.Severity);
                    }
                    else
                    {
                        existing.Severity = math.max(existing.Severity, req.Severity);
                    }
                    
                    effects[index] = existing;
                }
                else
                {
                    // Add new
                    effects.Add(new StatusEffect
                    {
                        Type = req.Type,
                        Severity = math.min(1.0f, req.Severity),
                        TimeRemaining = req.Duration,
                        TickTimer = 0f
                    });
                }
            }
            
            private DamageType MapToDamageType(StatusEffectType statusType)
            {
                return statusType switch
                {
                    StatusEffectType.Hypoxia => DamageType.Suffocation,
                    StatusEffectType.RadiationPoisoning => DamageType.Radiation,
                    StatusEffectType.Burn => DamageType.Heat,
                    StatusEffectType.Frostbite => DamageType.Ice,         // EPIC 15.30: was Heat
                    StatusEffectType.Bleed => DamageType.Physical,
                    // EPIC 15.29/15.30: Combat modifier status effects
                    StatusEffectType.Shock => DamageType.Lightning,       // EPIC 15.30: was Heat
                    StatusEffectType.PoisonDOT => DamageType.Toxic,
                    StatusEffectType.Stun => DamageType.Physical,
                    StatusEffectType.Slow => DamageType.Physical,
                    StatusEffectType.Weaken => DamageType.Physical,
                    _ => DamageType.Physical
                };
            }
        }
    }
}
