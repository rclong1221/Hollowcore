#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Entities;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.7: Live Tuning Overlay module.
    /// Play-mode only. Modifies ECS singletons in real-time via editor sliders.
    /// All overrides are transient — they don't modify ScriptableObjects.
    /// Overrides auto-clear on play mode exit.
    /// </summary>
    public class LiveTuningModule : IRunWorkstationModule
    {
        public string TabName => "Live Tuning";

        private Vector2 _scrollPos;
        private float _difficultyOverride;
        private float _spawnRateOverride;
        private bool _pauseSpawning;
        private bool _hasOverrides;
        private bool _showZoneInspector = true;
        private bool _showPhaseControls = true;
        private bool _showCurrencyControls = true;

        // Cached EntityQueries — recreated only when world changes
        private World _cachedWorld;
        private EntityQuery _zoneStateQuery;
        private EntityQuery _runStateQuery;
        private EntityQuery _metaBankQuery;
        private EntityQuery _overridesQuery;
        private EntityQuery _difficultyQuery;
        private bool _queriesCreated;

        public void OnEnable() { }

        public void OnDisable()
        {
            _difficultyOverride = 0f;
            _spawnRateOverride = 0f;
            _pauseSpawning = false;
            _hasOverrides = false;
            DisposeQueries();
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Live Tuning Overlay", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Live Tuning is only available during Play Mode.\n" +
                    "Enter Play Mode to adjust difficulty, spawn rates, currency, and phase in real-time.",
                    MessageType.Info);
                DisposeQueries();
                return;
            }

            var world = GetServerWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No active server/local world found.", MessageType.Warning);
                DisposeQueries();
                return;
            }

            EnsureQueries(world);

            // Override warning
            if (_hasOverrides)
            {
                GUI.backgroundColor = new Color(1f, 0.9f, 0.3f);
                EditorGUILayout.HelpBox("OVERRIDES ACTIVE — values shown in yellow are being overridden.", MessageType.Warning);
                GUI.backgroundColor = Color.white;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Zone State Inspector (read-only)
            _showZoneInspector = EditorGUILayout.Foldout(_showZoneInspector, "Zone State (Read-Only)", true);
            if (_showZoneInspector)
                DrawZoneStateInspector();

            EditorGUILayout.Space(8);

            // Difficulty slider
            DrawDifficultyControls();

            EditorGUILayout.Space(8);

            // Spawn controls
            DrawSpawnControls();

            EditorGUILayout.Space(8);

            // Apply overrides once per frame (not per section)
            ApplyOverrides();

            // Currency controls
            _showCurrencyControls = EditorGUILayout.Foldout(_showCurrencyControls, "Currency", true);
            if (_showCurrencyControls)
                DrawCurrencyControls();

            EditorGUILayout.Space(8);

            // Phase controls
            _showPhaseControls = EditorGUILayout.Foldout(_showPhaseControls, "Phase Control", true);
            if (_showPhaseControls)
                DrawPhaseControls();

            EditorGUILayout.Space(8);

            // Reset button
            if (GUILayout.Button("Reset All Overrides", GUILayout.Height(24)))
                ResetOverrides();

            EditorGUILayout.EndScrollView();
        }

        // ==================== Query Cache ====================

        private void EnsureQueries(World world)
        {
            if (_cachedWorld == world && _queriesCreated) return;

            DisposeQueries();
            _cachedWorld = world;
            var em = world.EntityManager;
            _zoneStateQuery = em.CreateEntityQuery(typeof(ZoneState));
            _runStateQuery = em.CreateEntityQuery(typeof(RunState));
            _metaBankQuery = em.CreateEntityQuery(typeof(MetaBank));
            _overridesQuery = em.CreateEntityQuery(typeof(LiveTuningOverrides));
            _difficultyQuery = em.CreateEntityQuery(typeof(RuntimeDifficultyScale));
            _queriesCreated = true;
        }

        private void DisposeQueries()
        {
            if (!_queriesCreated) { _cachedWorld = null; return; }
            _zoneStateQuery.Dispose();
            _runStateQuery.Dispose();
            _metaBankQuery.Dispose();
            _overridesQuery.Dispose();
            _difficultyQuery.Dispose();
            _queriesCreated = false;
            _cachedWorld = null;
        }

        // ==================== Zone State Inspector ====================

        private void DrawZoneStateInspector()
        {
            EditorGUI.indentLevel++;

            if (_zoneStateQuery.CalculateEntityCount() != 1)
            {
                EditorGUILayout.LabelField("No ZoneState singleton.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            var zone = _zoneStateQuery.GetSingleton<ZoneState>();
            EditorGUILayout.LabelField($"Zone Index: {zone.ZoneIndex}");
            EditorGUILayout.LabelField($"Type: {zone.Type}  |  Clear: {zone.ClearMode}");
            EditorGUILayout.LabelField($"Time: {zone.TimeInZone:F1}s  |  Difficulty: {zone.EffectiveDifficulty:F2}x");
            EditorGUILayout.LabelField($"Enemies: {zone.EnemiesAlive} alive / {zone.EnemiesSpawned} spawned / {zone.EnemiesKilled} killed");
            EditorGUILayout.LabelField($"Budget: {zone.SpawnBudget:F0}  |  Cleared: {zone.IsCleared}");

            EditorGUI.indentLevel--;
        }

        // ==================== Difficulty ====================

        private void DrawDifficultyControls()
        {
            EditorGUILayout.LabelField("Difficulty Override", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            Color origColor = GUI.color;
            if (_difficultyOverride > 0f) GUI.color = Color.yellow;

            _difficultyOverride = EditorGUILayout.Slider("Multiplier", _difficultyOverride, 0f, 10f);
            EditorGUILayout.HelpBox("0 = no override (use normal calculation). > 0 = force this multiplier.", MessageType.None);

            GUI.color = origColor;
            EditorGUI.indentLevel--;
        }

        // ==================== Spawn Controls ====================

        private void DrawSpawnControls()
        {
            EditorGUILayout.LabelField("Spawn Controls", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            Color origColor = GUI.color;
            if (_pauseSpawning || _spawnRateOverride > 0f) GUI.color = Color.yellow;

            _pauseSpawning = EditorGUILayout.Toggle("Pause Spawning", _pauseSpawning);
            _spawnRateOverride = EditorGUILayout.Slider("Budget Override", _spawnRateOverride, 0f, 2000f);
            EditorGUILayout.HelpBox("0 = no override. > 0 = set spawn budget to this value.", MessageType.None);

            GUI.color = origColor;
            EditorGUI.indentLevel--;
        }

        // ==================== Currency ====================

        private void DrawCurrencyControls()
        {
            EditorGUI.indentLevel++;

            if (_runStateQuery.CalculateEntityCount() == 1)
            {
                var rs = _runStateQuery.GetSingleton<RunState>();
                EditorGUILayout.LabelField($"Run Currency: {rs.RunCurrency}");
            }
            if (_metaBankQuery.CalculateEntityCount() == 1)
            {
                var mb = _metaBankQuery.GetSingleton<MetaBank>();
                EditorGUILayout.LabelField($"Meta Currency: {mb.MetaCurrency}");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+100 Run"))
                GrantCurrency(100, 0);
            if (GUILayout.Button("+500 Run"))
                GrantCurrency(500, 0);
            if (GUILayout.Button("+100 Meta"))
                GrantCurrency(0, 100);
            if (GUILayout.Button("+1000 Meta"))
                GrantCurrency(0, 1000);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        // ==================== Phase Controls ====================

        private void DrawPhaseControls()
        {
            EditorGUI.indentLevel++;

            if (_runStateQuery.CalculateEntityCount() == 1)
            {
                var rs = _runStateQuery.GetSingleton<RunState>();
                EditorGUILayout.LabelField($"Current Phase: {rs.Phase}");
                EditorGUILayout.LabelField($"Zone: {rs.CurrentZoneIndex}/{rs.MaxZones}  |  Score: {rs.Score}");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Skip to Next Zone", EditorStyles.miniButton))
                ForcePhase(RunPhase.ZoneTransition);
            if (GUILayout.Button("Trigger Boss", EditorStyles.miniButton))
                ForcePhase(RunPhase.BossEncounter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("End Run (Win)", EditorStyles.miniButton))
                ForcePhase(RunPhase.RunEnd);
            if (GUILayout.Button("Meta Screen", EditorStyles.miniButton))
                ForcePhase(RunPhase.MetaScreen);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        // ==================== ECS Bridge ====================

        private void ApplyOverrides()
        {
            if (_cachedWorld == null || !_cachedWorld.IsCreated) return;
            var em = _cachedWorld.EntityManager;

            if (_overridesQuery.CalculateEntityCount() == 0)
            {
                var entity = em.CreateEntity(typeof(LiveTuningOverrides));
                em.SetName(entity, "LiveTuningOverrides");
            }

            var overrides = _overridesQuery.GetSingleton<LiveTuningOverrides>();
            overrides.DifficultyMultiplierOverride = _difficultyOverride;
            overrides.SpawnRateOverride = _spawnRateOverride;
            overrides.PauseSpawning = _pauseSpawning ? (byte)1 : (byte)0;
            _overridesQuery.SetSingleton(overrides);

            _hasOverrides = _difficultyOverride > 0f || _spawnRateOverride > 0f || _pauseSpawning;
        }

        private void GrantCurrency(int run, int meta)
        {
            if (_overridesQuery.CalculateEntityCount() != 1) return;

            var overrides = _overridesQuery.GetSingleton<LiveTuningOverrides>();
            overrides.GrantRunCurrency += run;
            overrides.GrantMetaCurrency += meta;
            _overridesQuery.SetSingleton(overrides);
        }

        private void ForcePhase(RunPhase phase)
        {
            if (_overridesQuery.CalculateEntityCount() != 1) return;

            var overrides = _overridesQuery.GetSingleton<LiveTuningOverrides>();
            overrides.ForcePhase = phase;
            _overridesQuery.SetSingleton(overrides);
        }

        private void ResetOverrides()
        {
            _difficultyOverride = 0f;
            _spawnRateOverride = 0f;
            _pauseSpawning = false;
            _hasOverrides = false;

            if (_queriesCreated && _overridesQuery.CalculateEntityCount() == 1)
                _overridesQuery.SetSingleton(new LiveTuningOverrides());
        }

        // ==================== Helpers ====================

        private static World GetServerWorld()
        {
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;
                if (world.Name.Contains("Server") || world.Name.Contains("Local"))
                    return world;
            }
            return World.DefaultGameObjectInjectionWorld;
        }
    }
}
#endif
