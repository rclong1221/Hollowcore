#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DIG.Interaction.Authoring;

namespace DIG.Interaction.Editor
{
    public enum InteractionValidationSeverity { Error, Warning, Info }

    public struct InteractionValidationResult
    {
        public InteractionValidationSeverity Severity;
        public string Message;
        public string Context;
        public GameObject SourceObject;
    }

    /// <summary>
    /// EPIC 16.1 Phase 8: Scene-wide validation for interaction components.
    /// Checks all interactables for missing components, conflicting settings,
    /// and configuration errors. Menu: DIG > Interaction > Validate Scene.
    /// </summary>
    public static class InteractionValidator
    {
        public static List<InteractionValidationResult> ValidateScene()
        {
            var results = new List<InteractionValidationResult>();

            // Find all interaction-related MonoBehaviours in the scene
            var interactables = Object.FindObjectsByType<InteractableAuthoring>(FindObjectsSortMode.None);
            var stations = Object.FindObjectsByType<StationAuthoring>(FindObjectsSortMode.None);
            var mounts = Object.FindObjectsByType<MountPointAuthoring>(FindObjectsSortMode.None);
            var minigames = Object.FindObjectsByType<MinigameAuthoring>(FindObjectsSortMode.None);
            var coops = Object.FindObjectsByType<CoopInteractableAuthoring>(FindObjectsSortMode.None);
            var proximityZones = Object.FindObjectsByType<ProximityZoneAuthoring>(FindObjectsSortMode.None);
            var placeables = Object.FindObjectsByType<PlaceableAuthoring>(FindObjectsSortMode.None);
            var ranged = Object.FindObjectsByType<RangedInteractableAuthoring>(FindObjectsSortMode.None);
            var multiPhases = Object.FindObjectsByType<MultiPhaseAuthoring>(FindObjectsSortMode.None);
            var doors = Object.FindObjectsByType<DoorAuthoring>(FindObjectsSortMode.None);

            // --- Check addons without base InteractableAuthoring ---
            CheckMissingBase<StationAuthoring>(stations, "StationAuthoring", results);
            CheckMissingBase<MountPointAuthoring>(mounts, "MountPointAuthoring", results);
            CheckMissingBase<MinigameAuthoring>(minigames, "MinigameAuthoring", results);
            CheckMissingBase<CoopInteractableAuthoring>(coops, "CoopInteractableAuthoring", results);
            CheckMissingBase<RangedInteractableAuthoring>(ranged, "RangedInteractableAuthoring", results);
            CheckMissingBase<MultiPhaseAuthoring>(multiPhases, "MultiPhaseAuthoring", results);

            // --- Validate each interactable ---
            foreach (var interactable in interactables)
            {
                ValidateInteractable(interactable, results);
            }

            // --- Validate proximity zones (standalone, no InteractableAuthoring needed) ---
            foreach (var zone in proximityZones)
            {
                if (zone.Radius <= 0)
                {
                    results.Add(new InteractionValidationResult
                    {
                        Severity = InteractionValidationSeverity.Error,
                        Message = "ProximityZone has Radius <= 0. No entities will be detected.",
                        Context = zone.gameObject.name,
                        SourceObject = zone.gameObject
                    });
                }
            }

            // --- Validate placeables (standalone, on item/tool prefabs) ---
            foreach (var placeable in placeables)
            {
                if (placeable.PlaceablePrefab == null)
                {
                    results.Add(new InteractionValidationResult
                    {
                        Severity = InteractionValidationSeverity.Error,
                        Message = "PlaceableAuthoring has no PlaceablePrefab assigned. Nothing will spawn on confirm.",
                        Context = placeable.gameObject.name,
                        SourceObject = placeable.gameObject
                    });
                }
            }

            // --- Check duplicate InteractableIDs ---
            CheckDuplicateIDs(interactables, results);

            // Summary
            if (results.Count == 0)
            {
                results.Add(new InteractionValidationResult
                {
                    Severity = InteractionValidationSeverity.Info,
                    Message = "All interaction components validated successfully.",
                    Context = "Scene"
                });
            }

            results.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            return results;
        }

        private static void CheckMissingBase<T>(T[] components, string typeName,
            List<InteractionValidationResult> results) where T : MonoBehaviour
        {
            foreach (var comp in components)
            {
                if (comp.GetComponent<InteractableAuthoring>() == null)
                {
                    results.Add(new InteractionValidationResult
                    {
                        Severity = InteractionValidationSeverity.Error,
                        Message = $"{typeName} requires InteractableAuthoring on the same GameObject.",
                        Context = comp.gameObject.name,
                        SourceObject = comp.gameObject
                    });
                }
            }
        }

