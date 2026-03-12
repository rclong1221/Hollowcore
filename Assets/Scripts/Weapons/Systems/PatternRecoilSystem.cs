using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Weapons.Data;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// EPIC 15.5: Pattern-based Recoil System.
    /// Applies recoil patterns to weapon aim using ScriptableObject-defined sequences.
    /// Separates gameplay recoil (aim offset) from visual recoil (camera kick via FEEL).
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ShootableActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class PatternRecoilSystem : SystemBase
    {
        // Registry managed reference (set from MonoBehaviour)
        private static RecoilPatternRegistry s_Registry;

        /// <summary>
        /// Set the pattern registry (called from MonoBehaviour initialization).
        /// </summary>
        public static void SetRegistry(RecoilPatternRegistry registry)
        {
            s_Registry = registry;
        }

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            if (s_Registry == null) return;

            float deltaTime = SystemAPI.Time.DeltaTime;
            var registry = s_Registry;

            // Process pattern recoil for weapons using SystemAPI.Query
            foreach (var (patternState, recoilState, patternConfig, shootableState, fireState) in
                     SystemAPI.Query<RefRW<PatternRecoilState>, RefRW<RecoilState>,
                                    RefRO<PatternRecoil>, RefRO<ShootableState>, RefRO<WeaponFireState>>())
            {
                // Get pattern from registry
                var pattern = registry.GetPattern(patternConfig.ValueRO.PatternIndex);
                if (pattern == null) continue;

                // Detect new shot
                bool justFired = fireState.ValueRO.IsFiring && fireState.ValueRO.TimeSinceLastShot < deltaTime * 2f;

                if (justFired)
                {
                    // Reset recovery
                    patternState.ValueRW.IsRecovering = false;
                    patternState.ValueRW.TimeSinceLastShot = 0f;

                    // Get recoil offset for current shot
                    var offset = pattern.GetRecoilOffset(patternState.ValueRO.CurrentShotIndex, patternState.ValueRO.RandomSeed);

                    // Apply pattern offset to aim (gameplay)
                    float multiplier = patternConfig.ValueRO.PatternStrengthMultiplier > 0
                        ? patternConfig.ValueRO.PatternStrengthMultiplier
                        : 1f;

                    patternState.ValueRW.TargetOffset += new float2(offset.x, offset.y) * multiplier;
                    patternState.ValueRW.CurrentShotIndex++;

                    // Apply visual kick (separate from gameplay recoil)
                    patternState.ValueRW.VisualKick.y += pattern.VisualKickStrength * multiplier;
                    patternState.ValueRW.VisualKick.x += (patternState.ValueRO.RandomSeed % 2 < 1 ? 1 : -1) *
                                                 pattern.VisualKickStrength * 0.3f * multiplier;

                    // Update random seed for next shot
                    patternState.ValueRW.RandomSeed = math.frac(patternState.ValueRO.RandomSeed * 1.618f + 0.382f) * 1000f;
                }
                else
                {
                    patternState.ValueRW.TimeSinceLastShot += deltaTime;
                }

                // Smoothly apply target offset to accumulated
                float offsetLerpSpeed = 15f;
                patternState.ValueRW.AccumulatedOffset = math.lerp(
                    patternState.ValueRO.AccumulatedOffset,
                    patternState.ValueRO.TargetOffset,
                    math.saturate(deltaTime * offsetLerpSpeed)
                );

                // Feed into existing recoil system
                recoilState.ValueRW.CurrentRecoil = patternState.ValueRO.AccumulatedOffset;

                // Recover visual kick
                float kickRecoveryRate = pattern.VisualKickRecovery * deltaTime;
                patternState.ValueRW.VisualKick = math.lerp(patternState.ValueRO.VisualKick, float2.zero, kickRecoveryRate);

                // Handle pattern recovery when not firing
                if (patternState.ValueRO.TimeSinceLastShot > pattern.RecoveryDelay)
                {
                    patternState.ValueRW.IsRecovering = true;
                }

                if (patternState.ValueRO.IsRecovering)
                {
                    // Recover shot index
                    float recoveryProgress = (patternState.ValueRO.TimeSinceLastShot - pattern.RecoveryDelay) /
                                             pattern.RecoveryTimePerStep;
                    int stepsToRecover = (int)recoveryProgress;

                    if (stepsToRecover > 0 && patternState.ValueRO.CurrentShotIndex > 0)
                    {
                        int targetIndex = math.max(0, patternState.ValueRO.CurrentShotIndex - stepsToRecover);
                        patternState.ValueRW.CurrentShotIndex = targetIndex;
                    }

                    // Recover aim offset
                    float offsetRecoveryRate = deltaTime / pattern.RecoveryTimePerStep;
                    patternState.ValueRW.TargetOffset = math.lerp(
                        patternState.ValueRO.TargetOffset,
                        float2.zero,
                        offsetRecoveryRate
                    );

                    // Full reset after extended recovery
                    if (patternState.ValueRO.TimeSinceLastShot > pattern.RecoveryDelay +
                        pattern.RecoveryTimePerStep * pattern.PatternLength * 1.5f)
                    {
                        patternState.ValueRW.Reset();
                    }
                }
            }
        }
    }
}
