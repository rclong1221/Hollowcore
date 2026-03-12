using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Audio.Systems;
using DIG.Surface.Config;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 9: Spawns persistent ground decals and lingering VFX for ability AOEs.
    /// Drains GroundEffectQueue (populated by ability systems when casting ground-targeted abilities)
    /// and spawns DecalManager/VFXManager assets based on GroundEffectLibrary.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SurfaceImpactPresenterSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class AbilityGroundEffectSystem : SystemBase
    {
        private GroundEffectLibrary _library;
        private DecalManager _decalManager;
        private VFXManager _vfxManager;

        private const int MaxEffectsPerFrame = 8;

        protected override void OnCreate()
        {
            _library = Resources.Load<GroundEffectLibrary>("GroundEffectLibrary");
        }

        protected override void OnUpdate()
        {
            if (_decalManager == null)
                _decalManager = DecalManager.Instance;
            if (_vfxManager == null)
                _vfxManager = VFXManager.Instance;

            if (_library == null) return;

            int processed = 0;

            while (GroundEffectQueue.TryDequeue(out var request))
            {
                if (processed >= MaxEffectsPerFrame) break;

                ProcessGroundEffect(request);
                processed++;
            }
        }

        private void ProcessGroundEffect(GroundEffectRequest request)
        {
            if (!_library.TryGetEntry(request.EffectType, out var entry)) return;

            // Compute decal radius (clamped to library limits)
            float radius = math.clamp(request.Radius, entry.MinRadius, entry.MaxRadius);

            // Use ability duration if provided, else library default
            float duration = request.Duration > 0f ? request.Duration : entry.DefaultDuration;

            // Spawn ground decal (projected downward)
            if (entry.Decal != null && _decalManager != null)
            {
                // Ground decals project downward
                var rotation = quaternion.LookRotation(new float3(0, 0, 1), new float3(0, 1, 0));

                _decalManager.SpawnDecal(
                    entry.Decal,
                    request.Position,
                    rotation,
                    duration + entry.FadeOutDuration
                );
            }

            // Spawn lingering VFX (fire embers, frost crystals, etc.)
            if (entry.LingeringVFXPrefab != null && _vfxManager != null)
            {
                var pos = new Vector3(request.Position.x, request.Position.y, request.Position.z);
                var go = _vfxManager.SpawnVFX(entry.LingeringVFXPrefab, pos, Quaternion.identity);

                if (go != null)
                {
                    // Scale VFX to match radius
                    float scaleFactor = radius / math.max(entry.MinRadius, 0.5f);
                    go.transform.localScale = Vector3.one * scaleFactor;

                    // Auto-destroy after duration
                    Object.Destroy(go, duration + entry.FadeOutDuration);
                }
            }
        }
    }
}
