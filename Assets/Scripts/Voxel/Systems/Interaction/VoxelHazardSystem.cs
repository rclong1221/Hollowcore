using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel.Systems.Interaction
{
    /// <summary>
    /// Types of environmental hazards that materials can trigger.
    /// </summary>
    public enum VoxelHazardType : byte
    {
        None = 0,
        Fire = 1,       // Burns nearby entities, spreads
        Toxic = 2,      // Damage over time in radius
        Radiation = 3,  // Radiation damage, reduces max health
        Crystal = 4,    // Light source, attracts enemies
        Explosive = 5,  // Chain reaction when destroyed
        Freezing = 6    // Slows movement, damage over time
    }
    
    /// <summary>
    /// Component for voxel hazard zones.
    /// Created when hazardous materials are exposed to air.
    /// </summary>
    public struct VoxelHazardZone : IComponentData
    {
        public float3 Position;
        public float Radius;
        public VoxelHazardType HazardType;
        public float Intensity; // 0-1, affects damage/effect strength
        public float Duration;  // Time remaining (-1 = permanent until covered)
        public byte SourceMaterial;
    }
    
    /// <summary>
    /// Event when a hazard zone affects an entity.
    /// </summary>
    public struct VoxelHazardDamageEvent : IBufferElementData
    {
        public Entity Target;
        public VoxelHazardType HazardType;
        public float Damage;
        public float3 Position;
    }
    
    /// <summary>
    /// Configuration for hazardous materials.
    /// Add this to VoxelMaterialDefinition in future, for now use static config.
    /// </summary>
    [System.Serializable]
    public class VoxelHazardConfig
    {
        public byte MaterialID;
        public VoxelHazardType HazardType;
        public float Radius = 3f;
        public float Intensity = 1f;
        public float DamagePerSecond = 5f;
        public float Duration = -1f; // -1 = permanent
    }
    
    /// <summary>
    /// System that detects exposed hazardous materials and creates hazard zones.
    /// Runs on server/local only for authoritative hazard management.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class VoxelHazardDetectionSystem : SystemBase
    {
        // Static hazard config (would be replaced with VoxelMaterialRegistry integration)
        private static readonly VoxelHazardConfig[] HazardConfigs = new[]
        {
            // Example configs - these would come from VoxelMaterialDefinition in production
            new VoxelHazardConfig { MaterialID = 100, HazardType = VoxelHazardType.Fire, Radius = 2f, DamagePerSecond = 10f },
            new VoxelHazardConfig { MaterialID = 101, HazardType = VoxelHazardType.Toxic, Radius = 4f, DamagePerSecond = 3f },
            new VoxelHazardConfig { MaterialID = 102, HazardType = VoxelHazardType.Crystal, Radius = 8f, Intensity = 1f, DamagePerSecond = 0f },
            new VoxelHazardConfig { MaterialID = 103, HazardType = VoxelHazardType.Explosive, Radius = 5f, DamagePerSecond = 50f },
        };
        
        private float _lastScanTime;
        private const float SCAN_INTERVAL = 1f; // Scan every second
        
        protected override void OnUpdate()
        {
            // Throttle scanning for performance
            if (UnityEngine.Time.time - _lastScanTime < SCAN_INTERVAL)
                return;
            _lastScanTime = UnityEngine.Time.time;
            
            // Listen for VoxelDestroyedEvents to check if hazardous materials were exposed
            var query = EntityManager.CreateEntityQuery(typeof(VoxelEventsSingleton));
            if (query.IsEmpty) return;
            
            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0) return;
            
            var eventEntity = entities[0];
            if (!EntityManager.HasBuffer<VoxelDestroyedEvent>(eventEntity)) return;
            
            // Note: For full implementation, we would track which hazardous voxels
            // are now exposed to air and create VoxelHazardZone entities for them.
            // This is a placeholder that demonstrates the architecture.
        }
        
        /// <summary>
        /// Check if a material is hazardous.
        /// </summary>
        public static bool IsHazardousMaterial(byte materialId, out VoxelHazardConfig config)
        {
            foreach (var cfg in HazardConfigs)
            {
                if (cfg.MaterialID == materialId)
                {
                    config = cfg;
                    return true;
                }
            }
            config = null;
            return false;
        }
    }
    
    /// <summary>
    /// System that applies damage to entities within hazard zones.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoxelHazardDetectionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class VoxelHazardDamageSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Process all hazard zones
            foreach (var (hazard, entity) in SystemAPI.Query<RefRW<VoxelHazardZone>>().WithEntityAccess())
            {
                // Update duration
                if (hazard.ValueRO.Duration > 0)
                {
                    hazard.ValueRW.Duration -= deltaTime;
                    if (hazard.ValueRW.Duration <= 0)
                    {
                        ecb.DestroyEntity(entity);
                        continue;
                    }
                }
                
                // Apply effects based on hazard type
                // Note: Actual player damage would integrate with existing health systems
                switch (hazard.ValueRO.HazardType)
                {
                    case VoxelHazardType.Fire:
                        // Would check for entities in radius and apply burn damage
                        break;
                    case VoxelHazardType.Toxic:
                        // Would check for entities in radius and apply poison
                        break;
                    case VoxelHazardType.Crystal:
                        // Would create light source (integrate with lighting system)
                        break;
                    case VoxelHazardType.Explosive:
                        // Chain reaction - create crater if triggered
                        break;
                }
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// Static helper for creating hazard zones.
    /// </summary>
    public static class VoxelHazards
    {
        public static void CreateHazardZone(EntityManager em, float3 position, VoxelHazardType type, 
            float radius = 3f, float intensity = 1f, float duration = -1f)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new VoxelHazardZone
            {
                Position = position,
                Radius = radius,
                HazardType = type,
                Intensity = intensity,
                Duration = duration,
                SourceMaterial = 0
            });
        }
        
        public static void TriggerChainExplosion(EntityManager em, float3 center, float radius, float strength = 1f)
        {
            VoxelExplosion.CreateCrater(em, center, radius, strength, spawnLoot: true);
        }
    }
}
