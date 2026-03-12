using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.NetCode;
using UnityEngine;
using Audio.Systems;
using DIG.Surface.Audio;
using DIG.Surface.Config;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 8: Continuous surface audio system.
    /// Reads the local player's speed and ground surface to start/stop/crossfade
    /// looping audio (ice crackle, gravel crunch, water wading, snow compression).
    /// Uses GroundSurfaceState (computed by GroundSurfaceQuerySystem) instead of raycasting.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SurfaceImpactPresenterSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class SurfaceContactAudioSystem : SystemBase
    {
        private SurfaceAudioLoopConfig _config;
        private SurfaceID _currentSurface;
        private bool _loopActive;

        protected override void OnCreate()
        {
            _config = Resources.Load<SurfaceAudioLoopConfig>("SurfaceAudioLoopConfig");
        }

        protected override void OnUpdate()
        {
            if (!SurfaceAudioLoopManager.HasInstance) return;
            if (_config == null) return;

            var manager = SurfaceAudioLoopManager.Instance;

            // Phase 7 integration: apply spatial blend from paradigm config
            var paradigmConfig = ParadigmSurfaceConfig.Instance;
            if (paradigmConfig != null && paradigmConfig.ActiveProfile != null)
            {
                manager.SetSpatialBlend(paradigmConfig.ActiveProfile.Audio3DBlend);
            }

            bool foundPlayer = false;

            foreach (var (playerState, physVel, groundSurface) in
                     SystemAPI.Query<RefRO<PlayerState>, RefRO<PhysicsVelocity>, RefRO<GroundSurfaceState>>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                foundPlayer = true;

                // Only play loops when grounded
                if (!playerState.ValueRO.IsGrounded)
                {
                    if (_loopActive)
                    {
                        manager.StopLoop(_currentSurface, _config.FadeOutDuration);
                        _loopActive = false;
                    }
                    continue;
                }

                // Get horizontal speed
                var vel = physVel.ValueRO.Linear;
                float speed = math.length(new float3(vel.x, 0, vel.z));

                // Use GroundSurfaceState instead of raycasting
                SurfaceID surfaceId = groundSurface.ValueRO.SurfaceId;

                // Check if this surface has a loop config
                if (!_config.TryGetEntry(surfaceId, out var entry) || entry.LoopClip == null)
                {
                    // No loop for this surface — stop any active loop
                    if (_loopActive)
                    {
                        manager.StopLoop(_currentSurface, _config.FadeOutDuration);
                        _loopActive = false;
                    }
                    continue;
                }

                // Check speed threshold
                if (speed < entry.SpeedThreshold)
                {
                    if (_loopActive)
                    {
                        manager.StopLoop(_currentSurface, _config.FadeOutDuration);
                        _loopActive = false;
                    }
                    continue;
                }

                // Compute volume based on speed
                float speedNormalized = math.saturate(speed / entry.MaxSpeedForVolume);
                float volume = math.lerp(0f, entry.MaxVolume, speedNormalized);

                // Surface changed — crossfade
                if (_loopActive && _currentSurface != surfaceId)
                {
                    manager.StopLoop(_currentSurface, _config.CrossfadeDuration);
                }

                // Start or update loop
                if (!_loopActive || _currentSurface != surfaceId)
                {
                    manager.StartLoop(surfaceId, entry.LoopClip, volume);
                    _currentSurface = surfaceId;
                    _loopActive = true;
                }
                else
                {
                    manager.UpdateLoopVolume(surfaceId, volume);
                }
            }

            // No local player found — stop loops
            if (!foundPlayer && _loopActive)
            {
                manager.StopAllLoops();
                _loopActive = false;
            }
        }
    }
}
