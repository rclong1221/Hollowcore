using Unity.Entities;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Static helper for game code to interact with zone clear state.
    /// Use when ZoneClearMode = Manual, or for game-specific triggers.
    /// </summary>
    public static class ZoneClearAPI
    {
        /// <summary>Signal that the player has activated the zone exit.</summary>
        public static void ActivateExit(EntityManager em, Entity runEntity)
            => em.SetComponentEnabled<ZoneExitActivated>(runEntity, true);

        /// <summary>Manually mark the zone as cleared (for Manual clear mode).</summary>
        public static void ForceZoneClear(EntityManager em, Entity runEntity)
        {
            var state = em.GetComponentData<ZoneState>(runEntity);
            state.IsCleared = true;
            em.SetComponentData(runEntity, state);
        }
    }
}
