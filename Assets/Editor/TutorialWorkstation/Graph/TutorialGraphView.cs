#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using DIG.Tutorial.Config;

namespace DIG.Tutorial.Editor.Graph
{
    /// <summary>
    /// EPIC 18.4: Custom GraphView for tutorial step node editing.
    /// Color-coded nodes by step type, with input/output ports for flow connections.
    /// </summary>
    public class TutorialGraphView : GraphView
    {
        private readonly TutorialGraphEditorWindow _window;

        // Step type colors
        private static readonly Dictionary<TutorialStepType, Color> StepColors = new()
        {
            { TutorialStepType.Tooltip,      new Color(0.3f, 0.6f, 0.9f) },
            { TutorialStepType.Highlight,    new Color(0.9f, 0.7f, 0.2f) },
            { TutorialStepType.ForcedAction, new Color(0.9f, 0.4f, 0.3f) },
            { TutorialStepType.Popup,        new Color(0.4f, 0.8f, 0.4f) },
            { TutorialStepType.WorldMarker,  new Color(0.6f, 0.4f, 0.9f) },
            { TutorialStepType.Delay,        new Color(0.5f, 0.5f, 0.5f) },
            { TutorialStepType.Branch,       new Color(0.9f, 0.5f, 0.7f) },
        };

        public TutorialGraphView(TutorialGraphEditorWindow window)
        {
            _window = window;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // Grid background
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // Minimap
            var minimap = new MiniMap { anchored = true };
            minimap.SetPosition(new Rect(10, 30, 200, 140));
            Add(minimap);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                if (port != startPort && port.node != startPort.node && port.direction != startPort.direction)
                    compatible.Add(port);
            });
            return compatible;
        }

        public Node CreateStepNode(TutorialStepSO step, int index)
        {
            var node = new Node
            {
                title = $"{index + 1}. [{step.StepType}] {step.StepId ?? "unnamed"}"
            };

            // Color the title bar
            if (StepColors.TryGetValue(step.StepType, out var color))
            {
                node.titleContainer.style.backgroundColor = new StyleColor(color);
                node.titleContainer.style.borderBottomColor = new StyleColor(color * 0.7f);
            }

            // Input port (flow in)
            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            inputPort.portName = "In";
            node.inputContainer.Add(inputPort);

            // Output port (flow out)
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            outputPort.portName = step.StepType == TutorialStepType.Branch ? "Out" : "Next";
            node.outputContainer.Add(outputPort);

            // Branch: add second output port
            if (step.StepType == TutorialStepType.Branch)
            {
                var falsePort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
                falsePort.portName = "False";
                node.outputContainer.Add(falsePort);
                outputPort.portName = "True";
            }

            // Info labels
            var body = new VisualElement();
            body.style.paddingLeft = 8;
            body.style.paddingRight = 8;
            body.style.paddingBottom = 4;

            if (!string.IsNullOrEmpty(step.Title))
            {
                var titleLabel = new Label(step.Title);
                titleLabel.style.fontSize = 11;
                titleLabel.style.color = new StyleColor(Color.white);
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                body.Add(titleLabel);
            }

            var typeLabel = new Label($"Completion: {step.CompletionCondition}");
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            body.Add(typeLabel);

            if (!string.IsNullOrEmpty(step.CompletionParam))
            {
                var paramLabel = new Label($"Param: {step.CompletionParam}");
                paramLabel.style.fontSize = 10;
                paramLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                body.Add(paramLabel);
            }

            node.extensionContainer.Add(body);
            node.RefreshExpandedState();
            node.RefreshPorts();

            // Double-click to select SO in inspector
            node.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    UnityEditor.Selection.activeObject = step;
                    evt.StopPropagation();
                }
            });

            // Store step reference
            node.userData = step;

            return node;
        }

        public void ClearGraph()
        {
            graphElements.ForEach(RemoveElement);
        }
    }
}
#endif
