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
	/// Draws a custom inspector for the ClimbFromWater Ability.
	/// </summary>
	[ControlType(typeof(Opsive.UltimateCharacterController.AddOns.Swimming.ClimbFromWater))]
	public class ClimbFromWaterDrawer : DetectObjectAbilityBaseDrawer
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

				var baseStateMachine1610947154 = animatorControllers[i].layers[0].stateMachine;

				// The state machine should start fresh.
				for (int j = 0; j < animatorControllers[i].layers.Length; ++j) {
					for (int k = 0; k < baseStateMachine1610947154.stateMachines.Length; ++k) {
						if (baseStateMachine1610947154.stateMachines[k].stateMachine.name == "Climb From Water") {
							baseStateMachine1610947154.RemoveStateMachine(baseStateMachine1610947154.stateMachines[k].stateMachine);
							break;
						}
					}
				}

				// AnimationClip references.
				var swimClimbFromWaterMovingAnimationClip148146Path = AssetDatabase.GUIDToAssetPath("0a3989f9b05f2eb4e97f108cb17cf1c1"); 
				var swimClimbFromWaterMovingAnimationClip148146 = AnimatorBuilder.GetAnimationClip(swimClimbFromWaterMovingAnimationClip148146Path, "SwimClimbFromWaterMoving");
				var swimClimbFromWaterIdleAnimationClip148144Path = AssetDatabase.GUIDToAssetPath("0a3989f9b05f2eb4e97f108cb17cf1c1"); 
				var swimClimbFromWaterIdleAnimationClip148144 = AnimatorBuilder.GetAnimationClip(swimClimbFromWaterIdleAnimationClip148144Path, "SwimClimbFromWaterIdle");

				// State Machine.
				var climbFromWaterAnimatorStateMachine339334 = baseStateMachine1610947154.AddStateMachine("Climb From Water", new Vector3(624f, 12f, 0f));

				// States.
				var climbFromWaterMovingAnimatorState340162 = climbFromWaterAnimatorStateMachine339334.AddState("Climb From Water Moving", new Vector3(384f, 72f, 0f));
				climbFromWaterMovingAnimatorState340162.motion = swimClimbFromWaterMovingAnimationClip148146;
				climbFromWaterMovingAnimatorState340162.cycleOffset = 0f;
				climbFromWaterMovingAnimatorState340162.cycleOffsetParameterActive = false;
				climbFromWaterMovingAnimatorState340162.iKOnFeet = true;
				climbFromWaterMovingAnimatorState340162.mirror = false;
				climbFromWaterMovingAnimatorState340162.mirrorParameterActive = false;
				climbFromWaterMovingAnimatorState340162.speed = 2f;
				climbFromWaterMovingAnimatorState340162.speedParameterActive = false;
				climbFromWaterMovingAnimatorState340162.writeDefaultValues = true;

				var climbFromWaterIdleAnimatorState340164 = climbFromWaterAnimatorStateMachine339334.AddState("Climb From Water Idle", new Vector3(384f, 12f, 0f));
				climbFromWaterIdleAnimatorState340164.motion = swimClimbFromWaterIdleAnimationClip148144;
				climbFromWaterIdleAnimatorState340164.cycleOffset = 0f;
				climbFromWaterIdleAnimatorState340164.cycleOffsetParameterActive = false;
				climbFromWaterIdleAnimatorState340164.iKOnFeet = true;
				climbFromWaterIdleAnimatorState340164.mirror = false;
				climbFromWaterIdleAnimatorState340164.mirrorParameterActive = false;
				climbFromWaterIdleAnimatorState340164.speed = 2f;
				climbFromWaterIdleAnimatorState340164.speedParameterActive = false;
				climbFromWaterIdleAnimatorState340164.writeDefaultValues = true;

				// State Machine Defaults.
				climbFromWaterAnimatorStateMachine339334.anyStatePosition = new Vector3(-96f, 36f, 0f);
				climbFromWaterAnimatorStateMachine339334.defaultState = climbFromWaterMovingAnimatorState340162;
				climbFromWaterAnimatorStateMachine339334.entryPosition = new Vector3(-96f, -36f, 0f);
				climbFromWaterAnimatorStateMachine339334.exitPosition = new Vector3(876f, 36f, 0f);
				climbFromWaterAnimatorStateMachine339334.parentStateMachinePosition = new Vector3(864f, -60f, 0f);

				// State Transitions.
				var animatorStateTransition341406 = climbFromWaterMovingAnimatorState340162.AddExitTransition();
				animatorStateTransition341406.canTransitionToSelf = true;
				animatorStateTransition341406.duration = 0.25f;
				animatorStateTransition341406.exitTime = 0.9f;
				animatorStateTransition341406.hasExitTime = false;
				animatorStateTransition341406.hasFixedDuration = true;
				animatorStateTransition341406.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition341406.offset = 0f;
				animatorStateTransition341406.orderedInterruption = true;
				animatorStateTransition341406.isExit = true;
				animatorStateTransition341406.mute = false;
				animatorStateTransition341406.solo = false;
				animatorStateTransition341406.AddCondition(AnimatorConditionMode.NotEqual, 303f, "AbilityIndex");

				var animatorStateTransition341408 = climbFromWaterIdleAnimatorState340164.AddExitTransition();
				animatorStateTransition341408.canTransitionToSelf = true;
				animatorStateTransition341408.duration = 0.25f;
				animatorStateTransition341408.exitTime = 0.9f;
				animatorStateTransition341408.hasExitTime = false;
				animatorStateTransition341408.hasFixedDuration = true;
				animatorStateTransition341408.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition341408.offset = 0f;
				animatorStateTransition341408.orderedInterruption = true;
				animatorStateTransition341408.isExit = true;
				animatorStateTransition341408.mute = false;
				animatorStateTransition341408.solo = false;
				animatorStateTransition341408.AddCondition(AnimatorConditionMode.NotEqual, 303f, "AbilityIndex");

				// State Machine Transitions.
				var animatorStateTransition339876 = baseStateMachine1610947154.AddAnyStateTransition(climbFromWaterMovingAnimatorState340162);
				animatorStateTransition339876.canTransitionToSelf = false;
				animatorStateTransition339876.duration = 0.05f;
				animatorStateTransition339876.exitTime = 0.75f;
				animatorStateTransition339876.hasExitTime = false;
				animatorStateTransition339876.hasFixedDuration = true;
				animatorStateTransition339876.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition339876.offset = 0f;
				animatorStateTransition339876.orderedInterruption = true;
				animatorStateTransition339876.isExit = false;
				animatorStateTransition339876.mute = false;
				animatorStateTransition339876.solo = false;
				animatorStateTransition339876.AddCondition(AnimatorConditionMode.Equals, 303f, "AbilityIndex");
				animatorStateTransition339876.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");

				var animatorStateTransition339878 = baseStateMachine1610947154.AddAnyStateTransition(climbFromWaterIdleAnimatorState340164);
				animatorStateTransition339878.canTransitionToSelf = false;
				animatorStateTransition339878.duration = 0.05f;
				animatorStateTransition339878.exitTime = 0.75f;
				animatorStateTransition339878.hasExitTime = false;
				animatorStateTransition339878.hasFixedDuration = true;
				animatorStateTransition339878.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition339878.offset = 0f;
				animatorStateTransition339878.orderedInterruption = true;
				animatorStateTransition339878.isExit = false;
				animatorStateTransition339878.mute = false;
				animatorStateTransition339878.solo = false;
				animatorStateTransition339878.AddCondition(AnimatorConditionMode.Equals, 303f, "AbilityIndex");
				animatorStateTransition339878.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
			}
		}
	}
}
