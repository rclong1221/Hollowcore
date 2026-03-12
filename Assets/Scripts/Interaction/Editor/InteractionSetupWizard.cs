#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DIG.Interaction.Authoring;

namespace DIG.Interaction.Editor
{
    /// <summary>
    /// EPIC 16.1 Phase 8: Step-by-step wizard for adding interaction components.
    /// Provides archetype presets with sensible defaults and inline validation.
    /// Menu: DIG > Interaction > Setup Wizard.
    /// </summary>
    public class InteractionSetupWizard : EditorWindow
    {
        private enum WizardStep { SelectArchetype, Configure, Done }

        private struct ArchetypePreset
        {
            public string Name;
            public string Description;
            public System.Action<GameObject> ApplyAction;
        }

        private WizardStep _step = WizardStep.SelectArchetype;
        private Vector2 _scrollPos;
        private string _appliedArchetype;
        private List<string> _addedComponents = new();
        private List<InteractionValidationResult> _validationResults = new();

        private ArchetypePreset[] _presets;

        [MenuItem("DIG/Interaction/Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<InteractionSetupWizard>("Interaction Setup Wizard");
            window.minSize = new Vector2(420, 500);
        }

        private void OnEnable()
        {
            _presets = new[]
            {
                new ArchetypePreset
                {
                    Name = "Simple Door",
                    Description = "Toggle-type door with open/close animation",
                    ApplyAction = ApplySimpleDoor
                },
                new ArchetypePreset
                {
                    Name = "Crafting Station",
                    Description = "Timed interaction with station UI session",
                    ApplyAction = ApplyCraftingStation
                },
                new ArchetypePreset
                {
                    Name = "Lockpick",
                    Description = "Timed interaction gated by a minigame",
                    ApplyAction = ApplyLockpick
                },
                new ArchetypePreset
                {
                    Name = "Coop Door",
                    Description = "Requires multiple players to interact simultaneously",
                    ApplyAction = ApplyCoopDoor
                },
                new ArchetypePreset
                {
                    Name = "Turret Seat",
                    Description = "Mountable turret that transfers player input",
                    ApplyAction = ApplyTurretSeat
                },
                new ArchetypePreset
                {
                    Name = "Resource Node",
                    Description = "Timed collection with respawn",
                    ApplyAction = ApplyResourceNode
                },
                new ArchetypePreset
                {
                    Name = "Proximity Heal",
                    Description = "Area zone that heals nearby players",
                    ApplyAction = ApplyProximityHeal
                },
                new ArchetypePreset
                {
                    Name = "Placeable Item",
                    Description = "Item that can be placed in the world via raycast",
                    ApplyAction = ApplyPlaceableItem
                }
            };
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            switch (_step)
            {
                case WizardStep.SelectArchetype:
                    DrawArchetypeSelection();
                    break;
                case WizardStep.Configure:
                    DrawConfigure();
                    break;
                case WizardStep.Done:
                    DrawDone();
                    break;
            }
        }

        private void DrawArchetypeSelection()
        {
            EditorGUILayout.LabelField("Step 1: Select Archetype", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var target = Selection.activeGameObject;
            if (target == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject in the scene or hierarchy first.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"Target: {target.name}", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _presets.Length; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(_presets[i].Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_presets[i].Description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Apply", GUILayout.Width(60), GUILayout.Height(36)))
                {
                    ApplyPreset(target, i);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigure()
        {
            EditorGUILayout.LabelField("Step 2: Configure", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var target = Selection.activeGameObject;
            if (target == null)
            {
                EditorGUILayout.HelpBox("Target GameObject was deselected.", MessageType.Warning);
                if (GUILayout.Button("Back"))
                    _step = WizardStep.SelectArchetype;
                return;
            }

            EditorGUILayout.LabelField($"Archetype: {_appliedArchetype}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Target: {target.name}", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            // Show added components
            EditorGUILayout.LabelField("Added Components:", EditorStyles.boldLabel);
            foreach (var comp in _addedComponents)
            {
                EditorGUILayout.LabelField($"  + {comp}", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Use the Inspector to fine-tune component settings on the selected GameObject.",
                MessageType.Info);

            // Inline validation
            EditorGUILayout.Space(8);
            if (_validationResults.Count > 0)
            {
                EditorGUILayout.LabelField("Validation:", EditorStyles.boldLabel);
                foreach (var result in _validationResults)
                {
                    MessageType msgType = result.Severity switch
                    {
                        InteractionValidationSeverity.Error => MessageType.Error,
                        InteractionValidationSeverity.Warning => MessageType.Warning,
                        _ => MessageType.Info
                    };
                    EditorGUILayout.HelpBox(result.Message, msgType);
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Back"))
            {
                _step = WizardStep.SelectArchetype;
            }
            if (GUILayout.Button("Done"))
            {
                _step = WizardStep.Done;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDone()
        {
            EditorGUILayout.LabelField("Step 3: Complete", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            EditorGUILayout.HelpBox(
                $"Successfully applied \"{_appliedArchetype}\" archetype.\n\nComponents added:\n" +
                string.Join("\n", _addedComponents.ConvertAll(c => $"  - {c}")),
                MessageType.Info);

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Validate Scene"))
            {
                InteractionValidatorWindow.ShowWindow();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Setup Another"))
            {
                _step = WizardStep.SelectArchetype;
                _addedComponents.Clear();
                _validationResults.Clear();
            }
        }

        private void ApplyPreset(GameObject target, int presetIndex)
        {
            _addedComponents.Clear();
            _validationResults.Clear();

            Undo.SetCurrentGroupName($"Apply {_presets[presetIndex].Name} Archetype");
            int undoGroup = Undo.GetCurrentGroup();

            _presets[presetIndex].ApplyAction(target);
            _appliedArchetype = _presets[presetIndex].Name;

            Undo.CollapseUndoOperations(undoGroup);

            // Run targeted validation
            _validationResults = InteractionValidator.ValidateScene();
            _validationResults.RemoveAll(r =>
                r.SourceObject != null && r.SourceObject != target);

            _step = WizardStep.Configure;
        }

        private T EnsureComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null)
                return existing;

            var comp = Undo.AddComponent<T>(go);
            _addedComponents.Add(typeof(T).Name);
            return comp;
        }

        // --- Archetype Apply Methods ---

        private void ApplySimpleDoor(GameObject go)
        {
            var interactable = EnsureComponent<InteractableAuthoring>(go);
            interactable.Type = InteractableType.Toggle;
            interactable.Message = "Open/Close";
            interactable.Verb = InteractionVerb.Open;

            EnsureComponent<DoorAuthoring>(go);
        }

        private void ApplyCraftingStation(GameObject go)
        {
            var interactable = EnsureComponent<InteractableAuthoring>(go);
            interactable.Type = InteractableType.Instant;
            interactable.Message = "Use Crafting Station";
            interactable.Verb = InteractionVerb.Use;

            var station = EnsureComponent<StationAuthoring>(go);
            station.SessionType = SessionType.UIPanel;
            station.LockPosition = true;
            station.LockAbilities = true;
        }

        private void ApplyLockpick(GameObject go)
        {
            var interactable = EnsureComponent<InteractableAuthoring>(go);
            interactable.Type = InteractableType.Timed;
            interactable.RequiresHold = true;
            interactable.HoldDuration = 10f;
            interactable.Message = "Lockpick";
            interactable.Verb = InteractionVerb.Use;

            var minigame = EnsureComponent<MinigameAuthoring>(go);
            minigame.DifficultyLevel = 0.5f;
            minigame.TimeLimit = 30f;
            minigame.FailEndsInteraction = true;
        }

        private void ApplyCoopDoor(GameObject go)
        {
            var interactable = EnsureComponent<InteractableAuthoring>(go);
            interactable.Type = InteractableType.Instant;
            interactable.Message = "Requires 2 Players";
            interactable.Verb = InteractionVerb.Interact;

            var coop = EnsureComponent<CoopInteractableAuthoring>(go);
            coop.RequiredPlayers = 2;
            coop.Mode = CoopMode.Simultaneous;
            coop.SyncTolerance = 2f;
        }

        private void ApplyTurretSeat(GameObject go)
        {
            var interactable = EnsureComponent<InteractableAuthoring>(go);
            interactable.Type = InteractableType.Instant;
            interactable.Message = "Man Turret";
            interactable.Verb = InteractionVerb.Mount;

            var mount = EnsureComponent<MountPointAuthoring>(go);
            mount.Type = MountType.Seat;
            mount.TransferInputToMount = true;
            mount.HidePlayerModel = false;
        }

        private void ApplyResourceNode(GameObject go)
        {
            var interactable = EnsureComponent<InteractableAuthoring>(go);
            interactable.Type = InteractableType.Timed;
            interactable.RequiresHold = true;
            interactable.HoldDuration = 2f;
            interactable.Message = "Gather";
            interactable.Verb = InteractionVerb.Loot;

            EnsureComponent<ResourceAuthoring>(go);
        }

        private void ApplyProximityHeal(GameObject go)
        {
            var zone = EnsureComponent<ProximityZoneAuthoring>(go);
            zone.Effect = ProximityEffect.Heal;
            zone.Radius = 5f;
            zone.EffectInterval = 1f;
            zone.EffectValue = 10f;
        }

        private void ApplyPlaceableItem(GameObject go)
        {
            var placeable = EnsureComponent<PlaceableAuthoring>(go);
            placeable.MaxPlacementRange = 10f;
            placeable.MaxSurfaceAngle = 45f;
            placeable.Validation = PlacementValidation.FlatSurface;
        }
    }
}
#endif
