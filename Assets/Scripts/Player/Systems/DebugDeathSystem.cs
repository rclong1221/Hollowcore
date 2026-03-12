using Unity.Entities;
using Player.Components;
using UnityEngine;

namespace Player.Systems
{
    // Debug system to track death state issues
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DebugDeathSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (health, deathState, entity) in SystemAPI.Query<RefRO<Health>, RefRO<DeathState>>().WithEntityAccess())
            {
                if (health.ValueRO.Current <= 0 && deathState.ValueRO.Phase == DeathPhase.Alive)
                {
                    // Check if WillDieEvent is present/enabled
                    bool hasEvent = SystemAPI.HasComponent<WillDieEvent>(entity);
                    bool isEnabled = hasEvent && SystemAPI.IsComponentEnabled<WillDieEvent>(entity);
                    
                    // Only log if stuck (e.g. Health 0 for more than a few frames?)
                    // Just log every frame for now if user repro is consistent
                    // UnityEngine.Debug.Log($"[Stuck Dead] Entity {entity.Index}: HP={health.ValueRO.Current}, WillDie Present={hasEvent}, Enabled={isEnabled}, DeathPhase={deathState.ValueRO.Phase}");
                }
            }
        }
    }
}
