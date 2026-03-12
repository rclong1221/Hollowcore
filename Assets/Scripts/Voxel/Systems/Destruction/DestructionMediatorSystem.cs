using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Central system that processes destruction intents from all sources.
    /// Collects DestructionIntent from buffers, applies tool bit modifiers, creates VoxelDamageRequest entities.
    /// This is the SINGLE POINT of request creation for the voxel destruction pipeline.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VoxelDamageValidationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct DestructionMediatorSystem : ISystem
    {
        private EntityQuery _intentSourceQuery;
        
        public void OnCreate(ref SystemState state)
        {
            _intentSourceQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<DestructionSourceTag>(),
                ComponentType.ReadWrite<DestructionIntentBuffer>()
            );
            
            state.RequireForUpdate(_intentSourceQuery);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Process all entities with destruction intent buffers
            foreach (var (intentBuffer, sourceTag, entity) in 
                SystemAPI.Query<DynamicBuffer<DestructionIntentBuffer>, RefRO<DestructionSourceTag>>()
                    .WithEntityAccess())
            {
                if (!sourceTag.ValueRO.IsActive)
                    continue;
                
                // Process each pending intent
                for (int i = 0; i < intentBuffer.Length; i++)
                {
                    var intent = intentBuffer[i].Intent;
                    
                    if (!intent.IsValid)
                        continue;
                    
                    // TODO: Apply tool bit modifiers here if source has ToolBitSocket component
                    // var modifiedIntent = ApplyBitModifiers(intent, entity, state);
                    
                    // Create VoxelDamageRequest entity
                    var requestEntity = ecb.CreateEntity();
                    ecb.AddComponent(requestEntity, ConvertToRequest(intent));
                }
                
                // Clear processed intents
                intentBuffer.Clear();
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        /// <summary>
        /// Convert a DestructionIntent to a VoxelDamageRequest.
        /// </summary>
        private VoxelDamageRequest ConvertToRequest(DestructionIntent intent)
        {
            return new VoxelDamageRequest
            {
                SourcePosition = intent.SourcePosition,
                SourceEntity = intent.SourceEntity,
                TargetPosition = intent.TargetPosition,
                TargetRotation = intent.TargetRotation,
                ShapeType = intent.ShapeType,
                DamageType = intent.DamageType,
                Falloff = intent.Falloff,
                Damage = intent.Damage,
                EdgeMultiplier = intent.EdgeMultiplier,
                Param1 = intent.Param1,
                Param2 = intent.Param2,
                Param3 = intent.Param3,
                ValidatedTick = 0,
                IsValidated = false,
                IsProcessed = false
            };
        }
    }
}
