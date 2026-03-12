using System.Collections.Generic;
using DIG.UI.Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DIG.Accessibility.Visual
{
    /// <summary>
    /// EPIC 18.12: High-contrast mode service.
    /// Swaps UIThemeSO to high-contrast variant and increases outline thickness
    /// on renderers with outline shaders.
    /// Caches outline materials on first pass; invalidates on scene load.
    /// </summary>
    public static class HighContrastMode
    {
        private static bool _enabled;
        private static UIThemeSO _normalTheme;
        private static UIThemeSO _highContrastTheme;

        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static float _originalOutlineWidth = -1f;
        private const float HighContrastOutlineWidth = 3f;

        // Cached list of materials that actually have _OutlineWidth
        private static List<Material> _outlineMaterials;
        private static bool _sceneListenerRegistered;

        public static bool IsEnabled => _enabled;

        /// <summary>
        /// Enable/disable high-contrast mode.
        /// Swaps UI theme and increases world outline thickness.
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;

            // Theme swap via UIToolkitService
            if (UIServices.Screen is UIToolkitService uiService)
            {
                if (enabled)
                {
                    if (_highContrastTheme == null)
                        _highContrastTheme = Resources.Load<UIThemeSO>("Themes/HighContrast");

                    if (_highContrastTheme != null)
                        uiService.SetTheme(_highContrastTheme);
                }
                else
                {
                    if (_normalTheme != null)
                        uiService.SetTheme(_normalTheme);
                }
            }

            ApplyOutlineThickness(enabled);
        }

        /// <summary>Cache the normal theme so we can revert.</summary>
        public static void CacheNormalTheme(UIThemeSO theme)
        {
            _normalTheme = theme;
        }

        /// <summary>Invalidate cached outline materials (call after scene load).</summary>
        public static void InvalidateCache()
        {
            _outlineMaterials = null;
        }

        private static void ApplyOutlineThickness(bool highContrast)
        {
            EnsureSceneListener();

            if (_outlineMaterials == null)
                BuildOutlineMaterialCache();

            foreach (var mat in _outlineMaterials)
            {
                if (mat == null) continue;

                if (highContrast)
                {
                    if (_originalOutlineWidth < 0f)
                        _originalOutlineWidth = mat.GetFloat(OutlineWidthId);
                    mat.SetFloat(OutlineWidthId, HighContrastOutlineWidth);
                }
                else if (_originalOutlineWidth >= 0f)
                {
                    mat.SetFloat(OutlineWidthId, _originalOutlineWidth);
                }
            }
        }

        private static void BuildOutlineMaterialCache()
        {
            _outlineMaterials = new List<Material>(32);
            var seen = new HashSet<int>(); // material instance IDs to avoid duplicates

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                foreach (var mat in rend.sharedMaterials)
                {
                    if (mat == null || !mat.HasProperty(OutlineWidthId)) continue;
                    if (seen.Add(mat.GetInstanceID()))
                        _outlineMaterials.Add(mat);
                }
            }
        }

        private static void EnsureSceneListener()
        {
            if (_sceneListenerRegistered) return;
            _sceneListenerRegistered = true;
            SceneManager.sceneLoaded += (_, _) => InvalidateCache();
        }
    }
}
