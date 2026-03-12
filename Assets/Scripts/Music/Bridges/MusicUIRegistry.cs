using UnityEngine;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Static registry for music UI providers.
    /// Follows CombatUIRegistry pattern — loose coupling between ECS and UI.
    /// </summary>
    public static class MusicUIRegistry
    {
        public static IMusicUIProvider Provider { get; private set; }
        public static bool HasProvider => Provider != null;

        public static void Register(IMusicUIProvider provider)
        {
            if (Provider != null && Provider != provider)
                Debug.LogWarning("[MusicUIRegistry] Replacing existing provider.");
            Provider = provider;
        }

        public static void Unregister(IMusicUIProvider provider)
        {
            if (Provider == provider)
                Provider = null;
        }

        public static void UnregisterAll()
        {
            Provider = null;
        }
    }
}
