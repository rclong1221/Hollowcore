using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DIG.Voxel.Fluids
{
    /// <summary>
    /// System for applying damage to entities in hazardous fluids.
    /// Handles drowning, burning, toxic damage.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FluidDamageSystem : ISystem
    {
        // Note: OnUpdate is NOT Burst-compiled because it accesses FluidService static fields.
        // The inner job IS Burst-compiled.
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            
            // Skip if FluidService not initialized (accessed from managed code, not Burst)
            if (!FluidService.IsInitialized)
                return;
            
            var fluidParams = FluidService.FluidParamsArray;
            
            // Schedule the Burst-compiled job
            new FluidDamageJob
            {
                DeltaTime = dt,
                FluidParams = fluidParams
            }.Schedule();
        }
        
        [BurstCompile]
        private partial struct FluidDamageJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public NativeArray<FluidService.FluidParams> FluidParams;
            
            public void Execute(ref InFluidZone inFluid, ref Health health)
            {
                if (inFluid.FluidType == 0)
                    return;
                
                // Update time in fluid
                inFluid.TimeInFluid += DeltaTime;
                
                // Get fluid params
                if (inFluid.FluidType >= FluidParams.Length)
                    return;
                
                var fluid = FluidParams[inFluid.FluidType];
                if (fluid.DamagePerSecond <= 0)
                    return;
                
                // Calculate damage
                float damage = FluidLookup.CalculateFluidDamage(
                    inFluid.FluidType,
                    inFluid.SubmersionDepth,
                    fluid.DamageStartDepth,
                    fluid.DamagePerSecond,
                    DeltaTime);
                
                if (damage > 0)
                {
                    health.Current -= damage;
                }
            }
        }
    }
    
    /// <summary>
    /// Simple health component for damage testing.
    /// Replace with your actual health component.
    /// </summary>
    public struct Health : IComponentData
    {
        public float Current;
        public float Max;
    }
    
    /// <summary>
    /// System for detecting which entities are in fluid zones.
    /// Updates InFluidZone component based on entity position.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(FluidDamageSystem))]
    public partial struct FluidZoneUpdateSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // This would query fluid data and update InFluidZone components
            // For now, InFluidZone is set externally or during generation
            
            // Reset TimeInFluid when exiting fluid
            foreach (var (inFluid, entity) in 
                SystemAPI.Query<RefRW<InFluidZone>>()
                         .WithEntityAccess())
            {
                // If fluid type became None, reset
                if (inFluid.ValueRO.FluidType == 0)
                {
                    inFluid.ValueRW.TimeInFluid = 0;
                }
            }
        }
    }
    
    /// <summary>
    /// System for handling fluid eruptions from pressurized pockets.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FluidEruptionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;
            
            foreach (var (eruption, entity) in 
                SystemAPI.Query<RefRW<FluidEruptionEvent>>()
                         .WithEntityAccess())
            {
                // Count down eruption
                eruption.ValueRW.TimeRemaining -= dt;
                
                // Eruption finished
                if (eruption.ValueRO.TimeRemaining <= 0)
                {
                    ecb.DestroyEntity(entity);
                }
                else
                {
                    // Apply ongoing eruption effects
                    // Could spawn VFX, apply damage in radius, etc.
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// System for lava cooling to solid rock.
    /// Placeholder for future implementation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LavaCoolingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // This would check lava cells adjacent to water
            // and cool them to obsidian over time
            // For now, this is a placeholder for future implementation
        }
    }
}
