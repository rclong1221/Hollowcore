using System.Collections.Generic;

namespace DIG.SceneManagement
{
    /// <summary>
    /// EPIC 18.6: Pure C# state machine driven by GameFlowDefinitionSO.
    /// No MonoBehaviour, no ECS dependency. Testable in isolation.
    /// Evaluates transitions only on explicit calls (not per-frame polling).
    /// </summary>
    public class GameFlowStateMachine
    {
        private GameFlowDefinitionSO _definition;
        private string _currentStateId;
        private readonly Dictionary<string, GameFlowState> _stateLookup = new();
        private readonly List<GameFlowTransition> _transitions = new();

        public string CurrentStateId => _currentStateId;
        public GameFlowState CurrentState => FindState(_currentStateId);
        public bool IsInitialized => _definition != null;

        public void Initialize(GameFlowDefinitionSO definition)
        {
            _definition = definition;
            _stateLookup.Clear();
            _transitions.Clear();

            if (definition.States != null)
            {
                for (int i = 0; i < definition.States.Length; i++)
                {
                    var s = definition.States[i];
                    if (!string.IsNullOrEmpty(s.StateId))
                        _stateLookup[s.StateId] = s;
                }
            }

            if (definition.Transitions != null)
                _transitions.AddRange(definition.Transitions);

            _currentStateId = definition.InitialState;
        }

        /// <summary>
        /// Look up a state by ID. Returns null if not found.
        /// </summary>
        public GameFlowState FindState(string stateId)
        {
            if (string.IsNullOrEmpty(stateId)) return null;
            _stateLookup.TryGetValue(stateId, out var state);
            return state;
        }

        /// <summary>
        /// Check if a direct transition from the current state to <paramref name="toState"/>
        /// is defined. Returns the matching transition, or null.
        /// </summary>
        public GameFlowTransition TryTransition(string toState)
        {
            for (int i = 0; i < _transitions.Count; i++)
            {
                var t = _transitions[i];
                if (t.FromState == _currentStateId && t.ToState == toState)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Process a named event. Returns the target state ID if a matching
        /// Event-condition transition exists from the current state, else null.
        /// </summary>
        public string ProcessEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return null;

            for (int i = 0; i < _transitions.Count; i++)
            {
                var t = _transitions[i];
                if (t.FromState == _currentStateId &&
                    t.Condition == TransitionCondition.Event &&
                    t.TriggerEvent == eventName)
                    return t.ToState;
            }
            return null;
        }

        /// <summary>
        /// Commit a state transition (called after scene loading completes).
        /// </summary>
        public void SetState(string stateId)
        {
            _currentStateId = stateId;
        }
    }
}
