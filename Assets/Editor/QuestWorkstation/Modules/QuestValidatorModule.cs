using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Quest.Editor.Modules
{
    /// <summary>
    /// EPIC 16.12: Quest Validator — checks for broken references, unreachable quests,
    /// circular prerequisites, missing turn-in NPCs, duplicate IDs.
    /// </summary>
    public class QuestValidatorModule : IQuestModule
    {
        private QuestDatabaseSO _database;
        private readonly List<ValidationResult> _results = new();
        private Vector2 _scroll;

        private enum Severity { Error, Warning, Info }

        private struct ValidationResult
        {
            public Severity Severity;
            public string Message;
            public int QuestId;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Quest Validator", EditorStyles.boldLabel);

            _database = (QuestDatabaseSO)EditorGUILayout.ObjectField("Database", _database, typeof(QuestDatabaseSO), false);
            if (_database == null)
                _database = Resources.Load<QuestDatabaseSO>("QuestDatabase");

            if (_database == null)
            {
                EditorGUILayout.HelpBox("No QuestDatabaseSO found.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Run Validation", GUILayout.Height(30)))
                RunValidation();

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Run Validation' to check for issues.", MessageType.Info);
                return;
            }

            // Summary
            int errors = 0, warnings = 0, infos = 0;
            foreach (var r in _results)
            {
                switch (r.Severity)
                {
                    case Severity.Error: errors++; break;
                    case Severity.Warning: warnings++; break;
                    case Severity.Info: infos++; break;
                }
            }
            EditorGUILayout.LabelField($"Results: {errors} errors, {warnings} warnings, {infos} info");

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var result in _results)
            {
                var msgType = result.Severity switch
                {
                    Severity.Error => MessageType.Error,
                    Severity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(result.Message, msgType);
                if (result.QuestId > 0)
                {
                    if (GUILayout.Button("Select", GUILayout.Width(50), GUILayout.Height(38)))
                    {
                        var def = _database.GetQuest(result.QuestId);
                        if (def != null) Selection.activeObject = def;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(UnityEditor.SceneView sceneView) { }

        private void RunValidation()
        {
            _results.Clear();
            if (_database == null) return;
            _database.BuildLookupTable();

            var idSet = new HashSet<int>();

            foreach (var quest in _database.Quests)
            {
                if (quest == null)
                {
                    _results.Add(new ValidationResult { Severity = Severity.Error, Message = "Null quest entry in database." });
                    continue;
                }

                // Duplicate IDs
                if (!idSet.Add(quest.QuestId))
                {
                    _results.Add(new ValidationResult
                    {
                        Severity = Severity.Error,
                        Message = $"Duplicate QuestId {quest.QuestId}: '{quest.DisplayName}'",
                        QuestId = quest.QuestId
                    });
                }

                // Empty name
                if (string.IsNullOrWhiteSpace(quest.DisplayName))
                {
                    _results.Add(new ValidationResult
                    {
                        Severity = Severity.Warning,
                        Message = $"Quest #{quest.QuestId} has empty DisplayName.",
                        QuestId = quest.QuestId
                    });
                }

                // No objectives
                if (quest.Objectives == null || quest.Objectives.Length == 0)
                {
                    _results.Add(new ValidationResult
                    {
                        Severity = Severity.Warning,
                        Message = $"Quest '{quest.DisplayName}' (#{quest.QuestId}) has no objectives.",
                        QuestId = quest.QuestId
                    });
                }

                // Prerequisite references
                if (quest.PrerequisiteQuestIds != null)
                {
                    foreach (var prereqId in quest.PrerequisiteQuestIds)
                    {
                        if (!_database.HasQuest(prereqId))
                        {
                            _results.Add(new ValidationResult
                            {
                                Severity = Severity.Error,
                                Message = $"Quest '{quest.DisplayName}' references missing prerequisite #{prereqId}.",
                                QuestId = quest.QuestId
                            });
                        }
                    }

                    // Circular prerequisite detection
                    if (HasCircularDependency(quest.QuestId, new HashSet<int>()))
                    {
                        _results.Add(new ValidationResult
                        {
                            Severity = Severity.Error,
                            Message = $"Quest '{quest.DisplayName}' (#{quest.QuestId}) has circular prerequisites!",
                            QuestId = quest.QuestId
                        });
                    }
                }

                // Turn-in without auto-complete
                if (!quest.AutoComplete && quest.TurnInInteractableId == 0)
                {
                    _results.Add(new ValidationResult
                    {
                        Severity = Severity.Warning,
                        Message = $"Quest '{quest.DisplayName}' is not auto-complete but has no TurnInInteractableId.",
                        QuestId = quest.QuestId
                    });
                }

                // Objective validation
                if (quest.Objectives != null)
                {
                    var objIds = new HashSet<int>();
                    foreach (var obj in quest.Objectives)
                    {
                        if (!objIds.Add(obj.ObjectiveId))
                        {
                            _results.Add(new ValidationResult
                            {
                                Severity = Severity.Error,
                                Message = $"Quest '{quest.DisplayName}': duplicate ObjectiveId {obj.ObjectiveId}.",
                                QuestId = quest.QuestId
                            });
                        }

                        if (obj.RequiredCount <= 0)
                        {
                            _results.Add(new ValidationResult
                            {
                                Severity = Severity.Warning,
                                Message = $"Quest '{quest.DisplayName}': objective '{obj.Description}' has RequiredCount <= 0.",
                                QuestId = quest.QuestId
                            });
                        }

                        // UnlockAfter references valid objective
                        if (obj.UnlockAfterObjectiveId != 0)
                        {
                            bool found = false;
                            foreach (var other in quest.Objectives)
                                if (other.ObjectiveId == obj.UnlockAfterObjectiveId) { found = true; break; }

                            if (!found)
                            {
                                _results.Add(new ValidationResult
                                {
                                    Severity = Severity.Error,
                                    Message = $"Quest '{quest.DisplayName}': objective '{obj.Description}' references missing UnlockAfterObjectiveId {obj.UnlockAfterObjectiveId}.",
                                    QuestId = quest.QuestId
                                });
                            }
                        }
                    }
                }
            }

            if (_results.Count == 0)
            {
                _results.Add(new ValidationResult
                {
                    Severity = Severity.Info,
                    Message = $"All {_database.Quests.Count} quests passed validation."
                });
            }
        }

        private bool HasCircularDependency(int questId, HashSet<int> visited)
        {
            if (!visited.Add(questId)) return true;

            var def = _database.GetQuest(questId);
            if (def?.PrerequisiteQuestIds == null) return false;

            foreach (var prereqId in def.PrerequisiteQuestIds)
            {
                if (HasCircularDependency(prereqId, new HashSet<int>(visited)))
                    return true;
            }

            return false;
        }
    }
}
