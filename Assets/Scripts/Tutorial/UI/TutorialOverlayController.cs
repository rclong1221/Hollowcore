using DIG.Tutorial.Config;
using DIG.UI.Core.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Tutorial.UI
{
    /// <summary>
    /// EPIC 18.4: Manages UI Toolkit overlay for tutorial steps.
    /// Spotlight mode: 4 VisualElements forming a rect-cutout mask.
    /// Tooltip mode: positioned bubble with arrow.
    /// Popup mode: centered modal with continue button.
    /// World marker mode: screen-space arrow tracking world position.
    /// </summary>
    public class TutorialOverlayController
    {
        private readonly VisualElement _root;
        private readonly TutorialConfigSO _config;

        // Spotlight elements
        private readonly VisualElement _spotlightContainer;
        private readonly VisualElement _spotlightTop;
        private readonly VisualElement _spotlightBottom;
        private readonly VisualElement _spotlightLeft;
        private readonly VisualElement _spotlightRight;

        // Tooltip elements
        private readonly VisualElement _tooltipContainer;
        private readonly Label _tooltipTitle;
        private readonly Label _tooltipMessage;
        private readonly Button _tooltipContinueBtn;

        // Popup elements
        private readonly VisualElement _popupContainer;
        private readonly Label _popupTitle;
        private readonly Label _popupMessage;
        private readonly Button _popupContinueBtn;

        // World marker elements
        private readonly VisualElement _worldMarkerContainer;
        private readonly VisualElement _markerArrow;
        private readonly Label _markerDistance;

        // Controls
        private readonly Button _skipBtn;
        private readonly Label _stepCounter;

        // Tracking state
        private TutorialStepSO _currentStep;
        private string _targetElementName;
        private string _worldTargetTag;
        private VisualElement _layerRoot;

        // Cached per-step lookups (avoid per-frame Q() and FindWithTag())
        private VisualElement _cachedTargetElement;
        private GameObject _cachedWorldTarget;
        private Camera _cachedCamera;

        public TutorialOverlayController(VisualElement layerRoot, VisualTreeAsset template, TutorialConfigSO config)
        {
            _layerRoot = layerRoot;
            _config = config;

            if (template != null)
            {
                _root = template.CloneTree();
                _root.style.position = Position.Absolute;
                _root.style.top = 0;
                _root.style.left = 0;
                _root.style.right = 0;
                _root.style.bottom = 0;
                _root.pickingMode = PickingMode.Ignore;
                layerRoot.Add(_root);
            }
            else
            {
                _root = new VisualElement();
                _root.style.position = Position.Absolute;
                _root.style.top = 0;
                _root.style.left = 0;
                _root.style.right = 0;
                _root.style.bottom = 0;
                _root.pickingMode = PickingMode.Ignore;
                layerRoot.Add(_root);
            }

            // Query elements
            _spotlightContainer = _root.Q("spotlight-container");
            _spotlightTop = _root.Q("spotlight-top");
            _spotlightBottom = _root.Q("spotlight-bottom");
            _spotlightLeft = _root.Q("spotlight-left");
            _spotlightRight = _root.Q("spotlight-right");

            _tooltipContainer = _root.Q("tooltip-container");
            _tooltipTitle = _root.Q<Label>("tooltip-title");
            _tooltipMessage = _root.Q<Label>("tooltip-message");
            _tooltipContinueBtn = _root.Q<Button>("tooltip-continue-btn");

            _popupContainer = _root.Q("popup-container");
            _popupTitle = _root.Q<Label>("popup-title");
            _popupMessage = _root.Q<Label>("popup-message");
            _popupContinueBtn = _root.Q<Button>("popup-continue-btn");

            _worldMarkerContainer = _root.Q("world-marker-container");
            _markerArrow = _root.Q("marker-arrow");
            _markerDistance = _root.Q<Label>("marker-distance");

            _skipBtn = _root.Q<Button>("skip-btn");
            _stepCounter = _root.Q<Label>("step-counter");

            // Wire buttons
            if (_tooltipContinueBtn != null)
                _tooltipContinueBtn.clicked += () => TutorialService.Instance?.AdvanceStep();

            if (_popupContinueBtn != null)
                _popupContinueBtn.clicked += () => TutorialService.Instance?.AdvanceStep();

            if (_skipBtn != null)
                _skipBtn.clicked += () => TutorialService.Instance?.SkipTutorial();
        }

        public void ShowStep(TutorialStepSO step, int stepIndex, int totalSteps, bool canSkip)
        {
            HideAll();
            _currentStep = step;

            // Process message with input glyph tokens
            string processedMessage = ProcessGlyphTokens(step.Message);

            // Show step counter
            if (_stepCounter != null)
            {
                _stepCounter.text = $"Step {stepIndex + 1} of {totalSteps}";
                _stepCounter.style.display = DisplayStyle.Flex;
            }

            // Show skip button
            if (_skipBtn != null)
                _skipBtn.style.display = canSkip ? DisplayStyle.Flex : DisplayStyle.None;

            switch (step.StepType)
            {
                case TutorialStepType.Tooltip:
                    ShowTooltip(step, processedMessage);
                    break;
                case TutorialStepType.Highlight:
                    ShowHighlight(step, processedMessage);
                    break;
                case TutorialStepType.ForcedAction:
                    ShowTooltip(step, processedMessage);
                    if (_tooltipContinueBtn != null)
                        _tooltipContinueBtn.style.display = DisplayStyle.None;
                    break;
                case TutorialStepType.Popup:
                    ShowPopup(step, processedMessage);
                    break;
                case TutorialStepType.WorldMarker:
                    ShowWorldMarker(step, processedMessage);
                    break;
            }
        }

        public void HideAll()
        {
            _currentStep = null;
            _targetElementName = null;
            _worldTargetTag = null;
            _cachedTargetElement = null;
            _cachedWorldTarget = null;
            _cachedCamera = null;

            if (_spotlightContainer != null)
                _spotlightContainer.style.display = DisplayStyle.None;

            if (_tooltipContainer != null)
            {
                _tooltipContainer.RemoveFromClassList("tutorial-tooltip--visible");
                _tooltipContainer.style.display = DisplayStyle.None;
            }

            if (_popupContainer != null)
            {
                _popupContainer.RemoveFromClassList("tutorial-popup--visible");
                _popupContainer.style.display = DisplayStyle.None;
            }

            if (_worldMarkerContainer != null)
                _worldMarkerContainer.style.display = DisplayStyle.None;

            if (_skipBtn != null)
                _skipBtn.style.display = DisplayStyle.None;

            if (_stepCounter != null)
                _stepCounter.style.display = DisplayStyle.None;
        }

        public void Tick(float deltaTime)
        {
            if (_currentStep == null) return;

            // Update spotlight position tracking
            if (_currentStep.StepType == TutorialStepType.Highlight && !string.IsNullOrEmpty(_targetElementName))
                UpdateSpotlightPosition();

            // Update world marker projection
            if (_currentStep.StepType == TutorialStepType.WorldMarker && !string.IsNullOrEmpty(_worldTargetTag))
                UpdateWorldMarkerPosition();
        }

        // ── Private show methods ─────────────────────────────────

        private void ShowTooltip(TutorialStepSO step, string processedMessage)
        {
            if (_tooltipContainer == null) return;

            _targetElementName = step.TargetElementName;

            if (_tooltipTitle != null)
            {
                _tooltipTitle.text = step.Title ?? "";
                _tooltipTitle.style.display = string.IsNullOrEmpty(step.Title) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_tooltipMessage != null)
                _tooltipMessage.text = processedMessage;

            if (_tooltipContinueBtn != null)
                _tooltipContinueBtn.style.display = step.CompletionCondition == CompletionCondition.ManualContinue
                    ? DisplayStyle.Flex : DisplayStyle.None;

            _tooltipContainer.style.display = DisplayStyle.Flex;

            // Position near target element if specified
            if (!string.IsNullOrEmpty(_targetElementName))
                PositionTooltipNearTarget();

            // Defer visible class for CSS transition
            _tooltipContainer.schedule.Execute(() =>
                _tooltipContainer.AddToClassList("tutorial-tooltip--visible"));
        }

        private void ShowHighlight(TutorialStepSO step, string processedMessage)
        {
            if (_spotlightContainer == null) return;

            _targetElementName = step.TargetElementName;

            // Show spotlight mask
            _spotlightContainer.style.display = DisplayStyle.Flex;
            UpdateSpotlightPosition();

            // Also show tooltip alongside the spotlight
            ShowTooltip(step, processedMessage);
        }

        private void ShowPopup(TutorialStepSO step, string processedMessage)
        {
            if (_popupContainer == null) return;

            if (_popupTitle != null)
            {
                _popupTitle.text = step.Title ?? "";
                _popupTitle.style.display = string.IsNullOrEmpty(step.Title) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_popupMessage != null)
                _popupMessage.text = processedMessage;

            _popupContainer.style.display = DisplayStyle.Flex;

            // Defer visible class for CSS transition
            _popupContainer.schedule.Execute(() =>
                _popupContainer.AddToClassList("tutorial-popup--visible"));
        }

        private void ShowWorldMarker(TutorialStepSO step, string processedMessage)
        {
            if (_worldMarkerContainer == null) return;

            _worldTargetTag = step.WorldTargetTag;
            _worldMarkerContainer.style.display = DisplayStyle.Flex;

            // Also show tooltip with instructions
            if (!string.IsNullOrEmpty(processedMessage))
                ShowTooltip(step, processedMessage);

            UpdateWorldMarkerPosition();
        }

        // ── Spotlight positioning ────────────────────────────────

        private void UpdateSpotlightPosition()
        {
            if (string.IsNullOrEmpty(_targetElementName)) return;

            // Cache the target element — only Q() once per step, not per frame
            if (_cachedTargetElement == null)
                _cachedTargetElement = _layerRoot.panel?.visualTree?.Q(_targetElementName);

            if (_cachedTargetElement == null)
            {
                SetSpotlightFullScreen();
                return;
            }

            var bounds = _cachedTargetElement.worldBound;
            float padding = _currentStep?.HighlightPadding ?? 20f;

            float cutLeft = Mathf.Max(0, bounds.xMin - padding);
            float cutTop = Mathf.Max(0, bounds.yMin - padding);
            float cutRight = bounds.xMax + padding;
            float cutBottom = bounds.yMax + padding;

            // Top mask: full width, from top to cutout top
            _spotlightTop.style.top = 0;
            _spotlightTop.style.left = 0;
            _spotlightTop.style.right = 0;
            _spotlightTop.style.height = cutTop;

            // Bottom mask: full width, from cutout bottom to bottom
            _spotlightBottom.style.top = cutBottom;
            _spotlightBottom.style.left = 0;
            _spotlightBottom.style.right = 0;
            _spotlightBottom.style.bottom = 0;

            // Left mask: cutout height, from left to cutout left
            _spotlightLeft.style.top = cutTop;
            _spotlightLeft.style.left = 0;
            _spotlightLeft.style.width = cutLeft;
            _spotlightLeft.style.height = cutBottom - cutTop;

            // Right mask: cutout height, from cutout right to right
            _spotlightRight.style.top = cutTop;
            _spotlightRight.style.left = cutRight;
            _spotlightRight.style.right = 0;
            _spotlightRight.style.height = cutBottom - cutTop;
        }

        private void SetSpotlightFullScreen()
        {
            _spotlightTop.style.top = 0;
            _spotlightTop.style.left = 0;
            _spotlightTop.style.right = 0;
            _spotlightTop.style.bottom = 0;
            _spotlightBottom.style.height = 0;
            _spotlightLeft.style.width = 0;
            _spotlightRight.style.width = 0;
        }

        // ── Tooltip positioning ──────────────────────────────────

        private void PositionTooltipNearTarget()
        {
            if (string.IsNullOrEmpty(_targetElementName) || _tooltipContainer == null) return;

            if (_cachedTargetElement == null)
                _cachedTargetElement = _layerRoot.panel?.visualTree?.Q(_targetElementName);

            if (_cachedTargetElement == null) return;

            var bounds = _cachedTargetElement.worldBound;
            float offset = _config?.TooltipOffset ?? 12f;

            // Position below the target element, centered
            _tooltipContainer.style.top = bounds.yMax + offset;
            _tooltipContainer.style.left = bounds.center.x - 120; // Approximate centering
        }

        // ── World marker positioning ─────────────────────────────

        private void UpdateWorldMarkerPosition()
        {
            if (string.IsNullOrEmpty(_worldTargetTag) || _worldMarkerContainer == null) return;

            // Cache the target — only FindWithTag once per step, not per frame
            if (_cachedWorldTarget == null)
                _cachedWorldTarget = GameObject.FindWithTag(_worldTargetTag);

            if (_cachedWorldTarget == null)
            {
                _worldMarkerContainer.style.display = DisplayStyle.None;
                return;
            }

            if (_cachedCamera == null)
                _cachedCamera = Camera.main;

            if (_cachedCamera == null) return;

            Vector3 worldPos = _cachedWorldTarget.transform.position;
            Vector3 screenPos = _cachedCamera.WorldToScreenPoint(worldPos);
            float screenW = Screen.width;
            float screenH = Screen.height;

            // Behind camera or off-screen: clamp to edge
            float margin = _config?.ScreenEdgeMargin ?? 40f;
            bool onScreen = screenPos.z > 0 &&
                            screenPos.x >= 0 && screenPos.x <= screenW &&
                            screenPos.y >= 0 && screenPos.y <= screenH;

            if (!onScreen)
            {
                // Clamp to screen edges (UI Toolkit uses top-left origin)
                if (screenPos.z < 0)
                {
                    screenPos.x = screenW - screenPos.x;
                    screenPos.y = screenH - screenPos.y;
                }

                screenPos.x = Mathf.Clamp(screenPos.x, margin, screenW - margin);
                screenPos.y = Mathf.Clamp(screenPos.y, margin, screenH - margin);
            }

            // Convert from screen coords (bottom-left origin) to UI Toolkit (top-left origin)
            float uiX = screenPos.x;
            float uiY = screenH - screenPos.y;

            _worldMarkerContainer.style.display = DisplayStyle.Flex;
            _worldMarkerContainer.style.left = uiX - 12; // Center the 24px arrow
            _worldMarkerContainer.style.top = uiY - 12;

            // Distance label
            if (_markerDistance != null)
            {
                float dist = Vector3.Distance(_cachedCamera.transform.position, worldPos);
                _markerDistance.text = $"{dist:F0}m";
            }
        }

        // ── Glyph token processing ───────────────────────────────

        private static string ProcessGlyphTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Replace {input:ActionName} with InputGlyphProvider format <Action:ActionName>
            // InputGlyphProvider.ProcessText expects <Action:ActionName> tokens
            text = text.Replace("{input:", "<Action:");
            text = text.Replace("}", ">");

            // Process through InputGlyphProvider if available
            try
            {
                return InputGlyphProvider.ProcessText(text);
            }
            catch
            {
                // If InputGlyphProvider isn't initialized, return with raw tokens stripped
                return text;
            }
        }
    }
}
