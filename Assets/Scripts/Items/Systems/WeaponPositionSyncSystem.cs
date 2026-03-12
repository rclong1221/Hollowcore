using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Items.Systems
{
    /// <summary>
    /// Ensures weapon entity LocalTransform stays hidden underground.
    /// 
    /// Weapon ECS entities don't need visible transforms - all visuals are handled
    /// by the MonoBehaviour WeaponEquipVisualBridge. Keeping weapons hidden prevents:
    /// 1. Colliders blocking player movement
    /// 2. Visible mesh artifacts
    /// 3. Ghost replication position issues
    /// 
    /// EPIC 15.9: Fix for "assault rifle teleports player to 0,0,0" bug.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ItemSpawnSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct WeaponPositionSyncSystem : ISystem
    {
        private const float HIDDEN_Y = -5f; // -5f is safe from kill zone
        private const float HIDDEN_SCALE = 0.0001f;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Ensure all owned weapons stay hidden
            foreach (var (charItem, weaponTransform) in
                     SystemAPI.Query<RefRO<CharacterItem>, RefRW<LocalTransform>>()
                     .WithAll<Simulate>())
            {
                // Skip if no owner (world-dropped items may need different handling)
                if (charItem.ValueRO.OwnerEntity == Entity.Null) continue;
                
                // Keep weapon hidden underground with zero scale
                // Only update if not already hidden (avoid constant writes)
                if (weaponTransform.ValueRO.Position.y > -500f || weaponTransform.ValueRO.Scale > 0.001f)
                {
                    weaponTransform.ValueRW.Position = new float3(0, HIDDEN_Y, 0);
                    weaponTransform.ValueRW.Scale = HIDDEN_SCALE;
                }
            }
        }
    }
}
