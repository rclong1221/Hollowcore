using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using DIG.Items;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Manages weapon spread dynamics (increase on fire, recovery over time).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct WeaponSpreadSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (spread, fireState) in 
                     SystemAPI.Query<RefRW<WeaponSpreadState>, RefRO<WeaponFireState>>()
                     .WithAll<Simulate>())
            {
                ref var stateRef = ref spread.ValueRW;
                
                // Get config if available (assuming it is)
                // We need the config component on the same entity
                
                // Note: We need to use SystemAPI.GetComponent inside the loop or add to query. 
                // Adding to query is safer for Burst.
            }
            
            // Movement penalty lookup: owner's PhysicsVelocity
            var velocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(true);

            // Re-query with Config + CharacterItem for owner lookup
            foreach (var (spread, spreadConfig, fireState, charItem) in
                     SystemAPI.Query<RefRW<WeaponSpreadState>, RefRO<WeaponSpreadComponent>, RefRO<WeaponFireState>, RefRO<CharacterItem>>()
                     .WithAll<Simulate>())
            {
                ref var stateRef = ref spread.ValueRW;
                var config = spreadConfig.ValueRO;
                var fire = fireState.ValueRO;

                // Increase spread if just fired
                if (fire.IsFiring && fire.TimeSinceLastShot < deltaTime * 1.5f)
                {
                    stateRef.CurrentSpread += config.SpreadIncrement;
                    stateRef.CurrentSpread = math.min(stateRef.CurrentSpread, config.MaxSpread);
                }

                // Recover spread when not firing
                if (!fire.IsFiring || fire.TimeSinceLastShot > 0.1f)
                {
                    stateRef.CurrentSpread -= config.SpreadRecovery * deltaTime;
                    stateRef.CurrentSpread = math.max(stateRef.CurrentSpread, config.BaseSpread);
                }

                // Movement penalty: add spread proportional to owner's movement speed
                Entity owner = charItem.ValueRO.OwnerEntity;
                if (config.MovementMultiplier > 0f && owner != Entity.Null && velocityLookup.HasComponent(owner))
                {
                    float speed = math.length(velocityLookup[owner].Linear);
                    if (speed > 0.5f) // only penalize meaningful movement
                    {
                        float movePenalty = speed * config.MovementMultiplier * deltaTime;
                        stateRef.CurrentSpread += movePenalty;
                        stateRef.CurrentSpread = math.min(stateRef.CurrentSpread, config.MaxSpread);
                    }
                }
            }
        }
    }
}
