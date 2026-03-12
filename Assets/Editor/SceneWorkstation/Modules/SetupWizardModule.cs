using System.IO;
using UnityEditor;
using UnityEngine;

namespace DIG.SceneManagement.Editor.Modules
{
    /// <summary>
    /// EPIC 18.6: One-click setup wizard for the Scene Management system.
    /// Creates GameFlowDefinitionSO, LoadingScreenProfileSO, SceneDefinitionSOs,
    /// and the LoadingScreenManager prefab — all from within the workstation.
    /// </summary>
    public class SetupWizardModule : ISceneModule
    {
        // Paths
        private const string ResourcesDir = "Assets/Resources";
        private const string ConfigDir = "Assets/Resources/SceneManagement";
        private const string PrefabDir = "Assets/Prefabs/SceneManagement";
        private const string FlowDefPath = "Assets/Resources/GameFlowDefinition.asset";
        private const string DefaultProfilePath = "Assets/Resources/DefaultLoadingScreen.asset";

        // Cached status — refreshed on focus, Refresh button, or after asset creation (not every repaint)
        private bool _hasFlowDef;
        private bool _hasDefaultProfile;
        private bool _hasLoadingPrefab;
        private GameFlowDefinitionSO _flowDef;
        private LoadingScreenProfileSO _defaultProfile;
        private Vector2 _scrollPos;
        private double _lastRefreshTime = -1; // -1 = never refreshed
        private const double RefreshIntervalSeconds = 2.0;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Use this wizard to create the required assets for the Scene Management system. " +
                "Green items are ready. Red items need to be created.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Status", GUILayout.Width(120)))
                RefreshStatus();
            else if (_lastRefreshTime < 0 || EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshIntervalSeconds)
                RefreshStatus();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // === 1. GameFlowDefinitionSO ===
            DrawStatusHeader("Game Flow Definition", _hasFlowDef,
                "Resources/GameFlowDefinition");

            if (_hasFlowDef)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.ObjectField("Asset", _flowDef, typeof(GameFlowDefinitionSO), false);
                int stateCount = _flowDef.States != null ? _flowDef.States.Length : 0;
                int transCount = _flowDef.Transitions != null ? _flowDef.Transitions.Length : 0;
                EditorGUILayout.LabelField("States", stateCount.ToString());
                EditorGUILayout.LabelField("Transitions", transCount.ToString());
                EditorGUI.indentLevel--;
            }
            else
            {
                if (GUILayout.Button("Create Game Flow Definition", GUILayout.Height(24)))
                    CreateFlowDefinition();
            }

            EditorGUILayout.Space(6);

            // === 2. Default Loading Screen Profile ===
            DrawStatusHeader("Default Loading Screen Profile", _hasDefaultProfile,
                "Resources/DefaultLoadingScreen");

            if (_hasDefaultProfile)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.ObjectField("Asset", _defaultProfile, typeof(LoadingScreenProfileSO), false);
                EditorGUI.indentLevel--;
            }
            else
            {
                if (GUILayout.Button("Create Default Loading Screen Profile", GUILayout.Height(24)))
                    CreateDefaultLoadingProfile();
            }

            EditorGUILayout.Space(6);

            // === 3. Loading Screen Prefab ===
            DrawStatusHeader("Loading Screen Prefab", _hasLoadingPrefab,
                "Prefabs/SceneManagement/LoadingScreen");

            if (!_hasLoadingPrefab)
            {
                if (GUILayout.Button("Create Loading Screen Prefab", GUILayout.Height(24)))
                    CreateLoadingScreenPrefab();
            }
            else
            {
                EditorGUI.indentLevel++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabDir + "/LoadingScreen.prefab");
                EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            // === 4. Scene Definition Assets ===
            EditorGUILayout.LabelField("Scene Definitions", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawSceneDefCreator("MainMenu", "Main menu scene");
            DrawSceneDefCreator("Lobby", "Lobby / matchmaking scene");
            DrawSceneDefCreator("Gameplay", "Core gameplay scene (SubScene mode)");

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(12);

            // === Create All button ===
            int missing = (_hasFlowDef ? 0 : 1) + (_hasDefaultProfile ? 0 : 1) + (_hasLoadingPrefab ? 0 : 1);
            EditorGUI.BeginDisabledGroup(missing == 0);
            if (GUILayout.Button($"Create All Missing ({missing} remaining)", GUILayout.Height(32)))
                CreateAll();
            EditorGUI.EndDisabledGroup();

            if (missing == 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("All core assets are created. " +
                    "You can now configure states, transitions, and loading screen profiles " +
                    "using the other workstation tabs.", MessageType.None);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }

        // ========== STATUS ==========

        private void RefreshStatus()
        {
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            _flowDef = Resources.Load<GameFlowDefinitionSO>("GameFlowDefinition");
            _hasFlowDef = _flowDef != null;

            _defaultProfile = Resources.Load<LoadingScreenProfileSO>("DefaultLoadingScreen");
            _hasDefaultProfile = _defaultProfile != null;

            _hasLoadingPrefab = File.Exists(
                Path.Combine(Application.dataPath, "../", PrefabDir, "LoadingScreen.prefab"));
        }

        private static void DrawStatusHeader(string label, bool exists, string location)
        {
            EditorGUILayout.BeginHorizontal();
            var icon = exists ? EditorGUIUtility.IconContent("TestPassed")
                              : EditorGUIUtility.IconContent("TestFailed");
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(location, EditorStyles.miniLabel, GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();
        }

        // ========== CREATORS ==========

        private void CreateFlowDefinition()
        {
            EnsureDirectory(ResourcesDir);

            var flowDef = ScriptableObject.CreateInstance<GameFlowDefinitionSO>();
            flowDef.FlowName = "Default";
            flowDef.InitialState = "MainMenu";

            // Default states
            flowDef.States = new[]
            {
                new GameFlowState
                {
                    StateId = "MainMenu",
                    InputContext = DIG.Core.Input.InputContext.UI,
                    RequiresNetwork = false
                },
                new GameFlowState
                {
                    StateId = "Lobby",
                    InputContext = DIG.Core.Input.InputContext.UI,
                    RequiresNetwork = false
                },
                new GameFlowState
                {
                    StateId = "Gameplay",
                    InputContext = DIG.Core.Input.InputContext.Gameplay,
                    RequiresNetwork = true
                }
            };

            // Default transitions
            flowDef.Transitions = new[]
            {
                new GameFlowTransition
                {
                    FromState = "MainMenu",
                    ToState = "Lobby",
                    Condition = TransitionCondition.Event,
                    TriggerEvent = "EnterLobby",
                    Animation = TransitionAnimation.Fade,
                    AnimationDuration = 0.5f
                },
                new GameFlowTransition
                {
                    FromState = "Lobby",
                    ToState = "Gameplay",
                    Condition = TransitionCondition.Event,
                    TriggerEvent = "StartGame",
                    Animation = TransitionAnimation.Fade,
                    AnimationDuration = 0.5f
                },
                new GameFlowTransition
                {
                    FromState = "Gameplay",
                    ToState = "Lobby",
                    Condition = TransitionCondition.Event,
                    TriggerEvent = "ReturnToLobby",
                    Animation = TransitionAnimation.Fade,
                    AnimationDuration = 0.5f
                },
                new GameFlowTransition
                {
                    FromState = "Lobby",
                    ToState = "MainMenu",
                    Condition = TransitionCondition.Event,
                    TriggerEvent = "LeaveLobby",
                    Animation = TransitionAnimation.Fade,
                    AnimationDuration = 0.3f
                }
            };

            flowDef.DefaultTransitionAnimation = TransitionAnimation.Fade;
            flowDef.DefaultTransitionDuration = 0.5f;

            AssetDatabase.CreateAsset(flowDef, FlowDefPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(flowDef);
            Debug.Log("[SceneWorkstation] Created GameFlowDefinition at " + FlowDefPath);
            RefreshStatus();
        }

        private void CreateDefaultLoadingProfile()
        {
            EnsureDirectory(ResourcesDir);

            var profile = ScriptableObject.CreateInstance<LoadingScreenProfileSO>();
            profile.ShowProgressBar = true;
            profile.ProgressBarStyle = ProgressBarStyle.Continuous;
            profile.MinDisplaySeconds = 1.0f;
            profile.FadeInDuration = 0.3f;
            profile.FadeOutDuration = 0.3f;
            profile.Tips = new[]
            {
                "Stick together with your party for bonus XP!",
                "Higher difficulty means better loot drops.",
                "Use the trade system to share gear with friends.",
                "Check your map for spawn point locations.",
                "Dig deeper to find rarer ores and materials."
            };

            AssetDatabase.CreateAsset(profile, DefaultProfilePath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(profile);
            Debug.Log("[SceneWorkstation] Created DefaultLoadingScreen at " + DefaultProfilePath);

            // Wire it into the flow definition if it exists
            var flowDef = Resources.Load<GameFlowDefinitionSO>("GameFlowDefinition");
            if (flowDef != null && flowDef.DefaultLoadingScreen == null)
            {
                flowDef.DefaultLoadingScreen = profile;
                EditorUtility.SetDirty(flowDef);
                AssetDatabase.SaveAssets();
            }
            RefreshStatus();
        }

        private void CreateLoadingScreenPrefab()
        {
            EnsureDirectory(PrefabDir);

            // Root: LoadingScreenManager
            var root = new GameObject("LoadingScreen");

            var manager = root.AddComponent<LoadingScreenManager>();

            // Canvas child
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(root.transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var canvasGroup = canvasGO.AddComponent<CanvasGroup>();

            // View component on canvas
            var view = canvasGO.AddComponent<UI.LoadingScreenView>();

            // Background image (full-screen)
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgImage = bgGO.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // Progress bar
            var progressGO = new GameObject("ProgressBar");
            progressGO.transform.SetParent(canvasGO.transform, false);
            var slider = progressGO.AddComponent<UnityEngine.UI.Slider>();
            slider.interactable = false;
            slider.transition = UnityEngine.UI.Selectable.Transition.None;
            var progressRT = progressGO.GetComponent<RectTransform>();
            progressRT.anchorMin = new Vector2(0.2f, 0.1f);
            progressRT.anchorMax = new Vector2(0.8f, 0.13f);
            progressRT.sizeDelta = Vector2.zero;

            // Slider background
            var sliderBgGO = new GameObject("Background");
            sliderBgGO.transform.SetParent(progressGO.transform, false);
            var sliderBgImg = sliderBgGO.AddComponent<UnityEngine.UI.Image>();
            sliderBgImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var sliderBgRT = sliderBgGO.GetComponent<RectTransform>();
            sliderBgRT.anchorMin = Vector2.zero;
            sliderBgRT.anchorMax = Vector2.one;
            sliderBgRT.sizeDelta = Vector2.zero;

            // Slider fill area (empty container — Image with clear color adds RectTransform for UI layout)
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(progressGO.transform, false);
            var fillAreaImg = fillAreaGO.AddComponent<UnityEngine.UI.Image>();
            fillAreaImg.color = Color.clear;
            var fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.sizeDelta = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillImg = fillGO.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = new Color(0.2f, 0.7f, 0.3f, 1f);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;

            slider.fillRect = fillRT;
            slider.targetGraphic = fillImg;

            // Phase text
            var phaseGO = new GameObject("PhaseText");
            phaseGO.transform.SetParent(canvasGO.transform, false);
            var phaseText = phaseGO.AddComponent<UnityEngine.UI.Text>();
            phaseText.text = "Loading...";
            phaseText.alignment = TextAnchor.MiddleCenter;
            phaseText.fontSize = 24;
            phaseText.color = Color.white;
            phaseText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var phaseRT = phaseGO.GetComponent<RectTransform>();
            phaseRT.anchorMin = new Vector2(0.2f, 0.45f);
            phaseRT.anchorMax = new Vector2(0.8f, 0.55f);
            phaseRT.sizeDelta = Vector2.zero;

            // Tip text
            var tipGO = new GameObject("TipText");
            tipGO.transform.SetParent(canvasGO.transform, false);
            var tipText = tipGO.AddComponent<UnityEngine.UI.Text>();
            tipText.text = "Tip: Dig deeper to find rarer ores.";
            tipText.alignment = TextAnchor.MiddleCenter;
            tipText.fontSize = 16;
            tipText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            tipText.fontStyle = FontStyle.Italic;
            tipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tipRT = tipGO.GetComponent<RectTransform>();
            tipRT.anchorMin = new Vector2(0.15f, 0.18f);
            tipRT.anchorMax = new Vector2(0.85f, 0.25f);
            tipRT.sizeDelta = Vector2.zero;

            // Wire serialized fields via SerializedObject
            string prefabPath = PrefabDir + "/LoadingScreen.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            // Reopen prefab to wire serialized fields
            WirePrefabReferences(prefabPath);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[SceneWorkstation] Created LoadingScreen prefab at " + prefabPath);
            RefreshStatus();
        }

        private static void WirePrefabReferences(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            // Wire LoadingScreenManager._view
            var manager = prefab.GetComponent<LoadingScreenManager>();
            var viewComp = prefab.GetComponentInChildren<UI.LoadingScreenView>();

            if (manager != null && viewComp != null)
            {
                var managerSO = new SerializedObject(manager);
                var viewProp = managerSO.FindProperty("_view");
                if (viewProp != null)
                {
                    viewProp.objectReferenceValue = viewComp;
                    managerSO.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Wire LoadingScreenView fields
            if (viewComp != null)
            {
                var viewSO = new SerializedObject(viewComp);

                var canvasGO = viewComp.gameObject;
                var canvasGroup = canvasGO.GetComponent<CanvasGroup>();
                SetField(viewSO, "_canvasGroup", canvasGroup);

                var bgImage = FindChildComponent<UnityEngine.UI.Image>(canvasGO, "Background");
                SetField(viewSO, "_backgroundImage", bgImage);

                var slider = FindChildComponent<UnityEngine.UI.Slider>(canvasGO, "ProgressBar");
                SetField(viewSO, "_progressBar", slider);

                if (slider != null)
                {
                    var fill = FindChildComponent<UnityEngine.UI.Image>(slider.gameObject, "Fill");
                    SetField(viewSO, "_progressBarFill", fill);
                }

                var phaseText = FindChildComponent<UnityEngine.UI.Text>(canvasGO, "PhaseText");
                SetField(viewSO, "_phaseText", phaseText);

                var tipText = FindChildComponent<UnityEngine.UI.Text>(canvasGO, "TipText");
                SetField(viewSO, "_tipText", tipText);

                viewSO.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(prefab);
        }

        private static void SetField(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.objectReferenceValue = value;
        }

        private static T FindChildComponent<T>(GameObject parent, string childName) where T : Component
        {
            var t = parent.transform.Find(childName);
            return t != null ? t.GetComponent<T>() : null;
        }

        private void DrawSceneDefCreator(string sceneId, string description)
        {
            string path = ConfigDir + "/" + sceneId + "SceneDef.asset";
            bool exists = File.Exists(Path.Combine(Application.dataPath, "../", path));

            EditorGUILayout.BeginHorizontal();
            var icon = exists ? EditorGUIUtility.IconContent("TestPassed")
                              : EditorGUIUtility.IconContent("TestFailed");
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
            EditorGUILayout.LabelField(sceneId, GUILayout.Width(100));
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);

            if (exists)
            {
                var asset = AssetDatabase.LoadAssetAtPath<SceneDefinitionSO>(path);
                EditorGUILayout.ObjectField(asset, typeof(SceneDefinitionSO), false, GUILayout.Width(150));
            }
            else
            {
                if (GUILayout.Button("Create", GUILayout.Width(60)))
                    CreateSceneDefinition(sceneId, path);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CreateSceneDefinition(string sceneId, string path)
        {
            EnsureDirectory(ConfigDir);

            var sceneDef = ScriptableObject.CreateInstance<SceneDefinitionSO>();
            sceneDef.SceneId = sceneId;
            sceneDef.DisplayName = sceneId;

            // Defaults per state type
            if (sceneId == "Gameplay")
            {
                sceneDef.LoadMode = SceneLoadMode.SubScene;
                sceneDef.MinLoadTimeSeconds = 1.0f;
            }
            else
            {
                sceneDef.LoadMode = SceneLoadMode.Single;
                sceneDef.MinLoadTimeSeconds = 0.5f;
            }

            AssetDatabase.CreateAsset(sceneDef, path);
            AssetDatabase.SaveAssets();

            // Auto-wire into flow definition if state matches
            var flowDef = Resources.Load<GameFlowDefinitionSO>("GameFlowDefinition");
            if (flowDef != null && flowDef.States != null)
            {
                for (int i = 0; i < flowDef.States.Length; i++)
                {
                    if (flowDef.States[i].StateId == sceneId && flowDef.States[i].Scene == null)
                    {
                        flowDef.States[i].Scene = sceneDef;
                        EditorUtility.SetDirty(flowDef);
                        AssetDatabase.SaveAssets();
                        break;
                    }
                }
            }

            EditorGUIUtility.PingObject(sceneDef);
            Debug.Log($"[SceneWorkstation] Created SceneDefinition '{sceneId}' at {path}");
            RefreshStatus();
        }

        private void CreateAll()
        {
            if (!_hasFlowDef) CreateFlowDefinition();
            if (!_hasDefaultProfile) CreateDefaultLoadingProfile();
            if (!_hasLoadingPrefab) CreateLoadingScreenPrefab();

            // Create scene definitions for all states
            RefreshStatus();
            if (_flowDef != null && _flowDef.States != null)
            {
                for (int i = 0; i < _flowDef.States.Length; i++)
                {
                    var state = _flowDef.States[i];
                    if (state.Scene != null) continue;

                    string path = ConfigDir + "/" + state.StateId + "SceneDef.asset";
                    if (!File.Exists(Path.Combine(Application.dataPath, "../", path)))
                        CreateSceneDefinition(state.StateId, path);
                }
            }

            AssetDatabase.Refresh();
        }

        // ========== UTILITIES ==========

        private static void EnsureDirectory(string path)
        {
            // Split path and create folders as needed
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
