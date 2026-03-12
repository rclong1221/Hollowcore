using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using Player.Components;
using DIG.Player.Abilities;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 13.14.5: Teleport System
    ///
    /// Handles player teleportation and signals the fall system about immediate transform changes.
    /// This ensures the fall ability ends cleanly when a player is teleported to the ground.
    ///
    /// Usage:
    /// 1. Enable TeleportEvent component and set TargetPosition/TargetRotation
    /// 2. This system moves the entity and sets FallAbility.PendingImmediateTransformChange
    /// 3. FallDetectionSystem checks this flag and handles fall state cleanup
    ///
    /// For systems that move entities directly (like airlocks), they should set
    /// FallAbility.PendingImmediateTransformChange = true before moving the entity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(FallDetectionSystem))]
    public partial struct TeleportSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (teleportRef, transformRef, fallAbilityRef, entity) in
                     SystemAPI.Query<
                         RefRO<TeleportEvent>,
                         RefRW<LocalTransform>,
                         RefRW<FallAbility>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                // Check if teleport is requested
                if (!SystemAPI.IsComponentEnabled<TeleportEvent>(entity))
                    continue;

                var teleport = teleportRef.ValueRO;
                ref var transform = ref transformRef.ValueRW;
                ref var fallAbility = ref fallAbilityRef.ValueRW;

                // Apply teleport
                transform.Position = teleport.TargetPosition;
                transform.Rotation = teleport.TargetRotation;

                // Signal fall system about immediate transform change
                // The SnapAnimator flag is used by Opsive to determine if animator should snap
                // In our case, we always signal the change and let FallDetectionSystem handle it
                fallAbility.PendingImmediateTransformChange = true;

                // Disable the teleport event (consumed)
                SystemAPI.SetComponentEnabled<TeleportEvent>(entity, false);
            }
        }
    }
}
