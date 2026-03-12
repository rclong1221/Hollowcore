using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Stub level-up popup MonoBehaviour.
    /// Shows "LEVEL UP!" notification with new level and stat points awarded.
    /// Replace with full UI implementation (animation, particle effects, etc.).
    /// </summary>
    public class LevelUpPopupView : MonoBehaviour, ILevelUpPopupProvider
    {
        private void OnEnable() => ProgressionUIRegistry.RegisterLevelUpPopup(this);
        private void OnDisable() => ProgressionUIRegistry.UnregisterLevelUpPopup(this);

        public void ShowLevelUp(int newLevel, int previousLevel, int statPointsAwarded)
        {
            Debug.Log($"[LevelUp] Level {previousLevel} -> {newLevel}! +{statPointsAwarded} stat points");
        }
    }
}
