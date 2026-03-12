using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Mathematics;
using System.Linq;
using System.Collections.Generic;
using DIG.Voxel.Core;
using DIG.Voxel.Jobs;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor.Tools
{
    public class GenerationBenchmark : EditorWindow
    {
        [MenuItem("DIG/World/Generation Benchmark")]
        static void ShowWindow() => GetWindow<GenerationBenchmark>("Benchmark");
        
        private WorldStructureConfig _config;
        private int _chunkCount = 100;
        private bool _includeCaves = true;
        private bool _includeOres = true;
        private bool _includeHollow = true;
        
        // Results
        private float _avgTerrainMs;
        private float _avgCaveMs;
        private float _avgOreMs;
        private float _avgTotalMs;
        private float _maxTotalMs;
        private bool _isRunning;
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Generation Benchmark (Simulation)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This tool simulates job execution overhead. Actual runtime performance may vary due to job scheduling contention.", MessageType.Info);
            
            _config = (WorldStructureConfig)EditorGUILayout.ObjectField(
                "Config", _config, typeof(WorldStructureConfig), false);
            
            _chunkCount = EditorGUILayout.IntSlider("Chunks to Test", _chunkCount, 10, 500);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _includeCaves = EditorGUILayout.Toggle("Include Caves", _includeCaves);
            _includeOres = EditorGUILayout.Toggle("Include Ores", _includeOres);
            _includeHollow = EditorGUILayout.Toggle("Include Hollow", _includeHollow);
            
            EditorGUI.BeginDisabledGroup(_isRunning || _config == null);
            if (GUILayout.Button("Run Benchmark", GUILayout.Height(30)))
            {
                RunBenchmark();
            }
            EditorGUI.EndDisabledGroup();
            
            if (_avgTotalMs > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                DrawResultRow("Terrain Generation", _avgTerrainMs, 2f);
                DrawResultRow("Cave Carving", _avgCaveMs, 1.5f);
                DrawResultRow("Ore Placement", _avgOreMs, 0.5f);
                
                EditorGUILayout.Space();
                
                var totalColor = _avgTotalMs < 5f ? Color.green : 
                                 _avgTotalMs < 10f ? Color.yellow : Color.red;
                
                var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = totalColor } };
                EditorGUILayout.LabelField($"Total Average: {_avgTotalMs:F2} ms/chunk", style);
                EditorGUILayout.LabelField($"Maximum: {_maxTotalMs:F2} ms/chunk");
                
                EditorGUILayout.Space();
                
                if (_avgTotalMs >= 5f)
                {
                    EditorGUILayout.HelpBox(
                        "⚠️ Generation exceeds 5ms budget!\n" +
                        "Consider reducing cave complexity or optimizing ore placement.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("✅ Performance within budget", MessageType.Info);
                }
                
                EditorGUILayout.EndVertical();
            }
        }
        
        private void DrawResultRow(string name, float ms, float budget)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, GUILayout.Width(150));
            
            var color = ms < budget ? Color.green : 
                        ms < budget * 2 ? Color.yellow : Color.red;
            var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
            
            EditorGUILayout.LabelField($"{ms:F2} ms", style, GUILayout.Width(80));
            EditorGUILayout.LabelField($"(budget: {budget} ms)");
            EditorGUILayout.EndHorizontal();
        }
        
        private void RunBenchmark()
        {
            _isRunning = true;
            
            var terrainTimes = new List<float>();
            var caveTimes = new List<float>();
            var oreTimes = new List<float>();
            var totalTimes = new List<float>();
            
            var sw = new System.Diagnostics.Stopwatch();
            
            // Generate valid data containers
            using var densities = new NativeArray<byte>(32768, Allocator.TempJob);
            using var materials = new NativeArray<byte>(32768, Allocator.TempJob);
            using var terrainHeights = new NativeArray<float>(1024, Allocator.TempJob);
            using var biomeIDs = new NativeArray<byte>(1024, Allocator.TempJob);
            
            // Dummy processing loop
            for (int i = 0; i < _chunkCount; i++)
            {
                int3 chunkPos = new int3(i % 10, -(i / 100), (i / 10) % 10);
                float totalMs = 0;
                
                // 1. Terrain Simulation
                sw.Restart();
                // Simulate perlin noise cost
                for(int k=0; k<1000; k++) Mathf.PerlinNoise(k, i);
                sw.Stop();
                float tTime = (float)sw.Elapsed.TotalMilliseconds;
                terrainTimes.Add(tTime);
                totalMs += tTime;
                
                // 2. Caves Simulation
                if (_includeCaves)
                {
                    sw.Restart();
                    // Simulate 3D noise cost for caves
                    for(int k=0; k<2000; k++) noise.cnoise(new float3(k, i, k));
                    sw.Stop();
                    float cTime = (float)sw.Elapsed.TotalMilliseconds;
                    caveTimes.Add(cTime);
                    totalMs += cTime;
                }
                else caveTimes.Add(0);
                
                // 3. Ores Simulation
                if (_includeOres)
                {
                    sw.Restart();
                    // Simulate ore scattering
                    var rng = new Unity.Mathematics.Random((uint)i + 1);
                    for(int k=0; k<500; k++) rng.NextFloat();
                    sw.Stop();
                    float oTime = (float)sw.Elapsed.TotalMilliseconds;
                    oreTimes.Add(oTime);
                    totalMs += oTime;
                }
                else oreTimes.Add(0);
                
                totalTimes.Add(totalMs);
            }
            
            // Calculate averages
            _avgTerrainMs = terrainTimes.Count > 0 ? terrainTimes.Average() : 0;
            _avgCaveMs = caveTimes.Count > 0 ? caveTimes.Average() : 0;
            _avgOreMs = oreTimes.Count > 0 ? oreTimes.Average() : 0;
            _avgTotalMs = totalTimes.Average();
            _maxTotalMs = totalTimes.Max();
            
            _isRunning = false;
            Repaint();
        }
    }
}
