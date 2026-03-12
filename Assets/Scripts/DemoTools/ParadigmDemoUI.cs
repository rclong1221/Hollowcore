#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using DIG.CameraSystem.Cinemachine;
using DIG.Core.Input;

namespace DIG.DemoTools
{
    /// <summary>
    /// Simple UI panel for switching camera modes and paradigms at runtime.
    /// Change the Selected Mode in Inspector during Play mode to switch.
    /// 
    /// EPIC 15.20 - Demo Mode Switcher
    /// </summary>
    public class ParadigmDemoUI : MonoBehaviour
    {
        public enum DemoMode
        {
            Shooter,
            MMO,
            ARPG_Classic,
            ARPG_Hybrid,
            MOBA,
            TwinStick
        }
        
        [Header("Mode Selection (Change during Play)")]
        [SerializeField] private DemoMode _selectedMode = DemoMode.Shooter;
        
        [Header("UI Settings")]
        [SerializeField] private bool _showUIPanel = true;
        [SerializeField] private Vector2 _panelPosition = new Vector2(10, 10);
        
        private DemoMode _lastMode;
        
        // Auto-created UI
        private Canvas _canvas;
        private GameObject _panel;
        private TextMeshProUGUI _statusLabel;
        
        private void Start()
        {
            _lastMode = _selectedMode;
            
            if (_showUIPanel)
            {
                CreateUI();
            }
            
            // Apply initial mode
            ApplyMode(_selectedMode);
        }
        
        private void Update()
        {
            // Detect Inspector changes during play mode
            if (_selectedMode != _lastMode)
            {
                _lastMode = _selectedMode;
                ApplyMode(_selectedMode);
            }
        }
        
        private void ApplyMode(DemoMode mode)
        {
            switch (mode)
            {
                case DemoMode.Shooter:
                    SwitchCameraAndParadigm(CinemachineCameraMode.ThirdPerson, "Profile_Shooter");
                    break;
                case DemoMode.MMO:
                    SwitchCameraAndParadigm(CinemachineCameraMode.ThirdPerson, "Profile_MMO");
                    break;
                case DemoMode.ARPG_Classic:
                    SwitchCameraAndParadigm(CinemachineCameraMode.Isometric, "Profile_ARPG_Classic");
                    break;
                case DemoMode.ARPG_Hybrid:
                    SwitchCameraAndParadigm(CinemachineCameraMode.Isometric, "Profile_ARPG_Hybrid");
                    break;
                case DemoMode.MOBA:
                    SwitchCameraAndParadigm(CinemachineCameraMode.Isometric, "Profile_MOBA");
                    break;
                case DemoMode.TwinStick:
                    SwitchCameraAndParadigm(CinemachineCameraMode.Isometric, "Profile_TwinStick");
                    break;
            }
            
            UpdateStatus();
        }
        
        private void CreateUI()
        {
            // Ensure EventSystem exists for button clicks
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<InputSystemUIInputModule>(); // Use new Input System
            }
            
            // Create Canvas
            var canvasGO = new GameObject("ParadigmDemoCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Create Panel
            _panel = new GameObject("Panel");
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(_panelPosition.x, -_panelPosition.y);
            panelRect.sizeDelta = new Vector2(220, 80);
            
            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);
            
            var layout = _panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            // Title
            CreateLabel("Paradigm Demo", 16, FontStyle.Bold);
            CreateLabel("Change mode in Inspector", 11, FontStyle.Italic);
            _statusLabel = CreateLabel("Current: Loading...", 12, FontStyle.Normal);
            
            UpdateStatus();
        }
        
        private TextMeshProUGUI CreateLabel(string text, int fontSize, FontStyle style)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(_panel.transform, false);
            
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 20);
            
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : 
                           style == FontStyle.Italic ? FontStyles.Italic : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            return tmp;
        }
        
        private void UpdateStatus()
        {
            if (_statusLabel == null) return;
            
            var paradigm = ParadigmStateMachine.Instance?.ActiveProfile;
            var camera = CinemachineCameraController.Instance?.CurrentCameraMode;
            
            _statusLabel.text = $"Current: {paradigm?.displayName ?? "None"}\nCamera: {camera}";
        }
        
        private void SwitchCameraAndParadigm(CinemachineCameraMode cameraMode, string profileName)
        {
            Debug.Log($"[ParadigmDemoUI] === SwitchCameraAndParadigm START: profile='{profileName}', camera={cameraMode} ===");
            
            // Switch camera first
            if (CinemachineCameraController.HasInstance)
            {
                CinemachineCameraController.Instance.SetCameraMode(cameraMode);
                Debug.Log($"[ParadigmDemoUI] Camera switched to {cameraMode}");
            }
            else
            {
                Debug.LogWarning("[ParadigmDemoUI] No CinemachineCameraController in scene!");
            }
            
            // Then switch paradigm by profile name
            Debug.Log($"[ParadigmDemoUI] ParadigmStateMachine.Instance = {(ParadigmStateMachine.Instance != null ? "EXISTS" : "NULL")}");
            
            if (ParadigmStateMachine.Instance != null)
            {
                Debug.Log($"[ParadigmDemoUI] Calling TrySetParadigmByName('{profileName}')...");
                bool success = ParadigmStateMachine.Instance.TrySetParadigmByName(profileName);
                Debug.Log($"[ParadigmDemoUI] Paradigm switch to '{profileName}': {(success ? "SUCCESS" : "FAILED")}");
                
                // Debug: Check cursor state after switch
                var profile = ParadigmStateMachine.Instance.ActiveProfile;
                if (profile != null)
                {
                    Debug.Log($"[ParadigmDemoUI] Active profile: {profile.displayName}, cursorFreeByDefault={profile.cursorFreeByDefault}");
                }
                
                if (CursorController.Instance != null)
                {
                    Debug.Log($"[ParadigmDemoUI] CursorController.IsCursorFree={CursorController.Instance.IsCursorFree}");
                }
            }
            else
            {
                Debug.LogWarning("[ParadigmDemoUI] No ParadigmStateMachine in scene!");
            }
            
            Debug.Log($"[ParadigmDemoUI] === SwitchCameraAndParadigm END ===");
        }
    }
}
#endif
