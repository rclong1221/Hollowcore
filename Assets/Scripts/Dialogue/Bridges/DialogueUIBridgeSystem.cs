using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Reads DialogueSessionState from ECS and drives the dialogue UI
    /// via DialogueUIRegistry. Follows CombatUIBridgeSystem pattern.
    /// Runs in PresentationSystemGroup, Client|Local only.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DialogueUIBridgeSystem : SystemBase
    {
        private bool _wasShowingDialogue;
        private int _lastNodeId;
        private int _diagnosticFrames;
        private readonly List<DialogueChoiceUI> _choiceBuffer = new(8);
        private DialogueChoiceUI[] _choiceArrayCache = System.Array.Empty<DialogueChoiceUI>();

        protected override void OnCreate()
        {
            RequireForUpdate<DialogueRegistryManaged>();
            _diagnosticFrames = 120;
        }

        protected override void OnUpdate()
        {
            // Diagnostic warning for missing providers
            if (_diagnosticFrames > 0)
            {
                _diagnosticFrames--;
                if (_diagnosticFrames == 0 && !DialogueUIRegistry.HasDialogue)
                    Debug.LogWarning("[DialogueUIBridge] No IDialogueUIProvider registered after 120 frames.");
            }

            // Process event queue (camera updates, feedback)
            while (DialogueEventQueue.TryDequeue(out var evt))
            {
                // Events consumed by camera controller or UI adapters
            }

            // Find active dialogue session for local player
            if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry)) return;

            bool showingDialogue = false;

            foreach (var (session, entity) in
                SystemAPI.Query<RefRO<DialogueSessionState>>().WithEntityAccess())
            {
                if (!session.ValueRO.IsActive) continue;
                if (session.ValueRO.InteractingPlayer == Entity.Null) continue;

                // Check if this is the local player's dialogue
                if (!EntityManager.HasComponent<GhostOwnerIsLocal>(session.ValueRO.InteractingPlayer))
                    continue;

                showingDialogue = true;

                // Get tree and node
                var tree = registry.GetTree(session.ValueRO.CurrentTreeId);
                if (tree == null) continue;

                int nodeIndex = tree.FindNodeIndex(session.ValueRO.CurrentNodeId);
                if (nodeIndex < 0) continue;

                ref var node = ref tree.Nodes[nodeIndex];

                // Build UI state
                var uiState = new DialogueUIState
                {
                    SpeakerName = DialogueLocalization.Resolve(node.SpeakerName),
                    BodyText = DialogueLocalization.Resolve(node.Text),
                    AudioClipPath = node.AudioClipPath,
                    NodeType = node.NodeType,
                    AutoAdvanceSec = node.Duration,
                    CameraMode = node.CameraMode,
                    // EPIC 18.5
                    Expression = node.Expression,
                    VoiceClip = node.VoiceClip,
                    TypewriterSpeed = node.TypewriterSpeed,
                    Priority = tree.Priority
                };

                // Build filtered choices (reuse cached list to avoid per-frame allocation)
                if (node.NodeType == DialogueNodeType.PlayerChoice && node.Choices != null)
                {
                    _choiceBuffer.Clear();
                    for (int c = 0; c < node.Choices.Length && c < 8; c++)
                    {
                        if ((session.ValueRO.ValidChoicesMask & (1 << c)) != 0)
                        {
                            _choiceBuffer.Add(new DialogueChoiceUI
                            {
                                ChoiceIndex = c,
                                Text = DialogueLocalization.Resolve(node.Choices[c].Text)
                            });
                        }
                    }
                    // Reuse array if same length to avoid allocation
                    if (_choiceArrayCache.Length != _choiceBuffer.Count)
                        _choiceArrayCache = new DialogueChoiceUI[_choiceBuffer.Count];
                    for (int c = 0; c < _choiceBuffer.Count; c++)
                        _choiceArrayCache[c] = _choiceBuffer[c];
                    uiState.Choices = _choiceArrayCache;
                }

                // Push to UI provider
                if (DialogueUIRegistry.HasDialogue)
                {
                    if (!_wasShowingDialogue)
                        DialogueUIRegistry.Dialogue.OpenDialogue(uiState);
                    else if (session.ValueRO.CurrentNodeId != _lastNodeId)
                        DialogueUIRegistry.Dialogue.AdvanceDialogue(uiState);
                }

                _lastNodeId = session.ValueRO.CurrentNodeId;
                break; // Only one dialogue at a time
            }

            if (_wasShowingDialogue && !showingDialogue)
            {
                if (DialogueUIRegistry.HasDialogue)
                    DialogueUIRegistry.Dialogue.CloseDialogue();
            }

            _wasShowingDialogue = showingDialogue;
        }
    }
}
