using Unity.Entities;

namespace DIG.Targeting.Core
{
    /// <summary>
    /// Link from Player entity to its separate Targeting Module entity.
    /// This allows the player archetype to stay under the 16KB chunk limit
    /// by moving advanced targeting components to a child entity.
    /// </summary>
    public struct TargetingModuleLink : IComponentData
    {
        /// <summary>
        /// The child entity containing advanced targeting components:
        /// - AimAssistState
        /// - PartTargetingState  
        /// - PredictiveAimState
        /// - MultiLockState
        /// - LockedTargetElement buffer
        /// - OverTheShoulderState
        /// </summary>
        public Entity TargetingModule;
    }
    
    /// <summary>
    /// Tag to identify the Targeting Module entity.
    /// </summary>
    public struct TargetingModuleTag : IComponentData { }
    
    /// <summary>
    /// Back-reference from Targeting Module to its owner Player.
    /// </summary>
    public struct TargetingModuleOwner : IComponentData
    {
        public Entity Owner;
    }
}
