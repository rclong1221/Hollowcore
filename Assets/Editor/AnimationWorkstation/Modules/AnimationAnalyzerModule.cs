using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.AnimationWorkstation
{
    public class AnimationAnalyzerModule : IWorkstationModule
    {
        private AnimatorController _controller;
        private List<AnalysisResult> _results = new List<AnalysisResult>();
        private Vector2 _scrollPos;
        private bool _hasAnalyzed = false;

        // Cached styles to avoid allocation every frame
        private GUIStyle _errorStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _warningStyle;
        private bool _stylesInitialized = false;

        private class AnalysisResult
        {
            public string Category;
            public string Message;
            public MessageType Type;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Controller Analyzer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Performs a health check on the Animator Controller.\nDetects missing parameters, layers, and transition issues.", MessageType.Info);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _controller = (AnimatorController)EditorGUILayout.ObjectField("Controller", _controller, typeof(AnimatorController), false);
            if (_controller == null)
            {
                 // Auto-find
                 if (GUILayout.Button("Find ClimbingDemo.controller", GUILayout.Height(30)))
                 {
                     string[] guids = AssetDatabase.FindAssets("ClimbingDemo t:AnimatorController");
                     if (guids.Length > 0)
                     {
                         string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                         _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                         Analyze();
                     }
                 }
            }
            EditorGUILayout.EndVertical();

            if (_controller != null)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Run Health Check", GUILayout.Height(30)))
                {
                    Analyze();
                }
            }

            if (_hasAnalyzed)
            {
                EditorGUILayout.Space();
                DrawResults();
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _errorStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            _errorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

            _infoStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            _infoStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 1f, 0.6f) : new Color(0f, 0.6f, 0f);

            _warningStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            _warningStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(1f, 0.8f, 0.4f) : new Color(0.7f, 0.5f, 0f);

            _stylesInitialized = true;
        }

        private void DrawResults()
        {
            InitializeStyles();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, EditorStyles.helpBox);

            if (_results.Count == 0)
            {
                EditorGUILayout.LabelField("No issues found! (Or no checks ran)", EditorStyles.centeredGreyMiniLabel);
            }

            foreach (var res in _results)
            {
                // Custom row style
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Icon
                string icon = res.Type == MessageType.Info ? "testpassed" : (res.Type == MessageType.Warning ? "console.warnicon" : "console.erroricon");
                GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(25), GUILayout.Height(25));

                // Content
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(res.Category, EditorStyles.boldLabel);

                // Use cached style based on message type
                GUIStyle msgStyle = res.Type == MessageType.Error ? _errorStyle :
                                    res.Type == MessageType.Info ? _infoStyle : _warningStyle;

                EditorGUILayout.LabelField(res.Message, msgStyle);
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void Analyze()
        {
            _results.Clear();
            _hasAnalyzed = true;

            if (_controller == null) return;

            // 1. Check Parameters
            bool hasSlot0 = _controller.parameters.Any(p => p.name == "Slot0ItemID");
            bool hasSlot1 = _controller.parameters.Any(p => p.name == "Slot1ItemID");
            
            AddResult("Parameters", "Slot0ItemID (Main Weapon ID)", hasSlot0 ? MessageType.Info : MessageType.Error, hasSlot0 ? "Present" : "Missing");
            AddResult("Parameters", "Slot1ItemID", hasSlot1 ? MessageType.Info : MessageType.Warning, hasSlot1 ? "Present" : "Missing (May be needed for Dual Wield)");

            // 2. Check Upperbody Layer
            var ubLayer = _controller.layers.FirstOrDefault(l => l.name == "Upperbody Layer" || l.name == "Upperbody");
            if (ubLayer != null)
            {
                // Check AnyStates
                int bowTransitions = 0;
                int knifeTransitions = 0;

                foreach(var t in ubLayer.stateMachine.anyStateTransitions)
                {
                    if (IsAccurateTransition(t, 4)) bowTransitions++;
                    if (IsAccurateTransition(t, 23)) knifeTransitions++;
                }

                AddResult("Logic", "Bow Transitions (ID 4)", bowTransitions > 0 ? MessageType.Info : MessageType.Error, 
                    bowTransitions > 0 ? $"Found {bowTransitions} transitions." : "No AnyState transitions found for Bow.");

                AddResult("Logic", "Knife Transitions (ID 23)", knifeTransitions > 0 ? MessageType.Info : MessageType.Warning, 
                    knifeTransitions > 0 ? $"Found {knifeTransitions} transitions." : "No AnyState transitions found for Knife.");
            }
            else
            {
                AddResult("Structure", "Upperbody Layer", MessageType.Error, "Layer 'Upperbody Layer' not found!");
            }
        }

        private void AddResult(string category, string subject, MessageType type, string details)
        {
            _results.Add(new AnalysisResult
            {
                Category = $"{category}: {subject}",
                Message = details,
                Type = type
            });
        }

        private bool IsAccurateTransition(AnimatorTransitionBase transition, int itemID)
        {
            // Naive check if it relates to the item
            return transition.conditions.Any(c => c.parameter == "Slot0ItemID" && (int)c.threshold == itemID);
        }
    }
}
