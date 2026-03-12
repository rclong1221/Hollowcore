using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Map
{
    /// <summary>
    /// EPIC 17.6: Reveals fog-of-war texture as the player moves through the world.
    /// Uses GPU blit with circle stamp material for efficient reveal drawing.
    /// Double-buffers via staging RT to avoid self-blit undefined behavior.
    /// Also handles POI auto-discovery when player enters AutoDiscoverRadius.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MinimapCameraSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class FogOfWarSystem : SystemBase
    {
        private static readonly int _CenterProp = Shader.PropertyToID("_Center");
        private static readonly int _RadiusProp = Shader.PropertyToID("_Radius");

        private RenderTexture _stagingRT;
        private bool _cpuFallbackWarned;

        protected override void OnCreate()
        {
            RequireForUpdate<MapRevealState>();
            RequireForUpdate<MapManagedState>();
        }

        protected override void OnDestroy()
        {
            if (_stagingRT != null)
            {
                _stagingRT.Release();
                _stagingRT = null;
            }
        }

        protected override void OnUpdate()
        {
            var revealState = SystemAPI.GetSingleton<MapRevealState>();
            var managed = SystemAPI.ManagedAPI.GetSingleton<MapManagedState>();
            if (!managed.IsInitialized || managed.FogOfWarTexture == null) return;

            // Find local player position
            float3 playerPos = float3.zero;
            bool foundPlayer = false;

            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<GhostOwnerIsLocal>())
            {
                playerPos = ltw.ValueRO.Position;
                foundPlayer = true;
                break;
            }
            if (!foundPlayer) return;

            // Check if player moved enough to warrant new reveal
            float dx = playerPos.x - revealState.LastRevealX;
            float dz = playerPos.z - revealState.LastRevealZ;
            float moveDist = math.sqrt(dx * dx + dz * dz);

            if (moveDist < revealState.RevealMoveThreshold) return;

            // Convert world position to fog UV coordinates
            float worldRangeX = revealState.WorldMaxX - revealState.WorldMinX;
            float worldRangeZ = revealState.WorldMaxZ - revealState.WorldMinZ;
            if (worldRangeX <= 0f || worldRangeZ <= 0f) return;

            float fogU = (playerPos.x - revealState.WorldMinX) / worldRangeX;
            float fogV = (playerPos.z - revealState.WorldMinZ) / worldRangeZ;

            // Compute radius in UV space
            float revealRadiusU = revealState.RevealRadius / worldRangeX;
            float revealRadiusV = revealState.RevealRadius / worldRangeZ;
            float avgRevealRadius = (revealRadiusU + revealRadiusV) * 0.5f;

            // GPU blit: stamp circle on fog texture via double-buffer to avoid self-blit UB
            if (managed.FogRevealMaterial != null)
            {
                var fogRT = managed.FogOfWarTexture;

                // Ensure staging RT matches fog RT dimensions
                if (_stagingRT == null || _stagingRT.width != fogRT.width || _stagingRT.height != fogRT.height)
                {
                    if (_stagingRT != null) _stagingRT.Release();
                    _stagingRT = new RenderTexture(fogRT.width, fogRT.height, 0, fogRT.format);
                    _stagingRT.name = "FogStagingRT";
                    _stagingRT.filterMode = FilterMode.Bilinear;
                }

                managed.FogRevealMaterial.SetVector(_CenterProp, new Vector4(fogU, fogV, 0, 0));
                managed.FogRevealMaterial.SetFloat(_RadiusProp, avgRevealRadius);

                // Double-buffer: blit fog→staging with reveal shader, then copy staging→fog
                Graphics.Blit(fogRT, _stagingRT, managed.FogRevealMaterial);
                Graphics.Blit(_stagingRT, fogRT);
            }
            else
            {
                // CPU fallback removed — would freeze game for 30-100ms on 1024x1024 texture.
                // Log once and skip. Fix: assign FogRevealMaterial in Resources/MapFogRevealMat.
                if (!_cpuFallbackWarned)
                {
                    Debug.LogError("[FogOfWar] FogRevealMaterial is null — fog reveal disabled. " +
                        "Create material with DIG/Map/FogReveal shader at Resources/MapFogRevealMat.");
                    _cpuFallbackWarned = true;
                }
                // Still update position tracking so it doesn't spam every frame when material is fixed
            }

            // Approximate new pixels revealed
            float circleArea = math.PI * revealState.RevealRadius * revealState.RevealRadius;
            float worldArea = worldRangeX * worldRangeZ;
            int approxNewPixels = (int)(circleArea / worldArea * revealState.TotalPixels);
            revealState.TotalRevealed = math.min(revealState.TotalRevealed + approxNewPixels, revealState.TotalPixels);
            revealState.LastRevealX = playerPos.x;
            revealState.LastRevealZ = playerPos.z;
            SystemAPI.SetSingleton(revealState);

            // POI auto-discovery
            CheckPOIDiscovery(playerPos);
        }

        private void CheckPOIDiscovery(float3 playerPos)
        {
            if (!SystemAPI.ManagedAPI.HasSingleton<MapDatabaseManaged>()) return;
            var dbManaged = SystemAPI.ManagedAPI.GetSingleton<MapDatabaseManaged>();
            if (dbManaged.POIRegistry == null) return;

            float discoverRadiusSq = dbManaged.POIRegistry.AutoDiscoverRadius * dbManaged.POIRegistry.AutoDiscoverRadius;

            foreach (var (poi, ltw) in SystemAPI.Query<RefRW<PointOfInterest>, RefRO<LocalToWorld>>())
            {
                if (poi.ValueRO.DiscoveredByPlayer) continue;

                float distSq = math.distancesq(playerPos, ltw.ValueRO.Position);
                if (distSq < discoverRadiusSq)
                {
                    poi.ValueRW.DiscoveredByPlayer = true;
                    // Use FixedString.ToString() only on discovery (one-time event, not per-frame)
                    var label = poi.ValueRO.Label;
                    MapUIRegistry.NotifyPOIDiscovered(label.ToString(), poi.ValueRO.Type);
                }
            }
        }
    }
}
