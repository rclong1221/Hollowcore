using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using Visuals.Components;
using Player.Components;

namespace Visuals.Systems
{
    /// <summary>
    /// Handles flashlight toggle and battery logic using OPTIMIZED split components.
    /// - FlashlightState: Replicated to ALL clients (IsOn, IsFlickering)
    /// - FlashlightConfig: Replicated only to predicted clients (battery, settings)
    /// 
    /// Also syncs to legacy FlashlightData for backward compatibility during migration.
    /// 
    /// Runs in PredictedSimulationSystemGroup so the server processes it authoritatively
    /// and the client predicts it for immediate feedback.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct FlashlightLogicSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Primary loop: Use new optimized split components
            foreach (var (flashState, flashConfig, input) in
                     SystemAPI.Query<RefRW<FlashlightState>, RefRW<FlashlightConfig>, RefRO<PlayerInput>>()
                     .WithAll<Simulate>())
            {
                // Toggle Logic - use frame count comparison to prevent double-toggling
                // != instead of > handles rollback correctly
                if (input.ValueRO.ToggleFlashlight.IsSet)
                {
                    uint inputFrame = input.ValueRO.ToggleFlashlight.FrameCount;
                    if (inputFrame != flashConfig.ValueRO.LastInputFrame)
                    {
                        flashState.ValueRW.IsOn = !flashState.ValueRO.IsOn;
                        flashConfig.ValueRW.LastInputFrame = inputFrame;
                    }
                }

                // Battery Logic
                if (flashState.ValueRO.IsOn)
                {
                    // Drain battery while on
                    flashConfig.ValueRW.BatteryCurrent -= dt * flashConfig.ValueRO.DrainRate;
                    if (flashConfig.ValueRW.BatteryCurrent <= 0)
                    {
                        flashConfig.ValueRW.BatteryCurrent = 0;
                        flashState.ValueRW.IsOn = false;
                    }
                }
                else if (flashConfig.ValueRO.RechargeEnabled && flashConfig.ValueRO.BatteryCurrent < flashConfig.ValueRO.BatteryMax)
                {
                    // Recharge battery while off (if enabled)
                    flashConfig.ValueRW.BatteryCurrent += dt * flashConfig.ValueRO.RechargeRate;
                    if (flashConfig.ValueRW.BatteryCurrent > flashConfig.ValueRO.BatteryMax)
                    {
                        flashConfig.ValueRW.BatteryCurrent = flashConfig.ValueRO.BatteryMax;
                    }
                }

                // Compute IsFlickering server-side for remote clients to receive via FlashlightState
                // This allows remote clients to render the flicker effect without needing BatteryCurrent
                flashState.ValueRW.IsFlickering = flashState.ValueRO.IsOn && flashConfig.ValueRO.IsLowBattery;
            }

