using System;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Public contract for the UI screen lifecycle service.
    /// All game code talks to this interface via UIServices.Screen.
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// Opens a screen by its manifest ID.
        /// Returns a handle immediately; the onOpened callback fires after the transition completes.
        /// </summary>
        ScreenHandle OpenScreen(string screenId, object navigationData = null, Action<ScreenHandle> onOpened = null);

        /// <summary>Closes a specific screen by its handle.</summary>
        void CloseScreen(ScreenHandle handle, Action onClosed = null);

        /// <summary>Closes the topmost screen or modal (same as pressing Escape).</summary>
        void CloseTop(Action onClosed = null);

        /// <summary>Is a screen with this ID currently open?</summary>
        bool IsOpen(string screenId);

        /// <summary>Is a specific handle still active?</summary>
        bool IsOpen(ScreenHandle handle);

        /// <summary>Gets the handle for a currently open screen by ID. Returns Invalid if not open.</summary>
        ScreenHandle GetHandle(string screenId);

        /// <summary>Closes all screens and modals, optionally keeping HUD elements.</summary>
        void CloseAll(bool keepHUD = true);

        /// <summary>Applies a theme to all UI layer roots.</summary>
        void SetTheme(UIThemeSO theme);

        /// <summary>Fired after a screen finishes its open transition.</summary>
        event Action<ScreenHandle> OnScreenOpened;

        /// <summary>Fired after a screen finishes its close transition.</summary>
        event Action<ScreenHandle> OnScreenClosed;
    }
}
