#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Graph node view for Speech (dialogue line) nodes.
    /// Displays speaker name, text preview, expression, and VO clip.
    /// </summary>
    public class SpeechNodeView : DialogueNodeViewBase
    {
        private TextField _speakerField;
        private TextField _textField;
        private TextField _expressionField;
        private FloatField _durationField;
        private FloatField _typewriterSpeedField;
        private string _audioClipPath;
        private AudioClip _voiceClip;
        private DialogueCameraMode _cameraMode;
        private readonly DialogueNodeType _serializedNodeType;

        public SpeechNodeView(int nodeId, DialogueNodeType nodeType = DialogueNodeType.Speech)
            : base(nodeType, nodeId)
        {
            _serializedNodeType = nodeType;
            OutputPort = CreateOutputPort("Next");

            // Speaker name
            _speakerField = new TextField("Speaker") { value = "" };
            _speakerField.style.maxWidth = 250;
            extensionContainer.Add(_speakerField);

            // Text preview
            _textField = new TextField("Text") { value = "", multiline = true };
            _textField.style.maxWidth = 250;
            _textField.style.minHeight = 40;
            extensionContainer.Add(_textField);

            // Expression (EPIC 18.5)
            _expressionField = new TextField("Expression") { value = "" };
            _expressionField.style.maxWidth = 250;
            extensionContainer.Add(_expressionField);

            // Duration
            _durationField = new FloatField("Duration (s)") { value = 0 };
            extensionContainer.Add(_durationField);

            // Typewriter speed override
            _typewriterSpeedField = new FloatField("Typewriter (c/s)") { value = 0 };
            extensionContainer.Add(_typewriterSpeedField);

            RefreshExpandedState();
            RefreshPorts();
        }

        public override void LoadFromDialogueNode(ref DialogueNode node)
        {
            _speakerField.value = node.SpeakerName ?? "";
            _textField.value = node.Text ?? "";
            _expressionField.value = node.Expression ?? "";
            _durationField.value = node.Duration;
            _typewriterSpeedField.value = node.TypewriterSpeed;
            _audioClipPath = node.AudioClipPath;
            _voiceClip = node.VoiceClip;
            _cameraMode = node.CameraMode;
        }

        public override DialogueNode SaveToDialogueNode(Dictionary<Port, int> edgeMap)
        {
            return new DialogueNode
            {
                NodeId = NodeId,
                NodeType = _serializedNodeType,
                SpeakerName = _speakerField.value,
                Text = _textField.value,
                Expression = _expressionField.value,
                Duration = _durationField.value,
                TypewriterSpeed = _typewriterSpeedField.value,
                AudioClipPath = _audioClipPath,
                VoiceClip = _voiceClip,
                CameraMode = _cameraMode,
                NextNodeId = GetEdgeTarget(edgeMap, OutputPort)
            };
        }
    }
}
#endif
