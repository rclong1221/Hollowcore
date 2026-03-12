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
	/// Draws a custom inspector for the Swim Ability.
	/// </summary>
	[ControlType(typeof(Opsive.UltimateCharacterController.AddOns.Swimming.Swim))]
	public class SwimDrawer : DetectObjectAbilityBaseDrawer
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

				var baseStateMachine733959252 = animatorControllers[i].layers[0].stateMachine;

				// The state machine should start fresh.
				for (int j = 0; j < animatorControllers[i].layers.Length; ++j) {
					for (int k = 0; k < baseStateMachine733959252.stateMachines.Length; ++k) {
						if (baseStateMachine733959252.stateMachines[k].stateMachine.name == "Swim") {
							baseStateMachine733959252.RemoveStateMachine(baseStateMachine733959252.stateMachines[k].stateMachine);
							break;
						}
					}
				}

				// AnimationClip references.
				var fallInWaterAnimationClip66518Path = AssetDatabase.GUIDToAssetPath("5417f5795beed3748a4a339e448541f9"); 
				var fallInWaterAnimationClip66518 = AnimatorBuilder.GetAnimationClip(fallInWaterAnimationClip66518Path, "FallInWater");
				var surfaceSwimIdleAnimationClip66530Path = AssetDatabase.GUIDToAssetPath("49a950985203bcd47b45a342dd66617e"); 
				var surfaceSwimIdleAnimationClip66530 = AnimatorBuilder.GetAnimationClip(surfaceSwimIdleAnimationClip66530Path, "SurfaceSwimIdle");
				var surfacePowerSwimBwdAnimationClip66544Path = AssetDatabase.GUIDToAssetPath("656232ee21ea0ff41abf60f04e64858c"); 
				var surfacePowerSwimBwdAnimationClip66544 = AnimatorBuilder.GetAnimationClip(surfacePowerSwimBwdAnimationClip66544Path, "SurfacePowerSwimBwd");
				var surfaceSwimBwdAnimationClip66546Path = AssetDatabase.GUIDToAssetPath("1c6fd722260a32f49b477f2ec339ab20"); 
				var surfaceSwimBwdAnimationClip66546 = AnimatorBuilder.GetAnimationClip(surfaceSwimBwdAnimationClip66546Path, "SurfaceSwimBwd");
				var surfacePowerSwimStrafeAnimationClip66548Path = AssetDatabase.GUIDToAssetPath("bb557eb0c7ebd964b9909d247bf877d3"); 
				var surfacePowerSwimStrafeAnimationClip66548 = AnimatorBuilder.GetAnimationClip(surfacePowerSwimStrafeAnimationClip66548Path, "SurfacePowerSwimStrafe");
				var surfaceSwimStrafeAnimationClip66550Path = AssetDatabase.GUIDToAssetPath("8469ba753dfb3ce4fabe0bab48e638d0"); 
				var surfaceSwimStrafeAnimationClip66550 = AnimatorBuilder.GetAnimationClip(surfaceSwimStrafeAnimationClip66550Path, "SurfaceSwimStrafe");
				var surfaceSwimFwdAnimationClip66552Path = AssetDatabase.GUIDToAssetPath("34888dbddbf44204cb7ce3d920c28fc6"); 
				var surfaceSwimFwdAnimationClip66552 = AnimatorBuilder.GetAnimationClip(surfaceSwimFwdAnimationClip66552Path, "SurfaceSwimFwd");
				var surfacePowerSwimFwdAnimationClip66554Path = AssetDatabase.GUIDToAssetPath("60ee98433976c904fa0f81f9698a3f57"); 
				var surfacePowerSwimFwdAnimationClip66554 = AnimatorBuilder.GetAnimationClip(surfacePowerSwimFwdAnimationClip66554Path, "SurfacePowerSwimFwd");
				var underwaterSwimBwdAnimationClip66568Path = AssetDatabase.GUIDToAssetPath("18ed38574f4ad754c911778975d2963b"); 
				var underwaterSwimBwdAnimationClip66568 = AnimatorBuilder.GetAnimationClip(underwaterSwimBwdAnimationClip66568Path, "UnderwaterSwimBwd");
				var underwaterKickStrafeAnimationClip66570Path = AssetDatabase.GUIDToAssetPath("44eb2f91ce4dd134ca5df47dad8e2128"); 
				var underwaterKickStrafeAnimationClip66570 = AnimatorBuilder.GetAnimationClip(underwaterKickStrafeAnimationClip66570Path, "UnderwaterKickStrafe");
				var underwaterStrokeStrafeAnimationClip66572Path = AssetDatabase.GUIDToAssetPath("1b3960f74d07e0342b8a63b4bb757ca4"); 
				var underwaterStrokeStrafeAnimationClip66572 = AnimatorBuilder.GetAnimationClip(underwaterStrokeStrafeAnimationClip66572Path, "UnderwaterStrokeStrafe");
				var underwaterIdleUpAnimationClip66580Path = AssetDatabase.GUIDToAssetPath("04bb0ea0103e9534d99025bd9c28c33e"); 
				var underwaterIdleUpAnimationClip66580 = AnimatorBuilder.GetAnimationClip(underwaterIdleUpAnimationClip66580Path, "UnderwaterIdleUp");
				var underwaterIdleFwdAnimationClip66582Path = AssetDatabase.GUIDToAssetPath("99c8c8d422f251a48b0dcdae23379ed3"); 
				var underwaterIdleFwdAnimationClip66582 = AnimatorBuilder.GetAnimationClip(underwaterIdleFwdAnimationClip66582Path, "UnderwaterIdleFwd");
				var underwaterIdleDownAnimationClip66584Path = AssetDatabase.GUIDToAssetPath("a55727babd23dbf4c8e77812456885df"); 
				var underwaterIdleDownAnimationClip66584 = AnimatorBuilder.GetAnimationClip(underwaterIdleDownAnimationClip66584Path, "UnderwaterIdleDown");
				var underwaterStrokeUpAnimationClip66586Path = AssetDatabase.GUIDToAssetPath("d8bd72cf843e70642bf0ea85025013ba"); 
				var underwaterStrokeUpAnimationClip66586 = AnimatorBuilder.GetAnimationClip(underwaterStrokeUpAnimationClip66586Path, "UnderwaterStrokeUp");
				var underwaterStrokeFwdAnimationClip66588Path = AssetDatabase.GUIDToAssetPath("eaa27658d6a72d14ca7f62229295b62d"); 
				var underwaterStrokeFwdAnimationClip66588 = AnimatorBuilder.GetAnimationClip(underwaterStrokeFwdAnimationClip66588Path, "UnderwaterStrokeFwd");
				var underwaterStrokeDownAnimationClip66590Path = AssetDatabase.GUIDToAssetPath("52fcf2bd8220b464d8498e0642c27250"); 
				var underwaterStrokeDownAnimationClip66590 = AnimatorBuilder.GetAnimationClip(underwaterStrokeDownAnimationClip66590Path, "UnderwaterStrokeDown");
				var underwaterKickUpAnimationClip66592Path = AssetDatabase.GUIDToAssetPath("426dee5db36de4944860bae40dc080a5"); 
				var underwaterKickUpAnimationClip66592 = AnimatorBuilder.GetAnimationClip(underwaterKickUpAnimationClip66592Path, "UnderwaterKickUp");
				var underwaterKickFwdAnimationClip66594Path = AssetDatabase.GUIDToAssetPath("274cdf2dc4e78834c813439bb16217ee"); 
				var underwaterKickFwdAnimationClip66594 = AnimatorBuilder.GetAnimationClip(underwaterKickFwdAnimationClip66594Path, "UnderwaterKickFwd");
				var underwaterKickDownAnimationClip66596Path = AssetDatabase.GUIDToAssetPath("0c15d136a6ed78940adfecd9eb19afb4"); 
				var underwaterKickDownAnimationClip66596 = AnimatorBuilder.GetAnimationClip(underwaterKickDownAnimationClip66596Path, "UnderwaterKickDown");
				var diveFromSurfaceAnimationClip66600Path = AssetDatabase.GUIDToAssetPath("c49a2659f0fec5d4898d185c3fc51a99"); 
				var diveFromSurfaceAnimationClip66600 = AnimatorBuilder.GetAnimationClip(diveFromSurfaceAnimationClip66600Path, "DiveFromSurface");
				var swimExitWaterAnimationClip66604Path = AssetDatabase.GUIDToAssetPath("4805feacf83e71f4d85fb6721373364b"); 
				var swimExitWaterAnimationClip66604 = AnimatorBuilder.GetAnimationClip(swimExitWaterAnimationClip66604Path, "SwimExitWater");
				var surfaceSwimToIdleAnimationClip66608Path = AssetDatabase.GUIDToAssetPath("59e074ba83e4ebf49bb6bbafee759137"); 
				var surfaceSwimToIdleAnimationClip66608 = AnimatorBuilder.GetAnimationClip(surfaceSwimToIdleAnimationClip66608Path, "SurfaceSwimToIdle");

				// State Machine.
				var swimAnimatorStateMachine64524 = baseStateMachine733959252.AddStateMachine("Swim", new Vector3(630f, 110f, 0f));

				// States.
				var fallInWaterAnimatorState65344 = swimAnimatorStateMachine64524.AddState("Fall In Water", new Vector3(144f, 48f, 0f));
				fallInWaterAnimatorState65344.motion = fallInWaterAnimationClip66518;
				fallInWaterAnimatorState65344.cycleOffset = 0f;
				fallInWaterAnimatorState65344.cycleOffsetParameterActive = false;
				fallInWaterAnimatorState65344.iKOnFeet = false;
				fallInWaterAnimatorState65344.mirror = false;
				fallInWaterAnimatorState65344.mirrorParameterActive = false;
				fallInWaterAnimatorState65344.speed = 1.5f;
				fallInWaterAnimatorState65344.speedParameterActive = false;
				fallInWaterAnimatorState65344.writeDefaultValues = true;

				var surfaceIdleAnimatorState65346 = swimAnimatorStateMachine64524.AddState("Surface Idle", new Vector3(530f, -190f, 0f));
				surfaceIdleAnimatorState65346.motion = surfaceSwimIdleAnimationClip66530;
				surfaceIdleAnimatorState65346.cycleOffset = 0f;
				surfaceIdleAnimatorState65346.cycleOffsetParameterActive = false;
				surfaceIdleAnimatorState65346.iKOnFeet = false;
				surfaceIdleAnimatorState65346.mirror = false;
				surfaceIdleAnimatorState65346.mirrorParameterActive = false;
				surfaceIdleAnimatorState65346.speed = 1f;
				surfaceIdleAnimatorState65346.speedParameterActive = false;
				surfaceIdleAnimatorState65346.writeDefaultValues = true;

				var surfaceSwimAnimatorState65354 = swimAnimatorStateMachine64524.AddState("Surface Swim", new Vector3(230f, -190f, 0f));
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542 = new BlendTree();
				AssetDatabase.AddObjectToAsset(surfaceSwimAnimatorState65354blendTreeBlendTree66542, animatorControllers[i]);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.hideFlags = HideFlags.HideInHierarchy;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.blendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.blendParameterY = "ForwardMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.blendType = BlendTreeType.FreeformCartesian2D;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.maxThreshold = 8f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.minThreshold = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.name = "Blend Tree";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.useAutomaticThresholds = false;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0.motion = surfacePowerSwimBwdAnimationClip66544;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0.cycleOffset = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0.mirror = false;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0.position = new Vector2(0f, -2f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0.threshold = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0.timeScale = 1.25f;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1.motion = surfaceSwimBwdAnimationClip66546;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1.cycleOffset = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1.mirror = false;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1.position = new Vector2(0f, -1f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1.threshold = 1f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1.timeScale = 1f;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2.motion = surfacePowerSwimStrafeAnimationClip66548;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2.cycleOffset = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2.mirror = false;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2.position = new Vector2(-2f, 0f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2.threshold = 2f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2.timeScale = 1.25f;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3.motion = surfaceSwimStrafeAnimationClip66550;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3.cycleOffset = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3.mirror = false;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3.position = new Vector2(-1f, 0f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3.threshold = 3f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3.timeScale = 1f;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4.motion = surfaceSwimIdleAnimationClip66530;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4.cycleOffset = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4.mirror = false;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4.position = new Vector2(0f, 0f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4.threshold = 4f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4.timeScale = 1f;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5.motion = surfaceSwimStrafeAnimationClip66550;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5.cycleOffset = 0.5f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5.mirror = true;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5.position = new Vector2(1f, 0f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5.threshold = 5f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5.timeScale = 1f;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6.motion = surfacePowerSwimStrafeAnimationClip66548;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6.cycleOffset = 0.5f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6.mirror = true;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6.position = new Vector2(2f, 0f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6.threshold = 6f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6.timeScale = 1.25f;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7.motion = surfaceSwimFwdAnimationClip66552;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7.cycleOffset = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7.mirror = false;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7.position = new Vector2(0f, 1f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7.threshold = 7f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7.timeScale = 1f;
				var surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8 =  new ChildMotion();
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8.motion = surfacePowerSwimFwdAnimationClip66554;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8.cycleOffset = 0f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8.directBlendParameter = "HorizontalMovement";
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8.mirror = false;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8.position = new Vector2(0f, 2f);
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8.threshold = 8f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8.timeScale = 1.25f;
				surfaceSwimAnimatorState65354blendTreeBlendTree66542.children = new ChildMotion[] {
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child0,
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child1,
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child2,
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child3,
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child4,
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child5,
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child6,
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child7,
					surfaceSwimAnimatorState65354blendTreeBlendTree66542Child8
				};
				surfaceSwimAnimatorState65354.motion = surfaceSwimAnimatorState65354blendTreeBlendTree66542;
				surfaceSwimAnimatorState65354.cycleOffset = 0f;
				surfaceSwimAnimatorState65354.cycleOffsetParameterActive = false;
				surfaceSwimAnimatorState65354.iKOnFeet = false;
				surfaceSwimAnimatorState65354.mirror = false;
				surfaceSwimAnimatorState65354.mirrorParameterActive = false;
				surfaceSwimAnimatorState65354.speed = 1f;
				surfaceSwimAnimatorState65354.speedParameterActive = false;
				surfaceSwimAnimatorState65354.writeDefaultValues = true;

				var underwaterSwimAnimatorState65348 = swimAnimatorStateMachine64524.AddState("Underwater Swim", new Vector3(390f, 240f, 0f));
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566 = new BlendTree();
				AssetDatabase.AddObjectToAsset(underwaterSwimAnimatorState65348blendTreeBlendTree66566, animatorControllers[i]);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.hideFlags = HideFlags.HideInHierarchy;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.blendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.blendParameterY = "ForwardMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.blendType = BlendTreeType.FreeformCartesian2D;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.maxThreshold = 90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.minThreshold = -90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.name = "Blend Tree";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.useAutomaticThresholds = true;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0.motion = underwaterSwimBwdAnimationClip66568;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0.position = new Vector2(0f, -2f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0.threshold = -90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0.timeScale = 1.5f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1.motion = underwaterSwimBwdAnimationClip66568;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1.position = new Vector2(0f, -1f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1.threshold = -67.5f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2.motion = underwaterKickStrafeAnimationClip66570;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2.position = new Vector2(-2f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2.threshold = -45f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2.timeScale = 1.5f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3.motion = underwaterStrokeStrafeAnimationClip66572;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3.position = new Vector2(-1f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3.threshold = -22.5f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4.motion = underwaterStrokeStrafeAnimationClip66572;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4.cycleOffset = 0.5f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4.mirror = true;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4.position = new Vector2(1f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4.threshold = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5.motion = underwaterKickStrafeAnimationClip66570;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5.cycleOffset = 0.5f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5.mirror = true;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5.position = new Vector2(2f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5.threshold = 22.5f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5.timeScale = 1.5f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574 = new BlendTree();
				AssetDatabase.AddObjectToAsset(underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574, animatorControllers[i]);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.hideFlags = HideFlags.HideInHierarchy;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.blendParameter = "AbilityFloatData";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.blendParameterY = "Blend";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.blendType = BlendTreeType.Simple1D;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.maxThreshold = 90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.minThreshold = -90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.name = "BlendTree";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.useAutomaticThresholds = false;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0.motion = underwaterIdleUpAnimationClip66580;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0.threshold = -90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1.motion = underwaterIdleFwdAnimationClip66582;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1.threshold = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2.motion = underwaterIdleDownAnimationClip66584;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2.threshold = 90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2.timeScale = 1f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574.children = new ChildMotion[] {
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child0,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child1,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574Child2
				};
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6.motion = underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66574;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6.threshold = 45f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576 = new BlendTree();
				AssetDatabase.AddObjectToAsset(underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576, animatorControllers[i]);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.hideFlags = HideFlags.HideInHierarchy;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.blendParameter = "AbilityFloatData";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.blendParameterY = "Blend";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.blendType = BlendTreeType.Simple1D;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.maxThreshold = 90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.minThreshold = -90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.name = "BlendTree";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.useAutomaticThresholds = false;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0.motion = underwaterStrokeUpAnimationClip66586;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0.threshold = -90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1.motion = underwaterStrokeFwdAnimationClip66588;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1.threshold = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2.motion = underwaterStrokeDownAnimationClip66590;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2.threshold = 90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2.timeScale = 1f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576.children = new ChildMotion[] {
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child0,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child1,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576Child2
				};
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7.motion = underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66576;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7.position = new Vector2(0f, 1f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7.threshold = 67.5f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7.timeScale = 1f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578 = new BlendTree();
				AssetDatabase.AddObjectToAsset(underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578, animatorControllers[i]);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.hideFlags = HideFlags.HideInHierarchy;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.blendParameter = "AbilityFloatData";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.blendParameterY = "Blend";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.blendType = BlendTreeType.Simple1D;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.maxThreshold = 90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.minThreshold = -90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.name = "BlendTree";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.useAutomaticThresholds = false;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0.motion = underwaterKickUpAnimationClip66592;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0.threshold = -90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0.timeScale = 1.5f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1.motion = underwaterKickFwdAnimationClip66594;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1.threshold = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1.timeScale = 1.5f;
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2.motion = underwaterKickDownAnimationClip66596;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2.position = new Vector2(0f, 0f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2.threshold = 90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2.timeScale = 1.5f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578.children = new ChildMotion[] {
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child0,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child1,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578Child2
				};
				var underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8 =  new ChildMotion();
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8.motion = underwaterSwimAnimatorState65348blendTreeBlendTree66566blendTreeBlendTree66578;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8.cycleOffset = 0f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8.directBlendParameter = "HorizontalMovement";
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8.mirror = false;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8.position = new Vector2(0f, 2f);
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8.threshold = 90f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8.timeScale = 1f;
				underwaterSwimAnimatorState65348blendTreeBlendTree66566.children = new ChildMotion[] {
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child0,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child1,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child2,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child3,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child4,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child5,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child6,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child7,
					underwaterSwimAnimatorState65348blendTreeBlendTree66566Child8
				};
				underwaterSwimAnimatorState65348.motion = underwaterSwimAnimatorState65348blendTreeBlendTree66566;
				underwaterSwimAnimatorState65348.cycleOffset = 0f;
				underwaterSwimAnimatorState65348.cycleOffsetParameterActive = false;
				underwaterSwimAnimatorState65348.iKOnFeet = true;
				underwaterSwimAnimatorState65348.mirror = false;
				underwaterSwimAnimatorState65348.mirrorParameterActive = false;
				underwaterSwimAnimatorState65348.speed = 1f;
				underwaterSwimAnimatorState65348.speedParameterActive = false;
				underwaterSwimAnimatorState65348.writeDefaultValues = true;

				var diveFromSurfaceAnimatorState66502 = swimAnimatorStateMachine64524.AddState("Dive From Surface", new Vector3(432f, 48f, 0f));
				diveFromSurfaceAnimatorState66502.motion = diveFromSurfaceAnimationClip66600;
				diveFromSurfaceAnimatorState66502.cycleOffset = 0f;
				diveFromSurfaceAnimatorState66502.cycleOffsetParameterActive = false;
				diveFromSurfaceAnimatorState66502.iKOnFeet = false;
				diveFromSurfaceAnimatorState66502.mirror = false;
				diveFromSurfaceAnimatorState66502.mirrorParameterActive = false;
				diveFromSurfaceAnimatorState66502.speed = 1.5f;
				diveFromSurfaceAnimatorState66502.speedParameterActive = false;
				diveFromSurfaceAnimatorState66502.writeDefaultValues = true;

				var exitWaterMovingAnimatorState66504 = swimAnimatorStateMachine64524.AddState("Exit Water Moving", new Vector3(744f, 12f, 0f));
				exitWaterMovingAnimatorState66504.motion = swimExitWaterAnimationClip66604;
				exitWaterMovingAnimatorState66504.cycleOffset = 0f;
				exitWaterMovingAnimatorState66504.cycleOffsetParameterActive = false;
				exitWaterMovingAnimatorState66504.iKOnFeet = false;
				exitWaterMovingAnimatorState66504.mirror = false;
				exitWaterMovingAnimatorState66504.mirrorParameterActive = false;
				exitWaterMovingAnimatorState66504.speed = 1.5f;
				exitWaterMovingAnimatorState66504.speedParameterActive = false;
				exitWaterMovingAnimatorState66504.writeDefaultValues = true;

				var exitWaterIdleAnimatorState66506 = swimAnimatorStateMachine64524.AddState("Exit Water Idle", new Vector3(744f, -48f, 0f));
				exitWaterIdleAnimatorState66506.motion = surfaceSwimToIdleAnimationClip66608;
				exitWaterIdleAnimatorState66506.cycleOffset = 0f;
				exitWaterIdleAnimatorState66506.cycleOffsetParameterActive = false;
				exitWaterIdleAnimatorState66506.iKOnFeet = false;
				exitWaterIdleAnimatorState66506.mirror = false;
				exitWaterIdleAnimatorState66506.mirrorParameterActive = false;
				exitWaterIdleAnimatorState66506.speed = 1.5f;
				exitWaterIdleAnimatorState66506.speedParameterActive = false;
				exitWaterIdleAnimatorState66506.writeDefaultValues = true;

				// State Machine Defaults.
				swimAnimatorStateMachine64524.anyStatePosition = new Vector3(-168f, 48f, 0f);
				swimAnimatorStateMachine64524.defaultState = surfaceIdleAnimatorState65346;
				swimAnimatorStateMachine64524.entryPosition = new Vector3(-204f, -36f, 0f);
				swimAnimatorStateMachine64524.exitPosition = new Vector3(1140f, 48f, 0f);
				swimAnimatorStateMachine64524.parentStateMachinePosition = new Vector3(1128f, -48f, 0f);

				// State Transitions.
				var animatorStateTransition66508 = fallInWaterAnimatorState65344.AddTransition(surfaceIdleAnimatorState65346);
				animatorStateTransition66508.canTransitionToSelf = true;
				animatorStateTransition66508.duration = 0.5f;
				animatorStateTransition66508.exitTime = 0.95f;
				animatorStateTransition66508.hasExitTime = true;
				animatorStateTransition66508.hasFixedDuration = true;
				animatorStateTransition66508.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66508.offset = 0f;
				animatorStateTransition66508.orderedInterruption = true;
				animatorStateTransition66508.isExit = false;
				animatorStateTransition66508.mute = false;
				animatorStateTransition66508.solo = false;
				animatorStateTransition66508.AddCondition(AnimatorConditionMode.Less, 0.1f, "ForwardMovement");
				animatorStateTransition66508.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");

				var animatorStateTransition66510 = fallInWaterAnimatorState65344.AddTransition(surfaceSwimAnimatorState65354);
				animatorStateTransition66510.canTransitionToSelf = true;
				animatorStateTransition66510.duration = 0.25f;
				animatorStateTransition66510.exitTime = 0.8f;
				animatorStateTransition66510.hasExitTime = false;
				animatorStateTransition66510.hasFixedDuration = true;
				animatorStateTransition66510.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66510.offset = 0f;
				animatorStateTransition66510.orderedInterruption = true;
				animatorStateTransition66510.isExit = false;
				animatorStateTransition66510.mute = false;
				animatorStateTransition66510.solo = false;
				animatorStateTransition66510.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition66510.AddCondition(AnimatorConditionMode.If, 0f, "Moving");

				var animatorStateTransition66512 = fallInWaterAnimatorState65344.AddExitTransition();
				animatorStateTransition66512.canTransitionToSelf = true;
				animatorStateTransition66512.duration = 0.25f;
				animatorStateTransition66512.exitTime = 0.8888889f;
				animatorStateTransition66512.hasExitTime = false;
				animatorStateTransition66512.hasFixedDuration = true;
				animatorStateTransition66512.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66512.offset = 0f;
				animatorStateTransition66512.orderedInterruption = true;
				animatorStateTransition66512.isExit = true;
				animatorStateTransition66512.mute = false;
				animatorStateTransition66512.solo = false;
				animatorStateTransition66512.AddCondition(AnimatorConditionMode.NotEqual, 301f, "AbilityIndex");

				var animatorStateTransition66514 = fallInWaterAnimatorState65344.AddTransition(underwaterSwimAnimatorState65348);
				animatorStateTransition66514.canTransitionToSelf = true;
				animatorStateTransition66514.duration = 0.5f;
				animatorStateTransition66514.exitTime = 0.9f;
				animatorStateTransition66514.hasExitTime = true;
				animatorStateTransition66514.hasFixedDuration = true;
				animatorStateTransition66514.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66514.offset = 0f;
				animatorStateTransition66514.orderedInterruption = true;
				animatorStateTransition66514.isExit = false;
				animatorStateTransition66514.mute = false;
				animatorStateTransition66514.solo = false;
				animatorStateTransition66514.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");
				animatorStateTransition66514.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");

				var animatorStateTransition66516 = fallInWaterAnimatorState65344.AddTransition(underwaterSwimAnimatorState65348);
				animatorStateTransition66516.canTransitionToSelf = true;
				animatorStateTransition66516.duration = 0.25f;
				animatorStateTransition66516.exitTime = 0.8888889f;
				animatorStateTransition66516.hasExitTime = false;
				animatorStateTransition66516.hasFixedDuration = true;
				animatorStateTransition66516.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66516.offset = 0f;
				animatorStateTransition66516.orderedInterruption = true;
				animatorStateTransition66516.isExit = false;
				animatorStateTransition66516.mute = false;
				animatorStateTransition66516.solo = false;
				animatorStateTransition66516.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");
				animatorStateTransition66516.AddCondition(AnimatorConditionMode.If, 0f, "Moving");

				var animatorStateTransition66520 = surfaceIdleAnimatorState65346.AddTransition(surfaceSwimAnimatorState65354);
				animatorStateTransition66520.canTransitionToSelf = true;
				animatorStateTransition66520.duration = 0.4f;
				animatorStateTransition66520.exitTime = 0.75f;
				animatorStateTransition66520.hasExitTime = false;
				animatorStateTransition66520.hasFixedDuration = true;
				animatorStateTransition66520.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66520.offset = 0f;
				animatorStateTransition66520.orderedInterruption = true;
				animatorStateTransition66520.isExit = false;
				animatorStateTransition66520.mute = false;
				animatorStateTransition66520.solo = false;
				animatorStateTransition66520.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition66520.AddCondition(AnimatorConditionMode.If, 0f, "Moving");

				var animatorStateTransition66522 = surfaceIdleAnimatorState65346.AddTransition(diveFromSurfaceAnimatorState66502);
				animatorStateTransition66522.canTransitionToSelf = true;
				animatorStateTransition66522.duration = 0.25f;
				animatorStateTransition66522.exitTime = 0.75f;
				animatorStateTransition66522.hasExitTime = false;
				animatorStateTransition66522.hasFixedDuration = true;
				animatorStateTransition66522.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66522.offset = 0f;
				animatorStateTransition66522.orderedInterruption = true;
				animatorStateTransition66522.isExit = false;
				animatorStateTransition66522.mute = false;
				animatorStateTransition66522.solo = false;
				animatorStateTransition66522.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");

				var animatorStateTransition66524 = surfaceIdleAnimatorState65346.AddExitTransition();
				animatorStateTransition66524.canTransitionToSelf = true;
				animatorStateTransition66524.duration = 0.25f;
				animatorStateTransition66524.exitTime = 0.8f;
				animatorStateTransition66524.hasExitTime = false;
				animatorStateTransition66524.hasFixedDuration = true;
				animatorStateTransition66524.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66524.offset = 0f;
				animatorStateTransition66524.orderedInterruption = true;
				animatorStateTransition66524.isExit = true;
				animatorStateTransition66524.mute = false;
				animatorStateTransition66524.solo = false;
				animatorStateTransition66524.AddCondition(AnimatorConditionMode.NotEqual, 301f, "AbilityIndex");

				var animatorStateTransition66526 = surfaceIdleAnimatorState65346.AddTransition(exitWaterIdleAnimatorState66506);
				animatorStateTransition66526.canTransitionToSelf = true;
				animatorStateTransition66526.duration = 0.15f;
				animatorStateTransition66526.exitTime = 0f;
				animatorStateTransition66526.hasExitTime = false;
				animatorStateTransition66526.hasFixedDuration = true;
				animatorStateTransition66526.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66526.offset = 0f;
				animatorStateTransition66526.orderedInterruption = true;
				animatorStateTransition66526.isExit = false;
				animatorStateTransition66526.mute = false;
				animatorStateTransition66526.solo = false;
				animatorStateTransition66526.AddCondition(AnimatorConditionMode.Equals, 4f, "AbilityIntData");

				var animatorStateTransition66528 = surfaceIdleAnimatorState65346.AddTransition(exitWaterMovingAnimatorState66504);
				animatorStateTransition66528.canTransitionToSelf = true;
				animatorStateTransition66528.duration = 0.15f;
				animatorStateTransition66528.exitTime = 2.63155E-10f;
				animatorStateTransition66528.hasExitTime = false;
				animatorStateTransition66528.hasFixedDuration = true;
				animatorStateTransition66528.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66528.offset = 0f;
				animatorStateTransition66528.orderedInterruption = true;
				animatorStateTransition66528.isExit = false;
				animatorStateTransition66528.mute = false;
				animatorStateTransition66528.solo = false;
				animatorStateTransition66528.AddCondition(AnimatorConditionMode.Equals, 3f, "AbilityIntData");

				var animatorStateTransition66532 = surfaceSwimAnimatorState65354.AddTransition(exitWaterMovingAnimatorState66504);
				animatorStateTransition66532.canTransitionToSelf = true;
				animatorStateTransition66532.duration = 0.2259974f;
				animatorStateTransition66532.exitTime = 0.010637f;
				animatorStateTransition66532.hasExitTime = false;
				animatorStateTransition66532.hasFixedDuration = true;
				animatorStateTransition66532.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66532.offset = 0f;
				animatorStateTransition66532.orderedInterruption = true;
				animatorStateTransition66532.isExit = false;
				animatorStateTransition66532.mute = false;
				animatorStateTransition66532.solo = false;
				animatorStateTransition66532.AddCondition(AnimatorConditionMode.Equals, 3f, "AbilityIntData");

				var animatorStateTransition66534 = surfaceSwimAnimatorState65354.AddTransition(exitWaterIdleAnimatorState66506);
				animatorStateTransition66534.canTransitionToSelf = true;
				animatorStateTransition66534.duration = 0.15f;
				animatorStateTransition66534.exitTime = 0f;
				animatorStateTransition66534.hasExitTime = false;
				animatorStateTransition66534.hasFixedDuration = true;
				animatorStateTransition66534.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66534.offset = 0f;
				animatorStateTransition66534.orderedInterruption = true;
				animatorStateTransition66534.isExit = false;
				animatorStateTransition66534.mute = false;
				animatorStateTransition66534.solo = false;
				animatorStateTransition66534.AddCondition(AnimatorConditionMode.Equals, 4f, "AbilityIntData");

				var animatorStateTransition66536 = surfaceSwimAnimatorState65354.AddTransition(diveFromSurfaceAnimatorState66502);
				animatorStateTransition66536.canTransitionToSelf = true;
				animatorStateTransition66536.duration = 0.25f;
				animatorStateTransition66536.exitTime = 0.8f;
				animatorStateTransition66536.hasExitTime = false;
				animatorStateTransition66536.hasFixedDuration = true;
				animatorStateTransition66536.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66536.offset = 0f;
				animatorStateTransition66536.orderedInterruption = true;
				animatorStateTransition66536.isExit = false;
				animatorStateTransition66536.mute = false;
				animatorStateTransition66536.solo = false;
				animatorStateTransition66536.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");

				var animatorStateTransition66538 = surfaceSwimAnimatorState65354.AddTransition(surfaceIdleAnimatorState65346);
				animatorStateTransition66538.canTransitionToSelf = true;
				animatorStateTransition66538.duration = 0.4f;
				animatorStateTransition66538.exitTime = 0.8f;
				animatorStateTransition66538.hasExitTime = false;
				animatorStateTransition66538.hasFixedDuration = true;
				animatorStateTransition66538.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66538.offset = 0f;
				animatorStateTransition66538.orderedInterruption = true;
				animatorStateTransition66538.isExit = false;
				animatorStateTransition66538.mute = false;
				animatorStateTransition66538.solo = false;
				animatorStateTransition66538.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition66538.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");

				var animatorStateTransition66540 = surfaceSwimAnimatorState65354.AddExitTransition();
				animatorStateTransition66540.canTransitionToSelf = true;
				animatorStateTransition66540.duration = 0.25f;
				animatorStateTransition66540.exitTime = 0.8f;
				animatorStateTransition66540.hasExitTime = false;
				animatorStateTransition66540.hasFixedDuration = true;
				animatorStateTransition66540.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66540.offset = 0f;
				animatorStateTransition66540.orderedInterruption = true;
				animatorStateTransition66540.isExit = true;
				animatorStateTransition66540.mute = false;
				animatorStateTransition66540.solo = false;
				animatorStateTransition66540.AddCondition(AnimatorConditionMode.NotEqual, 301f, "AbilityIndex");

				var animatorStateTransition66556 = underwaterSwimAnimatorState65348.AddTransition(surfaceIdleAnimatorState65346);
				animatorStateTransition66556.canTransitionToSelf = true;
				animatorStateTransition66556.duration = 0.1f;
				animatorStateTransition66556.exitTime = 0.8f;
				animatorStateTransition66556.hasExitTime = false;
				animatorStateTransition66556.hasFixedDuration = true;
				animatorStateTransition66556.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66556.offset = 0f;
				animatorStateTransition66556.orderedInterruption = true;
				animatorStateTransition66556.isExit = false;
				animatorStateTransition66556.mute = false;
				animatorStateTransition66556.solo = false;
				animatorStateTransition66556.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition66556.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");

				var animatorStateTransition66558 = underwaterSwimAnimatorState65348.AddTransition(surfaceSwimAnimatorState65354);
				animatorStateTransition66558.canTransitionToSelf = true;
				animatorStateTransition66558.duration = 0.1f;
				animatorStateTransition66558.exitTime = 0.8285714f;
				animatorStateTransition66558.hasExitTime = false;
				animatorStateTransition66558.hasFixedDuration = true;
				animatorStateTransition66558.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66558.offset = 0f;
				animatorStateTransition66558.orderedInterruption = true;
				animatorStateTransition66558.isExit = false;
				animatorStateTransition66558.mute = false;
				animatorStateTransition66558.solo = false;
				animatorStateTransition66558.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition66558.AddCondition(AnimatorConditionMode.If, 0f, "Moving");

				var animatorStateTransition66560 = underwaterSwimAnimatorState65348.AddTransition(exitWaterMovingAnimatorState66504);
				animatorStateTransition66560.canTransitionToSelf = true;
				animatorStateTransition66560.duration = 0.2259974f;
				animatorStateTransition66560.exitTime = 0.01825405f;
				animatorStateTransition66560.hasExitTime = false;
				animatorStateTransition66560.hasFixedDuration = true;
				animatorStateTransition66560.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66560.offset = 0f;
				animatorStateTransition66560.orderedInterruption = true;
				animatorStateTransition66560.isExit = false;
				animatorStateTransition66560.mute = false;
				animatorStateTransition66560.solo = false;
				animatorStateTransition66560.AddCondition(AnimatorConditionMode.Equals, 3f, "AbilityIntData");

				var animatorStateTransition66562 = underwaterSwimAnimatorState65348.AddTransition(exitWaterIdleAnimatorState66506);
				animatorStateTransition66562.canTransitionToSelf = true;
				animatorStateTransition66562.duration = 0.15f;
				animatorStateTransition66562.exitTime = 0.8285714f;
				animatorStateTransition66562.hasExitTime = false;
				animatorStateTransition66562.hasFixedDuration = true;
				animatorStateTransition66562.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66562.offset = 0f;
				animatorStateTransition66562.orderedInterruption = true;
				animatorStateTransition66562.isExit = false;
				animatorStateTransition66562.mute = false;
				animatorStateTransition66562.solo = false;
				animatorStateTransition66562.AddCondition(AnimatorConditionMode.Equals, 4f, "AbilityIntData");

				var animatorStateTransition66564 = underwaterSwimAnimatorState65348.AddExitTransition();
				animatorStateTransition66564.canTransitionToSelf = true;
				animatorStateTransition66564.duration = 0.25f;
				animatorStateTransition66564.exitTime = 0.8f;
				animatorStateTransition66564.hasExitTime = false;
				animatorStateTransition66564.hasFixedDuration = true;
				animatorStateTransition66564.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66564.offset = 0f;
				animatorStateTransition66564.orderedInterruption = true;
				animatorStateTransition66564.isExit = true;
				animatorStateTransition66564.mute = false;
				animatorStateTransition66564.solo = false;
				animatorStateTransition66564.AddCondition(AnimatorConditionMode.NotEqual, 301f, "AbilityIndex");

				var animatorStateTransition66598 = diveFromSurfaceAnimatorState66502.AddTransition(underwaterSwimAnimatorState65348);
				animatorStateTransition66598.canTransitionToSelf = true;
				animatorStateTransition66598.duration = 0.25f;
				animatorStateTransition66598.exitTime = 0.88f;
				animatorStateTransition66598.hasExitTime = true;
				animatorStateTransition66598.hasFixedDuration = true;
				animatorStateTransition66598.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66598.offset = 0f;
				animatorStateTransition66598.orderedInterruption = true;
				animatorStateTransition66598.isExit = false;
				animatorStateTransition66598.mute = false;
				animatorStateTransition66598.solo = false;

				var animatorStateTransition66602 = exitWaterMovingAnimatorState66504.AddExitTransition();
				animatorStateTransition66602.canTransitionToSelf = true;
				animatorStateTransition66602.duration = 0.15f;
				animatorStateTransition66602.exitTime = 0.85f;
				animatorStateTransition66602.hasExitTime = true;
				animatorStateTransition66602.hasFixedDuration = true;
				animatorStateTransition66602.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66602.offset = 0f;
				animatorStateTransition66602.orderedInterruption = true;
				animatorStateTransition66602.isExit = true;
				animatorStateTransition66602.mute = false;
				animatorStateTransition66602.solo = false;
				animatorStateTransition66602.AddCondition(AnimatorConditionMode.NotEqual, 301f, "AbilityIndex");

				var animatorStateTransition66606 = exitWaterIdleAnimatorState66506.AddExitTransition();
				animatorStateTransition66606.canTransitionToSelf = true;
				animatorStateTransition66606.duration = 0.1f;
				animatorStateTransition66606.exitTime = 0.65f;
				animatorStateTransition66606.hasExitTime = false;
				animatorStateTransition66606.hasFixedDuration = true;
				animatorStateTransition66606.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition66606.offset = 0f;
				animatorStateTransition66606.orderedInterruption = true;
				animatorStateTransition66606.isExit = true;
				animatorStateTransition66606.mute = false;
				animatorStateTransition66606.solo = false;
				animatorStateTransition66606.AddCondition(AnimatorConditionMode.NotEqual, 301f, "AbilityIndex");

				// State Machine Transitions.
				var animatorStateTransition65058 = baseStateMachine733959252.AddAnyStateTransition(fallInWaterAnimatorState65344);
				animatorStateTransition65058.canTransitionToSelf = false;
				animatorStateTransition65058.duration = 0.25f;
				animatorStateTransition65058.exitTime = 0.75f;
				animatorStateTransition65058.hasExitTime = false;
				animatorStateTransition65058.hasFixedDuration = true;
				animatorStateTransition65058.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition65058.offset = 0f;
				animatorStateTransition65058.orderedInterruption = true;
				animatorStateTransition65058.isExit = false;
				animatorStateTransition65058.mute = false;
				animatorStateTransition65058.solo = false;
				animatorStateTransition65058.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition65058.AddCondition(AnimatorConditionMode.Equals, 301f, "AbilityIndex");
				animatorStateTransition65058.AddCondition(AnimatorConditionMode.Equals, 0f, "AbilityIntData");

				var animatorStateTransition65060 = baseStateMachine733959252.AddAnyStateTransition(surfaceIdleAnimatorState65346);
				animatorStateTransition65060.canTransitionToSelf = false;
				animatorStateTransition65060.duration = 0.25f;
				animatorStateTransition65060.exitTime = 0.75f;
				animatorStateTransition65060.hasExitTime = false;
				animatorStateTransition65060.hasFixedDuration = true;
				animatorStateTransition65060.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition65060.offset = 0f;
				animatorStateTransition65060.orderedInterruption = true;
				animatorStateTransition65060.isExit = false;
				animatorStateTransition65060.mute = false;
				animatorStateTransition65060.solo = false;
				animatorStateTransition65060.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition65060.AddCondition(AnimatorConditionMode.Equals, 301f, "AbilityIndex");
				animatorStateTransition65060.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition65060.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");

				var animatorStateTransition65062 = baseStateMachine733959252.AddAnyStateTransition(underwaterSwimAnimatorState65348);
				animatorStateTransition65062.canTransitionToSelf = false;
				animatorStateTransition65062.duration = 0.25f;
				animatorStateTransition65062.exitTime = 0.75f;
				animatorStateTransition65062.hasExitTime = false;
				animatorStateTransition65062.hasFixedDuration = true;
				animatorStateTransition65062.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition65062.offset = 0f;
				animatorStateTransition65062.orderedInterruption = true;
				animatorStateTransition65062.isExit = false;
				animatorStateTransition65062.mute = false;
				animatorStateTransition65062.solo = false;
				animatorStateTransition65062.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition65062.AddCondition(AnimatorConditionMode.Equals, 301f, "AbilityIndex");
				animatorStateTransition65062.AddCondition(AnimatorConditionMode.Equals, 2f, "AbilityIntData");

				var animatorStateTransition65068 = baseStateMachine733959252.AddAnyStateTransition(surfaceSwimAnimatorState65354);
				animatorStateTransition65068.canTransitionToSelf = false;
				animatorStateTransition65068.duration = 0.25f;
				animatorStateTransition65068.exitTime = 0.75f;
				animatorStateTransition65068.hasExitTime = false;
				animatorStateTransition65068.hasFixedDuration = true;
				animatorStateTransition65068.interruptionSource = TransitionInterruptionSource.None;
				animatorStateTransition65068.offset = 0f;
				animatorStateTransition65068.orderedInterruption = true;
				animatorStateTransition65068.isExit = false;
				animatorStateTransition65068.mute = false;
				animatorStateTransition65068.solo = false;
				animatorStateTransition65068.AddCondition(AnimatorConditionMode.If, 0f, "AbilityChange");
				animatorStateTransition65068.AddCondition(AnimatorConditionMode.Equals, 301f, "AbilityIndex");
				animatorStateTransition65068.AddCondition(AnimatorConditionMode.Equals, 1f, "AbilityIntData");
				animatorStateTransition65068.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
			}
		}
	}
}
