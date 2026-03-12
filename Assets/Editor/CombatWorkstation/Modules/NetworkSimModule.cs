using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.CombatWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CB-05: Network Sim module.
    /// Latency simulation for hit feedback testing.
    /// </summary>
    public class NetworkSimModule : ICombatModule
    {
        private Vector2 _scrollPosition;
        
        // Simulation settings
        private bool _isSimulationActive = false;
        private float _simulatedLatency = 50f; // ms
        private float _latencyVariance = 10f; // ms
        private float _packetLoss = 0f; // percentage
        private float _jitter = 5f; // ms
        
        // Presets
        private enum NetworkPreset { None, Local, LAN, Broadband, Mobile3G, Mobile4G, Satellite, BadWifi }
        private NetworkPreset _selectedPreset = NetworkPreset.None;
        
        // Stats
        private int _packetsSimulated = 0;
        private int _packetsDropped = 0;
        private float _averageActualLatency = 0f;
        private float _peakLatency = 0f;
        
        // Graph data
        private List<float> _latencyHistory = new List<float>();
        private const int MAX_HISTORY = 100;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Network Simulation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Simulate network conditions for testing hit feedback under various latencies.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawSimulationControls();
            EditorGUILayout.Space(10);
            DrawPresets();
            EditorGUILayout.Space(10);
            DrawLatencySettings();
            EditorGUILayout.Space(10);
            DrawLatencyGraph();
            EditorGUILayout.Space(10);
            DrawStats();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSimulationControls()
        {
            EditorGUILayout.LabelField("Simulation Control", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _isSimulationActive ? Color.red : Color.green;
            
            string buttonLabel = _isSimulationActive ? "⏹ Stop Simulation" : "▶ Start Simulation";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(35)))
            {
                ToggleSimulation();
            }
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Reset Stats", GUILayout.Height(35), GUILayout.Width(100)))
            {
                ResetStats();
            }

            EditorGUILayout.EndHorizontal();

            if (_isSimulationActive)
            {
                EditorGUILayout.HelpBox(
                    $"Simulating: {_simulatedLatency:F0}ms ± {_latencyVariance:F0}ms latency, " +
                    $"{_packetLoss:F1}% packet loss",
                    MessageType.Warning);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play mode to apply network simulation to hit detection.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresets()
        {
            EditorGUILayout.LabelField("Network Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (DrawPresetButton("Local", 0, 0, 0)) ApplyPreset(NetworkPreset.Local);
            if (DrawPresetButton("LAN", 5, 2, 0)) ApplyPreset(NetworkPreset.LAN);
            if (DrawPresetButton("Broadband", 30, 10, 0.1f)) ApplyPreset(NetworkPreset.Broadband);
            if (DrawPresetButton("4G", 60, 20, 0.5f)) ApplyPreset(NetworkPreset.Mobile4G);
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (DrawPresetButton("3G", 150, 50, 2)) ApplyPreset(NetworkPreset.Mobile3G);
            if (DrawPresetButton("Bad WiFi", 100, 80, 5)) ApplyPreset(NetworkPreset.BadWifi);
            if (DrawPresetButton("Satellite", 600, 100, 1)) ApplyPreset(NetworkPreset.Satellite);
            if (DrawPresetButton("Custom", -1, -1, -1)) { } // Just for layout
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private bool DrawPresetButton(string name, float latency, float variance, float loss)
        {
            string label = latency >= 0 
                ? $"{name}\n{latency}ms" 
                : name;
            
            return GUILayout.Button(label, GUILayout.Height(40));
        }

        private void DrawLatencySettings()
        {
            EditorGUILayout.LabelField("Latency Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Base latency
            EditorGUILayout.LabelField("Base Latency (RTT)", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _simulatedLatency = EditorGUILayout.Slider(_simulatedLatency, 0f, 1000f);
            EditorGUILayout.LabelField("ms", GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();

            // Variance
            EditorGUILayout.LabelField("Latency Variance", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _latencyVariance = EditorGUILayout.Slider(_latencyVariance, 0f, 200f);
            EditorGUILayout.LabelField("ms", GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();

            // Jitter
            EditorGUILayout.LabelField("Jitter", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _jitter = EditorGUILayout.Slider(_jitter, 0f, 100f);
            EditorGUILayout.LabelField("ms", GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();

            // Packet Loss
            EditorGUILayout.LabelField("Packet Loss", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _packetLoss = EditorGUILayout.Slider(_packetLoss, 0f, 50f);
            EditorGUILayout.LabelField("%", GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();

            // Visual representation
            EditorGUILayout.Space(10);
            DrawLatencyBar();

            EditorGUILayout.EndVertical();
        }

        private void DrawLatencyBar()
        {
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(30), GUILayout.ExpandWidth(true));

            // Background
            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

            // Latency zones
            float maxLatency = 500f;
            float normalizedLatency = Mathf.Clamp01(_simulatedLatency / maxLatency);
            float varianceWidth = (_latencyVariance / maxLatency) * barRect.width;

            // Good zone (0-50ms)
            Rect goodZone = new Rect(barRect.x, barRect.y, barRect.width * (50f / maxLatency), barRect.height);
            EditorGUI.DrawRect(goodZone, new Color(0.2f, 0.6f, 0.2f, 0.3f));

            // Okay zone (50-100ms)
            Rect okayZone = new Rect(barRect.x + goodZone.width, barRect.y, 
                barRect.width * (50f / maxLatency), barRect.height);
            EditorGUI.DrawRect(okayZone, new Color(0.6f, 0.6f, 0.2f, 0.3f));

            // Bad zone (100ms+)
            Rect badZone = new Rect(barRect.x + goodZone.width + okayZone.width, barRect.y, 
                barRect.width - goodZone.width - okayZone.width, barRect.height);
            EditorGUI.DrawRect(badZone, new Color(0.6f, 0.2f, 0.2f, 0.3f));

            // Current latency indicator with variance
            float latencyX = barRect.x + normalizedLatency * barRect.width;
            Rect varianceRect = new Rect(latencyX - varianceWidth / 2, barRect.y + 5, 
                varianceWidth, barRect.height - 10);
            EditorGUI.DrawRect(varianceRect, new Color(1f, 1f, 1f, 0.3f));

            // Center line
            EditorGUI.DrawRect(new Rect(latencyX - 1, barRect.y, 2, barRect.height), Color.white);

            // Labels
            GUI.Label(new Rect(barRect.x + 5, barRect.y + 5, 50, 20), "0ms", EditorStyles.miniLabel);
            GUI.Label(new Rect(barRect.center.x - 25, barRect.y + 5, 50, 20), "250ms", EditorStyles.miniLabel);
            GUI.Label(new Rect(barRect.xMax - 45, barRect.y + 5, 50, 20), "500ms+", EditorStyles.miniLabel);
        }

        private void DrawLatencyGraph()
        {
            EditorGUILayout.LabelField("Latency History", EditorStyles.boldLabel);
            
            Rect graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(100), GUILayout.ExpandWidth(true));

            // Background
            EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));

            // Grid lines
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            for (int i = 1; i < 4; i++)
            {
                float y = graphRect.y + graphRect.height * (i / 4f);
                Handles.DrawLine(new Vector3(graphRect.x, y), new Vector3(graphRect.xMax, y));
            }

            // Draw latency line
            if (_latencyHistory.Count > 1)
            {
                Handles.color = Color.cyan;
                
                float maxVal = 200f; // Max display latency
                
                for (int i = 1; i < _latencyHistory.Count; i++)
                {
                    float x1 = graphRect.x + ((i - 1) / (float)MAX_HISTORY) * graphRect.width;
                    float x2 = graphRect.x + (i / (float)MAX_HISTORY) * graphRect.width;
                    
                    float y1 = graphRect.yMax - (_latencyHistory[i - 1] / maxVal) * graphRect.height;
                    float y2 = graphRect.yMax - (_latencyHistory[i] / maxVal) * graphRect.height;
                    
                    y1 = Mathf.Clamp(y1, graphRect.y, graphRect.yMax);
                    y2 = Mathf.Clamp(y2, graphRect.y, graphRect.yMax);
                    
                    Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
                }
            }

            // Labels
            GUI.Label(new Rect(graphRect.x + 5, graphRect.y + 2, 50, 16), "200ms", EditorStyles.miniLabel);
            GUI.Label(new Rect(graphRect.x + 5, graphRect.yMax - 16, 50, 16), "0ms", EditorStyles.miniLabel);

            // Simulate some data for preview
            if (_isSimulationActive || !Application.isPlaying)
            {
                SimulateLatencyData();
            }
        }

        private void DrawStats()
        {
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Packets Simulated", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(_packetsSimulated.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Packets Dropped", EditorStyles.centeredGreyMiniLabel);
            Color prevColor = GUI.color;
            GUI.color = _packetsDropped > 0 ? Color.red : Color.white;
            EditorGUILayout.LabelField(_packetsDropped.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            GUI.color = prevColor;
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Avg Latency", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_averageActualLatency:F0}ms", 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Peak Latency", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_peakLatency:F0}ms", 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Drop rate
            if (_packetsSimulated > 0)
            {
                float dropRate = (float)_packetsDropped / _packetsSimulated;
                EditorGUILayout.LabelField($"Actual Drop Rate: {dropRate:P1}");
            }

            EditorGUILayout.EndVertical();
        }

        private void ToggleSimulation()
        {
            _isSimulationActive = !_isSimulationActive;
            
            if (_isSimulationActive)
            {
                Debug.Log($"[NetworkSim] Simulation started: {_simulatedLatency}ms latency, {_packetLoss}% loss");
                // Would apply to Unity Transport / Netcode settings
            }
            else
            {
                Debug.Log("[NetworkSim] Simulation stopped");
            }
        }

        private void ApplyPreset(NetworkPreset preset)
        {
            _selectedPreset = preset;
            
            switch (preset)
            {
                case NetworkPreset.Local:
                    _simulatedLatency = 0f;
                    _latencyVariance = 0f;
                    _packetLoss = 0f;
                    _jitter = 0f;
                    break;
                    
                case NetworkPreset.LAN:
                    _simulatedLatency = 5f;
                    _latencyVariance = 2f;
                    _packetLoss = 0f;
                    _jitter = 1f;
                    break;
                    
                case NetworkPreset.Broadband:
                    _simulatedLatency = 30f;
                    _latencyVariance = 10f;
                    _packetLoss = 0.1f;
                    _jitter = 5f;
                    break;
                    
                case NetworkPreset.Mobile4G:
                    _simulatedLatency = 60f;
                    _latencyVariance = 20f;
                    _packetLoss = 0.5f;
                    _jitter = 15f;
                    break;
                    
                case NetworkPreset.Mobile3G:
                    _simulatedLatency = 150f;
                    _latencyVariance = 50f;
                    _packetLoss = 2f;
                    _jitter = 30f;
                    break;
                    
                case NetworkPreset.BadWifi:
                    _simulatedLatency = 100f;
                    _latencyVariance = 80f;
                    _packetLoss = 5f;
                    _jitter = 50f;
                    break;
                    
                case NetworkPreset.Satellite:
                    _simulatedLatency = 600f;
                    _latencyVariance = 100f;
                    _packetLoss = 1f;
                    _jitter = 20f;
                    break;
            }
            
            Debug.Log($"[NetworkSim] Applied preset: {preset}");
        }

        private void ResetStats()
        {
            _packetsSimulated = 0;
            _packetsDropped = 0;
            _averageActualLatency = 0f;
            _peakLatency = 0f;
            _latencyHistory.Clear();
        }

        private void SimulateLatencyData()
        {
            // Add simulated data point for visualization
            float latency = _simulatedLatency + Random.Range(-_latencyVariance, _latencyVariance);
            latency += Random.Range(-_jitter, _jitter);
            latency = Mathf.Max(0, latency);
            
            _latencyHistory.Add(latency);
            if (_latencyHistory.Count > MAX_HISTORY)
            {
                _latencyHistory.RemoveAt(0);
            }

            // Update stats
            _packetsSimulated++;
            if (Random.value * 100f < _packetLoss)
            {
                _packetsDropped++;
            }
            
            _averageActualLatency = (_averageActualLatency * (_packetsSimulated - 1) + latency) / _packetsSimulated;
            _peakLatency = Mathf.Max(_peakLatency, latency);
        }
    }
}
