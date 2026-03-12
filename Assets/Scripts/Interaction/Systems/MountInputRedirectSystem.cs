using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 4: Forwards player input to mount entity.
    ///
    /// When a mount has TransferInputToMount = true and is occupied,
    /// reads PlayerInput from the occupant and writes to MountInput on the mount.
    /// Game-specific systems (turret rotation, vehicle driving) consume MountInput.
    ///
    /// Follows the same pattern as RideControlSystem (DIG.Player)
    /// and StationInputRoutingSystem (DIG.Runtime.Ship).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct MountInputRedirectSystem : ISystem
    {
        private ComponentLookup<PlayerInput> _playerInputLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MountInput>();
            _playerInputLookup = state.GetComponentLookup<PlayerInput>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _playerInputLookup.Update(ref state);

            foreach (var (mountPoint, mountInput) in
                     SystemAPI.Query<RefRO<MountPoint>, RefRW<MountInput>>()
                     .WithAll<Simulate>())
            {
                if (!mountPoint.ValueRO.TransferInputToMount)
                    continue;

                if (!mountPoint.ValueRO.IsOccupied)
                {
                    // No occupant — zero out input
                    mountInput.ValueRW = default;
                    continue;
                }

                Entity occupant = mountPoint.ValueRO.OccupantEntity;
                if (occupant == Entity.Null || !_playerInputLookup.HasComponent(occupant))
                {
                    mountInput.ValueRW = default;
                    continue;
                }

                var playerInput = _playerInputLookup[occupant];

                // Map PlayerInput → MountInput
                mountInput.ValueRW.Move = new float2(playerInput.Horizontal, playerInput.Vertical);
                mountInput.ValueRW.Look = playerInput.LookDelta;
                mountInput.ValueRW.Primary = playerInput.Use.IsSetByte;
                mountInput.ValueRW.Secondary = playerInput.AltUse.IsSetByte;
                mountInput.ValueRW.Interact = playerInput.Interact.IsSetByte;
                mountInput.ValueRW.Jump = playerInput.Jump.IsSetByte;
            }
        }
    }
}
