using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction
{
    // ─────────────────────────────────────────────────────
    //  EPIC 16.1 Phase 5: Minigames
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// EPIC 16.1 Phase 5: Configuration for an interactive minigame.
    /// Placed on the INTERACTABLE entity alongside InteractableAuthoring.
    /// Links to a managed MinigameLink MonoBehaviour via MinigameTypeID.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct MinigameConfig : IComponentData
    {
        /// <summary>Links to managed MinigameLink registry. Must match MinigameLink.MinigameTypeID.</summary>
        public int MinigameTypeID;

        /// <summary>0-1 difficulty scalar passed to the minigame UI.</summary>
        [GhostField(Quantization = 100)]
        public float DifficultyLevel;

        /// <summary>Max time to complete in seconds. 0 = no limit.</summary>
        public float TimeLimit;

        /// <summary>If true, failing the minigame cancels the interaction.</summary>
        public bool FailEndsInteraction;

        /// <summary>Quality tier of loot/result on success.</summary>
        public int RewardTier;
    }

    /// <summary>
    /// EPIC 16.1 Phase 5: Runtime state for an active minigame.
    /// Placed on the INTERACTABLE entity. Written by MinigameBridgeSystem and InteractAbilitySystem.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct MinigameState : IComponentData
    {
        /// <summary>Whether the minigame is currently running.</summary>
        [GhostField]
        public bool IsActive;

        /// <summary>Player completed the minigame successfully.</summary>
        [GhostField]
        public bool Succeeded;

        /// <summary>Player failed the minigame.</summary>
        [GhostField]
        public bool Failed;

        /// <summary>Countdown timer. Decremented each frame when active.</summary>
        [GhostField(Quantization = 100)]
        public float TimeRemaining;

        /// <summary>Optional score for graded minigames.</summary>
        [GhostField(Quantization = 100)]
        public float Score;

        /// <summary>The entity performing the minigame.</summary>
        [GhostField]
        public Entity PerformingEntity;
    }

    /// <summary>
    /// Result struct for the managed → ECS callback queue.
    /// </summary>
    public struct MinigameResult
    {
        public Entity TargetEntity;
        public bool Succeeded;
        public float Score;
    }
}
