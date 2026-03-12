using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace DIG.Settings
{
    /// <summary>
    /// Graphics settings tab for the pause menu.
    /// Window mode selection with PlayerPrefs persistence.
    ///
    /// NOTE: Window mode changes only take effect in standalone builds.
    /// Screen APIs have no effect in the Unity Editor Game View.
    ///
    /// macOS: ExclusiveFullScreen is not supported — both Fullscreen and
    /// Borderless map to FullScreenWindow (native macOS fullscreen).
    /// </summary>
    public class GraphicsSettingsPanel : MonoBehaviour
    {
        private const string PREF_WINDOW_MODE = "WindowMode";

        [Header("Window Mode")]
        [SerializeField] private TMP_Dropdown _windowModeDropdown;

        [Header("Windowed Resolution")]
        [Tooltip("Resolution when in Windowed mode")]
        [SerializeField] private int _windowedWidth = 1280;
        [SerializeField] private int _windowedHeight = 720;

        private void Start()
        {
            if (_windowModeDropdown == null)
            {
                Debug.LogWarning("[GraphicsSettings] No dropdown assigned!");
                return;
            }

            _windowModeDropdown.ClearOptions();
            _windowModeDropdown.AddOptions(new List<string>
            {
                "Fullscreen",
                "Windowed"
            });

            int saved = PlayerPrefs.GetInt(PREF_WINDOW_MODE, Screen.fullScreen ? 0 : 1);
            _windowModeDropdown.SetValueWithoutNotify(saved);
            _windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);

            Debug.Log($"[GraphicsSettings] Initialized. fullScreen={Screen.fullScreen}, mode={Screen.fullScreenMode}, saved={saved}");
        }

        private void OnWindowModeChanged(int index)
        {
            Debug.Log($"[GraphicsSettings] Dropdown changed to: {index}");
            StopAllCoroutines();
            StartCoroutine(ApplyWindowModeDelayed(index));
            PlayerPrefs.SetInt(PREF_WINDOW_MODE, index);
            PlayerPrefs.Save();
        }

        private IEnumerator ApplyWindowModeDelayed(int index)
        {
            switch (index)
            {
                case 0: // Fullscreen
                    Debug.Log("[GraphicsSettings] Setting fullScreen = true");
                    Screen.fullScreen = true;
                    break;

                case 1: // Windowed
                    Debug.Log($"[GraphicsSettings] Setting fullScreen = false, then resize to {_windowedWidth}x{_windowedHeight}");
                    Screen.fullScreen = false;
                    yield return null;
                    yield return null;
                    Screen.SetResolution(_windowedWidth, _windowedHeight, false);
                    break;
            }

            // Wait a frame then log the result
            yield return null;
            Debug.Log($"[GraphicsSettings] Result: fullScreen={Screen.fullScreen}, mode={Screen.fullScreenMode}, res={Screen.width}x{Screen.height}");
        }

        private void OnDestroy()
        {
            if (_windowModeDropdown != null)
                _windowModeDropdown.onValueChanged.RemoveListener(OnWindowModeChanged);
        }
    }
}
