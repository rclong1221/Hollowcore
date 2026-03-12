using Unity.Entities;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Disables/enables player input during cinematic playback.
    /// Reads CinematicState.IsPlaying and zeros PlayerInputComponent fields
    /// for FullCinematic and TextOverlay types. InWorldEvent does not lock input.
    /// Skip input is handled separately by CinematicSkipInputSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CinematicInputLockSystem : SystemBase
    {
        private EntityQuery _stateQuery;
        private EntityQuery _playerInputQuery;
        private bool _wasLocked;

        protected override void OnCreate()
        {
            _stateQuery = GetEntityQuery(ComponentType.ReadOnly<CinematicState>());
            _playerInputQuery = GetEntityQuery(
                ComponentType.ReadWrite<global::Player.Components.PlayerInputComponent>(),
                ComponentType.ReadOnly<PlayerTag>()
            );
            RequireForUpdate(_stateQuery);
        }

        protected override void OnUpdate()
        {
            var state = _stateQuery.GetSingleton<CinematicState>();
            bool shouldLock = state.IsPlaying && state.CinematicType != CinematicType.InWorldEvent;

            if (shouldLock && !_wasLocked)
            {
                // Lock input: zero out all movement/action inputs
                ZeroPlayerInput();
                _wasLocked = true;
            }
            else if (!shouldLock && _wasLocked)
            {
                // Unlock: inputs will be naturally restored by input system next frame
                _wasLocked = false;
            }
            else if (shouldLock)
            {
                // Keep zeroing input while locked (input system may write values each frame)
                ZeroPlayerInput();
            }
        }

        private void ZeroPlayerInput()
        {
            var entities = _playerInputQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var input = EntityManager.GetComponentData<global::Player.Components.PlayerInputComponent>(entities[i]);
                input.Move = Unity.Mathematics.float2.zero;
                input.LookDelta = Unity.Mathematics.float2.zero;
                input.ZoomDelta = 0f;
                input.Jump = 0;
                input.Crouch = 0;
                input.Sprint = 0;
                input.Slide = 0;
                input.DodgeRoll = 0;
                input.DodgeDive = 0;
                input.Prone = 0;
                input.LeanLeft = 0;
                input.LeanRight = 0;
                EntityManager.SetComponentData(entities[i], input);
            }
            entities.Dispose();
        }
    }
}
