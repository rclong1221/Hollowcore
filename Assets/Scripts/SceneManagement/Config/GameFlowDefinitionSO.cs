using System;
using UnityEngine;

namespace DIG.SceneManagement
{
    /// <summary>
    /// EPIC 18.6: Defines the entire game flow as a state machine graph.
    /// States represent game phases (MainMenu, Lobby, Gameplay, Results).
    /// Transitions define valid state changes and their trigger conditions.
    /// Loaded from Resources/ by SceneService at Awake.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Scene Management/Game Flow Definition")]
    public class GameFlowDefinitionSO : ScriptableObject
    {
        [Tooltip("Display name for this flow definition.")]
        public string FlowName = "Default";

        [Tooltip("State ID to enter on boot.")]
        public string InitialState = "MainMenu";

        [Tooltip("All possible game states.")]
        public GameFlowState[] States;

        [Tooltip("Valid state transitions.")]
        public GameFlowTransition[] Transitions;

        [Header("Defaults")]
        [Tooltip("Fallback loading screen when a state has none assigned.")]
        public LoadingScreenProfileSO DefaultLoadingScreen;

        public TransitionAnimation DefaultTransitionAnimation = TransitionAnimation.Fade;

        [Min(0f)] public float DefaultTransitionDuration = 0.5f;
    }

    /// <summary>
    /// A single game phase with its scene, loading screen, and behavior config.
    /// </summary>
    [Serializable]
    public class GameFlowState
    {
        [Tooltip("Unique identifier, e.g. 'MainMenu', 'Lobby', 'Gameplay', 'Results'.")]
        public string StateId;

        [Tooltip("Primary scene to load for this state.")]
        public SceneDefinitionSO Scene;

        [Tooltip("Additional scenes loaded alongside the primary scene (additive).")]
        public SceneDefinitionSO[] AdditiveScenes;

        [Tooltip("Loading screen appearance. Null uses GameFlowDefinitionSO.DefaultLoadingScreen.")]
        public LoadingScreenProfileSO LoadingScreen;

        [Tooltip("If true, SceneService wraps GameBootstrap for ECS world creation.")]
        public bool RequiresNetwork;

        [Tooltip("Input context activated when this state is entered.")]
        public DIG.Core.Input.InputContext InputContext;

        [Tooltip("Event name fired on entering this state (for external listeners).")]
        public string OnEnterEvent;

        [Tooltip("Event name fired on exiting this state.")]
        public string OnExitEvent;
    }

    /// <summary>
    /// A valid state change with trigger condition and visual transition config.
    /// </summary>
    [Serializable]
    public class GameFlowTransition
    {
        public string FromState;
        public string ToState;

        public TransitionCondition Condition = TransitionCondition.Event;

        [Tooltip("Event name that triggers this transition (when Condition = Event).")]
        public string TriggerEvent;

        public TransitionAnimation Animation;

        [Min(0f)] public float AnimationDuration;
    }
}
