#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Music.Editor
{
    /// <summary>
    /// EPIC 17.5: Play all 4 stems simultaneously with individual volume sliders.
    /// Intensity slider simulates combat intensity to preview stem activation.
    /// </summary>
    public class StemPreviewerModule : IMusicWorkstationModule
    {
        public string ModuleName => "Stem Previewer";

        private MusicTrackSO _previewTrack;
        private float _simulatedIntensity;
        private float _baseVol = 1f, _percVol = 1f, _melVol = 1f, _intVol = 1f;
        private float _masterVol = 1f;
        private bool _isPlaying;

        // Preview AudioSources (created at runtime in editor)
        private GameObject _previewGo;
        private AudioSource _baseSrc, _percSrc, _melSrc, _intSrc;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Stem Previewer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _previewTrack = EditorGUILayout.ObjectField("Track", _previewTrack, typeof(MusicTrackSO), false) as MusicTrackSO;

            if (_previewTrack == null)
            {
                EditorGUILayout.HelpBox("Assign a MusicTrackSO to preview its stems.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"{_previewTrack.TrackName} | {_previewTrack.BPM:F0} BPM", EditorStyles.boldLabel);

            // Intensity slider
            EditorGUILayout.Space(8);
            _simulatedIntensity = EditorGUILayout.Slider("Combat Intensity", _simulatedIntensity, 0f, 1f);

            // Auto-compute stem volumes from intensity
            var t = _previewTrack.CombatIntensityThresholds;
            _baseVol = 1f;
            _percVol = _simulatedIntensity >= t.x ? 1f : 0f;
            _melVol = _simulatedIntensity >= t.y ? 1f : 0f;
            _intVol = _simulatedIntensity >= t.z ? 1f : 0f;

            // Individual stem controls
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Stem Volumes (auto from intensity)", EditorStyles.miniLabel);
            DrawStemBar("Base", _baseVol, _previewTrack.BaseStem);
            DrawStemBar("Percussion", _percVol, _previewTrack.PercussionStem);
            DrawStemBar("Melody", _melVol, _previewTrack.MelodyStem);
            DrawStemBar("Intensity", _intVol, _previewTrack.IntensityStem);

            _masterVol = EditorGUILayout.Slider("Master Volume", _masterVol, 0f, 1f);

            // Transport controls
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isPlaying ? "Stop" : "Play", GUILayout.Height(30)))
            {
                if (_isPlaying)
                    StopPreview();
                else
                    StartPreview();
            }
            EditorGUILayout.EndHorizontal();

            // Update live volumes
            if (_isPlaying)
                UpdatePreviewVolumes();
        }

        private void DrawStemBar(string label, float vol, AudioClip clip)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(80));
            var rect = GUILayoutUtility.GetRect(200, 16);
            EditorGUI.ProgressBar(rect, vol, clip != null ? clip.name : "(none)");
            EditorGUILayout.EndHorizontal();
        }

        private void StartPreview()
        {
            StopPreview();
            if (_previewTrack == null) return;

            _previewGo = new GameObject("MusicStemPreview");
            _previewGo.hideFlags = HideFlags.HideAndDontSave;

            _baseSrc = CreateSource(_previewTrack.BaseStem);
            _percSrc = CreateSource(_previewTrack.PercussionStem);
            _melSrc = CreateSource(_previewTrack.MelodyStem);
            _intSrc = CreateSource(_previewTrack.IntensityStem);

            // Sync play
            if (_baseSrc != null) _baseSrc.Play();
            if (_percSrc != null) _percSrc.Play();
            if (_melSrc != null) _melSrc.Play();
            if (_intSrc != null) _intSrc.Play();

            _isPlaying = true;
        }

        private AudioSource CreateSource(AudioClip clip)
        {
            if (clip == null || _previewGo == null) return null;
            var src = _previewGo.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            return src;
        }

        private void StopPreview()
        {
            _isPlaying = false;
            if (_previewGo != null)
                Object.DestroyImmediate(_previewGo);
            _baseSrc = _percSrc = _melSrc = _intSrc = null;
        }

        private void UpdatePreviewVolumes()
        {
            float master = _masterVol * (_previewTrack != null ? _previewTrack.BaseVolume : 1f);
            if (_baseSrc != null) _baseSrc.volume = _baseVol * master;
            if (_percSrc != null) _percSrc.volume = _percVol * master;
            if (_melSrc != null) _melSrc.volume = _melVol * master;
            if (_intSrc != null) _intSrc.volume = _intVol * master;
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
