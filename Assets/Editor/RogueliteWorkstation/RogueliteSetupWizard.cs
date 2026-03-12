#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using DIG.Editor.Utilities;
using DIG.Roguelite;
using DIG.Roguelite.Rewards;
using DIG.Roguelite.Zones;

namespace DIG.Editor.Roguelite
{
    /// <summary>
    /// Setup wizard for the rogue-lite module.
    /// Validates setup, creates required assets, and provides one-click configuration
    /// for run lifecycle, meta-progression, modifiers, rewards, and analytics.
    /// </summary>
    public class RogueliteSetupWizard : EditorWindow
    {
        private Vector2 _scrollPos;
        private RunConfigSO _runConfig;
        private MetaUnlockTreeSO _unlockTree;
        private RunModifierPoolSO _modifierPool;
        private AscensionDefinitionSO _ascensionDef;
        private bool _showConfigEditor = true;
        private bool _showValidation = true;
        private bool _showUnlockTreeEditor;
        private bool _showModifierPoolEditor;
        private bool _showRuntimeDebug;
        private bool _showRuntimeMeta;
        private bool _showRuntimeModifiers;
        private UnityEditor.Editor _configEditor;
        private UnityEditor.Editor _unlockTreeEditor;
        private UnityEditor.Editor _modifierPoolEditor;
        private UnityEditor.Editor _ascensionDefEditor;

        // EPIC 23.7: Content Coverage integration
        private DIG.Roguelite.Editor.RogueliteDataContext _coverageContext;
        private DIG.Roguelite.Editor.ContentCoverageReport _coverageReport;
        private bool _showCoverageReport;

        // Validation cache — Lifecycle
        private bool _hasRunConfigAsset;
        private bool _hasPermadeathBridge;
        private bool _hasUIProvider;
        private string _permadeathBridgePath;
        private string _uiProviderPath;

        // Validation cache — Zones
        private bool _hasZoneSequenceAsset;
        private bool _hasZoneProvider;
        private bool _hasSpawnPositionProvider;
        private bool _hasZoneUIProvider;
        private string _zoneProviderPath;
        private string _spawnPositionProviderPath;
        private string _zoneUIProviderPath;
        private int _zoneDefCount;
        private int _encounterPoolCount;
        private int _directorConfigCount;

        // Validation cache — Meta-Progression
        private bool _hasUnlockTreeAsset;
        private bool _hasMetaStatBridge;
        private bool _hasMetaUIProvider;
        private string _metaStatBridgePath;
        private string _metaUIProviderPath;
        private string _unlockTreeValidationError;

        // Validation cache — Modifiers
        private bool _hasModifierPoolAsset;
        private bool _hasAscensionDefAsset;
        private bool _hasModifierUIProvider;
        private string _modifierPoolValidationError;
        private string _ascensionDefValidationError;
        private string _modifierUIProviderPath;

        // Validation cache — Rewards
        private bool _hasRewardPoolAsset;
        private bool _hasEventPoolAsset;
        private bool _hasRewardUIProvider;
        private string _rewardUIProviderPath;

        // Validation cache — Analytics
        private bool _hasAnalyticsProvider;
        private string _analyticsProviderPath;
        private bool _hasRogueliteAssembly;
        private bool _hasTrackingSystem;
        private bool _hasWorkstation;

        // Rewards editor state
        private RewardPoolSO _rewardPool;
        private EventPoolSO _eventPool;
        private bool _showRewardPoolEditor;
        private UnityEditor.Editor _rewardPoolEditor;
        private UnityEditor.Editor _eventPoolEditor;

        // Runtime query cache (avoids CreateEntityQuery every repaint)
        private Unity.Entities.World _cachedWorld;
        private Unity.Entities.EntityQuery _cachedRunStateQuery;
        private Unity.Entities.EntityQuery _cachedMetaBankQuery;
        private Unity.Entities.EntityQuery _cachedDifficultyQuery;
        private Unity.Entities.EntityQuery _cachedModifierRegistryQuery;

        [MenuItem("DIG/Roguelite Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<RogueliteSetupWizard>("Roguelite Workstation");
            window.minSize = new Vector2(520, 500);
        }

        private void OnEnable()
        {
            RefreshValidation();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            DestroyConfigEditor();
            DestroyUnlockTreeEditor();
            DestroyModifierPoolEditor();
            DestroyAscensionDefEditor();
            DestroyRewardPoolEditor();
            DestroyEventPoolEditor();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Invalidate cached queries when leaving play mode
            _cachedWorld = null;
            _cachedRunStateQuery = default;
            _cachedMetaBankQuery = default;
            _cachedDifficultyQuery = default;
            _cachedModifierRegistryQuery = default;
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            _scrollPos = EditorWindowUtilities.BeginScrollArea(_scrollPos);

            DrawHeader();
            EditorWindowUtilities.DrawSeparator();
            DrawValidationSection();
            EditorWindowUtilities.DrawSeparator();
            DrawContentCoverageSection();
            EditorWindowUtilities.DrawSeparator();
            DrawRunConfigSection();

            EditorWindowUtilities.DrawSeparator();
            DrawUnlockTreeSection();
            EditorWindowUtilities.DrawSeparator();
            DrawModifierPoolSection();
            EditorWindowUtilities.DrawSeparator();
            DrawRewardPoolSection();

            if (Application.isPlaying)
            {
                EditorWindowUtilities.DrawSeparator();
                DrawRuntimeDebugSection();
                EditorWindowUtilities.DrawSeparator();
                DrawRuntimeMetaSection();
                EditorWindowUtilities.DrawSeparator();
                DrawRuntimeModifiersSection();
                EditorWindowUtilities.DrawSeparator();
                DrawRuntimeRewardsSection();
            }

            EditorWindowUtilities.EndScrollArea();
        }

        // ==================== HEADER ====================

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Roguelite Module Setup", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Lifecycle \u2022 Zones \u2022 Meta-Progression \u2022 Modifiers \u2022 Rewards \u2022 Analytics", EditorStyles.miniLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Validation", GUILayout.Height(24)))
                RefreshValidation();
            if (GUILayout.Button("Lifecycle Guide", GUILayout.Height(24)))
                RevealDoc("SETUP_GUIDE_23.1.md");
            if (GUILayout.Button("Meta Guide", GUILayout.Height(24)))
                RevealDoc("SETUP_GUIDE_23.2.md");
            if (GUILayout.Button("Zones Guide", GUILayout.Height(24)))
                RevealDoc("EPIC23.3.md");
            if (GUILayout.Button("Modifiers Guide", GUILayout.Height(24)))
                RevealDoc("SETUP_GUIDE_23.4.md");
            if (GUILayout.Button("Rewards Guide", GUILayout.Height(24)))
                RevealDoc("SETUP_GUIDE_23.5.md");
            EditorGUILayout.EndHorizontal();
        }

        private static void RevealDoc(string filename)
        {
            var path = Path.Combine(Application.dataPath, "..", "Docs", "EPIC23", filename);
            if (File.Exists(path))
                EditorUtility.RevealInFinder(path);
            else
                Debug.LogWarning($"[RogueliteSetup] Doc not found at Docs/EPIC23/{filename}");
        }

        // ==================== VALIDATION ====================

