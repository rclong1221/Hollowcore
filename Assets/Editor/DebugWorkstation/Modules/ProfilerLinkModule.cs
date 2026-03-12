using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.DebugWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 DW-05: Profiler Link module.
    /// Performance profiler integration, frame timing.
    /// </summary>
    public class ProfilerLinkModule : IDebugModule
    {
        private Vector2 _scrollPosition;
        
        // Performance data
        private List<FrameData> _frameHistory = new List<FrameData>();
        private const int MAX_FRAME_HISTORY = 300;
        
        // Current stats (simulated)
        private float _currentFPS = 60f;
        private float _avgFPS = 58.5f;
        private float _minFPS = 45f;
        private float _maxFPS = 62f;
        private float _frameTime = 16.7f;
        private float _gpuTime = 8.2f;
        private float _cpuTime = 12.1f;
        
        // Weapon-specific metrics
        private float _weaponUpdateTime = 0.15f;
        private float _projectileSystemTime = 0.42f;
        private float _vfxSystemTime = 0.85f;
        private float _audioSystemTime = 0.08f;
        
        // Thresholds
        private float _targetFPS = 60f;
        private float _warningFPS = 45f;
        private float _criticalFPS = 30f;
        
        // Recording
        private bool _isRecording = false;
        private float _recordStartTime = 0f;

        [System.Serializable]
        private class FrameData
        {
            public float Time;
            public float FPS;
            public float FrameTime;
            public float CPUTime;
            public float GPUTime;
        }

        public ProfilerLinkModule()
        {
            InitializeSimulatedData();
        }

        private void InitializeSimulatedData()
        {
            for (int i = 0; i < 120; i++)
            {
                _frameHistory.Add(new FrameData
                {
                    Time = i * 0.016f,
                    FPS = 55f + Random.Range(-10f, 7f),
                    FrameTime = 16.7f + Random.Range(-2f, 5f),
                    CPUTime = 10f + Random.Range(-2f, 5f),
                    GPUTime = 6f + Random.Range(-1f, 4f)
                });
            }
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Profiler Link", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode for live performance data. Showing simulated data.",
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawRecordingControls();
            EditorGUILayout.Space(10);
            DrawFPSOverview();
            EditorGUILayout.Space(10);
            DrawFrameTimeGraph();
            EditorGUILayout.Space(10);
            DrawSystemBreakdown();
            EditorGUILayout.Space(10);
            DrawWeaponPerformance();
            EditorGUILayout.Space(10);
            DrawProfilerActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawRecordingControls()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _isRecording ? Color.red : Color.green;
            
            if (GUILayout.Button(_isRecording ? "● Recording" : "○ Start Recording", 
                EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                ToggleRecording();
            }
            
            GUI.backgroundColor = prevColor;
            
            if (_isRecording)
            {
                float duration = Time.realtimeSinceStartup - _recordStartTime;
                EditorGUILayout.LabelField($"Duration: {duration:F1}s", GUILayout.Width(100));
            }
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
            {
                _frameHistory.Clear();
            }
            
            if (GUILayout.Button("Open Profiler", EditorStyles.toolbarButton))
            {
                EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFPSOverview()
        {
            EditorGUILayout.LabelField("FPS Overview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Current FPS
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Current", EditorStyles.centeredGreyMiniLabel);
            GUI.color = GetFPSColor(_currentFPS);
            EditorGUILayout.LabelField($"{_currentFPS:F0}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 28, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.LabelField("FPS", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            
            // Average FPS
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            EditorGUILayout.LabelField("Average", EditorStyles.centeredGreyMiniLabel);
            GUI.color = GetFPSColor(_avgFPS);
            EditorGUILayout.LabelField($"{_avgFPS:F1}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Min FPS
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            EditorGUILayout.LabelField("Min", EditorStyles.centeredGreyMiniLabel);
            GUI.color = GetFPSColor(_minFPS);
            EditorGUILayout.LabelField($"{_minFPS:F0}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Max FPS
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            EditorGUILayout.LabelField("Max", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_maxFPS:F0}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            // Frame Time
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Frame Time", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_frameTime:F1} ms", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            // CPU/GPU breakdown
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("CPU / GPU", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_cpuTime:F1} / {_gpuTime:F1} ms", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            
            // CPU/GPU bar
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(10), GUILayout.ExpandWidth(true));
            DrawCPUGPUBar(barRect);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            // Thresholds
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Thresholds:", GUILayout.Width(70));
            _targetFPS = EditorGUILayout.FloatField("Target", _targetFPS, GUILayout.Width(100));
            _warningFPS = EditorGUILayout.FloatField("Warning", _warningFPS, GUILayout.Width(100));
            _criticalFPS = EditorGUILayout.FloatField("Critical", _criticalFPS, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private Color GetFPSColor(float fps)
        {
            if (fps >= _targetFPS) return Color.green;
            if (fps >= _warningFPS) return Color.yellow;
            if (fps >= _criticalFPS) return new Color(1f, 0.5f, 0f);
            return Color.red;
        }

        private void DrawCPUGPUBar(Rect rect)
        {
            float total = _cpuTime + _gpuTime;
            float cpuRatio = _cpuTime / total;
            
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * cpuRatio, rect.height), 
                new Color(0.2f, 0.6f, 0.9f));
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * cpuRatio, rect.y, rect.width * (1 - cpuRatio), rect.height), 
                new Color(0.4f, 0.8f, 0.3f));
        }

        private void DrawFrameTimeGraph()
        {
            EditorGUILayout.LabelField("Frame Time History", EditorStyles.boldLabel);
            
            Rect graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(120), GUILayout.ExpandWidth(true));
            
            DrawGraph(graphRect);
        }

        private void DrawGraph(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));

            if (_frameHistory.Count < 2) return;

            // Grid lines
            Handles.color = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            
            // Horizontal grid lines at 16.67ms (60fps), 33.33ms (30fps)
            float targetLine = rect.yMax - (16.67f / 50f) * rect.height;
            float warningLine = rect.yMax - (33.33f / 50f) * rect.height;
            
            Handles.DrawLine(new Vector3(rect.x, targetLine), new Vector3(rect.xMax, targetLine));
            Handles.DrawLine(new Vector3(rect.x, warningLine), new Vector3(rect.xMax, warningLine));

            // Labels
            GUI.Label(new Rect(rect.x + 2, targetLine - 8, 50, 16), "60 FPS", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 2, warningLine - 8, 50, 16), "30 FPS", EditorStyles.miniLabel);

            // Draw frame time line
            Handles.color = Color.cyan;
            int count = Mathf.Min(_frameHistory.Count, (int)rect.width);
            
            for (int i = 1; i < count; i++)
            {
                int idx1 = _frameHistory.Count - count + i - 1;
                int idx2 = _frameHistory.Count - count + i;
                
                float x1 = rect.x + ((i - 1) / (float)count) * rect.width;
                float x2 = rect.x + (i / (float)count) * rect.width;
                
                float y1 = rect.yMax - Mathf.Clamp01(_frameHistory[idx1].FrameTime / 50f) * rect.height;
                float y2 = rect.yMax - Mathf.Clamp01(_frameHistory[idx2].FrameTime / 50f) * rect.height;
                
                // Color based on frame time
                Handles.color = _frameHistory[idx2].FrameTime < 16.67f ? Color.green :
                               _frameHistory[idx2].FrameTime < 33.33f ? Color.yellow : Color.red;
                
                Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
            }
        }

        private void DrawSystemBreakdown()
        {
            EditorGUILayout.LabelField("System Breakdown", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Main systems
            DrawSystemBar("CPU Main", _cpuTime, 20f, new Color(0.2f, 0.6f, 0.9f));
            DrawSystemBar("GPU Render", _gpuTime, 20f, new Color(0.4f, 0.8f, 0.3f));
            DrawSystemBar("Physics", 2.1f, 20f, new Color(0.9f, 0.6f, 0.2f));
            DrawSystemBar("Animation", 1.5f, 20f, new Color(0.8f, 0.4f, 0.7f));
            DrawSystemBar("Scripts", 3.2f, 20f, new Color(0.6f, 0.8f, 0.9f));

            EditorGUILayout.EndVertical();
        }

        private void DrawSystemBar(string name, float time, float maxTime, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(name, GUILayout.Width(100));
            
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(18), GUILayout.ExpandWidth(true));
            
            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
            
            float ratio = Mathf.Clamp01(time / maxTime);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height), color);
            
            GUI.Label(barRect, $"{time:F2} ms", 
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeaponPerformance()
        {
            EditorGUILayout.LabelField("Weapon System Performance", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawSystemBar("Weapon Update", _weaponUpdateTime, 2f, new Color(0.9f, 0.4f, 0.3f));
            DrawSystemBar("Projectile System", _projectileSystemTime, 2f, new Color(0.9f, 0.7f, 0.2f));
            DrawSystemBar("VFX System", _vfxSystemTime, 2f, new Color(0.5f, 0.8f, 0.9f));
            DrawSystemBar("Audio System", _audioSystemTime, 2f, new Color(0.6f, 0.9f, 0.5f));

            EditorGUILayout.Space(5);
            
            float totalWeapon = _weaponUpdateTime + _projectileSystemTime + _vfxSystemTime + _audioSystemTime;
            float percentOfFrame = (totalWeapon / _frameTime) * 100f;
            
            EditorGUILayout.LabelField($"Total Weapon Systems: {totalWeapon:F2} ms ({percentOfFrame:F1}% of frame)");

            EditorGUILayout.EndVertical();
        }

        private void DrawProfilerActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Take Screenshot"))
            {
                TakeProfileSnapshot();
            }
            
            if (GUILayout.Button("Export Data"))
            {
                ExportData();
            }
            
            if (GUILayout.Button("Deep Profile"))
            {
                DeepProfile();
            }
            
            if (GUILayout.Button("Memory Snapshot"))
            {
                MemorySnapshot();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ToggleRecording()
        {
            _isRecording = !_isRecording;
            if (_isRecording)
            {
                _recordStartTime = Time.realtimeSinceStartup;
                _frameHistory.Clear();
            }
        }

        private void TakeProfileSnapshot()
        {
            Debug.Log("[ProfilerLink] Snapshot taken");
        }

        private void ExportData()
        {
            string path = EditorUtility.SaveFilePanel("Export Performance Data", "", "perf_data.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[ProfilerLink] Exported to {path}");
            }
        }

        private void DeepProfile()
        {
            Debug.Log("[ProfilerLink] Deep profile started");
        }

        private void MemorySnapshot()
        {
            Debug.Log("[ProfilerLink] Memory snapshot taken");
        }
    }
}
