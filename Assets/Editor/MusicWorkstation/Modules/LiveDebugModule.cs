#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Entities;

namespace DIG.Music.Editor
{
    /// <summary>
    /// EPIC 17.5: Play-mode live debug showing current track, intensity meter,
    /// active stems, zone stack, transition state, and stinger queue.
    /// </summary>
    public class LiveDebugModule : IMusicWorkstationModule
    {
        public string ModuleName => "Live Debug";

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Live Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live music state.", MessageType.Info);
                return;
            }

            // Find MusicState singleton in client world
            MusicState musicState = default;
            MusicConfig config = default;
            MusicDatabaseSO database = null;
            bool found = false;

            foreach (var world in World.All)
            {
                if ((world.Flags & WorldFlags.GameClient) == 0 &&
                    (world.Flags & WorldFlags.Game) == 0) continue;

                var em = world.EntityManager;
                var stateQuery = em.CreateEntityQuery(ComponentType.ReadOnly<MusicState>());
                if (stateQuery.CalculateEntityCount() > 0)
                {
                    musicState = stateQuery.GetSingleton<MusicState>();
                    found = true;

                    var configQuery = em.CreateEntityQuery(ComponentType.ReadOnly<MusicConfig>());
                    if (configQuery.CalculateEntityCount() > 0)
                        config = configQuery.GetSingleton<MusicConfig>();

                    var dbQuery = em.CreateEntityQuery(ComponentType.ReadOnly<MusicDatabaseManaged>());
                    if (dbQuery.CalculateEntityCount() > 0)
                        database = dbQuery.GetSingleton<MusicDatabaseManaged>().Database;

                    break;
                }
            }

            if (!found)
            {
                EditorGUILayout.HelpBox("MusicState singleton not found. Is MusicBootstrapSystem running? Check Resources/MusicConfig and Resources/MusicDatabase.", MessageType.Warning);
                return;
            }

            // Track info
            string trackName = "Unknown";
            string targetName = "Unknown";
            if (database != null)
            {
                var currentTrack = database.GetTrack(musicState.CurrentTrackId);
                if (currentTrack != null) trackName = currentTrack.TrackName;
                var targetTrack = database.GetTrack(musicState.TargetTrackId);
                if (targetTrack != null) targetName = targetTrack.TrackName;
            }

            EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Current Track: {trackName} (ID: {musicState.CurrentTrackId})");
            EditorGUILayout.LabelField($"Target Track: {targetName} (ID: {musicState.TargetTrackId})");

            // Intensity meter
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Combat Intensity", EditorStyles.boldLabel);
            var intensityRect = GUILayoutUtility.GetRect(200, 20);
            EditorGUI.ProgressBar(intensityRect, musicState.SmoothedIntensity,
                $"{musicState.SmoothedIntensity:P0} (raw: {musicState.CombatIntensity:F2})");
            EditorGUILayout.LabelField($"In Combat: {musicState.IsInCombat}");

            // Stem volumes
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Stem Volumes", EditorStyles.boldLabel);
            DrawStemBar("Base", musicState.StemVolumes.x);
            DrawStemBar("Percussion", musicState.StemVolumes.y);
            DrawStemBar("Melody", musicState.StemVolumes.z);
            DrawStemBar("Intensity", musicState.StemVolumes.w);

            // Transition
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Transition", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Crossfade: {musicState.CrossfadeProgress:F2} (dir={musicState.CrossfadeDirection})");
            EditorGUILayout.LabelField($"Zone Priority: {musicState.CurrentZonePriority}");
            EditorGUILayout.LabelField($"Fade In: {musicState.ZoneFadeInDuration:F1}s | Fade Out: {musicState.ZoneFadeOutDuration:F1}s");

            // Boss override
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Boss Override: {(musicState.BossOverrideTrackId != 0 ? musicState.BossOverrideTrackId.ToString() : "None")}");
            EditorGUILayout.LabelField($"Stinger Cooldown: {musicState.StingerCooldown:F1}s");

            // Telemetry
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Telemetry", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Track Transitions: {Audio.Systems.AudioTelemetry.TrackTransitionsThisSession}");
            EditorGUILayout.LabelField($"Stingers Played: {Audio.Systems.AudioTelemetry.StingersPlayedThisSession}");
            EditorGUILayout.LabelField($"Active Stems: {Audio.Systems.AudioTelemetry.ActiveStemCount}");
        }

        private void DrawStemBar(string label, float vol)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(80));
            var rect = GUILayoutUtility.GetRect(200, 14);
            EditorGUI.ProgressBar(rect, vol, $"{vol:F2}");
            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
