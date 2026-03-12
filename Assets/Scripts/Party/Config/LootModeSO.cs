using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Per-loot-mode descriptor for UI and rules.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Party/Loot Mode")]
    public class LootModeSO : ScriptableObject
    {
        public string ModeName;
        public LootMode ModeType;
        [TextArea(2, 4)] public string Description;
        public string IconPath;
        [Tooltip("Minimum party size required for this mode.")]
        public int RequiredMinMembers = 2;
        public bool AllowGoldSplit = true;
        public bool AllowCurrencyLoot = true;
    }
}
