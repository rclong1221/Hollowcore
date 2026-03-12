#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Entities;

namespace DIG.Cinematic.Editor.Modules
{
    /// <summary>
    /// EPIC 17.9: Play-mode debug overlay showing live CinematicState values,
    /// server-side cinematic tracker state, and playback preview controls.
    /// </summary>
    public class CinematicDebugModule : ICinematicWorkstationModule
    {
        public string ModuleName => "Debug Overlay";

        private int _previewCinematicId;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Cinematic Debug Overlay", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view live cinematic state.", MessageType.Info);
                return;
            }

            var world = CinematicWorkstationWindow.GetCinematicWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No active ECS world found.", MessageType.Warning);
                return;
            }

            // Find CinematicState singleton
            var stateQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CinematicState>());
            if (stateQuery.CalculateEntityCount() == 0)
            {
                EditorGUILayout.HelpBox("CinematicState singleton not found. CinematicBootstrapSystem may not have run.", MessageType.Warning);
                return;
            }

            var state = stateQuery.GetSingleton<CinematicState>();

            // State display
            EditorGUILayout.LabelField("Cinematic State", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var statusColor = state.IsPlaying ? Color.green : Color.gray;
            var statusText = state.IsPlaying ? "PLAYING" : "IDLE";
            EditorGUILayout.LabelField("Status", statusText);

            EditorGUILayout.IntField("Cinematic ID", state.CurrentCinematicId);
            EditorGUILayout.EnumPopup("Type", state.CinematicType);

            if (state.IsPlaying)
            {
                EditorGUILayout.Space(4);
                float progress = state.Duration > 0 ? state.ElapsedTime / state.Duration : 0f;
                var progressRect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
                EditorGUI.ProgressBar(progressRect, progress,
                    $"{state.ElapsedTime:F1}s / {state.Duration:F1}s ({progress:P0})");

                EditorGUILayout.FloatField("Blend Progress", state.BlendProgress);
                EditorGUILayout.Toggle("Can Skip", state.CanSkip);
                EditorGUILayout.LabelField("Skip Votes",
                    $"{state.SkipVotesReceived} / {state.TotalPlayersInScene}");
            }

            EditorGUI.indentLevel--;

            // Config display
            EditorGUILayout.Space(8);
            var configQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CinematicConfigSingleton>());
            if (configQuery.CalculateEntityCount() > 0)
            {
                var config = configQuery.GetSingleton<CinematicConfigSingleton>();
                EditorGUILayout.LabelField("Config", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.EnumPopup("Default Skip Policy", config.DefaultSkipPolicy);
                EditorGUILayout.FloatField("Blend In", config.BlendInDuration);
                EditorGUILayout.FloatField("Blend Out", config.BlendOutDuration);
                EditorGUILayout.FloatField("HUD Fade", config.HUDFadeDuration);
                EditorGUILayout.FloatField("Letterbox Height", config.LetterboxHeight);
                EditorGUI.indentLevel--;
            }

            // Registry info
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Registry", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var regQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CinematicRegistryManaged>());
            if (regQuery.CalculateEntityCount() > 0)
            {
                var regEntities = regQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                if (regEntities.Length > 0 && world.EntityManager.HasComponent<CinematicRegistryManaged>(regEntities[0]))
                {
                    var registry = world.EntityManager.GetComponentObject<CinematicRegistryManaged>(regEntities[0]);
                    if (registry != null && registry.Definitions != null)
                    {
                        EditorGUILayout.IntField("Registered Cinematics", registry.Definitions.Count);
                        EditorGUILayout.Toggle("Has Active Director", registry.ActiveDirector != null);
                        EditorGUILayout.Toggle("Has Cinematic Camera", registry.CinematicCamera != null);
                    }
                }
                regEntities.Dispose();
            }
            else
            {
                EditorGUILayout.LabelField("Registry not found");
            }

            EditorGUI.indentLevel--;

            // UI Provider status
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("UI Provider", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.Toggle("Has Provider", CinematicUIRegistry.HasProvider);
            EditorGUILayout.IntField("Event Queue Count", CinematicAnimEventQueue.Count);
            EditorGUI.indentLevel--;

            // Preview controls
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Preview Controls", EditorStyles.boldLabel);
            _previewCinematicId = EditorGUILayout.IntField("Cinematic ID", _previewCinematicId);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !state.IsPlaying;
            if (GUILayout.Button("Play Preview", GUILayout.Height(28)))
            {
                // Directly set CinematicState to trigger playback
                var newState = state;
                newState.IsPlaying = true;
                newState.CurrentCinematicId = _previewCinematicId;
                newState.ElapsedTime = 0f;
                newState.CanSkip = true;
                newState.CinematicType = CinematicType.FullCinematic;
                newState.TotalPlayersInScene = 1;
                stateQuery.SetSingleton(newState);
            }
            GUI.enabled = state.IsPlaying;
            if (GUILayout.Button("Force Stop", GUILayout.Height(28)))
            {
                var newState = state;
                newState.IsPlaying = false;
                stateQuery.SetSingleton(newState);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (!Application.isPlaying) return;

            // Draw trigger radii in scene view
            var world = CinematicWorkstationWindow.GetCinematicWorld();
            if (world == null || !world.IsCreated) return;

            var triggerQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CinematicTrigger>(),
                ComponentType.ReadOnly<Unity.Transforms.LocalToWorld>()
            );

            if (triggerQuery.CalculateEntityCount() == 0) return;

            var triggers = triggerQuery.ToComponentDataArray<CinematicTrigger>(Unity.Collections.Allocator.Temp);
            var transforms = triggerQuery.ToComponentDataArray<Unity.Transforms.LocalToWorld>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < triggers.Length; i++)
            {
                var trigger = triggers[i];
                Vector3 pos = transforms[i].Position;

                Handles.color = trigger.HasPlayed
                    ? new Color(0.5f, 0.5f, 0.5f, 0.3f)
                    : new Color(0.8f, 0.3f, 1f, 0.4f);

                Handles.DrawWireDisc(pos, Vector3.up, trigger.TriggerRadius);
                Handles.Label(pos + Vector3.up * 2f,
                    $"Cinematic #{trigger.CinematicId}\n{(trigger.HasPlayed ? "[Played]" : "[Ready]")}",
                    EditorStyles.whiteLabel);
            }

            triggers.Dispose();
            transforms.Dispose();
        }
    }
}
#endif
