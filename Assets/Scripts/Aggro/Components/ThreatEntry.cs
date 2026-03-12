using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.19: Buffer element for per-target threat tracking on AI entities.
    /// Each entry represents one source of threat (player, turret, etc.)
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ThreatEntry : IBufferElementData
    {
        /// <summary>Entity generating threat (player, turret, pet, etc.)</summary>
        public Entity SourceEntity;
        
        /// <summary>Cumulative threat value (damage * multiplier + modifiers)</summary>
        public float ThreatValue;
        
        /// <summary>Last world position seen (for memory/search behavior when LOS lost)</summary>
        public float3 LastKnownPosition;
        
        /// <summary>Time since this source was visible (0 = currently visible)</summary>
        public float TimeSinceVisible;
        
        /// <summary>Whether this source is currently in SeenTargetElement buffer</summary>
        public bool IsCurrentlyVisible;

        /// <summary>EPIC 15.33: Bitmask of all systems that contributed threat for this source.</summary>
        public ThreatSourceFlags SourceFlags;

        /// <summary>EPIC 15.33: Threat accumulated specifically from damage (for analytics/UI).</summary>
        public float DamageThreat;
    }
}
