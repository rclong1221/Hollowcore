using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Zones
{
    /// <summary>
    /// Managed singleton that tracks active reverb zones via stack.
    /// Innermost zone wins. Crossfades AudioMixer snapshots on zone transitions.
    /// EPIC 15.27 Phase 4.
    /// </summary>
    public class AudioReverbZoneManager : MonoBehaviour
    {
        public static AudioReverbZoneManager Instance { get; private set; }

        [Header("Mixer")]
        [Tooltip("The master AudioMixer")]
        public AudioMixer Mixer;

        [Header("Snapshots")]
        [Tooltip("Snapshot for OpenField (fallback)")]
        public AudioMixerSnapshot OpenFieldSnapshot;
        public AudioMixerSnapshot ForestSnapshot;
        public AudioMixerSnapshot SmallRoomSnapshot;
        public AudioMixerSnapshot LargeHallSnapshot;
        public AudioMixerSnapshot TunnelSnapshot;
        public AudioMixerSnapshot CaveSnapshot;
        public AudioMixerSnapshot UnderwaterSnapshot;
        public AudioMixerSnapshot ShipInteriorSnapshot;
        public AudioMixerSnapshot ShipExteriorSnapshot;

        // Public state for debug/UI
        public ReverbZoneAuthoring CurrentZone => _zoneStack.Count > 0 ? _zoneStack[_zoneStack.Count - 1] : null;
        public ReverbZoneAuthoring PreviousZone { get; private set; }
        public float TransitionProgress { get; private set; }
        public float IndoorFactor { get; private set; }

        private readonly List<ReverbZoneAuthoring> _zoneStack = new List<ReverbZoneAuthoring>();
        private float _transitionTimer;
        private float _transitionDuration;
        private float _targetIndoorFactor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void EnterZone(ReverbZoneAuthoring zone)
        {
            if (zone == null || _zoneStack.Contains(zone)) return;

            PreviousZone = CurrentZone;

            // Insert sorted by priority (highest at end = innermost wins)
            int insertIdx = _zoneStack.Count;
            for (int i = 0; i < _zoneStack.Count; i++)
            {
                if (_zoneStack[i].Priority > zone.Priority)
                {
                    insertIdx = i;
                    break;
                }
            }
            _zoneStack.Insert(insertIdx, zone);

            TransitionToCurrentZone();
        }

        public void ExitZone(ReverbZoneAuthoring zone)
        {
            if (zone == null || !_zoneStack.Contains(zone)) return;

            PreviousZone = CurrentZone;
            _zoneStack.Remove(zone);
            TransitionToCurrentZone();
        }

        private void TransitionToCurrentZone()
        {
            var target = CurrentZone;
            float duration = target != null ? target.TransitionDuration : 1.5f;

            var snapshot = GetSnapshot(target);
            if (snapshot != null)
            {
                snapshot.TransitionTo(duration);
            }

            _transitionDuration = duration;
            _transitionTimer = 0f;

            // Update indoor factor target
            _targetIndoorFactor = (target != null && target.IsInterior) ? 1f : 0f;
        }

        private void Update()
        {
            // Track transition progress
            if (_transitionDuration > 0f)
            {
                _transitionTimer += Time.deltaTime;
                TransitionProgress = Mathf.Clamp01(_transitionTimer / _transitionDuration);
            }

            // Lerp indoor factor
            float speed = 2f; // 0.5s transition
            IndoorFactor = Mathf.Lerp(IndoorFactor, _targetIndoorFactor, Time.deltaTime * speed);
        }

        private AudioMixerSnapshot GetSnapshot(ReverbZoneAuthoring zone)
        {
            if (zone == null) return OpenFieldSnapshot; // fallback

            switch (zone.Preset)
            {
                case ReverbPreset.OpenField: return OpenFieldSnapshot;
                case ReverbPreset.Forest: return ForestSnapshot;
                case ReverbPreset.SmallRoom: return SmallRoomSnapshot;
                case ReverbPreset.LargeHall: return LargeHallSnapshot;
                case ReverbPreset.Tunnel: return TunnelSnapshot;
                case ReverbPreset.Cave: return CaveSnapshot;
                case ReverbPreset.Underwater: return UnderwaterSnapshot;
                case ReverbPreset.Ship_Interior: return ShipInteriorSnapshot;
                case ReverbPreset.Ship_Exterior: return ShipExteriorSnapshot;
                case ReverbPreset.Custom: return zone.CustomSnapshot;
                default: return OpenFieldSnapshot;
            }
        }

        /// <summary>Get all zones currently in the stack (for editor display).</summary>
        public IReadOnlyList<ReverbZoneAuthoring> GetZoneStack() => _zoneStack;
    }
}
