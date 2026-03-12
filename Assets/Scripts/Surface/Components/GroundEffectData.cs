using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 15.24 Phase 9: Ground effect types for ability AOE persistent decals/VFX.
    /// Maps to DIG.Targeting.Theming.DamageType for auto-assignment.
    /// </summary>
    public enum GroundEffectType : byte
    {
        None = 0,
        FireScorch = 1,
        IcePatch = 2,
        PoisonPuddle = 3,
        LightningScorch = 4,
        HolyGlow = 5,
        ShadowPool = 6,
        ArcaneBurn = 7
    }

    /// <summary>
    /// Request to spawn a persistent ground effect at a world position.
    /// Enqueued by ability systems, consumed by AbilityGroundEffectSystem.
    /// </summary>
    public struct GroundEffectRequest
    {
        public GroundEffectType EffectType;
        public float3 Position;
        public float Radius;
        public float Duration;
        public float Intensity;
    }

    /// <summary>
    /// Static queue for ground effect requests. Bridges ability systems to the presenter.
    /// Same pattern as SurfaceImpactQueue and DamageVisualQueue.
    /// </summary>
    public static class GroundEffectQueue
    {
        private static readonly Queue<GroundEffectRequest> _queue = new Queue<GroundEffectRequest>();

        public static void Enqueue(GroundEffectRequest request)
        {
            _queue.Enqueue(request);
        }

        public static bool TryDequeue(out GroundEffectRequest request)
        {
            if (_queue.Count > 0)
            {
                request = _queue.Dequeue();
                return true;
            }
            request = default;
            return false;
        }

        public static int Count => _queue.Count;

        public static void Clear() => _queue.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _queue.Clear();
        }
    }
}
