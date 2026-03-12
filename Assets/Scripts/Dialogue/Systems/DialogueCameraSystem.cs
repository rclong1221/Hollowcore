using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Applies camera mode offsets during active dialogue sessions.
    /// CloseUp: positions camera toward NPC face.
    /// OverShoulder: behind player looking at NPC.
    /// Client|Local only (cosmetic camera effect).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DialogueCameraSystem : SystemBase
    {
        private bool _wasInDialogue;
        private DialogueCameraMode _lastMode;

        protected override void OnUpdate()
        {
            bool inDialogue = false;

            foreach (var (session, ltw) in
                SystemAPI.Query<RefRO<DialogueSessionState>, RefRO<LocalToWorld>>())
            {
                if (!session.ValueRO.IsActive) continue;

                // Only apply camera for the local player's dialogue
                if (session.ValueRO.InteractingPlayer == Entity.Null) continue;
                if (!EntityManager.HasComponent<GhostOwnerIsLocal>(session.ValueRO.InteractingPlayer))
                    continue;

                // Get camera mode from current node
                if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry))
                    continue;

                var tree = registry.GetTree(session.ValueRO.CurrentTreeId);
                if (tree == null) continue;

                int nodeIndex = tree.FindNodeIndex(session.ValueRO.CurrentNodeId);
                if (nodeIndex < 0) continue;

                var cameraMode = tree.Nodes[nodeIndex].CameraMode;
                if (cameraMode == DialogueCameraMode.None) continue;

                inDialogue = true;
                _lastMode = cameraMode;

                float3 npcPos = ltw.ValueRO.Position;

                // Push camera request to DialogueEventQueue for the camera controller to consume
                DialogueEventQueue.Enqueue(new DialogueUIEvent
                {
                    Type = DialogueUIEventType.CameraUpdate,
                    CameraMode = cameraMode,
                    NpcPosition = npcPos
                });
                break;
            }

            if (_wasInDialogue && !inDialogue)
            {
                // Dialogue ended — request camera restore
                DialogueEventQueue.Enqueue(new DialogueUIEvent
                {
                    Type = DialogueUIEventType.CameraRestore
                });
            }

            _wasInDialogue = inDialogue;
        }
    }
}
