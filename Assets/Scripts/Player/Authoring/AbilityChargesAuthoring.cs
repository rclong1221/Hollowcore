using UnityEngine;
using Unity.Entities;
using DIG.Player;

namespace DIG.Player.Authoring
{
    /// <summary>
    /// Adds ability charge tracking to an entity.
    /// For abilities with discrete charges (like dashes, grenades, etc.)
    /// </summary>
    [AddComponentMenu("DIG/Abilities/Ability Charges Authoring")]
    public class AbilityChargesAuthoring : MonoBehaviour
    {
        [Header("Charges")]
        [Tooltip("Maximum number of charges")]
        public int MaxCharges = 3;
        
        [Tooltip("Starting number of charges")]
        public int StartingCharges = 3;
        
        [Header("Recharge")]
        [Tooltip("Time in seconds to recharge one charge")]
        public float RechargeTime = 5f;
        
        [Tooltip("If true, all charges recharge simultaneously")]
        public bool ParallelRecharge = false;
        
        [Header("Behavior")]
        [Tooltip("If true, charges can exceed max temporarily (from buffs)")]
        public bool AllowOvercharge = false;
        
        [Tooltip("If overcharge allowed, maximum overcharge amount")]
        public int MaxOvercharge = 1;

        class Baker : Baker<AbilityChargesAuthoring>
        {
            public override void Bake(AbilityChargesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new AbilityCharges
                {
                    CurrentCharges = authoring.StartingCharges,
                    MaxCharges = authoring.MaxCharges,
                    RechargeTime = authoring.RechargeTime,
                    RechargeProgress = 0f,
                    ParallelRecharge = authoring.ParallelRecharge,
                    AllowOvercharge = authoring.AllowOvercharge,
                    MaxOvercharge = authoring.MaxOvercharge
                });
            }
        }
    }
}
