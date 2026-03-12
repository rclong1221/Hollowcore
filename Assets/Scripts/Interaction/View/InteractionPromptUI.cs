using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DIG.Interaction.View
{
    /// <summary>
    /// MonoBehaviour for displaying interaction prompts.
    /// Reads ECS InteractionPrompt state each frame.
    ///
    /// EPIC 15.23 additions:
    /// - Screen safe area clamping (90% title-safe bounds)
    /// - Completion animation (Scale Up -> Flash White -> Scale Down with EaseInBack)
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Image progressBar;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("EPIC 15.23: Visual Juice")]
        [Tooltip("Optional overlay image for white flash on completion.")]
        [SerializeField] private Image flashOverlay;
        [Tooltip("Duration of the completion animation in seconds.")]
        [SerializeField] private float completionDuration = 0.4f;

        [Header("Settings")]
        [SerializeField] private float fadeSpeed = 5f;
        [SerializeField] private Vector3 worldOffset = new Vector3(0, 1.5f, 0);

        [Header("EPIC 15.23: Screen Clamping")]
        [Tooltip("Fraction of screen edge to use as safe area margin (0.05 = 5%).")]
        [SerializeField] private float safeAreaMargin = 0.05f;

        private Camera mainCamera;
        private EntityManager entityManager;
        private Entity localPlayerEntity;
        private bool isInitialized;

        private float targetAlpha;
        private float currentAlpha;

        // EPIC 15.23: Completion animation state
        private bool _isPlayingCompletion;
        private float _completionTimer;
        private Vector3 _baseScale;

        // EPIC 15.23: Previous frame state for detecting completion
        private bool _previousVisible;
        private float _previousHoldProgress;

        private void Start()
        {
            mainCamera = Camera.main;
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
                _baseScale = promptPanel.transform.localScale;
            }

            // Hide flash overlay initially
            if (flashOverlay != null)
            {
                var c = flashOverlay.color;
                c.a = 0f;
                flashOverlay.color = c;
            }
        }

        private void Update()
        {
            TryInitialize();

            if (!isInitialized)
                return;

            // Play completion animation if active (runs independently of prompt state)
            if (_isPlayingCompletion)
            {
                UpdateCompletionAnimation();
                return;
            }

            UpdatePromptState();
            UpdateFade();
        }

        private void TryInitialize()
        {
            if (isInitialized)
                return;

            // Try to find ClientSimulation world
            foreach (var world in World.All)
            {
                if (world.Name.Contains("Client"))
                {
                    entityManager = world.EntityManager;
                    isInitialized = true;
                    break;
                }
            }
        }

        private void UpdatePromptState()
        {
            if (!isInitialized)
                return;

            // Find local player with interaction prompt
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<InteractionPrompt>(),
                ComponentType.ReadOnly<CanInteract>()
            );

            if (query.IsEmpty)
            {
                DetectCompletion(false, 0f);
                targetAlpha = 0f;
                return;
            }

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0)
            {
                entities.Dispose();
                DetectCompletion(false, 0f);
                targetAlpha = 0f;
                return;
            }

            // Use first entity (local player)
            var promptEntity = entities[0];
            entities.Dispose();

            if (!entityManager.HasComponent<InteractionPrompt>(promptEntity))
            {
                DetectCompletion(false, 0f);
                targetAlpha = 0f;
                return;
            }

            var prompt = entityManager.GetComponentData<InteractionPrompt>(promptEntity);

            if (prompt.IsVisible && prompt.InteractableEntity != Entity.Null)
            {
                targetAlpha = 1f;

                // Update message
                if (messageText != null)
                {
                    messageText.text = prompt.Message.ToString();
                }

                // Update progress bar
                if (progressBar != null)
                {
                    progressBar.fillAmount = prompt.HoldProgress;
                    progressBar.gameObject.SetActive(prompt.HoldProgress > 0f);
                }

                // Position prompt at interactable
                if (entityManager.HasComponent<LocalTransform>(prompt.InteractableEntity) && mainCamera != null)
                {
                    var targetTransform = entityManager.GetComponentData<LocalTransform>(prompt.InteractableEntity);
                    Vector3 worldPos = targetTransform.Position + (float3)worldOffset;
                    Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

                    if (screenPos.z > 0)
                    {
                        // EPIC 15.23: Clamp to screen safe area (90% title-safe bounds)
                        screenPos = ClampToSafeArea(screenPos);
                        transform.position = screenPos;
                    }
                }

                DetectCompletion(true, prompt.HoldProgress);
            }
            else
            {
                DetectCompletion(false, prompt.HoldProgress);
                targetAlpha = 0f;
            }
        }

        /// <summary>
        /// EPIC 15.23: Clamp screen position to title-safe area.
        /// Prevents prompt from clipping at screen edges.
        /// </summary>
        private Vector3 ClampToSafeArea(Vector3 screenPos)
        {
            float minX = Screen.width * safeAreaMargin;
            float maxX = Screen.width * (1f - safeAreaMargin);
            float minY = Screen.height * safeAreaMargin;
            float maxY = Screen.height * (1f - safeAreaMargin);

            screenPos.x = Mathf.Clamp(screenPos.x, minX, maxX);
            screenPos.y = Mathf.Clamp(screenPos.y, minY, maxY);

            return screenPos;
        }

        /// <summary>
        /// EPIC 15.23: Detect when an interaction completes (visible->hidden with progress >= 1)
        /// and trigger the completion animation.
        /// </summary>
        private void DetectCompletion(bool currentVisible, float currentHoldProgress)
        {
            // Completion: was visible with high progress, now becoming hidden
            if (_previousVisible && !currentVisible && _previousHoldProgress >= 0.95f)
            {
                StartCompletionAnimation();
            }

            _previousVisible = currentVisible;
            _previousHoldProgress = currentHoldProgress;
        }

        /// <summary>
        /// EPIC 15.23: Start the "Scale Up -> Flash White -> Scale Down" completion animation.
        /// </summary>
        private void StartCompletionAnimation()
        {
            _isPlayingCompletion = true;
            _completionTimer = 0f;

            // Keep panel visible during animation
            if (promptPanel != null)
                promptPanel.SetActive(true);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// EPIC 15.23: Update the completion animation each frame.
        /// Phase 1 (0->0.375): Scale 1.0 -> 1.3 (overshoot)
        /// Phase 2 (0.375->0.625): Flash white overlay alpha 0->1->0
        /// Phase 3 (0.625->1.0): Scale 1.3 -> 0 with EaseInBack curve
        /// </summary>
        private void UpdateCompletionAnimation()
        {
            _completionTimer += Time.deltaTime;
            float t = _completionTimer / completionDuration;

            if (t >= 1f)
            {
                // Animation complete — reset and hide
                _isPlayingCompletion = false;
                if (promptPanel != null)
                {
                    promptPanel.transform.localScale = _baseScale;
                    promptPanel.SetActive(false);
                }
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;
                if (flashOverlay != null)
                {
                    var c = flashOverlay.color;
                    c.a = 0f;
                    flashOverlay.color = c;
                }
                currentAlpha = 0f;
                targetAlpha = 0f;
                return;
            }

            // Phase 1: Scale up (0 -> 0.375)
            if (t < 0.375f)
            {
                float phase = t / 0.375f;
                float scale = Mathf.Lerp(1f, 1.3f, phase);
                if (promptPanel != null)
                    promptPanel.transform.localScale = _baseScale * scale;
            }
            // Phase 2: Flash white (0.375 -> 0.625)
            else if (t < 0.625f)
            {
                float phase = (t - 0.375f) / 0.25f;
                // Triangle wave: 0->1->0
                float flashAlpha = phase < 0.5f ? phase * 2f : (1f - phase) * 2f;

                if (flashOverlay != null)
                {
                    var c = flashOverlay.color;
                    c.a = flashAlpha;
                    flashOverlay.color = c;
                }

                // Keep at max scale during flash
                if (promptPanel != null)
                    promptPanel.transform.localScale = _baseScale * 1.3f;
            }
            // Phase 3: Scale down with EaseInBack (0.625 -> 1.0)
            else
            {
                float phase = (t - 0.625f) / 0.375f;
                float eased = EaseInBack(phase);
                float scale = Mathf.Lerp(1.3f, 0f, eased);
                if (promptPanel != null)
                    promptPanel.transform.localScale = _baseScale * Mathf.Max(0f, scale);

                // Fade flash overlay
                if (flashOverlay != null)
                {
                    var c = flashOverlay.color;
                    c.a = 0f;
                    flashOverlay.color = c;
                }

                // Fade out alpha
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f - phase;
            }
        }

        /// <summary>
        /// EaseInBack easing function: slight overshoot before accelerating.
        /// </summary>
        private static float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        private void UpdateFade()
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = currentAlpha;
            }

            if (promptPanel != null)
            {
                promptPanel.SetActive(currentAlpha > 0.01f);
            }
        }
    }
}
