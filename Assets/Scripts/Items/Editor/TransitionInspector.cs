using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

namespace DIG.Editor.Analysis
{
    /// <summary>
    /// [UNIVERSAL] Debug Tool
    /// 
    /// Purpose: Inspects Animator Controller transitions for specific ItemIDs.
    /// Useful for debugging animation routing issues.
    /// </summary>
    public class TransitionInspector : EditorWindow
    {
        private AnimatorController _controller;
        private int _itemIDToCheck = 25; // Sword

        [MenuItem("DIG/Debug/Transition Inspector")]
        public static void ShowWindow()
        {
            GetWindow<TransitionInspector>("Transition Inspector");
        }

    private void OnGUI()
    {
        _controller = (AnimatorController)EditorGUILayout.ObjectField("Controller", _controller, typeof(AnimatorController), false);
        _itemIDToCheck = EditorGUILayout.IntField("Item ID", _itemIDToCheck);

        if (GUILayout.Button("Inspect Transitions"))
        {
            if (_controller == null)
            {
                string[] guids = AssetDatabase.FindAssets("ClimbingDemo t:AnimatorController");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                    Debug.Log($"Auto-found controller at: {path}");
                }
            }

            if (_controller != null)
                Inspect();
            else
                Debug.LogError("Could not find ClimbingDemo.controller!");
        }
    }

    private void Inspect()
    {
        Debug.Log($"Inspecting controller: {_controller.name} for ItemID {_itemIDToCheck}");

        foreach (var layer in _controller.layers)
        {
            Debug.Log($"Layer: {layer.name} | Blending: {layer.blendingMode} | Weight: {layer.defaultWeight} | Mask: {(layer.avatarMask != null ? layer.avatarMask.name : "None")}");
            InspectStateMachine(layer.stateMachine, layer.name);
        }
    }

    private void InspectStateMachine(AnimatorStateMachine sm, string layerName)
    {
        // Check transitions from AnyState
        foreach (var transition in sm.anyStateTransitions)
        {
            CheckTransition(transition, "AnyState", layerName);
        }

        // Check states
        foreach (var state in sm.states)
        {
            foreach (var transition in state.state.transitions)
            {
                CheckTransition(transition, state.state.name, layerName);
            }
        }

        // Recurse sub-state machines
        foreach (var subSm in sm.stateMachines)
        {
            InspectStateMachine(subSm.stateMachine, layerName + "/" + subSm.stateMachine.name);
        }
    }

    private void CheckTransition(AnimatorTransitionBase transition, string sourceName, string layerName)
    {
        // Look for transitions that check Slot0ItemID == _itemIDToCheck
        bool matchesItem = false;
        bool checksSlot1 = false;
        string slot1Condition = "";

        foreach (var cond in transition.conditions)
        {
            if (cond.parameter == "Slot0ItemID" && (int)cond.threshold == _itemIDToCheck)
                matchesItem = true;
            
            if (cond.parameter == "Slot1ItemID")
            {
                checksSlot1 = true;
                slot1Condition = $"{cond.mode} {cond.threshold}";
            }
        }

        if (matchesItem && checksSlot1)
        {
            Debug.Log($"[FOUND] Layer: {layerName} | From: {sourceName} -> To: {transition.destinationState?.name ?? "Exit"}");
            Debug.Log($"   Condition: Slot0ItemID == {_itemIDToCheck} AND Slot1ItemID {slot1Condition}");
        }
        else if (matchesItem && !checksSlot1)
        {
             // Log useful "Sword" transitions that DON'T check Slot1 (maybe they're being overridden?)
            // Debug.Log($"[INFO] Layer: {layerName} | From: {sourceName} -> To: {transition.destinationState?.name} (No Slot1 check)");
        }
        }
    }
}
