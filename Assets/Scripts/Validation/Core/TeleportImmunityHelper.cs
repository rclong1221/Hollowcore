using Unity.Entities;
using Unity.Transforms;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Static helper to grant teleport immunity for legitimate teleportation.
    /// Call from any system that teleports a player (respawn, portal, load, admin TP)
    /// to prevent false-positive movement violations.
    /// </summary>
    public static class TeleportImmunityHelper
    {
        /// <summary>
        /// Grant teleport immunity to a player, preventing movement violations
        /// for the specified number of ticks. Also updates LastValidatedPosition
        /// to the current position to prevent delta spikes after teleport.
        /// </summary>
        public static void GrantImmunity(EntityManager em, Entity player, uint currentTick, uint graceTicks)
        {
            if (!em.HasComponent<ValidationLink>(player)) return;

            var link = em.GetComponentData<ValidationLink>(player);
            if (link.ValidationChild == Entity.Null) return;
            if (!em.HasComponent<MovementValidationState>(link.ValidationChild)) return;

            var state = em.GetComponentData<MovementValidationState>(link.ValidationChild);
            state.TeleportCooldownTick = currentTick + graceTicks;
            state.AccumulatedError = 0f;

            // Update last validated position to current position (avoid delta spike)
            if (em.HasComponent<LocalTransform>(player))
            {
                state.LastValidatedPosition = em.GetComponentData<LocalTransform>(player).Position;
                state.LastValidatedTick = currentTick;
            }

            em.SetComponentData(link.ValidationChild, state);
        }
    }
}
