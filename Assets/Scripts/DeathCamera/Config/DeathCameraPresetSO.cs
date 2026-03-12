using UnityEngine;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Per-game-mode preset that overrides specific fields from the base config.
    /// Create via: Create > DIG > Death Camera > Game Mode Preset
    /// </summary>
    [CreateAssetMenu(fileName = "DeathCameraPreset", menuName = "DIG/Death Camera/Game Mode Preset")]
    public class DeathCameraPresetSO : ScriptableObject
    {
        public string PresetName = "Default";

        [Header("Phase Overrides")]
        [Tooltip("Override the phase sequence (empty = use base config)")]
        public DeathCameraPhaseType[] PhaseSequenceOverride;

        [Header("Kill Cam Overrides")]
        public OptionalBool KillCamEnabled;
        public OptionalFloat KillCamDuration;
        public OptionalBool KillCamSlowMotion;
        public OptionalFloat KillCamTimeScale;

        [Header("Death Recap Overrides")]
        public OptionalBool DeathRecapEnabled;
        public OptionalFloat DeathRecapDuration;
        public OptionalBool ShowDamageBreakdown;

        [Header("Spectator Overrides")]
        public OptionalBool SpectatorEnabled;
        public OptionalBool AllowTPSOrbit;
        public OptionalBool AllowIsometric;
        public OptionalBool AllowTopDown;
        public OptionalBool AllowIsometricRotatable;
        public OptionalBool AllowFreeCam;
        public OptionalFloat TransitionBetweenPlayers;

        /// <summary>
        /// Apply this preset's overrides onto a config, producing an effective config.
        /// Does not modify the original — copies values to a runtime instance.
        /// </summary>
        public void ApplyTo(DeathCameraConfigSO target)
        {
            if (PhaseSequenceOverride != null && PhaseSequenceOverride.Length > 0)
                target.PhaseSequence = PhaseSequenceOverride;

            if (KillCamEnabled.HasValue) target.KillCamEnabled = KillCamEnabled.Value;
            if (KillCamDuration.HasValue) target.KillCamDuration = KillCamDuration.Value;
            if (KillCamSlowMotion.HasValue) target.KillCamSlowMotion = KillCamSlowMotion.Value;
            if (KillCamTimeScale.HasValue) target.KillCamTimeScale = KillCamTimeScale.Value;

            if (DeathRecapEnabled.HasValue) target.DeathRecapEnabled = DeathRecapEnabled.Value;
            if (DeathRecapDuration.HasValue) target.DeathRecapDuration = DeathRecapDuration.Value;
            if (ShowDamageBreakdown.HasValue) target.ShowDamageBreakdown = ShowDamageBreakdown.Value;

            if (SpectatorEnabled.HasValue) target.SpectatorEnabled = SpectatorEnabled.Value;
            if (AllowTPSOrbit.HasValue) target.AllowTPSOrbit = AllowTPSOrbit.Value;
            if (AllowIsometric.HasValue) target.AllowIsometric = AllowIsometric.Value;
            if (AllowTopDown.HasValue) target.AllowTopDown = AllowTopDown.Value;
            if (AllowIsometricRotatable.HasValue) target.AllowIsometricRotatable = AllowIsometricRotatable.Value;
            if (AllowFreeCam.HasValue) target.AllowFreeCam = AllowFreeCam.Value;
            if (TransitionBetweenPlayers.HasValue) target.TransitionBetweenPlayers = TransitionBetweenPlayers.Value;
        }
    }

    /// <summary>
    /// Optional bool for preset overrides. Only applies when Override is true.
    /// </summary>
    [System.Serializable]
    public struct OptionalBool
    {
        public bool Override;
        public bool Value;
        public bool HasValue => Override;
    }

    /// <summary>
    /// Optional float for preset overrides. Only applies when Override is true.
    /// </summary>
    [System.Serializable]
    public struct OptionalFloat
    {
        public bool Override;
        public float Value;
        public bool HasValue => Override;
    }
}
