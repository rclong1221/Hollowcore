/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Editor.Inspectors.Character.Abilities
{
	using Opsive.Shared.Editor.UIElements.Controls;
	using Opsive.UltimateCharacterController.Editor.Controls.Types.AbilityDrawers;
	using Opsive.UltimateCharacterController.Editor.Utility;
	using UnityEditor;
	using UnityEditor.Animations;
	using UnityEngine;

	/// <summary>
	/// Draws a custom inspector for the Drown Ability.
	/// </summary>
	[ControlType(typeof(Opsive.UltimateCharacterController.AddOns.Swimming.Drown))]
	public class DrownDrawer : AbilityDrawer
	{
		// ------------------------------------------- Start Generated Code -------------------------------------------
		// ------- Do NOT make any changes below. Changes will be removed when the animator is generated again. -------
		// ------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Returns true if the ability can build to the animator.
		/// </summary>
		public override bool CanBuildAnimator { get { return true; } }

		/// <summary>
		/// An editor only method which can add the abilities states/transitions to the animator.
		/// </summary>
		/// <param name="animatorControllers">The Animator Controllers to add the states to.</param>
		/// <param name="firstPersonAnimatorControllers">The first person Animator Controllers to add the states to.</param>
		public override void BuildAnimator(AnimatorController[] animatorControllers, AnimatorController[] firstPersonAnimatorControllers)
		{
			for (int i = 0; i < animatorControllers.Length; ++i) {
				if (animatorControllers[i].layers.Length <= 0) {
					Debug.LogWarning("Warning: The animator controller does not contain the same number of layers as the demo animator. All of the animations cannot be added.");
					return;
				}

				var baseStateMachine1404471404 = animatorControllers[i].layers[0].stateMachine;

				// The state machine should start fresh.
				for (int j = 0; j < animatorControllers[i].layers.Length; ++j) {
					for (int k = 0; k < baseStateMachine1404471404.stateMachines.Length; ++k) {
						if (baseStateMachine1404471404.stateMachines[k].stateMachine.name == "Drown") {
							baseStateMachine1404471404.RemoveStateMachine(baseStateMachine1404471404.stateMachines[k].stateMachine);
							break;
						}
					}
				}

				// AnimationClip references.
				var drowningAnimationClip27098Path = AssetDatabase.GUIDToAssetPath("6e27b67dcdb0ced43a37b38895376787"); 
				var drowningAnimationClip27098 = AnimatorBuilder.GetAnimationClip(drowningAnimationClip27098Path, "Drowning");

				// State Machine.
				var drownAnimatorStateMachine185676 = baseStateMachine1404471404.AddStateMachine("Drown", new Vector3(630f, 160f, 0f));

				// States.
				var drownAnimatorState186170 = drownAnimatorStateMachine185676.AddState("Drown", new Vector3(155.1106f, -25.15768f, 0f));
				drownAnimatorState186170.motion = drowningAnimationClip27098;
				drownAnimatorState186170.cycleOffset = 0f;
				drownAnimatorState186170.cycleOffsetParameterActive = false;
				drownAnimatorState186170.iKOnFeet = false;
				drownAnimatorState186170.mirror = false;
				drownAnimatorState186170.mirrorParameterActive = false;
				drownAnimatorState186170.speed = 1f;
				drownAnimatorState186170.speedParameterActive = false;
				drownAnimatorState186170.writeDefaultValues = true;

				// State Machine Defaults.
				drownAnimatorStateMachine185676.anyStatePosition = new Vector3(-168f, 48f, 0f);
				drownAnimatorStateMachine185676.defaultState = drownAnimatorState186170;
				drownAnimatorStateMachine185676.entryPosition = new Vector3(-204f, -36f, 0f);
				drownAnimatorStateMachine185676.exitPosition = new Vector3(580f, 40f, 0f);
				drownAnimatorStateMachine185676.parentStateMachinePosition = new Vector3(570f, -50f, 0f);

				// State Transitions.
				var animatorStateTransition186710 = drownAnimatorState186170.AddExitTransition();
				animatorStateTransition186710.canTransitionToSelf = true;
				animatorStateTransition186710.duration = 0.25f;
				animatorStateTransition186710.exitTime = 0.9555556f;
				animatorStateTransition186710.hasExitTime = false;
				animatorStateTransition186710.hasFixedDuration = true;
				animatorStateTransition186710.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition186710.offset = 0f;
				animatorStateTransition186710.orderedInterruption = true;
				animatorStateTransition186710.isExit = true;
				animatorStateTransition186710.mute = false;
				animatorStateTransition186710.solo = false;
				animatorStateTransition186710.AddCondition(AnimatorConditionMode.NotEqual, 304f, "AbilityIndex");

				// State Machine Transitions.
				var animatorStateTransition186650 = baseStateMachine1404471404.AddAnyStateTransition(drownAnimatorState186170);
				animatorStateTransition186650.canTransitionToSelf = true;
				animatorStateTransition186650.duration = 0.25f;
				animatorStateTransition186650.exitTime = 0.75f;
				animatorStateTransition186650.hasExitTime = false;
				animatorStateTransition186650.hasFixedDuration = true;
				animatorStateTransition186650.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition186650.offset = 0f;
				animatorStateTransition186650.orderedInterruption = true;
				animatorStateTransition186650.isExit = false;
				animatorStateTransition186650.mute = false;
				animatorStateTransition186650.solo = false;
				animatorStateTransition186650.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition186650.AddCondition(AnimatorConditionMode.Equals, 304f, "AbilityIndex");
			}
		}
	}
}
