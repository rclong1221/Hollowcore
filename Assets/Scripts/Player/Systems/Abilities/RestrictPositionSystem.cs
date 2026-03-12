using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Player.Abilities;

namespace DIG.Player.Systems.Abilities
{
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateAfter(typeof(global::PlayerMovementSystem))] // Enforce restrictions after movement
    public partial struct RestrictPositionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (restriction, transform) in 
                     SystemAPI.Query<RefRO<RestrictPosition>, RefRW<LocalTransform>>())
            {
                float3 pos = transform.ValueRO.Position;
                float3 min = restriction.ValueRO.Min;
                float3 max = restriction.ValueRO.Max;
                bool3 axes = restriction.ValueRO.AxesEnabled;

                if (axes.x) pos.x = math.clamp(pos.x, min.x, max.x);
                if (axes.y) pos.y = math.clamp(pos.y, min.y, max.y);
                if (axes.z) pos.z = math.clamp(pos.z, min.z, max.z);

                transform.ValueRW.Position = pos;
            }
            
            // Rotation restrictions could be a separate job or same loop if we queried RestrictRotation
            foreach (var (rotRes, transform) in 
                      SystemAPI.Query<RefRO<RestrictRotation>, RefRW<LocalTransform>>())
            {
                 // Euler conversion is expensive in Burst/Math usually, but necessary for clamping angles
                 // Simplified: Just clamp if we assume upright character.
                 // For now, placeholder for rotation clamping logic which is complex with Quaternions.
            }
        }
    }
}
