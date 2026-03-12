using UnityEditor;
using UnityEngine;
using DIG.Tutorial.Config;

namespace DIG.Tutorial.Editor.Modules
{
    /// <summary>
    /// EPIC 18.4: Sequence Browser module — browse TutorialSequenceSO assets and inspect their steps.
    /// </summary>
    public class SequenceBrowserModule : ITutorialModule
    {
        private TutorialSequenceSO[] _sequences;
        private string[] _sequenceNames;
        private int _selectedIndex = -1;
        private Vector2 _scrollPos;
        private bool _showSteps = true;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Tutorial Sequences", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("Refresh"))
                LoadSequences();

            if (_sequences == null || _sequences.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No TutorialSequenceSO assets found.\nCreate via Assets > Create > DIG > Tutorial > Sequence.",
                    MessageType.Info);
                if (_sequences == null) LoadSequences();
                return;
            }

            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _sequences.Length; i++)
            {
                if (_sequences[i] == null) continue;

                bool isSelected = i == _selectedIndex;
                var bgColor = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                string label = $"{_sequences[i].DisplayName ?? _sequences[i].name} [{_sequences[i].SequenceId}]";
                if (GUILayout.Button(label, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    _selectedIndex = i;
                    Selection.activeObject = _sequences[i];
                }

                // Completion status
                bool completed = PlayerPrefs.GetInt(_sequences[i].GetSaveKey(), 0) == 1;
                GUILayout.Label(completed ? "[Done]" : "", GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                if (isSelected)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.LabelField("CanSkip", _sequences[i].CanSkip.ToString());
                    EditorGUILayout.LabelField("AutoStart", _sequences[i].AutoStart.ToString());
                    EditorGUILayout.LabelField("Priority", _sequences[i].Priority.ToString());
                    EditorGUILayout.LabelField("SaveKey", _sequences[i].GetSaveKey());

                    if (_sequences[i].Steps != null && _sequences[i].Steps.Length > 0)
                    {
                        EditorGUILayout.Space(4);
                        _showSteps = EditorGUILayout.Foldout(_showSteps, $"Steps ({_sequences[i].Steps.Length})");
                        if (_showSteps)
                        {
                            EditorGUI.indentLevel++;
                            for (int s = 0; s < _sequences[i].Steps.Length; s++)
                            {
                                var step = _sequences[i].Steps[s];
                                if (step == null)
                                {
                                    EditorGUILayout.LabelField($"  {s + 1}. (null)");
                                    continue;
                                }

                                EditorGUILayout.BeginHorizontal();
                                string stepLabel = $"{s + 1}. [{step.StepType}] {step.StepId}";
                                if (GUILayout.Button(stepLabel, EditorStyles.label))
                                    Selection.activeObject = step;
                                EditorGUILayout.EndHorizontal();

                                if (!string.IsNullOrEmpty(step.Title))
                                    EditorGUILayout.LabelField("    Title", step.Title);
                                EditorGUILayout.LabelField("    Completion", step.CompletionCondition.ToString());
                            }
                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUILayout.Space(4);
                    if (GUILayout.Button("Open in Graph Editor", GUILayout.Height(24)))
                    {
                        Graph.TutorialGraphEditorWindow.OpenSequence(_sequences[i]);
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                GUI.backgroundColor = bgColor;
            }

            EditorGUILayout.EndScrollView();

            // Play mode controls
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

            if (Application.isPlaying && TutorialService.HasInstance)
            {
                EditorGUILayout.LabelField("State", TutorialService.Instance.CurrentState.ToString());
                if (TutorialService.Instance.ActiveSequence != null)
                    EditorGUILayout.LabelField("Active", TutorialService.Instance.ActiveSequence.SequenceId);

                if (GUILayout.Button("Reset All Progress"))
                {
                    TutorialService.Instance.ResetAll();
                    Debug.Log("[TutorialWorkstation] All tutorial progress reset.");
                }
            }
            else
            {
                if (GUILayout.Button("Clear All PlayerPrefs Progress"))
                {
                    foreach (var seq in _sequences)
                    {
                        if (seq != null)
                            PlayerPrefs.DeleteKey(seq.GetSaveKey());
                    }
                    PlayerPrefs.Save();
                    Debug.Log("[TutorialWorkstation] All tutorial PlayerPrefs cleared.");
                }
            }
        }

        private void LoadSequences()
        {
            var guids = AssetDatabase.FindAssets("t:TutorialSequenceSO");
            _sequences = new TutorialSequenceSO[guids.Length];
            _sequenceNames = new string[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _sequences[i] = AssetDatabase.LoadAssetAtPath<TutorialSequenceSO>(path);
                _sequenceNames[i] = _sequences[i] != null ? _sequences[i].name : "(null)";
            }

            _selectedIndex = -1;
        }
    }
}
