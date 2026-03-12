using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Multipliers for incoming damage types.
    /// 1.0 = full damage, 0.0 = full immunity, 0.5 = 50% damage reduction.
    /// Values > 1.0 mean vulnerability (extra damage).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DamageResistance : IComponentData
    {
        [GhostField(Quantization = 100)]
        public float PhysicalMult;

        [GhostField(Quantization = 100)]
        public float HeatMult;

        [GhostField(Quantization = 100)]
        public float RadiationMult;

        [GhostField(Quantization = 100)]
        public float SuffocationMult;

        [GhostField(Quantization = 100)]
        public float ExplosionMult;

        [GhostField(Quantization = 100)]
        public float ToxicMult;

        /// <summary>
        /// Default values (1.0 for all types).
        /// </summary>
        public static DamageResistance Default => new()
        {
            PhysicalMult = 1f,
            HeatMult = 1f,
            RadiationMult = 1f,
            SuffocationMult = 1f,
            ExplosionMult = 1f,
            ToxicMult = 1f
        };
        
        /// <summary>
        /// Get multiplier for a specific damage type.
        /// </summary>
        public float GetMultiplier(DamageType type)
        {
            return type switch
            {
                DamageType.Physical => PhysicalMult,
                DamageType.Heat => HeatMult,
                DamageType.Radiation => RadiationMult,
                DamageType.Suffocation => SuffocationMult,
                DamageType.Explosion => ExplosionMult,
                DamageType.Toxic => ToxicMult,
                _ => 1f
            };
        }
    }
}
