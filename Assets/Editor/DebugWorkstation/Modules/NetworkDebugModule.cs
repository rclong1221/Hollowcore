using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.DebugWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 DW-06: Network Debug module.
    /// Sync state visualization, prediction accuracy.
    /// </summary>
    public class NetworkDebugModule : IDebugModule
    {
        private Vector2 _scrollPosition;
        
        // Connection state
        private bool _isConnected = true;
        private string _serverAddress = "127.0.0.1:7777";
        private int _playerCount = 4;
        private float _uptime = 125.5f;
        
        // Network stats
        private float _ping = 45f;
        private float _avgPing = 52f;
        private float _jitter = 8f;
        private float _packetLoss = 0.5f;
        private float _bandwidth = 125f; // KB/s
        
        // Prediction stats
        private float _predictionAccuracy = 94.5f;
        private int _mispredictions = 12;
        private int _totalPredictions = 218;
        private float _avgRollbackFrames = 1.2f;
        
        // Sync entities
        private List<SyncedEntity> _syncedEntities = new List<SyncedEntity>();
        
        // Network log
        private List<NetworkEvent> _networkLog = new List<NetworkEvent>();
        private bool _logEnabled = true;

        [System.Serializable]
        private class SyncedEntity
        {
            public string Name;
            public EntityType Type;
            public int NetworkId;
            public SyncState State;
            public float LastSyncTime;
            public float PositionError;
            public bool HasAuthority;
        }

        private enum EntityType
        {
            Player,
            Projectile,
            Weapon,
            VFX,
            AI
        }

        private enum SyncState
        {
            Synced,
            Interpolating,
            Predicting,
            Rollback,
            Desynced
        }

        [System.Serializable]
        private class NetworkEvent
        {
            public float Timestamp;
            public NetworkEventType Type;
            public string Message;
            public bool IsError;
        }

        private enum NetworkEventType
        {
            Connect,
            Disconnect,
            Spawn,
            Despawn,
            Sync,
            Prediction,
            Rollback,
            Error
        }

        public NetworkDebugModule()
        {
            InitializeSimulatedData();
        }

        private void InitializeSimulatedData()
        {
            _syncedEntities = new List<SyncedEntity>
            {
                new SyncedEntity { Name = "Player_Local", Type = EntityType.Player, NetworkId = 1, State = SyncState.Synced, PositionError = 0f, HasAuthority = true },
                new SyncedEntity { Name = "Player_2", Type = EntityType.Player, NetworkId = 2, State = SyncState.Interpolating, PositionError = 0.02f, HasAuthority = false },
                new SyncedEntity { Name = "Player_3", Type = EntityType.Player, NetworkId = 3, State = SyncState.Predicting, PositionError = 0.15f, HasAuthority = false },
                new SyncedEntity { Name = "Bullet_127", Type = EntityType.Projectile, NetworkId = 127, State = SyncState.Synced, PositionError = 0f, HasAuthority = true },
                new SyncedEntity { Name = "Bullet_128", Type = EntityType.Projectile, NetworkId = 128, State = SyncState.Predicting, PositionError = 0.05f, HasAuthority = false },
                new SyncedEntity { Name = "Enemy_01", Type = EntityType.AI, NetworkId = 50, State = SyncState.Interpolating, PositionError = 0.08f, HasAuthority = false },
            };
            
            _networkLog = new List<NetworkEvent>
            {
                new NetworkEvent { Timestamp = 0.5f, Type = NetworkEventType.Connect, Message = "Connected to server" },
                new NetworkEvent { Timestamp = 1.2f, Type = NetworkEventType.Spawn, Message = "Spawned Player_Local (ID: 1)" },
                new NetworkEvent { Timestamp = 2.0f, Type = NetworkEventType.Sync, Message = "Initial sync complete" },
                new NetworkEvent { Timestamp = 15.3f, Type = NetworkEventType.Prediction, Message = "Misprediction detected, rolling back 2 frames" },
                new NetworkEvent { Timestamp = 45.8f, Type = NetworkEventType.Error, Message = "High latency spike: 180ms", IsError = true },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Network Debug", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode with NetCode for live network debugging. Showing simulated data.",
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawConnectionStatus();
            EditorGUILayout.Space(10);
            DrawNetworkStats();
            EditorGUILayout.Space(10);
            DrawPredictionStats();
            EditorGUILayout.Space(10);
            DrawSyncedEntities();
            EditorGUILayout.Space(10);
            DrawNetworkLog();
            EditorGUILayout.Space(10);
            DrawDebugActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawConnectionStatus()
        {
            EditorGUILayout.LabelField("Connection Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Connection indicator
            GUI.color = _isConnected ? Color.green : Color.red;
            EditorGUILayout.LabelField(_isConnected ? "● CONNECTED" : "○ DISCONNECTED", 
                EditorStyles.boldLabel, GUILayout.Width(120));
            GUI.color = Color.white;
            
            EditorGUILayout.LabelField($"Server: {_serverAddress}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"Players: {_playerCount}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"Uptime: {_uptime:F0}s", GUILayout.Width(100));
            
            GUILayout.FlexibleSpace();
            
            if (_isConnected)
            {
                if (GUILayout.Button("Disconnect", GUILayout.Width(80)))
                {
                    _isConnected = false;
                }
            }
            else
            {
                if (GUILayout.Button("Connect", GUILayout.Width(80)))
                {
                    _isConnected = true;
                }
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawNetworkStats()
        {
            EditorGUILayout.LabelField("Network Statistics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Ping
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Ping", EditorStyles.centeredGreyMiniLabel);
            GUI.color = GetPingColor(_ping);
            EditorGUILayout.LabelField($"{_ping:F0} ms", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.LabelField($"Avg: {_avgPing:F0} ms", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            
            // Jitter
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Jitter", EditorStyles.centeredGreyMiniLabel);
            GUI.color = _jitter < 10f ? Color.green : _jitter < 25f ? Color.yellow : Color.red;
            EditorGUILayout.LabelField($"±{_jitter:F0} ms", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Packet Loss
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Packet Loss", EditorStyles.centeredGreyMiniLabel);
            GUI.color = _packetLoss < 1f ? Color.green : _packetLoss < 5f ? Color.yellow : Color.red;
            EditorGUILayout.LabelField($"{_packetLoss:F1}%", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Bandwidth
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Bandwidth", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_bandwidth:F0} KB/s", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            
            // Bandwidth bar
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(8), GUILayout.ExpandWidth(true));
            DrawBandwidthBar(barRect);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            // Ping graph
            EditorGUILayout.Space(5);
            Rect graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(60), GUILayout.ExpandWidth(true));
            DrawPingGraph(graphRect);

            EditorGUILayout.EndVertical();
        }

        private Color GetPingColor(float ping)
        {
            if (ping < 50f) return Color.green;
            if (ping < 100f) return Color.yellow;
            if (ping < 150f) return new Color(1f, 0.5f, 0f);
            return Color.red;
        }

        private void DrawBandwidthBar(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            float ratio = Mathf.Clamp01(_bandwidth / 500f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), 
                new Color(0.3f, 0.7f, 0.9f));
        }

        private void DrawPingGraph(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            
            // Simulated ping history
            Handles.color = Color.cyan;
            int points = 60;
            
            for (int i = 1; i < points; i++)
            {
                float ping1 = _ping + Mathf.Sin(i * 0.3f) * _jitter;
                float ping2 = _ping + Mathf.Sin((i + 1) * 0.3f) * _jitter;
                
                float x1 = rect.x + ((i - 1) / (float)points) * rect.width;
                float x2 = rect.x + (i / (float)points) * rect.width;
                float y1 = rect.yMax - (ping1 / 200f) * rect.height;
                float y2 = rect.yMax - (ping2 / 200f) * rect.height;
                
                Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
            }
        }

        private void DrawPredictionStats()
        {
            EditorGUILayout.LabelField("Prediction & Rollback", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Prediction accuracy
            EditorGUILayout.BeginVertical(GUILayout.Width(130));
            EditorGUILayout.LabelField("Prediction Accuracy", EditorStyles.centeredGreyMiniLabel);
            GUI.color = _predictionAccuracy > 90f ? Color.green : 
                       _predictionAccuracy > 80f ? Color.yellow : Color.red;
            EditorGUILayout.LabelField($"{_predictionAccuracy:F1}%", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 24, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Mispredictions
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Mispredictions", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_mispredictions}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.LabelField($"of {_totalPredictions}", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            
            // Rollback frames
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Avg Rollback", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{_avgRollbackFrames:F1}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.LabelField("frames", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSyncedEntities()
        {
            EditorGUILayout.LabelField($"Synced Entities ({_syncedEntities.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("ID", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("State", EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField("Pos Error", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Auth", EditorStyles.boldLabel, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            foreach (var entity in _syncedEntities)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField(entity.Name, GUILayout.Width(120));
                EditorGUILayout.LabelField(entity.Type.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField(entity.NetworkId.ToString(), GUILayout.Width(50));
                
                // State with color
                Color stateColor = entity.State switch
                {
                    SyncState.Synced => Color.green,
                    SyncState.Interpolating => Color.cyan,
                    SyncState.Predicting => Color.yellow,
                    SyncState.Rollback => new Color(1f, 0.5f, 0f),
                    SyncState.Desynced => Color.red,
                    _ => Color.white
                };
                
                GUI.color = stateColor;
                EditorGUILayout.LabelField(entity.State.ToString(), GUILayout.Width(90));
                GUI.color = Color.white;
                
                // Position error
                GUI.color = entity.PositionError < 0.1f ? Color.green : 
                           entity.PositionError < 0.5f ? Color.yellow : Color.red;
                EditorGUILayout.LabelField($"{entity.PositionError:F2}m", GUILayout.Width(70));
                GUI.color = Color.white;
                
                // Authority
                EditorGUILayout.LabelField(entity.HasAuthority ? "✓" : "", GUILayout.Width(40));
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNetworkLog()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Network Log", EditorStyles.boldLabel);
            _logEnabled = EditorGUILayout.Toggle(_logEnabled, GUILayout.Width(20));
            
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _networkLog.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(100));

            foreach (var evt in _networkLog.TakeLast(10).Reverse())
            {
                EditorGUILayout.BeginHorizontal();
                
                GUI.color = evt.IsError ? Color.red : Color.white;
                EditorGUILayout.LabelField($"[{evt.Timestamp:F1}s]", GUILayout.Width(50));
                EditorGUILayout.LabelField(evt.Type.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField(evt.Message);
                GUI.color = Color.white;
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDebugActions()
        {
            EditorGUILayout.LabelField("Debug Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Simulate Lag"))
            {
                SimulateLag();
            }
            
            if (GUILayout.Button("Simulate Packet Loss"))
            {
                SimulatePacketLoss();
            }
            
            if (GUILayout.Button("Force Resync"))
            {
                ForceResync();
            }
            
            if (GUILayout.Button("Export Metrics"))
            {
                ExportMetrics();
            }
            
            EditorGUILayout.EndHorizontal();

            // Simulation controls
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Simulate Ping:", GUILayout.Width(90));
            _ping = EditorGUILayout.Slider(_ping, 0f, 300f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void SimulateLag()
        {
            _ping = 200f;
            _jitter = 50f;
            Debug.Log("[NetworkDebug] Simulating high latency");
        }

        private void SimulatePacketLoss()
        {
            _packetLoss = 10f;
            Debug.Log("[NetworkDebug] Simulating packet loss");
        }

        private void ForceResync()
        {
            foreach (var entity in _syncedEntities)
            {
                entity.State = SyncState.Synced;
                entity.PositionError = 0f;
            }
            Debug.Log("[NetworkDebug] Force resync triggered");
        }

        private void ExportMetrics()
        {
            Debug.Log("[NetworkDebug] Metrics export pending");
        }
    }
}