        private void DrawValidationSection()
        {
            _showValidation = EditorWindowUtilities.DrawFoldoutSection(_showValidation, "Setup Validation", () =>
            {
                // 1. RunConfig asset
                EditorWindowUtilities.DrawStatusRow(
                    "RunConfig asset in Resources/",
                    _hasRunConfigAsset,
                    "Found",
                    "Missing — click Create below"
                );

                if (!_hasRunConfigAsset)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    if (GUILayout.Button("Create RunConfig Asset", GUILayout.Height(22)))
                        CreateRunConfigAsset();
                    EditorGUILayout.EndHorizontal();
                }

                // 2. PermadeathBridge system
                EditorWindowUtilities.DrawStatusRow(
                    "PermadeathBridge system",
                    _hasPermadeathBridge,
                    _permadeathBridgePath ?? "Found",
                    "Not found — see Lifecycle Guide, Section 3"
                );

                if (!_hasPermadeathBridge)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    if (GUILayout.Button("Create PermadeathBridge Template", GUILayout.Height(22)))
                        CreatePermadeathBridgeTemplate();
                    EditorGUILayout.EndHorizontal();
                }

                // 3. IRunUIProvider implementation
                EditorWindowUtilities.DrawStatusRow(
                    "IRunUIProvider implementation",
                    _hasUIProvider,
                    _uiProviderPath ?? "Found",
                    "Optional — implement to show run HUD"
                );

                // 4. Assembly definition
                EditorWindowUtilities.DrawStatusRow(
                    "DIG.Roguelite assembly",
                    _hasRogueliteAssembly,
                    "Present",
                    "Missing — Roguelite module not installed"
                );

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Zones & Encounters", EditorStyles.boldLabel);

                // Zone assets
                EditorWindowUtilities.DrawStatusRow(
                    "ZoneSequenceSO asset",
                    _hasZoneSequenceAsset,
                    "Found",
                    "Missing — create via Assets > Create > DIG > Roguelite > Zone Sequence"
                );

                EditorWindowUtilities.DrawStatusRow(
                    "ZoneDefinitionSO assets",
                    _zoneDefCount > 0,
                    $"{_zoneDefCount} found",
                    "None — create via Assets > Create > DIG > Roguelite > Zone Definition"
                );

                EditorWindowUtilities.DrawStatusRow(
                    "EncounterPoolSO assets",
                    _encounterPoolCount > 0,
                    $"{_encounterPoolCount} found",
                    "None — create via Assets > Create > DIG > Roguelite > Encounter Pool"
                );

                EditorWindowUtilities.DrawStatusRow(
                    "SpawnDirectorConfigSO assets",
                    _directorConfigCount > 0,
                    $"{_directorConfigCount} found",
                    "None — create via Assets > Create > DIG > Roguelite > Spawn Director Config"
                );

                // IZoneProvider
                EditorWindowUtilities.DrawStatusRow(
                    "IZoneProvider implementation",
                    _hasZoneProvider,
                    _zoneProviderPath ?? "Found",
                    "Required — implement to provide zone geometry/layout"
                );

                // ISpawnPositionProvider
                EditorWindowUtilities.DrawStatusRow(
                    "ISpawnPositionProvider implementation",
                    _hasSpawnPositionProvider,
                    _spawnPositionProviderPath ?? "Found",
                    "Optional — implement for custom spawn placement"
                );

                // IZoneUIProvider
                EditorWindowUtilities.DrawStatusRow(
                    "IZoneUIProvider implementation",
                    _hasZoneUIProvider,
                    _zoneUIProviderPath ?? "Found",
                    "Optional — implement to show zone HUD"
                );

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Meta-Progression", EditorStyles.boldLabel);

                // 5. MetaUnlockTree asset
                EditorWindowUtilities.DrawStatusRow(
                    "MetaUnlockTree asset in Resources/",
                    _hasUnlockTreeAsset,
                    _unlockTreeValidationError == null ? "Valid" : $"Found (warning: {_unlockTreeValidationError})",
                    "Missing — click Create below"
                );

                if (!_hasUnlockTreeAsset)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    if (GUILayout.Button("Create MetaUnlockTree Asset", GUILayout.Height(22)))
                        CreateUnlockTreeAsset();
                    EditorGUILayout.EndHorizontal();
                }

                // 6. MetaStatApply bridge
                EditorWindowUtilities.DrawStatusRow(
                    "MetaStatApply bridge system",
                    _hasMetaStatBridge,
                    _metaStatBridgePath ?? "Found",
                    "Optional — implement to apply meta stat bonuses"
                );

                if (!_hasMetaStatBridge)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    if (GUILayout.Button("Create MetaStatApply Template", GUILayout.Height(22)))
                        CreateMetaStatApplyTemplate();
                    EditorGUILayout.EndHorizontal();
                }

                // 7. IMetaUIProvider
                EditorWindowUtilities.DrawStatusRow(
                    "IMetaUIProvider implementation",
                    _hasMetaUIProvider,
                    _metaUIProviderPath ?? "Found",
                    "Optional — implement to show meta-progression UI"
                );

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Modifiers & Difficulty", EditorStyles.boldLabel);

                // 8. RunModifierPool asset
                EditorWindowUtilities.DrawStatusRow(
                    "RunModifierPool asset in Resources/",
                    _hasModifierPoolAsset,
                    _modifierPoolValidationError == null ? "Valid" : $"Found (warning: {_modifierPoolValidationError})",
                    "Missing — click Create below"
                );

