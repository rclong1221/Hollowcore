using UnityEngine;
using UnityEditor;
using Audio.Events;
using Audio.Music;
using Audio.Ambient;
using Audio.Systems;
using DIG.Editor.AudioWorkstation;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// EPIC 18.8: Audio Event Workstation module.
    /// Audition AudioEventSOs in-editor, preview music state machines,
    /// inspect ambient soundscapes, and monitor active audio event instances.
    /// </summary>
    public class AudioEventModule : IAudioModule
    {
        private enum SubTab { Audition, MusicPreview, AmbientPreview, Monitor }

        private SubTab _subTab;
        private Vector2 _scrollPos;

        // Audition
        private AudioEventSO _selectedEvent;
        private AudioSource _previewSource;

        // Music
        private MusicStateMachineSO _selectedMachine;
        private Vector2 _musicScroll;

        // Ambient
        private AmbientSoundscapeSO _selectedSoundscape;
        private AudioSource _ambientPreviewSource;

        // Asset list cache — avoids FindAssets/LoadAssetAtPath every repaint
        private AudioEventSO[] _cachedEventAssets;
        private bool _assetCacheDirty = true;
        private double _lastCacheTime;
        private const double kCacheStaleSeconds = 3.0;

        public void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(_subTab == SubTab.Audition, "Audition", EditorStyles.toolbarButton))
                _subTab = SubTab.Audition;
            if (GUILayout.Toggle(_subTab == SubTab.MusicPreview, "Music", EditorStyles.toolbarButton))
                _subTab = SubTab.MusicPreview;
            if (GUILayout.Toggle(_subTab == SubTab.AmbientPreview, "Ambient", EditorStyles.toolbarButton))
                _subTab = SubTab.AmbientPreview;
            if (GUILayout.Toggle(_subTab == SubTab.Monitor, "Monitor", EditorStyles.toolbarButton))
                _subTab = SubTab.Monitor;
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_subTab)
            {
                case SubTab.Audition: DrawAudition(); break;
                case SubTab.MusicPreview: DrawMusicPreview(); break;
                case SubTab.AmbientPreview: DrawAmbientPreview(); break;
                case SubTab.Monitor: DrawMonitor(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ---- Audition ----

        private void DrawAudition()
        {
            EditorGUILayout.LabelField("Audio Event Audition", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedEvent = (AudioEventSO)EditorGUILayout.ObjectField(
                "Audio Event", _selectedEvent, typeof(AudioEventSO), false);

            if (_selectedEvent == null)
            {
                EditorGUILayout.HelpBox("Drag an AudioEventSO here to preview it.", MessageType.Info);
                DrawEventAssetList();
                return;
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Event ID", _selectedEvent.EventId);
            EditorGUILayout.LabelField("Bus", _selectedEvent.Bus.ToString());
            EditorGUILayout.LabelField("Priority", _selectedEvent.Priority.ToString());
            EditorGUILayout.LabelField("Selection", _selectedEvent.SelectionMode.ToString());
            EditorGUILayout.LabelField("Volume",
                $"{_selectedEvent.Volume.Min:F2} - {_selectedEvent.Volume.Max:F2}");
            EditorGUILayout.LabelField("Pitch",
                $"{_selectedEvent.Pitch.Min:F2} - {_selectedEvent.Pitch.Max:F2}");
            EditorGUILayout.LabelField("Cooldown", $"{_selectedEvent.Cooldown:F2}s");
            EditorGUILayout.LabelField("Max Instances", _selectedEvent.MaxInstances.ToString());
            EditorGUILayout.LabelField("Spatial Blend", _selectedEvent.SpatialBlend.ToString("F2"));
            EditorGUILayout.LabelField("Range",
                $"{_selectedEvent.MinDistance:F1}m - {_selectedEvent.MaxDistance:F1}m");

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Clip Variations", EditorStyles.boldLabel);

            if (_selectedEvent.Clips == null || _selectedEvent.Clips.Length == 0)
            {
                EditorGUILayout.HelpBox("No clips assigned.", MessageType.Warning);
                return;
            }

            for (int i = 0; i < _selectedEvent.Clips.Length; i++)
            {
                var clip = _selectedEvent.Clips[i];
                EditorGUILayout.BeginHorizontal();

                string clipName = clip != null ? clip.name : "(null)";
                string duration = clip != null ? $"{clip.length:F2}s" : "--";
                EditorGUILayout.LabelField($"  [{i}] {clipName} ({duration})", GUILayout.ExpandWidth(true));

                EditorGUI.BeginDisabledGroup(clip == null);
                if (GUILayout.Button("Play", GUILayout.Width(50)))
                    PreviewClip(clip);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play Event (Random)", GUILayout.Height(28)))
            {
                int idx = Random.Range(0, _selectedEvent.Clips.Length);
                if (_selectedEvent.Clips[idx] != null)
                    PreviewClip(_selectedEvent.Clips[idx]);
            }
            if (GUILayout.Button("Stop", GUILayout.Width(60), GUILayout.Height(28)))
                StopPreview();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEventAssetList()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("All Audio Events in Project", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                _assetCacheDirty = true;
            EditorGUILayout.EndHorizontal();

            RefreshAssetCacheIfNeeded();

            if (_cachedEventAssets == null || _cachedEventAssets.Length == 0)
            {
                EditorGUILayout.LabelField("  (none found)");
                return;
            }

            for (int i = 0; i < _cachedEventAssets.Length; i++)
            {
                var evt = _cachedEventAssets[i];
                if (evt == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  {evt.name}", GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField($"[{evt.Bus}]", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{evt.Clips?.Length ?? 0} clips", GUILayout.Width(60));
                if (GUILayout.Button("Select", GUILayout.Width(55)))
                    _selectedEvent = evt;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RefreshAssetCacheIfNeeded()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!_assetCacheDirty && _cachedEventAssets != null && (now - _lastCacheTime) < kCacheStaleSeconds)
                return;

            var guids = AssetDatabase.FindAssets("t:AudioEventSO");
            _cachedEventAssets = new AudioEventSO[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _cachedEventAssets[i] = AssetDatabase.LoadAssetAtPath<AudioEventSO>(path);
            }

            _assetCacheDirty = false;
            _lastCacheTime = now;
        }

        // ---- Music Preview ----

        private void DrawMusicPreview()
        {
            EditorGUILayout.LabelField("Music State Machine Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedMachine = (MusicStateMachineSO)EditorGUILayout.ObjectField(
                "State Machine", _selectedMachine, typeof(MusicStateMachineSO), false);

            if (_selectedMachine == null)
            {
                EditorGUILayout.HelpBox("Assign a MusicStateMachineSO to preview.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Default State", _selectedMachine.DefaultState);
            EditorGUILayout.LabelField("Crossfade Duration", $"{_selectedMachine.GlobalCrossfadeDuration:F2}s");

            EditorGUILayout.Space(8);

            _musicScroll = EditorGUILayout.BeginScrollView(_musicScroll, GUILayout.Height(300));

            for (int s = 0; s < _selectedMachine.States.Length; s++)
            {
                var state = _selectedMachine.States[s];
                bool isDefault = state.StateId == _selectedMachine.DefaultState;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(
                    isDefault ? $"{state.StateId} (DEFAULT)" : state.StateId,
                    EditorStyles.boldLabel);

                EditorGUILayout.LabelField("Mode", state.Mode.ToString());
                EditorGUILayout.LabelField("Volume", state.Volume.ToString("F2"));
                EditorGUILayout.LabelField("Tracks", (state.Tracks?.Length ?? 0).ToString());

                if (state.Tracks != null)
                {
                    for (int t = 0; t < state.Tracks.Length; t++)
                    {
                        string trackName = state.Tracks[t] != null ? state.Tracks[t].name : "(null)";
                        EditorGUILayout.LabelField($"  [{t}] {trackName}");
                    }
                }

                if (state.IntensityLayers != null && state.IntensityLayers.Length > 0)
                {
                    EditorGUILayout.LabelField("Intensity Layers", EditorStyles.miniBoldLabel);
                    for (int l = 0; l < state.IntensityLayers.Length; l++)
                    {
                        var layer = state.IntensityLayers[l];
                        EditorGUILayout.LabelField(
                            $"  {layer.LayerName}: threshold={layer.ActivateThreshold:F2}, fade={layer.FadeTime:F2}s");
                    }
                }

                if (state.Transitions != null && state.Transitions.Length > 0)
                {
                    EditorGUILayout.LabelField("Transitions", EditorStyles.miniBoldLabel);
                    for (int tr = 0; tr < state.Transitions.Length; tr++)
                    {
                        var trans = state.Transitions[tr];
                        EditorGUILayout.LabelField(
                            $"  on \"{trans.TriggerEvent}\" -> {trans.TargetStateId}");
                    }
                }

                if (Application.isPlaying && AudioEventService.Instance?.Music != null)
                {
                    bool isCurrent = AudioEventService.Instance.Music.CurrentStateId == state.StateId;
                    EditorGUI.BeginDisabledGroup(isCurrent);
                    if (GUILayout.Button(isCurrent ? "Active" : "Transition To"))
                        AudioEventService.Instance.Music.SetState(state.StateId);
                    EditorGUI.EndDisabledGroup();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            if (Application.isPlaying && AudioEventService.Instance?.Music != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Current State",
                    AudioEventService.Instance.Music.CurrentStateId ?? "(none)");

                float intensity = AudioEventService.Instance.Music.CurrentIntensity;
                float newIntensity = EditorGUILayout.Slider("Intensity", intensity, 0f, 1f);
                if (!Mathf.Approximately(newIntensity, intensity))
                    AudioEventService.Instance.Music.SetIntensity(newIntensity);
            }
        }

        // ---- Ambient Preview ----

        private void DrawAmbientPreview()
        {
            EditorGUILayout.LabelField("Ambient Soundscape Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedSoundscape = (AmbientSoundscapeSO)EditorGUILayout.ObjectField(
                "Soundscape", _selectedSoundscape, typeof(AmbientSoundscapeSO), false);

            if (_selectedSoundscape == null)
            {
                EditorGUILayout.HelpBox("Assign an AmbientSoundscapeSO to inspect.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("ID", _selectedSoundscape.SoundscapeId);
            EditorGUILayout.LabelField("Crossfade", $"{_selectedSoundscape.CrossfadeDuration:F2}s");
            EditorGUILayout.LabelField("Priority", _selectedSoundscape.Priority.ToString());

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);

            if (_selectedSoundscape.Layers == null || _selectedSoundscape.Layers.Length == 0)
            {
                EditorGUILayout.LabelField("  (no layers)");
                return;
            }

            for (int i = 0; i < _selectedSoundscape.Layers.Length; i++)
            {
                var layer = _selectedSoundscape.Layers[i];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(layer.LayerName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Volume", layer.Volume.ToString("F2"));
                EditorGUILayout.LabelField("3D", layer.Is3D ? "Yes" : "No");
                EditorGUILayout.LabelField("Clips", (layer.Clips?.Length ?? 0).ToString());
                EditorGUILayout.LabelField("Variance", layer.VolumeVariance.ToString("F3"));

                if (layer.Clips != null)
                {
                    for (int c = 0; c < layer.Clips.Length; c++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        string clipName = layer.Clips[c] != null ? layer.Clips[c].name : "(null)";
                        EditorGUILayout.LabelField($"  [{c}] {clipName}", GUILayout.ExpandWidth(true));

                        EditorGUI.BeginDisabledGroup(layer.Clips[c] == null);
                        if (GUILayout.Button("Play", GUILayout.Width(50)))
                            PreviewClip(layer.Clips[c]);
                        EditorGUI.EndDisabledGroup();

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Stop Preview"))
                StopPreview();
        }

        // ---- Monitor ----

        private void DrawMonitor()
        {
            EditorGUILayout.LabelField("Audio Event Monitor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to monitor active audio events.", MessageType.Info);
                return;
            }

            if (AudioEventService.Instance == null)
            {
                EditorGUILayout.HelpBox("AudioEventService not found in scene.", MessageType.Warning);
                return;
            }

            var player = AudioEventService.Instance.Player;
            EditorGUILayout.LabelField("Active Instances", player.ActiveCount.ToString());

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Audio Telemetry", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Throttled Events", AudioTelemetry.ThrottledEventsThisSession.ToString());
            EditorGUILayout.LabelField("Playback Failures", AudioTelemetry.PlaybackFailuresThisSession.ToString());

            if (AudioEventService.Instance.Music != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Music", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("State", AudioEventService.Instance.Music.CurrentStateId ?? "(none)");
                EditorGUILayout.LabelField("Intensity", AudioEventService.Instance.Music.CurrentIntensity.ToString("F2"));
                EditorGUILayout.LabelField("Playing", AudioEventService.Instance.Music.IsPlaying.ToString());
            }

            if (AudioEventService.Instance.Ambient != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Ambient", EditorStyles.boldLabel);
                var activeSoundscape = AudioEventService.Instance.Ambient.ActiveSoundscape;
                EditorGUILayout.LabelField("Soundscape",
                    activeSoundscape != null ? activeSoundscape.SoundscapeId : "(none)");
                EditorGUILayout.LabelField("Time of Day",
                    AudioEventService.Instance.Ambient.TimeOfDay.ToString("F1"));
            }

            if (AudioSourcePool.Instance != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Pool", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Active Sources",
                    AudioSourcePool.Instance.ActiveCount.ToString());
                EditorGUILayout.LabelField("Peak",
                    AudioSourcePool.Instance.PeakCount.ToString());
                EditorGUILayout.LabelField("Evictions This Frame",
                    AudioSourcePool.Instance.EvictionsThisFrame.ToString());
            }
        }

        // ---- Preview Helpers ----

        private void PreviewClip(AudioClip clip)
        {
            if (clip == null) return;

            StopPreview();

            if (_previewSource == null)
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags(
                    "AudioEventPreview", HideFlags.HideAndDontSave);
                _previewSource = go.AddComponent<AudioSource>();
                _previewSource.spatialBlend = 0f;
                _previewSource.playOnAwake = false;
            }

            _previewSource.clip = clip;
            _previewSource.volume = 1f;
            _previewSource.pitch = 1f;
            _previewSource.Play();
        }

        private void StopPreview()
        {
            if (_previewSource != null)
            {
                _previewSource.Stop();
                Object.DestroyImmediate(_previewSource.gameObject);
                _previewSource = null;
            }

            if (_ambientPreviewSource != null)
            {
                _ambientPreviewSource.Stop();
                Object.DestroyImmediate(_ambientPreviewSource.gameObject);
                _ambientPreviewSource = null;
            }
        }
    }
}
