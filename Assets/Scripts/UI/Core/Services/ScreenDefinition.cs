using System;
using DIG.UI.Core.Navigation;
using UnityEngine;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Serializable definition for a UI screen.
    /// Stored in ScreenManifestSO. Describes identity, layer, asset paths,
    /// transition profiles, pooling config, and focus behavior.
    /// </summary>
    [Serializable]
    public class ScreenDefinition
    {
        [Tooltip("Unique identifier for this screen. Must be unique across the manifest.")]
        public string ScreenId;

        [Tooltip("Which UI layer this screen occupies.")]
        public UILayer Layer;

        [Tooltip("Path to VisualTreeAsset under Resources/ (without .uxml extension).")]
        public string UXMLPath;

        [Tooltip("Optional path to StyleSheet under Resources/ (without .uss extension).")]
        public string USSPath;

        [Tooltip("Transition profile when this screen opens. Falls back to manifest default if null.")]
        public TransitionProfileSO OpenTransition;

        [Tooltip("Transition profile when this screen closes. Falls back to manifest default if null.")]
        public TransitionProfileSO CloseTransition;

        [Tooltip("If true, the screen's VisualElement tree is pooled on close rather than destroyed.")]
        public bool Poolable = true;

        [Tooltip("If true, blocks input to screens below when open (modal behavior).")]
        public bool BlocksInput;

        [Tooltip("USS name of the element to auto-focus when the screen opens (for gamepad/keyboard navigation).")]
        public string InitialFocusElement;
    }
}
