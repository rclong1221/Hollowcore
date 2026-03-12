using System;
using System.Collections.Generic;
using DIG.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Core implementation of IUIService.
    /// Manages screen lifecycle: loading UXML from Resources, pooling, transitions, focus.
    /// Delegates stack management to the existing NavigationManager.
    /// </summary>
    public class UIToolkitService : IUIService
    {
        /// <summary>Internal state for each open screen.</summary>
        internal class OpenScreenState
        {
            public ScreenHandle Handle;
            public ScreenDefinition Definition;
            public VisualElement Root;
            public VisualElement LayerContainer;
            public bool IsTransitioning;
        }

        private readonly ScreenManifestSO _manifest;
        private readonly VisualElement _serviceRoot;

        // Layer containers — children of _serviceRoot, sorted by z-order
        private readonly VisualElement _hudLayer;
        private readonly VisualElement _screenLayer;
        private readonly VisualElement _modalLayer;
        private readonly VisualElement _tooltipLayer;

        // Pool: screenId → queue of reusable VisualElement trees
        private readonly Dictionary<string, Queue<VisualElement>> _pool = new();
        private const int MaxPoolPerScreen = 1; // Only need 1 since duplicate opens are blocked

        // Open screens: handle.Id → state
        private readonly Dictionary<int, OpenScreenState> _openScreens = new();

        // Quick lookup: screenId → handle.Id (for IsOpen/GetHandle)
        private readonly Dictionary<string, int> _openByName = new();

        // Tracked topmost handles for O(1) CloseTop
        private ScreenHandle _topmostModal = ScreenHandle.Invalid;
        private ScreenHandle _topmostScreen = ScreenHandle.Invalid;

        // Reusable list for CloseAll to avoid per-call allocation
        private readonly List<ScreenHandle> _closeAllBuffer = new();

        // Cached UXML assets to avoid repeated Resources.Load
        private readonly Dictionary<string, VisualTreeAsset> _uxmlCache = new();

        // Cached USS assets
        private readonly Dictionary<string, StyleSheet> _ussCache = new();

        private int _nextHandleId = 1;
        private UIThemeSO _currentTheme;

        public event Action<ScreenHandle> OnScreenOpened;
        public event Action<ScreenHandle> OnScreenClosed;

        /// <summary>
        /// Creates the service and builds the layer hierarchy.
        /// </summary>
        /// <param name="manifest">The screen manifest to use for lookups.</param>
        /// <param name="hostRoot">The root VisualElement of the host UIDocument.</param>
        public UIToolkitService(ScreenManifestSO manifest, VisualElement hostRoot)
        {
            _manifest = manifest;

            // Create service root that contains all layers
            _serviceRoot = new VisualElement { name = "ui-service-root" };
            _serviceRoot.style.position = Position.Absolute;
            _serviceRoot.style.left = 0;
            _serviceRoot.style.right = 0;
            _serviceRoot.style.top = 0;
            _serviceRoot.style.bottom = 0;
            _serviceRoot.pickingMode = PickingMode.Ignore;

            // Create layer containers in z-order (first = bottom)
            _hudLayer = CreateLayer("ui-layer-hud");
            _screenLayer = CreateLayer("ui-layer-screen");
            _modalLayer = CreateLayer("ui-layer-modal");
            _tooltipLayer = CreateLayer("ui-layer-tooltip");

            _serviceRoot.Add(_hudLayer);
            _serviceRoot.Add(_screenLayer);
            _serviceRoot.Add(_modalLayer);
            _serviceRoot.Add(_tooltipLayer);

            hostRoot.Add(_serviceRoot);

            // Load the transitions stylesheet
            var transitionsSheet = Resources.Load<StyleSheet>("Styles/Transitions");
            if (transitionsSheet != null)
            {
                _serviceRoot.styleSheets.Add(transitionsSheet);
            }
        }

        public ScreenHandle OpenScreen(string screenId, object navigationData = null, Action<ScreenHandle> onOpened = null)
        {
            if (string.IsNullOrEmpty(screenId))
            {
                Debug.LogError("[UIService] Cannot open screen with null/empty screenId.");
                return ScreenHandle.Invalid;
            }

            if (!_manifest.TryGetScreen(screenId, out var def))
            {
                Debug.LogError($"[UIService] Screen '{screenId}' not found in manifest.");
                return ScreenHandle.Invalid;
            }

            // Check if already open (don't allow duplicate opens of the same screen)
            if (_openByName.ContainsKey(screenId))
            {
                Debug.LogWarning($"[UIService] Screen '{screenId}' is already open.");
                return GetHandle(screenId);
            }

            // Resolve or load the VisualElement tree
            VisualElement screenRoot = ResolveScreenElement(def);
            if (screenRoot == null)
            {
                Debug.LogError($"[UIService] Failed to load UXML for screen '{screenId}' at path '{def.UXMLPath}'.");
                return ScreenHandle.Invalid;
            }

            // Create handle
            var handle = new ScreenHandle(_nextHandleId++, screenId);

            // Get the correct layer container
            var layerContainer = GetLayerContainer(def.Layer);

            // Add to layer
            layerContainer.Add(screenRoot);

            // Create state
            var state = new OpenScreenState
            {
                Handle = handle,
                Definition = def,
                Root = screenRoot,
                LayerContainer = layerContainer,
                IsTransitioning = true
            };
            _openScreens[handle.Id] = state;
            _openByName[screenId] = handle.Id;

            // Track topmost for O(1) CloseTop
            if (def.Layer == UILayer.Modal)
                _topmostModal = handle;
            else if (def.Layer == UILayer.Screen)
                _topmostScreen = handle;

            // Resolve transition
            var openTransition = _manifest.ResolveOpenTransition(def);

            // Prepare and play transition
            TransitionPlayer.Prepare(screenRoot, openTransition);
            TransitionPlayer.PlayIn(screenRoot, openTransition, () =>
            {
                state.IsTransitioning = false;
                OnScreenOpened?.Invoke(handle);
                onOpened?.Invoke(handle);
            });

            // Push to NavigationManager for stack management + escape handling
            var navEntry = new NavigationEntry
            {
                Id = screenId,
                Layer = def.Layer,
                Root = screenRoot,
                Handle = handle,
                NavigationData = navigationData,
                OnHide = () => { /* NavigationManager hide is handled by our CloseScreen */ },
            };
            NavigationManager.Instance.Push(navEntry);

            // Focus management
            if (!string.IsNullOrEmpty(def.InitialFocusElement))
            {
                // Delay focus slightly to let the layout complete
                screenRoot.schedule.Execute(() =>
                {
                    FocusManager.PushFocus(screenRoot, def.InitialFocusElement);
                }).StartingIn(50);
            }
            else
            {
                FocusManager.PushFocus(screenRoot);
            }

            return handle;
        }

        public void CloseScreen(ScreenHandle handle, Action onClosed = null)
        {
            if (!handle.IsValid || !_openScreens.TryGetValue(handle.Id, out var state))
            {
                Debug.LogWarning($"[UIService] Cannot close invalid or unknown handle: {handle}");
                onClosed?.Invoke();
                return;
            }

            if (state.IsTransitioning)
            {
                Debug.LogWarning($"[UIService] Screen '{handle.ScreenId}' is mid-transition, ignoring close.");
                return;
            }

            state.IsTransitioning = true;

            // Resolve transition
            var closeTransition = _manifest.ResolveCloseTransition(state.Definition);

            // Play out transition
            TransitionPlayer.PlayOut(state.Root, closeTransition, () =>
            {
                CompleteClose(state);
                onClosed?.Invoke();
            });
        }

        public void CloseTop(Action onClosed = null)
        {
            // O(1) lookup via tracked topmost handles — modals first
            ScreenHandle topHandle = ScreenHandle.Invalid;

            if (_topmostModal.IsValid && _openScreens.TryGetValue(_topmostModal.Id, out var modalState) && !modalState.IsTransitioning)
            {
                topHandle = _topmostModal;
            }
            else if (_topmostScreen.IsValid && _openScreens.TryGetValue(_topmostScreen.Id, out var screenState) && !screenState.IsTransitioning)
            {
                topHandle = _topmostScreen;
            }

            if (topHandle.IsValid)
            {
                CloseScreen(topHandle, onClosed);
            }
            else
            {
                onClosed?.Invoke();
            }
        }

        public bool IsOpen(string screenId)
        {
            return !string.IsNullOrEmpty(screenId) && _openByName.ContainsKey(screenId);
        }

        public bool IsOpen(ScreenHandle handle)
        {
            return handle.IsValid && _openScreens.ContainsKey(handle.Id);
        }

        public ScreenHandle GetHandle(string screenId)
        {
            if (!string.IsNullOrEmpty(screenId) && _openByName.TryGetValue(screenId, out int handleId))
            {
                if (_openScreens.TryGetValue(handleId, out var state))
                    return state.Handle;
            }
            return ScreenHandle.Invalid;
        }

        public void CloseAll(bool keepHUD = true)
        {
            // Reuse buffer to avoid per-call allocation
            _closeAllBuffer.Clear();
            foreach (var kvp in _openScreens)
            {
                if (keepHUD && kvp.Value.Definition.Layer == UILayer.HUD)
                    continue;
                _closeAllBuffer.Add(kvp.Value.Handle);
            }

            for (int i = 0; i < _closeAllBuffer.Count; i++)
            {
                if (_openScreens.TryGetValue(_closeAllBuffer[i].Id, out var state))
                {
                    // Instant close for bulk operations
                    TransitionPlayer.HideInstant(state.Root);
                    CompleteClose(state);
                }
            }
            _closeAllBuffer.Clear();
        }

        public void SetTheme(UIThemeSO theme)
        {
            _currentTheme = theme;
            if (theme == null) return;

            theme.ApplyToPanel(_screenLayer);
            theme.ApplyToPanel(_modalLayer);
            // HUD and tooltip layers typically have transparent backgrounds
        }

        /// <summary>Exposes the manifest for editor tooling.</summary>
        internal ScreenManifestSO Manifest => _manifest;

        /// <summary>Exposes a layer root for external systems (notifications, etc.).</summary>
        public VisualElement GetLayerRoot(UILayer layer)
        {
            return GetLayerContainer(layer);
        }

        /// <summary>Exposes open screens for editor tooling.</summary>
        internal IReadOnlyDictionary<int, OpenScreenState> OpenScreensDebug => _openScreens;

        /// <summary>Exposes pool for editor tooling.</summary>
        internal IReadOnlyDictionary<string, Queue<VisualElement>> PoolDebug => _pool;

        // === Private helpers ===

        private void CompleteClose(OpenScreenState state)
        {
            // Remove from layer
            state.LayerContainer.Remove(state.Root);

            // Pool or destroy
            if (state.Definition.Poolable)
            {
                ReturnToPool(state.Definition.ScreenId, state.Root);
            }

            // Remove from tracking
            _openScreens.Remove(state.Handle.Id);
            _openByName.Remove(state.Handle.ScreenId);

            // Refresh topmost tracking
            if (state.Handle == _topmostModal)
                _topmostModal = FindTopmost(UILayer.Modal);
            else if (state.Handle == _topmostScreen)
                _topmostScreen = FindTopmost(UILayer.Screen);

            // Pop from NavigationManager
            NavigationManager.Instance.Pop();

            // Restore focus
            FocusManager.PopFocus();

            // Fire event
            OnScreenClosed?.Invoke(state.Handle);
        }

        private VisualElement ResolveScreenElement(ScreenDefinition def)
        {
            // Check pool first
            if (def.Poolable && _pool.TryGetValue(def.ScreenId, out var queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            // Load UXML
            if (!_uxmlCache.TryGetValue(def.UXMLPath, out var uxml))
            {
                uxml = Resources.Load<VisualTreeAsset>(def.UXMLPath);
                if (uxml != null)
                    _uxmlCache[def.UXMLPath] = uxml;
            }

            if (uxml == null) return null;

            var root = uxml.Instantiate();

            // Apply optional per-screen USS
            if (!string.IsNullOrEmpty(def.USSPath))
            {
                if (!_ussCache.TryGetValue(def.USSPath, out var uss))
                {
                    uss = Resources.Load<StyleSheet>(def.USSPath);
                    if (uss != null)
                        _ussCache[def.USSPath] = uss;
                }
                if (uss != null)
                    root.styleSheets.Add(uss);
            }

            // Make screen root fill its layer container
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;

            return root;
        }

        private void ReturnToPool(string screenId, VisualElement root)
        {
            if (!_pool.TryGetValue(screenId, out var queue))
            {
                queue = new Queue<VisualElement>();
                _pool[screenId] = queue;
            }

            // Cap pool size — only need 1 since duplicate opens are blocked
            if (queue.Count >= MaxPoolPerScreen)
                return;

            // Reset state for reuse
            root.style.display = DisplayStyle.None;
            root.style.opacity = 1f;
            queue.Enqueue(root);
        }

        private ScreenHandle FindTopmost(UILayer layer)
        {
            ScreenHandle result = ScreenHandle.Invalid;
            foreach (var kvp in _openScreens)
            {
                if (kvp.Value.Definition.Layer == layer)
                    result = kvp.Value.Handle;
            }
            return result;
        }

        private VisualElement GetLayerContainer(UILayer layer)
        {
            return layer switch
            {
                UILayer.HUD => _hudLayer,
                UILayer.Screen => _screenLayer,
                UILayer.Modal => _modalLayer,
                UILayer.Tooltip => _tooltipLayer,
                _ => _screenLayer
            };
        }

        private static VisualElement CreateLayer(string name)
        {
            var layer = new VisualElement { name = name };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.right = 0;
            layer.style.top = 0;
            layer.style.bottom = 0;
            layer.pickingMode = PickingMode.Ignore;
            return layer;
        }
    }
}
