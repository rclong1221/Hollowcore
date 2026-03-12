using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 AW-06: Audio Preview module.
    /// In-editor playback with 3D positioning simulation.
    /// </summary>
    public class AudioPreviewModule : IAudioModule
    {
        private Vector2 _scrollPosition;
        
        // Current clip
        private AudioClip _currentClip;
        private bool _isPlaying = false;
        private float _playbackPosition = 0f;
        
        // Playback settings
        private float _volume = 1f;
        private float _pitch = 1f;
        private bool _loop = false;
        
        // 3D simulation
        private bool _enable3D = false;
        private Vector2 _listenerPosition = Vector2.zero;
        private Vector2 _sourcePosition = new Vector2(5, 0);
        private float _simulatedDistance = 5f;
        private float _minDistance = 1f;
        private float _maxDistance = 20f;
        
        // Waveform
        private Texture2D _waveformTexture;
        
        // Clip list for quick access
        private List<AudioClip> _recentClips = new List<AudioClip>();
        private const int MAX_RECENT = 10;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Audio Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Preview audio clips in the editor with volume, pitch, and 3D positioning simulation.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawClipSelection();
            EditorGUILayout.Space(10);
            DrawWaveform();
            EditorGUILayout.Space(10);
            DrawPlaybackControls();
            EditorGUILayout.Space(10);
            Draw3DSimulation();
            EditorGUILayout.Space(10);
            DrawClipInfo();
            EditorGUILayout.Space(10);
            DrawRecentClips();

            EditorGUILayout.EndScrollView();
        }

        private void DrawClipSelection()
        {
            EditorGUILayout.LabelField("Clip Selection", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _currentClip = (AudioClip)EditorGUILayout.ObjectField(
                "Audio Clip", _currentClip, typeof(AudioClip), false);
            
            if (EditorGUI.EndChangeCheck())
            {
                StopPlayback();
                
                if (_currentClip != null && !_recentClips.Contains(_currentClip))
                {
                    _recentClips.Insert(0, _currentClip);
                    if (_recentClips.Count > MAX_RECENT)
                    {
                        _recentClips.RemoveAt(_recentClips.Count - 1);
                    }
                }
            }

            // Drag and drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, _currentClip == null ? "Drag & Drop Audio Clip" : _currentClip.name, 
                EditorStyles.helpBox);
            HandleDragDrop(dropArea);

            EditorGUILayout.EndVertical();
        }

        private void HandleDragDrop(Rect dropArea)
        {
            Event evt = Event.current;
            
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition)) return;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is AudioClip clip)
                        {
                            _currentClip = clip;
                            StopPlayback();
                            break;
                        }
                    }
                }
                
                evt.Use();
            }
        }

        private void DrawWaveform()
        {
            EditorGUILayout.LabelField("Waveform", EditorStyles.boldLabel);
            
            Rect waveformRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(80), GUILayout.ExpandWidth(true));
            
            // Background
            EditorGUI.DrawRect(waveformRect, new Color(0.15f, 0.15f, 0.15f));

            if (_currentClip != null)
            {
                // Draw waveform
                DrawWaveformData(waveformRect);
                
                // Playback position
                if (_isPlaying || _playbackPosition > 0)
                {
                    float posX = waveformRect.x + (_playbackPosition / _currentClip.length) * waveformRect.width;
                    EditorGUI.DrawRect(new Rect(posX - 1, waveformRect.y, 2, waveformRect.height), Color.yellow);
                }
                
                // Handle click to seek
                if (Event.current.type == EventType.MouseDown && waveformRect.Contains(Event.current.mousePosition))
                {
                    float clickPos = (Event.current.mousePosition.x - waveformRect.x) / waveformRect.width;
                    _playbackPosition = clickPos * _currentClip.length;
                    Event.current.Use();
                }
            }
            else
            {
                GUI.Label(waveformRect, "No clip loaded", 
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
            }
        }

        private void DrawWaveformData(Rect rect)
        {
            if (_currentClip == null) return;

            // Get waveform data
            int sampleCount = Mathf.Min(_currentClip.samples, (int)rect.width * 10);
            float[] samples = new float[sampleCount];
            
            try
            {
                _currentClip.GetData(samples, 0);
            }
            catch
            {
                GUI.Label(rect, "Cannot read waveform (compressed audio)", 
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            // Draw waveform
            Handles.color = new Color(0.4f, 0.8f, 0.4f, 0.8f);
            
            float centerY = rect.center.y;
            float heightScale = rect.height * 0.45f;
            int samplesPerPixel = Mathf.Max(1, sampleCount / (int)rect.width);
            
            for (int x = 0; x < rect.width; x++)
            {
                int sampleIndex = Mathf.Min((int)(x * samplesPerPixel), samples.Length - 1);
                
                float min = 0, max = 0;
                for (int i = 0; i < samplesPerPixel && sampleIndex + i < samples.Length; i++)
                {
                    float sample = samples[sampleIndex + i];
                    min = Mathf.Min(min, sample);
                    max = Mathf.Max(max, sample);
                }
                
                float y1 = centerY - max * heightScale;
                float y2 = centerY - min * heightScale;
                
                Handles.DrawLine(
                    new Vector3(rect.x + x, y1, 0),
                    new Vector3(rect.x + x, y2, 0)
                );
            }

            // Center line
            Handles.color = new Color(1, 1, 1, 0.2f);
            Handles.DrawLine(
                new Vector3(rect.x, centerY, 0),
                new Vector3(rect.xMax, centerY, 0)
            );
        }

        private void DrawPlaybackControls()
        {
            EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginDisabledGroup(_currentClip == null);

            // Play/Pause button
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _isPlaying ? Color.yellow : Color.green;
            
            if (GUILayout.Button(_isPlaying ? "⏸ Pause" : "▶ Play", GUILayout.Height(30), GUILayout.Width(80)))
            {
                TogglePlayback();
            }
            
            GUI.backgroundColor = prevColor;

            // Stop button
            if (GUILayout.Button("⏹ Stop", GUILayout.Height(30), GUILayout.Width(80)))
            {
                StopPlayback();
            }

            // Loop toggle
            _loop = GUILayout.Toggle(_loop, "Loop", GUILayout.Width(50));

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Volume and pitch
            _volume = EditorGUILayout.Slider("Volume", _volume, 0f, 1f);
            _pitch = EditorGUILayout.Slider("Pitch", _pitch, 0.5f, 2f);

            // Time display
            if (_currentClip != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{FormatTime(_playbackPosition)} / {FormatTime(_currentClip.length)}");
                
                EditorGUI.BeginChangeCheck();
                _playbackPosition = EditorGUILayout.Slider(_playbackPosition, 0f, _currentClip.length);
                if (EditorGUI.EndChangeCheck() && _isPlaying)
                {
                    // Would seek to position
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void Draw3DSimulation()
        {
            EditorGUILayout.LabelField("3D Position Simulation", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enable3D = EditorGUILayout.Toggle("Enable 3D Simulation", _enable3D);

            if (_enable3D)
            {
                EditorGUILayout.Space(5);
                
                // 2D position display
                Rect positionRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(150), GUILayout.ExpandWidth(true));
                Draw3DPositionView(positionRect);

                EditorGUILayout.Space(5);

                // Distance settings
                EditorGUILayout.LabelField($"Simulated Distance: {_simulatedDistance:F1}m");
                
                _minDistance = EditorGUILayout.FloatField("Min Distance", _minDistance);
                _maxDistance = EditorGUILayout.FloatField("Max Distance", _maxDistance);

                // Calculate simulated volume
                float attenuatedVolume = CalculateAttenuatedVolume();
                EditorGUILayout.LabelField($"Attenuated Volume: {attenuatedVolume * 100:F0}%");
            }

            EditorGUILayout.EndVertical();
        }

        private void Draw3DPositionView(Rect rect)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            // Grid
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            for (int i = 1; i < 4; i++)
            {
                float x = rect.x + rect.width * (i / 4f);
                float y = rect.y + rect.height * (i / 4f);
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }

            Vector2 center = rect.center;
            float scale = Mathf.Min(rect.width, rect.height) * 0.4f / _maxDistance;

            // Draw distance rings
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            Handles.DrawWireDisc(center, Vector3.forward, _minDistance * scale);
            Handles.DrawWireDisc(center, Vector3.forward, _maxDistance * scale);

            // Draw listener (center)
            Vector2 listenerScreenPos = center + _listenerPosition * scale;
            EditorGUI.DrawRect(new Rect(listenerScreenPos.x - 5, listenerScreenPos.y - 5, 10, 10), Color.blue);
            GUI.Label(new Rect(listenerScreenPos.x - 20, listenerScreenPos.y + 8, 50, 16), "Listener", EditorStyles.miniLabel);

            // Draw source (draggable)
            Vector2 sourceScreenPos = center + _sourcePosition * scale;
            EditorGUI.DrawRect(new Rect(sourceScreenPos.x - 5, sourceScreenPos.y - 5, 10, 10), Color.green);
            GUI.Label(new Rect(sourceScreenPos.x - 15, sourceScreenPos.y + 8, 40, 16), "Source", EditorStyles.miniLabel);

            // Handle dragging
            if (Event.current.type == EventType.MouseDrag && rect.Contains(Event.current.mousePosition))
            {
                Vector2 mousePos = Event.current.mousePosition;
                _sourcePosition = (mousePos - center) / scale;
                _simulatedDistance = Vector2.Distance(_listenerPosition, _sourcePosition);
                Event.current.Use();
            }

            // Update distance
            _simulatedDistance = Vector2.Distance(_listenerPosition, _sourcePosition);
        }

        private float CalculateAttenuatedVolume()
        {
            if (_simulatedDistance <= _minDistance) return 1f;
            if (_simulatedDistance >= _maxDistance) return 0f;
            
            float normalized = (_simulatedDistance - _minDistance) / (_maxDistance - _minDistance);
            return 1f / (1f + normalized * 9f); // Logarithmic falloff
        }

        private void DrawClipInfo()
        {
            if (_currentClip == null) return;

            EditorGUILayout.LabelField("Clip Information", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Name: {_currentClip.name}");
            EditorGUILayout.LabelField($"Duration: {FormatTime(_currentClip.length)}");
            EditorGUILayout.LabelField($"Channels: {_currentClip.channels}");
            EditorGUILayout.LabelField($"Frequency: {_currentClip.frequency} Hz");
            EditorGUILayout.LabelField($"Samples: {_currentClip.samples:N0}");
            EditorGUILayout.LabelField($"Load Type: {_currentClip.loadType}");

            // Memory estimate
            float memorySizeMB = (_currentClip.samples * _currentClip.channels * 2) / (1024f * 1024f);
            EditorGUILayout.LabelField($"Est. Memory: {memorySizeMB:F2} MB (uncompressed)");

            EditorGUILayout.EndVertical();
        }

        private void DrawRecentClips()
        {
            if (_recentClips.Count == 0) return;

            EditorGUILayout.LabelField("Recent Clips", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _recentClips.Count; i++)
            {
                if (_recentClips[i] == null)
                {
                    _recentClips.RemoveAt(i);
                    i--;
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button(_recentClips[i].name, EditorStyles.miniButton))
                {
                    _currentClip = _recentClips[i];
                    StopPlayback();
                }
                
                EditorGUILayout.LabelField($"{_recentClips[i].length:F2}s", 
                    EditorStyles.miniLabel, GUILayout.Width(50));
                
                if (GUILayout.Button("▶", GUILayout.Width(25)))
                {
                    PlayClip(_recentClips[i]);
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Clear Recent"))
            {
                _recentClips.Clear();
            }

            EditorGUILayout.EndVertical();
        }

        private void TogglePlayback()
        {
            if (_isPlaying)
            {
                StopPlayback();
            }
            else
            {
                PlayCurrentClip();
            }
        }

        private void PlayCurrentClip()
        {
            if (_currentClip == null) return;
            PlayClip(_currentClip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;

            StopPlayback();
            
            var assembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = assembly.GetType("UnityEditor.AudioUtil");
            
            var playMethod = audioUtilClass.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            
            playMethod?.Invoke(null, new object[] { clip, 0, _loop });
            
            _isPlaying = true;
            _currentClip = clip;
        }

        private void StopPlayback()
        {
            var assembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = assembly.GetType("UnityEditor.AudioUtil");
            
            var stopMethod = audioUtilClass.GetMethod("StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public);
            
            stopMethod?.Invoke(null, null);
            
            _isPlaying = false;
            _playbackPosition = 0f;
        }

        private string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            int ms = (int)((seconds % 1) * 100);
            return $"{minutes}:{secs:D2}.{ms:D2}";
        }
    }
}
