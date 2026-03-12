using Unity.Entities;

namespace DIG.Combat.Components
{
    /// <summary>
    /// EPIC 16.3: Corpse lifecycle phase.
    /// </summary>
    public enum CorpsePhase : byte
    {
        Ragdoll = 0,  // Ragdoll playing, full components still present
        Settled = 1,  // Ragdoll done, AI/combat components stripped
        Fading = 2    // Sinking into ground, physics stripped
    }

    /// <summary>
    /// EPIC 16.3: Tracks corpse lifecycle state.
    /// Baked disabled on damageable entities, enabled by DeathTransitionSystem on death.
    /// IEnableableComponent avoids ghost serialization issues from runtime AddComponent.
    /// </summary>
    public struct CorpseState : IComponentData, IEnableableComponent
    {
        public CorpsePhase Phase;
        public float PhaseStartTime;
        public float RagdollDuration;
        public float CorpseLifetime;
        public float FadeOutDuration;
        public bool IsBoss;
    }

    /// <summary>
    /// EPIC 16.3: Global corpse lifecycle configuration singleton.
    /// Place CorpseConfigAuthoring on a GameObject in your subscene.
    /// If absent, CorpseLifecycleSystem creates one with these defaults.
    /// </summary>
    public struct CorpseConfig : IComponentData
    {
        public float RagdollDuration;
        public float CorpseLifetime;
        public float FadeOutDuration;
        public int MaxCorpses;
        public bool PersistentBosses;
        public float DistanceCullRange;

        public static CorpseConfig Default => new CorpseConfig
        {
            RagdollDuration = 2.0f,
            CorpseLifetime = 15.0f,
            FadeOutDuration = 1.5f,
            MaxCorpses = 30,
            PersistentBosses = true,
            DistanceCullRange = 100f
        };
    }

    /// <summary>
    /// EPIC 16.3: Optional per-prefab override for corpse timings.
    /// Values of -1 mean "use global CorpseConfig default".
    /// </summary>
    public struct CorpseSettingsOverride : IComponentData
    {
        public float RagdollDuration;
        public float CorpseLifetime;
        public float FadeOutDuration;
        public bool IsBoss;

        public static CorpseSettingsOverride Default => new CorpseSettingsOverride
        {
            RagdollDuration = -1f,
            CorpseLifetime = -1f,
            FadeOutDuration = -1f,
            IsBoss = false
        };
    }
}
