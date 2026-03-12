using UnityEditor;
using UnityEngine;

namespace DIG.Lobby.Editor
{
    /// <summary>
    /// EPIC 17.4: Live lobby state viewer for play mode.
    /// Shows players, ready status, connection IDs, ping, and Relay info.
    /// </summary>
    public class LobbyInspectorModule : ILobbyWorkstationModule
    {
        public string ModuleName => "Lobby Inspector";

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Lobby Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect live lobby state.", MessageType.Info);
                return;
            }

            var mgr = LobbyWorkstationWindow.GetLobbyManager();
            if (mgr == null)
            {
                EditorGUILayout.HelpBox("No LobbyManager instance found. Add LobbyManager to your scene.", MessageType.Warning);
                return;
            }

            // State
            EditorGUILayout.LabelField("Phase", mgr.Phase.ToString());
            EditorGUILayout.LabelField("Is Host", mgr.IsHost.ToString());
            EditorGUILayout.LabelField("Local Slot", mgr.LocalSlotIndex.ToString());
            EditorGUILayout.Space(4);

            var lobby = mgr.CurrentLobby;
            if (lobby == null)
            {
                EditorGUILayout.HelpBox("No active lobby.", MessageType.Info);
                return;
            }

            // Lobby info
            EditorGUILayout.LabelField("Lobby Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Lobby ID", lobby.LobbyId);
            EditorGUILayout.LabelField("Join Code", lobby.JoinCode);
            EditorGUILayout.LabelField("Map ID", lobby.MapId.ToString());
            EditorGUILayout.LabelField("Difficulty ID", lobby.DifficultyId.ToString());
            EditorGUILayout.LabelField("Game Mode", lobby.Mode.ToString());
            EditorGUILayout.LabelField("Private", lobby.IsPrivate.ToString());
            EditorGUILayout.LabelField("Players", $"{lobby.PlayerCount}/{lobby.MaxPlayers}");
            EditorGUILayout.Space(4);

            // Relay
            if (mgr.Transport != null)
            {
                EditorGUILayout.LabelField("Transport", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Relay Join Code", mgr.Transport.RelayJoinCode ?? "N/A");
            }

            EditorGUILayout.Space(4);

            // Player slots
            EditorGUILayout.LabelField("Players", EditorStyles.boldLabel);
            for (int i = 0; i < lobby.Players.Count; i++)
            {
                var slot = lobby.Players[i];
                EditorGUILayout.BeginHorizontal("box");

                if (slot.IsEmpty)
                {
                    EditorGUILayout.LabelField($"Slot {i}: [Empty]");
                }
                else
                {
                    string hostTag = slot.IsHost ? " [HOST]" : "";
                    string readyTag = slot.IsReady ? " [READY]" : "";
                    EditorGUILayout.LabelField($"Slot {i}: {slot.DisplayName}{hostTag}{readyTag}");
                    EditorGUILayout.LabelField($"Lv.{slot.Level}", GUILayout.Width(50));
                    EditorGUILayout.LabelField($"Conn:{slot.ConnectionId}", GUILayout.Width(70));
                    EditorGUILayout.LabelField($"{slot.PingMs}ms", GUILayout.Width(50));
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }

    /// <summary>
    /// EPIC 17.4: Validates MapDefinitionSO assets.
    /// Checks spawn count >= MaxPlayers for each map.
    /// </summary>
    public class MapRegistryModule : ILobbyWorkstationModule
    {
        public string ModuleName => "Map Registry";

        private MapDefinitionSO[] _cachedMaps;
        private double _lastCacheTime;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Map Registry", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_cachedMaps == null || EditorApplication.timeSinceStartup - _lastCacheTime > 2.0)
            {
                _cachedMaps = Resources.LoadAll<MapDefinitionSO>("");
                _lastCacheTime = EditorApplication.timeSinceStartup;
            }
            var maps = _cachedMaps;
            if (maps.Length == 0)
            {
                EditorGUILayout.HelpBox("No MapDefinitionSO assets found. Create them via Assets > Create > DIG > Lobby > Map Definition.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Found {maps.Length} map(s):");
            EditorGUILayout.Space(2);

            for (int i = 0; i < maps.Length; i++)
            {
                var map = maps[i];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{map.MapId}] {map.DisplayName}", EditorStyles.boldLabel);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                    Selection.activeObject = map;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"Players: {map.MinPlayers}-{map.MaxPlayers}  |  Duration: ~{map.EstimatedMinutes}min");

                int spawnCount = map.SpawnPositions != null ? map.SpawnPositions.Length : 0;
                if (spawnCount < map.MaxPlayers)
                {
                    EditorGUILayout.HelpBox(
                        $"Spawn positions ({spawnCount}) < MaxPlayers ({map.MaxPlayers}). Add more spawn points!",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField($"Spawn points: {spawnCount} (OK)");
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }

    /// <summary>
    /// EPIC 17.4: Validates DifficultyDefinitionSO assets.
    /// Checks scaling values are within reasonable ranges.
    /// </summary>
    public class DifficultyRegistryModule : ILobbyWorkstationModule
    {
        public string ModuleName => "Difficulty Registry";

        private DifficultyDefinitionSO[] _cachedDiffs;
        private double _lastCacheTime;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Difficulty Registry", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_cachedDiffs == null || EditorApplication.timeSinceStartup - _lastCacheTime > 2.0)
            {
                _cachedDiffs = Resources.LoadAll<DifficultyDefinitionSO>("");
                _lastCacheTime = EditorApplication.timeSinceStartup;
            }
            var diffs = _cachedDiffs;
            if (diffs.Length == 0)
            {
                EditorGUILayout.HelpBox("No DifficultyDefinitionSO assets found. Create them via Assets > Create > DIG > Lobby > Difficulty Definition.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Found {diffs.Length} difficulty preset(s):");
            EditorGUILayout.Space(2);

            for (int i = 0; i < diffs.Length; i++)
            {
                var diff = diffs[i];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{diff.DifficultyId}] {diff.DisplayName}", EditorStyles.boldLabel);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                    Selection.activeObject = diff;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"HP: x{diff.EnemyHealthScale:F1}  |  DMG: x{diff.EnemyDamageScale:F1}  |  Spawn: x{diff.EnemySpawnRateScale:F1}");
                EditorGUILayout.LabelField($"Loot Qty: x{diff.LootQuantityScale:F1}  |  Quality: +{diff.LootQualityBonus:F1}  |  XP: x{diff.XPMultiplier:F1}");

                // Validation
                if (diff.EnemyHealthScale <= 0f || diff.EnemyDamageScale <= 0f)
                {
                    EditorGUILayout.HelpBox("Health/Damage scale must be > 0!", MessageType.Error);
                }
                if (diff.XPMultiplier <= 0f)
                {
                    EditorGUILayout.HelpBox("XP multiplier must be > 0!", MessageType.Warning);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
