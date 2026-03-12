using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    public class StatusEffectAuthoring : MonoBehaviour
    {
        [Header("Tick Settings")]
        public float TickInterval = 1.0f;
        
        [Header("Damage Per Tick (at max severity)")]
        public float HypoxiaDamage = 5.0f;
        public float RadiationDamage = 2.0f;
        public float BurnDamage = 5.0f;
        public float FrostbiteDamage = 2.0f;
        public float BleedDamage = 2.0f;

        [Header("Combat Modifier DOTs")]
        public float ShockDamage = 4.0f;
        public float PoisonDOTDamage = 3.0f;

        class Baker : Baker<StatusEffectAuthoring>
        {
            public override void Bake(StatusEffectAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new StatusEffectConfig
                {
                    TickInterval = authoring.TickInterval,
                    HypoxiaDamage = authoring.HypoxiaDamage,
                    RadiationDamage = authoring.RadiationDamage,
                    BurnDamage = authoring.BurnDamage,
                    FrostbiteDamage = authoring.FrostbiteDamage,
                    BleedDamage = authoring.BleedDamage,
                    ConcussionDamage = 0f,
                    ShockDamage = authoring.ShockDamage,
                    PoisonDOTDamage = authoring.PoisonDOTDamage
                });
            }
        }
    }
}
