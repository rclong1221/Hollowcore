using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DIG.Core.Input.Keybinds.UI
{
    /// <summary>
    /// Modal overlay shown during interactive rebinding.
    /// Displays "Press a key..." prompt with cancel option.
    /// 
    /// EPIC 15.21 Phase 6: Keybind UI
    /// </summary>
    public class RebindOverlay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _overlayRoot;
        [SerializeField] private TextMeshProUGUI _promptText;
        [SerializeField] private TextMeshProUGUI _actionNameText;
        [SerializeField] private Button _cancelButton;
        
        [Header("Settings")]
        [SerializeField] private string _promptFormat = "Press a key for \"{0}\"...";

        private Action _onCancel;
        
        private void Awake()
        {
            if (_overlayRoot != null)
                _overlayRoot.SetActive(false);
            
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);
        }
        
        private void OnDestroy()
        {
            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(OnCancelClicked);
        }
        
        /// <summary>
        /// Show the overlay with the action being rebound.
        /// </summary>
        public void Show(string actionDisplayName, Action onCancel = null)
        {
            _onCancel = onCancel;
            
            if (_actionNameText != null)
                _actionNameText.text = actionDisplayName;
            
            if (_promptText != null)
                _promptText.text = string.Format(_promptFormat, actionDisplayName);
            
            if (_overlayRoot != null)
                _overlayRoot.SetActive(true);
        }
        
        /// <summary>
        /// Hide the overlay.
        /// </summary>
        public void Hide()
        {
            if (_overlayRoot != null)
                _overlayRoot.SetActive(false);
            
            _onCancel = null;
        }
        
        /// <summary>
        /// Returns true if the overlay is currently visible.
        /// </summary>
        public bool IsVisible => _overlayRoot != null && _overlayRoot.activeSelf;
        
        private void OnCancelClicked()
        {
            _onCancel?.Invoke();
            Hide();
        }
    }
}
