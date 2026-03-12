using Unity.Entities;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.19: Event component for taunt/detaunt abilities and threat manipulation.
    /// Enable this component to trigger threat modification, system will disable after processing.
    /// </summary>
    public struct ThreatModifierEvent : IComponentData, IEnableableComponent
    {
        /// <summary>Which AI entity to modify threat on</summary>
        public Entity TargetAI;
        
        /// <summary>Who the threat applies to (the player/source generating threat)</summary>
        public Entity ThreatSource;
        
        /// <summary>Flat threat to add (taunt typically +1000)</summary>
        public float FlatThreatAdd;
        
        /// <summary>Multiplicative modifier (0 = wipe, 0.5 = halve, 2.0 = double)</summary>
        public float ThreatMultiplier;
        
        /// <summary>Type of modification to apply</summary>
        public ThreatModifierType Type;
    }

    /// <summary>
    /// Types of threat modification operations.
    /// </summary>
    public enum ThreatModifierType : byte
    {
        /// <summary>Add FlatThreatAdd to existing threat</summary>
        Add = 0,
        
        /// <summary>Multiply existing threat by ThreatMultiplier</summary>
        Multiply = 1,
        
        /// <summary>Set threat to exact FlatThreatAdd value</summary>
        Set = 2,
        
        /// <summary>Remove source from threat table entirely</summary>
        Wipe = 3
    }
}
