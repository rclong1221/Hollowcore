using Unity.Entities;
using Unity.NetCode;

namespace DIG.Survival.Core
{
    /// <summary>
    /// Damage event raised by survival systems (oxygen, radiation, temperature).
    /// Consumed by a bridge system in Assembly-CSharp that applies to Health.
    /// </summary>
    /// <remarks>
    /// This component is used to decouple the Survival assembly from Player.Components.
    /// The SurvivalDamageAdapterSystem in Assembly-CSharp reads this and applies damage.
    /// </remarks>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SurvivalDamageEvent : IComponentData
    {
        /// <summary>
        /// Damage to apply this frame from survival systems.
        /// Reset to 0 after being consumed.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float PendingDamage;

        /// <summary>
        /// Source of the damage for UI/effects.
        /// </summary>
        public SurvivalDamageSource Source;
    }

    /// <summary>
    /// Source of survival damage for UI feedback.
    /// </summary>
    public enum SurvivalDamageSource : byte
    {
        None = 0,
        Suffocation = 1,
        Radiation = 2,
        Hypothermia = 3,
        Hyperthermia = 4,
        Toxic = 5
    }
}
