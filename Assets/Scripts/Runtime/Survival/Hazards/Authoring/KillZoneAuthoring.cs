using UnityEngine;
using Unity.Entities;
using DIG.Survival.Hazards;

namespace DIG.Survival.Hazards.Authoring
{
    public class KillZoneAuthoring : MonoBehaviour
    {
        public float DamagePerSecond = 1000f;
    }

    public class KillZoneAuthoringBaker : Baker<KillZoneAuthoring>
    {
        public override void Bake(KillZoneAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new KillZone
            {
                DamagePerSecond = authoring.DamagePerSecond
            });
        }
    }
}
