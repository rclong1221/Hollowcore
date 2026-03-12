using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 18.5: Dialogue priority and interrupt system.
    /// Higher priority dialogue interrupts lower. Same or lower priority is silently dropped.
    /// Interrupted dialogue can Resume, Restart, or be Discarded per tree config.
    /// Runs server/local — modifies DialogueSessionState.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DialogueInitiationSystem))]
    public partial class DialoguePrioritySystem : SystemBase
    {
        /// <summary>
        /// Queued dialogue request from external systems or triggers.
        /// </summary>
        public struct DialogueRequest
        {
            public Entity NpcEntity;
            public Entity PlayerEntity;
            public int TreeId;
            public DialoguePriority Priority;
        }

        private static readonly Queue<DialogueRequest> _requestQueue = new(4);

        /// <summary>
        /// External API: Queue a dialogue request with priority.
        /// The system will evaluate it next frame against any active dialogue.
        /// </summary>
        public static void QueueDialogue(Entity npcEntity, Entity playerEntity, int treeId, DialoguePriority priority)
        {
            _requestQueue.Enqueue(new DialogueRequest
            {
                NpcEntity = npcEntity,
                PlayerEntity = playerEntity,
                TreeId = treeId,
                Priority = priority
            });
        }

        // Interrupted dialogue state for potential resume
        private struct InterruptedState
        {
            public Entity NpcEntity;
            public int TreeId;
            public int NodeId;
            public Entity PlayerEntity;
            public InterruptBehavior Behavior;
        }

        private readonly List<InterruptedState> _interruptedStack = new(4);
        private ComponentLookup<DialogueSessionState> _sessionLookup;

        protected override void OnCreate()
        {
            _sessionLookup = GetComponentLookup<DialogueSessionState>(false);
            RequireForUpdate<DialogueConfig>();
        }

        protected override void OnUpdate()
        {
            if (_requestQueue.Count == 0)
            {
                // Check for resume after active dialogue ends
                TryResumeInterrupted();
                return;
            }

            _sessionLookup.Update(this);

            if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry))
            {
                _requestQueue.Clear();
                return;
            }

            while (_requestQueue.Count > 0)
            {
                var request = _requestQueue.Dequeue();

                if (!_sessionLookup.HasComponent(request.NpcEntity)) continue;

                var session = _sessionLookup[request.NpcEntity];

                // If NPC is already in active dialogue, evaluate priority
                if (session.IsActive)
                {
                    var activeTree = registry.GetTree(session.CurrentTreeId);
                    var activePriority = activeTree != null ? activeTree.Priority : DialoguePriority.Exploration;

                    if (request.Priority > activePriority)
                    {
                        // Higher priority — interrupt current dialogue
                        var interruptBehavior = activeTree != null
                            ? activeTree.InterruptBehavior
                            : InterruptBehavior.Discard;

                        if (interruptBehavior != InterruptBehavior.Discard)
                        {
                            // Save interrupted state for later resume/restart
                            _interruptedStack.Add(new InterruptedState
                            {
                                NpcEntity = request.NpcEntity,
                                TreeId = session.CurrentTreeId,
                                NodeId = interruptBehavior == InterruptBehavior.Resume
                                    ? session.CurrentNodeId
                                    : -1, // Restart starts from beginning
                                PlayerEntity = session.InteractingPlayer,
                                Behavior = interruptBehavior
                            });
                        }

                        // Start new dialogue
                        StartDialogue(ref session, request, registry);
                        _sessionLookup[request.NpcEntity] = session;
                    }
                    // Same or lower priority — silently drop
                }
                else
                {
                    // No active dialogue — start immediately
                    StartDialogue(ref session, request, registry);
                    _sessionLookup[request.NpcEntity] = session;
                }
            }
        }

        private void StartDialogue(ref DialogueSessionState session, in DialogueRequest request,
            DialogueRegistryManaged registry)
        {
            var tree = registry.GetTree(request.TreeId);
            if (tree == null) return;

            session.IsActive = true;
            session.CurrentTreeId = request.TreeId;
            session.CurrentNodeId = tree.StartNodeId;
            session.InteractingPlayer = request.PlayerEntity;
            session.SessionStartTick = (uint)SystemAPI.GetSingleton<NetworkTime>()
                .ServerTick.TickIndexForValidTick;
            session.ValidChoicesMask = 0;
        }

        private void TryResumeInterrupted()
        {
            if (_interruptedStack.Count == 0) return;

            _sessionLookup.Update(this);

            if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry))
                return;

            // Check most recent interrupted dialogue first
            for (int i = _interruptedStack.Count - 1; i >= 0; i--)
            {
                var interrupted = _interruptedStack[i];

                if (!_sessionLookup.HasComponent(interrupted.NpcEntity))
                {
                    _interruptedStack.RemoveAt(i);
                    continue;
                }

                var session = _sessionLookup[interrupted.NpcEntity];
                if (session.IsActive) continue; // Still busy

                // Resume or restart
                var tree = registry.GetTree(interrupted.TreeId);
                if (tree == null)
                {
                    _interruptedStack.RemoveAt(i);
                    continue;
                }

                session.IsActive = true;
                session.CurrentTreeId = interrupted.TreeId;
                session.CurrentNodeId = interrupted.Behavior == InterruptBehavior.Resume && interrupted.NodeId >= 0
                    ? interrupted.NodeId
                    : tree.StartNodeId;
                session.InteractingPlayer = interrupted.PlayerEntity;
                session.SessionStartTick = (uint)SystemAPI.GetSingleton<NetworkTime>()
                    .ServerTick.TickIndexForValidTick;
                session.ValidChoicesMask = 0;
                _sessionLookup[interrupted.NpcEntity] = session;

                _interruptedStack.RemoveAt(i);
                break; // One resume per frame
            }
        }
    }
}
