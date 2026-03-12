using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    // Runtime state for prone/crawl
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ProneStateComponent : IComponentData
    {
        [GhostField] public byte IsProne; // 0 = not prone, 1 = prone
        [GhostField] public byte IsCrawling; // 0/1
        [GhostField] public float TransitionTimer; // seconds remaining for transition animation
        [GhostField] public float TransitionDuration; // configured duration for transitions
        [GhostField] public float SpeedMultiplier; // movement multiplier while prone/crawling
    }
}
