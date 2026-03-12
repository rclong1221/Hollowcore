#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using DIG.AI.Authoring;
using DIG.AI.Components;

namespace DIG.AI.Editor
{
    /// <summary>
    /// EPIC 15.32: Encounter Designer — 4-panel editor window for authoring
    /// boss encounters, ability profiles, triggers, and phases.
    /// </summary>
    public class EncounterDesignerWindow : EditorWindow
    {
        // Asset references
        private EncounterProfileSO _encounterProfile;
        private AbilityProfileSO _abilityProfile;
        private AbilityDefinitionSO _selectedAbility;
        private int _selectedPhaseIndex = -1;
        private int _selectedTriggerIndex = -1;

        // UI state
        private Vector2 _libraryScroll;
        private Vector2 _timelineScroll;
        private Vector2 _triggerScroll;
        private Vector2 _inspectorScroll;
        private string _searchFilter = "";
        private int _inspectorTab;
        private List<AbilityDefinitionSO> _allAbilities = new();
        private List<ValidationResult> _validationResults = new();

        // Layout
        private float _leftPanelWidth = 220f;
        private float _topPanelHeight;
        private const float ToolbarHeight = 24f;
        private static readonly string[] InspectorTabs = { "Targeting", "Timing", "Damage", "Effects", "Conditions", "Telegraph" };

        [MenuItem("DIG/Encounter Designer")]
        public static void ShowWindow()
        {
            var window = GetWindow<EncounterDesignerWindow>("Encounter Designer");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            RefreshAbilityLibrary();
        }

