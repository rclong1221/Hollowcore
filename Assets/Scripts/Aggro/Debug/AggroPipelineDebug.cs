using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using DIG.Aggro.Components;
using DIG.Vision.Components;
using DIG.Vision.Core;
using DIG.Combat.UI;
using DIG.Player.Components;

namespace DIG.Aggro.Debug
{
    /// <summary>
    /// EPIC 15.19 Debug: End-to-end aggro pipeline diagnostics.
    /// Filter console by "[AGGRO]" to see all related logs.
    /// Attach to any GameObject in scene to enable.
    /// </summary>
    public class AggroPipelineDebug : MonoBehaviour
    {
        [Header("Settings")]
        public bool EnableLogging = true;
        public float LogInterval = 1.0f;
        
        private float _lastLogTime;
        private World _serverWorld;
        
        void Update()
        {
            if (!EnableLogging) return;
            if (Time.time - _lastLogTime < LogInterval) return;
            _lastLogTime = Time.time;
            
            FindServerWorld();
            if (_serverWorld == null)
            {
                UnityEngine.Debug.Log("[AGGRO] ERR: No ServerWorld found");
                return;
            }
            
            var em = _serverWorld.EntityManager;
            em.CompleteAllTrackedJobs();
            
            LogVisionSensors(em);
            LogAggroEntities(em);
            LogHasAggroOn(em);
        }
        
        void FindServerWorld()
        {
            if (_serverWorld != null && _serverWorld.IsCreated) return;
            foreach (var w in World.All)
            {
                if (w.Name == "ServerWorld" && w.IsCreated)
                {
                    _serverWorld = w;
                    return;
                }
            }
        }
        
        void LogVisionSensors(EntityManager em)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<DetectionSensor>(),
                ComponentType.ReadOnly<SeenTargetElement>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            
            int sensorCount = query.CalculateEntityCount();
            if (sensorCount == 0)
            {
                UnityEngine.Debug.Log("[AGGRO] VISION: 0 sensors (need DetectionSensorAuthoring on enemy)");
                return;
            }
            
            using var entities = query.ToEntityArray(Allocator.Temp);
            int totalSeen = 0;
            int visibleNow = 0;
            
            for (int i = 0; i < entities.Length; i++)
            {
                var buffer = em.GetBuffer<SeenTargetElement>(entities[i], true);
                totalSeen += buffer.Length;
                for (int j = 0; j < buffer.Length; j++)
                {
                    if (buffer[j].IsVisibleNow) visibleNow++;
                }
            }
            
            UnityEngine.Debug.Log($"[AGGRO] VISION: {sensorCount} sensors, {totalSeen} seen entries, {visibleNow} visible now");
            
            // Extra debug: Check if Detectable entities exist
            LogDetectables(em, entities);
        }
        
        void LogDetectables(EntityManager em, NativeArray<Entity> sensorEntities)
        {
            // Count Detectable entities
            using var detectableQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<Detectable>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            int detectableCount = detectableQuery.CalculateEntityCount();
            
            if (detectableCount == 0)
            {
                UnityEngine.Debug.Log("[AGGRO] DETECT: 0 Detectable entities (player needs DetectableAuthoring)");
                return;
            }
            
            // Get first sensor position and first detectable position
            if (sensorEntities.Length == 0) return;
            
            var sensorPos = em.GetComponentData<LocalTransform>(sensorEntities[0]).Position;
            var sensor = em.GetComponentData<DetectionSensor>(sensorEntities[0]);
            
            using var detectables = detectableQuery.ToEntityArray(Allocator.Temp);
            var detectablePos = em.GetComponentData<LocalTransform>(detectables[0]).Position;
            float dist = math.distance(sensorPos, detectablePos);
            
            // Check if Detectable is enabled and has PhysicsCollider
            var detectableEntity = detectables[0];
            bool isEnabled = em.IsComponentEnabled<Detectable>(detectableEntity);
            bool hasCollider = em.HasComponent<PhysicsCollider>(detectableEntity);
            
            // Check the collider's BelongsTo layer
            uint belongsTo = 0;
            if (hasCollider)
            {
                var collider = em.GetComponentData<PhysicsCollider>(detectableEntity);
                if (collider.IsValid)
                {
                    belongsTo = collider.Value.Value.GetCollisionFilter().BelongsTo;
                }
            }
            
            // Check physics world for overlap test
            string physicsInfo = "N/A";
            int detectableHits = 0;
            using var physicsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            if (!physicsQuery.IsEmpty)
            {
                var physicsWorld = physicsQuery.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
                var filter = VisionLayers.DetectableFilter;
                var candidates = new NativeList<DistanceHit>(8, Allocator.Temp);
                float3 eyePos = sensorPos + new float3(0, sensor.EyeHeight, 0);
                
                DIG.Player.Systems.CollisionSpatialQueryUtility.OverlapSphere(
                    in physicsWorld, eyePos, sensor.ViewDistance, ref candidates, filter);
                
                // Count how many hits have Detectable component
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (em.HasComponent<Detectable>(candidates[i].Entity))
                        detectableHits++;
                }
                
                physicsInfo = $"overlap={candidates.Length} hits, {detectableHits} with Detectable";
                candidates.Dispose();
            }
            
