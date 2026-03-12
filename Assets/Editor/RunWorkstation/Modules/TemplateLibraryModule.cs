#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.7: Template & Preset Library module.
    /// One-click creation of complete run configurations from proven templates.
    /// Supports built-in templates and custom user-saved templates.
    /// </summary>
    public class TemplateLibraryModule : IRunWorkstationModule
    {
        public string TabName => "Templates";

        private RogueliteDataContext _context;
        private Vector2 _scrollPos;
        private RunConfigTemplate[] _builtInTemplates;
        private RunConfigTemplate[] _customTemplates;
        private int _selectedTemplate = -1;
        private bool _isBuiltIn = true;
        private bool _showBuiltIn = true;
        private bool _showCustom = true;

        // Clone source
        private RunConfigSO _cloneSource;

        private const string TemplatesFolder = "Assets/Data/Roguelite/Templates";
        private const string OutputFolder = "Assets/Data/Roguelite";

        public void OnEnable()
        {
            _builtInTemplates = BuiltInTemplates.GetAll();
            RefreshCustomTemplates();
        }

        public void OnDisable()
        {
            // Cleanup built-in template instances
            if (_builtInTemplates != null)
            {
                for (int i = 0; i < _builtInTemplates.Length; i++)
                {
                    if (_builtInTemplates[i] != null)
                        Object.DestroyImmediate(_builtInTemplates[i]);
                }
            }
        }

        public void SetContext(RogueliteDataContext context)
        {
            _context = context;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Template & Preset Library", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Built-in templates
            _showBuiltIn = EditorGUILayout.Foldout(_showBuiltIn, $"Built-In Templates ({_builtInTemplates?.Length ?? 0})", true);
            if (_showBuiltIn && _builtInTemplates != null)
                DrawTemplateGrid(_builtInTemplates, true);

            EditorGUILayout.Space(8);

            // Custom templates
            _showCustom = EditorGUILayout.Foldout(_showCustom, $"Custom Templates ({_customTemplates?.Length ?? 0})", true);
            if (_showCustom)
            {
                if (_customTemplates != null && _customTemplates.Length > 0)
                    DrawTemplateGrid(_customTemplates, false);
                else
                    EditorGUILayout.HelpBox("No custom templates. Save one from an existing RunConfig below.", MessageType.Info);
            }

            EditorGUILayout.Space(8);

            // Selected template details
            DrawSelectedTemplate();

            EditorGUILayout.Space(8);

            // Clone from existing
            DrawCloneSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTemplateGrid(RunConfigTemplate[] templates, bool builtIn)
        {
            EditorGUI.indentLevel++;
            int columns = Mathf.Max(1, (int)((EditorGUIUtility.currentViewWidth - 40) / 220));
            int col = 0;

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < templates.Length; i++)
            {
                var t = templates[i];
                if (t == null) continue;

                if (col >= columns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    col = 0;
                }

                bool isSelected = _selectedTemplate == i && _isBuiltIn == builtIn;
                DrawTemplateCard(t, i, builtIn, isSelected);
                col++;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawTemplateCard(RunConfigTemplate template, int index, bool builtIn, bool isSelected)
        {
            var boxStyle = isSelected ? "selectionRect" : "box";
            EditorGUILayout.BeginVertical(boxStyle, GUILayout.Width(200), GUILayout.Height(90));

            // Header
            EditorGUILayout.LabelField(template.TemplateName, EditorStyles.boldLabel);

            // Zone pattern summary
            string pattern = "";
            if (template.Zones != null)
            {
                for (int z = 0; z < Mathf.Min(template.Zones.Length, 6); z++)
                {
                    if (z > 0) pattern += "-";
                    pattern += GetZoneTypeAbbrev(template.Zones[z].Type);
                }
                if (template.Zones.Length > 6) pattern += "...";
            }
            EditorGUILayout.LabelField($"{template.ZoneCount} zones: {pattern}", EditorStyles.miniLabel);

            // Description
            if (!string.IsNullOrEmpty(template.Description))
            {
                string desc = template.Description.Length > 60
                    ? template.Description.Substring(0, 57) + "..."
                    : template.Description;
                EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
            }

            // Select button
            if (GUILayout.Button(isSelected ? "Selected" : "Select", EditorStyles.miniButton))
            {
                _selectedTemplate = index;
                _isBuiltIn = builtIn;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedTemplate()
        {
            RunConfigTemplate selected = GetSelectedTemplate();
            if (selected == null) return;

            EditorGUILayout.LabelField("Selected Template", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField($"Name: {selected.TemplateName}");
            EditorGUILayout.LabelField($"Zones: {selected.ZoneCount}");
            if (selected.EnableLooping)
                EditorGUILayout.LabelField($"Looping: from layer {selected.LoopStartIndex}, {selected.LoopDifficultyMultiplier:F1}x");
            EditorGUILayout.LabelField($"Currency/Zone: {selected.CurrencyPerZoneClear}");
            EditorGUILayout.LabelField($"Director: Budget={selected.DefaultInitialBudget:F0}, Rate={selected.DefaultCreditsPerSecond:F1}/s, MaxAlive={selected.DefaultMaxAliveEnemies}");

            // Zone breakdown
            if (selected.Zones != null)
            {
                EditorGUILayout.Space(4);
                for (int i = 0; i < selected.Zones.Length; i++)
                {
                    var z = selected.Zones[i];
                    EditorGUILayout.LabelField($"  {i}: {z.Type} | {z.ClearMode} | {z.DifficultyMultiplier:F1}x" +
                        (z.SelectionMode != ZoneSelectionMode.Fixed ? $" | {z.SelectionMode}" : ""),
                        EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Apply Template — Create Assets", GUILayout.Height(28)))
                ApplyTemplate(selected);

            EditorGUILayout.EndVertical();
        }

        private void DrawCloneSection()
        {
            EditorGUILayout.LabelField("Clone Existing Config", EditorStyles.boldLabel);
            _cloneSource = (RunConfigSO)EditorGUILayout.ObjectField("Source Config", _cloneSource, typeof(RunConfigSO), false);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _cloneSource != null;
            if (GUILayout.Button("Clone as New Config"))
                CloneConfig(_cloneSource);
            if (GUILayout.Button("Save as Custom Template"))
                SaveAsTemplate(_cloneSource);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        // ==================== Template Application ====================

        private void ApplyTemplate(RunConfigTemplate template)
        {
            string folder = $"{OutputFolder}/{template.TemplateName.Replace(" ", "")}";
            EnsureFolderExists(folder);

            // Create SpawnDirectorConfig
            string directorPath = $"{folder}/SpawnDirector_{template.TemplateName.Replace(" ", "")}.asset";
            var director = AssetDatabase.LoadAssetAtPath<SpawnDirectorConfigSO>(directorPath);
            if (director == null)
            {
                director = ScriptableObject.CreateInstance<SpawnDirectorConfigSO>();
                director.InitialBudget = template.DefaultInitialBudget;
                director.CreditsPerSecond = template.DefaultCreditsPerSecond;
                director.Acceleration = template.DefaultAcceleration;
                director.MaxAliveEnemies = template.DefaultMaxAliveEnemies;
                director.EliteChance = template.DefaultEliteChance;
                director.EliteMinDifficulty = template.DefaultEliteMinDifficulty;
                AssetDatabase.CreateAsset(director, directorPath);
            }

            // Create ZoneDefinitions
            var zoneDefs = new ZoneDefinitionSO[template.Zones?.Length ?? 0];
            for (int i = 0; i < zoneDefs.Length; i++)
            {
                var zt = template.Zones[i];
                string zonePath = $"{folder}/Zone_{i}_{zt.Type}.asset";
                zoneDefs[i] = AssetDatabase.LoadAssetAtPath<ZoneDefinitionSO>(zonePath);
                if (zoneDefs[i] == null)
                {
                    zoneDefs[i] = ScriptableObject.CreateInstance<ZoneDefinitionSO>();
                    zoneDefs[i].ZoneId = i;
                    zoneDefs[i].DisplayName = $"{zt.Type} {i}";
                    zoneDefs[i].Type = zt.Type;
                    zoneDefs[i].ClearMode = zt.ClearMode;
                    zoneDefs[i].DifficultyMultiplier = zt.DifficultyMultiplier;
                    zoneDefs[i].SpawnDirectorConfig = director;
                    AssetDatabase.CreateAsset(zoneDefs[i], zonePath);
                }
            }

            // Create ZoneSequence
            string seqPath = $"{folder}/Sequence_{template.TemplateName.Replace(" ", "")}.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<ZoneSequenceSO>(seqPath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<ZoneSequenceSO>();
                sequence.SequenceName = template.TemplateName;
                sequence.EnableLooping = template.EnableLooping;
                sequence.LoopStartIndex = template.LoopStartIndex;
                sequence.LoopDifficultyMultiplier = template.LoopDifficultyMultiplier;
                sequence.Layers = new List<ZoneSequenceLayer>();

                for (int i = 0; i < zoneDefs.Length; i++)
                {
                    var zt = template.Zones[i];
                    var layer = new ZoneSequenceLayer
                    {
                        LayerName = $"Zone {i}",
                        Mode = zt.SelectionMode,
                        ChoiceCount = zt.ChoiceCount > 0 ? zt.ChoiceCount : 2,
                        Entries = new List<ZoneSequenceEntry> { new ZoneSequenceEntry { Zone = zoneDefs[i], Weight = 1f } }
                    };
                    sequence.Layers.Add(layer);
                }
                AssetDatabase.CreateAsset(sequence, seqPath);
            }

            // Create RunConfig
            string configPath = $"{folder}/RunConfig_{template.TemplateName.Replace(" ", "")}.asset";
            var config = AssetDatabase.LoadAssetAtPath<RunConfigSO>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RunConfigSO>();
                config.ConfigName = template.TemplateName;
                config.ZoneCount = template.ZoneCount;
                config.ZoneSequence = sequence;
                config.DifficultyPerZone = new AnimationCurve(template.DifficultyCurve);
                config.StartingRunCurrency = template.StartingCurrency;
                config.RunCurrencyPerZoneClear = template.CurrencyPerZoneClear;
                AssetDatabase.CreateAsset(config, configPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(config);
            Selection.activeObject = config;
            Debug.Log($"[TemplateLibrary] Created run config from '{template.TemplateName}' at {folder}");

            // Invalidate context
            _context?.Invalidate();
        }

        private void CloneConfig(RunConfigSO source)
        {
            string folder = $"{OutputFolder}/Clone_{source.ConfigName}";
            EnsureFolderExists(folder);

            string path = $"{folder}/RunConfig_{source.ConfigName}_Clone.asset";
            var clone = Object.Instantiate(source);
            clone.ConfigName = $"{source.ConfigName} (Clone)";

            // Deep-clone zone sequence if present
            if (source.ZoneSequence != null)
            {
                var seqClone = Object.Instantiate(source.ZoneSequence);
                string seqPath = $"{folder}/Sequence_{source.ConfigName}_Clone.asset";
                AssetDatabase.CreateAsset(seqClone, seqPath);
                clone.ZoneSequence = seqClone;
            }

            AssetDatabase.CreateAsset(clone, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(clone);
            Selection.activeObject = clone;
            _context?.Invalidate();
        }

        private void SaveAsTemplate(RunConfigSO source)
        {
            EnsureFolderExists(TemplatesFolder);

            string path = $"{TemplatesFolder}/Template_{source.ConfigName}.asset";
            var template = ScriptableObject.CreateInstance<RunConfigTemplate>();
            template.TemplateName = source.ConfigName;
            template.Description = $"Custom template from {source.ConfigName}";
            template.ZoneCount = source.ZoneCount;
            template.DifficultyCurve = source.DifficultyPerZone.keys;
            template.StartingCurrency = source.StartingRunCurrency;
            template.CurrencyPerZoneClear = source.RunCurrencyPerZoneClear;

            // Extract zone structure from sequence
            if (source.ZoneSequence?.Layers != null)
            {
                template.EnableLooping = source.ZoneSequence.EnableLooping;
                template.LoopStartIndex = source.ZoneSequence.LoopStartIndex;
                template.LoopDifficultyMultiplier = source.ZoneSequence.LoopDifficultyMultiplier;

                var zones = new List<ZoneTemplateEntry>();
                foreach (var layer in source.ZoneSequence.Layers)
                {
                    if (layer.Entries != null && layer.Entries.Count > 0 && layer.Entries[0].Zone != null)
                    {
                        var z = layer.Entries[0].Zone;
                        zones.Add(new ZoneTemplateEntry
                        {
                            Type = z.Type,
                            ClearMode = z.ClearMode,
                            DifficultyMultiplier = z.DifficultyMultiplier,
                            SelectionMode = layer.Mode,
                            ChoiceCount = layer.ChoiceCount
                        });
                    }
                }
                template.Zones = zones.ToArray();
            }

            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();
            RefreshCustomTemplates();
            EditorGUIUtility.PingObject(template);
            Debug.Log($"[TemplateLibrary] Saved custom template at {path}");
        }

        // ==================== Helpers ====================

        private RunConfigTemplate GetSelectedTemplate()
        {
            if (_selectedTemplate < 0) return null;
            var arr = _isBuiltIn ? _builtInTemplates : _customTemplates;
            if (arr == null || _selectedTemplate >= arr.Length) return null;
            return arr[_selectedTemplate];
        }

        private void RefreshCustomTemplates()
        {
            if (!AssetDatabase.IsValidFolder(TemplatesFolder))
            {
                _customTemplates = System.Array.Empty<RunConfigTemplate>();
                return;
            }

            var guids = AssetDatabase.FindAssets("t:RunConfigTemplate", new[] { TemplatesFolder });
            _customTemplates = new RunConfigTemplate[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                _customTemplates[i] = AssetDatabase.LoadAssetAtPath<RunConfigTemplate>(p);
            }
        }

        private static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);
            string folderName = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string GetZoneTypeAbbrev(ZoneType type)
        {
            return type switch
            {
                ZoneType.Combat => "C",
                ZoneType.Elite => "E",
                ZoneType.Boss => "B",
                ZoneType.Shop => "$",
                ZoneType.Event => "Ev",
                ZoneType.Rest => "R",
                ZoneType.Treasure => "T",
                ZoneType.Exploration => "Ex",
                ZoneType.Arena => "A",
                ZoneType.Secret => "S",
                _ => "?"
            };
        }
    }
}
#endif