                if (!_hasModifierPoolAsset)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    if (GUILayout.Button("Create RunModifierPool Asset", GUILayout.Height(22)))
                        CreateModifierPoolAsset();
                    EditorGUILayout.EndHorizontal();
                }

                // 9. AscensionDefinition asset
                EditorWindowUtilities.DrawStatusRow(
                    "AscensionDefinition asset in Resources/",
                    _hasAscensionDefAsset,
                    _ascensionDefValidationError == null ? "Valid" : $"Found (warning: {_ascensionDefValidationError})",
                    "Optional — click Create below"
                );

                if (!_hasAscensionDefAsset)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    if (GUILayout.Button("Create AscensionDefinition Asset", GUILayout.Height(22)))
                        CreateAscensionDefAsset();
                    EditorGUILayout.EndHorizontal();
                }

                // 10. IModifierUIProvider
                EditorWindowUtilities.DrawStatusRow(
                    "IModifierUIProvider implementation",
                    _hasModifierUIProvider,
                    _modifierUIProviderPath ?? "Found",
                    "Optional — implement to show modifier selection UI"
                );

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Rewards & Choices", EditorStyles.boldLabel);

                // 11. RewardPool asset
                EditorWindowUtilities.DrawStatusRow(
                    "RewardPool asset in Resources/",
                    _hasRewardPoolAsset,
                    "Found",
                    "Missing — click Create below"
                );

                if (!_hasRewardPoolAsset)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    if (GUILayout.Button("Create RewardPool Asset", GUILayout.Height(22)))
                        CreateRewardPoolAsset();
                    EditorGUILayout.EndHorizontal();
                }

                // 12. EventPool asset
                EditorWindowUtilities.DrawStatusRow(
                    "EventPool asset in Resources/",
                    _hasEventPoolAsset,
                    "Found",
                    "Optional — click Create below"
                );

                if (!_hasEventPoolAsset)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    if (GUILayout.Button("Create EventPool Asset", GUILayout.Height(22)))
                        CreateEventPoolAsset();
                    EditorGUILayout.EndHorizontal();
                }

                // 13. IRewardUIProvider
                EditorWindowUtilities.DrawStatusRow(
                    "IRewardUIProvider implementation",
                    _hasRewardUIProvider,
                    _rewardUIProviderPath ?? "Found",
                    "Optional — implement to show reward choice UI"
                );

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Analytics & Tooling", EditorStyles.boldLabel);

                // 14. RunStatistics tracking (auto — part of framework)
                EditorWindowUtilities.DrawStatusRow(
                    "RunStatisticsTrackingSystem",
                    _hasTrackingSystem,
                    "Found",
                    "Missing — required for per-run stats tracking"
                );

                // 15. IRunAnalyticsProvider
                EditorWindowUtilities.DrawStatusRow(
                    "IRunAnalyticsProvider implementation",
                    _hasAnalyticsProvider,
                    _analyticsProviderPath ?? "Found",
                    "Optional — implement to show analytics HUD"
                );

                // 16. Run Workstation
                EditorWindowUtilities.DrawStatusRow(
                    "Run Workstation editor window",
                    _hasWorkstation,
                    "Available (DIG > Run Workstation)",
                    "Missing — editor tooling not installed"
                );
            });
        }

        // ==================== CONTENT COVERAGE (EPIC 23.7) ====================

        private void DrawContentCoverageSection()
        {
            _showCoverageReport = EditorWindowUtilities.DrawFoldoutSection(_showCoverageReport, "Content Coverage Analysis", () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Run Coverage Scan", GUILayout.Height(22)))
                {
                    if (_coverageContext == null)
                        _coverageContext = new DIG.Roguelite.Editor.RogueliteDataContext();
                    _coverageContext.Invalidate();
                    _coverageContext.Build();
                    _coverageReport = DIG.Roguelite.Editor.ContentCoverageAnalyzer.Analyze(_coverageContext);
                }
                if (_coverageReport != null)
                {
                    float score = _coverageReport.CompletenessScore;
                    Color c = score >= 80f ? new Color(0.3f, 0.8f, 0.3f)
                            : score >= 50f ? new Color(0.8f, 0.8f, 0.3f)
                            : new Color(0.8f, 0.3f, 0.3f);
                    var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = c } };
                    GUILayout.Label($"Score: {score:F0}%", style, GUILayout.Width(90));
                }
                EditorGUILayout.EndHorizontal();

                if (_coverageReport == null)
                {
                    EditorGUILayout.HelpBox("Click 'Run Coverage Scan' for automated data completeness analysis.", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField(
                    $"Errors: {_coverageReport.ErrorCount}  |  Warnings: {_coverageReport.WarningCount}  |  Info: {_coverageReport.InfoCount}",
                    EditorStyles.miniLabel);

                // Show first 10 issues inline
                int shown = 0;
                for (int i = 0; i < _coverageReport.Issues.Count && shown < 10; i++)
                {
                    var issue = _coverageReport.Issues[i];
                    MessageType mt = issue.Severity switch
                    {
                        DIG.Roguelite.Editor.CoverageSeverity.Error => MessageType.Error,
                        DIG.Roguelite.Editor.CoverageSeverity.Warning => MessageType.Warning,
                        _ => MessageType.Info
                    };
                    EditorGUILayout.HelpBox($"[{issue.Category}] {issue.Message}", mt);
                    shown++;
                }

                if (_coverageReport.Issues.Count > 10)
                    EditorGUILayout.LabelField($"  ... and {_coverageReport.Issues.Count - 10} more. Open Run Workstation > Content Coverage for full list.", EditorStyles.miniLabel);
            });
        }

        // ==================== RUN CONFIG EDITOR ====================

        private void DrawRunConfigSection()
        {
            _showConfigEditor = EditorWindowUtilities.DrawFoldoutSection(_showConfigEditor, "Run Configuration", () =>
            {
                // Asset picker
                EditorGUI.BeginChangeCheck();
                _runConfig = (RunConfigSO)EditorGUILayout.ObjectField(
                    "RunConfig Asset",
                    _runConfig,
                    typeof(RunConfigSO),
                    false
                );
                if (EditorGUI.EndChangeCheck())
                    RebuildConfigEditor();

                if (_runConfig == null)
                {
                    // Try auto-load
                    _runConfig = Resources.Load<RunConfigSO>("RunConfig");
                    if (_runConfig != null)
                        RebuildConfigEditor();
                }

                if (_runConfig == null)
                {
                    EditorGUILayout.HelpBox(
                        "No RunConfig asset selected. Create one via the button above or drag one here.",
                        MessageType.Info
                    );
                    return;
                }

                EditorGUILayout.Space(5);

                // Draw the full inspector for the RunConfigSO inline
                if (_configEditor == null)
                    RebuildConfigEditor();

                if (_configEditor != null)
                {
                    _configEditor.OnInspectorGUI();
                }

                EditorGUILayout.Space(5);

                // Difficulty curve preview
                DrawDifficultyPreview();
            });
        }

        private void DrawDifficultyPreview()
        {
            if (_runConfig == null || _runConfig.ZoneCount <= 0)
                return;

            EditorGUILayout.LabelField("Difficulty Per Zone", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            for (int i = 0; i < _runConfig.ZoneCount; i++)
            {
                float diff = _runConfig.GetDifficultyAtZone(i);
                float maxExpected = _runConfig.DifficultyPerZone.Evaluate(1f) * 1.1f;
                float barFill = maxExpected > 0f ? Mathf.Clamp01(diff / maxExpected) : 0f;

                var rect = EditorGUILayout.GetControlRect(false, 18);
                var labelRect = new Rect(rect.x, rect.y, 60, rect.height);
                var barRect = new Rect(rect.x + 65, rect.y + 2, rect.width - 130, rect.height - 4);
                var valueRect = new Rect(rect.xMax - 60, rect.y, 60, rect.height);

                EditorGUI.LabelField(labelRect, $"Zone {i}");

                // Bar background
                EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));
                // Bar fill
                var fillRect = new Rect(barRect.x, barRect.y, barRect.width * barFill, barRect.height);
                Color barColor = Color.Lerp(new Color(0.3f, 0.7f, 0.3f), new Color(0.9f, 0.2f, 0.2f), barFill);
                EditorGUI.DrawRect(fillRect, barColor);

                EditorGUI.LabelField(valueRect, $"{diff:F2}x", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        // ==================== RUNTIME DEBUG ====================

        private void DrawRuntimeDebugSection()
        {
            _showRuntimeDebug = EditorWindowUtilities.DrawFoldoutSection(_showRuntimeDebug, "Runtime State (Play Mode)", () =>
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to see runtime state.", MessageType.Info);
                    return;
                }

                // Find RunState entity — use cached query when possible
                if (_cachedWorld == null || !_cachedWorld.IsCreated)
                {
                    _cachedWorld = null;
                    _cachedRunStateQuery = default;
                    foreach (var world in Unity.Entities.World.All)
                    {
                        if (world.IsCreated)
                        {
                            var q = world.EntityManager.CreateEntityQuery(
                                Unity.Entities.ComponentType.ReadOnly<RunState>());
                            if (q.CalculateEntityCount() > 0)
                            {
                                _cachedWorld = world;
                                _cachedRunStateQuery = q;
                                break;
                            }
                        }
                    }
                }

                if (_cachedWorld == null)
                {
                    EditorGUILayout.HelpBox("No RunState entity found. Is RunConfigBootstrapSystem running?", MessageType.Warning);
                    return;
                }

                var runState = _cachedRunStateQuery.GetSingleton<RunState>();

                EditorGUILayout.LabelField("World", _cachedWorld.Name);
                EditorWindowUtilities.DrawSeparator();

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.EnumPopup("Phase", runState.Phase);
                EditorGUILayout.IntField("Run ID", (int)runState.RunId);
                EditorGUILayout.IntField("Seed", (int)runState.Seed);
                EditorGUILayout.IntField("Zone", runState.CurrentZoneIndex);
                EditorGUILayout.IntField("Max Zones", runState.MaxZones);
                EditorGUILayout.FloatField("Elapsed Time", runState.ElapsedTime);
                EditorGUILayout.IntField("Score", runState.Score);
                EditorGUILayout.IntField("Run Currency", runState.RunCurrency);
                EditorGUILayout.IntField("Ascension", runState.AscensionLevel);
                EditorGUILayout.IntField("Zone Seed", (int)runState.ZoneSeed);
                if (runState.EndReason != RunEndReason.None)
                    EditorGUILayout.EnumPopup("End Reason", runState.EndReason);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(5);

                // Quick-action buttons for testing
                EditorGUILayout.LabelField("Test Actions", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Start Run"))
                    SetRunPhase(_cachedWorld, RunPhase.Preparation, true);
                if (GUILayout.Button("Activate Zone"))
                    SetRunPhase(_cachedWorld, RunPhase.Active, false);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Zone"))
                    SetRunPhase(_cachedWorld, RunPhase.ZoneTransition, false);
                if (GUILayout.Button("Kill Player"))
                    TriggerPermadeath(_cachedWorld);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Boss Victory"))
                    SetRunEndReason(_cachedWorld, RunEndReason.BossDefeated);
                if (GUILayout.Button("Reset Run"))
                    SetRunPhase(_cachedWorld, RunPhase.None, true);
                EditorGUILayout.EndHorizontal();
            });
        }

        // ==================== UNLOCK TREE EDITOR ====================

        private void DrawUnlockTreeSection()
        {
            _showUnlockTreeEditor = EditorWindowUtilities.DrawFoldoutSection(_showUnlockTreeEditor, "Meta Unlock Tree", () =>
            {
                EditorGUI.BeginChangeCheck();
                _unlockTree = (MetaUnlockTreeSO)EditorGUILayout.ObjectField(
                    "UnlockTree Asset",
                    _unlockTree,
                    typeof(MetaUnlockTreeSO),
                    false
                );
                if (EditorGUI.EndChangeCheck())
                    RebuildUnlockTreeEditor();

                if (_unlockTree == null)
                {
                    _unlockTree = Resources.Load<MetaUnlockTreeSO>("MetaUnlockTree");
                    if (_unlockTree != null)
                        RebuildUnlockTreeEditor();
                }

                if (_unlockTree == null)
                {
                    EditorGUILayout.HelpBox(
                        "No MetaUnlockTree asset found. Create one via the validation section above.",
                        MessageType.Info
                    );
                    return;
                }

                EditorGUILayout.Space(5);

                if (_unlockTreeEditor == null)
                    RebuildUnlockTreeEditor();

                if (_unlockTreeEditor != null)
                    _unlockTreeEditor.OnInspectorGUI();

                EditorGUILayout.Space(5);

                // Unlock summary
                if (_unlockTree.Unlocks != null && _unlockTree.Unlocks.Count > 0)
                {
                    EditorGUILayout.LabelField("Unlock Summary", EditorStyles.boldLabel);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Category counts
                    var categoryCounts = new int[8];
                    int totalCost = 0;
                    int prereqCount = 0;
                    foreach (var u in _unlockTree.Unlocks)
                    {
                        categoryCounts[(int)u.Category]++;
                        totalCost += u.Cost;
                        if (u.PrerequisiteId >= 0) prereqCount++;
                    }

                    EditorGUILayout.LabelField($"Total Unlocks: {_unlockTree.Unlocks.Count}  |  Total Cost: {totalCost}  |  With Prerequisites: {prereqCount}");

                    EditorGUILayout.BeginHorizontal();
                    for (int i = 0; i < 8; i++)
                    {
                        if (categoryCounts[i] > 0)
                            GUILayout.Label($"{(MetaUnlockCategory)i}: {categoryCounts[i]}", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndHorizontal();

                    // Validation
                    if (_unlockTree.Validate(out string error))
                    {
                        EditorGUILayout.HelpBox("All unlock IDs unique, prerequisites valid.", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"Validation error: {error}", MessageType.Error);
                    }

                    EditorGUILayout.EndVertical();
                }
            });
        }

        // ==================== MODIFIER POOL EDITOR ====================

        private void DrawModifierPoolSection()
        {
            _showModifierPoolEditor = EditorWindowUtilities.DrawFoldoutSection(_showModifierPoolEditor, "Run Modifiers & Ascension", () =>
            {
                // Modifier pool picker
                EditorGUI.BeginChangeCheck();
                _modifierPool = (RunModifierPoolSO)EditorGUILayout.ObjectField(
                    "RunModifierPool Asset",
                    _modifierPool,
                    typeof(RunModifierPoolSO),
                    false
                );
                if (EditorGUI.EndChangeCheck())
                    RebuildModifierPoolEditor();

                if (_modifierPool == null)
                {
                    _modifierPool = Resources.Load<RunModifierPoolSO>("RunModifierPool");
                    if (_modifierPool != null)
                        RebuildModifierPoolEditor();
                }

                if (_modifierPool != null)
                {
                    EditorGUILayout.Space(5);

                    if (_modifierPoolEditor == null)
                        RebuildModifierPoolEditor();

                    if (_modifierPoolEditor != null)
                        _modifierPoolEditor.OnInspectorGUI();

                    EditorGUILayout.Space(5);

                    // Modifier summary
                    if (_modifierPool.Modifiers != null && _modifierPool.Modifiers.Count > 0)
                    {
                        EditorGUILayout.LabelField("Modifier Summary", EditorStyles.boldLabel);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        int positive = 0, negative = 0, neutral = 0;
                        foreach (var m in _modifierPool.Modifiers)
                        {
                            switch (m.Polarity) { case ModifierPolarity.Positive: positive++; break; case ModifierPolarity.Negative: negative++; break; default: neutral++; break; }
                        }
                        EditorGUILayout.LabelField($"Total: {_modifierPool.Modifiers.Count}  |  Positive: {positive}  |  Negative: {negative}  |  Neutral: {neutral}");

                        // Target breakdown
                        var targetCounts = new int[5];
                        foreach (var m in _modifierPool.Modifiers)
                            targetCounts[(int)m.Target]++;

                        EditorGUILayout.BeginHorizontal();
                        for (int i = 0; i < 5; i++)
                        {
                            if (targetCounts[i] > 0)
                                GUILayout.Label($"{(ModifierTarget)i}: {targetCounts[i]}", EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndHorizontal();

                        if (_modifierPool.Validate(out string error))
                            EditorGUILayout.HelpBox("All modifier IDs unique, stacking valid.", MessageType.Info);
                        else
                            EditorGUILayout.HelpBox($"Validation error: {error}", MessageType.Error);

                        EditorGUILayout.EndVertical();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "No RunModifierPool asset found. Create one via the validation section above.",
                        MessageType.Info
                    );
                }

                EditorGUILayout.Space(10);

                // Ascension definition
                EditorGUI.BeginChangeCheck();
                _ascensionDef = (AscensionDefinitionSO)EditorGUILayout.ObjectField(
                    "AscensionDefinition Asset",
                    _ascensionDef,
                    typeof(AscensionDefinitionSO),
                    false
                );
                if (EditorGUI.EndChangeCheck())
                    RebuildAscensionDefEditor();

                if (_ascensionDef == null)
                {
                    _ascensionDef = Resources.Load<AscensionDefinitionSO>("AscensionDefinition");
                    if (_ascensionDef != null)
                        RebuildAscensionDefEditor();
                }

                if (_ascensionDef != null)
                {
                    EditorGUILayout.Space(5);

                    if (_ascensionDefEditor == null)
                        RebuildAscensionDefEditor();

                    if (_ascensionDefEditor != null)
                        _ascensionDefEditor.OnInspectorGUI();

                    EditorGUILayout.Space(5);

                    if (_ascensionDef.Tiers != null && _ascensionDef.Tiers.Count > 0)
                    {
                        EditorGUILayout.LabelField("Ascension Summary", EditorStyles.boldLabel);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        int totalForced = 0;
                        foreach (var t in _ascensionDef.Tiers)
                            totalForced += (t.ForcedModifierIds != null ? t.ForcedModifierIds.Length : 0);

                        EditorGUILayout.LabelField($"Tiers: {_ascensionDef.Tiers.Count}  |  Total Forced Modifiers: {totalForced}");

                        if (_ascensionDef.Validate(out string error))
                            EditorGUILayout.HelpBox("All ascension levels unique, reward multipliers valid.", MessageType.Info);
                        else
                            EditorGUILayout.HelpBox($"Validation error: {error}", MessageType.Error);

                        EditorGUILayout.EndVertical();
                    }
                }
            });
        }

        // ==================== RUNTIME MODIFIERS STATE ====================

        private void DrawRuntimeModifiersSection()
        {
            _showRuntimeModifiers = EditorWindowUtilities.DrawFoldoutSection(_showRuntimeModifiers, "Modifiers & Difficulty State", () =>
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to see modifier/difficulty state.", MessageType.Info);
                    return;
                }

                if (_cachedWorld == null || !_cachedWorld.IsCreated)
                    return;

                // RuntimeDifficultyScale query
                if (_cachedDifficultyQuery == default)
                {
                    _cachedDifficultyQuery = _cachedWorld.EntityManager.CreateEntityQuery(
                        Unity.Entities.ComponentType.ReadOnly<RuntimeDifficultyScale>());
                }

                if (_cachedDifficultyQuery.IsEmptyIgnoreFilter)
                {
                    EditorGUILayout.HelpBox("No RuntimeDifficultyScale entity found. Is ModifierBootstrapSystem running?", MessageType.Warning);
                    return;
                }

                var difficulty = _cachedDifficultyQuery.GetSingleton<RuntimeDifficultyScale>();

                EditorGUILayout.LabelField("Effective Difficulty", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.FloatField("Zone Difficulty", difficulty.ZoneDifficultyMultiplier);
                EditorGUILayout.FloatField("Enemy Health Scale", difficulty.EnemyHealthScale);
                EditorGUILayout.FloatField("Enemy Damage Scale", difficulty.EnemyDamageScale);
                EditorGUILayout.FloatField("Enemy Spawn Rate", difficulty.EnemySpawnRateScale);
                EditorGUILayout.FloatField("Loot Quantity Scale", difficulty.LootQuantityScale);
                EditorGUILayout.FloatField("Loot Quality Bonus", difficulty.LootQualityBonus);
                EditorGUILayout.FloatField("XP Multiplier", difficulty.XPMultiplier);
                EditorGUILayout.FloatField("Currency Multiplier", difficulty.CurrencyMultiplier);
                EditorGUILayout.FloatField("Ascension Reward Mult", difficulty.AscensionRewardMultiplier);
                EditorGUI.EndDisabledGroup();

                // Active modifiers on RunState
                if (_cachedRunStateQuery != default && !_cachedRunStateQuery.IsEmptyIgnoreFilter)
                {
                    var runEntity = _cachedRunStateQuery.GetSingletonEntity();
                    if (_cachedWorld.EntityManager.HasBuffer<RunModifierStack>(runEntity))
                    {
                        var modStack = _cachedWorld.EntityManager.GetBuffer<RunModifierStack>(runEntity, true);
                        EditorGUILayout.Space(5);
                        EditorWindowUtilities.DrawCountBadge($"active modifiers", modStack.Length, 1);

                        if (modStack.Length > 0)
                        {
                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                            for (int i = 0; i < modStack.Length; i++)
                            {
                                var mod = modStack[i];
                                string op = mod.IsMultiplicative ? "×" : "+";
                                EditorGUILayout.LabelField(
                                    $"  ID={mod.ModifierId}  {mod.Target}:{mod.StatId}  {op}{mod.EffectiveValue:F2}  (×{mod.StackCount})",
                                    EditorStyles.miniLabel);
                            }
                            EditorGUILayout.EndVertical();
                        }
                    }

                    if (_cachedWorld.EntityManager.HasBuffer<PendingModifierChoice>(runEntity))
                    {
                        var choices = _cachedWorld.EntityManager.GetBuffer<PendingModifierChoice>(runEntity, true);
                        if (choices.Length > 0)
                            EditorWindowUtilities.DrawCountBadge($"pending modifier choices", choices.Length, 1);
                    }
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Test Actions", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Random Modifier"))
                    AddRandomModifier(_cachedWorld);
                if (GUILayout.Button("Clear Modifiers"))
                    ClearModifiers(_cachedWorld);
                EditorGUILayout.EndHorizontal();
            });
        }

        // ==================== RUNTIME META STATE ====================

        private void DrawRuntimeMetaSection()
        {
            _showRuntimeMeta = EditorWindowUtilities.DrawFoldoutSection(_showRuntimeMeta, "Meta-Progression State", () =>
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to see meta-progression state.", MessageType.Info);
                    return;
                }

                if (_cachedWorld == null || !_cachedWorld.IsCreated)
                    return;

                // Find MetaBank query — cache it
                if (_cachedMetaBankQuery == default)
                {
                    _cachedMetaBankQuery = _cachedWorld.EntityManager.CreateEntityQuery(
                        Unity.Entities.ComponentType.ReadOnly<MetaBank>());
                }

                if (_cachedMetaBankQuery.IsEmptyIgnoreFilter)
                {
                    EditorGUILayout.HelpBox("No MetaBank entity found. Is MetaBootstrapSystem running?", MessageType.Warning);
                    return;
                }

                var bank = _cachedMetaBankQuery.GetSingleton<MetaBank>();
                var bankEntity = _cachedMetaBankQuery.GetSingletonEntity();

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Meta Currency", bank.MetaCurrency);
                EditorGUILayout.IntField("Lifetime Earned", bank.LifetimeMetaEarned);
                EditorGUILayout.IntField("Runs Attempted", bank.TotalRunsAttempted);
                EditorGUILayout.IntField("Runs Won", bank.TotalRunsWon);
                EditorGUILayout.IntField("Best Score", bank.BestScore);
                EditorGUILayout.IntField("Best Zone", bank.BestZoneReached);

                int minutes = (int)(bank.TotalPlaytime / 60f);
                int seconds = (int)(bank.TotalPlaytime % 60f);
                EditorGUILayout.TextField("Total Playtime", $"{minutes}m {seconds}s");
                EditorGUI.EndDisabledGroup();

                // Unlock status
                if (_cachedWorld.EntityManager.HasBuffer<MetaUnlockEntry>(bankEntity))
                {
                    var unlocks = _cachedWorld.EntityManager.GetBuffer<MetaUnlockEntry>(bankEntity, true);
                    int unlockedCount = 0;
                    for (int i = 0; i < unlocks.Length; i++)
                        if (unlocks[i].IsUnlocked) unlockedCount++;

                    EditorWindowUtilities.DrawCountBadge($"unlocks purchased ({unlockedCount}/{unlocks.Length})", unlockedCount, 1);
                }

                // Run history count
                if (_cachedWorld.EntityManager.HasBuffer<RunHistoryEntry>(bankEntity))
                {
                    var history = _cachedWorld.EntityManager.GetBuffer<RunHistoryEntry>(bankEntity, true);
                    EditorWindowUtilities.DrawCountBadge($"runs in history", history.Length, 1);
                }

                EditorGUILayout.Space(5);

                // Test buttons
                EditorGUILayout.LabelField("Test Actions", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Grant 100 Meta"))
                    GrantMetaCurrency(_cachedWorld, 100);
                if (GUILayout.Button("Grant 500 Meta"))
                    GrantMetaCurrency(_cachedWorld, 500);
                if (GUILayout.Button("Reset Meta"))
                    ResetMetaBank(_cachedWorld);
                EditorGUILayout.EndHorizontal();
            });
        }

        // ==================== ACTIONS ====================

        private void CreateUnlockTreeAsset()
        {
            var resourcesPath = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);

            var asset = CreateInstance<MetaUnlockTreeSO>();
            asset.TreeName = "Default Unlock Tree";

            // Add a few example unlocks
            asset.Unlocks = new System.Collections.Generic.List<MetaUnlockDefinition>
            {
                new MetaUnlockDefinition
                {
                    UnlockId = 0, DisplayName = "Thicker Skin", Description = "Gain +5 Vitality permanently.",
                    Category = MetaUnlockCategory.StatBoost, Cost = 50, PrerequisiteId = -1, FloatValue = 5f, IntValue = 1
                },
                new MetaUnlockDefinition
                {
                    UnlockId = 1, DisplayName = "Sharp Blade", Description = "Gain +3 Strength permanently.",
                    Category = MetaUnlockCategory.StatBoost, Cost = 75, PrerequisiteId = -1, FloatValue = 3f, IntValue = 0
                },
                new MetaUnlockDefinition
                {
                    UnlockId = 2, DisplayName = "Iron Hide", Description = "Gain +10 Vitality permanently.",
                    Category = MetaUnlockCategory.StatBoost, Cost = 150, PrerequisiteId = 0, FloatValue = 10f, IntValue = 1
                },
                new MetaUnlockDefinition
                {
                    UnlockId = 3, DisplayName = "Better Deals", Description = "Shop prices reduced by 10%.",
                    Category = MetaUnlockCategory.ShopUpgrade, Cost = 200, PrerequisiteId = -1, FloatValue = 0.1f, IntValue = 0
                }
            };

            AssetDatabase.CreateAsset(asset, "Assets/Resources/MetaUnlockTree.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _unlockTree = asset;
            RebuildUnlockTreeEditor();
            RefreshValidation();

            EditorGUIUtility.PingObject(asset);
            Debug.Log("[RogueliteSetup] Created MetaUnlockTree asset at Assets/Resources/MetaUnlockTree.asset with 4 example unlocks.");
        }

        private void CreateMetaStatApplyTemplate()
        {
            var dir = Path.Combine(Application.dataPath, "Scripts", "Game", "Roguelite");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var filePath = Path.Combine(dir, "MetaStatApplySystem.cs");
            if (File.Exists(filePath))
            {
                if (!EditorUtility.DisplayDialog("File Exists",
                    "MetaStatApplySystem.cs already exists. Overwrite?", "Overwrite", "Cancel"))
                    return;
            }

            var template = @"using Unity.Entities;
using DIG.Roguelite;

/// <summary>
/// Game-side bridge: reads unlocked MetaUnlockEntry entries and applies their effects.
/// Customize for your game's stat/item/ability systems.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MetaUnlockPurchaseSystem))]
public partial class MetaStatApplySystem : SystemBase
{
    private bool _applied;
    private int _lastUnlockCount;

    protected override void OnCreate()
    {
        RequireForUpdate<MetaBank>();
    }

    protected override void OnUpdate()
    {
        var bankEntity = SystemAPI.GetSingletonEntity<MetaBank>();
        var unlocks = SystemAPI.GetBuffer<MetaUnlockEntry>(bankEntity);

        // Count unlocked entries — reapply when count changes
        int unlockedCount = 0;
        for (int i = 0; i < unlocks.Length; i++)
            if (unlocks[i].IsUnlocked) unlockedCount++;

        if (_applied && unlockedCount == _lastUnlockCount)
            return;

        _lastUnlockCount = unlockedCount;
        _applied = true;

        // TODO: Apply each unlocked entry based on your game's stat system
        for (int i = 0; i < unlocks.Length; i++)
        {
            var entry = unlocks[i];
            if (!entry.IsUnlocked) continue;

            switch (entry.Category)
            {
                case MetaUnlockCategory.StatBoost:
                    // Apply entry.FloatValue as a stat bonus for entry.IntValue (stat ID)
                    break;
                case MetaUnlockCategory.StarterItem:
                    // Grant item entry.IntValue at run start
                    break;
                case MetaUnlockCategory.CurrencyBonus:
                    // Store entry.FloatValue as a multiplier for earn rates
                    break;
            }
        }
    }
}
";

            File.WriteAllText(filePath, template);
            AssetDatabase.Refresh();
            RefreshValidation();

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Game/Roguelite/MetaStatApplySystem.cs");
            if (asset != null) EditorGUIUtility.PingObject(asset);

            Debug.Log("[RogueliteSetup] Created MetaStatApplySystem template at Assets/Scripts/Game/Roguelite/MetaStatApplySystem.cs");
        }

        private void CreateRunConfigAsset()
        {
            var resourcesPath = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);

            var asset = CreateInstance<RunConfigSO>();
            asset.ConfigName = "Default Run";
            asset.ConfigId = 0;
            asset.ZoneCount = 5;
            asset.StartingRunCurrency = 0;
            asset.RunCurrencyPerZoneClear = 10;
            asset.MetaCurrencyConversionRate = 0.5f;

            AssetDatabase.CreateAsset(asset, "Assets/Resources/RunConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _runConfig = asset;
            RebuildConfigEditor();
            RefreshValidation();

            EditorGUIUtility.PingObject(asset);
            Debug.Log("[RogueliteSetup] Created RunConfig asset at Assets/Resources/RunConfig.asset");
        }

        private void CreatePermadeathBridgeTemplate()
        {
            var dir = Path.Combine(Application.dataPath, "Scripts", "Game", "Roguelite");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var filePath = Path.Combine(dir, "PermadeathBridgeSystem.cs");
            if (File.Exists(filePath))
            {
                if (!EditorUtility.DisplayDialog("File Exists",
                    "PermadeathBridgeSystem.cs already exists. Overwrite?", "Overwrite", "Cancel"))
                    return;
            }

            var template = @"using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using DIG.Roguelite;

/// <summary>
/// Game-side bridge: detects player death and signals the roguelite framework.
/// Customize the death condition for your game (single-player vs co-op, downed state, etc.).
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PermadeathSystem))]
public partial class PermadeathBridgeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<RunState>();
    }

    protected override void OnUpdate()
    {
        var run = SystemAPI.GetSingleton<RunState>();
        if (run.Phase != RunPhase.Active && run.Phase != RunPhase.BossEncounter)
            return;

        // TODO: Customize for co-op (check ALL players dead vs ANY player dead)
        foreach (var (death, _) in
            SystemAPI.Query<RefRO<DeathState>, RefRO<PlayerTag>>())
        {
            if (death.ValueRO.Phase == DeathPhase.Dead)
            {
                var runEntity = SystemAPI.GetSingletonEntity<RunState>();
                EntityManager.SetComponentEnabled<PermadeathSignal>(runEntity, true);
                return;
            }
        }
    }
}
";

            File.WriteAllText(filePath, template);
            AssetDatabase.Refresh();
            RefreshValidation();

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Game/Roguelite/PermadeathBridgeSystem.cs");
            if (asset != null) EditorGUIUtility.PingObject(asset);

            Debug.Log("[RogueliteSetup] Created PermadeathBridgeSystem template at Assets/Scripts/Game/Roguelite/PermadeathBridgeSystem.cs");
        }

        private void CreateModifierPoolAsset()
        {
            var resourcesPath = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);

            var asset = CreateInstance<RunModifierPoolSO>();
            asset.PoolName = "Default Modifier Pool";
            asset.Modifiers = new System.Collections.Generic.List<RunModifierDefinition>
            {
                new RunModifierDefinition
                {
                    ModifierId = 0, DisplayName = "Tough Enemies", Description = "Enemies have +50% health.",
                    Polarity = ModifierPolarity.Negative, Target = ModifierTarget.EnemyStat, StatId = 0,
                    FloatValue = 1.5f, IsMultiplicative = true, Stackable = true, MaxStacks = 3
                },
                new RunModifierDefinition
                {
                    ModifierId = 1, DisplayName = "Aggressive Enemies", Description = "Enemies deal +25% damage.",
                    Polarity = ModifierPolarity.Negative, Target = ModifierTarget.EnemyStat, StatId = 1,
                    FloatValue = 1.25f, IsMultiplicative = true, Stackable = true, MaxStacks = 3
                },
                new RunModifierDefinition
                {
                    ModifierId = 2, DisplayName = "Lucky Loot", Description = "Loot drops +30% more items.",
                    Polarity = ModifierPolarity.Positive, Target = ModifierTarget.Economy, StatId = 0,
                    FloatValue = 1.3f, IsMultiplicative = true, Stackable = false, MaxStacks = 1
                },
                new RunModifierDefinition
                {
                    ModifierId = 3, DisplayName = "XP Boost", Description = "Earn +25% XP from all sources.",
                    Polarity = ModifierPolarity.Positive, Target = ModifierTarget.Economy, StatId = 2,
                    FloatValue = 1.25f, IsMultiplicative = true, Stackable = true, MaxStacks = 2
                },
                new RunModifierDefinition
                {
                    ModifierId = 4, DisplayName = "Swarm", Description = "Enemy spawn rate increased by 50%.",
                    Polarity = ModifierPolarity.Negative, Target = ModifierTarget.Encounter, StatId = 0,
                    FloatValue = 1.5f, IsMultiplicative = true, Stackable = true, MaxStacks = 2
                }
            };

            AssetDatabase.CreateAsset(asset, "Assets/Resources/RunModifierPool.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _modifierPool = asset;
            RebuildModifierPoolEditor();
            RefreshValidation();

            EditorGUIUtility.PingObject(asset);
            Debug.Log("[RogueliteSetup] Created RunModifierPool asset at Assets/Resources/RunModifierPool.asset with 5 example modifiers.");
        }

        private void CreateAscensionDefAsset()
        {
            var resourcesPath = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);

            var asset = CreateInstance<AscensionDefinitionSO>();
            asset.DefinitionName = "Default Ascension";
            asset.Tiers = new System.Collections.Generic.List<AscensionTier>
            {
                new AscensionTier
                {
                    Level = 1, DisplayName = "Heat 1", Description = "Enemies are tougher.",
                    ForcedModifierIds = new[] { 0 }, RewardMultiplier = 1.25f, BonusHeatBudget = 0
                },
                new AscensionTier
                {
                    Level = 2, DisplayName = "Heat 2", Description = "Enemies are tougher and more aggressive.",
                    ForcedModifierIds = new[] { 1 }, RewardMultiplier = 1.5f, BonusHeatBudget = 1
                },
                new AscensionTier
                {
                    Level = 3, DisplayName = "Heat 3", Description = "Maximum challenge. Swarm mode.",
                    ForcedModifierIds = new[] { 4 }, RewardMultiplier = 2.0f, BonusHeatBudget = 2
                }
            };

            AssetDatabase.CreateAsset(asset, "Assets/Resources/AscensionDefinition.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _ascensionDef = asset;
            RebuildAscensionDefEditor();
            RefreshValidation();

            EditorGUIUtility.PingObject(asset);
            Debug.Log("[RogueliteSetup] Created AscensionDefinition asset at Assets/Resources/AscensionDefinition.asset with 3 tiers.");
        }

        private void RefreshValidation()
        {
            // 1. RunConfig asset
            _hasRunConfigAsset = Resources.Load<RunConfigSO>("RunConfig") != null;

            // 2. PermadeathBridge — filename search first, text scan only on candidates
            _hasPermadeathBridge = false;
            _permadeathBridgePath = null;
            var bridgeCandidates = AssetDatabase.FindAssets("PermadeathBridge t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in bridgeCandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue;
                _hasPermadeathBridge = true;
                _permadeathBridgePath = path;
                break;
            }
            // Fallback: if no file named PermadeathBridge, check for any file referencing PermadeathSignal
            if (!_hasPermadeathBridge)
            {
                var signalCandidates = AssetDatabase.FindAssets("Permadeath t:MonoScript", new[] { "Assets/Scripts" });
                foreach (var guid in signalCandidates)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains("/Roguelite/")) continue;
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script != null && script.text.Contains("PermadeathSignal"))
                    {
                        _hasPermadeathBridge = true;
                        _permadeathBridgePath = path;
                        break;
                    }
                }
            }

            // 3. IRunUIProvider — filename search first, text scan only on candidates
            _hasUIProvider = false;
            _uiProviderPath = null;
            var uiCandidates = AssetDatabase.FindAssets("RunUI RunHUD t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in uiCandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.text.Contains("IRunUIProvider"))
                {
                    _hasUIProvider = true;
                    _uiProviderPath = path;
                    break;
                }
            }

            // === Zones Validation ===

            // Zone assets: ZoneSequenceSO, ZoneDefinitionSO, EncounterPoolSO, SpawnDirectorConfigSO
            _hasZoneSequenceAsset = false;
            var seqGuids = AssetDatabase.FindAssets("t:ZoneSequenceSO");
            _hasZoneSequenceAsset = seqGuids.Length > 0;

            var defGuids = AssetDatabase.FindAssets("t:ZoneDefinitionSO");
            _zoneDefCount = defGuids.Length;

            var poolGuids = AssetDatabase.FindAssets("t:EncounterPoolSO");
            _encounterPoolCount = poolGuids.Length;

            var dirGuids = AssetDatabase.FindAssets("t:SpawnDirectorConfigSO");
            _directorConfigCount = dirGuids.Length;

            // IZoneProvider implementation — filename search
            _hasZoneProvider = false;
            _zoneProviderPath = null;
            var zoneProvCandidates = AssetDatabase.FindAssets("ZoneProvider Zone t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in zoneProvCandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue; // Skip framework interfaces
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.text.Contains("IZoneProvider"))
                {
                    _hasZoneProvider = true;
                    _zoneProviderPath = path;
                    break;
                }
            }

            // ISpawnPositionProvider implementation — filename search
            _hasSpawnPositionProvider = false;
            _spawnPositionProviderPath = null;
            var spawnProvCandidates = AssetDatabase.FindAssets("SpawnPosition SpawnPoint t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in spawnProvCandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.text.Contains("ISpawnPositionProvider"))
                {
                    _hasSpawnPositionProvider = true;
                    _spawnPositionProviderPath = path;
                    break;
                }
            }

            // IZoneUIProvider implementation — filename search
            _hasZoneUIProvider = false;
            _zoneUIProviderPath = null;
            var zoneUICandidates = AssetDatabase.FindAssets("ZoneUI ZoneHUD t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in zoneUICandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.text.Contains("IZoneUIProvider"))
                {
                    _hasZoneUIProvider = true;
                    _zoneUIProviderPath = path;
                    break;
                }
            }

            // === Meta-Progression Validation ===

            // 4. MetaUnlockTree asset
            var treeSO = Resources.Load<MetaUnlockTreeSO>("MetaUnlockTree");
            _hasUnlockTreeAsset = treeSO != null;
            _unlockTreeValidationError = null;
            if (treeSO != null && !treeSO.Validate(out _unlockTreeValidationError))
            {
                // Validation error stored — displayed in UI
            }
            else
            {
                _unlockTreeValidationError = null;
            }

            // 5. MetaStatApply bridge — filename search
            _hasMetaStatBridge = false;
            _metaStatBridgePath = null;
            var statBridgeCandidates = AssetDatabase.FindAssets("MetaStatApply t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in statBridgeCandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue;
                _hasMetaStatBridge = true;
                _metaStatBridgePath = path;
                break;
            }

            // 6. IMetaUIProvider — filename search
            _hasMetaUIProvider = false;
            _metaUIProviderPath = null;
            var metaUICandidates = AssetDatabase.FindAssets("MetaScreen MetaUI t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in metaUICandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.text.Contains("IMetaUIProvider"))
                {
                    _hasMetaUIProvider = true;
                    _metaUIProviderPath = path;
                    break;
                }
            }

            // === Modifiers Validation ===

            // 7. RunModifierPool asset
            var poolSO = Resources.Load<RunModifierPoolSO>("RunModifierPool");
            _hasModifierPoolAsset = poolSO != null;
            _modifierPoolValidationError = null;
            if (poolSO != null && !poolSO.Validate(out _modifierPoolValidationError))
            {
                // Validation error stored
            }
            else
            {
                _modifierPoolValidationError = null;
            }

            // 8. AscensionDefinition asset
            var ascSO = Resources.Load<AscensionDefinitionSO>("AscensionDefinition");
            _hasAscensionDefAsset = ascSO != null;
            _ascensionDefValidationError = null;
            if (ascSO != null && !ascSO.Validate(out _ascensionDefValidationError))
            {
                // Validation error stored
            }
            else
            {
                _ascensionDefValidationError = null;
            }

            // 9. IModifierUIProvider — filename search
            _hasModifierUIProvider = false;
            _modifierUIProviderPath = null;
            var modUICandidates = AssetDatabase.FindAssets("ModifierUI ModifierScreen t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in modUICandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.text.Contains("IModifierUIProvider"))
                {
                    _hasModifierUIProvider = true;
                    _modifierUIProviderPath = path;
                    break;
                }
            }

            // === Rewards Validation ===

            // 10. RewardPool asset
            _hasRewardPoolAsset = Resources.Load<RewardPoolSO>("RewardPool") != null;

            // 11. EventPool asset
            _hasEventPoolAsset = Resources.Load<EventPoolSO>("EventPool") != null;

            // 12. IRewardUIProvider — filename search
            _hasRewardUIProvider = false;
            _rewardUIProviderPath = null;
            var rewardUICandidates = AssetDatabase.FindAssets("RewardUI RewardScreen RewardHUD t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in rewardUICandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Rewards/Bridges/")) continue; // Skip framework file
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.text.Contains("IRewardUIProvider"))
                {
                    _hasRewardUIProvider = true;
                    _rewardUIProviderPath = path;
                    break;
                }
            }

            // === Analytics Validation ===

            // 13. IRunAnalyticsProvider — filename search
            _hasAnalyticsProvider = false;
            _analyticsProviderPath = null;
            var analyticsCandidates = AssetDatabase.FindAssets("Analytics Stats RunStats t:MonoScript", new[] { "Assets/Scripts" });
            foreach (var guid in analyticsCandidates)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Roguelite/")) continue; // Skip framework files
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.text.Contains("IRunAnalyticsProvider"))
                {
                    _hasAnalyticsProvider = true;
                    _analyticsProviderPath = path;
                    break;
                }
            }

            // === Filesystem checks (cached here, NOT in OnGUI) ===
            _hasRogueliteAssembly = File.Exists(
                Path.Combine(Application.dataPath, "Scripts", "Roguelite", "DIG.Roguelite.asmdef"));
            _hasTrackingSystem = File.Exists(
                Path.Combine(Application.dataPath, "Scripts", "Analytics", "Systems", "RunStatisticsTrackingSystem.cs"));
            _hasWorkstation = File.Exists(
                Path.Combine(Application.dataPath, "Editor", "RunWorkstation", "RunWorkstationWindow.cs"));
        }

        // ==================== REWARD POOL EDITOR ====================

        private void DrawRewardPoolSection()
        {
            _showRewardPoolEditor = EditorWindowUtilities.DrawFoldoutSection(_showRewardPoolEditor, "Reward & Event Pools", () =>
            {
                // Reward pool picker
                EditorGUI.BeginChangeCheck();
                _rewardPool = (RewardPoolSO)EditorGUILayout.ObjectField(
                    "RewardPool Asset", _rewardPool, typeof(RewardPoolSO), false);
                if (EditorGUI.EndChangeCheck())
                    RebuildRewardPoolEditor();

                if (_rewardPool == null)
                {
                    _rewardPool = Resources.Load<RewardPoolSO>("RewardPool");
                    if (_rewardPool != null) RebuildRewardPoolEditor();
                }

                if (_rewardPool != null)
                {
                    EditorGUILayout.Space(5);
                    if (_rewardPoolEditor == null) RebuildRewardPoolEditor();
                    if (_rewardPoolEditor != null) _rewardPoolEditor.OnInspectorGUI();

                    EditorGUILayout.Space(5);
                    if (_rewardPool.Entries != null && _rewardPool.Entries.Count > 0)
                    {
                        EditorGUILayout.LabelField("Reward Pool Summary", EditorStyles.boldLabel);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        int byType = 0;
                        var typeCounts = new int[8];
                        float totalWeight = 0f;
                        foreach (var e in _rewardPool.Entries)
                        {
                            if (e.Reward == null) continue;
                            typeCounts[(int)e.Reward.Type]++;
                            totalWeight += e.Weight;
                            byType++;
                        }

                        EditorGUILayout.LabelField($"Entries: {byType}  |  ChoiceCount: {_rewardPool.ChoiceCount}  |  Total Weight: {totalWeight:F1}");

                        EditorGUILayout.BeginHorizontal();
                        for (int i = 0; i < 8; i++)
                        {
                            if (typeCounts[i] > 0)
                                GUILayout.Label($"{(RewardType)i}: {typeCounts[i]}", EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.EndVertical();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "No RewardPool asset found. Create one via the validation section above.",
                        MessageType.Info);
                }

                EditorGUILayout.Space(10);

                // Event pool
                EditorGUI.BeginChangeCheck();
                _eventPool = (EventPoolSO)EditorGUILayout.ObjectField(
                    "EventPool Asset", _eventPool, typeof(EventPoolSO), false);
                if (EditorGUI.EndChangeCheck())
                    RebuildEventPoolEditor();

                if (_eventPool == null)
                {
                    _eventPool = Resources.Load<EventPoolSO>("EventPool");
                    if (_eventPool != null) RebuildEventPoolEditor();
                }

                if (_eventPool != null)
                {
                    EditorGUILayout.Space(5);
                    if (_eventPoolEditor == null) RebuildEventPoolEditor();
                    if (_eventPoolEditor != null) _eventPoolEditor.OnInspectorGUI();

                    if (_eventPool.Events != null && _eventPool.Events.Count > 0)
                    {
                        EditorGUILayout.LabelField($"Events: {_eventPool.Events.Count}", EditorStyles.miniLabel);
                    }
                }
            });
        }

        // ==================== RUNTIME REWARDS STATE ====================

        private void DrawRuntimeRewardsSection()
        {
            EditorGUILayout.LabelField("Rewards & Shop State", EditorStyles.boldLabel);

            if (!Application.isPlaying || _cachedWorld == null || !_cachedWorld.IsCreated)
                return;

            if (_cachedRunStateQuery == default || _cachedRunStateQuery.IsEmptyIgnoreFilter)
                return;

            var runEntity = _cachedRunStateQuery.GetSingletonEntity();

            // Pending reward choices
            if (_cachedWorld.EntityManager.HasBuffer<PendingRewardChoice>(runEntity))
            {
                var choices = _cachedWorld.EntityManager.GetBuffer<PendingRewardChoice>(runEntity, true);
                EditorWindowUtilities.DrawCountBadge("pending reward choices", choices.Length, 1);

                if (choices.Length > 0)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    for (int i = 0; i < choices.Length; i++)
                    {
                        var c = choices[i];
                        EditorGUILayout.LabelField(
                            $"  Slot {c.SlotIndex}: ID={c.RewardId} {c.Type} R{c.Rarity} Int={c.IntValue} Float={c.FloatValue:F2}{(c.IsSelected ? " [SELECTED]" : "")}",
                            EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            // Shop inventory
            if (_cachedWorld.EntityManager.HasBuffer<ShopInventoryEntry>(runEntity))
            {
                var shop = _cachedWorld.EntityManager.GetBuffer<ShopInventoryEntry>(runEntity, true);
                EditorWindowUtilities.DrawCountBadge("shop inventory entries", shop.Length, 1);

                if (shop.Length > 0)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    for (int i = 0; i < shop.Length; i++)
                    {
                        var e = shop[i];
                        string sold = e.IsSoldOut ? " [SOLD OUT]" : "";
                        EditorGUILayout.LabelField(
                            $"  [{i}] ID={e.RewardId} {e.Type} Price={e.Price}{sold}",
                            EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }

        // ==================== RUNTIME HELPERS ====================

        private void SetRunPhase(Unity.Entities.World world, RunPhase phase, bool resetFields)
        {
            if (_cachedRunStateQuery == default || _cachedRunStateQuery.IsEmptyIgnoreFilter) return;

            var run = _cachedRunStateQuery.GetSingleton<RunState>();

            if (resetFields)
            {
                run.RunId = (phase == RunPhase.Preparation) ? run.RunId + 1 : 0;
                run.Seed = (phase == RunPhase.Preparation)
                    ? (uint)UnityEngine.Random.Range(1, int.MaxValue)
                    : 0;
                run.CurrentZoneIndex = 0;
                run.ElapsedTime = 0f;
                run.Score = 0;
                run.RunCurrency = 0;
                run.EndReason = RunEndReason.None;
            }

            run.Phase = phase;
            _cachedRunStateQuery.SetSingleton(run);
        }

        private void TriggerPermadeath(Unity.Entities.World world)
        {
            if (_cachedRunStateQuery == default || _cachedRunStateQuery.IsEmptyIgnoreFilter) return;

            var entity = _cachedRunStateQuery.GetSingletonEntity();
            world.EntityManager.SetComponentEnabled<PermadeathSignal>(entity, true);
        }

        private void SetRunEndReason(Unity.Entities.World world, RunEndReason reason)
        {
            if (_cachedRunStateQuery == default || _cachedRunStateQuery.IsEmptyIgnoreFilter) return;

            var run = _cachedRunStateQuery.GetSingleton<RunState>();
            run.Phase = RunPhase.RunEnd;
            run.EndReason = reason;
            _cachedRunStateQuery.SetSingleton(run);
        }

        private void GrantMetaCurrency(Unity.Entities.World world, int amount)
        {
            if (_cachedMetaBankQuery == default || _cachedMetaBankQuery.IsEmptyIgnoreFilter) return;

            var bank = _cachedMetaBankQuery.GetSingleton<MetaBank>();
            bank.MetaCurrency += amount;
            bank.LifetimeMetaEarned += amount;
            _cachedMetaBankQuery.SetSingleton(bank);
        }

        private void AddRandomModifier(Unity.Entities.World world)
        {
            if (_cachedRunStateQuery == default || _cachedRunStateQuery.IsEmptyIgnoreFilter) return;

            if (_cachedModifierRegistryQuery == default)
                _cachedModifierRegistryQuery = world.EntityManager.CreateEntityQuery(
                    Unity.Entities.ComponentType.ReadOnly<ModifierRegistrySingleton>());
            if (_cachedModifierRegistryQuery.IsEmptyIgnoreFilter) return;

            var runEntity = _cachedRunStateQuery.GetSingletonEntity();
            if (!world.EntityManager.HasBuffer<RunModifierStack>(runEntity)) return;

            ref var registry = ref _cachedModifierRegistryQuery.GetSingleton<ModifierRegistrySingleton>().Registry.Value;
            if (registry.Modifiers.Length == 0) return;

            int idx = UnityEngine.Random.Range(0, registry.Modifiers.Length);
            var modStack = world.EntityManager.GetBuffer<RunModifierStack>(runEntity);
            ModifierStackUtility.TryAddModifier(ref registry, modStack, registry.Modifiers[idx].ModifierId);
        }

        private void ClearModifiers(Unity.Entities.World world)
        {
            if (_cachedRunStateQuery == default || _cachedRunStateQuery.IsEmptyIgnoreFilter) return;

            var runEntity = _cachedRunStateQuery.GetSingletonEntity();
            if (world.EntityManager.HasBuffer<RunModifierStack>(runEntity))
                world.EntityManager.GetBuffer<RunModifierStack>(runEntity).Clear();
        }

        private void ResetMetaBank(Unity.Entities.World world)
        {
            if (_cachedMetaBankQuery == default || _cachedMetaBankQuery.IsEmptyIgnoreFilter) return;

            _cachedMetaBankQuery.SetSingleton(new MetaBank());

            // Reset unlock flags
            var bankEntity = _cachedMetaBankQuery.GetSingletonEntity();
            if (world.EntityManager.HasBuffer<MetaUnlockEntry>(bankEntity))
            {
                var unlocks = world.EntityManager.GetBuffer<MetaUnlockEntry>(bankEntity);
                for (int i = 0; i < unlocks.Length; i++)
                {
                    var entry = unlocks[i];
                    entry.IsUnlocked = false;
                    unlocks[i] = entry;
                }
            }
        }

        // ==================== HELPERS ====================

        private void RebuildConfigEditor()
        {
            DestroyConfigEditor();
            if (_runConfig != null)
                _configEditor = UnityEditor.Editor.CreateEditor(_runConfig);
        }

        private void DestroyConfigEditor()
        {
            if (_configEditor != null)
            {
                DestroyImmediate(_configEditor);
                _configEditor = null;
            }
        }

        private void RebuildUnlockTreeEditor()
        {
            DestroyUnlockTreeEditor();
            if (_unlockTree != null)
                _unlockTreeEditor = UnityEditor.Editor.CreateEditor(_unlockTree);
        }

        private void DestroyUnlockTreeEditor()
        {
            if (_unlockTreeEditor != null)
            {
                DestroyImmediate(_unlockTreeEditor);
                _unlockTreeEditor = null;
            }
        }

        private void RebuildModifierPoolEditor()
        {
            DestroyModifierPoolEditor();
            if (_modifierPool != null)
                _modifierPoolEditor = UnityEditor.Editor.CreateEditor(_modifierPool);
        }

        private void DestroyModifierPoolEditor()
        {
            if (_modifierPoolEditor != null)
            {
                DestroyImmediate(_modifierPoolEditor);
                _modifierPoolEditor = null;
            }
        }

        private void RebuildAscensionDefEditor()
        {
            DestroyAscensionDefEditor();
            if (_ascensionDef != null)
                _ascensionDefEditor = UnityEditor.Editor.CreateEditor(_ascensionDef);
        }

        private void DestroyAscensionDefEditor()
        {
            if (_ascensionDefEditor != null)
            {
                DestroyImmediate(_ascensionDefEditor);
                _ascensionDefEditor = null;
            }
        }

        private void RebuildRewardPoolEditor()
        {
            DestroyRewardPoolEditor();
            if (_rewardPool != null)
                _rewardPoolEditor = UnityEditor.Editor.CreateEditor(_rewardPool);
        }

        private void DestroyRewardPoolEditor()
        {
            if (_rewardPoolEditor != null)
            {
                DestroyImmediate(_rewardPoolEditor);
                _rewardPoolEditor = null;
            }
        }

        private void RebuildEventPoolEditor()
        {
            DestroyEventPoolEditor();
            if (_eventPool != null)
                _eventPoolEditor = UnityEditor.Editor.CreateEditor(_eventPool);
        }

        private void DestroyEventPoolEditor()
        {
            if (_eventPoolEditor != null)
            {
                DestroyImmediate(_eventPoolEditor);
                _eventPoolEditor = null;
            }
        }

        private void CreateRewardPoolAsset()
        {
            var resourcesPath = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);

            var asset = CreateInstance<RewardPoolSO>();
            asset.PoolName = "Default Reward Pool";
            asset.ChoiceCount = 3;
            asset.Entries = new System.Collections.Generic.List<RewardPoolEntry>();

            AssetDatabase.CreateAsset(asset, "Assets/Resources/RewardPool.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _rewardPool = asset;
            RebuildRewardPoolEditor();
            RefreshValidation();

            EditorGUIUtility.PingObject(asset);
            Debug.Log("[RogueliteSetup] Created RewardPool asset at Assets/Resources/RewardPool.asset. Add RewardDefinitionSO entries to populate.");
        }

        private void CreateEventPoolAsset()
        {
            var resourcesPath = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);

            var asset = CreateInstance<EventPoolSO>();
            asset.Events = new System.Collections.Generic.List<RunEventDefinitionSO>();

            AssetDatabase.CreateAsset(asset, "Assets/Resources/EventPool.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _eventPool = asset;
            RebuildEventPoolEditor();
            RefreshValidation();

            EditorGUIUtility.PingObject(asset);
            Debug.Log("[RogueliteSetup] Created EventPool asset at Assets/Resources/EventPool.asset. Add RunEventDefinitionSO entries to populate.");
        }
    }
}
#endif
