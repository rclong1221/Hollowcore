using UnityEngine;
using UnityEditor;
using DIG.UI;

namespace DIG.Editor
{
    public static class CrosshairSetup
    {
        [MenuItem("DIG/UI/Create Crosshair")]
        public static void CreateCrosshair()
        {
            // Check if one already exists
            var existing = Object.FindFirstObjectByType<CrosshairUI>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog("Crosshair Exists", "A crosshair already exists in the scene.", "OK");
                return;
            }
            
            // Create crosshair
            var go = new GameObject("Crosshair");
            go.AddComponent<CrosshairUI>();
            
            Undo.RegisterCreatedObjectUndo(go, "Create Crosshair");
            Selection.activeGameObject = go;
            
            Debug.Log("[CrosshairSetup] Created crosshair UI. Adjust settings in Inspector.");
        }
    }
}