        private void OnGUI()
        {
            DrawToolbar();

            var contentRect = new Rect(0, ToolbarHeight, position.width, position.height - ToolbarHeight);
            _topPanelHeight = contentRect.height * 0.55f;

            // Top row: Library (left) + Timeline (right)
            var libraryRect = new Rect(0, ToolbarHeight, _leftPanelWidth, _topPanelHeight);
            var timelineRect = new Rect(_leftPanelWidth, ToolbarHeight, contentRect.width - _leftPanelWidth, _topPanelHeight);

            // Bottom row: Trigger Editor (left) + Ability Inspector (right)
            float bottomY = ToolbarHeight + _topPanelHeight;
            float bottomHeight = contentRect.height - _topPanelHeight;
            var triggerRect = new Rect(0, bottomY, _leftPanelWidth, bottomHeight);
            var inspectorRect = new Rect(_leftPanelWidth, bottomY, contentRect.width - _leftPanelWidth, bottomHeight);

            DrawAbilityLibrary(libraryRect);
            DrawEncounterTimeline(timelineRect);
            DrawTriggerEditor(triggerRect);
            DrawAbilityInspector(inspectorRect);

            // Draw panel borders
            Handles.color = new Color(0.2f, 0.2f, 0.2f);
            Handles.DrawLine(new Vector3(_leftPanelWidth, ToolbarHeight), new Vector3(_leftPanelWidth, position.height));
            Handles.DrawLine(new Vector3(0, bottomY), new Vector3(position.width, bottomY));
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("Encounter:", GUILayout.Width(70));
            _encounterProfile = (EncounterProfileSO)EditorGUILayout.ObjectField(
                _encounterProfile, typeof(EncounterProfileSO), false, GUILayout.Width(200));

            EditorGUILayout.LabelField("Abilities:", GUILayout.Width(60));
            _abilityProfile = (AbilityProfileSO)EditorGUILayout.ObjectField(
                _abilityProfile, typeof(AbilityProfileSO), false, GUILayout.Width(200));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40)))
                CreateNewEncounter();

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(40)))
                SaveAll();

            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(60)))
                RunValidation();

            if (GUILayout.Button("Test", EditorStyles.toolbarButton, GUILayout.Width(40)))
                RunSimulation();

            EditorGUILayout.EndHorizontal();
        }

        #region Ability Library Panel

        private void DrawAbilityLibrary(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.LabelField("Ability Library", EditorStyles.boldLabel);

            _searchFilter = EditorGUILayout.TextField("Search:", _searchFilter);

            _libraryScroll = EditorGUILayout.BeginScrollView(_libraryScroll);

            var filtered = _allAbilities
                .Where(a => a != null && (string.IsNullOrEmpty(_searchFilter) ||
                    a.AbilityName.ToLower().Contains(_searchFilter.ToLower()) ||
                    a.DamageType.ToString().ToLower().Contains(_searchFilter.ToLower())))
                .OrderBy(a => a.DamageType)
                .ThenBy(a => a.AbilityName);

            foreach (var ability in filtered)
            {
                bool isSelected = _selectedAbility == ability;
                var style = isSelected ? EditorStyles.selectionRect : EditorStyles.helpBox;

                EditorGUILayout.BeginHorizontal(style);

                // Color indicator by damage type
                var prevColor = GUI.color;
                GUI.color = GetDamageTypeColor(ability.DamageType);
                EditorGUILayout.LabelField("■", GUILayout.Width(14));
                GUI.color = prevColor;

                if (GUILayout.Button(ability.AbilityName, EditorStyles.label))
                {
                    _selectedAbility = ability;
                    Selection.activeObject = ability;
                }

                EditorGUILayout.LabelField($"R:{ability.Range:F0}", GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ New Ability"))
                CreateNewAbility();

            if (GUILayout.Button("Refresh"))
                RefreshAbilityLibrary();

            GUILayout.EndArea();
        }

        #endregion

        #region Encounter Timeline Panel

        private void DrawEncounterTimeline(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.LabelField("Encounter Timeline", EditorStyles.boldLabel);

            if (_encounterProfile == null)
            {
                EditorGUILayout.HelpBox("Assign an Encounter Profile to view timeline.", MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            _timelineScroll = EditorGUILayout.BeginScrollView(_timelineScroll);

            // HP bar visualization
            DrawHPBar(rect.width - _leftPanelWidth - 20f);

            EditorGUILayout.Space(10);

            // Phase list
            var phases = _encounterProfile.Phases;
            for (int i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                bool isSelected = _selectedPhaseIndex == i;

                EditorGUILayout.BeginVertical(isSelected ? EditorStyles.selectionRect : EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button($"Phase {i}: {phase.PhaseName}", EditorStyles.boldLabel))
                    _selectedPhaseIndex = i;

                EditorGUILayout.LabelField($"HP ≤ {phase.HPThresholdEntry:P0}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"Spd:{phase.SpeedMultiplier:F1}x Dmg:{phase.DamageMultiplier:F1}x", GUILayout.Width(140));

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    phases.RemoveAt(i);
                    EditorUtility.SetDirty(_encounterProfile);
                    i--;
                }

                EditorGUILayout.EndHorizontal();

                if (isSelected)
                {
                    EditorGUI.indentLevel++;
                    phase.PhaseName = EditorGUILayout.TextField("Name", phase.PhaseName);
                    phase.HPThresholdEntry = EditorGUILayout.Slider("HP Threshold", phase.HPThresholdEntry, -1f, 1f);
                    phase.SpeedMultiplier = EditorGUILayout.FloatField("Speed Multiplier", phase.SpeedMultiplier);
                    phase.DamageMultiplier = EditorGUILayout.FloatField("Damage Multiplier", phase.DamageMultiplier);
                    phase.InvulnerableDuration = EditorGUILayout.FloatField("Invuln Duration", phase.InvulnerableDuration);
                    phase.TransitionAbility = (AbilityDefinitionSO)EditorGUILayout.ObjectField(
                        "Transition Ability", phase.TransitionAbility, typeof(AbilityDefinitionSO), false);
                    phase.SpawnGroupId = (byte)EditorGUILayout.IntSlider("Spawn Group", phase.SpawnGroupId, 0, 3);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Phase"))
            {
                phases.Add(new PhaseEntry { PhaseName = $"Phase {phases.Count}" });
                EditorUtility.SetDirty(_encounterProfile);
            }

            // Abilities per phase (from ability profile)
            if (_abilityProfile != null && _abilityProfile.Abilities != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Ability Rotation:", EditorStyles.boldLabel);
                for (int i = 0; i < _abilityProfile.Abilities.Count; i++)
                {
                    var ab = _abilityProfile.Abilities[i];
                    if (ab == null) continue;
                    string phaseRange = $"P{ab.PhaseMin}-{ab.PhaseMax}";
                    EditorGUILayout.LabelField($"  [{i}] {ab.AbilityName} ({phaseRange}, CD:{ab.Cooldown:F1}s)");
                }
            }

            // Validation results
            if (_validationResults.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Validation:", EditorStyles.boldLabel);
                foreach (var result in _validationResults)
                {
                    var msgType = result.Severity switch
                    {
                        ValidationSeverity.Error => MessageType.Error,
                        ValidationSeverity.Warning => MessageType.Warning,
                        _ => MessageType.Info
                    };
                    EditorGUILayout.HelpBox($"[{result.Context}] {result.Message}", msgType);
                }
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawHPBar(float width)
        {
            if (_encounterProfile == null || _encounterProfile.Phases.Count == 0) return;

            var barRect = GUILayoutUtility.GetRect(width, 30f);
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));

            var sortedPhases = _encounterProfile.Phases
                .Where(p => p.HPThresholdEntry >= 0)
                .OrderByDescending(p => p.HPThresholdEntry)
                .ToList();

            Color[] phaseColors = {
                new(0.2f, 0.7f, 0.2f), new(0.7f, 0.7f, 0.2f),
                new(0.7f, 0.4f, 0.2f), new(0.7f, 0.2f, 0.2f)
            };

            float prevX = barRect.x;
            for (int i = 0; i < sortedPhases.Count; i++)
            {
                float threshold = sortedPhases[i].HPThresholdEntry;
                float nextThreshold = (i + 1 < sortedPhases.Count) ? sortedPhases[i + 1].HPThresholdEntry : 0f;
                float startX = barRect.x + (1f - threshold) * barRect.width;
                float endX = barRect.x + (1f - nextThreshold) * barRect.width;

                var color = i < phaseColors.Length ? phaseColors[i] : Color.gray;
                EditorGUI.DrawRect(new Rect(startX, barRect.y, endX - startX, barRect.height), color);

                // Label
                var labelRect = new Rect(startX + 2, barRect.y + 2, endX - startX - 4, barRect.height - 4);
                GUI.Label(labelRect, $"{sortedPhases[i].PhaseName} ({threshold:P0})", EditorStyles.miniLabel);
            }
        }

        #endregion

        #region Trigger Editor Panel

        private void DrawTriggerEditor(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.LabelField("Trigger Editor", EditorStyles.boldLabel);

            if (_encounterProfile == null)
            {
                EditorGUILayout.HelpBox("Assign an Encounter Profile.", MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            _triggerScroll = EditorGUILayout.BeginScrollView(_triggerScroll);

            var triggers = _encounterProfile.Triggers;
            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                bool isSelected = _selectedTriggerIndex == i;

                EditorGUILayout.BeginVertical(isSelected ? EditorStyles.selectionRect : EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"[{i}] {trigger.TriggerName}", EditorStyles.miniLabel))
                    _selectedTriggerIndex = i;

                if (GUILayout.Button("X", GUILayout.Width(18)))
                {
                    triggers.RemoveAt(i);
                    EditorUtility.SetDirty(_encounterProfile);
                    i--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                if (isSelected)
                {
                    trigger.TriggerName = EditorGUILayout.TextField("Name", trigger.TriggerName);

                    // Condition
                    EditorGUILayout.LabelField("Condition:", EditorStyles.miniBoldLabel);
                    trigger.Condition = (TriggerConditionType)EditorGUILayout.EnumPopup("Type", trigger.Condition);
                    DrawConditionFields(trigger);

                    EditorGUILayout.Space(3);

                    // Action
                    EditorGUILayout.LabelField("Action:", EditorStyles.miniBoldLabel);
                    trigger.Action = (TriggerActionType)EditorGUILayout.EnumPopup("Type", trigger.Action);
                    DrawActionFields(trigger);

                    EditorGUILayout.Space(3);

                    // Options
                    trigger.FireOnce = EditorGUILayout.Toggle("Fire Once", trigger.FireOnce);
                    trigger.Delay = EditorGUILayout.FloatField("Delay (s)", trigger.Delay);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add Trigger"))
            {
                triggers.Add(new TriggerEntry { TriggerName = $"Trigger {triggers.Count}" });
                EditorUtility.SetDirty(_encounterProfile);
            }

            GUILayout.EndArea();
        }

        private void DrawConditionFields(TriggerEntry trigger)
        {
            switch (trigger.Condition)
            {
                case TriggerConditionType.HPBelow:
                case TriggerConditionType.HPAbove:
                    trigger.ConditionValue = EditorGUILayout.Slider("HP %", trigger.ConditionValue, 0f, 1f);
                    break;
                case TriggerConditionType.TimerElapsed:
                    trigger.ConditionValue = EditorGUILayout.FloatField("Seconds", trigger.ConditionValue);
                    trigger.ConditionParam = (byte)EditorGUILayout.IntPopup("Timer",
                        trigger.ConditionParam, new[] { "Encounter", "Phase" }, new[] { 0, 1 });
                    break;
                case TriggerConditionType.AddsDead:
                case TriggerConditionType.AddsAlive:
                    trigger.ConditionParam = (byte)EditorGUILayout.IntSlider("Group ID", trigger.ConditionParam, 0, 3);
                    trigger.ConditionValue = EditorGUILayout.FloatField("Count", trigger.ConditionValue);
                    break;
                case TriggerConditionType.AbilityCastCount:
                    trigger.ConditionValue = EditorGUILayout.FloatField("Cast Count >=", trigger.ConditionValue);
                    break;
                case TriggerConditionType.PhaseIs:
                    trigger.ConditionValue = EditorGUILayout.FloatField("Phase Index", trigger.ConditionValue);
                    break;
                case TriggerConditionType.BossAtPosition:
                    trigger.ConditionPosition = EditorGUILayout.Vector3Field("Position", trigger.ConditionPosition);
                    trigger.ConditionRange = EditorGUILayout.FloatField("Range", trigger.ConditionRange);
                    break;
                case TriggerConditionType.Composite_AND:
                case TriggerConditionType.Composite_OR:
                    trigger.SubTriggerIndex0 = EditorGUILayout.IntField("Sub 0", trigger.SubTriggerIndex0);
                    trigger.SubTriggerIndex1 = EditorGUILayout.IntField("Sub 1", trigger.SubTriggerIndex1);
                    trigger.SubTriggerIndex2 = EditorGUILayout.IntField("Sub 2", trigger.SubTriggerIndex2);
                    break;
            }
        }

        private void DrawActionFields(TriggerEntry trigger)
        {
            switch (trigger.Action)
            {
                case TriggerActionType.TransitionPhase:
                    trigger.ActionValue = EditorGUILayout.FloatField("Target Phase", trigger.ActionValue);
                    break;
                case TriggerActionType.ForceAbility:
                    trigger.ActionParam = (byte)EditorGUILayout.IntField("Ability ID", trigger.ActionParam);
                    break;
                case TriggerActionType.SetInvulnerable:
                    trigger.ActionValue = EditorGUILayout.FloatField("Duration (s)", trigger.ActionValue);
                    break;
                case TriggerActionType.Teleport:
                    trigger.ActionPosition = EditorGUILayout.Vector3Field("Position", trigger.ActionPosition);
                    break;
                case TriggerActionType.SpawnAddGroup:
                    trigger.ActionParam = (byte)EditorGUILayout.IntSlider("Group ID", trigger.ActionParam, 0, 3);
                    break;
                case TriggerActionType.EnableTrigger:
                case TriggerActionType.DisableTrigger:
                    trigger.ActionParam = (byte)EditorGUILayout.IntField("Trigger Index", trigger.ActionParam);
                    break;
            }
        }

        #endregion

        #region Ability Inspector Panel

        private void DrawAbilityInspector(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.LabelField("Ability Inspector", EditorStyles.boldLabel);

            if (_selectedAbility == null)
            {
                EditorGUILayout.HelpBox("Select an ability from the library.", MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            EditorGUILayout.LabelField(_selectedAbility.AbilityName, EditorStyles.largeLabel);

            _inspectorTab = GUILayout.Toolbar(_inspectorTab, InspectorTabs);

            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

            var so = new SerializedObject(_selectedAbility);
            so.Update();

            switch (_inspectorTab)
            {
                case 0: DrawTargetingTab(so); break;
                case 1: DrawTimingTab(so); break;
                case 2: DrawDamageTab(so); break;
                case 3: DrawEffectsTab(so); break;
                case 4: DrawConditionsTab(so); break;
                case 5: DrawTelegraphTab(so); break;
            }

            so.ApplyModifiedProperties();

            // Inline validation warnings
            DrawAbilityWarnings();

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTargetingTab(SerializedObject so)
        {
            EditorGUILayout.PropertyField(so.FindProperty("TargetingMode"));
            EditorGUILayout.PropertyField(so.FindProperty("Range"));
            EditorGUILayout.PropertyField(so.FindProperty("Radius"));
            EditorGUILayout.PropertyField(so.FindProperty("Angle"));
            EditorGUILayout.PropertyField(so.FindProperty("MaxTargets"));
            EditorGUILayout.PropertyField(so.FindProperty("RequiresLineOfSight"));
        }

        private void DrawTimingTab(SerializedObject so)
        {
            EditorGUILayout.PropertyField(so.FindProperty("CastTime"));
            EditorGUILayout.PropertyField(so.FindProperty("ActiveDuration"));
            EditorGUILayout.PropertyField(so.FindProperty("RecoveryTime"));
            EditorGUILayout.PropertyField(so.FindProperty("Cooldown"));
            EditorGUILayout.PropertyField(so.FindProperty("GlobalCooldown"));
            EditorGUILayout.PropertyField(so.FindProperty("TickInterval"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Charges", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("MaxCharges"));
            EditorGUILayout.PropertyField(so.FindProperty("ChargeRegenTime"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Cooldown Group", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("CooldownGroupId"));
            EditorGUILayout.PropertyField(so.FindProperty("CooldownGroupDuration"));
        }

        private void DrawDamageTab(SerializedObject so)
        {
            EditorGUILayout.PropertyField(so.FindProperty("DamageBase"));
            EditorGUILayout.PropertyField(so.FindProperty("DamageVariance"));
            EditorGUILayout.PropertyField(so.FindProperty("DamageType"));
            EditorGUILayout.PropertyField(so.FindProperty("HitCount"));
            EditorGUILayout.PropertyField(so.FindProperty("CanCrit"));
            EditorGUILayout.PropertyField(so.FindProperty("HitboxMultiplier"));
            EditorGUILayout.PropertyField(so.FindProperty("ResolverType"));
        }

        private void DrawEffectsTab(SerializedObject so)
        {
            EditorGUILayout.LabelField("Modifier 1", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Modifier0"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Modifier 2", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Modifier1"));
        }

        private void DrawConditionsTab(SerializedObject so)
        {
            EditorGUILayout.PropertyField(so.FindProperty("PhaseMin"));
            EditorGUILayout.PropertyField(so.FindProperty("PhaseMax"));
            EditorGUILayout.PropertyField(so.FindProperty("HPThresholdMin"));
            EditorGUILayout.PropertyField(so.FindProperty("HPThresholdMax"));
            EditorGUILayout.PropertyField(so.FindProperty("MinTargetsInRange"));
            EditorGUILayout.PropertyField(so.FindProperty("MovementDuringCast"));
            EditorGUILayout.PropertyField(so.FindProperty("Interruptible"));
            EditorGUILayout.PropertyField(so.FindProperty("PriorityWeight"));
        }

        private void DrawTelegraphTab(SerializedObject so)
        {
            EditorGUILayout.PropertyField(so.FindProperty("TelegraphShape"));
            EditorGUILayout.PropertyField(so.FindProperty("TelegraphDuration"));
            EditorGUILayout.PropertyField(so.FindProperty("TelegraphDamageOnExpire"));
        }

        private void DrawAbilityWarnings()
        {
            if (_selectedAbility == null) return;

            if (_selectedAbility.TelegraphDuration > 0 && _selectedAbility.TelegraphShape == TelegraphShape.None)
                EditorGUILayout.HelpBox("Telegraph duration set but TelegraphShape is None.", MessageType.Warning);

            if (_selectedAbility.CooldownGroupId > 0 && _selectedAbility.CooldownGroupDuration <= 0)
                EditorGUILayout.HelpBox("Cooldown group set but duration is 0.", MessageType.Warning);

            if (_selectedAbility.Radius > 0 && _selectedAbility.TargetingMode == AbilityTargetingMode.CurrentTarget)
                EditorGUILayout.HelpBox("Radius > 0 but targeting is CurrentTarget. Should it be AllInRange?", MessageType.Warning);

            if (_selectedAbility.MaxCharges > 0 && _selectedAbility.ChargeRegenTime <= 0)
                EditorGUILayout.HelpBox("MaxCharges > 0 but ChargeRegenTime is 0 — charges won't regenerate.", MessageType.Warning);
        }

        #endregion

        #region Actions

        private void CreateNewEncounter()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Encounter Profile", "NewEncounter", "asset", "Create encounter profile");
            if (string.IsNullOrEmpty(path)) return;

            var profile = CreateInstance<EncounterProfileSO>();
            profile.Phases.Add(new PhaseEntry { PhaseName = "Phase 0", HPThresholdEntry = 1.0f });
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            _encounterProfile = profile;
        }

        private void CreateNewAbility()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Ability", "NewAbility", "asset", "Create ability definition");
            if (string.IsNullOrEmpty(path)) return;

            var ability = CreateInstance<AbilityDefinitionSO>();
            ability.AbilityName = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(ability, path);
            AssetDatabase.SaveAssets();
            RefreshAbilityLibrary();
            _selectedAbility = ability;
        }

        private void SaveAll()
        {
            if (_encounterProfile != null) EditorUtility.SetDirty(_encounterProfile);
            if (_abilityProfile != null) EditorUtility.SetDirty(_abilityProfile);
            if (_selectedAbility != null) EditorUtility.SetDirty(_selectedAbility);
            AssetDatabase.SaveAssets();
        }

        private void RunValidation()
        {
            _validationResults = EncounterValidator.Validate(_encounterProfile, _abilityProfile);
            Repaint();
        }

        private void RunSimulation()
        {
            if (_encounterProfile == null || _abilityProfile == null)
            {
                EditorUtility.DisplayDialog("Simulation", "Assign both an Encounter Profile and Ability Profile.", "OK");
                return;
            }

            var sim = new EncounterSimulator();
            var events = sim.Simulate(_encounterProfile, _abilityProfile);

            string log = "=== Encounter Simulation ===\n";
            foreach (var evt in events)
            {
                int min = (int)(evt.Time / 60f);
                int sec = (int)(evt.Time % 60f);
                log += $"{min}:{sec:D2} — HP:{evt.HP:P0} — {evt.Description}\n";
            }

            if (sim.Warnings.Count > 0)
            {
                log += "\n=== Warnings ===\n";
                foreach (var w in sim.Warnings)
                    log += $"  {w}\n";
            }

            Debug.Log(log);
            EditorUtility.DisplayDialog("Simulation Complete",
                $"{events.Count} events logged. {sim.Warnings.Count} warnings. See Console for details.", "OK");
        }

        private void RefreshAbilityLibrary()
        {
            _allAbilities = AssetDatabase.FindAssets("t:AbilityDefinitionSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null)
                .ToList();
        }

        #endregion

        #region Helpers

        private Color GetDamageTypeColor(DIG.Targeting.Theming.DamageType type)
        {
            return type switch
            {
                DIG.Targeting.Theming.DamageType.Fire => new Color(1f, 0.4f, 0.2f),
                DIG.Targeting.Theming.DamageType.Ice => new Color(0.3f, 0.7f, 1f),
                DIG.Targeting.Theming.DamageType.Lightning => new Color(1f, 1f, 0.3f),
                DIG.Targeting.Theming.DamageType.Poison => new Color(0.3f, 0.8f, 0.3f),
                DIG.Targeting.Theming.DamageType.Holy => new Color(1f, 1f, 0.8f),
                DIG.Targeting.Theming.DamageType.Shadow => new Color(0.5f, 0.2f, 0.8f),
                DIG.Targeting.Theming.DamageType.Arcane => new Color(0.8f, 0.3f, 1f),
                _ => new Color(0.7f, 0.7f, 0.7f) // Physical
            };
        }

        #endregion
    }
}
#endif