        private static void ValidateInteractable(InteractableAuthoring interactable,
            List<InteractionValidationResult> results)
        {
            var go = interactable.gameObject;

            // Timed without duration
            if (interactable.Type == InteractableType.Timed && interactable.HoldDuration <= 0)
            {
                results.Add(new InteractionValidationResult
                {
                    Severity = InteractionValidationSeverity.Warning,
                    Message = "Timed interaction has HoldDuration <= 0. Will complete instantly.",
                    Context = go.name,
                    SourceObject = go
                });
            }

            // MultiPhase without MultiPhaseAuthoring
            if (interactable.Type == InteractableType.MultiPhase &&
                go.GetComponent<MultiPhaseAuthoring>() == null)
            {
                results.Add(new InteractionValidationResult
                {
                    Severity = InteractionValidationSeverity.Error,
                    Message = "InteractableType is MultiPhase but no MultiPhaseAuthoring found.",
                    Context = go.name,
                    SourceObject = go
                });
            }

            // Minigame type mismatch
            var minigame = go.GetComponent<MinigameAuthoring>();
            if (minigame != null && interactable.Type == InteractableType.Instant)
            {
                results.Add(new InteractionValidationResult
                {
                    Severity = InteractionValidationSeverity.Warning,
                    Message = "MinigameAuthoring on Instant interaction. Minigame gate requires Timed type to be effective.",
                    Context = go.name,
                    SourceObject = go
                });
            }

            // Coop slot count
            var coop = go.GetComponent<CoopInteractableAuthoring>();
            if (coop != null)
            {
                if (coop.Slots == null || coop.Slots.Length < coop.RequiredPlayers)
                {
                    results.Add(new InteractionValidationResult
                    {
                        Severity = InteractionValidationSeverity.Warning,
                        Message = $"CoopInteractable has {(coop.Slots?.Length ?? 0)} slots but requires {coop.RequiredPlayers} players. Extra slots will use default positions.",
                        Context = go.name,
                        SourceObject = go
                    });
                }
            }

            // Ranged without range
            var rangedComp = go.GetComponent<RangedInteractableAuthoring>();
            if (rangedComp != null && rangedComp.MaxRange <= 0)
            {
                results.Add(new InteractionValidationResult
                {
                    Severity = InteractionValidationSeverity.Error,
                    Message = "RangedInteractable has MaxRange <= 0. Players cannot initiate ranged interaction.",
                    Context = go.name,
                    SourceObject = go
                });
            }

            // InteractionRadius check
            if (interactable.InteractionRadius <= 0)
            {
                results.Add(new InteractionValidationResult
                {
                    Severity = InteractionValidationSeverity.Warning,
                    Message = "InteractionRadius <= 0. Players may not detect this interactable.",
                    Context = go.name,
                    SourceObject = go
                });
            }
        }

        private static void CheckDuplicateIDs(InteractableAuthoring[] interactables,
            List<InteractionValidationResult> results)
        {
            var idMap = new Dictionary<int, List<string>>();
            foreach (var interactable in interactables)
            {
                if (interactable.InteractableID == 0)
                    continue;

                if (!idMap.TryGetValue(interactable.InteractableID, out var names))
                {
                    names = new List<string>();
                    idMap[interactable.InteractableID] = names;
                }
                names.Add(interactable.gameObject.name);
            }

            foreach (var kvp in idMap)
            {
                if (kvp.Value.Count > 1)
                {
                    results.Add(new InteractionValidationResult
                    {
                        Severity = InteractionValidationSeverity.Warning,
                        Message = $"InteractableID {kvp.Key} is shared by {kvp.Value.Count} objects: {string.Join(", ", kvp.Value)}. Ensure this is intentional.",
                        Context = "Scene"
                    });
                }
            }
        }
    }

    /// <summary>
    /// EPIC 16.1 Phase 8: EditorWindow for displaying scene validation results.
    /// </summary>
    public class InteractionValidatorWindow : EditorWindow
    {
        private List<InteractionValidationResult> _results = new();
        private Vector2 _scrollPos;

        [MenuItem("DIG/Interaction/Validate Scene")]
        public static void ShowWindow()
        {
            var window = GetWindow<InteractionValidatorWindow>("Interaction Validator");
            window.minSize = new Vector2(400, 300);
            window.RunValidation();
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            RunValidation();
        }

        private void RunValidation()
        {
            _results = InteractionValidator.ValidateScene();
            Repaint();
        }

        private void OnGUI()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RunValidation();
            }

            int errors = 0, warnings = 0, infos = 0;
            foreach (var r in _results)
            {
                switch (r.Severity)
                {
                    case InteractionValidationSeverity.Error: errors++; break;
                    case InteractionValidationSeverity.Warning: warnings++; break;
                    case InteractionValidationSeverity.Info: infos++; break;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Errors: {errors}  Warnings: {warnings}  Info: {infos}",
                EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            // Results list
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var result in _results)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Severity icon
                MessageType msgType = result.Severity switch
                {
                    InteractionValidationSeverity.Error => MessageType.Error,
                    InteractionValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };

                EditorGUILayout.BeginVertical();
                EditorGUILayout.HelpBox($"[{result.Context}] {result.Message}", msgType);
                EditorGUILayout.EndVertical();

                // Select button
                if (result.SourceObject != null)
                {
                    if (GUILayout.Button("Select", GUILayout.Width(50), GUILayout.Height(36)))
                    {
                        Selection.activeGameObject = result.SourceObject;
                        EditorGUIUtility.PingObject(result.SourceObject);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
