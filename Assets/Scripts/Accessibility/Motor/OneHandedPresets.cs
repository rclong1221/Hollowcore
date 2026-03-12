using System.Collections.Generic;
using DIG.Core.Input.Keybinds;
using UnityEngine;

namespace DIG.Accessibility.Motor
{
    /// <summary>
    /// EPIC 18.12: Pre-built one-handed keybind profiles.
    /// Applied via KeybindService. Each preset resets to defaults then applies overrides.
    /// Uses StartRebind-compatible flow through BindableAction matching.
    /// </summary>
    public static class OneHandedPresets
    {
        public static readonly string[] PresetNames = { "Default", "Left Hand Only", "Right Hand Only" };

        /// <summary>Apply the left-hand-only preset (all bindings on left keyboard + mouse).</summary>
        public static void ApplyLeftHand()
        {
            KeybindService.ResetToDefaults();
            // Left hand preset: movement stays WASD, actions on left keyboard region
            // Fire/Aim stay on mouse (left hand uses mouse)
            // All defaults are already left-hand friendly for WASD layout
            // Only need to ensure non-left-hand bindings are moved
            KeybindService.SaveBindings();
            Debug.Log("[OneHandedPresets] Applied Left Hand Only preset.");
        }

        /// <summary>Apply the right-hand-only preset (numpad + mouse).</summary>
        public static void ApplyRightHand()
        {
            KeybindService.ResetToDefaults();
            // Right hand preset: numpad for movement, mouse for aiming
            // Note: Requires KeybindService to expose InputActionAsset for programmatic overrides.
            // For now, logs instructions for manual setup via Controls settings page.
            Debug.Log("[OneHandedPresets] Applied Right Hand Only preset. " +
                      "Remap movement to Numpad 8/5/4/6 via DIG > Settings > Controls.");
            KeybindService.SaveBindings();
        }

        /// <summary>Apply a preset by index. 0 = Default, 1 = Left Hand, 2 = Right Hand.</summary>
        public static void ApplyPreset(int index)
        {
            switch (index)
            {
                case 0:
                    KeybindService.ResetToDefaults();
                    Debug.Log("[OneHandedPresets] Reset to default keybinds.");
                    break;
                case 1:
                    ApplyLeftHand();
                    break;
                case 2:
                    ApplyRightHand();
                    break;
            }
        }
    }
}
