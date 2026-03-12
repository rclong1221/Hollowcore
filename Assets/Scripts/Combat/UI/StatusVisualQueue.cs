using Unity.Mathematics;
using System.Collections.Generic;

namespace DIG.Combat.UI
{
    /// <summary>
    /// EPIC 15.30: Lightweight data struct for status effect application visual events.
    /// </summary>
    public struct StatusAppliedVisual
    {
        public global::Player.Components.StatusEffectType Type;
        public float3 Position;
    }

    /// <summary>
    /// EPIC 15.30: Static queue bridging server-side status detection to client-side UI.
    /// StatusEffectVisualBridgeSystem enqueues when new effects appear.
    /// CombatUIBridgeSystem dequeues in PresentationSystemGroup.
    /// </summary>
    public static class StatusVisualQueue
    {
        private static readonly Queue<StatusAppliedVisual> _queue = new();

        public static void Enqueue(StatusAppliedVisual data) => _queue.Enqueue(data);

        public static bool TryDequeue(out StatusAppliedVisual data)
        {
            if (_queue.Count > 0)
            {
                data = _queue.Dequeue();
                return true;
            }
            data = default;
            return false;
        }

        public static int Count => _queue.Count;
        public static void Clear() => _queue.Clear();
    }
}
