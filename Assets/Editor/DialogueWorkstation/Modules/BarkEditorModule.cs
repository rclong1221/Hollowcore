#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 16.16: Editor for BarkCollectionSO assets.
    /// Edit bark lines, weights, conditions, cooldowns.
    /// </summary>
    public class BarkEditorModule : IDialogueModule
    {
        private BarkCollectionSO _collection;
        private Vector2 _scrollPos;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Bark Collection Editor", EditorStyles.boldLabel);

            _collection = (BarkCollectionSO)EditorGUILayout.ObjectField(
                "Bark Collection", _collection, typeof(BarkCollectionSO), false);

            if (_collection == null)
            {
                EditorGUILayout.HelpBox("Select a BarkCollectionSO to edit.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            // Core properties
            bool changed = false;
            var newId = EditorGUILayout.IntField("Bark ID", _collection.BarkId);
            if (newId != _collection.BarkId) { _collection.BarkId = newId; changed = true; }

            var newCat = (BarkCategory)EditorGUILayout.EnumPopup("Category", _collection.Category);
            if (newCat != _collection.Category) { _collection.Category = newCat; changed = true; }

            var newCooldown = EditorGUILayout.FloatField("Cooldown (sec)", _collection.Cooldown);
            if (!Mathf.Approximately(newCooldown, _collection.Cooldown)) { _collection.Cooldown = newCooldown; changed = true; }

            var newRange = EditorGUILayout.FloatField("Max Range (m)", _collection.MaxRange);
            if (!Mathf.Approximately(newRange, _collection.MaxRange)) { _collection.MaxRange = newRange; changed = true; }

            var newLOS = EditorGUILayout.Toggle("Require Line of Sight", _collection.RequiresLineOfSight);
            if (newLOS != _collection.RequiresLineOfSight) { _collection.RequiresLineOfSight = newLOS; changed = true; }

            // Lines
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"Lines ({_collection.Lines.Length})", EditorStyles.miniBoldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(350));

            for (int i = 0; i < _collection.Lines.Length; i++)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Line {i}", EditorStyles.miniLabel);

                var line = _collection.Lines[i];
                line.Text = EditorGUILayout.TextField("Text (key)", line.Text);
                line.AudioClipPath = EditorGUILayout.TextField("Audio Path", line.AudioClipPath);
                line.Weight = EditorGUILayout.Slider("Weight", line.Weight, 0.01f, 10f);
                line.ConditionType = (DialogueConditionType)EditorGUILayout.EnumPopup("Condition", line.ConditionType);
                if (line.ConditionType != DialogueConditionType.None)
                    line.ConditionValue = EditorGUILayout.IntField("Condition Value", line.ConditionValue);

                if (!line.Equals(_collection.Lines[i])) { _collection.Lines[i] = line; changed = true; }

                if (GUILayout.Button("Remove Line", GUILayout.Width(90)))
                {
                    var list = new System.Collections.Generic.List<BarkLine>(_collection.Lines);
                    list.RemoveAt(i);
                    _collection.Lines = list.ToArray();
                    changed = true;
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add Line", GUILayout.Width(100)))
            {
                var list = new System.Collections.Generic.List<BarkLine>(_collection.Lines);
                list.Add(new BarkLine { Weight = 1f, Text = "New bark line" });
                _collection.Lines = list.ToArray();
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(_collection);
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
