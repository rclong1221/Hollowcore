using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 16.1 Phase 5: Authoring component for proximity zones.
    ///
    /// Designer workflow:
    /// 1. Create an empty GameObject in the subscene
    /// 2. Add ProximityZoneAuthoring
    /// 3. Configure radius, effect type, interval, and value
    /// 4. Game-specific systems (in DIG.Player) apply the actual effects
    ///    by reading ProximityZone.EffectTickReady and the occupant buffer
    /// </summary>
    public class ProximityZoneAuthoring : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [Tooltip("Detection radius around this object")]
        public float Radius = 5f;

        [Tooltip("What effect to apply to entities inside")]
        public ProximityEffect Effect = ProximityEffect.Heal;

        [Tooltip("Seconds between effect ticks. 0 = every frame")]
        public float EffectInterval = 1f;

        [Tooltip("Magnitude of the effect (heal amount, damage per tick, etc.)")]
        public float EffectValue = 10f;

        [Header("Constraints")]
        [Tooltip("Maximum simultaneous occupants. 0 = unlimited")]
        public int MaxOccupants = 0;

        [Tooltip("Occupants must have line of sight to zone center")]
        public bool RequiresLineOfSight = false;

        [Header("Visual")]
        [Tooltip("Display a world-space radius indicator")]
        public bool ShowWorldSpaceUI = false;

        public class Baker : Baker<ProximityZoneAuthoring>
        {
            public override void Bake(ProximityZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new ProximityZone
                {
                    Radius = authoring.Radius,
                    Effect = authoring.Effect,
                    EffectInterval = authoring.EffectInterval,
                    EffectValue = authoring.EffectValue,
                    MaxOccupants = authoring.MaxOccupants,
                    RequiresLineOfSight = authoring.RequiresLineOfSight,
                    ShowWorldSpaceUI = authoring.ShowWorldSpaceUI,
                    EffectTimer = 0,
                    EffectTickReady = false,
                    CurrentOccupantCount = 0
                });

                AddBuffer<ProximityZoneOccupant>(entity);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Color-code by effect type
            Color zoneColor = Effect switch
            {
                ProximityEffect.Heal => new Color(0.2f, 1f, 0.3f, 0.3f),
                ProximityEffect.Damage => new Color(1f, 0.2f, 0.2f, 0.3f),
                ProximityEffect.Buff => new Color(0.3f, 0.6f, 1f, 0.3f),
                ProximityEffect.Debuff => new Color(0.8f, 0.4f, 1f, 0.3f),
                ProximityEffect.Shop => new Color(1f, 0.9f, 0.2f, 0.3f),
                ProximityEffect.Dialogue => new Color(1f, 1f, 1f, 0.2f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.3f)
            };

            // Filled sphere
            Gizmos.color = zoneColor;
            Gizmos.DrawSphere(transform.position, Radius);

            // Wire sphere outline
            zoneColor.a = 0.8f;
            Gizmos.color = zoneColor;
            Gizmos.DrawWireSphere(transform.position, Radius);

            // Center marker
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }

        private void OnDrawGizmos()
        {
            // Always show a subtle outline
            Color zoneColor = Effect switch
            {
                ProximityEffect.Heal => new Color(0.2f, 1f, 0.3f, 0.1f),
                ProximityEffect.Damage => new Color(1f, 0.2f, 0.2f, 0.1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.1f)
            };

            Gizmos.color = zoneColor;
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
#endif
    }
}
