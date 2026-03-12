using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Designer-facing party configuration.
    /// Place at Resources/PartyConfig for PartyBootstrapSystem to load.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Party/Party Config")]
    public class PartyConfigSO : ScriptableObject
    {
        [Header("Party Size")]
        [Range(2, 6)] public int MaxPartySize = 6;

        [Header("Invites")]
        [Tooltip("Seconds before pending invite expires.")]
        public float InviteTimeoutSeconds = 60f;

        [Header("XP Sharing")]
        [Tooltip("Distance (units) within which members share XP.")]
        public float XPShareRange = 50f;

        [Tooltip("Per-member XP bonus (e.g., 0.10 = +10% per extra member).")]
        [Range(0f, 1f)] public float XPShareBonusPerMember = 0.10f;

        [Header("Loot")]
        [Tooltip("Distance within which members are eligible for loot.")]
        public float LootRange = 60f;

        [Tooltip("Seconds before designated loot reverts to FFA.")]
        public float LootDesignationTimeoutSeconds = 30f;

        [Tooltip("Seconds for NeedGreed vote before auto-Pass.")]
        public float NeedGreedVoteTimeoutSeconds = 15f;

        [Tooltip("Fraction of gold split equally (1.0 = full split).")]
        [Range(0f, 1f)] public float LootGoldSplitPercent = 1f;

        [Header("Kill Credit")]
        [Tooltip("Distance for party kill credit distribution.")]
        public float KillCreditRange = 50f;

        [Header("Options")]
        [Tooltip("Allow non-leaders to request loot mode change.")]
        public bool AllowLootModeVote = false;

        [Tooltip("Initial loot mode when party is formed.")]
        public LootMode DefaultLootMode = LootMode.FreeForAll;
    }
}
