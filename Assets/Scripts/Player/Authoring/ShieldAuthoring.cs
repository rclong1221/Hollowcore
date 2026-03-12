using UnityEngine;
using Unity.Entities;
using DIG.Player;

namespace DIG.Player.Authoring
{
    /// <summary>
    /// Adds energy shield capability to an entity.
    /// Shield absorbs damage before health and regenerates after a delay.
    /// </summary>
    [AddComponentMenu("DIG/Player/Shield Authoring")]
    public class ShieldAuthoring : MonoBehaviour
    {
        [Header("Shield Capacity")]
        [Tooltip("Maximum shield value")]
        public float MaxShield = 100f;
        
        [Tooltip("Starting shield value")]
        public float StartingShield = 100f;
        
        [Header("Regeneration")]
        [Tooltip("Delay in seconds before shield starts regenerating after damage")]
        public float RechargeDelay = 3f;
        
        [Tooltip("Shield regeneration rate per second")]
        public float RechargeRate = 25f;
        
        [Header("Break Behavior")]
        [Tooltip("If true, shield breaks completely at 0 and requires full recharge delay")]
        public bool CanBreak = true;
        
        [Tooltip("Extra delay when shield breaks completely (added to recharge delay)")]
        public float BreakPenaltyDelay = 2f;
        
        [Header("Damage Absorption")]
        [Tooltip("Percentage of damage absorbed by shield (0-1). Remainder goes to health.")]
        [Range(0f, 1f)]
        public float AbsorptionRatio = 1f;

        class Baker : Baker<ShieldAuthoring>
        {
            public override void Bake(ShieldAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new PlayerShield
                {
                    Current = authoring.StartingShield,
                    Max = authoring.MaxShield,
                    RechargeDelay = authoring.RechargeDelay,
                    RechargeRate = authoring.RechargeRate,
                    RechargeTimer = 0f,
                    IsBroken = false,
                    CanBreak = authoring.CanBreak,
                    BreakPenaltyDelay = authoring.BreakPenaltyDelay,
                    AbsorptionRatio = authoring.AbsorptionRatio
                });
            }
        }
    }
}
