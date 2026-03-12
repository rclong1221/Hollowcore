using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 3: Advances multi-phase interaction sequences.
    ///
    /// Per phase type:
    /// - Instant: auto-advance next frame
    /// - Timed: advance when PhaseTimeElapsed >= Duration
    /// - InputSequence: advance when InputSequenceState.SequenceComplete
    ///
    /// On failure: reset to phase 0 (if ResetOnFail) or deactivate.
    /// On final phase complete: set SequenceComplete = true.
    ///
    /// Runs AFTER InteractAbilitySystem (which activates the sequence)
    /// and AFTER InputSequenceSystem (which validates combo inputs).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InteractAbilitySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhaseSequenceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InteractionPhaseState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (phaseState, phaseConfig, entity) in
                     SystemAPI.Query<RefRW<InteractionPhaseState>, RefRO<InteractionPhaseConfig>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                if (!phaseState.ValueRO.IsActive)
                    continue;

                ref var blob = ref phaseConfig.ValueRO.PhaseBlob.Value;
                int currentPhase = phaseState.ValueRO.CurrentPhase;

                if (currentPhase < 0 || currentPhase >= blob.Phases.Length)
                {
                    // Invalid phase — deactivate
                    phaseState.ValueRW.IsActive = false;
                    phaseState.ValueRW.CurrentPhase = -1;
                    continue;
                }

                // Tick timers
                phaseState.ValueRW.PhaseTimeElapsed += deltaTime;
                phaseState.ValueRW.TotalTimeElapsed += deltaTime;

                // Check total time limit
                if (blob.TotalTimeLimit > 0 && phaseState.ValueRO.TotalTimeElapsed >= blob.TotalTimeLimit)
                {
                    phaseState.ValueRW.PhaseFailed = true;
                }

                ref var phaseDef = ref blob.Phases[currentPhase];

                // Check for failure (set by InputSequenceSystem or timeout above)
                if (phaseState.ValueRO.PhaseFailed)
                {
                    if (blob.ResetOnFail)
                    {
                        // Reset to phase 0
                        phaseState.ValueRW.CurrentPhase = 0;
                        phaseState.ValueRW.PhaseTimeElapsed = 0;
                        phaseState.ValueRW.TotalTimeElapsed = 0;
                        phaseState.ValueRW.PhaseFailed = false;

                        // Reset input sequence state if present
                        if (SystemAPI.HasComponent<InputSequenceState>(entity))
                        {
                            var seqState = SystemAPI.GetComponentRW<InputSequenceState>(entity);
                            seqState.ValueRW.ActiveSequenceIndex = -1;
                            seqState.ValueRW.CurrentInputIndex = 0;
                            seqState.ValueRW.TimeSinceLastInput = 0;
                            seqState.ValueRW.SequenceComplete = false;
                            seqState.ValueRW.SequenceFailed = false;
                        }

                        // Initialize first phase if it's an InputSequence
                        ref var firstPhase = ref blob.Phases[0];
                        if (firstPhase.Type == PhaseType.InputSequence && firstPhase.InputSequenceIndex >= 0)
                        {
                            InitializeInputSequence(ref state, entity, firstPhase.InputSequenceIndex);
                        }
                    }
                    else
                    {
                        // Cancel the whole sequence
                        phaseState.ValueRW.IsActive = false;
                        phaseState.ValueRW.CurrentPhase = -1;
                        phaseState.ValueRW.PhaseFailed = false;
                        phaseState.ValueRW.PerformingEntity = Entity.Null;
                    }
                    continue;
                }

                // Check phase completion conditions
                bool phaseComplete = false;

                switch (phaseDef.Type)
                {
                    case PhaseType.Instant:
                        phaseComplete = true;
                        break;

                    case PhaseType.Timed:
                        if (phaseState.ValueRO.PhaseTimeElapsed >= phaseDef.Duration)
                            phaseComplete = true;
                        break;

                    case PhaseType.InputSequence:
                        if (SystemAPI.HasComponent<InputSequenceState>(entity))
                        {
                            var seqState = SystemAPI.GetComponent<InputSequenceState>(entity);
                            if (seqState.SequenceComplete)
                                phaseComplete = true;
                            else if (seqState.SequenceFailed)
                                phaseState.ValueRW.PhaseFailed = true;
                        }
                        break;
                }

                if (phaseComplete)
                {
                    int nextPhase = currentPhase + 1;

                    if (nextPhase >= blob.Phases.Length)
                    {
                        // All phases complete
                        phaseState.ValueRW.SequenceComplete = true;
                        phaseState.ValueRW.IsActive = false;
                    }
                    else
                    {
                        // Advance to next phase
                        phaseState.ValueRW.CurrentPhase = nextPhase;
                        phaseState.ValueRW.PhaseTimeElapsed = 0;

                        // If next phase is InputSequence, initialize it
                        ref var nextPhaseDef = ref blob.Phases[nextPhase];
                        if (nextPhaseDef.Type == PhaseType.InputSequence && nextPhaseDef.InputSequenceIndex >= 0)
                        {
                            InitializeInputSequence(ref state, entity, nextPhaseDef.InputSequenceIndex);
                        }
                    }
                }
            }
        }

        private void InitializeInputSequence(ref SystemState state, Entity entity, int sequenceIndex)
        {
            if (!SystemAPI.HasComponent<InputSequenceState>(entity))
                return;

            var seqState = SystemAPI.GetComponentRW<InputSequenceState>(entity);
            seqState.ValueRW.ActiveSequenceIndex = sequenceIndex;
            seqState.ValueRW.CurrentInputIndex = 0;
            seqState.ValueRW.TimeSinceLastInput = 0;
            seqState.ValueRW.SequenceComplete = false;
            seqState.ValueRW.SequenceFailed = false;
            seqState.ValueRW.PreviousInput = SequenceInput.None;
        }
    }
}
