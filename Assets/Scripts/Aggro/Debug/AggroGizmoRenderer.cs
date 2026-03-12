using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;

namespace DIG.Aggro.Debug
{
    /// <summary>
    /// EPIC 15.33: Scene-view gizmos for the aggro/threat system.
    ///
    /// Draws:
    /// - Lines from enemy to threat sources (color coded by ThreatSourceFlags)
    /// - Wireframe spheres for ProximityThreatRadius, CallForHelpRadius, AggroShareRadius
    /// - Alert level color on enemy capsule
    /// - Lines between LinkedPull group members
    ///
    /// Attach to any GameObject in scene. Only draws in Scene view (OnDrawGizmos).
    /// </summary>
    public class AggroGizmoRenderer : MonoBehaviour
    {
        [Header("Toggle")]
        public bool DrawThreatLines = true;
        public bool DrawRadii = true;
        public bool DrawAlertCapsules = true;
        public bool DrawLinkedPullGroups = true;

        [Header("Appearance")]
        [Range(0.5f, 3f)]
        public float CapsuleRadius = 0.5f;
        [Range(1f, 3f)]
        public float CapsuleHeight = 2f;

        private World _serverWorld;

        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            FindServerWorld();
            if (_serverWorld == null || !_serverWorld.IsCreated) return;

            var em = _serverWorld.EntityManager;
            em.CompleteAllTrackedJobs();

            using var aggroQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<AggroConfig>(),
                ComponentType.ReadOnly<AggroState>(),
                ComponentType.ReadOnly<ThreatEntry>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            if (aggroQuery.CalculateEntityCount() == 0) return;

            using var entities = aggroQuery.ToEntityArray(Allocator.Temp);
            using var configs = aggroQuery.ToComponentDataArray<AggroConfig>(Allocator.Temp);
            using var states = aggroQuery.ToComponentDataArray<AggroState>(Allocator.Temp);
            using var transforms = aggroQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Vector3 pos = transforms[i].Position;

                // === Alert Level Capsule ===
                if (DrawAlertCapsules)
                {
                    int alertLevel = AlertState.IDLE;
                    if (em.HasComponent<AlertState>(entities[i]))
                        alertLevel = em.GetComponentData<AlertState>(entities[i]).AlertLevel;

                    Gizmos.color = AlertLevelColor(alertLevel);
                    DrawWireCapsule(pos + Vector3.up * CapsuleHeight * 0.5f, CapsuleRadius, CapsuleHeight);
                }

                // === Radii ===
                if (DrawRadii)
                {
                    // Proximity threat radius (purple)
                    if (configs[i].ProximityThreatRadius > 0f)
                    {
                        Gizmos.color = new Color(0.6f, 0f, 0.8f, 0.3f);
                        Gizmos.DrawWireSphere(pos, configs[i].ProximityThreatRadius);
                    }

                    // Aggro share radius (cyan)
                    if (configs[i].AggroShareRadius > 0f)
                    {
                        Gizmos.color = new Color(0f, 0.8f, 0.8f, 0.15f);
                        Gizmos.DrawWireSphere(pos, configs[i].AggroShareRadius);
                    }

                    // Social radii
                    if (em.HasComponent<SocialAggroConfig>(entities[i]))
                    {
                        var social = em.GetComponentData<SocialAggroConfig>(entities[i]);

                        // Call-for-help radius (orange)
                        if (social.CallForHelpRadius > 0f)
                        {
                            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
                            Gizmos.DrawWireSphere(pos, social.CallForHelpRadius);
                        }
                    }
                }

                // === Threat Lines ===
                if (DrawThreatLines)
                {
                    var buffer = em.GetBuffer<ThreatEntry>(entities[i], true);
                    for (int t = 0; t < buffer.Length; t++)
                    {
                        var entry = buffer[t];
                        Vector3 targetPos = (Vector3)(float3)entry.LastKnownPosition;
                        Gizmos.color = ThreatFlagColor(entry.SourceFlags);
                        float thickness = math.clamp(entry.ThreatValue / 50f, 0.5f, 3f);
                        Gizmos.DrawLine(pos + Vector3.up, targetPos + Vector3.up);

                        // Small sphere at target end sized by threat
                        float sphereR = math.clamp(entry.ThreatValue / 100f, 0.1f, 0.5f);
                        Gizmos.DrawWireSphere(targetPos + Vector3.up, sphereR);
                    }
                }

                // === LinkedPull Group Lines ===
                if (DrawLinkedPullGroups && em.HasComponent<SocialAggroConfig>(entities[i]))
                {
                    var social = em.GetComponentData<SocialAggroConfig>(entities[i]);
                    if (social.EncounterGroupId > 0 && (social.Flags & SocialAggroFlags.LinkedPull) != 0)
                    {
                        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
                        // Draw lines to other members with same group ID
                        for (int j = i + 1; j < entities.Length; j++)
                        {
                            if (!em.HasComponent<SocialAggroConfig>(entities[j])) continue;
                            var otherSocial = em.GetComponentData<SocialAggroConfig>(entities[j]);
                            if (otherSocial.EncounterGroupId == social.EncounterGroupId)
                            {
                                Vector3 otherPos = transforms[j].Position;
                                Gizmos.DrawLine(pos + Vector3.up * 0.1f, otherPos + Vector3.up * 0.1f);
                            }
                        }
                    }
                }
            }
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

        static Color AlertLevelColor(int level)
        {
            return level switch
            {
                AlertState.IDLE => new Color(1f, 1f, 1f, 0.3f),
                AlertState.CURIOUS => new Color(1f, 1f, 0f, 0.5f),
                AlertState.SUSPICIOUS => new Color(1f, 0.65f, 0f, 0.6f),
                AlertState.SEARCHING => new Color(1f, 0.3f, 0f, 0.7f),
                AlertState.COMBAT => new Color(1f, 0f, 0f, 0.8f),
                _ => Color.gray
            };
        }

        static Color ThreatFlagColor(ThreatSourceFlags flags)
        {
            // Priority: Damage > Taunt > Vision > Hearing > Social > Proximity > Healing
            if ((flags & ThreatSourceFlags.Damage) != 0) return Color.red;
            if ((flags & ThreatSourceFlags.Taunt) != 0) return Color.magenta;
            if ((flags & ThreatSourceFlags.Vision) != 0) return Color.blue;
            if ((flags & ThreatSourceFlags.Hearing) != 0) return Color.yellow;
            if ((flags & ThreatSourceFlags.Social) != 0) return Color.green;
            if ((flags & ThreatSourceFlags.Proximity) != 0) return new Color(0.6f, 0f, 0.8f);
            if ((flags & ThreatSourceFlags.Healing) != 0) return Color.cyan;
            return Color.gray;
        }

        static void DrawWireCapsule(Vector3 center, float radius, float height)
        {
            float halfH = height * 0.5f - radius;
            Vector3 top = center + Vector3.up * halfH;
            Vector3 bottom = center - Vector3.up * halfH;
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
            Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
        }
    }
}
