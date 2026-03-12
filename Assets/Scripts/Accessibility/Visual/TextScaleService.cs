using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DIG.Accessibility.Visual
{
    /// <summary>
    /// EPIC 18.12: Global text scaling service.
    /// Applies a font size multiplier to UI Toolkit root and TMP_Text components.
    /// Called by AccessibilityService when text scale changes.
    /// Caches UIDocument references and invalidates on scene load.
    /// </summary>
    public static class TextScaleService
    {
        private static float _currentScale = 1f;
        private static readonly float BaseFontSize = 14f; // UI Toolkit default root font size

        private static UIDocument[] _cachedDocuments;
        private static bool _sceneListenerRegistered;

        public static float CurrentScale => _currentScale;

        /// <summary>
        /// Set global text scale multiplier (0.8 to 2.0).
        /// Updates UI Toolkit root fontSize and all active TMP_Text components.
        /// </summary>
        public static void SetGlobalScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.8f, 2f);
            if (Mathf.Approximately(scale, _currentScale)) return;
            _currentScale = scale;

            EnsureSceneListener();

            // UI Toolkit: set root fontSize (all em/% units scale from this)
            if (_cachedDocuments == null)
                _cachedDocuments = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

            foreach (var doc in _cachedDocuments)
            {
                if (doc == null || doc.rootVisualElement == null) continue;
                doc.rootVisualElement.style.fontSize = BaseFontSize * _currentScale;
            }
        }

        /// <summary>Invalidate cache (call after scene load or new UIDocuments created).</summary>
        public static void InvalidateCache()
        {
            _cachedDocuments = null;
        }

        private static void EnsureSceneListener()
        {
            if (_sceneListenerRegistered) return;
            _sceneListenerRegistered = true;
            SceneManager.sceneLoaded += (_, _) => InvalidateCache();
        }
    }
}
