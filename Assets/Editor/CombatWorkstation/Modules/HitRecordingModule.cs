using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DIG.Editor.CombatWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CB-04: Hit Recording module.
    /// Record & replay hit scenarios, predicted vs confirmed comparison.
    /// </summary>
    public class HitRecordingModule : ICombatModule
    {
        private Vector2 _scrollPosition;
        
        // Recording state
        private bool _isRecording = false;
        private float _recordingStartTime = 0f;
        private List<HitRecord> _currentRecording = new List<HitRecord>();
        private List<HitRecordingSession> _savedSessions = new List<HitRecordingSession>();
        private int _selectedSessionIndex = -1;
        
        // Playback state
        private bool _isPlaying = false;
        private float _playbackTime = 0f;
        
        // Analysis
        private int _totalPredictedHits = 0;
        private int _totalConfirmedHits = 0;
        private int _mispredictions = 0;
        private float _averageLatency = 0f;
        
        // Comparison view
        private bool _showComparison = false;

        [System.Serializable]
        private class HitRecord
        {
            public float Timestamp;
            public Vector3 ShooterPosition;
            public Vector3 ShooterAimDirection;
            public Vector3 TargetPosition;
            public Vector3 HitPoint;
            public bool WasPredicted;
            public bool WasConfirmed;
            public float PredictionTime;
            public float ConfirmationTime;
            public float Latency;
            public string WeaponId;
            public string TargetId;
            public float Damage;
            public string HitRegion;
        }

        [System.Serializable]
        private class HitRecordingSession
        {
            public string Name;
            public float Duration;
            public int HitCount;
            public float Accuracy;
            public List<HitRecord> Records = new List<HitRecord>();
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Hit Recording", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Record and replay hit scenarios. Compare predicted vs server-confirmed hits.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawRecordingControls();
            EditorGUILayout.Space(10);
            DrawSessionList();
            EditorGUILayout.Space(10);
            
            if (_selectedSessionIndex >= 0 && _selectedSessionIndex < _savedSessions.Count)
            {
                DrawSessionDetails(_savedSessions[_selectedSessionIndex]);
                EditorGUILayout.Space(10);
                DrawHitTimeline(_savedSessions[_selectedSessionIndex]);
            }
            
            EditorGUILayout.Space(10);
            DrawAnalysis();

            EditorGUILayout.EndScrollView();
        }

        private void DrawRecordingControls()
        {
            EditorGUILayout.LabelField("Recording", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            // Record button
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _isRecording ? Color.red : Color.green;
            
            string recordLabel = _isRecording 
                ? $"⏹ Stop ({_currentRecording.Count} hits)" 
                : "⏺ Start Recording";
            
            if (GUILayout.Button(recordLabel, GUILayout.Height(30)))
            {
                ToggleRecording();
            }
            
            GUI.backgroundColor = prevColor;

            // Play button
            EditorGUI.BeginDisabledGroup(_selectedSessionIndex < 0 || _isRecording);
            
            GUI.backgroundColor = _isPlaying ? Color.yellow : Color.white;
            if (GUILayout.Button(_isPlaying ? "⏸ Pause" : "▶ Replay", GUILayout.Height(30)))
            {
                TogglePlayback();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            // Recording info
            if (_isRecording)
            {
                float duration = Time.realtimeSinceStartup - _recordingStartTime;
                EditorGUILayout.LabelField($"Recording: {duration:F1}s | Hits: {_currentRecording.Count}");
                
                Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(10), GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(progressRect, Mathf.PingPong(duration * 0.5f, 1f), "");
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play mode to record live hit data.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSessionList()
        {
            EditorGUILayout.LabelField($"Saved Sessions ({_savedSessions.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _savedSessions.Count; i++)
            {
                var session = _savedSessions[i];
                
                EditorGUILayout.BeginHorizontal();
                
                bool selected = i == _selectedSessionIndex;
                Color prevColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = Color.cyan;
                
                if (GUILayout.Button(session.Name, EditorStyles.miniButton))
                {
                    _selectedSessionIndex = i;
                    AnalyzeSession(session);
                }
                
                GUI.backgroundColor = prevColor;

                EditorGUILayout.LabelField($"{session.HitCount} hits", 
                    EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField($"{session.Duration:F1}s", 
                    EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField($"{session.Accuracy:P0}", 
                    EditorStyles.miniLabel, GUILayout.Width(50));

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _savedSessions.RemoveAt(i);
                    if (_selectedSessionIndex >= _savedSessions.Count)
                        _selectedSessionIndex = _savedSessions.Count - 1;
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (_savedSessions.Count == 0)
            {
                EditorGUILayout.LabelField("No recordings yet. Start recording in Play mode.", 
                    EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import"))
            {
                ImportSession();
            }
            
            EditorGUI.BeginDisabledGroup(_selectedSessionIndex < 0);
            if (GUILayout.Button("Export"))
            {
                ExportSession(_savedSessions[_selectedSessionIndex]);
            }
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("Add Test Session"))
            {
                AddTestSession();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSessionDetails(HitRecordingSession session)
        {
            EditorGUILayout.LabelField("Session Details", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Name: {session.Name}");
            EditorGUILayout.LabelField($"Duration: {session.Duration:F2}s");
            EditorGUILayout.LabelField($"Total Hits: {session.HitCount}");
            EditorGUILayout.LabelField($"Accuracy: {session.Accuracy:P1}");

            EditorGUILayout.Space(5);
            _showComparison = EditorGUILayout.Toggle("Show Predicted vs Confirmed", _showComparison);

            if (_showComparison && session.Records.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Hit Records", EditorStyles.miniLabel);

                int displayCount = Mathf.Min(10, session.Records.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    var record = session.Records[i];
                    EditorGUILayout.BeginHorizontal();
                    
                    Color prevColor = GUI.color;
                    
                    // Predicted
                    GUI.color = record.WasPredicted ? Color.cyan : Color.gray;
                    EditorGUILayout.LabelField(record.WasPredicted ? "✓ Predicted" : "✗ Missed", 
                        GUILayout.Width(80));
                    
                    // Confirmed
                    GUI.color = record.WasConfirmed ? Color.green : Color.red;
                    EditorGUILayout.LabelField(record.WasConfirmed ? "✓ Confirmed" : "✗ Denied", 
                        GUILayout.Width(80));
                    
                    GUI.color = prevColor;
                    
                    // Latency
                    if (record.Latency > 0)
                    {
                        EditorGUILayout.LabelField($"{record.Latency * 1000:F0}ms", GUILayout.Width(50));
                    }
                    
                    EditorGUILayout.LabelField($"{record.Damage:F0} dmg", GUILayout.Width(60));
                    EditorGUILayout.LabelField(record.HitRegion, GUILayout.Width(60));

                    EditorGUILayout.EndHorizontal();
                }

                if (session.Records.Count > displayCount)
                {
                    EditorGUILayout.LabelField($"... and {session.Records.Count - displayCount} more", 
                        EditorStyles.centeredGreyMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHitTimeline(HitRecordingSession session)
        {
            EditorGUILayout.LabelField("Hit Timeline", EditorStyles.boldLabel);
            
            Rect timelineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(80), GUILayout.ExpandWidth(true));
            
            // Background
            EditorGUI.DrawRect(timelineRect, new Color(0.15f, 0.15f, 0.15f));

            if (session.Records.Count == 0 || session.Duration <= 0) return;

            // Draw hits
            foreach (var record in session.Records)
            {
                float normalizedTime = record.Timestamp / session.Duration;
                float x = timelineRect.x + normalizedTime * timelineRect.width;
                
                // Prediction marker (top)
                if (record.WasPredicted)
                {
                    Rect predRect = new Rect(x - 2, timelineRect.y, 4, timelineRect.height * 0.4f);
                    EditorGUI.DrawRect(predRect, Color.cyan);
                }
                
                // Confirmation marker (bottom)
                if (record.WasConfirmed)
                {
                    Rect confRect = new Rect(x - 2, timelineRect.y + timelineRect.height * 0.6f, 
                        4, timelineRect.height * 0.4f);
                    EditorGUI.DrawRect(confRect, Color.green);
                }
                
                // Mismatch indicator
                if (record.WasPredicted != record.WasConfirmed)
                {
                    Rect mismatchRect = new Rect(x - 3, timelineRect.center.y - 3, 6, 6);
                    EditorGUI.DrawRect(mismatchRect, Color.red);
                }
            }

            // Labels
            GUI.Label(new Rect(timelineRect.x + 5, timelineRect.y + 2, 100, 16), 
                "Predicted", EditorStyles.miniLabel);
            GUI.Label(new Rect(timelineRect.x + 5, timelineRect.yMax - 18, 100, 16), 
                "Confirmed", EditorStyles.miniLabel);

            // Playback position
            if (_isPlaying)
            {
                float playX = timelineRect.x + (_playbackTime / session.Duration) * timelineRect.width;
                EditorGUI.DrawRect(new Rect(playX - 1, timelineRect.y, 2, timelineRect.height), Color.white);
            }
        }

        private void DrawAnalysis()
        {
            EditorGUILayout.LabelField("Analysis", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Predicted Hits", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(_totalPredictedHits.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Confirmed Hits", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(_totalConfirmedHits.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Mispredictions", EditorStyles.centeredGreyMiniLabel);
            Color prevColor = GUI.color;
            GUI.color = _mispredictions > 0 ? Color.red : Color.green;
            EditorGUILayout.LabelField(_mispredictions.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            GUI.color = prevColor;
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Avg Latency", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_averageLatency * 1000:F0}ms", 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Accuracy
            if (_totalPredictedHits > 0)
            {
                float accuracy = (float)_totalConfirmedHits / _totalPredictedHits;
                Rect accuracyRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(20), GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(accuracyRect, accuracy, $"Prediction Accuracy: {accuracy:P1}");
            }

            EditorGUILayout.EndVertical();
        }

        private void ToggleRecording()
        {
            if (_isRecording)
            {
                // Stop and save
                StopRecording();
            }
            else
            {
                // Start new recording
                StartRecording();
            }
        }

        private void StartRecording()
        {
            _isRecording = true;
            _recordingStartTime = Time.realtimeSinceStartup;
            _currentRecording.Clear();
            Debug.Log("[HitRecording] Recording started");
        }

        private void StopRecording()
        {
            _isRecording = false;
            float duration = Time.realtimeSinceStartup - _recordingStartTime;

            if (_currentRecording.Count > 0)
            {
                int confirmed = _currentRecording.Count(r => r.WasConfirmed);
                int predicted = _currentRecording.Count(r => r.WasPredicted);
                
                var session = new HitRecordingSession
                {
                    Name = $"Recording_{System.DateTime.Now:HHmmss}",
                    Duration = duration,
                    HitCount = _currentRecording.Count,
                    Accuracy = predicted > 0 ? (float)confirmed / predicted : 0f,
                    Records = new List<HitRecord>(_currentRecording)
                };

                _savedSessions.Add(session);
                _selectedSessionIndex = _savedSessions.Count - 1;
                AnalyzeSession(session);
            }

            _currentRecording.Clear();
            Debug.Log($"[HitRecording] Recording stopped. {_savedSessions.Last().HitCount} hits captured.");
        }

        private void TogglePlayback()
        {
            _isPlaying = !_isPlaying;
            if (_isPlaying)
            {
                _playbackTime = 0f;
            }
        }

        private void AnalyzeSession(HitRecordingSession session)
        {
            _totalPredictedHits = session.Records.Count(r => r.WasPredicted);
            _totalConfirmedHits = session.Records.Count(r => r.WasConfirmed);
            _mispredictions = session.Records.Count(r => r.WasPredicted != r.WasConfirmed);
            
            var latencies = session.Records.Where(r => r.Latency > 0).Select(r => r.Latency).ToList();
            _averageLatency = latencies.Count > 0 ? latencies.Average() : 0f;
        }

        private void AddTestSession()
        {
            var session = new HitRecordingSession
            {
                Name = $"Test_{_savedSessions.Count + 1}",
                Duration = 10f,
                HitCount = 20
            };

            for (int i = 0; i < 20; i++)
            {
                bool predicted = Random.value > 0.1f;
                bool confirmed = predicted && Random.value > 0.15f;
                
                session.Records.Add(new HitRecord
                {
                    Timestamp = i * 0.5f,
                    WasPredicted = predicted,
                    WasConfirmed = confirmed,
                    Latency = Random.Range(0.02f, 0.15f),
                    Damage = Random.Range(20f, 80f),
                    HitRegion = Random.value > 0.8f ? "Head" : "Torso",
                    WeaponId = "TestWeapon",
                    TargetId = $"Target_{Random.Range(1, 5)}"
                });
            }

            session.Accuracy = (float)session.Records.Count(r => r.WasConfirmed) / 
                              session.Records.Count(r => r.WasPredicted);

            _savedSessions.Add(session);
            _selectedSessionIndex = _savedSessions.Count - 1;
            AnalyzeSession(session);
        }

        private void ExportSession(HitRecordingSession session)
        {
            string json = JsonUtility.ToJson(session, true);
            string path = EditorUtility.SaveFilePanel("Export Session", "", session.Name, "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[HitRecording] Exported session to {path}");
            }
        }

        private void ImportSession()
        {
            string path = EditorUtility.OpenFilePanel("Import Session", "", "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                string json = System.IO.File.ReadAllText(path);
                var session = JsonUtility.FromJson<HitRecordingSession>(json);
                _savedSessions.Add(session);
                _selectedSessionIndex = _savedSessions.Count - 1;
                AnalyzeSession(session);
                Debug.Log($"[HitRecording] Imported session: {session.Name}");
            }
        }
    }
}
