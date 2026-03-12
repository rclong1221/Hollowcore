#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Scenes;
using DIG.Swarm.Authoring;
using DIG.Swarm.Components;
using DIG.Swarm.Rendering;

namespace DIG.Swarm.Editor
{
    /// <summary>
    /// EPIC 16.2 Phase 8: One-click swarm setup.
    /// Auto-finds BoxingJoe prefab/mesh, creates all required objects in the correct places.
    ///
    /// Menu: DIG > Swarm > Setup Wizard
    /// </summary>
    public class SwarmSetupWizard : EditorWindow
    {
        // Auto-detected assets
        private GameObject _combatPrefab;
        private Mesh _swarmMesh;

        // Flow field
        private int _gridWidth = 100;
        private int _gridHeight = 100;
        private float _cellSize = 2f;

        // Spawner
        private SwarmSpawnMode _spawnMode = SwarmSpawnMode.Continuous;
        private int _targetPopulation = 1000;
        private float _spawnRate = 200f;
        private int _batchSize = 250;

        // Config
        private float _baseSpeed = 3.5f;
        private int _maxCombatEntities = 20;
        private int _maxAwareEntities = 100;

        private Vector2 _scrollPos;

        [MenuItem("DIG/Swarm/Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<SwarmSetupWizard>("Swarm Setup");
            window.minSize = new Vector2(420, 550);
        }

        private void OnEnable()
        {
            FindAssets();
        }

        private void FindAssets()
        {
            // Find BoxingJoe combat prefab
            if (_combatPrefab == null)
            {
                // Prefer the ECS-converted version, fall back to regular
                string[] ecsGuids = AssetDatabase.FindAssets("BoxingJoe_ECS t:Prefab");
                if (ecsGuids.Length > 0)
                {
                    _combatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        AssetDatabase.GUIDToAssetPath(ecsGuids[0]));
                }

                if (_combatPrefab == null)
                {
                    // Look in project Prefabs folder first (not OPSIVE samples)
                    string[] guids = AssetDatabase.FindAssets("BoxingJoe t:Prefab", new[] { "Assets/Prefabs" });
                    if (guids.Length > 0)
                    {
                        _combatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                            AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }
            }

            // Find BoxingJoe mesh from FBX — always re-detect to pick the LARGEST mesh (body, not hat/accessory)
            {
                string[] fbxGuids = AssetDatabase.FindAssets("BoxingJoe t:Model");
                foreach (string guid in fbxGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    Mesh bestMesh = null;
                    int bestVertCount = 0;
                    foreach (var asset in assets)
                    {
                        if (asset is Mesh mesh && mesh.vertexCount > bestVertCount)
                        {
                            bestVertCount = mesh.vertexCount;
                            bestMesh = mesh;
                        }
                    }
                    if (bestMesh != null)
                    {
                        _swarmMesh = bestMesh;
                        break;
                    }
                }
            }

            // Fallback: try to get mesh from the combat prefab's MeshFilter/SkinnedMeshRenderer
            if (_swarmMesh == null && _combatPrefab != null)
            {
                var smr = _combatPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                    _swarmMesh = smr.sharedMesh;

                if (_swarmMesh == null)
                {
                    var mf = _combatPrefab.GetComponentInChildren<MeshFilter>();
                    if (mf != null)
                        _swarmMesh = mf.sharedMesh;
                }
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Swarm Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // --- Assets ---
            EditorGUILayout.LabelField("Detected Assets", EditorStyles.boldLabel);

            _combatPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Combat Prefab", _combatPrefab, typeof(GameObject), false);

            _swarmMesh = (Mesh)EditorGUILayout.ObjectField(
                "Swarm Mesh", _swarmMesh, typeof(Mesh), false);

            if (_combatPrefab != null)
                EditorGUILayout.HelpBox($"Combat Prefab: {_combatPrefab.name}", MessageType.Info);
            else
                EditorGUILayout.HelpBox("No combat prefab found. Promoted swarm entities won't spawn.", MessageType.Warning);

            if (_swarmMesh != null)
                EditorGUILayout.HelpBox($"Mesh: {_swarmMesh.name} ({_swarmMesh.vertexCount} verts)", MessageType.Info);
            else
                EditorGUILayout.HelpBox("No mesh found. Will use capsule placeholder.", MessageType.Warning);

            EditorGUILayout.Space(10);

            // --- Flow Field ---
            EditorGUILayout.LabelField("Flow Field", EditorStyles.boldLabel);
            _gridWidth = EditorGUILayout.IntSlider("Grid Width", _gridWidth, 50, 500);
            _gridHeight = EditorGUILayout.IntSlider("Grid Height", _gridHeight, 50, 500);
            _cellSize = EditorGUILayout.Slider("Cell Size (m)", _cellSize, 0.5f, 5f);

            float worldW = _gridWidth * _cellSize;
            float worldH = _gridHeight * _cellSize;
            EditorGUILayout.HelpBox($"Coverage: {worldW:F0}m x {worldH:F0}m", MessageType.None);

            EditorGUILayout.Space(10);

            // --- Spawner ---
            EditorGUILayout.LabelField("Spawner", EditorStyles.boldLabel);
            _spawnMode = (SwarmSpawnMode)EditorGUILayout.EnumPopup("Spawn Mode", _spawnMode);
            _targetPopulation = EditorGUILayout.IntField("Target Population", _targetPopulation);
            _targetPopulation = Mathf.Max(100, _targetPopulation);
            _spawnRate = EditorGUILayout.Slider("Spawn Rate (/sec)", _spawnRate, 50f, 5000f);
            _batchSize = EditorGUILayout.IntSlider("Batch Size", _batchSize, 100, 5000);

            EditorGUILayout.Space(10);

            // --- Config ---
            EditorGUILayout.LabelField("Swarm Config", EditorStyles.boldLabel);
            _baseSpeed = EditorGUILayout.Slider("Base Speed (m/s)", _baseSpeed, 1f, 10f);
            _maxCombatEntities = EditorGUILayout.IntSlider("Max Combat Entities", _maxCombatEntities, 10, 500);
            _maxAwareEntities = EditorGUILayout.IntSlider("Max Aware Entities", _maxAwareEntities, 50, 2000);

            EditorGUILayout.Space(20);

            // --- Create ---
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("Create Swarm Setup", GUILayout.Height(40)))
                CreateSwarmSetup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Creates:\n" +
                "- FlowField, SwarmConfig, SwarmSpawner in active subscene (ECS baking)\n" +
                "- SwarmRenderConfig in scene root (MonoBehaviour singleton)\n" +
                "- Auto-wires BoxingJoe mesh + instancing material",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private void CreateSwarmSetup()
        {
            // Find the active subscene
            SubScene targetSubscene = null;
            var subscenes = Object.FindObjectsByType<SubScene>(FindObjectsSortMode.None);
            foreach (var sub in subscenes)
            {
                if (sub.gameObject.activeInHierarchy)
                {
                    targetSubscene = sub;
                    break;
                }
            }

            if (targetSubscene == null)
            {
                EditorUtility.DisplayDialog("No Subscene Found",
                    "No active SubScene found in the scene.\n\n" +
                    "Create a SubScene first, then run this wizard again.\n" +
                    "The FlowField, SwarmConfig, and SwarmSpawner need to be in a SubScene for ECS baking.",
                    "OK");
                return;
            }

            // Check if subscene is editable
            var subsceneScene = targetSubscene.EditingScene;
            if (!subsceneScene.IsValid() || !subsceneScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Subscene Not Editable",
                    $"The subscene '{targetSubscene.name}' is not open for editing.\n\n" +
                    "Open it by clicking 'Open' on the SubScene component, then run this wizard again.",
                    "OK");
                return;
            }

            Undo.SetCurrentGroupName("Create Swarm Setup");
            int undoGroup = Undo.GetCurrentGroup();

            // Check for existing swarm objects and remove them
            CleanExistingSwarmObjects(subsceneScene);

            // Create instancing material from prefab's material
            Material instanceMat = CreateInstancingMaterial();

            // --- ECS objects (inside subscene) ---

            // 1. Flow Field
            var flowFieldGO = new GameObject("SwarmFlowField");
            EditorSceneManager.MoveGameObjectToScene(flowFieldGO, subsceneScene);
            Undo.RegisterCreatedObjectUndo(flowFieldGO, "Create FlowField");
            var flowField = flowFieldGO.AddComponent<FlowFieldAuthoring>();
            flowField.GridWidth = _gridWidth;
            flowField.GridHeight = _gridHeight;
            flowField.CellSize = _cellSize;
            flowField.UpdateInterval = 0.5f;

            // 2. Swarm Config
            var configGO = new GameObject("SwarmConfig");
            EditorSceneManager.MoveGameObjectToScene(configGO, subsceneScene);
            Undo.RegisterCreatedObjectUndo(configGO, "Create SwarmConfig");
            var config = configGO.AddComponent<SwarmConfigAuthoring>();
            config.BaseSpeed = _baseSpeed;
            config.CombatPrefab = _combatPrefab;
            config.MaxCombatEntities = _maxCombatEntities;
            config.MaxAwareEntities = _maxAwareEntities;

            // 3. Spawner
            var spawnerGO = new GameObject("SwarmSpawner");
            EditorSceneManager.MoveGameObjectToScene(spawnerGO, subsceneScene);
            Undo.RegisterCreatedObjectUndo(spawnerGO, "Create SwarmSpawner");
            var spawner = spawnerGO.AddComponent<SwarmSpawnerAuthoring>();
            spawner.Mode = _spawnMode;
            spawner.TotalParticles = _targetPopulation;
            spawner.TargetPopulation = _targetPopulation;
            spawner.SpawnRate = _spawnRate;
            spawner.BatchSize = _batchSize;
            spawner.SpawnOnStart = true;
            spawner.EdgeInset = 5f;

            // --- Managed objects (outside subscene, in main scene) ---

            // 4. Render Config (MonoBehaviour singleton)
            // Check if one already exists
            var existingRenderConfig = Object.FindFirstObjectByType<SwarmRenderConfigManaged>();
            if (existingRenderConfig == null)
            {
                var renderGO = new GameObject("SwarmRenderConfig");
                Undo.RegisterCreatedObjectUndo(renderGO, "Create SwarmRenderConfig");
                var renderConfig = renderGO.AddComponent<SwarmRenderConfigManaged>();
                renderConfig.FullMesh = _swarmMesh;
                renderConfig.SwarmMaterial = instanceMat;
                renderConfig.MaxRenderDistance = 200f;
                renderConfig.LODDistance1 = 30f;
                renderConfig.LODDistance2 = 80f;
                renderConfig.ShadowDistance = 30f;
                renderConfig.CastShadows = true;
            }
            else
            {
                // Always update mesh and material to latest detected values
                if (_swarmMesh != null)
                    existingRenderConfig.FullMesh = _swarmMesh;
                if (instanceMat != null)
                    existingRenderConfig.SwarmMaterial = instanceMat;
                EditorUtility.SetDirty(existingRenderConfig);
            }

            Undo.CollapseUndoOperations(undoGroup);

            // Mark subscene dirty
            EditorSceneManager.MarkSceneDirty(subsceneScene);

            Selection.activeGameObject = spawnerGO;

            Debug.Log($"[Swarm Setup] Created in subscene '{targetSubscene.name}':" +
                      $"\n  FlowField: {_gridWidth}x{_gridHeight} @ {_cellSize}m ({_gridWidth * _cellSize}m x {_gridHeight * _cellSize}m)" +
                      $"\n  Spawner: {_spawnMode} mode, target {_targetPopulation}, rate {_spawnRate}/s" +
                      $"\n  Combat Prefab: {(_combatPrefab != null ? _combatPrefab.name : "none")}" +
                      $"\n  Mesh: {(_swarmMesh != null ? _swarmMesh.name : "capsule placeholder")}");
        }

        private void CleanExistingSwarmObjects(UnityEngine.SceneManagement.Scene subsceneScene)
        {
            // Remove existing swarm objects from subscene to avoid duplicates
            var roots = subsceneScene.GetRootGameObjects();
            foreach (var go in roots)
            {
                if (go.name == "SwarmFlowField" || go.name == "SwarmConfig" || go.name == "SwarmSpawner")
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }
        }

        private Material CreateInstancingMaterial()
        {
            // Try to get material from the combat prefab
            Material sourceMat = null;
            if (_combatPrefab != null)
            {
                var smr = _combatPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMaterial != null)
                    sourceMat = smr.sharedMaterial;

                if (sourceMat == null)
                {
                    var mr = _combatPrefab.GetComponentInChildren<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                        sourceMat = mr.sharedMaterial;
                }
            }

            if (sourceMat != null)
            {
                // Clone the material and enable instancing
                var mat = new Material(sourceMat);
                mat.name = "SwarmInstance_" + sourceMat.name;
                mat.enableInstancing = true;
                return mat;
            }

            // Fallback: create a basic lit instancing material
            var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallback.name = "SwarmInstance_Fallback";
            fallback.color = new Color(0.4f, 0.35f, 0.3f); // Muted brownish
            fallback.enableInstancing = true;
            return fallback;
        }
    }
}
#endif
