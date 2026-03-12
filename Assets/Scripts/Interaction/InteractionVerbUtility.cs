using Unity.Collections;

namespace DIG.Interaction
{
    /// <summary>
    /// EPIC 15.23: Burst-compatible utility for mapping InteractionVerb to display strings.
    ///
    /// Provides default English display names for all interaction verbs.
    /// This is the localization hook — when a real localization framework is added,
    /// replace the switch body with table lookups.
    /// </summary>
    public static class InteractionVerbUtility
    {
        /// <summary>
        /// Returns a display-ready string for the given interaction verb.
        /// </summary>
        public static FixedString32Bytes GetVerbDisplayName(InteractionVerb verb)
        {
            return verb switch
            {
                InteractionVerb.Interact => (FixedString32Bytes)"Interact",
                InteractionVerb.Loot => (FixedString32Bytes)"Loot",
                InteractionVerb.Open => (FixedString32Bytes)"Open",
                InteractionVerb.Close => (FixedString32Bytes)"Close",
                InteractionVerb.Revive => (FixedString32Bytes)"Revive",
                InteractionVerb.Breach => (FixedString32Bytes)"Breach",
                InteractionVerb.Talk => (FixedString32Bytes)"Talk",
                InteractionVerb.Use => (FixedString32Bytes)"Use",
                InteractionVerb.Craft => (FixedString32Bytes)"Craft",
                InteractionVerb.Mount => (FixedString32Bytes)"Mount",
                InteractionVerb.Dismount => (FixedString32Bytes)"Dismount",
                InteractionVerb.Place => (FixedString32Bytes)"Place",
                InteractionVerb.Pickup => (FixedString32Bytes)"Pick Up",
                InteractionVerb.Activate => (FixedString32Bytes)"Activate",
                InteractionVerb.Deactivate => (FixedString32Bytes)"Deactivate",
                _ => (FixedString32Bytes)"Interact"
            };
        }
    }
}
