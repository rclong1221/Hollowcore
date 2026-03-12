using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.OpsiveExtractor
{
    /// <summary>
    /// Converts OPSIVE demo scenes to ECS-compatible scenes.
    ///
    /// This tool:
    /// 1. Opens an OPSIVE scene
    /// 2. Removes OPSIVE-specific GameObjects (characters, managers, spawners)
    /// 3. Preserves static geometry, lighting, and navigation
    /// 4. Optionally adds DIG spawn points and trigger zones
    /// 5. Saves as a new scene ready for ECS subscene baking
    ///
    /// Note: The resulting scene will need manual setup of:
    /// - Player spawn points
    /// - Item pickup locations
    /// - Trigger zone configurations
    /// - Subscene boundaries
    /// </summary>
    public class OpsiveSceneConverter : EditorWindow
    {
        private string _sourceScenePath = "";
        private string _outputFolder = "Assets/Scenes/Converted";
        private bool _preserveLighting = true;
        private bool _preserveNavMesh = true;
        private bool _preserveTerrain = true;
        private bool _addDefaultSpawnPoint = true;
        private bool _convertTriggerZones = true;
        private Vector2 _scrollPosition;

        private ConversionReport _lastReport;

        [MenuItem("Tools/DIG/OPSIVE Scene Converter")]
        public static void ShowWindow()
        {
            var window = GetWindow<OpsiveSceneConverter>("Scene Converter");
            window.minSize = new Vector2(500, 500);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("OPSIVE → ECS Scene Converter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Converts OPSIVE demo scenes to ECS-compatible scenes.\n\n" +
                "• Removes OPSIVE characters, managers, and spawners\n" +
                "• Preserves static geometry, lighting, and NavMesh\n" +
                "• Adds placeholder spawn points for DIG players\n" +
                "• Prepares scene for subscene baking",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Source scene selection
            using (new EditorGUILayout.HorizontalScope())
            {
                _sourceScenePath = EditorGUILayout.TextField("Source Scene", _sourceScenePath);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    var path = EditorUtility.OpenFilePanel("Select OPSIVE Scene",
                        "Assets", "unity");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var relativePath = GetProjectRelativePath(path);
                        if (!string.IsNullOrEmpty(relativePath))
                        {
                            _sourceScenePath = relativePath;
                        }
                    }
                }
            }

            // Output folder
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        var relativePath = GetProjectRelativePath(selected);
                        if (!string.IsNullOrEmpty(relativePath))
                        {
                            _outputFolder = relativePath;
                        }
                    }
                }
            }

            EditorGUILayout.Space(10);

            // Options
            EditorGUILayout.LabelField("Conversion Options", EditorStyles.boldLabel);
            _preserveLighting = EditorGUILayout.Toggle("Preserve Lighting", _preserveLighting);
            _preserveNavMesh = EditorGUILayout.Toggle("Preserve NavMesh", _preserveNavMesh);
            _preserveTerrain = EditorGUILayout.Toggle("Preserve Terrain", _preserveTerrain);
            _addDefaultSpawnPoint = EditorGUILayout.Toggle("Add Default Spawn Point", _addDefaultSpawnPoint);
            _convertTriggerZones = EditorGUILayout.Toggle("Convert Trigger Zones", _convertTriggerZones);

            EditorGUILayout.Space(15);

            // Convert button
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_sourceScenePath)))
            {
                if (GUILayout.Button("Convert Scene", GUILayout.Height(35)))
                {
                    ConvertScene();
                }
            }

            EditorGUILayout.Space(10);

            // Scan for OPSIVE scenes
            if (GUILayout.Button("Scan for OPSIVE Demo Scenes"))
            {
                ScanForOpsiveScenes();
            }

            // Display conversion report
            if (_lastReport != null)
            {
                EditorGUILayout.Space(15);
                DrawConversionReport();
            }
        }

        private void ConvertScene()
        {
            if (string.IsNullOrEmpty(_sourceScenePath))
            {
                Debug.LogWarning("[SceneConverter] No source scene specified");
                return;
            }

            // Save current scene
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }
            }

            _lastReport = new ConversionReport { SourcePath = _sourceScenePath };

            try
            {
                // Open the source scene
                var scene = EditorSceneManager.OpenScene(_sourceScenePath, OpenSceneMode.Single);
                _lastReport.SourceName = scene.name;

                // Analyze scene before modification
                AnalyzeScene(_lastReport);

                // Remove OPSIVE objects
                RemoveOpsiveObjects(_lastReport);

                // Convert trigger zones if requested
                if (_convertTriggerZones)
                {
                    ConvertTriggerZones(_lastReport);
                }

                // Add default spawn point
                if (_addDefaultSpawnPoint)
                {
                    AddDefaultSpawnPoint(_lastReport);
                }

                // Create markers for subscene organization
                AddSubsceneMarkers(_lastReport);

                // Handle lighting
                if (!_preserveLighting)
                {
                    RemoveLightingData(_lastReport);
                }

                // Ensure output folder exists
                if (!AssetDatabase.IsValidFolder(_outputFolder))
                {
                    CreateFolderRecursive(_outputFolder);
                }

                // Save the converted scene
                var outputPath = $"{_outputFolder}/{scene.name}_ECS.unity";
                EditorSceneManager.SaveScene(scene, outputPath);
                _lastReport.OutputPath = outputPath;
                _lastReport.Success = true;

                Debug.Log($"[SceneConverter] Scene converted successfully: {outputPath}");

                // Open the converted scene
                EditorSceneManager.OpenScene(outputPath);
            }
            catch (System.Exception e)
            {
                _lastReport.Success = false;
                _lastReport.ErrorMessage = e.Message;
                Debug.LogException(e);
            }
        }

        private void AnalyzeScene(ConversionReport report)
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                AnalyzeGameObject(root, report);
            }
        }

        private void AnalyzeGameObject(GameObject go, ConversionReport report)
        {
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;

                var ns = component.GetType().Namespace ?? "";
                if (ns.StartsWith("Opsive"))
                {
                    if (!report.OpsiveComponentTypes.Contains(component.GetType().Name))
                    {
                        report.OpsiveComponentTypes.Add(component.GetType().Name);
                    }
                }
            }

            foreach (Transform child in go.transform)
            {
                AnalyzeGameObject(child.gameObject, report);
            }
        }

        private void RemoveOpsiveObjects(ConversionReport report)
        {
            var objectsToRemove = new List<GameObject>();
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                if (ShouldRemoveObject(root, report))
                {
                    objectsToRemove.Add(root);
                    report.RemovedObjects.Add(root.name);
                }
                else
                {
                    // Check children and remove OPSIVE components
                    RemoveOpsiveComponentsRecursive(root, report);
                }
            }

            // Remove marked objects
            foreach (var obj in objectsToRemove)
            {
                DestroyImmediate(obj);
            }
        }

        private bool ShouldRemoveObject(GameObject go, ConversionReport report)
        {
            var name = go.name.ToLower();

            // Remove OPSIVE characters
            if (HasComponentByName(go, "UltimateCharacterLocomotion") ||
                HasComponentByName(go, "CharacterLocomotion"))
            {
                return true;
            }

            // Remove OPSIVE managers
            if (HasComponentByName(go, "SpawnManager") ||
                HasComponentByName(go, "ObjectPool") ||
                HasComponentByName(go, "StateManager") ||
                HasComponentByName(go, "SurfaceManager"))
            {
                return true;
            }

            // Remove OPSIVE spawners
            if (name.Contains("spawner") && HasAnyOpsiveComponent(go))
            {
                return true;
            }

            // Remove OPSIVE cameras
            if (name.Contains("camera") && HasComponentByName(go, "CameraController"))
            {
                return true;
            }

            // Remove OPSIVE UI
            if (HasComponentByName(go, "ItemMonitor") ||
                HasComponentByName(go, "HealthFlash"))
            {
                return true;
            }

            return false;
        }

        private void RemoveOpsiveComponentsRecursive(GameObject go, ConversionReport report)
        {
            var componentsToRemove = new List<Component>();

            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;

                var ns = component.GetType().Namespace ?? "";
                if (ns.StartsWith("Opsive"))
                {
                    componentsToRemove.Add(component);
                    report.RemovedComponents.Add($"{go.name}/{component.GetType().Name}");
                }
            }

            foreach (var component in componentsToRemove)
            {
                DestroyImmediate(component);
            }

            // Clean up missing scripts
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            // Process children
            foreach (Transform child in go.transform)
            {
                RemoveOpsiveComponentsRecursive(child.gameObject, report);
            }
        }

        private void ConvertTriggerZones(ConversionReport report)
        {
            var triggers = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.isTrigger)
                .ToList();

            foreach (var trigger in triggers)
            {
                var go = trigger.gameObject;

                // Check if it had OPSIVE components (already removed)
                // Add a placeholder component for ECS conversion
                if (!go.GetComponent<TriggerZoneMarker>())
                {
                    go.AddComponent<TriggerZoneMarker>();
                    report.ConvertedTriggers.Add(go.name);
                }
            }
        }

        private void AddDefaultSpawnPoint(ConversionReport report)
        {
            // Find a reasonable spawn location
            Vector3 spawnPos = Vector3.zero;

            // Try to find existing spawn points
            var existingSpawns = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.ToLower().Contains("spawn"))
                .ToList();

            if (existingSpawns.Count > 0)
            {
                spawnPos = existingSpawns[0].position;
            }
            else
            {
                // Try to find ground level
                if (Physics.Raycast(new Vector3(0, 100, 0), Vector3.down, out var hit, 200f))
                {
                    spawnPos = hit.point + Vector3.up * 0.5f;
                }
            }

            // Create spawn point object
            var spawnPoint = new GameObject("DIG_PlayerSpawn");
            spawnPoint.transform.position = spawnPos;
            spawnPoint.AddComponent<PlayerSpawnMarker>();

            report.AddedObjects.Add("DIG_PlayerSpawn");
        }

        private void AddSubsceneMarkers(ConversionReport report)
        {
            // Create a parent for static geometry
            var staticRoot = new GameObject("StaticGeometry_Subscene");
            staticRoot.AddComponent<SubSceneMarker>();

            // Find and parent all static renderers
            var staticRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.gameObject.isStatic)
                .Select(r => r.gameObject)
                .ToList();

            // Don't actually reparent - just mark the intent
            // Reparenting would break prefab references

            report.AddedObjects.Add("StaticGeometry_Subscene (marker)");
        }

        private void RemoveLightingData(ConversionReport report)
        {
            // Clear baked lighting references
            Lightmapping.Clear();
            report.Notes.Add("Cleared baked lighting data");
        }

        private void ScanForOpsiveScenes()
        {
            var guids = AssetDatabase.FindAssets("t:Scene");
            int found = 0;

            Debug.Log("[SceneConverter] Scanning for OPSIVE demo scenes...");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.ToLower().Contains("opsive") ||
                    path.ToLower().Contains("demo"))
                {
                    Debug.Log($"[SceneConverter] Found: {path}");
                    found++;
                }
            }

            Debug.Log($"[SceneConverter] Scan complete. Found {found} potential OPSIVE scenes.");
        }

        private void DrawConversionReport()
        {
            EditorGUILayout.LabelField("Conversion Report", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Source", _lastReport.SourceName);
            EditorGUILayout.LabelField("Status", _lastReport.Success ? "Success" : "Failed");

            if (!_lastReport.Success)
            {
                EditorGUILayout.HelpBox(_lastReport.ErrorMessage, MessageType.Error);
            }
            else
            {
                EditorGUILayout.LabelField("Output", _lastReport.OutputPath);
            }

            EditorGUILayout.Space(5);

            if (_lastReport.OpsiveComponentTypes.Count > 0)
            {
                EditorGUILayout.LabelField($"OPSIVE Components Found: {_lastReport.OpsiveComponentTypes.Count}");
            }

            if (_lastReport.RemovedObjects.Count > 0)
            {
                EditorGUILayout.LabelField($"Objects Removed: {_lastReport.RemovedObjects.Count}");
            }

            if (_lastReport.RemovedComponents.Count > 0)
            {
                EditorGUILayout.LabelField($"Components Removed: {_lastReport.RemovedComponents.Count}");
            }

            if (_lastReport.ConvertedTriggers.Count > 0)
            {
                EditorGUILayout.LabelField($"Trigger Zones Marked: {_lastReport.ConvertedTriggers.Count}");
            }

            if (_lastReport.AddedObjects.Count > 0)
            {
                EditorGUILayout.LabelField($"Objects Added: {string.Join(", ", _lastReport.AddedObjects)}");
            }

            foreach (var note in _lastReport.Notes)
            {
                EditorGUILayout.HelpBox(note, MessageType.Info);
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.EndScrollView();

            if (_lastReport.Success)
            {
                if (GUILayout.Button("Open Converted Scene"))
                {
                    EditorSceneManager.OpenScene(_lastReport.OutputPath);
                }
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private void CreateFolderRecursive(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private bool HasComponentByName(GameObject go, string typeName)
        {
            foreach (var component in go.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (component.GetType().Name == typeName)
                    return true;
            }
            return false;
        }

        private bool HasAnyOpsiveComponent(GameObject go)
        {
            foreach (var component in go.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var ns = component.GetType().Namespace ?? "";
                if (ns.StartsWith("Opsive"))
                    return true;
            }
            return false;
        }

        private string GetProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return "";
            
            absolutePath = absolutePath.Replace("\\", "/");
            if (absolutePath.StartsWith(Application.dataPath))
            {
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
                
            var projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath)?.Replace("\\", "/");
            if (projectRoot != null && absolutePath.StartsWith(projectRoot))
            {
                return absolutePath.Substring(projectRoot.Length + 1);
            }
                
            return absolutePath;
        }

        // ============================================
        // DATA STRUCTURES
        // ============================================

        private class ConversionReport
        {
            public string SourcePath;
            public string SourceName;
            public string OutputPath;
            public bool Success;
            public string ErrorMessage;
            public List<string> OpsiveComponentTypes = new List<string>();
            public List<string> RemovedObjects = new List<string>();
            public List<string> RemovedComponents = new List<string>();
            public List<string> ConvertedTriggers = new List<string>();
            public List<string> AddedObjects = new List<string>();
            public List<string> Notes = new List<string>();
        }
    }

    /// <summary>
    /// Marker component for trigger zones that need ECS conversion.
    /// </summary>
    public class TriggerZoneMarker : MonoBehaviour
    {
        [Tooltip("Type of trigger zone for ECS conversion")]
        public TriggerZoneType ZoneType = TriggerZoneType.Generic;

        [Tooltip("Custom data for this trigger")]
        public string CustomData;
    }

    public enum TriggerZoneType
    {
        Generic,
        ItemPickup,
        DamageZone,
        HealZone,
        Teleport,
        Objective
    }

    /// <summary>
    /// Marker for player spawn points.
    /// </summary>
    public class PlayerSpawnMarker : MonoBehaviour
    {
        [Tooltip("Team ID for this spawn point (0 = any)")]
        public int TeamId = 0;

        [Tooltip("Priority of this spawn point")]
        public int Priority = 0;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }

    /// <summary>
    /// Marker for subscene boundaries.
    /// </summary>
    public class SubSceneMarker : MonoBehaviour
    {
        [Tooltip("Name for the subscene when baked")]
        public string SubSceneName = "StaticGeometry";

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 5f);
        }
    }
}
