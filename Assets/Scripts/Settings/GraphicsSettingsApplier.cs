using UnityEngine;

namespace DIG.Settings
{
    /// <summary>
    /// Applies saved graphics settings on game launch.
    /// Simplified to use Screen.fullScreen which is the most reliable
    /// cross-platform API for toggling fullscreen/windowed.
    /// </summary>
    public static class GraphicsSettingsApplier
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplySavedSettings()
        {
            int mode = PlayerPrefs.GetInt("WindowMode", -1);
            Debug.Log($"[GraphicsSettingsApplier] Saved pref: {mode}, current fullScreen: {Screen.fullScreen}");

            if (mode < 0) return;

            switch (mode)
            {
                case 0: // Fullscreen
                    Screen.fullScreen = true;
                    break;
                case 1: // Windowed
                    Screen.fullScreen = false;
                    Screen.SetResolution(1280, 720, false);
                    break;
            }
        }
    }
}
