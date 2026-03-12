using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Voxel.Core;
using DIG.Voxel.Components;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Singleton component holding voxel health data.
    /// </summary>
    public struct VoxelHealthTracker : IComponentData
    {
        public bool IsInitialized;
    }
    
    /// <summary>
    /// EPIC 15.10: Event emitted when a single voxel is destroyed.
    /// Used for granular effects (particles, chain reactions).
    /// </summary>
    public struct IndividualVoxelDestructionEvent : IComponentData
    {
        public int3 VoxelCoord;
        public Entity SourceEntity;
        public VoxelDamageType DamageType;
        public float FinalDamage;
    }
    
    /// <summary>
    /// Legacy event for loot spawning and aggregated effects.
    /// Buffer element on VoxelEventsSingleton.
    /// </summary>
    public struct VoxelDestroyedEvent : IBufferElementData
    {
        public float3 Position;
        public byte MaterialID;
        public int Amount;
    }
    
    /// <summary>
    /// Singleton to hold global voxel events.
    /// </summary>
    public struct VoxelEventsSingleton : IComponentData
    {
    }
    
    /// <summary>
    /// EPIC 15.10: Managed singleton for voxel health storage.
    /// Thread-safe for parallel job access via NativeQueue.
    /// Converted to struct for Burst compatibility (flat map).
    /// </summary>
    public struct VoxelHealthStorage : IComponentData
    {
        public NativeHashMap<int3, float> HealthMap;
        public NativeQueue<VoxelDestructionRecord> DestructionQueue;
        public bool IsDisposed;
        
        public const float DEFAULT_VOXEL_HEALTH = 100f;
        public const int CHUNK_SIZE = 16;
        
        public struct VoxelDestructionRecord
        {
            public int3 VoxelCoord;
            public VoxelDamageType DamageType;
            public float FinalDamage;
        }
        
        public void Initialize()
        {
            HealthMap = new NativeHashMap<int3, float>(1024, Allocator.Persistent);
            DestructionQueue = new NativeQueue<VoxelDestructionRecord>(Allocator.Persistent);
            IsDisposed = false;
        }
        
        public void Dispose()
        {
            if (IsDisposed) return;
            if (HealthMap.IsCreated) HealthMap.Dispose();
            if (DestructionQueue.IsCreated) DestructionQueue.Dispose();
            IsDisposed = true;
        }
        
        /// <summary>
        /// Apply damage to a voxel, accounting for damage type resistances.
        /// </summary>
        public void ApplyDamage(int3 voxelCoord, float damage, VoxelDamageType damageType, bool enableDebug = false, float materialResistance = 0f)
        {
            if (!HealthMap.TryGetValue(voxelCoord, out float currentHealth))
            {
                currentHealth = DEFAULT_VOXEL_HEALTH;
            }
            
            float oldHealth = currentHealth;
            
            // Apply damage type modifiers
            float damageModifier = GetDamageTypeModifier(damageType);
            float effectiveDamage = damage * damageModifier * (1f - math.saturate(materialResistance));
            currentHealth -= effectiveDamage;
            
            if (enableDebug)
            {
                FixedString128Bytes msg = default;
                msg.Append(VoxelHealthTrackingSystem.LogHitRef.Data);
                msg.Append(voxelCoord.x); msg.Append(VoxelHealthTrackingSystem.LogCommaRef.Data); msg.Append(voxelCoord.y); msg.Append(VoxelHealthTrackingSystem.LogCommaRef.Data); msg.Append(voxelCoord.z);
                msg.Append(VoxelHealthTrackingSystem.LogDmgRef.Data); msg.Append(effectiveDamage);
                msg.Append(VoxelHealthTrackingSystem.LogHpRef.Data); msg.Append(oldHealth);
                msg.Append(VoxelHealthTrackingSystem.LogArrowRef.Data); msg.Append(currentHealth);
                UnityEngine.Debug.Log(msg);
            }
            
            if (currentHealth <= 0f)
            {
                DestructionQueue.Enqueue(new VoxelDestructionRecord
                {
                    VoxelCoord = voxelCoord,
                    DamageType = damageType,
                    FinalDamage = effectiveDamage
                });
                HealthMap.Remove(voxelCoord);
                
                if (enableDebug)
                {
                     FixedString128Bytes msg = default;
                     msg.Append(VoxelHealthTrackingSystem.LogDestroyedRef.Data);
                     msg.Append(voxelCoord.x); msg.Append(VoxelHealthTrackingSystem.LogCommaRef.Data); msg.Append(voxelCoord.y); msg.Append(VoxelHealthTrackingSystem.LogCommaRef.Data); msg.Append(voxelCoord.z);
                     UnityEngine.Debug.Log(msg);
                }
            }
            else
            {
                HealthMap[voxelCoord] = currentHealth;
            }
        }
        
        /// <summary>
        /// Get damage modifier based on type.
        /// </summary>
        private float GetDamageTypeModifier(VoxelDamageType damageType)
        {
            switch (damageType)
            {
                case VoxelDamageType.Mining: return 1.0f;
                case VoxelDamageType.Explosive: return 1.5f;
                case VoxelDamageType.Laser: return 1.2f;
                case VoxelDamageType.Heat: return 0.8f;
                case VoxelDamageType.Electric: return 0.6f;
                case VoxelDamageType.Corrosive: return 1.1f;
                default: return 1.0f;
            }
        }
        
        public float GetHealth(int3 voxelCoord)
        {
            if (HealthMap.TryGetValue(voxelCoord, out float health))
            {
                return health;
            }
            return DEFAULT_VOXEL_HEALTH;
        }
    }

    /// <summary>
    /// EPIC 15.10: Per-voxel health tracking for damage accumulation.
    /// Processes destruction queue and emits VoxelDestroyedEvent entities.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoxelDamageProcessingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct VoxelHealthTrackingSystem : ISystem
    {
        // SharedStatic keys
        private class LogHitKey {}
        private class LogDmgKey {}
        private class LogHpKey {}
        private class LogArrowKey {}
        private class LogDestroyedKey {}
        private class LogCommaKey {}

        public static readonly SharedStatic<FixedString64Bytes> LogHitRef = SharedStatic<FixedString64Bytes>.GetOrCreate<VoxelHealthTrackingSystem, LogHitKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogDmgRef = SharedStatic<FixedString32Bytes>.GetOrCreate<VoxelHealthTrackingSystem, LogDmgKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogHpRef = SharedStatic<FixedString32Bytes>.GetOrCreate<VoxelHealthTrackingSystem, LogHpKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogArrowRef = SharedStatic<FixedString32Bytes>.GetOrCreate<VoxelHealthTrackingSystem, LogArrowKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogDestroyedRef = SharedStatic<FixedString64Bytes>.GetOrCreate<VoxelHealthTrackingSystem, LogDestroyedKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogCommaRef = SharedStatic<FixedString32Bytes>.GetOrCreate<VoxelHealthTrackingSystem, LogCommaKey>();
        public void OnCreate(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(entity, "VoxelHealthStorage");
            
            var storage = new VoxelHealthStorage();
            storage.Initialize();
            state.EntityManager.AddComponentData(entity, storage);
            state.EntityManager.AddComponentData(entity, new VoxelHealthTracker { IsInitialized = true });
            
            // Initialize SharedStatic strings
            LogHitRef.Data = new FixedString64Bytes("[VoxelHealth] Hit: ");
            LogDmgRef.Data = new FixedString32Bytes(" Dmg: ");
            LogHpRef.Data = new FixedString32Bytes(" HP: ");
            LogArrowRef.Data = new FixedString32Bytes(" -> ");
            LogDestroyedRef.Data = new FixedString64Bytes("[VoxelHealth] DESTROYED: ");
            LogCommaRef.Data = new FixedString32Bytes(",");

            // Ensure Singleton for legacy events exists
            var query = state.EntityManager.CreateEntityQuery(typeof(VoxelEventsSingleton));
            if (query.IsEmpty)
            {
                var eventEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(eventEntity, new VoxelEventsSingleton());
                state.EntityManager.AddBuffer<VoxelDestroyedEvent>(eventEntity);
                state.EntityManager.SetName(eventEntity, "VoxelEvents_Destruction");
            }
        }
        
        public void OnDestroy(ref SystemState state)
        {
            foreach (var storage in SystemAPI.Query<VoxelHealthStorage>())
            {
                storage.Dispose();
            }
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Get event buffer
            DynamicBuffer<VoxelDestroyedEvent> legacyEvents = default;
            if (SystemAPI.TryGetSingletonEntity<VoxelEventsSingleton>(out Entity eventEntity))
            {
                legacyEvents = SystemAPI.GetBuffer<VoxelDestroyedEvent>(eventEntity);
            }
            
            foreach (var storage in SystemAPI.Query<VoxelHealthStorage>())
            {
                // Process all destroyed voxels this frame
                while (storage.DestructionQueue.TryDequeue(out var record))
                {
                    // 1. Create Modification Request (Handled by VoxelModificationServerSystem)
                    // This ensures the change is applied to Data, Meshes, AND broadcast to Clients via RPC.
                    var modEntity = ecb.CreateEntity();
                    int3 chunkPos = CoordinateUtils.WorldToChunkPos((float3)record.VoxelCoord);
                    int3 localPos = CoordinateUtils.WorldToLocalVoxelPos((float3)record.VoxelCoord);
                    
                    ecb.AddComponent(modEntity, new VoxelModificationRequest
                    {
                        ChunkPos = chunkPos,
                        LocalVoxelPos = localPos,
                        TargetDensity = VoxelConstants.DENSITY_AIR,
                        TargetMaterial = VoxelConstants.MATERIAL_AIR
                    });

                    // 2. Emit precise event for other systems (particles, etc)
                    var eventEntityPrecise = ecb.CreateEntity();
                    ecb.AddComponent(eventEntityPrecise, new IndividualVoxelDestructionEvent
                    {
                        VoxelCoord = record.VoxelCoord,
                        SourceEntity = Entity.Null,
                        DamageType = record.DamageType,
                        FinalDamage = record.FinalDamage
                    });
                    
                    // Note: VoxelModificationServerSystem handles VoxelDestroyedEvent (Loot) emission automatically
                    // when density becomes AIR. We don't need to duplicate it here.
                    
                    // 3. Emit explosion event for chain reactions if explosive damage
                    if (record.DamageType == VoxelDamageType.Explosive)
                    {
                        var explosionEntity = ecb.CreateEntity();
                        ecb.AddComponent(explosionEntity, new VoxelExplosionEvent
                        {
                            Position = (float3)record.VoxelCoord + 0.5f,
                            BlastRadius = 2f,
                            Damage = record.FinalDamage
                        });
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// EPIC 15.10: Cleanup system for IndividualVoxelDestructionEvent entities.
    /// Runs at end of frame to remove processed events.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct VoxelDestroyedEventCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (_, entity) in SystemAPI.Query<RefRO<IndividualVoxelDestructionEvent>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
            
            foreach (var (_, entity) in SystemAPI.Query<RefRO<VoxelExplosionEvent>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
