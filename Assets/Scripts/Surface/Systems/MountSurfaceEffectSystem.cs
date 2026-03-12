using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;
using Audio.Systems;
using DIG.Interaction;
using DIG.Surface.Config;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 10: Spawns tire tracks, hoof prints, skid marks, and surface spray
    /// for mounted entities. Queries players with MountState.IsMounted == true.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SurfaceImpactPresenterSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class MountSurfaceEffectSystem : SystemBase
    {
        private MountSurfaceEffectConfig _config;
        private DecalManager _decalManager;

        // Per-entity state tracking (using static since we only track local player)
        private float _accumulatedDistance;
        private float3 _lastPosition;
        private float _lastSpeed;
        private bool _tracking;

        protected override void OnCreate()
        {
            _config = Resources.Load<MountSurfaceEffectConfig>("MountSurfaceEffectConfig");
        }

        protected override void OnUpdate()
        {
            if (_decalManager == null)
                _decalManager = DecalManager.Instance;

            if (_config == null || _decalManager == null) return;

            bool foundMountedPlayer = false;

            foreach (var (mountState, transform, groundState) in
                     SystemAPI.Query<RefRO<MountState>, RefRO<LocalTransform>, RefRO<GroundSurfaceState>>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                if (!mountState.ValueRO.IsMounted)
                {
                    if (_tracking)
                    {
                        _tracking = false;
                        _accumulatedDistance = 0f;
                    }
                    continue;
                }

                foundMountedPlayer = true;
                float3 pos = transform.ValueRO.Position;

                if (!_tracking)
                {
                    _lastPosition = pos;
                    _lastSpeed = 0f;
                    _accumulatedDistance = 0f;
                    _tracking = true;
                    continue;
                }

                float3 delta = pos - _lastPosition;
                float distance = math.length(delta);
                float speed = distance / math.max(SystemAPI.Time.DeltaTime, 0.001f);

                // Read ground surface from ECS state (no raycast needed)
                SurfaceID groundSurface = groundState.ValueRO.SurfaceId;

                // Track marks: spawn decal every TrackSpacing meters
                _accumulatedDistance += distance;
                if (_accumulatedDistance >= _config.TrackSpacing && _config.TrackDecal != null)
                {
                    // Align decal to movement direction
                    float3 facing = math.normalizesafe(delta);
                    if (math.lengthsq(facing) < 0.01f)
                        facing = new float3(0, 0, 1);

                    var rotation = quaternion.LookRotation(facing, new float3(0, 1, 0));
                    _decalManager.SpawnDecal(_config.TrackDecal, pos, rotation, _config.TrackLifetime);
                    _accumulatedDistance = 0f;
                }

                // Skid marks: sudden deceleration
                float deceleration = (_lastSpeed - speed) / math.max(SystemAPI.Time.DeltaTime, 0.001f);
                if (deceleration > _config.SkidDecelThreshold && _config.SkidDecal != null)
                {
                    float3 facing = math.normalizesafe(delta);
                    if (math.lengthsq(facing) < 0.01f)
                        facing = new float3(0, 0, 1);

                    var rotation = quaternion.LookRotation(facing, new float3(0, 1, 0));
                    _decalManager.SpawnDecal(_config.SkidDecal, pos, rotation, _config.TrackLifetime);
                }

                // Surface spray: dust/mud/snow at speed
                if (speed > _config.SpraySpeedThreshold && _config.IsSpraySurface(groundSurface))
                {
                    // Enqueue a spray impact behind the mount
                    float3 behind = pos - math.normalizesafe(delta) * 0.5f;
                    SurfaceImpactQueue.Enqueue(new SurfaceImpactData
                    {
                        Position = behind,
                        Normal = new float3(0, 1, 0),
                        Velocity = -delta * 2f,
                        SurfaceId = groundSurface,
                        ImpactClass = ImpactClass.Environmental,
                        SurfaceMaterialId = SurfaceDetectionService.GetDefaultId(),
                        Intensity = math.saturate(speed / (_config.SpraySpeedThreshold * 3f)),
                        LODTier = EffectLODTier.Full
                    });
                }

                _lastPosition = pos;
                _lastSpeed = speed;
            }

            if (!foundMountedPlayer && _tracking)
            {
                _tracking = false;
                _accumulatedDistance = 0f;
            }
        }
    }
}
