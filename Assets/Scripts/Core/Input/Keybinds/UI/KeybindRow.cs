using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DIG.Core.Input.Keybinds;

namespace DIG.Core.Input.Keybinds.UI
{
    /// <summary>
    /// UI row representing a single rebindable action.
    /// Shows action name, current binding, and rebind button.
    /// 
    /// EPIC 15.21 Phase 6: Keybind UI
    /// </summary>
    public class KeybindRow : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _actionNameText;
        [SerializeField] private TextMeshProUGUI _bindingText;
        [SerializeField] private Button _rebindButton;
        [SerializeField] private Button _resetButton;
        
        private BindableAction _bindableAction;
        private RebindOverlay _overlay;
        
        /// <summary>
        /// Initialize the row with action data.
        /// </summary>
        public void Setup(BindableAction action, RebindOverlay overlay)
        {
            _bindableAction = action;
            _overlay = overlay;
            
            if (_actionNameText != null)
                _actionNameText.text = action.DisplayName;
            
            RefreshBinding();
            
            if (_rebindButton != null)
                _rebindButton.onClick.AddListener(OnRebindClicked);
            
            if (_resetButton != null)
                _resetButton.onClick.AddListener(OnResetClicked);
        }
        
        private void OnEnable()
        {
            KeybindService.OnBindingChanged += OnBindingChanged;
        }
        
        private void OnDisable()
        {
            KeybindService.OnBindingChanged -= OnBindingChanged;
            
            if (_rebindButton != null)
                _rebindButton.onClick.RemoveListener(OnRebindClicked);
            
            if (_resetButton != null)
                _resetButton.onClick.RemoveListener(OnResetClicked);
        }
        
        private void OnBindingChanged(string actionName)
        {
            // Refresh if our action changed, or if all bindings reset (null)
            if (actionName == null || actionName == _bindableAction?.ActionName)
            {
                RefreshBinding();
            }
        }
        
        /// <summary>
        /// Refresh the displayed binding text.
        /// </summary>
        public void RefreshBinding()
        {
            if (_bindableAction == null || _bindingText == null) return;
            
            string display = KeybindService.GetBindingDisplayString(
                _bindableAction.ActionMap,
                _bindableAction.ActionName,
                _bindableAction.BindingIndex
            );
            
            _bindingText.text = string.IsNullOrEmpty(display) ? "[Not Bound]" : display;
        }
        
        private void OnRebindClicked()
        {
            if (_bindableAction == null) return;
            
            // Show overlay and start rebind
            _overlay?.Show(_bindableAction.DisplayName, () =>
            {
                KeybindService.CancelRebind();
            });
            
            KeybindService.StartRebind(_bindableAction, success =>
            {
                _overlay?.Hide();
            });
        }
        
        private void OnResetClicked()
        {
            // Individual reset is complex - for now just refresh
            RefreshBinding();
        }
    }
}
