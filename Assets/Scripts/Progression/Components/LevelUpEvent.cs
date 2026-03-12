using Unity.Entities;
using Unity.NetCode;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Transient level-up event. IEnableableComponent baked disabled.
    /// LevelUpSystem enables it on level-up; consuming systems disable it.
    /// Zero structural change to toggle (no ECB overhead).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct LevelUpEvent : IComponentData, IEnableableComponent
    {
        [GhostField] public int NewLevel;
        [GhostField] public int PreviousLevel;
    }
}
