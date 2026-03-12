using System.Collections.Generic;
using UnityEngine;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Central registry of all cinematic definitions.
    /// Loaded from Resources/CinematicDatabase by CinematicBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Cinematic/Cinematic Database")]
    public class CinematicDatabaseSO : ScriptableObject
    {
        [Header("Cinematics")]
        public List<CinematicDefinitionSO> Cinematics = new();

        [Header("Defaults")]
        public SkipPolicy DefaultSkipPolicy = SkipPolicy.AnyoneCanSkip;
        [Tooltip("Default camera blend in seconds.")]
        public float BlendInDuration = 0.5f;
        [Tooltip("Default camera blend out seconds.")]
        public float BlendOutDuration = 0.5f;
        [Tooltip("HUD fade in/out seconds.")]
        public float HUDFadeDuration = 0.3f;
        [Tooltip("Letterbox bar screen fraction (0-0.25).")]
        [Range(0f, 0.25f)]
        public float LetterboxHeight = 0.12f;
    }
}
