using System;
using System.Collections.Generic;
using UnityEngine;

namespace Audio.Music
{
    public enum PlaylistMode : byte
    {
        Sequential = 0,
        Shuffle = 1,
        Single = 2
    }

    [Serializable]
    public class MusicTransition
    {
        [Tooltip("Target state ID to transition to.")]
        public string TargetStateId;

        [Tooltip("Event name that triggers this transition (matched via MusicController.FireEvent).")]
        public string TriggerEvent;

        [Tooltip("Crossfade duration override. 0 = use global default.")]
        [Min(0)]
        public float CrossfadeDuration;
    }

    [Serializable]
    public class MusicLayer
    {
        [Tooltip("Editor label for this layer.")]
        public string LayerName;

        [Tooltip("Audio clip for this intensity layer (should match main track duration for stems).")]
        public AudioClip Clip;

        [Tooltip("Intensity value (0-1) above which this layer fades in.")]
        [Range(0f, 1f)]
        public float ActivateThreshold = 0.5f;

        [Tooltip("Fade time when layer activates/deactivates.")]
        [Min(0.05f)]
        public float FadeTime = 1f;
    }

    [Serializable]
    public class MusicState
    {
        [Tooltip("Unique identifier for this music state.")]
        public string StateId;

        [Tooltip("Tracks to play in this state (playlist).")]
        public AudioClip[] Tracks = Array.Empty<AudioClip>();

        [Tooltip("How tracks are selected from the list.")]
        public PlaylistMode Mode = PlaylistMode.Sequential;

        [Tooltip("Base volume for this state.")]
        [Range(0f, 1f)]
        public float Volume = 0.7f;

        [Tooltip("Override crossfade-in duration. 0 = use global default.")]
        [Min(0)]
        public float CrossfadeIn;

        [Tooltip("Override crossfade-out duration. 0 = use global default.")]
        [Min(0)]
        public float CrossfadeOut;

        [Tooltip("Optional intensity layers that fade in/out based on SetIntensity().")]
        public MusicLayer[] IntensityLayers = Array.Empty<MusicLayer>();

        [Tooltip("Allowed outgoing transitions from this state.")]
        public MusicTransition[] Transitions = Array.Empty<MusicTransition>();
    }

    [CreateAssetMenu(fileName = "MusicStateMachine", menuName = "DIG/Audio/Music State Machine")]
    public class MusicStateMachineSO : ScriptableObject
    {
        [Tooltip("All music states in this state machine.")]
        public MusicState[] States = Array.Empty<MusicState>();

        [Tooltip("State to enter on initialization.")]
        public string DefaultState = "Explore";

        [Tooltip("Default crossfade duration when transitioning between states.")]
        [Min(0.1f)]
        public float GlobalCrossfadeDuration = 2f;

        private Dictionary<string, MusicState> _stateCache;

        public MusicState FindState(string stateId)
        {
            if (_stateCache == null) RebuildCache();

            if (_stateCache.TryGetValue(stateId, out var state))
                return state;
            return null;
        }

        public MusicTransition FindTransition(string fromState, string triggerEvent)
        {
            var state = FindState(fromState);
            if (state == null) return null;

            for (int i = 0; i < state.Transitions.Length; i++)
                if (state.Transitions[i].TriggerEvent == triggerEvent)
                    return state.Transitions[i];
            return null;
        }

        private void RebuildCache()
        {
            _stateCache = new Dictionary<string, MusicState>(States.Length, StringComparer.Ordinal);
            for (int i = 0; i < States.Length; i++)
            {
                if (States[i] == null || string.IsNullOrEmpty(States[i].StateId)) continue;
                _stateCache[States[i].StateId] = States[i];
            }
        }

        private void OnValidate()
        {
            _stateCache = null;
        }

        private void OnEnable()
        {
            _stateCache = null;
        }
    }
}
