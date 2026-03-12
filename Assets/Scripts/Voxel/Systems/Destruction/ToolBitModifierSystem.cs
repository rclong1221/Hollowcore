using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Applies tool bit modifiers to destruction requests.
    /// Runs before validation to modify damage and shape parameters.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VoxelDamageValidationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct ToolBitModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (request, entity) in
                     SystemAPI.Query<RefRW<VoxelDamageRequest>>()
                     .WithAll<ToolBitModifier>()
                     .WithEntityAccess())
            {
                // Skip already processed
                if (request.ValueRO.IsProcessed)
                    continue;
                
                // Get the bit modifier
                var modifier = SystemAPI.GetComponent<ToolBitModifier>(entity);
                var bit = modifier.Bit;
                
                // Apply damage multiplier
                request.ValueRW.Damage *= bit.DamageMultiplier;
                
                // Override shape if bit specifies one (not Point = keep original)
                if (bit.ShapeType != VoxelDamageShapeType.Point || 
                    request.ValueRO.ShapeType == VoxelDamageShapeType.Point)
                {
                    if (bit.ShapeParam1 > 0 || bit.ShapeParam2 > 0)
                    {
                        request.ValueRW.ShapeType = bit.ShapeType;
                        request.ValueRW.Param1 = bit.ShapeParam1;
                        request.ValueRW.Param2 = bit.ShapeParam2;
                        request.ValueRW.Param3 = bit.ShapeParam3;
                    }
                }
                
                // Override damage type if specified
                if (bit.DamageType != VoxelDamageType.Mining)
                {
                    request.ValueRW.DamageType = bit.DamageType;
                }
                
                // Reduce bit durability
                bit.Durability -= 1f;
                
                // Update if tracking durability
                if (SystemAPI.HasComponent<EquippedToolBit>(modifier.ToolEntity))
                {
                    var equipped = SystemAPI.GetComponent<EquippedToolBit>(modifier.ToolEntity);
                    equipped.Bit.Durability = bit.Durability;
                    SystemAPI.SetComponent(modifier.ToolEntity, equipped);
                }
            }
        }
    }
    
    /// <summary>
    /// EPIC 15.10: Tag component to apply bit modifications to a VoxelDamageRequest.
    /// Added by tools when creating requests.
    /// </summary>
    public struct ToolBitModifier : IComponentData
    {
        public ToolBit Bit;
        public Entity ToolEntity;
    }
}
