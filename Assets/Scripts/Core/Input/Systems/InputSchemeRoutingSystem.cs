using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Targeting;

namespace DIG.Core.Input
{
    /// <summary>
    /// Reads InputSchemeState and adjusts PlayerInput accordingly:
    /// - ShooterDirect: no modification (pass-through)
    /// - HybridToggle + modifier held: zeros LookDelta, writes cursor AimDirection
    /// - TacticalCursor: zeros LookDelta, writes cursor AimDirection
    ///
    /// Runs AFTER PlayerInputSystem so it overrides only the fields that differ.
    ///
    /// EPIC 15.18
    /// </summary>
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.PlayerInputSystem))]
    public partial class InputSchemeRoutingSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnUpdate()
        {
            foreach (var (playerInput, schemeState, localTransform, entity) in
                SystemAPI.Query<RefRW<PlayerInput>, RefRO<InputSchemeState>, RefRO<LocalTransform>>()
                    .WithAll<GhostOwnerIsLocal>()
                    .WithEntityAccess())
            {
                var scheme = schemeState.ValueRO.ActiveScheme;
                bool cursorFree = scheme == InputScheme.TacticalCursor
                    || (scheme == InputScheme.HybridToggle && schemeState.ValueRO.IsTemporaryCursorActive);

                if (!cursorFree)
                    continue; // ShooterDirect or HybridToggle without modifier — no-op

                // Zero LookDelta so camera does not orbit
                playerInput.ValueRW.LookDelta = float2.zero;

                // If hover result has a valid hit point, compute aim direction from player toward it
                if (EntityManager.HasComponent<CursorHoverResult>(entity))
                {
                    var hover = EntityManager.GetComponentData<CursorHoverResult>(entity);
                    if (hover.IsValid)
                    {
                        float3 playerPos = localTransform.ValueRO.Position;
                        float3 toTarget = hover.HitPoint - playerPos;
                        toTarget.y = 0f; // Flatten for horizontal aim
                        float len = math.length(toTarget);

                        if (len > 0.01f)
                        {
                            // Store aim direction in CameraYaw so downstream systems
                            // can derive facing from it. Convert direction to yaw angle.
                            float yaw = math.degrees(math.atan2(toTarget.x, toTarget.z));
                            playerInput.ValueRW.CameraYaw = yaw;
                            playerInput.ValueRW.CameraYawValid = 1;
                        }
                    }
                }
            }
        }
    }
}
