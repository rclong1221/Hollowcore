#if UNITY_EDITOR
using UnityEngine;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.6: Interface for RunWorkstation tab modules.
    /// Follows ICombatModule pattern from CombatWorkstation.
    /// EPIC 23.7: Added SetContext for shared data context.
    /// </summary>
    public interface IRunWorkstationModule
    {
        string TabName { get; }
        void OnGUI();
        void OnEnable();
        void OnDisable();

        /// <summary>
        /// Optional: receive shared data context. Called after Build().
        /// Modules that don't need cross-SO data can ignore this.
        /// </summary>
        void SetContext(RogueliteDataContext context) { }
    }
}
#endif
