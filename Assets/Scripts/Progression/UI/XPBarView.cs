using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Stub XP bar UI MonoBehaviour.
    /// Displays current level, XP progress, unspent stat points, rested status.
    /// Replace with full UI implementation.
    /// </summary>
    public class XPBarView : MonoBehaviour, IXPBarProvider
    {
        private int _lastLevel;
        private float _displayPercent;

        private void OnEnable() => ProgressionUIRegistry.RegisterXPBar(this);
        private void OnDisable() => ProgressionUIRegistry.UnregisterXPBar(this);

        public void UpdateXPBar(int level, int currentXP, int xpToNextLevel, float percent, int unspentStatPoints, float restedXP)
        {
            _lastLevel = level;
            _displayPercent = percent;
        }
    }
}
