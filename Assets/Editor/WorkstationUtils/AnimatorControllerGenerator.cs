using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.Utils
{
    /// <summary>
    /// Helper class for generating Animator Controller assets and states.
    /// Simplified interface for the UnityEditor.Animations API.
    /// </summary>
    public static class AnimatorControllerGenerator
    {
        /// <summary>
        /// Finds or creates a sub-state machine within the root state machine.
        /// </summary>
        public static AnimatorStateMachine GetOrCreateSubStateMachine(AnimatorStateMachine root, string name)
        {
            // Check existing
            foreach (var childStateMachine in root.stateMachines)
            {
                if (childStateMachine.stateMachine.name == name)
                    return childStateMachine.stateMachine;
            }

            // Create new
            return root.AddStateMachine(name);
        }

        /// <summary>
        /// Adds a state to a state machine with a specific motion (clip).
        /// </summary>
        public static AnimatorState AddStateWithClip(AnimatorStateMachine stateMachine, string stateName, Motion clip)
        {
            // Check if exists
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == stateName)
                {
                    // Update clip if exists
                    childState.state.motion = clip;
                    return childState.state;
                }
            }

            // Create new
            var state = stateMachine.AddState(stateName);
            state.motion = clip;
            return state;
        }

        /// <summary>
        /// Creates a transition between two states with a specific condition.
        /// </summary>
        public static AnimatorStateTransition AddTransition(AnimatorState fromState, AnimatorState toState, 
            AnimatorConditionMode mode = AnimatorConditionMode.If, string parameter = "", float threshold = 0)
        {
            // Check for existing transition to avoid duplicates
            foreach (var transition in fromState.transitions)
            {
                if (transition.destinationState == toState)
                    return transition;
            }

            var newTransition = fromState.AddTransition(toState);
            
            if (!string.IsNullOrEmpty(parameter))
            {
                newTransition.AddCondition(mode, threshold, parameter);
            }

            return newTransition;
        }
        
        /// <summary>
        /// Ensures a parameter exists in the controller.
        /// </summary>
        public static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var param in controller.parameters)
            {
                if (param.name == name && param.type == type)
                    return;
            }
            
            controller.AddParameter(name, type);
        }
    }
}
