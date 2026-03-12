using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 16.1 Phase 5: Authoring component for interactive minigames.
    ///
    /// Designer workflow:
    /// 1. Add InteractableAuthoring (Type = Timed or Instant)
    /// 2. Add MinigameAuthoring (configure type ID, difficulty, time limit)
    /// 3. Create a MinigameLink MonoBehaviour on a scene GameObject with matching TypeID
    /// 4. Assign a minigame UI prefab to the MinigameLink
    /// 5. The UI prefab should implement IMinigameUI and call link.ReportResult()
    ///
    /// When the player interacts:
    /// - InteractAbilitySystem activates MinigameState
    /// - MinigameBridgeSystem detects activation, opens UI via MinigameLink
    /// - Player completes/fails the minigame UI
    /// - MinigameLink.ReportResult() writes back to MinigameState
    /// - InteractAbilitySystem checks MinigameState for completion/failure
    /// </summary>
    public class MinigameAuthoring : MonoBehaviour
    {
        [Header("Minigame Configuration")]
        [Tooltip("Type ID linking to a MinigameLink MonoBehaviour in the scene")]
        public int MinigameTypeID = 1;

        [Tooltip("Difficulty level passed to the minigame UI (0 = easy, 1 = hard)")]
        [Range(0f, 1f)]
        public float DifficultyLevel = 0.5f;

        [Tooltip("Time limit in seconds. 0 = no limit")]
        public float TimeLimit = 30f;

        [Tooltip("If true, failing the minigame cancels the interaction")]
        public bool FailEndsInteraction = true;

        [Tooltip("Reward tier for loot/result quality on success")]
        public int RewardTier = 0;

        public class Baker : Baker<MinigameAuthoring>
        {
            public override void Bake(MinigameAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new MinigameConfig
                {
                    MinigameTypeID = authoring.MinigameTypeID,
                    DifficultyLevel = authoring.DifficultyLevel,
                    TimeLimit = authoring.TimeLimit,
                    FailEndsInteraction = authoring.FailEndsInteraction,
                    RewardTier = authoring.RewardTier
                });

                AddComponent(entity, new MinigameState
                {
                    IsActive = false,
                    Succeeded = false,
                    Failed = false,
                    TimeRemaining = 0,
                    Score = 0,
                    PerformingEntity = Entity.Null
                });
            }
        }
    }
}
