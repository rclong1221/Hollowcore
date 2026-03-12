using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 3: Validates input sequence (combo) inputs.
    ///
    /// Reads PlayerInput from the performing entity to detect directional/button inputs.
    /// Uses edge detection (PreviousInput) to fire only on input changes.
    ///
    /// Correct input → advance CurrentInputIndex, reset timeout.
    /// Wrong input → SequenceFailed = true.
    /// Timeout → SequenceFailed = true.
    /// All inputs matched → SequenceComplete = true.
    ///
    /// Runs in PredictedSimulationSystemGroup for responsive client-side feel.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct InputSequenceSystem : ISystem
    {
        private ComponentLookup<PlayerInput> _playerInputLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InputSequenceState>();
            _playerInputLookup = state.GetComponentLookup<PlayerInput>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _playerInputLookup.Update(ref state);
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (seqState, seqConfig, phaseState) in
                     SystemAPI.Query<RefRW<InputSequenceState>, RefRO<InputSequenceConfig>,
                                     RefRO<InteractionPhaseState>>()
                     .WithAll<Simulate>())
            {
                // Only process when a sequence is active
                if (seqState.ValueRO.ActiveSequenceIndex < 0)
                    continue;

                if (seqState.ValueRO.SequenceComplete || seqState.ValueRO.SequenceFailed)
                    continue;

                // Must have an active performing entity
                Entity performer = phaseState.ValueRO.PerformingEntity;
                if (performer == Entity.Null || !_playerInputLookup.HasComponent(performer))
                    continue;

                var playerInput = _playerInputLookup[performer];
                ref var blob = ref seqConfig.ValueRO.SequenceBlob.Value;

                int seqIdx = seqState.ValueRO.ActiveSequenceIndex;
                if (seqIdx < 0 || seqIdx >= blob.Sequences.Length)
                    continue;

                ref var seqDef = ref blob.Sequences[seqIdx];
                int currentStep = seqState.ValueRO.CurrentInputIndex;

                if (currentStep >= seqDef.Steps.Length)
                {
                    seqState.ValueRW.SequenceComplete = true;
                    continue;
                }

                // Tick timeout
                seqState.ValueRW.TimeSinceLastInput += deltaTime;

                // Check timeout
                ref var step = ref seqDef.Steps[currentStep];
                float timeout = step.TimeLimit > 0 ? step.TimeLimit : seqDef.DefaultStepTimeout;
                if (timeout > 0 && seqState.ValueRO.TimeSinceLastInput > timeout)
                {
                    seqState.ValueRW.SequenceFailed = true;
                    continue;
                }

                // Detect current input
                SequenceInput currentInput = DetectInput(ref playerInput);

                // Edge detection: only process on change from previous
                SequenceInput previousInput = seqState.ValueRO.PreviousInput;
                seqState.ValueRW.PreviousInput = currentInput;

                if (currentInput == SequenceInput.None || currentInput == previousInput)
                    continue;

                // We have a new input — check if it matches
                if (currentInput == step.ExpectedInput)
                {
                    // Correct input
                    seqState.ValueRW.CurrentInputIndex = currentStep + 1;
                    seqState.ValueRW.TimeSinceLastInput = 0;

                    // Check if sequence is now complete
                    if (currentStep + 1 >= seqDef.Steps.Length)
                    {
                        seqState.ValueRW.SequenceComplete = true;
                    }
                }
                else
                {
                    // Wrong input
                    seqState.ValueRW.SequenceFailed = true;
                }
            }
        }

        private static SequenceInput DetectInput(ref PlayerInput input)
        {
            // Directional inputs (from Horizontal/Vertical integers: -1, 0, 1)
            if (input.Vertical > 0) return SequenceInput.Up;
            if (input.Vertical < 0) return SequenceInput.Down;
            if (input.Horizontal < 0) return SequenceInput.Left;
            if (input.Horizontal > 0) return SequenceInput.Right;

            // Button inputs
            if (input.Interact.IsSet) return SequenceInput.Interact;
            if (input.Use.IsSet) return SequenceInput.Fire;
            if (input.Reload.IsSet) return SequenceInput.Reload;
            if (input.Jump.IsSet) return SequenceInput.Jump;

            return SequenceInput.None;
        }
    }
}
