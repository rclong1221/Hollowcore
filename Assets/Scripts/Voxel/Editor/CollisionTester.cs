using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DIG.Voxel.Editor
{
    public class CollisionTester : EditorWindow
    {
        [MenuItem("DIG/Voxel/Collision Tester")]
        static void ShowWindow() => GetWindow<CollisionTester>("Collision Tester");
        
        private struct TestResult
        {
            public string Name;
            public bool Passed;
            public string Message;
        }
        
        private List<TestResult> _results = new();
        private bool _testRunning = false;
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Collision Validation Suite", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "These tests verify collision is working correctly.\n" +
                "Run in Play Mode after chunks are loaded.",
                MessageType.Info);
            
            EditorGUI.BeginDisabledGroup(!Application.isPlaying || _testRunning);
            
            if (GUILayout.Button("Run All Tests"))
            {
                RunAllTests();
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Raycast Down"))
                _results.Add(TestRaycastDown());
            if (GUILayout.Button("Test Layer Matrix"))
                _results.Add(TestLayerMatrix());
            if (GUILayout.Button("Test Collider State"))
                _results.Add(TestColliderEnabled());
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(10);
            
            // Results
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            
            foreach (var result in _results)
            {
                var rect = EditorGUILayout.GetControlRect();
                
                // Icon
                var iconRect = new Rect(rect.x, rect.y, 20, rect.height);
                EditorGUI.LabelField(iconRect, result.Passed ? "✅" : "❌");
                
                // Name
                var nameRect = new Rect(rect.x + 25, rect.y, 150, rect.height);
                EditorGUI.LabelField(nameRect, result.Name);
                
                // Message
                var msgRect = new Rect(rect.x + 180, rect.y, rect.width - 180, rect.height);
                EditorGUI.LabelField(msgRect, result.Message, 
                    result.Passed ? EditorStyles.label : EditorStyles.boldLabel);
            }
            
            if (_results.Count > 0)
            {
                EditorGUILayout.Space();
                
                int passed = _results.Count(r => r.Passed);
                int total = _results.Count;
                
                if (passed == total)
                    EditorGUILayout.HelpBox($"All {total} tests passed!", MessageType.Info);
                else
                    EditorGUILayout.HelpBox($"{passed}/{total} tests passed. See failures above.", MessageType.Error);
                
                if (GUILayout.Button("Clear Results"))
                    _results.Clear();
            }
        }
        
        private void RunAllTests()
        {
            _results.Clear();
            _results.Add(TestRaycastDown());
            _results.Add(TestLayerMatrix());
            _results.Add(TestColliderEnabled());
        }
        
        private TestResult TestRaycastDown()
        {
            var cam = Camera.main;
            if (cam == null)
                return new TestResult { Name = "Raycast Down", Passed = false, Message = "No main camera found" };
            
            var origin = cam.transform.position; 
            // Ensure we are high enough? 
            // Assuming camera is above ground.
            
            int voxelLayer = 1 << LayerMask.NameToLayer("Voxel");
            if (voxelLayer == 0) // Layer not found (returns 0 or -1? LayerMask.GetMask returns int. NameToLayer returns int)
            {
                return new TestResult { Name = "Raycast Down", Passed = false, Message = "Layer 'Voxel' not found" };
            }

            if (Physics.Raycast(origin, Vector3.down, out var hit, 100f, voxelLayer))
            {
                return new TestResult 
                { 
                    Name = "Raycast Down", 
                    Passed = true, 
                    Message = $"Hit {hit.collider.name} at Y={hit.point.y:F1}" 
                };
            }
            else
            {
                // Try casting from higher up?
                origin = origin + Vector3.up * 50;
                if (Physics.Raycast(origin, Vector3.down, out hit, 100f, voxelLayer))
                {
                     return new TestResult 
                    { 
                        Name = "Raycast Down", 
                        Passed = true, 
                        Message = $"Hit {hit.collider.name} at Y={hit.point.y:F1} (from +50y)" 
                    };
                }
                
                return new TestResult 
                { 
                    Name = "Raycast Down", 
                    Passed = false, 
                    Message = "No collision detected! Camera might be under ground or no chunks." 
                };
            }
        }
        
        private TestResult TestLayerMatrix()
        {
            int player = LayerMask.NameToLayer("Player"); // Or "Default"
            if (player == -1) player = LayerMask.NameToLayer("Default");
            
            int voxel = LayerMask.NameToLayer("Voxel");
            
            if (player == -1)
                return new TestResult { Name = "Layer Matrix", Passed = false, Message = "Player/Default layer not defined" };
            if (voxel == -1)
                return new TestResult { Name = "Layer Matrix", Passed = false, Message = "Voxel layer not defined" };
            
            bool ignore = Physics.GetIgnoreLayerCollision(player, voxel);
            bool canCollide = !ignore;
            
            return new TestResult 
            { 
                Name = "Layer Matrix", 
                Passed = canCollide, 
                Message = canCollide ? "Player↔Voxel enabled" : "Player↔Voxel DISABLED in Physics settings!" 
            };
        }
        
        private TestResult TestColliderEnabled()
        {
            var colliders = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
            int voxelLayer = LayerMask.NameToLayer("Voxel");
            
            int total = 0;
            int disabled = 0;
            int noMesh = 0;
            
            foreach (var col in colliders)
            {
                if (col.gameObject.layer != voxelLayer) continue;
                total++;
                
                if (!col.enabled) disabled++;
                if (col.sharedMesh == null || col.sharedMesh.vertexCount == 0) noMesh++;
            }
            
            if (total == 0)
                return new TestResult { Name = "Collider State", Passed = false, Message = "No voxel colliders found! (Wait for generation?)" };
            if (disabled > 0)
                return new TestResult { Name = "Collider State", Passed = false, Message = $"{disabled}/{total} colliders disabled" };
            if (noMesh > 0)
                return new TestResult { Name = "Collider State", Passed = false, Message = $"{noMesh}/{total} colliders have no mesh" };
            
            return new TestResult { Name = "Collider State", Passed = true, Message = $"{total} colliders OK" };
        }
    }
}
