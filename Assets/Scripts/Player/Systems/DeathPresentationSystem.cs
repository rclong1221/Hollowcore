using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Client-side system for death presentation effects.
    /// Tracks health changes and death state for UI/VFX/audio feedback.
    /// </summary>
    /// <remarks>
    /// Design goals (EPIC 4.1):
    /// - Client-only: runs in PresentationSystemGroup for VFX/audio triggers
    /// - Prediction-safe: clients predict feedback, not health/death outcomes
    /// - Provides data for:
    ///   - Low-health vignette and heartbeat effects
    ///   - Death screen activation
    ///   - Hit direction indicators
    /// 
    /// Note: Actual UI/VFX is typically handled by MonoBehaviour systems that
    /// read this component data. This system prepares presentation state.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct DeathPresentationSystem : ISystem
    {
        private float _lastLocalHealth;
        private DeathPhase _lastDeathPhase;

        public void OnCreate(ref SystemState state)
        {
            _lastLocalHealth = -1f;
            _lastDeathPhase = DeathPhase.Alive;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Find local player (the one this client controls)
            foreach (var (health, deathState, presentationState, entity) in
                SystemAPI.Query<RefRO<Health>, RefRO<DeathState>, RefRW<DeathPresentationState>>()
                .WithAll<GhostOwnerIsLocal>()
                .WithEntityAccess())
            {
                var presentation = presentationState.ValueRW;
                var currentHealth = health.ValueRO.Current;
                var maxHealth = health.ValueRO.Max;
                var currentPhase = deathState.ValueRO.Phase;

                // Detect health change for hit effects
                if (_lastLocalHealth >= 0f && currentHealth < _lastLocalHealth)
                {
                    float damageAmount = _lastLocalHealth - currentHealth;
                    presentation.LastDamageTime = (float)SystemAPI.Time.ElapsedTime;
                    presentation.LastDamageAmount = damageAmount;
                    presentation.TriggerHitEffect = true;
                }
                else
                {
                    presentation.TriggerHitEffect = false;
                }

                // Calculate low health intensity (0-1, increases as health decreases)
                float healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0f;
                presentation.LowHealthIntensity = math.saturate(1f - (healthPercent / 0.3f)); // Full intensity below 30%

                // Detect death transition for death screen
                if (_lastDeathPhase == DeathPhase.Alive && 
                    (currentPhase == DeathPhase.Dead || currentPhase == DeathPhase.Downed))
                {
                    presentation.TriggerDeathEffect = true;
                    presentation.DeathTime = (float)SystemAPI.Time.ElapsedTime;
                }
                else
                {
                    presentation.TriggerDeathEffect = false;
                }

                // Update state
                presentation.CurrentHealthPercent = healthPercent;
                presentation.IsDead = currentPhase == DeathPhase.Dead || currentPhase == DeathPhase.Downed;

                // Cache for next frame
                _lastLocalHealth = currentHealth;
                _lastDeathPhase = currentPhase;
            }
        }
    }

    /// <summary>
    /// Presentation state component for client-side death/damage effects.
    /// Added to player entities for UI systems to read.
    /// </summary>
    /// <remarks>
    /// This component is not replicated - it's local client state derived from
    /// the replicated Health and DeathState components.
    /// </remarks>
    public struct DeathPresentationState : IComponentData
    {
        /// <summary>
        /// Current health as percentage (0-1).
        /// </summary>
        public float CurrentHealthPercent;

        /// <summary>
        /// Intensity of low-health effects (0 = healthy, 1 = critical).
        /// </summary>
        public float LowHealthIntensity;

        /// <summary>
        /// True if player is dead or downed.
        /// </summary>
        public bool IsDead;

        /// <summary>
        /// Time when death occurred (for death screen timing).
        /// </summary>
        public float DeathTime;

        /// <summary>
        /// Time when last damage was taken (for hit effect timing).
        /// </summary>
        public float LastDamageTime;

        /// <summary>
        /// Amount of last damage taken (for hit effect intensity).
        /// </summary>
        public float LastDamageAmount;

        /// <summary>
        /// One-frame trigger for hit effect.
        /// </summary>
        public bool TriggerHitEffect;

        /// <summary>
        /// One-frame trigger for death effect.
        /// </summary>
        public bool TriggerDeathEffect;

        /// <summary>
        /// Default values.
        /// </summary>
        public static DeathPresentationState Default => new DeathPresentationState
        {
            CurrentHealthPercent = 1f,
            LowHealthIntensity = 0f,
            IsDead = false,
            DeathTime = 0f,
            LastDamageTime = 0f,
            LastDamageAmount = 0f,
            TriggerHitEffect = false,
            TriggerDeathEffect = false
        };
    }
}
