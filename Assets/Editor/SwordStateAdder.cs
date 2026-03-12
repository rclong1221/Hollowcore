using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
using System.Collections.Generic;

public class SwordStateAdder : MonoBehaviour
{
    [MenuItem("Tools/Opsive/Add Sword States")]
    public static void AddSwordStates()
    {
        string controllerPath = "Assets/Art/AddOns/Climbing/ClimbingDemo.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"Could not load controller at {controllerPath}");
            return;
        }

        AnimatorControllerLayer upperBodyLayer = controller.layers.FirstOrDefault(l => l.name == "Upperbody Layer");
        if (upperBodyLayer == null)
        {
            Debug.LogError("Could not find 'Upperbody Layer'");
            return;
        }

        AnimatorStateMachine swordMachine = FindStateMachine(upperBodyLayer.stateMachine, "Sword");
        if (swordMachine == null)
        {
            Debug.LogError("Could not find 'Sword' state machine in Upperbody Layer");
            return;
        }

        string swordAnimPath = "Assets/Art/Animations/Opsive/Demo/Items/Melee/Sword";

        // --- 1. Add Attack States ---
        AnimationClip attack1Clip = FindClipInFBX($"{swordAnimPath}/SwordAttack1FromIdle.fbx", "SwordAttack1FromIdle");
        AnimationClip attack2Clip = FindClipInFBX($"{swordAnimPath}/SwordAttack2FromIdle.fbx", "SwordAttack2FromIdle");

        if (attack1Clip == null || attack2Clip == null)
        {
             // Fallback to "Take 001" if logic requires
             if(attack1Clip == null) attack1Clip = FindClipInFBX($"{swordAnimPath}/SwordAttack1FromIdle.fbx", "Take 001");
             if(attack2Clip == null) attack2Clip = FindClipInFBX($"{swordAnimPath}/SwordAttack2FromIdle.fbx", "Take 001");
        }
        
        AnimatorState attack1State = CreateOrGetState(swordMachine, "Attack 1 Light From Idle", attack1Clip);
        AnimatorState attack2State = CreateOrGetState(swordMachine, "Attack 2 Light From Idle", attack2Clip);

        AddExitTransition(attack1State);
        AddExitTransition(attack2State);

        // --- 2. Add Equip/Unequip States ---
        AnimationClip equipIdleClip = FindClipInFBX($"{swordAnimPath}/SwordEquipFromIdle.fbx", "SwordEquipFromIdle") ?? FindClipInFBX($"{swordAnimPath}/SwordEquipFromIdle.fbx", "Take 001");
        AnimationClip unequipIdleClip = FindClipInFBX($"{swordAnimPath}/SwordUnequipFromIdle.fbx", "SwordUnequipFromIdle") ?? FindClipInFBX($"{swordAnimPath}/SwordUnequipFromIdle.fbx", "Take 001");
        
        // Use "Equip From Idle" name to match Opsive convention
        AnimatorState equipState = CreateOrGetState(swordMachine, "Equip From Idle", equipIdleClip);
        AnimatorState unequipState = CreateOrGetState(swordMachine, "Unequip From Idle", unequipIdleClip);
        
        // Add basic transitions for Equip -> Idle (if Idle existed) or Exit
        // For sub-state machines, usually Equip transitions to the machine's "Idle" or Exit.
        // The Sword machine has "Aim" and "Ride Idle", but seemingly no "Idle".
        // Let's create an "Idle" state if missing, or use Entry?
        // Actually, for now, let's just make sure Equip exists.
        // And ensure it transitions to *something* so it doesn't loop.
        // Opsive usually transitions to "Idle" state within the same sub-machine.
        // Let's check if "Idle" exists, if not create logic to just Exit.
        AddExitTransition(equipState);
        AddExitTransition(unequipState); // Unequip -> Exit (back to parent)

        // --- 3. Add AnyState Transition for Equip ---
        // We need a transition from "Any State" (or Upperbody's AnyState) to "Sword.Equip From Idle"
        // Condition: Slot0ItemID == 25
        
        AddAnyStateTransition(upperBodyLayer.stateMachine, equipState, 25);


        Debug.Log($"Successfully added Sword states and transitions to {controller.name}");
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    // ... Helper methods (FindStateMachine, FindClipInFBX, CreateOrGetState, AddExitTransition) ...
    
    private static void AddAnyStateTransition(AnimatorStateMachine rootStateMachine, AnimatorState dstState, int itemID)
    {
        // Opsive documentation/examples put these transitions on the root "Any State" node 
        // or the specific Layer's Any State.
        // In Unity API, we add to stateMachine.anyStateTransitions.
        
        // Check if transition already exists to avoid duplicates
        foreach(var t in rootStateMachine.anyStateTransitions)
        {
            if (t.destinationState == dstState)
            {
                 // Check conditions
                 bool hasID = false;
                 foreach(var c in t.conditions)
                 {
                     if (c.mode == AnimatorConditionMode.Equals && c.parameter == "Slot0ItemID" && Mathf.Approximately(c.threshold, itemID))
                     {
                         hasID = true;
                         break;
                     }
                 }
                 if (hasID) return; // Already exists
            }
        }

        // Create new transition
        var trans = rootStateMachine.AddAnyStateTransition(dstState);
        trans.AddCondition(AnimatorConditionMode.Equals, itemID, "Slot0ItemID");
        // Opsive usually adds "Slot0ItemStateIndex" condition too, typically 0 for Equip?
        // But ItemID change is primary. Let's stick to ItemID for now.
        // Actually, to prevent re-triggering while already equipped, usually there is more logic,
        // but AnyState transitions usually are "Can Transition To Self = false".
        trans.canTransitionToSelf = false;
        trans.duration = 0.1f;
    }

    private static AnimatorStateMachine FindStateMachine(AnimatorStateMachine parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (var childSm in parent.stateMachines)
        {
            if (childSm.stateMachine.name == name) return childSm.stateMachine;
            var found = FindStateMachine(childSm.stateMachine, name);
            if (found != null) return found; 
        }
        return null;
    }

    private static AnimationClip FindClipInFBX(string fbxPath, string clipName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip)
            {
                if (clip.name == clipName || clip.name == "Take 001") return clip;
            }
        }
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);
    }

    private static AnimatorState CreateOrGetState(AnimatorStateMachine sm, string stateName, AnimationClip clip)
    {
        ChildAnimatorState[] states = sm.states;
        foreach (var cs in states)
        {
            if (cs.state.name == stateName)
            {
                cs.state.motion = clip;
                return cs.state;
            }
        }
        AnimatorState newState = sm.AddState(stateName);
        newState.motion = clip;
        return newState;
    }

    private static void AddExitTransition(AnimatorState state)
    {
        foreach (var t in state.transitions) { if (t.isExit) return; }
        var trans = state.AddExitTransition();
        trans.hasExitTime = true;
        trans.exitTime = 0.9f;
        trans.duration = 0.1f;
    }
}
