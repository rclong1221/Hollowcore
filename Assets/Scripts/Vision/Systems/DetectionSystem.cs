using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Vision.Components;
using DIG.Vision.Core;
using DIG.Aggro.Components;
using DIG.Weather;

namespace DIG.Vision.Systems
{
    /// <summary>
    /// Main detection system. Runs the 3-phase pipeline per DetectionSensor entity:
    ///   1. Broad phase — OverlapSphere to find candidates within ViewDistance
    ///   2. Cone/proximity check — discard targets outside detection range
    ///   3. Occlusion check — raycast to verify clear line-of-sight
    ///
    /// EPIC 15.19: Now supports AlertStateMultiplier for improved detection when alert.
    /// Server-authoritative: runs on ServerSimulation and LocalSimulation worlds.
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// 
    /// OPTIMIZED: Parallelized Burst job implementation.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct DetectionSystem : ISystem
    {
        private ComponentLookup<AlertState> _alertStateLookup;
        private ComponentLookup<AggroConfig> _aggroConfigLookup;
        private ComponentLookup<Detectable> _detectableLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<WeatherVisionModifier> _weatherVisionLookup;
        private uint _frameCount;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _alertStateLookup = state.GetComponentLookup<AlertState>(true);
            _aggroConfigLookup = state.GetComponentLookup<AggroConfig>(true);
            _detectableLookup = state.GetComponentLookup<Detectable>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _weatherVisionLookup = state.GetComponentLookup<WeatherVisionModifier>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _frameCount++;
            _alertStateLookup.Update(ref state);
            _aggroConfigLookup.Update(ref state);
            _detectableLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _weatherVisionLookup.Update(ref state);

            float deltaTime = SystemAPI.Time.DeltaTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            // Read global settings (fall back to defaults if singleton missing)
            var settings = SystemAPI.HasSingleton<VisionSettings>()
                ? SystemAPI.GetSingleton<VisionSettings>()
                : VisionSettings.Default;

            var occlusionFilter = VisionLayers.OcclusionFilter;
            var detectableFilter = VisionLayers.DetectableFilter;

            uint spreadFrames = (uint)math.max(1, settings.SensorSpreadFrames);

            // Schedule parallel job
            new DetectionJob
            {
                DeltaTime = deltaTime,
                PhysicsWorld = physicsWorld,
                Settings = settings,
                OcclusionFilter = occlusionFilter,
                DetectableFilter = detectableFilter,
                AlertStateLookup = _alertStateLookup,
                AggroConfigLookup = _aggroConfigLookup,
                DetectableLookup = _detectableLookup,
                TransformLookup = _transformLookup,
                WeatherVisionLookup = _weatherVisionLookup,
                FrameCount = _frameCount,
                SpreadFrames = spreadFrames
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct DetectionJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public VisionSettings Settings;
            [ReadOnly] public CollisionFilter OcclusionFilter;
            [ReadOnly] public CollisionFilter DetectableFilter;
            
            [ReadOnly] public ComponentLookup<AlertState> AlertStateLookup;
            [ReadOnly] public ComponentLookup<AggroConfig> AggroConfigLookup;
            [ReadOnly] public ComponentLookup<Detectable> DetectableLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<WeatherVisionModifier> WeatherVisionLookup;
            [ReadOnly] public uint FrameCount;
            [ReadOnly] public uint SpreadFrames;

            public void Execute(
                Entity sensorEntity,
                ref DetectionSensor sensor,
                ref DynamicBuffer<SeenTargetElement> seenBuffer,
                in LocalTransform transform)
            {
                // --- Always accumulate time ---
                sensor.TimeSinceLastUpdate += DeltaTime;

                // --- Frame-slot spreading: cap how many sensors run per frame ---
                // With 100 sensors and SpreadFrames=10, only ~10 can run each frame.
                // This prevents thundering herd where all sensors fire simultaneously.
                if ((uint)sensorEntity.Index % SpreadFrames != FrameCount % SpreadFrames)
                    return;

                // --- Throttle check ---
                float interval = sensor.UpdateInterval > 0f
                    ? sensor.UpdateInterval
                    : Settings.GlobalUpdateInterval;

                if (sensor.TimeSinceLastUpdate < interval)
                    return;

                sensor.TimeSinceLastUpdate = 0f;

                // --- Compute sensor parameters ---
                float3 sensorPos = transform.Position;
                float3 eyePos = sensorPos + new float3(0f, sensor.EyeHeight, 0f);
                float3 sensorForward = math.normalize(math.forward(transform.Rotation));
                float viewDistance = sensor.ViewDistance;
                float halfAngle = sensor.ViewAngle;
                float proximityRadius = sensor.ProximityRadius;
                
                // EPIC 15.19: Apply alert state multiplier to detection range
                if (AlertStateLookup.HasComponent(sensorEntity) && AggroConfigLookup.HasComponent(sensorEntity))
                {
                    var alertState = AlertStateLookup[sensorEntity];
                    if (alertState.AlertLevel > AlertState.IDLE)
                    {
                        float alertMultiplier = AggroConfigLookup[sensorEntity].AlertStateMultiplier;
                        viewDistance *= alertMultiplier;
                        proximityRadius *= alertMultiplier;
                    }
                }

                // EPIC 17.8: Apply weather vision modifier (fog/rain reduces range)
                if (WeatherVisionLookup.HasComponent(sensorEntity))
                {
                    float weatherMult = WeatherVisionLookup[sensorEntity].RangeMultiplier;
                    viewDistance *= weatherMult;
                    proximityRadius *= weatherMult;
                }

                // --- Mark all existing entries as not visible (decay system will handle timing) ---
                for (int i = 0; i < seenBuffer.Length; i++)
                {
                    var entry = seenBuffer[i];
                    entry.IsVisibleNow = false;
                    seenBuffer[i] = entry;
                }

                // --- Broad phase: find candidates within ViewDistance ---
                var candidates = new NativeList<DistanceHit>(32, Allocator.Temp);

                DIG.Player.Systems.CollisionSpatialQueryUtility.OverlapSphere(
                    in PhysicsWorld, eyePos, viewDistance, ref candidates, DetectableFilter);

                // --- Per-candidate: cone check + occlusion ---
                int raycastsThisJob = 0;
                
                for (int c = 0; c < candidates.Length; c++)
                {
                    // Soft cap for raycasts per entity to avoid single entity stalling a thread
                    // (Global limit is hard to enforce in parallel without atomics, relying on per-entity sensible limits)
                    if (raycastsThisJob >= Settings.MaxRaycastsPerFrame) 
                        break;

                    var candidate = candidates[c];
                    Entity candidateEntity = candidate.Entity;

                    // Skip self
                    if (candidateEntity == sensorEntity)
                        continue;

                    // Must have Detectable component (use cached lookup)
                    if (!DetectableLookup.HasComponent(candidateEntity))
                        continue;
                    
                    // Check if component is enabled
                    if (!DetectableLookup.IsComponentEnabled(candidateEntity))
                        continue;

                    var detectable = DetectableLookup[candidateEntity];

                    // Stealth multiplier
                    float stealthMult = Settings.EnableStealthModifiers
                        ? detectable.StealthMultiplier
                        : 1f;

                    // Get target position with height offset (use cached lookup)
                    float3 targetPos;
                    if (TransformLookup.HasComponent(candidateEntity))
                    {
                        var targetTransform = TransformLookup[candidateEntity];
                        targetPos = targetTransform.Position + new float3(0f, detectable.DetectionHeightOffset, 0f);
                    }
                    else
                    {
                        targetPos = candidate.Position + new float3(0f, detectable.DetectionHeightOffset, 0f);
                    }

                    // --- Cone + Proximity + LOS check ---
                    bool visible = DetectionQueryUtility.CanSee(
                        in PhysicsWorld, eyePos, sensorForward, targetPos,
                        viewDistance, halfAngle, proximityRadius, stealthMult, OcclusionFilter);

                    raycastsThisJob++;

                    if (!visible)
                        continue;

                    // --- Update or add buffer entry ---
                    bool found = false;
                    for (int b = 0; b < seenBuffer.Length; b++)
                    {
                        if (seenBuffer[b].Entity == candidateEntity)
                        {
                            seenBuffer[b] = new SeenTargetElement
                            {
                                Entity = candidateEntity,
                                LastKnownPosition = targetPos,
                                TimeSinceLastSeen = 0f,
                                IsVisibleNow = true
                            };
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        seenBuffer.Add(new SeenTargetElement
                        {
                            Entity = candidateEntity,
                            LastKnownPosition = targetPos,
                            TimeSinceLastSeen = 0f,
                            IsVisibleNow = true
                        });
                    }
                }

                candidates.Dispose();
            }
        }
    }
}
