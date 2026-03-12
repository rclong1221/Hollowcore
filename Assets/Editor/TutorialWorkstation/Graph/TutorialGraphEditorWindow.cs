#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using DIG.Tutorial.Config;

namespace DIG.Tutorial.Editor.Graph
{
    /// <summary>
    /// EPIC 18.4: Visual graph editor for authoring TutorialSequenceSO step flow.
    /// Opens via DIG > Tutorial > Graph Editor or from SequenceBrowserModule.
    /// Follows DialogueGraphEditorWindow pattern.
    /// </summary>
    public class TutorialGraphEditorWindow : EditorWindow
    {
        private TutorialSequenceSO _sequence;
        private TutorialGraphView _graphView;

        [MenuItem("DIG/Tutorial/Graph Editor")]
        public static void OpenEmpty()
        {
            var window = GetWindow<TutorialGraphEditorWindow>("Tutorial Graph");
            window.minSize = new Vector2(800, 600);
        }

        public static void OpenSequence(TutorialSequenceSO sequence)
        {
            var window = GetWindow<TutorialGraphEditorWindow>("Tutorial Graph");
            window.minSize = new Vector2(800, 600);
            window.LoadSequence(sequence);
        }

        private void OnEnable()
        {
            rootVisualElement.Clear();
            CreateGraphView();
            CreateToolbar();
        }

        private void OnDisable()
        {
            if (_graphView != null)
                rootVisualElement.Remove(_graphView);
        }

        private void CreateGraphView()
        {
            _graphView = new TutorialGraphView(this)
            {
                name = "Tutorial Graph"
            };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        }

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();

            // Sequence selector
            var seqField = new IMGUIContainer(() =>
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
                var newSeq = (TutorialSequenceSO)EditorGUILayout.ObjectField(
                    _sequence, typeof(TutorialSequenceSO), false, GUILayout.Width(250));
                if (newSeq != _sequence && newSeq != null)
                    LoadSequence(newSeq);
                EditorGUILayout.EndHorizontal();
            });
            toolbar.Add(seqField);

            // Save button
            var saveButton = new Button(SaveSequence) { text = "Save" };
            saveButton.style.width = 60;
            toolbar.Add(saveButton);

            // Auto-layout button
            var layoutButton = new Button(AutoLayout) { text = "Auto-Layout" };
            layoutButton.style.width = 90;
            toolbar.Add(layoutButton);

            // Clear button
            var clearButton = new Button(() => _graphView?.ClearGraph()) { text = "Clear" };
            clearButton.style.width = 60;
            toolbar.Add(clearButton);

            rootVisualElement.Add(toolbar);
            toolbar.BringToFront();
        }

        public void LoadSequence(TutorialSequenceSO sequence)
        {
            _sequence = sequence;
            _graphView.ClearGraph();

            if (_sequence == null) return;

            // Create nodes for each step
            float xOffset = 50;
            float yOffset = 50;
            float nodeSpacing = 220;

            if (_sequence.Steps != null)
            {
                var nodeMap = new Dictionary<string, Node>();

                for (int i = 0; i < _sequence.Steps.Length; i++)
                {
                    var step = _sequence.Steps[i];
                    if (step == null) continue;

                    var node = _graphView.CreateStepNode(step, i);
                    node.SetPosition(new Rect(xOffset + i * nodeSpacing, yOffset, 180, 120));
                    _graphView.AddElement(node);

                    if (!string.IsNullOrEmpty(step.StepId))
                        nodeMap[step.StepId] = node;
                }

                // Create edges for sequential connections and branches
                for (int i = 0; i < _sequence.Steps.Length; i++)
                {
                    var step = _sequence.Steps[i];
                    if (step == null) continue;

                    var currentNode = nodeMap.GetValueOrDefault(step.StepId);
                    if (currentNode == null) continue;

                    // Branch step: connect to true/false targets
                    if (step.StepType == TutorialStepType.Branch)
                    {
                        if (!string.IsNullOrEmpty(step.TrueStepId) && nodeMap.TryGetValue(step.TrueStepId, out var trueNode))
                            ConnectNodes(currentNode, trueNode, "True");
                        if (!string.IsNullOrEmpty(step.FalseStepId) && nodeMap.TryGetValue(step.FalseStepId, out var falseNode))
                            ConnectNodes(currentNode, falseNode, "False");
                    }
                    // NextStepId override
                    else if (!string.IsNullOrEmpty(step.NextStepId) && nodeMap.TryGetValue(step.NextStepId, out var nextNode))
                    {
                        ConnectNodes(currentNode, nextNode, "Next");
                    }
                    // Sequential: connect to i+1
                    else if (i + 1 < _sequence.Steps.Length)
                    {
                        var nextStep = _sequence.Steps[i + 1];
                        if (nextStep != null && !string.IsNullOrEmpty(nextStep.StepId) && nodeMap.TryGetValue(nextStep.StepId, out var seqNode))
                            ConnectNodes(currentNode, seqNode, "");
                    }
                }
            }

            titleContent = new GUIContent($"Tutorial: {_sequence.DisplayName ?? _sequence.name}");
        }

        private void ConnectNodes(Node from, Node to, string edgeLabel)
        {
            var outputPort = from.outputContainer.Q<Port>();
            var inputPort = to.inputContainer.Q<Port>();
            if (outputPort == null || inputPort == null) return;

            var edge = outputPort.ConnectTo(inputPort);
            _graphView.AddElement(edge);
        }

        private void SaveSequence()
        {
            if (_sequence == null)
            {
                EditorUtility.DisplayDialog("No Sequence", "No TutorialSequenceSO loaded.", "OK");
                return;
            }

            Undo.RecordObject(_sequence, "Save Tutorial Graph");
            // Node positions are stored for graph layout but steps reference the SOs
            EditorUtility.SetDirty(_sequence);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TutorialGraph] Saved {_sequence.DisplayName ?? _sequence.name}.");
        }

        private void AutoLayout()
        {
            if (_sequence == null || _graphView == null) return;

            // Simple left-to-right auto-layout
            float x = 50;
            float y = 50;
            float spacing = 220;

            foreach (var element in _graphView.graphElements.ToList())
            {
                if (element is Node node)
                {
                    node.SetPosition(new Rect(x, y, 180, 120));
                    x += spacing;
                }
            }
        }
    }

    /// <summary>
    /// Minimal Toolbar element for GraphView editor.
    /// </summary>
    public class Toolbar : VisualElement
    {
        public Toolbar()
        {
            style.flexDirection = FlexDirection.Row;
            style.height = 24;
            style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            style.paddingLeft = 4;
            style.paddingRight = 4;
        }
    }
}
#endif
