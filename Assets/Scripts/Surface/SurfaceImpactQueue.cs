using System.Collections.Generic;
using UnityEngine;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 15.24: Static queue bridging impact event producers to the client-side presenter.
    /// Follows the same pattern as DamageVisualQueue — server/client systems enqueue,
    /// SurfaceImpactPresenterSystem dequeues in PresentationSystemGroup.
    /// </summary>
    public static class SurfaceImpactQueue
    {
        private static readonly Queue<SurfaceImpactData> _queue = new();

        public static void Enqueue(SurfaceImpactData data) => _queue.Enqueue(data);

        public static bool TryDequeue(out SurfaceImpactData data)
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            _queue.Clear();
        }
    }
}
