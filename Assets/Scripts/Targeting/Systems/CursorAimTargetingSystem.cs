using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Core.Input;
using DIG.Player.Components;
using DIG.Targeting.Components;
using Player.Systems;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// When the local player's TargetData.Mode is CursorAim, performs a screen-to-world
    /// raycast via Unity.Physics and writes TargetData.TargetPoint + AimDirection.
    ///
    /// Uses the same ECS physics raycast pattern as ClickToMoveHandler (EPIC 18.15).
    /// Falls back to ground plane (Y=0) if the raycast misses.
    ///
    /// EPIC 18.19 - Phase 2
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TargetingModeDispatcherSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class CursorAimTargetingSystem : SystemBase
    {
        private const float MaxRaycastDistance = 200f;
        private const float SoftTargetRadius = 3f;

        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsWorldSingleton>();
        }

        protected override void OnUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;

            // Read cursor screen position from shared PlayerInputState
            var screenPos = new Vector2(
                PlayerInputState.CursorScreenPosition.x,
                PlayerInputState.CursorScreenPosition.y);

            UnityEngine.Ray ray = camera.ScreenPointToRay(screenPos);
            float3 rayStart = ray.origin;
            float3 rayDir = (float3)ray.direction;
            float3 rayEnd = rayStart + rayDir * MaxRaycastDistance;

            // ECS physics raycast
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var rayInput = new RaycastInput
            {
                Start = rayStart,
                End = rayEnd,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = CollisionLayers.Environment | CollisionLayers.Default
                                 | CollisionLayers.Creature | CollisionLayers.Player
                                 | CollisionLayers.Interactable | CollisionLayers.Ship,
                    GroupIndex = 0
                }
            };

            float3 hitPoint;
            bool didHit = physicsWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit);

            if (didHit)
            {
                hitPoint = hit.Position;
            }
            else
            {
                // Ground plane fallback (Y=0)
                if (math.abs(rayDir.y) > 0.001f)
                {
                    float t = -rayStart.y / rayDir.y;
                    if (t > 0f)
                        hitPoint = rayStart + rayDir * t;
                    else
                        return; // Camera looking away from ground
                }
                else
                {
                    return; // Horizontal ray, no ground intersection
                }
            }

            // Write to local player TargetData
            foreach (var (targetData, localToWorld, _) in
                SystemAPI.Query<RefRW<TargetData>, RefRO<Unity.Transforms.LocalToWorld>, RefRO<GhostOwnerIsLocal>>())
            {
                if (targetData.ValueRO.Mode != TargetingMode.CursorAim) continue;

                float3 playerPos = localToWorld.ValueRO.Position;
                float3 toTarget = hitPoint - playerPos;

                // Project onto XZ plane for aim direction
                float3 aimDir = new float3(toTarget.x, 0f, toTarget.z);
                float lenSq = math.lengthsq(aimDir);
                if (lenSq > 0.0001f)
                {
                    aimDir = math.normalize(aimDir);
                }
                else
                {
                    // Target directly above/below player — use camera forward projected onto XZ
                    float3 camFwd = (float3)camera.transform.forward;
                    aimDir = math.normalizesafe(new float3(camFwd.x, 0f, camFwd.z), new float3(0f, 0f, 1f));
                }

                targetData.ValueRW.TargetPoint = hitPoint;
                targetData.ValueRW.AimDirection = aimDir;
                targetData.ValueRW.TargetDistance = math.length(toTarget);

                // Soft-target: find nearest LockOnTarget entity near cursor hit point
                Entity nearestTarget = Entity.Null;
                float nearestDistSq = SoftTargetRadius * SoftTargetRadius;

                foreach (var (lockOn, targetLtw, candidateEntity) in
                    SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalToWorld>>()
                        .WithEntityAccess())
                {
                    float3 candidatePos = targetLtw.ValueRO.Position;
                    float3 offset = candidatePos - hitPoint;
                    offset.y = 0f;
                    float distSq = math.lengthsq(offset);

                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestTarget = candidateEntity;
                    }
                }

                targetData.ValueRW.TargetEntity = nearestTarget;
                targetData.ValueRW.HasValidTarget = nearestTarget != Entity.Null;
            }
        }
    }
}
