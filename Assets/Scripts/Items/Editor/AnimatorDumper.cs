using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;

namespace DIG.Editor.Analysis
{
    /// <summary>
    /// [UNIVERSAL] Debug Tool
    /// 
    /// Purpose: Dumps Animator Controller layer and transition info to file for analysis.
    /// Useful for debugging complex Animator setups.
    /// </summary>
    [InitializeOnLoad]
    public class AnimatorDumper
    {
        static AnimatorDumper()
        {
            EditorApplication.delayCall += Dump;
        }

        [MenuItem("DIG/Debug/Dump Animator Info")]
        public static void Dump()
    {
        string[] guids = AssetDatabase.FindAssets("ClimbingDemo t:AnimatorController");
        if (guids.Length == 0) return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Analyzing Controller: {path}");
        sb.AppendLine("Searching for transitions involving ItemID 26 (Shield)...");

        int targetID = 26;

        foreach (var layer in controller.layers)
        {
            sb.AppendLine($"\nLayer: {layer.name} (Weight: {layer.defaultWeight}, Mask: {(layer.avatarMask ? layer.avatarMask.name : "None")})");
            InspectStateMachine(layer.stateMachine, layer.name, targetID, sb);
        }

        File.WriteAllText("AnimatorDump.txt", sb.ToString());
        Debug.Log("Animator info dumped to AnimatorDump.txt");
    }

    static void InspectStateMachine(AnimatorStateMachine sm, string layerPath, int targetID, System.Text.StringBuilder sb)
    {
        foreach (var transition in sm.anyStateTransitions)
            CheckTransition(transition, "AnyState", layerPath, targetID, sb);

        foreach (var state in sm.states)
            foreach (var transition in state.state.transitions)
                CheckTransition(transition, state.state.name, layerPath, targetID, sb);

        foreach (var subSm in sm.stateMachines)
            InspectStateMachine(subSm.stateMachine, layerPath + "/" + subSm.stateMachine.name, targetID, sb);
    }

    static void CheckTransition(AnimatorTransitionBase transition, string source, string layerPath, int targetID, System.Text.StringBuilder sb)
    {
        foreach (var cond in transition.conditions)
        {
            // Check Slot0ItemID == 26 OR Slot1ItemID == 26
            if ((cond.parameter == "Slot0ItemID" || cond.parameter == "Slot1ItemID") && 
                Mathf.Approximately(cond.threshold, targetID) && cond.mode == AnimatorConditionMode.Equals)
            {
                sb.AppendLine($"  [FOUND] {source} -> {transition.destinationState?.name ?? "Exit"}");
                sb.AppendLine($"      Condition: {cond.parameter} == {targetID}");
            }
        }
        }
    }
}
