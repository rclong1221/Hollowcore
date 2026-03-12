using System;
using UnityEngine;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: ScriptableObject defining a PvP arena map.
    /// Loaded from Resources/PvPMaps/ by PvPBootstrapSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "PvPMap", menuName = "DIG/PvP/Map Definition")]
    public class PvPMapDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public byte MapId;
        public string MapName;
        [TextArea(2, 4)] public string MapDescription;

        [Header("Capacity")]
        [Range(2, 16)] public byte MaxPlayers = 8;
        [Tooltip("0 = FFA, 2 or 4 = team modes.")]
        [Range(0, 4)] public byte TeamCount = 2;

        [Header("Supported Modes")]
        public PvPGameMode[] SupportedModes = { PvPGameMode.FreeForAll, PvPGameMode.TeamDeathmatch };

        [Header("Spawn Points")]
        public PvPSpawnPointEntry[] SpawnPoints;

        [Header("Capture Zones (CapturePoint mode)")]
        public PvPCaptureZoneEntry[] CaptureZones;

        [Header("Arena")]
        [Tooltip("Path to arena subscene asset.")]
        public string ArenaSubscenePath;

        public bool SupportsMode(PvPGameMode mode)
        {
            if (SupportedModes == null) return false;
            for (int i = 0; i < SupportedModes.Length; i++)
                if (SupportedModes[i] == mode) return true;
            return false;
        }
    }

    [Serializable]
    public struct PvPSpawnPointEntry
    {
        public byte TeamId;
        public byte SpawnIndex;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    [Serializable]
    public struct PvPCaptureZoneEntry
    {
        public byte ZoneId;
        public Vector3 Position;
        public float Radius;
        public float PointsPerSecond;
    }
}
