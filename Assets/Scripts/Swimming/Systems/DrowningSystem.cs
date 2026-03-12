using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;
using DIG.Survival;
using DIG.Survival.Oxygen;
using DIG.Survival.Environment;
using Unity.Transforms;

namespace DIG.Swimming.Systems
{
    /// <summary>
    /// Server-side system that handles breath mechanics and drowning damage.
    /// When player is underwater without a suit, breath depletes and drowning damage occurs.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SwimmingMovementSystem))]
    public partial struct DrowningSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0) return;

            foreach (var (swimState, breathState, health, transform, entity) in 
                SystemAPI.Query<
                    RefRO<SwimmingState>, 
                    RefRW<BreathState>,
                    RefRW<Health>,
                    RefRO<LocalTransform>>()
                    .WithAll<CanSwim>()
                    .WithEntityAccess())
            {
                // Check if player has EVA suit (oxygen tank)
                bool hasSuit = SystemAPI.HasComponent<OxygenTank>(entity);
                
                if (swimState.ValueRO.IsSubmerged && !hasSuit)
                {
                    // Underwater without suit - holding breath
                    breathState.ValueRW.IsHoldingBreath = true;
                    
                    // Drain breath
                    breathState.ValueRW.CurrentBreath = math.max(0, breathState.ValueRO.CurrentBreath - dt);
                    
                    // Check for drowning
                    if (breathState.ValueRO.CurrentBreath <= 0)
                    {
                        // Apply drowning damage
                        breathState.ValueRW.DrowningDamageTimer += dt;
                        
                        if (breathState.ValueRO.DrowningDamageTimer >= breathState.ValueRO.DrowningDamageInterval)
                        {
                            breathState.ValueRW.DrowningDamageTimer = 0;
                            
                            // Apply damage
                            float damage = breathState.ValueRO.DrowningDamagePerTick;
                            health.ValueRW.Current = math.max(0, health.ValueRO.Current - damage);
                            
                            var pos = transform.ValueRO.Position;
                            var headY = pos.y + swimState.ValueRO.PlayerHeight;
                            var surfY = swimState.ValueRO.WaterSurfaceY;
                            
                            // Get pool info
                            string poolInfo = "No Zone Entity";
                            if (swimState.ValueRO.WaterZoneEntity != Entity.Null && SystemAPI.HasComponent<ZoneBounds>(swimState.ValueRO.WaterZoneEntity))
                            {
                                var bounds = SystemAPI.GetComponent<ZoneBounds>(swimState.ValueRO.WaterZoneEntity);
                                var relY = pos.y - bounds.Center.y;
                                poolInfo = $"BoundsCenter={bounds.Center}, Size={bounds.HalfExtents * 2}, RelY={relY:F2}";
                            }

                            UnityEngine.Debug.Log($"[Drowning Debug] Player {entity.Index} DAMAGED. " +
                                $"Health: {health.ValueRO.Current}. " +
                                $"Pos={pos}, Height={swimState.ValueRO.PlayerHeight}, HeadY={headY:F2}, SurfY={surfY:F2}. " +
                                $"IsSubmerged={swimState.ValueRO.IsSubmerged}. Pool: {poolInfo}");
                        }
                    }
                }
                else
                {
                    // Above water or has suit
                    breathState.ValueRW.IsHoldingBreath = false;
                    breathState.ValueRW.DrowningDamageTimer = 0;
                    
                    // Recover breath when above water
                    if (!swimState.ValueRO.IsSubmerged && breathState.ValueRO.CurrentBreath < breathState.ValueRO.MaxBreath)
                    {
                        breathState.ValueRW.CurrentBreath = math.min(
                            breathState.ValueRO.MaxBreath,
                            breathState.ValueRO.CurrentBreath + breathState.ValueRO.BreathRecoveryRate * dt
                        );
                    }
                }
            }
        }
    }
}
