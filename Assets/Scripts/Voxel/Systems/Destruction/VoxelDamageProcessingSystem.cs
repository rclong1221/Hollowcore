using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Jobs;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: Server-authoritative voxel damage processing.
    /// Uses parallel Burst jobs for shape queries, then applies damage to health storage.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoxelDamageValidationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct VoxelDamageProcessingSystem : ISystem
    {
        private const float VOXEL_SIZE = 1f;
        
        // Keys for SharedStatic strings
        private class LogProcPrefixKey {}
        private class LogReqSuffixKey {}
        private class LogReqPrefixKey {}
        private class LogDmgKey {}
        private class LogPrefixKey {}
        private class LogProcessKey {}
        
        public static readonly SharedStatic<FixedString64Bytes> LogProcPrefixRef = SharedStatic<FixedString64Bytes>.GetOrCreate<VoxelDamageProcessingSystem, LogProcPrefixKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogReqSuffixRef = SharedStatic<FixedString32Bytes>.GetOrCreate<VoxelDamageProcessingSystem, LogReqSuffixKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogReqPrefixRef = SharedStatic<FixedString32Bytes>.GetOrCreate<VoxelDamageProcessingSystem, LogReqPrefixKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogDmgRef = SharedStatic<FixedString32Bytes>.GetOrCreate<VoxelDamageProcessingSystem, LogDmgKey>();
        public static readonly SharedStatic<FixedString32Bytes> LogPrefixRef = SharedStatic<FixedString32Bytes>.GetOrCreate<VoxelDamageProcessingSystem, LogPrefixKey>();
        public static readonly SharedStatic<FixedString64Bytes> LogProcessRef = SharedStatic<FixedString64Bytes>.GetOrCreate<VoxelDamageProcessingSystem, LogProcessKey>();

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VoxelHealthTracker>();
            
            // Initialize SharedStatic strings
            EnableDebugLogs = true;
            LogPrefixRef.Data = new FixedString32Bytes("[Voxel Processing] Requests: ");
            LogProcessRef.Data = new FixedString64Bytes("[Voxel Processing] Validated Req from Entity: ");
            LogReqPrefixRef.Data = new FixedString32Bytes("- Req: ");
            LogDmgRef.Data = new FixedString32Bytes(" Dmg: ");
        }

        // BC1040 Fix: Use SharedStatic for Burst-compatible mutable statics
        public static readonly SharedStatic<bool> EnableDebugLogsRef = SharedStatic<bool>.GetOrCreate<VoxelDamageProcessingSystem>();
        public static bool EnableDebugLogs { get => EnableDebugLogsRef.Data; set => EnableDebugLogsRef.Data = value; }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get health storage singleton
            if (!SystemAPI.TryGetSingleton<VoxelHealthStorage>(out var storage))
                return;
            
            // Collect validated requests
            using var requests = new NativeList<VoxelDamageRequest>(64, Allocator.TempJob);
            using var entities = new NativeList<Entity>(64, Allocator.TempJob);
            
            foreach (var (request, entity) in SystemAPI.Query<RefRW<VoxelDamageRequest>>().WithEntityAccess())
            {
                if (request.ValueRO.IsValidated && !request.ValueRO.IsProcessed)
                {
                    requests.Add(request.ValueRO);
                    entities.Add(entity);
                }
            }
            
            if (requests.Length == 0)
                return;
            
            if (EnableDebugLogs)
            {
                FixedString128Bytes msg = default;
                msg.Append(LogProcPrefixRef.Data);
                msg.Append(requests.Length);
                msg.Append(LogReqSuffixRef.Data);
                UnityEngine.Debug.Log(msg);
            }
            
            // Process each request with parallel jobs
            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (EnableDebugLogs)
                {
                    FixedString128Bytes msg = default;
                    msg.Append(LogReqPrefixRef.Data);
                    msg.Append((int)request.ShapeType);
                    msg.Append(LogDmgRef.Data);
                    msg.Append(request.Damage);
                    UnityEngine.Debug.Log(msg);
                }

                ProcessRequest(ref request, storage, ref state);
                
                // Mark as processed
                SystemAPI.GetComponentRW<VoxelDamageRequest>(entities[i]).ValueRW.IsProcessed = true;
            }
        }
        
        private void ProcessRequest(ref VoxelDamageRequest request, VoxelHealthStorage storage, ref SystemState state)
        {
            switch (request.ShapeType)
            {
                case VoxelDamageShapeType.Point:
                    ProcessPointDamage(ref request, storage);
                    break;
                    
                case VoxelDamageShapeType.Sphere:
                    ProcessSphereDamage(ref request, storage, ref state);
                    break;
                    
                case VoxelDamageShapeType.Cylinder:
                    ProcessCylinderDamage(ref request, storage, ref state);
                    break;
                    
                case VoxelDamageShapeType.Cone:
                    ProcessConeDamage(ref request, storage, ref state);
                    break;
                    
                case VoxelDamageShapeType.Capsule:
                    ProcessCapsuleDamage(ref request, storage, ref state);
                    break;
                    
                case VoxelDamageShapeType.Box:
                    ProcessBoxDamage(ref request, storage, ref state);
                    break;
            }
        }
        
        private void ProcessPointDamage(ref VoxelDamageRequest request, VoxelHealthStorage storage)
        {
            var result = VoxelShapeQueryJobs.QueryPoint(request.TargetPosition, VOXEL_SIZE);
            storage.ApplyDamage(result.VoxelCoord, request.Damage, request.DamageType, EnableDebugLogs);
        }
        
        private void ProcessSphereDamage(ref VoxelDamageRequest request, VoxelHealthStorage storage, ref SystemState state)
        {
            var results = new NativeQueue<VoxelShapeQueryJobs.VoxelQueryResult>(Allocator.TempJob);
            
            try
            {
                var jobHandle = VoxelShapeQueryJobs.ScheduleSphereQuery(
                    request.TargetPosition, request.Param1, request.Falloff,
                    request.EdgeMultiplier, VOXEL_SIZE, ref results);
                
                jobHandle.Complete();
                ApplyQueuedDamage(storage, results, request.Damage, request.DamageType);
            }
            finally
            {
                results.Dispose();
            }
        }
        
        private void ProcessCylinderDamage(ref VoxelDamageRequest request, VoxelHealthStorage storage, ref SystemState state)
        {
            var results = new NativeQueue<VoxelShapeQueryJobs.VoxelQueryResult>(Allocator.TempJob);
            
            try
            {
                var jobHandle = VoxelShapeQueryJobs.ScheduleCylinderQuery(
                    request.TargetPosition, request.TargetRotation, request.Param1, request.Param2,
                    request.Falloff, request.EdgeMultiplier, VOXEL_SIZE, ref results);
                
                jobHandle.Complete();
                ApplyQueuedDamage(storage, results, request.Damage, request.DamageType);
            }
            finally
            {
                results.Dispose();
            }
        }
        
        private void ProcessConeDamage(ref VoxelDamageRequest request, VoxelHealthStorage storage, ref SystemState state)
        {
            var results = new NativeQueue<VoxelShapeQueryJobs.VoxelQueryResult>(Allocator.TempJob);
            
            try
            {
                var jobHandle = VoxelShapeQueryJobs.ScheduleConeQuery(
                    request.TargetPosition, request.TargetRotation, request.Param1, request.Param2, request.Param3,
                    request.Falloff, request.EdgeMultiplier, VOXEL_SIZE, ref results);
                
                jobHandle.Complete();
                ApplyQueuedDamage(storage, results, request.Damage, request.DamageType);
            }
            finally
            {
                results.Dispose();
            }
        }
        
        private void ProcessCapsuleDamage(ref VoxelDamageRequest request, VoxelHealthStorage storage, ref SystemState state)
        {
            var results = new NativeQueue<VoxelShapeQueryJobs.VoxelQueryResult>(Allocator.TempJob);
            
            try
            {
                var jobHandle = VoxelShapeQueryJobs.ScheduleCapsuleQuery(
                    request.TargetPosition, request.TargetRotation, request.Param1, request.Param2,
                    request.Falloff, request.EdgeMultiplier, VOXEL_SIZE, ref results);
                
                jobHandle.Complete();
                ApplyQueuedDamage(storage, results, request.Damage, request.DamageType);
            }
            finally
            {
                results.Dispose();
            }
        }
        
        private void ProcessBoxDamage(ref VoxelDamageRequest request, VoxelHealthStorage storage, ref SystemState state)
        {
            float3 extents = new float3(request.Param1, request.Param2, request.Param3);
            var results = new NativeQueue<VoxelShapeQueryJobs.VoxelQueryResult>(Allocator.TempJob);
            
            try
            {
                var jobHandle = VoxelShapeQueryJobs.ScheduleBoxQuery(
                    request.TargetPosition, request.TargetRotation, extents,
                    request.Falloff, request.EdgeMultiplier, VOXEL_SIZE, ref results);
                
                jobHandle.Complete();
                ApplyQueuedDamage(storage, results, request.Damage, request.DamageType);
            }
            finally
            {
                results.Dispose();
            }
        }

        private void ApplyQueuedDamage(
            VoxelHealthStorage storage,
            NativeQueue<VoxelShapeQueryJobs.VoxelQueryResult> results,
            float baseDamage,
            VoxelDamageType damageType)
        {
            bool debug = EnableDebugLogs;
            while (results.TryDequeue(out var result))
            {
                float damage = baseDamage * result.DamageMultiplier;
                storage.ApplyDamage(result.VoxelCoord, damage, damageType, debug);
            }
        }
    }
}
