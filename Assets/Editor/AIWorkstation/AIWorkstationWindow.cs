using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using System.Collections.Generic;
using DIG.AI.Components;

namespace DIG.Editor.AIWorkstation
{
    /// <summary>
    /// AI Workstation: Unified debug and design tools for the enemy AI pipeline.
    /// Tabs: Brain Inspector, Dashboard, Overlay, Scene Tools.
    /// Shared entity selector toolbar feeds selected entity to all modules.
    /// </summary>
    public class AIWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private readonly string[] _tabs = { "Brain Inspector", "Dashboard", "Overlay", "Scene Tools" };

        private Dictionary<string, IAIWorkstationModule> _modules;
        private Vector2 _scrollPosition;

        // Entity selection state
        private Entity _selectedEntity = Entity.Null;
        private int _selectedEntityIndex;
        private bool _pickingEntity;
        private string _stateFilter = "All";
        private readonly string[] _stateFilterOptions = { "All", "Idle", "Patrol", "Combat", "ReturnHome", "Investigate", "Flee" };

        [MenuItem("DIG/AI Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<AIWorkstationWindow>("AI Workstation");
            window.minSize = new Vector2(700, 550);
        }

        private void OnEnable()
        {
            InitializeModules();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, IAIWorkstationModule>
            {
                { "Brain Inspector", new Modules.BrainInspectorModule() },
                { "Dashboard", new Modules.DashboardModule() },
                { "Overlay", new Modules.OverlaySettingsModule() },
                { "Scene Tools", new Modules.SceneToolsModule() }
            };
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawEntitySelector();
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(120), GUILayout.ExpandHeight(true));
            _selectedTab = GUILayout.SelectionGrid(_selectedTab, _tabs, 1, EditorStyles.miniButton);
            EditorGUILayout.EndVertical();

            // Content
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            string currentTabName = _tabs[_selectedTab];
            if (_modules != null && _modules.ContainsKey(currentTabName))
            {
                _modules[currentTabName].OnGUI();
            }
            else
            {
                EditorGUILayout.HelpBox($"Module '{currentTabName}' not initialized.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Repaint during play mode for live updates
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("DIG AI Workstation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
            {
                InitializeModules();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntitySelector()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Pick from scene button
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _pickingEntity ? Color.yellow : prevBg;
            if (GUILayout.Button(_pickingEntity ? "Click Scene..." : "Pick Entity", GUILayout.Width(100)))
            {
                _pickingEntity = !_pickingEntity;
            }
            GUI.backgroundColor = prevBg;

            // Manual entity index
            EditorGUILayout.LabelField("Entity:", GUILayout.Width(42));
            int newIndex = EditorGUILayout.IntField(_selectedEntityIndex, GUILayout.Width(60));
            if (newIndex != _selectedEntityIndex)
            {
                _selectedEntityIndex = newIndex;
                TrySelectEntityByIndex(newIndex);
            }

            // State filter
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(38));
            int filterIdx = System.Array.IndexOf(_stateFilterOptions, _stateFilter);
            int newFilterIdx = EditorGUILayout.Popup(filterIdx >= 0 ? filterIdx : 0, _stateFilterOptions, GUILayout.Width(90));
            _stateFilter = _stateFilterOptions[newFilterIdx];

            GUILayout.FlexibleSpace();

            // Status display
            if (_selectedEntity != Entity.Null)
            {
                var world = GetAIWorld();
                if (world != null && world.EntityManager.Exists(_selectedEntity))
                {
                    string stateName = "?";
                    if (world.EntityManager.HasComponent<AIState>(_selectedEntity))
                    {
                        var aiState = world.EntityManager.GetComponentData<AIState>(_selectedEntity);
                        stateName = aiState.CurrentState.ToString();
                    }
                    var stateColor = AIWorkstationStyles.GetStateColor(
                        world.EntityManager.HasComponent<AIState>(_selectedEntity)
                            ? world.EntityManager.GetComponentData<AIState>(_selectedEntity).CurrentState
                            : AIBehaviorState.Idle);
                    var prevColor = GUI.color;
                    GUI.color = stateColor;
                    EditorGUILayout.LabelField($"Entity {_selectedEntity.Index} [{stateName}]", EditorStyles.boldLabel, GUILayout.Width(180));
                    GUI.color = prevColor;
                }
                else
                {
                    EditorGUILayout.LabelField("Entity destroyed", EditorStyles.miniLabel, GUILayout.Width(100));
                    _selectedEntity = Entity.Null;
                }
            }
            else
            {
                EditorGUILayout.LabelField("No entity selected", EditorStyles.miniLabel, GUILayout.Width(120));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // Entity picking via scene click
            if (_pickingEntity && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                TryPickEntityFromScene(sceneView);
                Event.current.Use();
                _pickingEntity = false;
                Repaint();
            }

            // Delegate to active module for scene drawing
            if (_modules != null)
            {
                string currentTabName = _tabs[_selectedTab];
                if (_modules.ContainsKey(currentTabName))
                {
                    _modules[currentTabName].OnSceneGUI(sceneView);
                }
            }
        }

        private void TryPickEntityFromScene(SceneView sceneView)
        {
            var world = GetAIWorld();
            if (world == null) return;

            var em = world.EntityManager;
            var mousePos = Event.current.mousePosition;
            var ray = HandleUtility.GUIPointToWorldRay(mousePos);

            // Find closest AI entity to the ray
            Entity closest = Entity.Null;
            float closestDist = float.MaxValue;

            var query = em.CreateEntityQuery(typeof(AIState), typeof(LocalTransform));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                float3 pos = transforms[i].Position;
                float3 toEntity = pos - (float3)ray.origin;
                float projLen = math.dot(toEntity, (float3)ray.direction);
                if (projLen < 0f) continue;

                float3 closestPoint = (float3)ray.origin + (float3)ray.direction * projLen;
                float dist = math.distance(closestPoint, pos);

                if (dist < 2f && dist < closestDist)
                {
                    closestDist = dist;
                    closest = entities[i];
                }
            }

            entities.Dispose();
            transforms.Dispose();

            if (closest != Entity.Null)
            {
                SelectEntity(closest, em);
            }
        }

        private void TrySelectEntityByIndex(int index)
        {
            var world = GetAIWorld();
            if (world == null) return;

            // Create entity from index (version 1 — may need refinement)
            var entity = new Entity { Index = index, Version = 1 };
            if (world.EntityManager.Exists(entity) && world.EntityManager.HasComponent<AIState>(entity))
            {
                SelectEntity(entity, world.EntityManager);
            }
        }

        private void SelectEntity(Entity entity, EntityManager em)
        {
            _selectedEntity = entity;
            _selectedEntityIndex = entity.Index;

            if (_modules != null)
            {
                foreach (var module in _modules.Values)
                {
                    module.OnEntityChanged(entity, em);
                }
            }
        }

        /// <summary>
        /// Get the appropriate ECS World for AI queries.
        /// Prefers ServerSimulation (listen server), falls back to DefaultGameObjectInjectionWorld.
        /// </summary>
        public static World GetAIWorld()
        {
            if (!Application.isPlaying) return null;

            // Try server world first (listen server has full AI data)
            foreach (var world in World.All)
            {
                if (world.IsCreated && (world.Flags & WorldFlags.GameServer) != 0)
                    return world;
            }

            return World.DefaultGameObjectInjectionWorld;
        }

        /// <summary>Current selected entity — accessible by modules.</summary>
        public Entity SelectedEntity => _selectedEntity;
    }
}
