using Unity.Entities;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Zone type for current ZoneTransition. Set by zone system (23.3) or game logic.
    /// Default Combat = reward choice. Shop = shop inventory. Event = event presentation.
    /// </summary>
    public enum ZoneTransitionType : byte
    {
        Combat = 0,     // Reward choice (choose-N-of-pool)
        Shop = 1,
        Event = 2
    }

    /// <summary>
    /// EPIC 23.5: Singleton indicating current zone transition type. Optional.
    /// When absent, ChoiceGenerationSystem assumes Combat (reward choice).
    /// </summary>
    public struct ZoneContextSingleton : IComponentData
    {
        public ZoneTransitionType CurrentType;
    }
}
