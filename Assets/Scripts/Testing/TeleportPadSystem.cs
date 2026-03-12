using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Player.Components;
using DIG.Player.Abilities;

namespace DIG.Testing
{
    /// <summary>
    /// System that handles teleport pad triggers.
    /// When a player enters a teleport pad trigger, they are teleported to the destination.
    /// Uses the TeleportEvent component to properly signal the fall system.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct TeleportPadSystem : ISystem
    {
        private ComponentLookup<TeleportPad> _teleportPadLookup;
        private ComponentLookup<PlayerState> _playerStateLookup;
        private ComponentLookup<TeleportEvent> _teleportEventLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            _teleportPadLookup = state.GetComponentLookup<TeleportPad>(isReadOnly: false);
            _playerStateLookup = state.GetComponentLookup<PlayerState>(isReadOnly: true);
            _teleportEventLookup = state.GetComponentLookup<TeleportEvent>(isReadOnly: false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _teleportPadLookup.Update(ref state);
            _playerStateLookup.Update(ref state);
            _teleportEventLookup.Update(ref state);
            _transformLookup.Update(ref state);

            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            state.Dependency = new TeleportPadJob
            {
                TeleportPadLookup = _teleportPadLookup,
                PlayerStateLookup = _playerStateLookup,
                TeleportEventLookup = _teleportEventLookup,
                TransformLookup = _transformLookup,
                CurrentTime = currentTime
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        struct TeleportPadJob : ITriggerEventsJob
        {
            public ComponentLookup<TeleportPad> TeleportPadLookup;
            [ReadOnly] public ComponentLookup<PlayerState> PlayerStateLookup;
            public ComponentLookup<TeleportEvent> TeleportEventLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public float CurrentTime;

            public void Execute(TriggerEvent triggerEvent)
            {
                Entity entityA = triggerEvent.EntityA;
                Entity entityB = triggerEvent.EntityB;

                // Determine which is the pad and which is the player
                Entity padEntity = Entity.Null;
                Entity playerEntity = Entity.Null;

                if (TeleportPadLookup.HasComponent(entityA) && PlayerStateLookup.HasComponent(entityB))
                {
                    padEntity = entityA;
                    playerEntity = entityB;
                }
                else if (TeleportPadLookup.HasComponent(entityB) && PlayerStateLookup.HasComponent(entityA))
                {
                    padEntity = entityB;
                    playerEntity = entityA;
                }

                if (padEntity == Entity.Null || playerEntity == Entity.Null)
                    return;

                // Check cooldown
                var pad = TeleportPadLookup[padEntity];
                if (CurrentTime - pad.LastTeleportTime < pad.Cooldown)
                    return;

                // Check if player has TeleportEvent component
                if (!TeleportEventLookup.HasComponent(playerEntity))
                    return;

                // Get current player rotation if preserving
                var currentTransform = TransformLookup[playerEntity];
                var targetRotation = pad.PreserveRotation ? currentTransform.Rotation : pad.DestinationRotation;

                // Set up the teleport event
                var teleportEvent = new TeleportEvent
                {
                    TargetPosition = pad.Destination,
                    TargetRotation = targetRotation,
                    SnapAnimator = true
                };

                TeleportEventLookup[playerEntity] = teleportEvent;
                TeleportEventLookup.SetComponentEnabled(playerEntity, true);

                // Update cooldown
                pad.LastTeleportTime = CurrentTime;
                TeleportPadLookup[padEntity] = pad;
            }
        }
    }
}
