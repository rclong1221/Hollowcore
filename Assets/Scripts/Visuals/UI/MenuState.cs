using UnityEngine;

namespace DIG.UI
{
    /// <summary>
    /// Central utility for tracking if any UI menus are open.
    /// Used to automate cursor locking and input suppression.
    /// </summary>
    public static class MenuState
    {
        private static System.Collections.Generic.HashSet<object> _openMenus = new();

        /// <summary>
        /// Registers a menu as open or closed.
        /// </summary>
        public static void RegisterMenu(object menu, bool isOpen)
        {
            if (isOpen) _openMenus.Add(menu);
            else _openMenus.Remove(menu);
        }

        /// <summary>
        /// Returns true if the main connection menu or any other blocking UI is visible.
        /// </summary>
        public static bool IsAnyMenuOpen()
        {
            // 1. Check dynamic registry (Pause, Cargo, etc.)
            if (_openMenus.Count > 0)
                return true;

            // 2. Check "Host/Join" menu (implicit)
            if (!GameBootstrap.HasInitialized)
                return true;
            
            return false;
        }
    }
}