            UnityEngine.Debug.Log($"[AGGRO] DETECT: {detectableCount} entities, hasCollider={hasCollider}, belongsTo={belongsTo}, enabled={isEnabled}, dist={dist:F1}m, {physicsInfo}");
        }
        
        void LogAggroEntities(EntityManager em)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<AggroConfig>(),
                ComponentType.ReadOnly<AggroState>(),
                ComponentType.ReadOnly<ThreatEntry>()
            );

            int aggroCount = query.CalculateEntityCount();
            if (aggroCount == 0)
            {
                UnityEngine.Debug.Log("[AGGRO] THREAT: 0 entities (need AggroAuthoring on enemy)");
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            int withTarget = 0;
            int totalThreats = 0;
            int socialCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                var agState = em.GetComponentData<AggroState>(entities[i]);
                if (agState.CurrentThreatLeader != Entity.Null) withTarget++;

                if (em.HasComponent<SocialAggroConfig>(entities[i]))
                    socialCount++;

                var buffer = em.GetBuffer<ThreatEntry>(entities[i], true);
                totalThreats += buffer.Length;

                // Show first entity's threat details with source flags
                if (i == 0 && buffer.Length > 0)
                {
                    var cfg = em.GetComponentData<AggroConfig>(entities[i]);
                    var entry = buffer[0];
                    string flags = FormatSourceFlags(entry.SourceFlags);
                    UnityEngine.Debug.Log($"[AGGRO] THREAT DETAIL: threat={entry.ThreatValue:F1}, dmgThreat={entry.DamageThreat:F1}, visible={entry.IsCurrentlyVisible}, " +
                        $"timeSinceVisible={entry.TimeSinceVisible:F1}s, flags=[{flags}], mode={cfg.SelectionMode}");
                }

                // Log alert state for first entity
                if (i == 0 && em.HasComponent<AlertState>(entities[i]))
                {
                    var alert = em.GetComponentData<AlertState>(entities[i]);
                    UnityEngine.Debug.Log($"[AGGRO] ALERT: level={AlertLevelName(alert.AlertLevel)}, timer={alert.AlertTimer:F1}s, searchTimer={alert.SearchTimer:F1}s");
                }
            }

            UnityEngine.Debug.Log($"[AGGRO] THREAT: {aggroCount} entities, {totalThreats} threat entries, {withTarget} with target, {socialCount} social");
        }

        static string FormatSourceFlags(ThreatSourceFlags flags)
        {
            if (flags == ThreatSourceFlags.None) return "None";
            var parts = new System.Collections.Generic.List<string>();
            if ((flags & ThreatSourceFlags.Damage) != 0) parts.Add("Dmg");
            if ((flags & ThreatSourceFlags.Vision) != 0) parts.Add("Vis");
            if ((flags & ThreatSourceFlags.Hearing) != 0) parts.Add("Hear");
            if ((flags & ThreatSourceFlags.Social) != 0) parts.Add("Soc");
            if ((flags & ThreatSourceFlags.Proximity) != 0) parts.Add("Prox");
            if ((flags & ThreatSourceFlags.Taunt) != 0) parts.Add("Taunt");
            if ((flags & ThreatSourceFlags.Healing) != 0) parts.Add("Heal");
            return string.Join("|", parts);
        }

        static string AlertLevelName(int level)
        {
            return level switch
            {
                AlertState.IDLE => "IDLE",
                AlertState.CURIOUS => "CURIOUS",
                AlertState.SUSPICIOUS => "SUSPICIOUS",
                AlertState.SEARCHING => "SEARCHING",
                AlertState.COMBAT => "COMBAT",
                _ => $"Unknown({level})"
            };
        }
        
        void LogHasAggroOn(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<HasAggroOn>());
            int count = query.CalculateEntityCount();
            
            if (count == 0)
            {
                UnityEngine.Debug.Log("[AGGRO] OUTPUT: 0 HasAggroOn components (aggro not triggering)");
            }
            else
            {
                UnityEngine.Debug.Log($"[AGGRO] OUTPUT: {count} entities have HasAggroOn (should show health bars)");
            }
        }
    }
}
