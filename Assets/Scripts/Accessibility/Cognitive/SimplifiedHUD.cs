using System.Collections.Generic;
using DIG.UI.Core.Navigation;
using DIG.UI.Core.Services;
using UnityEngine.UIElements;

namespace DIG.Accessibility.Cognitive
{
    /// <summary>
    /// EPIC 18.12: Simplified HUD mode for cognitive accessibility.
    /// Hides non-essential HUD elements by setting display=none on registered element names.
    /// Other systems register their non-essential elements via RegisterNonEssential().
    /// </summary>
    public static class SimplifiedHUD
    {
        private static bool _enabled;
        private static readonly HashSet<string> _nonEssentialElements = new(16)
        {
            // Default non-essential elements (can be expanded by other systems)
            "minimap-decorations",
            "xp-bar-particles",
            "damage-shake-overlay",
            "pulse-indicator",
            "combo-counter",
            "kill-feed",
            "ambient-effects-overlay"
        };

        public static bool IsEnabled => _enabled;

        /// <summary>Register a HUD element name as non-essential (hidden in simplified mode).</summary>
        public static void RegisterNonEssential(string elementName)
        {
            _nonEssentialElements.Add(elementName);
            if (_enabled) HideElement(elementName);
        }

        /// <summary>Unregister a HUD element name.</summary>
        public static void UnregisterNonEssential(string elementName)
        {
            _nonEssentialElements.Remove(elementName);
        }

        /// <summary>Enable/disable simplified HUD mode.</summary>
        public static void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;

            if (!(UIServices.Screen is UIToolkitService uiService)) return;

            var hudRoot = uiService.GetLayerRoot(UILayer.HUD);
            if (hudRoot == null) return;

            foreach (var elementName in _nonEssentialElements)
            {
                var element = hudRoot.Q(elementName);
                if (element == null) continue;

                element.style.display = enabled ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        /// <summary>Refresh visibility (call after HUD rebuild).</summary>
        public static void Refresh()
        {
            if (!_enabled) return;
            SetEnabled(false);
            SetEnabled(true);
        }

        private static void HideElement(string elementName)
        {
            if (!(UIServices.Screen is UIToolkitService uiService)) return;
            var hudRoot = uiService.GetLayerRoot(UILayer.HUD);
            var element = hudRoot?.Q(elementName);
            if (element != null)
                element.style.display = DisplayStyle.None;
        }
    }
}
