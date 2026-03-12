using System.Collections.Generic;
using UnityEngine;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Central registry of all UI screen definitions.
    /// Create via Assets > Create > DIG/UI/Screen Manifest.
    /// Place in a Resources/ folder (e.g., Resources/ScreenManifest) for runtime loading.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/UI/Screen Manifest")]
    public class ScreenManifestSO : ScriptableObject
    {
        [SerializeField] private List<ScreenDefinition> _screens = new();

        [Header("Default Transitions")]
        [Tooltip("Fallback open transition when a ScreenDefinition's OpenTransition is null.")]
        public TransitionProfileSO DefaultOpenTransition;

        [Tooltip("Fallback close transition when a ScreenDefinition's CloseTransition is null.")]
        public TransitionProfileSO DefaultCloseTransition;

        [Header("Theme")]
        [Tooltip("Theme applied at startup.")]
        public UIThemeSO DefaultTheme;

        // Lazy lookup cache
        private Dictionary<string, ScreenDefinition> _lookup;

        public IReadOnlyList<ScreenDefinition> AllScreens => _screens;

        public bool TryGetScreen(string screenId, out ScreenDefinition def)
        {
            EnsureLookup();
            return _lookup.TryGetValue(screenId, out def);
        }

        public ScreenDefinition GetScreen(string screenId)
        {
            EnsureLookup();
            _lookup.TryGetValue(screenId, out var def);
            return def;
        }

        /// <summary>
        /// Resolves the open transition for a screen definition,
        /// falling back to the manifest default.
        /// </summary>
        public TransitionProfileSO ResolveOpenTransition(ScreenDefinition def)
        {
            return def?.OpenTransition != null ? def.OpenTransition : DefaultOpenTransition;
        }

        /// <summary>
        /// Resolves the close transition for a screen definition,
        /// falling back to the manifest default.
        /// </summary>
        public TransitionProfileSO ResolveCloseTransition(ScreenDefinition def)
        {
            return def?.CloseTransition != null ? def.CloseTransition : DefaultCloseTransition;
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, ScreenDefinition>(_screens.Count);
            foreach (var s in _screens)
            {
                if (string.IsNullOrEmpty(s?.ScreenId)) continue;
                if (_lookup.ContainsKey(s.ScreenId))
                {
                    Debug.LogWarning($"[ScreenManifest] Duplicate ScreenId '{s.ScreenId}' — only the first entry is used.");
                    continue;
                }
                _lookup[s.ScreenId] = s;
            }
        }

        private void OnValidate() => _lookup = null;
        private void OnEnable() => _lookup = null;
    }
}
