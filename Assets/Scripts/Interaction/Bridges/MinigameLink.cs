using Unity.Entities;
using UnityEngine;
using DIG.Interaction.Systems;

namespace DIG.Interaction.Bridges
{
    /// <summary>
    /// EPIC 16.1 Phase 5: MonoBehaviour bridge for minigame UI.
    ///
    /// Follows the InteractableHybridLink pattern:
    /// 1. Place on a scene GameObject
    /// 2. Set MinigameTypeID to match MinigameConfig on the interactable
    /// 3. Assign a MinigamePrefab (the UI to instantiate)
    /// 4. Minigame UI calls ReportResult() when the player finishes
    ///
    /// MinigameBridgeSystem detects MinigameState.IsActive transitions
    /// and calls OpenMinigame/CloseMinigame on the registered link.
    /// </summary>
    public class MinigameLink : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Must match MinigameConfig.MinigameTypeID on the interactable entity")]
        public int MinigameTypeID;

        [Tooltip("UI prefab to instantiate when the minigame starts")]
        public GameObject MinigamePrefab;

        [Tooltip("Parent transform for instantiated UI (null = Canvas root)")]
        public Transform UIParent;

        // Runtime state
        private GameObject _activeInstance;
        private Entity _currentTargetEntity;

        private void OnEnable()
        {
            MinigameBridgeSystem.RegisterLink(MinigameTypeID, this);
        }

        private void OnDisable()
        {
            MinigameBridgeSystem.UnregisterLink(MinigameTypeID);

            // Clean up if UI is still open
            if (_activeInstance != null)
            {
                Destroy(_activeInstance);
                _activeInstance = null;
            }
        }

        /// <summary>
        /// Called by MinigameBridgeSystem when MinigameState.IsActive becomes true.
        /// Instantiates the minigame UI prefab and passes configuration.
        /// </summary>
        public void OpenMinigame(Entity targetEntity, float difficulty, float timeLimit)
        {
            // Close any existing instance
            CloseMinigame();

            _currentTargetEntity = targetEntity;

            if (MinigamePrefab == null)
            {
                Debug.LogWarning($"[MinigameLink] MinigamePrefab is null for TypeID {MinigameTypeID}");
                return;
            }

            _activeInstance = UIParent != null
                ? Instantiate(MinigamePrefab, UIParent)
                : Instantiate(MinigamePrefab);

            // Pass config to the minigame UI if it implements IMinigameUI
            var minigameUI = _activeInstance.GetComponent<IMinigameUI>();
            if (minigameUI != null)
            {
                minigameUI.Initialize(this, difficulty, timeLimit);
            }
        }

        /// <summary>
        /// Called by MinigameBridgeSystem when MinigameState.IsActive becomes false
        /// or on timeout. Destroys the minigame UI instance.
        /// </summary>
        public void CloseMinigame()
        {
            if (_activeInstance != null)
            {
                Destroy(_activeInstance);
                _activeInstance = null;
            }
            _currentTargetEntity = Entity.Null;
        }

        /// <summary>
        /// Called by minigame UI to report the result back to ECS.
        /// Enqueues to the static result queue consumed by MinigameBridgeSystem.
        /// </summary>
        public void ReportResult(bool succeeded, float score = 0f)
        {
            MinigameBridgeSystem.EnqueueResult(new MinigameResult
            {
                TargetEntity = _currentTargetEntity,
                Succeeded = succeeded,
                Score = score
            });
        }

        /// <summary>Whether a minigame UI is currently open.</summary>
        public bool IsOpen => _activeInstance != null;
    }

    /// <summary>
    /// Interface for minigame UI prefabs. Implement this on the root
    /// component of your minigame UI prefab to receive configuration.
    /// </summary>
    public interface IMinigameUI
    {
        /// <summary>
        /// Called when the minigame UI is instantiated.
        /// Use the link to call ReportResult() when the player finishes.
        /// </summary>
        void Initialize(MinigameLink link, float difficulty, float timeLimit);
    }
}
