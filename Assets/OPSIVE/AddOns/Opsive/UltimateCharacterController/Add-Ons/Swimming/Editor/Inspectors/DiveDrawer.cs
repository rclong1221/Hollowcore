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
	/// Draws a custom inspector for the Dive Ability.
	/// </summary>
	[ControlType(typeof(Opsive.UltimateCharacterController.AddOns.Swimming.Dive))]
	public class DiveDrawer : AbilityDrawer
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

				var baseStateMachine1458930018 = animatorControllers[i].layers[0].stateMachine;

				// The state machine should start fresh.
				for (int j = 0; j < animatorControllers[i].layers.Length; ++j) {
					for (int k = 0; k < baseStateMachine1458930018.stateMachines.Length; ++k) {
						if (baseStateMachine1458930018.stateMachines[k].stateMachine.name == "Dive") {
							baseStateMachine1458930018.RemoveStateMachine(baseStateMachine1458930018.stateMachines[k].stateMachine);
							break;
						}
					}
				}

				// AnimationClip references.
				var shallowDiveStartAnimationClip27050Path = AssetDatabase.GUIDToAssetPath("a4bd809109678b74d89a0d193252fdb5"); 
				var shallowDiveStartAnimationClip27050 = AnimatorBuilder.GetAnimationClip(shallowDiveStartAnimationClip27050Path, "ShallowDiveStart");
				var shallowDiveEnterWaterAnimationClip27054Path = AssetDatabase.GUIDToAssetPath("a4bd809109678b74d89a0d193252fdb5"); 
				var shallowDiveEnterWaterAnimationClip27054 = AnimatorBuilder.GetAnimationClip(shallowDiveEnterWaterAnimationClip27054Path, "ShallowDiveEnterWater");
				var shallowDiveEndAnimationClip27058Path = AssetDatabase.GUIDToAssetPath("a4bd809109678b74d89a0d193252fdb5"); 
				var shallowDiveEndAnimationClip27058 = AnimatorBuilder.GetAnimationClip(shallowDiveEndAnimationClip27058Path, "ShallowDiveEnd");
				var shallowDiveFallAnimationClip27062Path = AssetDatabase.GUIDToAssetPath("a4bd809109678b74d89a0d193252fdb5"); 
				var shallowDiveFallAnimationClip27062 = AnimatorBuilder.GetAnimationClip(shallowDiveFallAnimationClip27062Path, "ShallowDiveFall");
				var highDiveEndAnimationClip27072Path = AssetDatabase.GUIDToAssetPath("5203dd6ff2f0f4742a81f21778d48d5e"); 
				var highDiveEndAnimationClip27072 = AnimatorBuilder.GetAnimationClip(highDiveEndAnimationClip27072Path, "HighDiveEnd");
				var highDiveEnterWaterAnimationClip27076Path = AssetDatabase.GUIDToAssetPath("5203dd6ff2f0f4742a81f21778d48d5e"); 
				var highDiveEnterWaterAnimationClip27076 = AnimatorBuilder.GetAnimationClip(highDiveEnterWaterAnimationClip27076Path, "HighDiveEnterWater");
				var highDiveStartAnimationClip27082Path = AssetDatabase.GUIDToAssetPath("5203dd6ff2f0f4742a81f21778d48d5e"); 
				var highDiveStartAnimationClip27082 = AnimatorBuilder.GetAnimationClip(highDiveStartAnimationClip27082Path, "HighDiveStart");
				var highDiveFallAnimationClip27086Path = AssetDatabase.GUIDToAssetPath("5203dd6ff2f0f4742a81f21778d48d5e"); 
				var highDiveFallAnimationClip27086 = AnimatorBuilder.GetAnimationClip(highDiveFallAnimationClip27086Path, "HighDiveFall");

				// State Machine.
				var diveAnimatorStateMachine23622 = baseStateMachine1458930018.AddStateMachine("Dive", new Vector3(624f, 60f, 0f));

				// State Machine.
				var shallowDiveAnimatorStateMachine27036 = diveAnimatorStateMachine23622.AddStateMachine("Shallow Dive", new Vector3(384f, 0f, 0f));

				// States.
				var diveStartAnimatorState24450 = shallowDiveAnimatorStateMachine27036.AddState("Dive Start", new Vector3(504f, -168f, 0f));
				diveStartAnimatorState24450.motion = shallowDiveStartAnimationClip27050;
				diveStartAnimatorState24450.cycleOffset = 0f;
				diveStartAnimatorState24450.cycleOffsetParameterActive = false;
				diveStartAnimatorState24450.iKOnFeet = false;
				diveStartAnimatorState24450.mirror = false;
				diveStartAnimatorState24450.mirrorParameterActive = false;
				diveStartAnimatorState24450.speed = 3f;
				diveStartAnimatorState24450.speedParameterActive = false;
				diveStartAnimatorState24450.writeDefaultValues = true;

				var enterWaterAnimatorState27040 = shallowDiveAnimatorStateMachine27036.AddState("Enter Water", new Vector3(504f, 36f, 0f));
				enterWaterAnimatorState27040.motion = shallowDiveEnterWaterAnimationClip27054;
				enterWaterAnimatorState27040.cycleOffset = 0f;
				enterWaterAnimatorState27040.cycleOffsetParameterActive = false;
				enterWaterAnimatorState27040.iKOnFeet = false;
				enterWaterAnimatorState27040.mirror = false;
				enterWaterAnimatorState27040.mirrorParameterActive = false;
				enterWaterAnimatorState27040.speed = 1f;
				enterWaterAnimatorState27040.speedParameterActive = false;
				enterWaterAnimatorState27040.writeDefaultValues = true;

				var endAnimatorState27042 = shallowDiveAnimatorStateMachine27036.AddState("End", new Vector3(852f, 36f, 0f));
				endAnimatorState27042.motion = shallowDiveEndAnimationClip27058;
				endAnimatorState27042.cycleOffset = 0f;
				endAnimatorState27042.cycleOffsetParameterActive = false;
				endAnimatorState27042.iKOnFeet = false;
				endAnimatorState27042.mirror = false;
				endAnimatorState27042.mirrorParameterActive = false;
				endAnimatorState27042.speed = 0.5f;
				endAnimatorState27042.speedParameterActive = false;
				endAnimatorState27042.writeDefaultValues = true;

				var diveFallAnimatorState27044 = shallowDiveAnimatorStateMachine27036.AddState("Dive Fall", new Vector3(744f, -72f, 0f));
				diveFallAnimatorState27044.motion = shallowDiveFallAnimationClip27062;
				diveFallAnimatorState27044.cycleOffset = 0f;
				diveFallAnimatorState27044.cycleOffsetParameterActive = false;
				diveFallAnimatorState27044.iKOnFeet = false;
				diveFallAnimatorState27044.mirror = false;
				diveFallAnimatorState27044.mirrorParameterActive = false;
				diveFallAnimatorState27044.speed = 1f;
				diveFallAnimatorState27044.speedParameterActive = false;
				diveFallAnimatorState27044.writeDefaultValues = true;

				// State Machine Defaults.
				shallowDiveAnimatorStateMachine27036.anyStatePosition = new Vector3(36f, 48f, 0f);
				shallowDiveAnimatorStateMachine27036.defaultState = diveStartAnimatorState24450;
				shallowDiveAnimatorStateMachine27036.entryPosition = new Vector3(48f, -48f, 0f);
				shallowDiveAnimatorStateMachine27036.exitPosition = new Vector3(1116f, 48f, 0f);
				shallowDiveAnimatorStateMachine27036.parentStateMachinePosition = new Vector3(1104f, -48f, 0f);

				// State Machine.
				var highDiveAnimatorStateMachine27038 = diveAnimatorStateMachine23622.AddStateMachine("High Dive", new Vector3(384f, 72f, 0f));

				// States.
				var endAnimatorState27064 = highDiveAnimatorStateMachine27038.AddState("End", new Vector3(564f, 204f, 0f));
				endAnimatorState27064.motion = highDiveEndAnimationClip27072;
				endAnimatorState27064.cycleOffset = 0f;
				endAnimatorState27064.cycleOffsetParameterActive = false;
				endAnimatorState27064.iKOnFeet = false;
				endAnimatorState27064.mirror = false;
				endAnimatorState27064.mirrorParameterActive = false;
				endAnimatorState27064.speed = 0.5f;
				endAnimatorState27064.speedParameterActive = false;
				endAnimatorState27064.writeDefaultValues = true;

				var enterWaterAnimatorState27066 = highDiveAnimatorStateMachine27038.AddState("Enter Water", new Vector3(264f, 204f, 0f));
				enterWaterAnimatorState27066.motion = highDiveEnterWaterAnimationClip27076;
				enterWaterAnimatorState27066.cycleOffset = 0f;
				enterWaterAnimatorState27066.cycleOffsetParameterActive = false;
				enterWaterAnimatorState27066.iKOnFeet = false;
				enterWaterAnimatorState27066.mirror = false;
				enterWaterAnimatorState27066.mirrorParameterActive = false;
				enterWaterAnimatorState27066.speed = 1f;
				enterWaterAnimatorState27066.speedParameterActive = false;
				enterWaterAnimatorState27066.writeDefaultValues = true;

				var diveStartAnimatorState24452 = highDiveAnimatorStateMachine27038.AddState("Dive Start", new Vector3(264f, 0f, 0f));
				diveStartAnimatorState24452.motion = highDiveStartAnimationClip27082;
				diveStartAnimatorState24452.cycleOffset = 0f;
				diveStartAnimatorState24452.cycleOffsetParameterActive = false;
				diveStartAnimatorState24452.iKOnFeet = false;
				diveStartAnimatorState24452.mirror = false;
				diveStartAnimatorState24452.mirrorParameterActive = false;
				diveStartAnimatorState24452.speed = 3f;
				diveStartAnimatorState24452.speedParameterActive = false;
				diveStartAnimatorState24452.writeDefaultValues = true;

				var diveFallAnimatorState27068 = highDiveAnimatorStateMachine27038.AddState("Dive Fall", new Vector3(384f, 96f, 0f));
				diveFallAnimatorState27068.motion = highDiveFallAnimationClip27086;
				diveFallAnimatorState27068.cycleOffset = 0f;
				diveFallAnimatorState27068.cycleOffsetParameterActive = false;
				diveFallAnimatorState27068.iKOnFeet = false;
				diveFallAnimatorState27068.mirror = false;
				diveFallAnimatorState27068.mirrorParameterActive = false;
				diveFallAnimatorState27068.speed = 1f;
				diveFallAnimatorState27068.speedParameterActive = false;
				diveFallAnimatorState27068.writeDefaultValues = true;

				// State Machine Defaults.
				highDiveAnimatorStateMachine27038.anyStatePosition = new Vector3(36f, 168f, 0f);
				highDiveAnimatorStateMachine27038.defaultState = diveStartAnimatorState24452;
				highDiveAnimatorStateMachine27038.entryPosition = new Vector3(36f, 84f, 0f);
				highDiveAnimatorStateMachine27038.exitPosition = new Vector3(888f, 168f, 0f);
				highDiveAnimatorStateMachine27038.parentStateMachinePosition = new Vector3(864f, 108f, 0f);

				// State Machine Defaults.
				diveAnimatorStateMachine23622.anyStatePosition = new Vector3(50f, 20f, 0f);
				diveAnimatorStateMachine23622.defaultState = diveStartAnimatorState24450;
				diveAnimatorStateMachine23622.entryPosition = new Vector3(50f, 120f, 0f);
				diveAnimatorStateMachine23622.exitPosition = new Vector3(800f, 120f, 0f);
				diveAnimatorStateMachine23622.parentStateMachinePosition = new Vector3(800f, 20f, 0f);

				// State Transitions.
				var animatorStateTransition27046 = diveStartAnimatorState24450.AddTransition(enterWaterAnimatorState27040);
				animatorStateTransition27046.canTransitionToSelf = true;
				animatorStateTransition27046.duration = 0.25f;
				animatorStateTransition27046.exitTime = 0.8f;
				animatorStateTransition27046.hasExitTime = true;
				animatorStateTransition27046.hasFixedDuration = true;
				animatorStateTransition27046.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27046.offset = 0f;
				animatorStateTransition27046.orderedInterruption = true;
				animatorStateTransition27046.isExit = false;
				animatorStateTransition27046.mute = false;
				animatorStateTransition27046.solo = false;
				animatorStateTransition27046.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");

				var animatorStateTransition27048 = diveStartAnimatorState24450.AddTransition(diveFallAnimatorState27044);
				animatorStateTransition27048.canTransitionToSelf = true;
				animatorStateTransition27048.duration = 0.25f;
				animatorStateTransition27048.exitTime = 0.8f;
				animatorStateTransition27048.hasExitTime = true;
				animatorStateTransition27048.hasFixedDuration = true;
				animatorStateTransition27048.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27048.offset = 0f;
				animatorStateTransition27048.orderedInterruption = true;
				animatorStateTransition27048.isExit = false;
				animatorStateTransition27048.mute = false;
				animatorStateTransition27048.solo = false;
				animatorStateTransition27048.AddCondition(AnimatorConditionMode.NotEqual, 2f, "AbilityIntData");

				var animatorStateTransition27052 = enterWaterAnimatorState27040.AddTransition(endAnimatorState27042);
				animatorStateTransition27052.canTransitionToSelf = true;
				animatorStateTransition27052.duration = 0.05f;
				animatorStateTransition27052.exitTime = 0.9f;
				animatorStateTransition27052.hasExitTime = true;
				animatorStateTransition27052.hasFixedDuration = true;
				animatorStateTransition27052.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27052.offset = 0f;
				animatorStateTransition27052.orderedInterruption = true;
				animatorStateTransition27052.isExit = false;
				animatorStateTransition27052.mute = false;
				animatorStateTransition27052.solo = false;

				var animatorStateTransition27056 = endAnimatorState27042.AddExitTransition();
				animatorStateTransition27056.canTransitionToSelf = true;
				animatorStateTransition27056.duration = 0.15f;
				animatorStateTransition27056.exitTime = 0.8f;
				animatorStateTransition27056.hasExitTime = true;
				animatorStateTransition27056.hasFixedDuration = true;
				animatorStateTransition27056.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27056.offset = 0f;
				animatorStateTransition27056.orderedInterruption = true;
				animatorStateTransition27056.isExit = true;
				animatorStateTransition27056.mute = false;
				animatorStateTransition27056.solo = false;
				animatorStateTransition27056.AddCondition(AnimatorConditionMode.NotEqual, 302f, "AbilityIndex");

				var animatorStateTransition27060 = diveFallAnimatorState27044.AddTransition(enterWaterAnimatorState27040);
				animatorStateTransition27060.canTransitionToSelf = true;
				animatorStateTransition27060.duration = 0.015f;
				animatorStateTransition27060.exitTime = 0.4000001f;
				animatorStateTransition27060.hasExitTime = false;
				animatorStateTransition27060.hasFixedDuration = true;
				animatorStateTransition27060.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27060.offset = 0f;
				animatorStateTransition27060.orderedInterruption = true;
				animatorStateTransition27060.isExit = false;
				animatorStateTransition27060.mute = false;
				animatorStateTransition27060.solo = false;
				animatorStateTransition27060.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");

				// State Transitions.
				var animatorStateTransition27070 = endAnimatorState27064.AddExitTransition();
				animatorStateTransition27070.canTransitionToSelf = true;
				animatorStateTransition27070.duration = 0.15f;
				animatorStateTransition27070.exitTime = 0.8f;
				animatorStateTransition27070.hasExitTime = false;
				animatorStateTransition27070.hasFixedDuration = true;
				animatorStateTransition27070.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27070.offset = 0f;
				animatorStateTransition27070.orderedInterruption = true;
				animatorStateTransition27070.isExit = true;
				animatorStateTransition27070.mute = false;
				animatorStateTransition27070.solo = false;
				animatorStateTransition27070.AddCondition(AnimatorConditionMode.NotEqual, 302f, "AbilityIndex");

				var animatorStateTransition27074 = enterWaterAnimatorState27066.AddTransition(endAnimatorState27064);
				animatorStateTransition27074.canTransitionToSelf = true;
				animatorStateTransition27074.duration = 0.05f;
				animatorStateTransition27074.exitTime = 0.9f;
				animatorStateTransition27074.hasExitTime = true;
				animatorStateTransition27074.hasFixedDuration = true;
				animatorStateTransition27074.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27074.offset = 0f;
				animatorStateTransition27074.orderedInterruption = true;
				animatorStateTransition27074.isExit = false;
				animatorStateTransition27074.mute = false;
				animatorStateTransition27074.solo = false;

				var animatorStateTransition27078 = diveStartAnimatorState24452.AddTransition(enterWaterAnimatorState27066);
				animatorStateTransition27078.canTransitionToSelf = true;
				animatorStateTransition27078.duration = 0.25f;
				animatorStateTransition27078.exitTime = 0.8f;
				animatorStateTransition27078.hasExitTime = true;
				animatorStateTransition27078.hasFixedDuration = true;
				animatorStateTransition27078.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27078.offset = 0f;
				animatorStateTransition27078.orderedInterruption = true;
				animatorStateTransition27078.isExit = false;
				animatorStateTransition27078.mute = false;
				animatorStateTransition27078.solo = false;
				animatorStateTransition27078.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");

				var animatorStateTransition27080 = diveStartAnimatorState24452.AddTransition(diveFallAnimatorState27068);
				animatorStateTransition27080.canTransitionToSelf = true;
				animatorStateTransition27080.duration = 0.5f;
				animatorStateTransition27080.exitTime = 0.8f;
				animatorStateTransition27080.hasExitTime = true;
				animatorStateTransition27080.hasFixedDuration = true;
				animatorStateTransition27080.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27080.offset = 0f;
				animatorStateTransition27080.orderedInterruption = true;
				animatorStateTransition27080.isExit = false;
				animatorStateTransition27080.mute = false;
				animatorStateTransition27080.solo = false;
				animatorStateTransition27080.AddCondition(AnimatorConditionMode.NotEqual, 2f, "AbilityIntData");

				var animatorStateTransition27084 = diveFallAnimatorState27068.AddTransition(enterWaterAnimatorState27066);
				animatorStateTransition27084.canTransitionToSelf = true;
				animatorStateTransition27084.duration = 0.015f;
				animatorStateTransition27084.exitTime = 0.75f;
				animatorStateTransition27084.hasExitTime = false;
				animatorStateTransition27084.hasFixedDuration = true;
				animatorStateTransition27084.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition27084.offset = 0f;
				animatorStateTransition27084.orderedInterruption = true;
				animatorStateTransition27084.isExit = false;
				animatorStateTransition27084.mute = false;
				animatorStateTransition27084.solo = false;
				animatorStateTransition27084.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");

				// State Machine Transitions.
				var animatorStateTransition24160 = baseStateMachine1458930018.AddAnyStateTransition(diveStartAnimatorState24450);
				animatorStateTransition24160.canTransitionToSelf = false;
				animatorStateTransition24160.duration = 0.15f;
				animatorStateTransition24160.exitTime = 0.75f;
				animatorStateTransition24160.hasExitTime = false;
				animatorStateTransition24160.hasFixedDuration = true;
				animatorStateTransition24160.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition24160.offset = 0f;
				animatorStateTransition24160.orderedInterruption = true;
				animatorStateTransition24160.isExit = false;
				animatorStateTransition24160.mute = false;
				animatorStateTransition24160.solo = false;
				animatorStateTransition24160.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition24160.AddCondition(AnimatorConditionMode.Equals, 302f, "AbilityIndex");
				animatorStateTransition24160.AddCondition(AnimatorConditionMode.NotEqual, 1f, "AbilityIntData");
				animatorStateTransition24160.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");

				var animatorStateTransition24162 = baseStateMachine1458930018.AddAnyStateTransition(diveStartAnimatorState24452);
				animatorStateTransition24162.canTransitionToSelf = false;
				animatorStateTransition24162.duration = 0.15f;
				animatorStateTransition24162.exitTime = 0.75f;
				animatorStateTransition24162.hasExitTime = false;
				animatorStateTransition24162.hasFixedDuration = true;
				animatorStateTransition24162.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition24162.offset = 0f;
				animatorStateTransition24162.orderedInterruption = true;
				animatorStateTransition24162.isExit = false;
				animatorStateTransition24162.mute = false;
				animatorStateTransition24162.solo = false;
				animatorStateTransition24162.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition24162.AddCondition(AnimatorConditionMode.Equals, 302f, "AbilityIndex");
				animatorStateTransition24162.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition24162.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");

				var animatorStateTransition24174 = baseStateMachine1458930018.AddAnyStateTransition(diveStartAnimatorState24452);
				animatorStateTransition24174.canTransitionToSelf = false;
				animatorStateTransition24174.duration = 0.1f;
				animatorStateTransition24174.exitTime = 0.75f;
				animatorStateTransition24174.hasExitTime = false;
				animatorStateTransition24174.hasFixedDuration = true;
				animatorStateTransition24174.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition24174.offset = 0.5f;
				animatorStateTransition24174.orderedInterruption = true;
				animatorStateTransition24174.isExit = false;
				animatorStateTransition24174.mute = false;
				animatorStateTransition24174.solo = false;
				animatorStateTransition24174.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition24174.AddCondition(AnimatorConditionMode.Equals, 302f, "AbilityIndex");
				animatorStateTransition24174.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition24174.AddCondition(AnimatorConditionMode.If, 0f, "Moving");

				var animatorStateTransition24176 = baseStateMachine1458930018.AddAnyStateTransition(diveStartAnimatorState24450);
				animatorStateTransition24176.canTransitionToSelf = false;
				animatorStateTransition24176.duration = 0.15f;
				animatorStateTransition24176.exitTime = 0.75f;
				animatorStateTransition24176.hasExitTime = false;
				animatorStateTransition24176.hasFixedDuration = true;
				animatorStateTransition24176.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition24176.offset = 0.5f;
				animatorStateTransition24176.orderedInterruption = true;
				animatorStateTransition24176.isExit = false;
				animatorStateTransition24176.mute = false;
				animatorStateTransition24176.solo = false;
				animatorStateTransition24176.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition24176.AddCondition(AnimatorConditionMode.Equals, 302f, "AbilityIndex");
				animatorStateTransition24176.AddCondition(AnimatorConditionMode.NotEqual, 1f, "AbilityIntData");
				animatorStateTransition24176.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
			}
		}
	}
}
