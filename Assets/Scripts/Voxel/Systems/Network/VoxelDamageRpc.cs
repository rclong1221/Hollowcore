using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Collections;

namespace DIG.Voxel
{
    /// <summary>
    /// EPIC 15.10: RPC request for voxel damage from client to server.
    /// Clients send this to request destruction; server validates and processes.
    /// </summary>
    public struct VoxelDamageRpcRequest : IRpcCommand
    {
        /// <summary>Type of shape for destruction.</summary>
        public VoxelDamageShapeType ShapeType;
        
        /// <summary>Target position for destruction.</summary>
        public float3 TargetPosition;
        
        /// <summary>Target rotation for directional shapes.</summary>
        public quaternion TargetRotation;
        
        /// <summary>Shape parameter 1 (radius, angle, etc).</summary>
        public float Param1;
        
        /// <summary>Shape parameter 2 (height, length, etc).</summary>
        public float Param2;
        
        /// <summary>Shape parameter 3 (tip radius, etc).</summary>
        public float Param3;
        
        /// <summary>Requested damage amount.</summary>
        public float Damage;
        
        /// <summary>Damage type.</summary>
        public VoxelDamageType DamageType;
        
        /// <summary>Falloff type.</summary>
        public VoxelDamageFalloff Falloff;
        
        /// <summary>Edge multiplier.</summary>
        public float EdgeMultiplier;
        
        /// <summary>Tool bit type used (for validation).</summary>
        public ToolBitType ToolBitType;
    }
    
    /// <summary>
    /// EPIC 15.10: RPC response from server to clients about voxel destruction result.
    /// </summary>
    public struct VoxelDamageRpcResponse : IRpcCommand
    {
        /// <summary>Whether the request was accepted.</summary>
        public bool Accepted;
        
        /// <summary>Rejection reason if not accepted.</summary>
        public VoxelDamageRejectionReason RejectionReason;
        
        /// <summary>Position of destruction (for visual feedback).</summary>
        public float3 Position;
        
        /// <summary>Number of voxels affected.</summary>
        public int VoxelsAffected;
    }
    
    /// <summary>
    /// EPIC 15.10: Reasons a voxel damage request can be rejected.
    /// </summary>
    public enum VoxelDamageRejectionReason : byte
    {
        None = 0,
        OutOfRange = 1,
        RateLimited = 2,
        InvalidTool = 3,
        InvalidShape = 4,
        NotOnGround = 5,
        Blocked = 6
    }
    
    /// <summary>
    /// EPIC 15.10: System that receives voxel damage RPC requests on server.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VoxelDamageValidationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct VoxelDamageRpcReceiveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (request, receiveRpc, entity) in
                     SystemAPI.Query<RefRO<VoxelDamageRpcRequest>, RefRO<ReceiveRpcCommandRequest>>()
                     .WithEntityAccess())
            {
                var rpc = request.ValueRO;
                Entity sourceConnection = receiveRpc.ValueRO.SourceConnection;
                
                // Get player entity from connection
                Entity playerEntity = Entity.Null;
                if (SystemAPI.HasComponent<CommandTarget>(sourceConnection))
                {
                    var cmdTarget = SystemAPI.GetComponent<CommandTarget>(sourceConnection);
                    playerEntity = cmdTarget.targetEntity;
                }

                // Create VoxelDamageRequest entity for processing
                var requestEntity = ecb.CreateEntity();
                
                // Build request based on shape type
                VoxelDamageRequest damageRequest = rpc.ShapeType switch
                {
                    VoxelDamageShapeType.Point => VoxelDamageRequest.CreatePoint(
                        rpc.TargetPosition, playerEntity, rpc.TargetPosition, rpc.Damage, rpc.DamageType),
                        
                    VoxelDamageShapeType.Sphere => VoxelDamageRequest.CreateSphere(
                        rpc.TargetPosition, playerEntity, rpc.TargetPosition, rpc.Param1, rpc.Damage,
                        rpc.Falloff, rpc.EdgeMultiplier, rpc.DamageType),
                        
                    _ => VoxelDamageRequest.CreatePoint(
                        rpc.TargetPosition, playerEntity, rpc.TargetPosition, rpc.Damage, rpc.DamageType)
                };
                
                ecb.AddComponent(requestEntity, damageRequest);
                
                // Add source tracking for response
                ecb.AddComponent(requestEntity, new VoxelDamageRpcSource
                {
                    SourceConnection = sourceConnection
                });
                
                // Destroy RPC entity
                ecb.DestroyEntity(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// EPIC 15.10: Tracks which connection sent a damage request.
    /// </summary>
    public struct VoxelDamageRpcSource : IComponentData
    {
        public Entity SourceConnection;
    }
    
    /// <summary>
    /// EPIC 15.10: Sends response RPCs to clients after processing.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoxelDamageProcessingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct VoxelDamageRpcResponseSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (request, source, entity) in
                     SystemAPI.Query<RefRO<VoxelDamageRequest>, RefRO<VoxelDamageRpcSource>>()
                     .WithEntityAccess())
            {
                if (!request.ValueRO.IsProcessed)
                    continue;
                
                // Send response to client
                var responseEntity = ecb.CreateEntity();
                ecb.AddComponent(responseEntity, new VoxelDamageRpcResponse
                {
                    Accepted = request.ValueRO.IsValidated,
                    RejectionReason = VoxelDamageRejectionReason.None,
                    Position = request.ValueRO.TargetPosition,
                    VoxelsAffected = 1 // TODO: Track actual count
                });
                ecb.AddComponent(responseEntity, new SendRpcCommandRequest
                {
                    TargetConnection = source.ValueRO.SourceConnection
                });
                
                // Remove source tracking
                ecb.RemoveComponent<VoxelDamageRpcSource>(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
