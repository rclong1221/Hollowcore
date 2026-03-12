using Unity.Entities;
using Unity.NetCode;

namespace DIG.Core.Input
{
    /// <summary>
    /// Replicated input scheme state. Server needs this to correctly interpret
    /// aim direction (camera-forward vs cursor-projected).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InputSchemeState : IComponentData
    {
        /// <summary>Active input scheme for this player.</summary>
        [GhostField] public InputScheme ActiveScheme;

        /// <summary>True when hybrid modifier key is held (HybridToggle only).</summary>
        [GhostField] public bool IsTemporaryCursorActive;
    }
}
