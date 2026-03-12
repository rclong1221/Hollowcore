#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace DIG.ProceduralMotion.Editor
{
    /// <summary>
    /// EPIC 15.25 Phase 6: Wizard for creating weapon class preset profiles.
    /// Window > DIG > Create Motion Profile Presets
    /// Creates 7 pre-configured ProceduralMotionProfile assets based on weapon class defaults.
    /// </summary>
    public class ProceduralMotionProfilePresetCreator : EditorWindow
    {
        private string _outputFolder = "Assets/Resources/MotionProfiles";

        [MenuItem("Window/DIG/Create Motion Profile Presets")]
        public static void ShowWindow()
        {
            GetWindow<ProceduralMotionProfilePresetCreator>("Motion Profile Presets");
        }

        private void OnGUI()
        {
            GUILayout.Label("Procedural Motion Profile Preset Creator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Creates 7 preset profiles: Default, Pistol, Rifle, LMG, Shotgun, Melee, Bow.\n" +
                "Values match the EPIC 15.25 weapon class tuning table.",
                MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create All Presets", GUILayout.Height(30)))
            {
                CreateAllPresets();
            }
        }

        private void CreateAllPresets()
        {
            if (!AssetDatabase.IsValidFolder(_outputFolder))
            {
                string parent = System.IO.Path.GetDirectoryName(_outputFolder);
                string folder = System.IO.Path.GetFileName(_outputFolder);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
                    AssetDatabase.CreateFolder(parent, folder);
            }

            CreatePreset("Default", 1.5f, 0.025f, 1.8f, 0.005f, -0.03f, 8f, 0.7f);
            CreatePreset("Pistol", 2.0f, 0.02f, 2.0f, 0.003f, -0.02f, 12f, 0.6f);
            CreatePreset("Rifle", 1.5f, 0.025f, 1.8f, 0.005f, -0.03f, 8f, 0.7f);
            CreatePreset("LMG", 0.8f, 0.03f, 1.5f, 0.008f, -0.05f, 5f, 0.5f);
            CreatePreset("Shotgun", 1.8f, 0.025f, 1.8f, 0.004f, -0.06f, 10f, 0.55f);
            CreatePreset("Melee", 2.5f, 0.035f, 2.2f, 0.006f, 0f, 6f, 0.65f);
            CreatePreset("Bow", 1.0f, 0.01f, 1.5f, 0.002f, -0.01f, 10f, 0.8f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EPIC 15.25] Created 7 motion profile presets in " + _outputFolder);
        }

        private void CreatePreset(string name, float swayRot, float bobY, float bobFreq,
            float inertia, float kickZ, float hz, float zeta)
        {
            var profile = ScriptableObject.CreateInstance<ProceduralMotionProfile>();

            // Sway
            profile.SwayRotationScale = swayRot;

            // Bob
            profile.BobAmplitudeY = bobY;
            profile.BobFrequency = bobFreq;

            // Inertia
            profile.InertiaPositionScale = inertia;

            // Visual Recoil
            profile.VisualRecoilKickZ = kickZ;

            // Spring defaults
            profile.DefaultPositionFrequency = new Vector3(hz, hz, hz);
            profile.DefaultPositionDampingRatio = new Vector3(zeta, zeta, zeta);
            profile.DefaultRotationFrequency = new Vector3(hz, hz, hz);
            profile.DefaultRotationDampingRatio = new Vector3(zeta, zeta, zeta);

            string path = $"{_outputFolder}/MotionProfile_{name}.asset";
            AssetDatabase.CreateAsset(profile, path);
        }
    }
}
#endif
