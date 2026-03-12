using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 AW-03: Randomization module.
    /// Pitch variance, volume variation, clip alternation rules.
    /// </summary>
    public class RandomizationModule : IAudioModule
    {
        private Vector2 _scrollPosition;
        
        // Pitch settings
        private bool _enablePitchVariation = true;
        private float _basePitch = 1f;
        private float _pitchMin = 0.95f;
        private float _pitchMax = 1.05f;
        private AnimationCurve _pitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        // Volume settings
        private bool _enableVolumeVariation = true;
        private float _baseVolume = 1f;
        private float _volumeMin = 0.9f;
        private float _volumeMax = 1f;
        
        // Clip alternation
        private ClipSelectionMode _selectionMode = ClipSelectionMode.Random;
        private bool _preventRepeat = true;
        private int _minClipsBeforeRepeat = 2;
        
        // Test clips
        private List<AudioClip> _testClips = new List<AudioClip>();
        private int _lastPlayedIndex = -1;
        private List<int> _playHistory = new List<int>();

        private enum ClipSelectionMode
        {
            Random,
            Sequential,
            WeightedRandom,
            RoundRobin
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Audio Randomization", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure pitch variance, volume variation, and clip alternation rules for natural-sounding audio.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawPitchSettings();
            EditorGUILayout.Space(10);
            DrawVolumeSettings();
            EditorGUILayout.Space(10);
            DrawClipAlternation();
            EditorGUILayout.Space(10);
            DrawPreview();
            EditorGUILayout.Space(10);
            DrawPresets();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPitchSettings()
        {
            EditorGUILayout.LabelField("Pitch Variation", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enablePitchVariation = EditorGUILayout.Toggle("Enable Pitch Variation", _enablePitchVariation);

            if (_enablePitchVariation)
            {
                EditorGUI.indentLevel++;
                
                _basePitch = EditorGUILayout.Slider("Base Pitch", _basePitch, 0.5f, 2f);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Range:", GUILayout.Width(50));
                _pitchMin = EditorGUILayout.FloatField(_pitchMin, GUILayout.Width(50));
                EditorGUILayout.MinMaxSlider(ref _pitchMin, ref _pitchMax, 0.5f, 2f);
                _pitchMax = EditorGUILayout.FloatField(_pitchMax, GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                _pitchCurve = EditorGUILayout.CurveField("Distribution Curve", _pitchCurve);
                
                // Preview current range
                EditorGUILayout.HelpBox(
                    $"Pitch will vary between {_pitchMin:F2} and {_pitchMax:F2} (±{((_pitchMax - _pitchMin) / 2 * 100):F0}%)",
                    MessageType.None);
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawVolumeSettings()
        {
            EditorGUILayout.LabelField("Volume Variation", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableVolumeVariation = EditorGUILayout.Toggle("Enable Volume Variation", _enableVolumeVariation);

            if (_enableVolumeVariation)
            {
                EditorGUI.indentLevel++;
                
                _baseVolume = EditorGUILayout.Slider("Base Volume", _baseVolume, 0f, 1f);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Range:", GUILayout.Width(50));
                _volumeMin = EditorGUILayout.FloatField(_volumeMin, GUILayout.Width(50));
                EditorGUILayout.MinMaxSlider(ref _volumeMin, ref _volumeMax, 0f, 1f);
                _volumeMax = EditorGUILayout.FloatField(_volumeMax, GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
                
                // Volume bar preview
                Rect volumeRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(20), GUILayout.ExpandWidth(true));
                DrawVolumeBar(volumeRect);
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawVolumeBar(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            float minX = rect.x + _volumeMin * rect.width;
            float maxX = rect.x + _volumeMax * rect.width;
            
            EditorGUI.DrawRect(new Rect(minX, rect.y, maxX - minX, rect.height), 
                new Color(0.3f, 0.7f, 0.3f));
            
            // Base volume indicator
            float baseX = rect.x + _baseVolume * rect.width;
            EditorGUI.DrawRect(new Rect(baseX - 1, rect.y, 2, rect.height), Color.white);
        }

        private void DrawClipAlternation()
        {
            EditorGUILayout.LabelField("Clip Alternation", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _selectionMode = (ClipSelectionMode)EditorGUILayout.EnumPopup("Selection Mode", _selectionMode);
            
            EditorGUILayout.Space(5);
            
            switch (_selectionMode)
            {
                case ClipSelectionMode.Random:
                    EditorGUILayout.LabelField("Clips are selected randomly each time.", EditorStyles.wordWrappedMiniLabel);
                    _preventRepeat = EditorGUILayout.Toggle("Prevent Immediate Repeat", _preventRepeat);
                    break;
                    
                case ClipSelectionMode.Sequential:
                    EditorGUILayout.LabelField("Clips play in order, looping back to start.", EditorStyles.wordWrappedMiniLabel);
                    break;
                    
                case ClipSelectionMode.WeightedRandom:
                    EditorGUILayout.LabelField("Clips are selected randomly with weights.", EditorStyles.wordWrappedMiniLabel);
                    _preventRepeat = EditorGUILayout.Toggle("Prevent Immediate Repeat", _preventRepeat);
                    break;
                    
                case ClipSelectionMode.RoundRobin:
                    EditorGUILayout.LabelField("Each clip must play before any repeats.", EditorStyles.wordWrappedMiniLabel);
                    _minClipsBeforeRepeat = EditorGUILayout.IntSlider("Min Before Repeat", _minClipsBeforeRepeat, 1, 10);
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Test Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Drop test clips to preview randomization:", EditorStyles.miniLabel);

            for (int i = 0; i < _testClips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (i == _lastPlayedIndex)
                {
                    EditorGUILayout.LabelField("▶", GUILayout.Width(15));
                }
                else
                {
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                }
                
                _testClips[i] = (AudioClip)EditorGUILayout.ObjectField(
                    _testClips[i], typeof(AudioClip), false);
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _testClips.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Test Clip"))
            {
                _testClips.Add(null);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Play Random", GUILayout.Height(30)))
            {
                PlayRandomClip();
            }
            
            if (GUILayout.Button("Play Sequence", GUILayout.Height(30)))
            {
                PlaySequentialClip();
            }
            
            if (GUILayout.Button("Stop", GUILayout.Height(30)))
            {
                StopPreview();
            }
            
            EditorGUILayout.EndHorizontal();

            // Show generated values
            if (_lastPlayedIndex >= 0)
            {
                float testPitch = Random.Range(_pitchMin, _pitchMax);
                float testVolume = Random.Range(_volumeMin, _volumeMax);
                EditorGUILayout.LabelField($"Last: Pitch={testPitch:F2}, Volume={testVolume:F2}", 
                    EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresets()
        {
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Subtle"))
            {
                ApplyPreset(0.98f, 1.02f, 0.95f, 1f);
            }
            if (GUILayout.Button("Moderate"))
            {
                ApplyPreset(0.95f, 1.05f, 0.9f, 1f);
            }
            if (GUILayout.Button("Dramatic"))
            {
                ApplyPreset(0.85f, 1.15f, 0.8f, 1f);
            }
            if (GUILayout.Button("Extreme"))
            {
                ApplyPreset(0.7f, 1.3f, 0.7f, 1f);
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Apply to Selection", GUILayout.Height(30)))
            {
                ApplyToSelection();
            }
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Copy Settings", GUILayout.Height(30)))
            {
                CopySettings();
            }
            
            if (GUILayout.Button("Paste Settings", GUILayout.Height(30)))
            {
                PasteSettings();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(float pitchMin, float pitchMax, float volMin, float volMax)
        {
            _pitchMin = pitchMin;
            _pitchMax = pitchMax;
            _volumeMin = volMin;
            _volumeMax = volMax;
            Debug.Log($"[Randomization] Applied preset: Pitch [{pitchMin:F2}-{pitchMax:F2}], Volume [{volMin:F2}-{volMax:F2}]");
        }

        private void PlayRandomClip()
        {
            if (_testClips.Count == 0) return;
            
            var validClips = new List<int>();
            for (int i = 0; i < _testClips.Count; i++)
            {
                if (_testClips[i] != null && (!_preventRepeat || i != _lastPlayedIndex))
                {
                    validClips.Add(i);
                }
            }
            
            if (validClips.Count > 0)
            {
                _lastPlayedIndex = validClips[Random.Range(0, validClips.Count)];
                PlayClipWithRandomization(_testClips[_lastPlayedIndex]);
            }
        }

        private void PlaySequentialClip()
        {
            if (_testClips.Count == 0) return;
            
            _lastPlayedIndex = (_lastPlayedIndex + 1) % _testClips.Count;
            
            // Skip null clips
            int attempts = 0;
            while (_testClips[_lastPlayedIndex] == null && attempts < _testClips.Count)
            {
                _lastPlayedIndex = (_lastPlayedIndex + 1) % _testClips.Count;
                attempts++;
            }
            
            if (_testClips[_lastPlayedIndex] != null)
            {
                PlayClipWithRandomization(_testClips[_lastPlayedIndex]);
            }
        }

        private void PlayClipWithRandomization(AudioClip clip)
        {
            if (clip == null) return;
            
            // Note: Editor preview doesn't support pitch/volume modification
            // This is for demonstration - runtime would apply these values
            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtilClass.GetMethod("PlayPreviewClip",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            
            method?.Invoke(null, new object[] { clip, 0, false });
        }

        private void StopPreview()
        {
            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtilClass.GetMethod("StopAllPreviewClips",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            
            method?.Invoke(null, null);
        }

        private void ApplyToSelection()
        {
            Debug.Log("[Randomization] Apply to selection pending");
        }

        private void CopySettings()
        {
            EditorGUIUtility.systemCopyBuffer = $"Pitch:{_pitchMin},{_pitchMax};Volume:{_volumeMin},{_volumeMax}";
            Debug.Log("[Randomization] Settings copied to clipboard");
        }

        private void PasteSettings()
        {
            Debug.Log("[Randomization] Paste settings pending");
        }
    }
}
