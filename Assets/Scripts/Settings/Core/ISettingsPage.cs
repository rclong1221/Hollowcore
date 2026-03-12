using UnityEngine.UIElements;

namespace DIG.Settings.Core
{
    /// <summary>
    /// EPIC 18.2: Plugin interface for a settings page (tab).
    /// Each implementation bridges to one or more existing settings managers.
    /// </summary>
    public interface ISettingsPage
    {
        /// <summary>Unique identifier (e.g., "Graphics", "Audio").</summary>
        string PageId { get; }

        /// <summary>Display name shown on the tab button.</summary>
        string DisplayName { get; }

        /// <summary>Tab ordering (lower = leftmost).</summary>
        int SortOrder { get; }

        /// <summary>Build the page UI into the given container.</summary>
        void BuildUI(VisualElement container);

        /// <summary>Called when this tab becomes visible — refresh displayed values.</summary>
        void OnPageShown();

        /// <summary>Capture current manager state for later revert.</summary>
        void TakeSnapshot();

        /// <summary>Persist changes to PlayerPrefs / managers.</summary>
        void ApplyChanges();

        /// <summary>Restore snapshot values (discard unsaved changes).</summary>
        void RevertChanges();

        /// <summary>Reset all settings on this page to factory defaults.</summary>
        void ResetToDefaults();

        /// <summary>True if any value differs from the last snapshot.</summary>
        bool HasUnsavedChanges { get; }
    }
}
