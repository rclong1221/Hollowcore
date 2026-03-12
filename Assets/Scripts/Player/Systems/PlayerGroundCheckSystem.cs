using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using DIG.Player.Abilities;
using DIG.Player.Components;
using Player.Components;


/// <summary>
/// Performs ground detection using physics raycasts
/// Updates PlayerState.IsGrounded and tracks when player leaves ground (for coyote time)
/// </summary>
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerMovementSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct PlayerGroundCheckSystem : ISystem
{
    [ReadOnly] public ComponentLookup<SurfaceMaterialId> SurfaceMaterialLookup;
    private BlobAssetReference<Unity.Physics.Collider> _groundSphereBlob;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        SurfaceMaterialLookup = state.GetComponentLookup<SurfaceMaterialId>(true);

        // Create a sphere collider for ground checks
        // Radius 0.15f matches the value used in the job logic
        var geometry = new Unity.Physics.SphereGeometry { Radius = 0.15f };
        
        // Match the original Raycast filter (~0u for everything)
        // GroupIndex 0, BelongsTo ~0u, CollidesWith ~0u
        var filter = new CollisionFilter 
        { 
            BelongsTo = CollisionLayers.Player,
            CollidesWith = ~CollisionLayers.Player,
            GroupIndex = 0 
        };
        
        _groundSphereBlob = Unity.Physics.SphereCollider.Create(geometry, filter);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_groundSphereBlob.IsCreated)
        {
            _groundSphereBlob.Dispose();
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var currentTime = (float)SystemAPI.Time.ElapsedTime;
        
        SurfaceMaterialLookup.Update(ref state);
        
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        new GroundCheckJob
        {
            PhysicsWorld = physicsWorld,
            CurrentTime = currentTime,
            SurfaceMaterialLookup = SurfaceMaterialLookup,
            ecb = ecb,
            GroundSphereBlob = _groundSphereBlob
        }.ScheduleParallel();
    }

    [BurstCompile]
    public partial struct GroundCheckJob : IJobEntity
    {
        [ReadOnly] public PhysicsWorld PhysicsWorld;
        public float CurrentTime;
        [ReadOnly] public ComponentLookup<SurfaceMaterialId> SurfaceMaterialLookup;
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public BlobAssetReference<Unity.Physics.Collider> GroundSphereBlob;

        void Execute(
            Entity entity,
            [ChunkIndexInQuery] int chunkIndex,
            ref PlayerState pState,
            ref JumpAbility jAbility,
            in LocalTransform transform,
            in PhysicsVelocity velocity,
            in CharacterControllerSettings ccSettings,
            in Simulate simulate)
        {
            float3 pos = transform.Position;
            // Increased grace period from 0.2s to 0.35s to prevent premature grounding on steep slopes/voxels
            bool isJumping = jAbility.TimeSinceJump < 0.35f && jAbility.IsJumping;
            bool wasGrounded = pState.IsGrounded;
            Entity hitEntity = Entity.Null;
            float groundHeight = pos.y;
            float3 groundNormal = new float3(0, 1, 0);

            if (isJumping)
            {
                pState.IsGrounded = false;
            }
            else
            {
                 pState.IsGrounded = CheckGround(ref PhysicsWorld, entity, pos, pState.GroundCheckDistance, GroundSphereBlob, ccSettings.MaxSlopeAngleDeg, out groundHeight, out groundNormal, out hitEntity);
            }

            if (pState.IsGrounded)
            {
                pState.GroundHeight = groundHeight;
                pState.GroundNormal = groundNormal;
                if (hitEntity != Entity.Null && SurfaceMaterialLookup.HasComponent(hitEntity))
                {
                    int detectedMaterialId = SurfaceMaterialLookup[hitEntity].Id;
                    if (SurfaceMaterialLookup.HasComponent(entity))
                        ecb.SetComponent(chunkIndex, entity, new SurfaceMaterialId { Id = detectedMaterialId });
                    else
                        ecb.AddComponent(chunkIndex, entity, new SurfaceMaterialId { Id = detectedMaterialId });
                }
            }

            if (wasGrounded && !pState.IsGrounded)
            {
                jAbility.LastGroundedTime = CurrentTime;
            }
        }

        private bool CheckGround(ref PhysicsWorld physicsWorld, Entity entity, float3 position, float checkDistance, BlobAssetReference<Unity.Physics.Collider> sphereCollider, float maxSlopeAngle, out float groundHeight, out float3 groundNormal, out Entity hitEntity)
        {
            groundHeight = position.y;
            hitEntity = Entity.Null;
            groundNormal = new float3(0, 1, 0);

            if (!sphereCollider.IsCreated) return false;

            // SphereCast parameters
            float radius = 0.15f; 
            float footOffset = radius + 0.05f; 
            float castDistance = math.max(0.1f, checkDistance + 0.1f); 

            var start = position + new float3(0, footOffset, 0);
            var end = position - new float3(0, castDistance, 0);

            var castInput = new ColliderCastInput
            {
                Start = start,
                End = end,
                Orientation = quaternion.identity
            };
            castInput.SetCollider(sphereCollider);
            
            // Calculate walkable threshold (cosine of max slope angle)
            float walkableThreshold = math.cos(math.radians(maxSlopeAngle));

            if (physicsWorld.CollisionWorld.CastCollider(castInput, out ColliderCastHit hit))
            {
                if (hit.RigidBodyIndex >= 0)
                {
                    var bodyEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;
                    
                    if (bodyEntity != entity)
                    {
                        // WALKABLE ANGLE CHECK: hit.SurfaceNormal.y is the dot product with Up(0,1,0)
                        if (hit.SurfaceNormal.y >= walkableThreshold)
                        {
                            groundHeight = hit.Position.y;
                            groundNormal = hit.SurfaceNormal;
                            hitEntity = bodyEntity;
                            // LogGroundDebug("GROUND_HIT", bodyEntity, hit.SurfaceNormal, hit.Position, hit.Fraction, true);
                            return true;
                        }
                        else
                        {
                            // LogGroundDebug("GROUND_REJECT_STEEP", bodyEntity, hit.SurfaceNormal, hit.Position, hit.Fraction, false);
                        }
                    }
                }
            }
            else
            {
                //  LogGroundDebug("GROUND_MISS", Entity.Null, float3.zero, float3.zero, 0, false);
            }

            return false;
        }

        [BurstDiscard]
        private static void LogGroundDebug(string status, Entity entity, float3 normal, float3 pos, float fraction, bool success)
        {
            // Only log if requested (could filter here, but user asked for logs)
            UnityEngine.Debug.Log($"[STAIRS][GROUND][{status}] Entity:{entity.Index} | Normal:{normal} | Pos:{pos} | Frac:{fraction}");
        }
    }
}

