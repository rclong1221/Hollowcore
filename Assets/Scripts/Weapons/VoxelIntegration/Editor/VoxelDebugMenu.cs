using UnityEditor;
using UnityEngine;
using DIG.Voxel;

namespace DIG.Voxel.Editor
{
    public static class VoxelDebugMenu
    {
        private const string MENU_PATH = "DIG/Voxel/Debug/Toggle Hit Logs";

        [MenuItem(MENU_PATH)]
        private static void ToggleHitLogs()
        {
            // Toggle based on one of the systems (assuming they are synced)
            bool newState = !MeleeVoxelDamageSystem.EnableDebugLogs;
            
            // Apply to all relevant systems
            MeleeVoxelDamageSystem.EnableDebugLogs = newState;
            VoxelDamageProcessingSystem.EnableDebugLogs = newState;
            
            UnityEngine.Debug.Log($"[VoxelDebug] Hit Logs {(newState ? "ENABLED" : "DISABLED")}");
        }

        // Validate method to show checkmark
        [MenuItem(MENU_PATH, true)]
        private static bool ValidateToggleHitLogs()
        {
            Menu.SetChecked(MENU_PATH, MeleeVoxelDamageSystem.EnableDebugLogs);
            return true;
        }
    }
}
