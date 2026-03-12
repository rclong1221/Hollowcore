using UnityEngine;
using Unity.Entities;
using DIG.Player;

namespace DIG.Player.Authoring
{
    /// <summary>
    /// Adds weapon durability tracking to an entity.
    /// Weapons degrade with use and can break.
    /// Note: This goes on weapon entities, not the player.
    /// </summary>
    [AddComponentMenu("DIG/Weapons/Weapon Durability Authoring")]
    public class WeaponDurabilityAuthoring : MonoBehaviour
    {
        [Header("Durability")]
        [Tooltip("Maximum durability value")]
        public float MaxDurability = 100f;
        
        [Tooltip("Starting durability (usually full)")]
        public float StartingDurability = 100f;
        
        [Header("Degradation")]
        [Tooltip("Durability lost per attack/use")]
        public float DegradePerUse = 1f;
        
        [Tooltip("Durability lost per blocked hit (for shields/parry weapons)")]
        public float DegradePerBlock = 2f;
        
        [Header("Break Behavior")]
        [Tooltip("If true, weapon is destroyed when durability reaches 0")]
        public bool DestroyOnBreak = false;
        
        [Tooltip("If true, weapon is unusable (but not destroyed) when broken")]
        public bool DisableOnBreak = true;
        
        [Header("Repair")]
        [Tooltip("Can this weapon be repaired?")]
        public bool CanRepair = true;
        
        [Tooltip("Maximum durability after repair (can be less than original max)")]
        public float MaxRepairDurability = 100f;

        class Baker : Baker<WeaponDurabilityAuthoring>
        {
            public override void Bake(WeaponDurabilityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new WeaponDurability
                {
                    Current = authoring.StartingDurability,
                    Max = authoring.MaxDurability,
                    DegradePerUse = authoring.DegradePerUse,
                    DegradePerBlock = authoring.DegradePerBlock,
                    DestroyOnBreak = authoring.DestroyOnBreak,
                    DisableOnBreak = authoring.DisableOnBreak,
                    CanRepair = authoring.CanRepair,
                    MaxRepairDurability = authoring.MaxRepairDurability,
                    IsBroken = false
                });
            }
        }
    }
}
