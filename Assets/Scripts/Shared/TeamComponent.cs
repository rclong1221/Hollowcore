using Unity.Entities;

namespace DIG.Shared
{
    /// <summary>
    /// ECS component identifying which team an entity belongs to.
    /// Used for friend/foe determination in MOBA attack-move and NPC AI.
    ///
    /// Team 0 = neutral/unaffiliated (never hostile).
    /// Non-zero teams are hostile to each other if different.
    ///
    /// EPIC 15.20 Phase 4a
    /// </summary>
    public struct TeamComponent : IComponentData
    {
        /// <summary>Team identifier. 0 = neutral, 1+ = team IDs.</summary>
        public byte TeamId;

        /// <summary>
        /// Returns true if two teams are hostile to each other.
        /// Neutral (0) is never hostile to anything.
        /// </summary>
        public static bool AreHostile(byte teamA, byte teamB)
        {
            if (teamA == 0 || teamB == 0) return false;
            return teamA != teamB;
        }
    }
}
