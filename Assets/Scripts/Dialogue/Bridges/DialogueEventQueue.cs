using System.Collections.Generic;
using Unity.Mathematics;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Event types for cross-system dialogue UI notifications.
    /// </summary>
    public enum DialogueUIEventType : byte
    {
        SessionOpened,
        SessionClosed,
        NodeAdvanced,
        ActionFeedback,
        CameraUpdate,
        CameraRestore
    }

    /// <summary>
    /// EPIC 16.16: UI event data for dialogue state transitions.
    /// </summary>
    public struct DialogueUIEvent
    {
        public DialogueUIEventType Type;
        public int TreeId;
        public int NodeId;
        public DialogueCameraMode CameraMode;
        public float3 NpcPosition;
        public string Message;
    }

    /// <summary>
    /// EPIC 16.16: Static event queue for dialogue UI notifications.
    /// Systems enqueue events, DialogueUIBridgeSystem dequeues in PresentationSystemGroup.
    /// </summary>
    public static class DialogueEventQueue
    {
        private static readonly Queue<DialogueUIEvent> _queue = new(8);

        public static void Enqueue(DialogueUIEvent evt) => _queue.Enqueue(evt);
        public static bool TryDequeue(out DialogueUIEvent evt) => _queue.TryDequeue(out evt);
        public static void Clear() => _queue.Clear();
        public static int Count => _queue.Count;
    }
}
