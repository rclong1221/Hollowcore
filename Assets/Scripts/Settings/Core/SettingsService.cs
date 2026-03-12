using System;
using System.Collections.Generic;
using DIG.UI.Core.Services;
using UnityEngine;

namespace DIG.Settings.Core
{
    /// <summary>
    /// EPIC 18.2: Lightweight static service for opening/closing the Settings screen.
    /// Pages are registered by SettingsScreenController (not self-registering).
    /// </summary>
    public static class SettingsService
    {
        private static readonly List<ISettingsPage> _pages = new();
        private static ScreenHandle _handle;
        private static SettingsScreenController _controller;

        /// <summary>All registered settings pages (sorted by SortOrder).</summary>
        public static IReadOnlyList<ISettingsPage> Pages => _pages;

        /// <summary>Whether the Settings screen is currently open.</summary>
        public static bool IsOpen => UIServices.Screen != null && UIServices.Screen.IsOpen("Settings");

        /// <summary>The active controller (null when closed).</summary>
        internal static SettingsScreenController Controller => _controller;

        /// <summary>Register a page. Called by SettingsScreenController during init.</summary>
        public static void RegisterPage(ISettingsPage page)
        {
            if (page == null) return;
            // Avoid duplicate registration
            for (int i = 0; i < _pages.Count; i++)
            {
                if (_pages[i].PageId == page.PageId) return;
            }
            _pages.Add(page);
            _pages.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        }

        /// <summary>Clear all registered pages. Called on domain reload and when screen closes.</summary>
        public static void ClearPages()
        {
            _pages.Clear();
        }

        /// <summary>Open the Settings screen via UIServices.</summary>
        public static void Open()
        {
            if (UIServices.Screen == null)
            {
                Debug.LogError("[SettingsService] UIServices.Screen is not initialized.");
                return;
            }

            if (IsOpen)
            {
                Debug.LogWarning("[SettingsService] Settings screen is already open.");
                return;
            }

            UIServices.Screen.OnScreenOpened += OnScreenOpened;
            _handle = UIServices.Screen.OpenScreen("Settings");

            if (!_handle.IsValid)
            {
                UIServices.Screen.OnScreenOpened -= OnScreenOpened;
                Debug.LogError("[SettingsService] Failed to open Settings screen. Ensure 'Settings' is registered in the ScreenManifest.");
            }
        }

        /// <summary>Close the Settings screen.</summary>
        public static void Close()
        {
            if (!_handle.IsValid || UIServices.Screen == null) return;

            UIServices.Screen.CloseScreen(_handle, () =>
            {
                Cleanup();
            });
        }

        private static void OnScreenOpened(ScreenHandle handle)
        {
            if (handle != _handle) return;
            UIServices.Screen.OnScreenOpened -= OnScreenOpened;

            // Find the screen root from NavigationManager
            var navEntry = DIG.UI.Core.Navigation.NavigationManager.Instance?.CurrentModal;
            if (navEntry == null)
            {
                // Fallback: try screen stack
                navEntry = DIG.UI.Core.Navigation.NavigationManager.Instance?.CurrentScreen;
            }

            if (navEntry?.Root == null)
            {
                Debug.LogError("[SettingsService] Could not find Settings screen root VisualElement.");
                return;
            }

            _controller = new SettingsScreenController(navEntry.Root);
        }

        private static void Cleanup()
        {
            _controller?.Dispose();
            _controller = null;
            _handle = ScreenHandle.Invalid;
            ClearPages();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _pages.Clear();
            _controller = null;
            _handle = ScreenHandle.Invalid;
        }
    }
}
