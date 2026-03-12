using Unity.Mathematics;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Games implement this to spawn actual interactable objects from placement nodes.
    /// The framework's InteractableDirectorSystem calls this with positions and budgets;
    /// the game decides what to place (chests, shrines, equipment barrels, etc).
    ///
    /// Games that don't want framework-managed interactables simply don't register a handler.
    /// </summary>
    public interface IInteractableHandler
    {
        /// <summary>
        /// Place interactables in the zone. Called once after zone activation.
        /// </summary>
        /// <param name="nodes">Positions from ZoneActivationResult.InteractableNodes.</param>
        /// <param name="typeIds">Interactable type IDs selected from the InteractablePoolSO.</param>
        /// <param name="seed">Deterministic seed for any randomization during placement.</param>
        /// <param name="difficulty">Current effective difficulty for cost/quality scaling.</param>
        void PlaceInteractables(
            float3[] nodes,
            int[] typeIds,
            uint seed,
            float difficulty);

        /// <summary>Cleanup all placed interactables on zone deactivation.</summary>
        void ClearInteractables();
    }
}
