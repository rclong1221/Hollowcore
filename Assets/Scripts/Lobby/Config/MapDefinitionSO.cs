using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Defines a playable map with spawn positions, player limits,
    /// and supported game modes. Place in Resources/Maps/ folder.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Lobby/Map Definition")]
    public class MapDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int MapId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Thumbnail;

        [Header("Scene")]
        [Tooltip("Addressable scene path for this map.")]
        public string ScenePath;

        [Tooltip("SubScene asset paths loaded into ECS worlds.")]
        public string[] SubscenePaths;

        [Header("Player Limits")]
        [Range(1, 6)] public int MinPlayers = 1;
        [Range(1, 6)] public int MaxPlayers = 4;
        public int EstimatedMinutes = 30;

        [Header("Game Modes")]
        public GameMode[] SupportedGameModes = { GameMode.Cooperative };

        [Header("Spawn Points")]
        [Tooltip("One position per player slot.")]
        public Vector3[] SpawnPositions = { new Vector3(0, 1, 0), new Vector3(2, 1, 0), new Vector3(4, 1, 0), new Vector3(6, 1, 0) };
        public Quaternion[] SpawnRotations = { Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity };

        [Header("Unlock")]
        [Tooltip("Minimum player level to select this map (0 = always available).")]
        public int UnlockRequirement;

        public bool SupportsMode(GameMode mode)
        {
            if (SupportedGameModes == null) return false;
            for (int i = 0; i < SupportedGameModes.Length; i++)
                if (SupportedGameModes[i] == mode) return true;
            return false;
        }
    }
}
