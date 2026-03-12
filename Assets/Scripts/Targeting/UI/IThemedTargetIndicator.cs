using Unity.Entities;
using Unity.Mathematics;
using DIG.Targeting.Theming;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// Extended interface for themed target indicators.
    /// Receives full IndicatorThemeContext for conditional styling.
    /// Use this for 3rd-party UI adapters that need theming.
    /// </summary>
    public interface IThemedTargetIndicator : ITargetIndicator
    {
        /// <summary>
        /// Update indicator with full theme context.
        /// Includes faction, damage type, hit type, etc.
        /// </summary>
        void UpdateIndicatorThemed(IndicatorThemeContext context);
        
        /// <summary>
        /// Called when theme profile changes.
        /// Indicator should refresh its styling.
        /// </summary>
        void OnThemeChanged(IndicatorThemeData themeData);
    }
}
