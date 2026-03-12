using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Melee voxel damage component.
    /// Add this to melee weapon entities that should damage voxels.
    /// </summary>
    [GhostComponent]
    public struct MeleeVoxelDamageConfig : IComponentData
    {
        /// <summary>Damage type for material resistance calculations.</summary>
        public VoxelDamageType DamageType;
        
        /// <summary>Damage dealt per hit to voxels.</summary>
        public float VoxelDamage;
        
        /// <summary>Shape type (usually Point for precise mining).</summary>
        public VoxelDamageShapeType ShapeType;
        
        /// <summary>Default pickaxe configuration.</summary>
        public static MeleeVoxelDamageConfig Pickaxe => new()
        {
            DamageType = VoxelDamageType.Mining,
            VoxelDamage = 25f,
            ShapeType = VoxelDamageShapeType.Point,
            Range = 2.5f
        };
        
        /// <summary>Hammer configuration - crush damage.</summary>
        public static MeleeVoxelDamageConfig Hammer => new()
        {
            DamageType = VoxelDamageType.Crush,
            VoxelDamage = 40f,
            ShapeType = VoxelDamageShapeType.Point,
            Range = 2.0f
        };

        /// <summary>Max range for voxel raycast.</summary>
        public float Range;
    }
    
    /// <summary>
    /// EPIC 15.10: State for tracking melee voxel hit detection.
    /// </summary>
    [GhostComponent]
    public struct MeleeVoxelHitState : IComponentData
    {
        public bool HasHitThisSwing;
        public int LastComboIndex;
        public float CooldownRemaining;
    }
    
    /// <summary>
    /// EPIC 15.10: Melee voxel damage system.
    /// Detects when melee weapons hit voxel terrain and creates VoxelDamageRequest entities.
    /// This system queries for entities with MeleeVoxelDamageConfig.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Weapons.Systems.MeleeActionSystem))] // Ensure MeleeState is up to date
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct MeleeVoxelDamageSystem : ISystem
    {
        // Layer mask for voxel terrain (adjust to match your project's physics layers)
        private const uint VOXEL_LAYER_MASK = 1u << 8; // Assuming layer 8 is voxel terrain
        private const float HIT_COOLDOWN = 0.5f; // Prevent multi-hits per swing unless continuous
        private const float PLAYER_EYE_HEIGHT = 1.6f;
        private const float RAY_OFFSET_FORWARD = 0.2f;

        // BC1040 Fix: Use SharedStatic for Burst-compatible mutable statics
        public static readonly SharedStatic<bool> EnableDebugLogsRef = SharedStatic<bool>.GetOrCreate<MeleeVoxelDamageSystem>();
        public static bool EnableDebugLogs { get => EnableDebugLogsRef.Data; set => EnableDebugLogsRef.Data = value; }
        
        // Keys for SharedStatic strings
        private class LogPrefixKey {}
        private class LogCommaKey {}
        private class LogSwingKey {}
        private class LogQueryKey {}
        private class LogOwnerKey {}
        private class LogAimInputKey {}
        private class LogAimCameraKey {}
        private class LogYawKey {}
        private class LogAimUseReqKey {}
        private class LogAimOwnerBodyKey {}
        private class LogMissKey {}

        public static readonly SharedStatic<FixedString64Bytes> LogHitPrefixRef = SharedStatic<FixedString64Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogPrefixKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogCommaRef = SharedStatic<FixedString32Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogCommaKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogSwingRef = SharedStatic<FixedString64Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogSwingKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogQueryRef = SharedStatic<FixedString64Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogQueryKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogOwnerMissingRef = SharedStatic<FixedString64Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogOwnerKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogAimInputRef = SharedStatic<FixedString64Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogAimInputKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogAimCameraRef = SharedStatic<FixedString64Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogAimCameraKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogYawRef = SharedStatic<FixedString32Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogYawKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogAimUseReqRef = SharedStatic<FixedString64Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogAimUseReqKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogAimOwnerBodyRef = SharedStatic<FixedString64Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogAimOwnerBodyKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogMissRef = SharedStatic<FixedString32Bytes>.GetOrCreate<MeleeVoxelDamageSystem, LogMissKey>();

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            
            // Force enable for debugging
            EnableDebugLogs = true;
            UnityEngine.Debug.Log($"[MeleeVoxelDamage] System Created in World: {state.WorldUnmanaged.Name}. Debug Logs FORCED ON.");
            
            // Initialize SharedStatic strings (managed only)
            LogHitPrefixRef.Data = new FixedString64Bytes("[MeleeVoxelDamage] HIT! Pos: ");
            LogCommaRef.Data = new FixedString32Bytes(", ");
            LogSwingRef.Data = new FixedString64Bytes("[MeleeVoxelDamage] Swing! EyePos: ");
            LogQueryRef.Data = new FixedString64Bytes("[MeleeVoxelDamage] Query Count: ");
            LogOwnerMissingRef.Data = new FixedString64Bytes("[MeleeVoxelDamage] Skipping - No Owner/Transform");
            LogAimInputRef.Data = new FixedString64Bytes("[MeleeVoxel] Aim: Input | P=");
            LogAimCameraRef.Data = new FixedString64Bytes("[MeleeVoxel] Aim: Camera | P=");
            LogYawRef.Data = new FixedString32Bytes(" Y=");
            LogAimUseReqRef.Data = new FixedString64Bytes("[MeleeVoxel] Aim: UseRequest");
            LogAimOwnerBodyRef.Data = new FixedString64Bytes("[MeleeVoxel] Aim: OwnerBody");
            LogMissRef.Data = new FixedString32Bytes("[MeleeVoxel] Miss. R: ");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var useRequestLookup = SystemAPI.GetComponentLookup<DIG.Weapons.UseRequest>(true);
            
            // Query for MeleeVoxelDamageConfig + MeleeVoxelHitState + CharacterItem
            // Only process currently attacking weapons
            // We also need the item to be "Worn" and have an Owner
            int count = 0;
            
            foreach (var (config, state2, charItem, entity) in 
                     SystemAPI.Query<RefRO<MeleeVoxelDamageConfig>, RefRW<MeleeVoxelHitState>, RefRO<DIG.Items.CharacterItem>>()
                     .WithAll<DIG.Weapons.MeleeState>() // Only iterate items with MeleeState (optimized)
                     .WithEntityAccess())
            {
                count++;
                
                // Get MeleeState to see if attacking
                var meleeState = SystemAPI.GetComponent<DIG.Weapons.MeleeState>(entity);
                
                // 1. Check if Attacking
                // If not attacking, reset hit state and skip
                if (!meleeState.IsAttacking)
                {
                    if (state2.ValueRO.HasHitThisSwing)
                        state2.ValueRW.HasHitThisSwing = false;
                        
                    // Reset cooldown if not attacking
                    if (state2.ValueRO.CooldownRemaining > 0)
                        state2.ValueRW.CooldownRemaining = 0f;
                        
                    continue;
                }
                
                // 2. Check Hit State / Cooldown
                if (state2.ValueRO.HasHitThisSwing)
                    continue;

                // Simple cooldown to prevent spam-hits in one swing (though HasHitThisSwing usually handles it)
                if (state2.ValueRO.CooldownRemaining > 0)
                {
                    state2.ValueRW.CooldownRemaining -= SystemAPI.Time.DeltaTime;
                    if (state2.ValueRO.CooldownRemaining > 0) continue;
                }

                // Get Owner Entity (Player)
                Entity owner = charItem.ValueRO.OwnerEntity;
                if (owner == Entity.Null || !localToWorldLookup.HasComponent(owner))
                {
                    if (EnableDebugLogsRef.Data)
                    {
                         FixedString128Bytes msg = default;
                         msg.Append(LogOwnerMissingRef.Data);
                         UnityEngine.Debug.Log(msg);
                    }
                    continue;
                }
                
                LocalToWorld ownerLTW = localToWorldLookup[owner];
                
                // 3. Determine Raycast UseReq (Origin & Direction)
                float3 rayOrigin = ownerLTW.Position + new float3(0, 1.6f, 0); // Default Eye Height
                float3 rayDir;

                // Priority 1: Player Input (Synced to Server & Predicted)
                if (SystemAPI.HasComponent<PlayerInput>(owner))
                {
                    var input = SystemAPI.GetComponent<PlayerInput>(owner);
                    quaternion lookRot = quaternion.Euler(math.radians(input.CameraPitch), math.radians(input.CameraYaw), 0);
                    rayDir = math.rotate(lookRot, new float3(0, 0, 1)); // Forward Z
                    
                    if (EnableDebugLogsRef.Data)
                    {
                        var msg = new FixedString128Bytes();
                        msg.Append(LogAimInputRef.Data);
                        msg.Append(input.CameraPitch);
                        msg.Append(LogYawRef.Data);
                        msg.Append(input.CameraYaw);
                        UnityEngine.Debug.Log(msg);
                    }
                }
                // Priority 2: Player Camera (Local Client Override)
                else if (SystemAPI.HasComponent<PlayerCameraSettings>(owner))
                {
                    var camSettings = SystemAPI.GetComponent<PlayerCameraSettings>(owner);
                    quaternion lookRot = quaternion.Euler(math.radians(camSettings.Pitch), math.radians(camSettings.Yaw), 0);
                    rayDir = math.rotate(lookRot, new float3(0, 0, 1));
                    
                    if (EnableDebugLogsRef.Data)
                    {
                        var msg = new FixedString128Bytes();
                        msg.Append(LogAimCameraRef.Data);
                        msg.Append(camSettings.Pitch);
                        msg.Append(LogYawRef.Data);
                        msg.Append(camSettings.Yaw);
                        UnityEngine.Debug.Log(msg);
                    }
                }
                // Priority 3: UseRequest (Fallback)
                else if (SystemAPI.HasComponent<DIG.Weapons.UseRequest>(owner))
                {
                    var useReq = SystemAPI.GetComponent<DIG.Weapons.UseRequest>(owner);
                    rayDir = useReq.AimDirection;
                    
                    if (EnableDebugLogsRef.Data)
                    {
                        var msg = new FixedString128Bytes();
                        msg.Append(LogAimUseReqRef.Data);
                        UnityEngine.Debug.Log(msg);
                    }
                }
                // Priority 3: Fallback to Owner Body Orientation
                else
                {
                    rayOrigin = ownerLTW.Position + new float3(0, 1.5f, 0);
                    rayDir = ownerLTW.Forward; // Only horizontal usually
                    if (EnableDebugLogsRef.Data)
                    {
                        var msg = new FixedString128Bytes();
                        msg.Append(LogAimOwnerBodyRef.Data);
                        UnityEngine.Debug.Log(msg);
                    }
                }

                // Raycast
                float range = config.ValueRO.Range;
                if (range <= 0) range = 2.5f; // Fallback if 0

                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = VOXEL_LAYER_MASK, // 1u << 8
                    GroupIndex = 0
                };

                var raycastInput = new RaycastInput
                {
                    Start = rayOrigin,
                    End = rayOrigin + (rayDir * range),
                    Filter = filter
                };

                // Explicit Debug Ray in Editor (Visual only, non-Burst)
                // UnityEngine.Debug.DrawRay(rayOrigin, rayDir * range, UnityEngine.Color.red, 1.0f);

                if (physicsWorld.CastRay(raycastInput, out RaycastHit hit))
                {
                    if (EnableDebugLogsRef.Data)
                    {
                        FixedString128Bytes msg = default;
                        msg.Append(LogHitPrefixRef.Data);
                        msg.Append(hit.Position.x);
                        msg.Append(LogCommaRef.Data);
                        msg.Append(hit.Position.y);
                        msg.Append(LogCommaRef.Data);
                        msg.Append(hit.Position.z);
                        UnityEngine.Debug.Log(msg);
                    }

                    // Hit voxel terrain! Create damage request
                    var requestEntity = ecb.CreateEntity();
                    ecb.AddComponent(requestEntity, VoxelDamageRequest.CreatePoint(
                        sourcePos: rayOrigin,
                        source: owner,
                        targetPos: hit.Position,
                        damage: config.ValueRO.VoxelDamage,
                        damageType: config.ValueRO.DamageType
                    ));
                    
                    // Mark as hit
                    state2.ValueRW.HasHitThisSwing = true;
                    state2.ValueRW.CooldownRemaining = HIT_COOLDOWN;
                }
                else if (EnableDebugLogsRef.Data)
                {
                     FixedString128Bytes msg = default;
                     msg.Append(LogMissRef.Data);
                     msg.Append(range);
                     UnityEngine.Debug.Log(msg);
                }
            }
            
            if (EnableDebugLogsRef.Data && count == 0)
            {
                FixedString128Bytes msg = default;
                msg.Append(LogQueryRef.Data);
                msg.Append(count);
                UnityEngine.Debug.Log(msg);
            }
        }
    }
}
