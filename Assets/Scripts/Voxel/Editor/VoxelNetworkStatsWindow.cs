using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.NetCode;
using DIG.Voxel.Systems.Network;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// Editor window for monitoring voxel network statistics.
    /// Shows bandwidth usage, RPC counts, and sync status.
    /// </summary>
    public class VoxelNetworkStatsWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        
        // Stats history for graphs
        private float[] _modificationHistory = new float[60];
        private float[] _bandwidthHistory = new float[60];
        private int _historyIndex;
        private float _lastHistoryUpdate;
        
        // Estimated bytes per RPC
        private const int BYTES_PER_MODIFICATION = 64; // Approximate RPC overhead + data
        
        [MenuItem("DIG/Voxel/Network Stats")]
        public static void ShowWindow()
        {
            var window = GetWindow<VoxelNetworkStatsWindow>("Voxel Net Stats");
            window.minSize = new Vector2(350, 450);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Voxel Network Statistics", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see network statistics.", MessageType.Info);
                return;
            }
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            DrawServerStats();
            EditorGUILayout.Space(10);
            DrawClientStats();
            EditorGUILayout.Space(10);
            DrawHistoryStats();
            EditorGUILayout.Space(10);
            DrawBandwidthGraph();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawServerStats()
        {
            EditorGUILayout.LabelField("Server Statistics", EditorStyles.boldLabel);
            
            var serverWorld = GetServerWorld();
            if (serverWorld == null)
            {
                EditorGUILayout.LabelField("Server world not found");
                return;
            }
            
            // Get batching stats from singletons
            var em = serverWorld.EntityManager;
            
            // Check if singletons exist
            // Getting singletons via query is safer in editor context
            using var statsQuery = em.CreateEntityQuery(typeof(VoxelBatchingStats));
            using var queueQuery = em.CreateEntityQuery(typeof(VoxelBatchingQueue));
            using var historyQuery = em.CreateEntityQuery(typeof(VoxelHistory));
            
            if (statsQuery.CalculateEntityCount() > 0)
            {
                var stats = statsQuery.GetSingleton<VoxelBatchingStats>();
                
                // Real-time stats (rolling average - more stable)
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Network Activity (5s avg)", EditorStyles.miniLabel);
                
                // Show rolling average prominently
                EditorGUILayout.LabelField($"Modifications/sec: {stats.RollingModsPerSec:F1}");
                EditorGUILayout.LabelField($"Batches/sec: {stats.RollingBatchesPerSec:F1}");
                
                // Get pending count from Queue if available
                int pendingCount = 0;
                if (queueQuery.CalculateEntityCount() > 0)
                {
                    var queue = queueQuery.GetSingleton<VoxelBatchingQueue>();
                    if (queue.Value.IsCreated) pendingCount = queue.Value.Length;
                }
                EditorGUILayout.LabelField($"Pending: {pendingCount}");
                
                // Estimated bandwidth from rolling average
                float kbPerSec = (stats.RollingModsPerSec * BYTES_PER_MODIFICATION) / 1024f;
                var bandwidthStyle = new GUIStyle(EditorStyles.label);
                bandwidthStyle.normal.textColor = kbPerSec > 50 ? Color.red : (kbPerSec > 25 ? Color.yellow : Color.green);
                EditorGUILayout.LabelField($"Est. Bandwidth: {kbPerSec:F1} KB/s", bandwidthStyle);
                
                EditorGUILayout.EndVertical();
                
                // Lifetime totals
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Lifetime Totals", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Total Batches Sent: {stats.TotalBatchesSent}");
                EditorGUILayout.LabelField($"Total Modifications Sent: {stats.TotalModificationsSent}");
                if (stats.TotalBatchesSent > 0)
                    EditorGUILayout.LabelField($"Avg Batch Size: {(float)stats.TotalModificationsSent / stats.TotalBatchesSent:F1}");
                EditorGUILayout.EndVertical();
                
                // Update history for graph
                if (Time.time - _lastHistoryUpdate >= 1f)
                {
                    _modificationHistory[_historyIndex] = stats.ModificationsThisSecond;
                    _bandwidthHistory[_historyIndex] = kbPerSec;
                    _historyIndex = (_historyIndex + 1) % 60;
                    _lastHistoryUpdate = Time.time;
                }
                
                // Modification history
                if (historyQuery.CalculateEntityCount() > 0)
                {
                    var history = historyQuery.GetSingleton<VoxelHistory>();
                    if (history.Value.IsCreated)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField("Modification History", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"Total Recorded: {history.Value.Length}");
                        
                        // Calculate memory usage safely
                        long memBytes = history.Value.Capacity * Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<PendingModification>();
                        EditorGUILayout.LabelField($"Memory: {memBytes / 1024f:F1} KB");
                        EditorGUILayout.EndVertical();
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("VoxelBatchingStats not found (System running?)");
            }
        }
        
        private void DrawClientStats()
        {
            EditorGUILayout.LabelField("Client Statistics", EditorStyles.boldLabel);
            
            var clientWorld = GetClientWorld();
            if (clientWorld == null)
            {
                EditorGUILayout.LabelField("Client world not found");
                return;
            }
            
            // Get late-join sync stats
            var syncSystem = clientWorld.GetExistingSystemManaged<LateJoinSyncClientSystem>();
            if (syncSystem != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                if (syncSystem.SyncInProgress)
                {
                    EditorGUILayout.LabelField("Late-Join Sync", EditorStyles.miniLabel);
                    
                    var rect = EditorGUILayout.GetControlRect(false, 20);
                    EditorGUI.ProgressBar(rect, syncSystem.SyncProgress, $"Syncing... {syncSystem.SyncProgress * 100:F0}%");
                    
                    EditorGUILayout.LabelField($"Modifications Applied: {syncSystem.AppliedModifications}");
                }
                else
                {
                    EditorGUILayout.LabelField("Sync Status: Complete", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Modifications Synced: {syncSystem.AppliedModifications}");
                }
                
                EditorGUILayout.EndVertical();
            }
        }
        
        private void DrawHistoryStats()
        {
            EditorGUILayout.LabelField("Performance Summary (Last 60s)", EditorStyles.boldLabel);
            
            // Calculate averages
            float avgMods = 0, maxMods = 0;
            float avgBandwidth = 0, maxBandwidth = 0;
            int validSamples = 0;
            
            for (int i = 0; i < 60; i++)
            {
                if (_modificationHistory[i] > 0 || i < _historyIndex)
                {
                    avgMods += _modificationHistory[i];
                    avgBandwidth += _bandwidthHistory[i];
                    maxMods = Mathf.Max(maxMods, _modificationHistory[i]);
                    maxBandwidth = Mathf.Max(maxBandwidth, _bandwidthHistory[i]);
                    validSamples++;
                }
            }
            
            if (validSamples > 0)
            {
                avgMods /= validSamples;
                avgBandwidth /= validSamples;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Avg Modifications/sec: {avgMods:F1}");
            EditorGUILayout.LabelField($"Peak Modifications/sec: {maxMods:F0}");
            EditorGUILayout.LabelField($"Avg Bandwidth: {avgBandwidth:F1} KB/s");
            EditorGUILayout.LabelField($"Peak Bandwidth: {maxBandwidth:F1} KB/s");
            EditorGUILayout.EndVertical();
        }
        
        private void DrawBandwidthGraph()
        {
            EditorGUILayout.LabelField("Bandwidth History (60s)", EditorStyles.boldLabel);
            
            var rect = GUILayoutUtility.GetRect(position.width - 20, 100);
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            
            // Draw threshold line at 50 KB/s
            float thresholdY = rect.y + rect.height * (1 - 50f / 100f);
            Handles.color = new Color(1, 0.5f, 0, 0.5f);
            Handles.DrawLine(new Vector3(rect.x, thresholdY), new Vector3(rect.x + rect.width, thresholdY));
            
            // Draw bandwidth graph
            Handles.color = Color.green;
            
            for (int i = 1; i < 60; i++)
            {
                int idx0 = (_historyIndex + i - 1) % 60;
                int idx1 = (_historyIndex + i) % 60;
                
                float x0 = rect.x + (i - 1) * (rect.width / 59);
                float x1 = rect.x + i * (rect.width / 59);
                
                float y0 = rect.y + rect.height * (1 - Mathf.Clamp01(_bandwidthHistory[idx0] / 100f));
                float y1 = rect.y + rect.height * (1 - Mathf.Clamp01(_bandwidthHistory[idx1] / 100f));
                
                Handles.DrawLine(new Vector3(x0, y0), new Vector3(x1, y1));
            }
            
            // Labels
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("0 KB/s", GUILayout.Width(50));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("100 KB/s", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
        }
        
        private World GetServerWorld()
        {
            foreach (var world in World.All)
            {
                if (world.IsServer() || world.Name.Contains("Server"))
                    return world;
            }
            return null;
        }
        
        private World GetClientWorld()
        {
            foreach (var world in World.All)
            {
                if (world.IsClient() || world.Name.Contains("Client"))
                    return world;
            }
            return null;
        }

        private double _lastRepaint;

        private void OnInspectorUpdate()
        {
            // Throttled repaint at 2Hz max instead of every inspector update
            if (Application.isPlaying && EditorApplication.timeSinceStartup - _lastRepaint > 0.5)
            {
                _lastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }
    }
}