            // LEGACY SYNC: Keep FlashlightData in sync for backward compatibility
            // TODO: Remove this loop once all dependent systems are migrated
            foreach (var (flashState, flashConfig, flashData) in
                     SystemAPI.Query<RefRO<FlashlightState>, RefRO<FlashlightConfig>, RefRW<FlashlightData>>()
                     .WithAll<Simulate>())
            {
                flashData.ValueRW.IsOn = flashState.ValueRO.IsOn;
                flashData.ValueRW.BatteryCurrent = flashConfig.ValueRO.BatteryCurrent;
                flashData.ValueRW.BatteryMax = flashConfig.ValueRO.BatteryMax;
                flashData.ValueRW.DrainRate = flashConfig.ValueRO.DrainRate;
                flashData.ValueRW.RechargeRate = flashConfig.ValueRO.RechargeRate;
                flashData.ValueRW.RechargeEnabled = flashConfig.ValueRO.RechargeEnabled;
                flashData.ValueRW.LastInputFrame = flashConfig.ValueRO.LastInputFrame;
            }
        }
    }

    /// <summary>
    /// Handles flashlight visual presentation for ALL players.
    /// Uses OPTIMIZED FlashlightState component (2 bits) for remote clients.
    /// Uses FlashlightConfig for local player (battery percentage display).
    ///
    /// Performance optimizations:
    /// - Light pooling to avoid instantiation overhead
    /// - Only local player light casts shadows
    /// - Distance culling for remote lights
    /// - Bandwidth optimization: remote clients only receive FlashlightState
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class FlashlightPresentationSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentationSystem;

        // Pool of lights for all players (keyed by presentation GO instance ID)
        private readonly Dictionary<int, Light> _playerFlashlights = new();
        private readonly List<int> _staleKeys = new();

        // Light configuration (matching prefab specs)
        private const float LocalLightIntensity = 1000f;
        private const float LocalLightRange = 100f;
        private const float LocalLightInnerSpotAngle = 10f;
        private const float LocalLightOuterSpotAngle = 20f;
        private const float LocalLightIndirectMultiplier = 10f;
        // Remote players get reduced settings for performance
        private const float RemoteLightIntensity = 500f;
        private const float RemoteLightRange = 50f;
        private const float MaxRemoteLightDistance = 50f;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            // Clear stale references from previous domain reload
            _playerFlashlights.Clear();
            _presentationSystem = null;
        }

        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                    return;
            }

            // Track which lights are still in use this frame
            var activeLights = new HashSet<int>();

            // Get local player position for distance culling
            float3 localPlayerPos = float3.zero;
            bool hasLocalPlayer = false;

            foreach (var transform in SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>>()
                     .WithAll<GhostOwnerIsLocal, FlashlightState>())
            {
                localPlayerPos = transform.ValueRO.Position;
                hasLocalPlayer = true;
                break;
            }

            // Query all entities with FlashlightState (optimized - only 2 bits replicated to remote clients)
            foreach (var (flashState, entity) in
                     SystemAPI.Query<RefRO<FlashlightState>>()
                     .WithEntityAccess())
            {
                bool isLocalPlayer = EntityManager.HasComponent<GhostOwnerIsLocal>(entity) &&
                                     EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity);

                // For remote clients, shouldBeOn is simply IsOn (battery is handled server-side)
                // For local player, we check FlashlightConfig for battery
                bool shouldBeOn = flashState.ValueRO.IsOn;

                // Get battery ratio for local player (remote clients don't receive FlashlightConfig)
                float batteryRatio = 1f;
                bool isFlickering = flashState.ValueRO.IsFlickering;
                if (isLocalPlayer && EntityManager.HasComponent<FlashlightConfig>(entity))
                {
                    var config = EntityManager.GetComponentData<FlashlightConfig>(entity);
                    batteryRatio = config.BatteryPercent;
                }

                HandlePlayerFlashlight(entity, shouldBeOn, isFlickering, batteryRatio, isLocalPlayer,
                                       localPlayerPos, hasLocalPlayer, activeLights);
            }

            // Clean up lights for players that no longer exist
            CleanupStaleLights(activeLights);
        }

        private void HandlePlayerFlashlight(Entity entity, bool shouldBeOn, bool isFlickering,
                                            float batteryRatio, bool isLocalPlayer, float3 localPlayerPos,
                                            bool hasLocalPlayer, HashSet<int> activeLights)
        {
            Light light = null;

            // First, try to use existing FlashlightReference (light from prefab)
            if (EntityManager.HasComponent<FlashlightReference>(entity))
            {
                var flashRef = EntityManager.GetComponentObject<FlashlightReference>(entity);
                if (flashRef != null && flashRef.LightSource != null)
                {
                    light = flashRef.LightSource;
                    activeLights.Add(light.GetInstanceID());
                }
            }

            // If no prefab light, try to create one on the presentation GameObject
            if (light == null)
            {
                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation == null)
                    return;

                int instanceId = presentation.GetInstanceID();
                activeLights.Add(instanceId);

                // Distance culling for remote players only
                if (!isLocalPlayer && hasLocalPlayer)
                {
                    float3 remotePos = presentation.transform.position;
                    float distSq = math.distancesq(localPlayerPos, remotePos);
                    if (distSq > MaxRemoteLightDistance * MaxRemoteLightDistance)
                    {
                        if (_playerFlashlights.TryGetValue(instanceId, out var farLight) && farLight != null)
                        {
                            farLight.enabled = false;
                        }
                        return;
                    }
                }

                // Get or create light for this player
                if (!_playerFlashlights.TryGetValue(instanceId, out light) || light == null)
                {
                    light = CreateFlashlight(presentation, isLocalPlayer);
                    _playerFlashlights[instanceId] = light;
                }
            }

            if (light != null)
            {
                light.enabled = shouldBeOn;

                if (shouldBeOn)
                {
                    float intensity = isLocalPlayer ? LocalLightIntensity : RemoteLightIntensity;

                    // Flicker effect - uses IsFlickering from FlashlightState (replicated to all)
                    // This allows remote clients to see the flicker without needing BatteryCurrent
                    if (isFlickering)
                    {
                        float noise = math.sin((float)SystemAPI.Time.ElapsedTime * 20f) * 0.5f + 0.5f;
                        if (noise > 0.8f) intensity = 0f;
                    }

                    light.intensity = intensity;
                    light.range = isLocalPlayer ? LocalLightRange : RemoteLightRange;
                }
            }
        }

        private Light CreateFlashlight(GameObject presentation, bool isLocalPlayer)
        {
            // Find mount point on player model
            Transform mountPoint = FindFlashlightMount(presentation.transform);

            if (mountPoint == null)
            {
                mountPoint = presentation.transform;
            }

            // Create light GameObject
            var lightGO = new GameObject(isLocalPlayer ? "LocalFlashlight" : "RemoteFlashlight");
            lightGO.transform.SetParent(mountPoint, false);
            lightGO.transform.localPosition = Vector3.zero;
            lightGO.transform.localRotation = Quaternion.identity;

            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = Color.white;

            if (isLocalPlayer)
            {
                light.intensity = LocalLightIntensity;
                light.range = LocalLightRange;
                light.innerSpotAngle = LocalLightInnerSpotAngle;
                light.spotAngle = LocalLightOuterSpotAngle;
                light.bounceIntensity = LocalLightIndirectMultiplier;
                light.shadows = LightShadows.None;
            }
            else
            {
                light.intensity = RemoteLightIntensity;
                light.range = RemoteLightRange;
                light.innerSpotAngle = LocalLightInnerSpotAngle;
                light.spotAngle = LocalLightOuterSpotAngle;
                light.bounceIntensity = 1f;
                light.shadows = LightShadows.None;
            }

            light.enabled = false;
            return light;
        }

        private Transform FindFlashlightMount(Transform root)
        {
            // First, look for explicit FlashlightMount
            var mount = root.Find("FlashlightMount");
            if (mount != null)
                return mount;

            // Search recursively for FlashlightMount
            mount = FindChildRecursive(root, "FlashlightMount");
            if (mount != null)
                return mount;

            // Fall back to head bone
            string[] headNames = { "Head", "head", "Bip01 Head", "mixamorig:Head", "ORG-head", "Bone_Head" };
            foreach (var name in headNames)
            {
                var found = FindChildRecursive(root, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void CleanupStaleLights(HashSet<int> activeLights)
        {
            _staleKeys.Clear();

            foreach (var kvp in _playerFlashlights)
            {
                if (!activeLights.Contains(kvp.Key))
                {
                    _staleKeys.Add(kvp.Key);
                }
            }

            foreach (var key in _staleKeys)
            {
                if (_playerFlashlights.TryGetValue(key, out var light) && light != null)
                {
                    Object.Destroy(light.gameObject);
                }
                _playerFlashlights.Remove(key);
            }
        }

        protected override void OnDestroy()
        {
            foreach (var kvp in _playerFlashlights)
            {
                if (kvp.Value != null)
                {
                    Object.Destroy(kvp.Value.gameObject);
                }
            }
            _playerFlashlights.Clear();
        }
    }
}
