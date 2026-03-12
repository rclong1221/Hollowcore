using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;

namespace Player.Systems.Abilities
{
    /// <summary>
    /// Detects vaultable obstacles and triggers VaultState when player presses jump near them.
    /// Uses ray/sphere casts to detect obstacles at vault-height in front of the player.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial struct VaultAbilitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (vaultState, playerState, playerInput, transform, velocity, config, entity) in
                SystemAPI.Query<RefRW<VaultState>, RefRO<PlayerState>, RefRO<PlayerInput>,
                        RefRO<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<AgilityConfig>>()
                    .WithAll<Simulate>()
                    .WithEntityAccess())
            {
                ref var vs = ref vaultState.ValueRW;
                var pState = playerState.ValueRO;
                var input = playerInput.ValueRO;
                var pos = transform.ValueRO.Position;
                var rot = transform.ValueRO.Rotation;
                var vel = velocity.ValueRO;
                var cfg = config.ValueRO;

                // Skip if already vaulting
                if (vs.IsVaulting)
                    continue;

                // Skip if not grounded or if vault is disabled
                if (!pState.IsGrounded || !cfg.CanVault)
                    continue;

                // Check for jump input (vault is triggered by jump near obstacle)
                if (!input.Jump.IsSet)
                    continue;

                // Get forward direction from rotation
                float3 forward = math.mul(rot, new float3(0, 0, 1));

                // Cast ray forward at waist height to detect obstacle
                float castHeight = 0.7f; // Waist height
                float castDistance = 1.0f; // How far ahead to check
                float3 rayStart = pos + new float3(0, castHeight, 0);
                float3 rayEnd = rayStart + forward * castDistance;

                var rayInput = new RaycastInput
                {
                    Start = rayStart,
                    End = rayEnd,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u, // Collide with everything
                        GroupIndex = 0
                    }
                };

                if (!physicsWorld.CastRay(rayInput, out var hit))
                    continue; // No obstacle ahead

                // Found obstacle - check if it's vaultable (within height range)
                // Cast another ray from above to find the top of the obstacle
                float3 topCheckStart = hit.Position + new float3(0, cfg.MaxVaultHeight + 0.5f, 0) + forward * 0.1f;
                float3 topCheckEnd = topCheckStart - new float3(0, cfg.MaxVaultHeight + 1f, 0);

                var topRayInput = new RaycastInput
                {
                    Start = topCheckStart,
                    End = topCheckEnd,
                    Filter = rayInput.Filter
                };

                float obstacleHeight;
                if (physicsWorld.CastRay(topRayInput, out var topHit))
                {
                    // Calculate obstacle height from player position
                    obstacleHeight = topHit.Position.y - pos.y;
                }
                else
                {
                    // Obstacle too tall or no top surface found
                    continue;
                }

                // Check if height is within vaultable range
                if (obstacleHeight < cfg.MinVaultHeight || obstacleHeight > cfg.MaxVaultHeight)
                    continue;

                // Check for space on the other side of the obstacle
                float3 clearanceCheckStart = topHit.Position + forward * 0.5f + new float3(0, 0.5f, 0);
                float3 clearanceCheckEnd = clearanceCheckStart - new float3(0, obstacleHeight + 0.5f, 0);

                var clearanceRayInput = new RaycastInput
                {
                    Start = clearanceCheckStart,
                    End = clearanceCheckEnd,
                    Filter = rayInput.Filter
                };

                // If this ray hits, there's ground on the other side (good!)
                // If it doesn't hit, there might be a gap (still allow vault, character will fall)
                // Just proceed with vault

                // Start vault!
                vs.IsVaulting = true;
                vs.VaultHeight = obstacleHeight;
                vs.StartVelocity = math.length(new float2(vel.Linear.x, vel.Linear.z));
                vs.TimeRemaining = vs.VaultDuration;

                // Note: Movement and positioning during vault should be handled by root motion
                // or a separate VaultMovementSystem
            }
        }
    }

    /// <summary>
    /// Handles vault movement - applies root motion or controlled movement during vault.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(VaultAbilitySystem))]
    public partial struct VaultMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (vaultState, transform, entity) in
                SystemAPI.Query<RefRW<VaultState>, RefRW<LocalTransform>>()
                    .WithAll<Simulate>()
                    .WithEntityAccess())
            {
                ref var vs = ref vaultState.ValueRW;

                if (!vs.IsVaulting)
                    continue;

                // During vault, movement is typically controlled by root motion
                // from the Opsive Vault animation. If root motion is not enabled,
                // we could add manual movement here:
                //
                // float progress = 1f - (vs.TimeRemaining / vs.VaultDuration);
                // float3 forward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
                // float vaultSpeed = 3f;
                // transform.ValueRW.Position += forward * vaultSpeed * dt;
                //
                // For now, rely on animation root motion or ClimbAnimatorBridge
                // handling of vault root motion.
            }
        }
    }
}
