using UnityEditor;
using UnityEngine;
using Player.Systems;
using Unity.Entities;

namespace DIG.Editor
{
    public class CacheStatsWindow : EditorWindow
    {
        [MenuItem("Window/DIG/Cache Stats")]
        public static void ShowWindow() => GetWindow<CacheStatsWindow>("Cache Stats");

        private CharacterControllerSystem _cachedSystem;
        private Vector2 _scrollPos;

        CharacterControllerSystem GetSystem()
        {
            if (_cachedSystem == null || !_cachedSystem.World.IsCreated)
            {
                _cachedSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CharacterControllerSystem>();
            }
            return _cachedSystem;
        }

        private bool _autoRefresh = false;

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnInspectorUpdate()
        {
            // Only repaint periodically when auto-refresh is enabled and in play mode
            if (_autoRefresh && Application.isPlaying)
            {
                Repaint();
            }
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            _cachedSystem = null; // Clear cache on play mode change
            Repaint();
        }

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Capsule Cache Stats", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            
            var system = GetSystem();
            if (system == null)
            {
                GUILayout.Label("CharacterControllerSystem not found (not in Play mode?)");
                EditorGUILayout.EndScrollView();
                return;
            }
            
            var stats = system.GetCacheStats();
            long hits = stats.hits;
            long misses = stats.misses;
            GUILayout.Label($"Hits: {hits}");
            GUILayout.Label($"Misses: {misses}");
            float rate = (hits + misses) > 0 ? (float)hits / (hits + misses) : 0f;
            GUILayout.Label($"Hit Rate: {rate:P1}");

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Counters"))
            {
                system.ResetCacheStats();
            }
            if (GUILayout.Button("Clear Cache"))
            {
                system.ClearCapsuleCache();
            }
            if (GUILayout.Button("Reset (Clear + Counters)"))
            {
                system.ResetCache(true);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Copy to Clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = $"Capsule Cache - Hits:{hits} Misses:{misses} HitRate:{rate:P1}";
            }
            
            EditorGUILayout.EndScrollView();
        }
    }
}
