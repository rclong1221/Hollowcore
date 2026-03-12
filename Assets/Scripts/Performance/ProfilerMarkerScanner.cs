using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using System.Text;
using System.IO;

namespace DIG.Performance
{
    public class ProfilerMarkerScanner : MonoBehaviour
    {
        public bool ScanOnStart = true;

        // List of candidate names to check
        private readonly string[] _candidates = new[]
        {
            // Physics
            "PhysicsSystemGroup",
            "Physics.Simulate",
            "Physics.Processing",
            "Physics.SimulationStep",
            "Physics.FetchResults",
            "Physics.UpdateBodies",
            "Physics.ProcessReports",
            
            // NetCode
            "NetworkReceiveSystemGroup",
            "NetworkUpdateSystemGroup",
            "GhostUpdateSystemGroup",
            "PredictedSimulationSystemGroup",
            "NetworkTickSystem",
            "GhostPredictionSystemGroup",
            "Transport.Receive",
            
            // Transforms
            "TransformSystemGroup",
            "LocalToWorldSystem",
            "TRSToLocalToWorldSystem",
            "ParentSystem",
            "CompositeScaleSystem",
            
            // Rendering / Presentation
            "PresentationSystemGroup",
            "RenderMeshSystemV2",
            "RenderBoundsUpdateSystem",
            "CullingGroup",
            
            // General ECS
            "StructuralChanges",
            "InitializationSystemGroup",
            "SimulationSystemGroup",
            "FixedStepSimulationSystemGroup"
        };

        void Start()
        {
            if (ScanOnStart)
            {
                ScanMarkers();
            }
        }

        [ContextMenu("Scan Markers")]
        public void ScanMarkers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Profiler Marker Scan Results ===");
            int found = 0;

            foreach (var name in _candidates)
            {
                // Try as Script
                if (CheckMarker(name, ProfilerCategory.Scripts, sb)) found++;
                
                // Try as Physics
                else if (CheckMarker(name, ProfilerCategory.Physics, sb)) found++;
                
                // Try as Render
                else if (CheckMarker(name, ProfilerCategory.Render, sb)) found++;
                
                // Try as Internal
                else if (CheckMarker(name, ProfilerCategory.Internal, sb)) found++;
            }

            sb.AppendLine($"Total Found: {found} / {_candidates.Length}");
            
            string path = Path.Combine(Application.dataPath, "ProfilerMarkersScan.txt");
            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log($"Scanned markers saved to {path}");
        }

        private bool CheckMarker(string name, ProfilerCategory category, StringBuilder sb)
        {
            var recorder = ProfilerRecorder.StartNew(category, name);
            try
            {
                if (recorder.Valid)
                {
                    sb.AppendLine($"[VALID] {name} (Category: {category})");
                    return true;
                }
            }
            finally
            {
                recorder.Dispose();
            }
            return false;
        }
    }
}
