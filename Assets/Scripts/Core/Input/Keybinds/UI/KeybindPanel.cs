using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DIG.Core.Input.Keybinds;
using UnityEngine.InputSystem;

namespace DIG.Core.Input.Keybinds.UI
{
    /// <summary>
    /// Main container panel for keybind settings.
    /// Displays all rebindable actions with paradigm filtering.
    /// 
    /// EPIC 15.21 Phase 6: Keybind UI
    /// 
    /// Toggle visibility with Escape key or via code.
    /// Starts hidden by default.
    /// </summary>
    public class KeybindPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _contentParent;
        [SerializeField] private GameObject _rowPrefab;
        [SerializeField] private RebindOverlay _rebindOverlay;
        
        [Header("Category Headers")]
        [SerializeField] private GameObject _categoryHeaderPrefab;
        
        [Header("Paradigm Filter")]
        [SerializeField] private TMP_Dropdown _paradigmDropdown;
        
        [Header("Buttons")]
        [SerializeField] private Button _resetAllButton;
        [SerializeField] private Button _closeButton;
        
        [Header("Panel Root")]
        [SerializeField] private GameObject _panelRoot;
        
        [Header("Toggle Settings")]
        [SerializeField] private bool _startHidden = true;
        [Tooltip("Disable when embedded as a tab inside another menu.")]
        [SerializeField] private bool _enableKeyboardToggle = true;
        
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private int _currentFilterIndex = 0;
        
        private void Awake()
        {
            if (_resetAllButton != null)
                _resetAllButton.onClick.AddListener(OnResetAllClicked);
            
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
            
            if (_paradigmDropdown != null)
            {
                SetupParadigmDropdown();
                _paradigmDropdown.onValueChanged.AddListener(OnParadigmFilterChanged);
            }
            
            // Start hidden
            if (_startHidden && _panelRoot != null)
                _panelRoot.SetActive(false);
        }
        
        private void Update()
        {
            if (!_enableKeyboardToggle) return;

            // Toggle with F1
            if (Keyboard.current != null && Keyboard.current[Key.F1].wasPressedThisFrame)
            {
                Toggle();
            }

            // Toggle with Escape (open if closed, close if open)
            if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
            {
                Toggle();
            }
        }
        
        private void OnEnable()
        {
            RefreshUI();
        }
        
        private void OnDestroy()
        {
            if (_resetAllButton != null)
                _resetAllButton.onClick.RemoveListener(OnResetAllClicked);
            
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Hide);
            
            if (_paradigmDropdown != null)
                _paradigmDropdown.onValueChanged.RemoveListener(OnParadigmFilterChanged);
        }
        
        /// <summary>
        /// Toggle panel visibility.
        /// </summary>
        public void Toggle()
        {
            if (_panelRoot == null) return;
            
            if (_panelRoot.activeSelf)
                Hide();
            else
                Show();
        }
        
        /// <summary>
        /// Show the panel.
        /// </summary>
        public void Show()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
                RefreshUI();
            }
        }
        
        /// <summary>
        /// Hide the panel.
        /// </summary>
        public void Hide()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }
        
        /// <summary>
        /// Returns true if the panel is currently visible.
        /// </summary>
        public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;
        
        private void SetupParadigmDropdown()
        {
            _paradigmDropdown.ClearOptions();
            _paradigmDropdown.AddOptions(new List<string>
            {
                "All Controls",
                "Shooter",
                "MMO/RPG",
                "ARPG"
            });
        }
        
        private void OnParadigmFilterChanged(int index)
        {
            _currentFilterIndex = index;
            RefreshUI();
        }
        
        public void RefreshUI()
        {
            ClearRows();
            
            var actions = KeybindService.GetAllBindableActions();
            string currentCategory = null;
            
            foreach (var action in actions)
            {
                if (!PassesFilter(action))
                    continue;
                
                if (action.Category != currentCategory)
                {
                    currentCategory = action.Category;
                    SpawnCategoryHeader(currentCategory);
                }
                
                SpawnRow(action);
            }
        }
        
        private bool PassesFilter(BindableAction action)
        {
            if (_currentFilterIndex == 0) return true;
            if (_currentFilterIndex == 1) return action.IsAvailableInParadigm(InputParadigm.Shooter);
            if (_currentFilterIndex == 2) return action.IsAvailableInParadigm(InputParadigm.MMO);
            if (_currentFilterIndex == 3) return action.IsAvailableInParadigm(InputParadigm.ARPG);
            return true;
        }
        
        private void SpawnCategoryHeader(string category)
        {
            if (_categoryHeaderPrefab == null || _contentParent == null) return;
            
            var header = Instantiate(_categoryHeaderPrefab, _contentParent);
            header.SetActive(true);
            
            var text = header.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = category.ToUpperInvariant();
            
            _spawnedRows.Add(header);
        }
        
        private void SpawnRow(BindableAction action)
        {
            if (_rowPrefab == null || _contentParent == null) return;
            
            var row = Instantiate(_rowPrefab, _contentParent);
            row.SetActive(true);
            
            var rowComponent = row.GetComponent<KeybindRow>();
            if (rowComponent != null)
                rowComponent.Setup(action, _rebindOverlay);
            
            _spawnedRows.Add(row);
        }
        
        private void ClearRows()
        {
            foreach (var row in _spawnedRows)
            {
                if (row != null)
                    Destroy(row);
            }
            _spawnedRows.Clear();
        }
        
        private void OnResetAllClicked()
        {
            KeybindService.ResetToDefaults();
            RefreshUI();
        }
    }
}
