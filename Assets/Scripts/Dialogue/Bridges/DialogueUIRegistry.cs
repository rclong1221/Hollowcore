using UnityEngine;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Central registry for dialogue UI providers.
    /// Follows CombatUIRegistry pattern — static singleton, MonoBehaviours register on Awake.
    /// </summary>
    public static class DialogueUIRegistry
    {
        private static IDialogueUIProvider _dialogue;
        private static IBarkUIProvider _bark;

        public static IDialogueUIProvider Dialogue => _dialogue;
        public static IBarkUIProvider Bark => _bark;

        public static bool HasDialogue => _dialogue != null;
        public static bool HasBark => _bark != null;

        public static void RegisterDialogue(IDialogueUIProvider provider)
        {
            if (_dialogue != null && provider != null)
                Debug.LogWarning("[DialogueUIRegistry] Replacing existing dialogue provider.");
            _dialogue = provider;
        }

        public static void RegisterBark(IBarkUIProvider provider)
        {
            if (_bark != null && provider != null)
                Debug.LogWarning("[DialogueUIRegistry] Replacing existing bark provider.");
            _bark = provider;
        }

        public static void UnregisterDialogue(IDialogueUIProvider provider)
        {
            if (_dialogue == provider) _dialogue = null;
        }

        public static void UnregisterBark(IBarkUIProvider provider)
        {
            if (_bark == provider) _bark = null;
        }

        public static void UnregisterAll()
        {
            _dialogue = null;
            _bark = null;
        }
    }
}
