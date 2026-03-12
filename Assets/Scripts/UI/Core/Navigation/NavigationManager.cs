using System;
using System.Collections.Generic;
using DIG.UI.Core.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Core.Navigation
{
    /// <summary>
    /// Screen layer types for navigation.
    /// Screens are full replacements, Modals stack on top.
    /// </summary>
    public enum UILayer
    {
        /// <summary>Full screen replacement. Hides previous screens.</summary>
        Screen,
        /// <summary>Popup that stacks on top. Previous content remains visible.</summary>
        Modal,
        /// <summary>Always-visible HUD elements.</summary>
        HUD,
        /// <summary>Tooltip layer, above everything.</summary>
        Tooltip
    }
    
    /// <summary>
    /// Navigation entry representing a screen/modal in the stack.
    /// </summary>
    public class NavigationEntry
    {
        public string Id { get; set; }
        public UILayer Layer { get; set; }
        public VisualElement Root { get; set; }
        public Action OnShow { get; set; }
        public Action OnHide { get; set; }
        public Action OnDestroy { get; set; }
        public bool IsVisible { get; set; }
        
        /// <summary>
        /// Optional data passed when navigating to this screen.
        /// </summary>
        public object NavigationData { get; set; }

        /// <summary>
        /// EPIC 18.1: Associated ScreenHandle when managed by UIToolkitService.
        /// </summary>
        public ScreenHandle Handle { get; set; }
    }
    
    /// <summary>
    /// Manages UI navigation with history-based stack.
    /// Supports Push, Pop, and Back operations.
    /// Ensures Escape/B-Button behaves correctly.
    /// 
    /// EPIC 15.8: Core MVVM Framework - Navigation Stack
    /// 
    /// Usage:
    ///   NavigationManager.Push("Inventory", inventoryElement, UILayer.Screen);
    ///   NavigationManager.Pop(); // or Press Escape
    ///   NavigationManager.NavigateTo("Settings", settingsElement, UILayer.Modal);
    /// </summary>
    public class NavigationManager : MonoBehaviour
    {
        private static NavigationManager _instance;
        public static NavigationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("NavigationManager");
                    _instance = go.AddComponent<NavigationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        [Header("Configuration")]
        [SerializeField] private bool _debugLogging = false;
        [SerializeField] private bool _closeOnEscape = true;
        
        private readonly Stack<NavigationEntry> _screenStack = new();
        private readonly Stack<NavigationEntry> _modalStack = new();
        private readonly Dictionary<string, NavigationEntry> _registeredScreens = new();
        
        /// <summary>Event fired when navigation occurs.</summary>
        public event Action<NavigationEntry> OnNavigated;
        
        /// <summary>Event fired when a screen/modal is popped.</summary>
        public event Action<NavigationEntry> OnPopped;
        
        /// <summary>Event fired when back is pressed but nothing to pop.</summary>
        public event Action OnBackPressedEmpty;
        
        /// <summary>The currently active screen (top of screen stack).</summary>
        public NavigationEntry CurrentScreen => _screenStack.Count > 0 ? _screenStack.Peek() : null;
        
        /// <summary>The currently active modal (top of modal stack).</summary>
        public NavigationEntry CurrentModal => _modalStack.Count > 0 ? _modalStack.Peek() : null;
        
        /// <summary>Whether any modal is currently open.</summary>
        public bool HasOpenModal => _modalStack.Count > 0;
        
        /// <summary>Total items in navigation (screens + modals).</summary>
        public int StackDepth => _screenStack.Count + _modalStack.Count;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void Update()
        {
            // EPIC 15.21: Migrated from legacy Input.GetKeyDown to Input System
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (_closeOnEscape && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                HandleBack();
            }
        }
        
        /// <summary>
        /// Registers a screen for later navigation by ID.
        /// </summary>
        public void Register(string id, VisualElement root, UILayer layer, 
            Action onShow = null, Action onHide = null, Action onDestroy = null)
        {
            var entry = new NavigationEntry
            {
                Id = id,
                Root = root,
                Layer = layer,
                OnShow = onShow,
                OnHide = onHide,
                OnDestroy = onDestroy,
                IsVisible = false
            };
            
            _registeredScreens[id] = entry;
            
            // Start hidden
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }
            
            Log($"Registered: {id} ({layer})");
        }
        
        /// <summary>
        /// Navigates to a registered screen by ID.
        /// </summary>
        public bool NavigateTo(string id, object navigationData = null)
        {
            if (!_registeredScreens.TryGetValue(id, out var entry))
            {
                Debug.LogError($"[Navigation] Screen '{id}' not registered");
                return false;
            }
            
            entry.NavigationData = navigationData;
            return Push(entry);
        }
        
        /// <summary>
        /// Pushes a new screen/modal onto the appropriate stack.
        /// </summary>
        public bool Push(string id, VisualElement root, UILayer layer, 
            Action onShow = null, Action onHide = null, object navigationData = null)
        {
            var entry = new NavigationEntry
            {
                Id = id,
                Root = root,
                Layer = layer,
                OnShow = onShow,
                OnHide = onHide,
                NavigationData = navigationData
            };
            
            return Push(entry);
        }
        
        /// <summary>
        /// Pushes a NavigationEntry onto the appropriate stack.
        /// </summary>
        public bool Push(NavigationEntry entry)
        {
            if (entry == null)
            {
                Debug.LogError("[Navigation] Cannot push null entry");
                return false;
            }
            
            var stack = entry.Layer == UILayer.Modal ? _modalStack : _screenStack;
            
            // Hide current entry if it's a screen
            if (entry.Layer == UILayer.Screen && _screenStack.Count > 0)
            {
                var current = _screenStack.Peek();
                HideEntry(current);
            }
            
            stack.Push(entry);
            ShowEntry(entry);
            
            Log($"Pushed: {entry.Id} ({entry.Layer}) - Stack depth: {StackDepth}");
            OnNavigated?.Invoke(entry);
            
            return true;
        }
        
        /// <summary>
        /// Pops the top modal if any, otherwise pops the top screen.
        /// </summary>
        public NavigationEntry Pop()
        {
            // Modals first
            if (_modalStack.Count > 0)
            {
                var modal = _modalStack.Pop();
                HideEntry(modal);
                modal.OnDestroy?.Invoke();
                
                Log($"Popped modal: {modal.Id} - Stack depth: {StackDepth}");
                OnPopped?.Invoke(modal);
                
                return modal;
            }
            
            // Then screens
            if (_screenStack.Count > 1) // Keep at least one screen
            {
                var screen = _screenStack.Pop();
                HideEntry(screen);
                screen.OnDestroy?.Invoke();
                
                // Show previous screen
                if (_screenStack.Count > 0)
                {
                    ShowEntry(_screenStack.Peek());
                }
                
                Log($"Popped screen: {screen.Id} - Stack depth: {StackDepth}");
                OnPopped?.Invoke(screen);
                
                return screen;
            }
            
            Log("Pop called but no items to pop (root screen protected)");
            return null;
        }
        
        /// <summary>
        /// Handles back button/escape key press.
        /// </summary>
        public void HandleBack()
        {
            if (_modalStack.Count > 0 || _screenStack.Count > 1)
            {
                Pop();
            }
            else
            {
                Log("Back pressed but at root");
                OnBackPressedEmpty?.Invoke();
            }
        }
        
        /// <summary>
        /// Pops all modals.
        /// </summary>
        public void PopAllModals()
        {
            while (_modalStack.Count > 0)
            {
                Pop();
            }
        }
        
        /// <summary>
        /// Clears all navigation and optionally destroys entries.
        /// </summary>
        public void Clear(bool destroyEntries = true)
        {
            if (destroyEntries)
            {
                foreach (var entry in _modalStack)
                {
                    HideEntry(entry);
                    entry.OnDestroy?.Invoke();
                }
                foreach (var entry in _screenStack)
                {
                    HideEntry(entry);
                    entry.OnDestroy?.Invoke();
                }
            }
            
            _modalStack.Clear();
            _screenStack.Clear();
            _registeredScreens.Clear();
            
            Log("Navigation cleared");
        }
        
        /// <summary>
        /// Navigates back to a specific screen, popping everything above it.
        /// </summary>
        public bool PopTo(string id)
        {
            // Check modals first
            while (_modalStack.Count > 0)
            {
                if (_modalStack.Peek().Id == id)
                {
                    ShowEntry(_modalStack.Peek());
                    return true;
                }
                Pop();
            }
            
            // Then screens
            while (_screenStack.Count > 1)
            {
                if (_screenStack.Peek().Id == id)
                {
                    ShowEntry(_screenStack.Peek());
                    return true;
                }
                Pop();
            }
            
            // Check if root screen matches
            if (_screenStack.Count == 1 && _screenStack.Peek().Id == id)
            {
                ShowEntry(_screenStack.Peek());
                return true;
            }
            
            Debug.LogWarning($"[Navigation] Could not find screen '{id}' to pop to");
            return false;
        }
        
        private void ShowEntry(NavigationEntry entry)
        {
            if (entry == null) return;
            
            entry.IsVisible = true;
            
            if (entry.Root != null)
            {
                entry.Root.style.display = DisplayStyle.Flex;
            }
            
            entry.OnShow?.Invoke();
        }
        
        private void HideEntry(NavigationEntry entry)
        {
            if (entry == null) return;
            
            entry.IsVisible = false;
            
            if (entry.Root != null)
            {
                entry.Root.style.display = DisplayStyle.None;
            }
            
            entry.OnHide?.Invoke();
        }
        
        private void Log(string message)
        {
            if (_debugLogging)
            {
                Debug.Log($"[Navigation] {message}");
            }
        }
    }
}
