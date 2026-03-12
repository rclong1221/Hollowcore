#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using DIG.CameraSystem;

namespace DIG.Core.Input.Editor
{
    /// <summary>
    /// Editor utility for generating default paradigm profile assets.
    /// Creates pre-configured profiles for common game genres.
    /// 
    /// Menu: Tools > DIG > Input > Generate Default Paradigm Profiles
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public static class ParadigmProfileGenerator
    {
        private const string PROFILES_PATH = "Assets/Data/Input/Profiles";

        [MenuItem("Tools/DIG/Input/Generate Default Paradigm Profiles")]
        public static void GenerateDefaultProfiles()
        {
            // Ensure directory exists
            EnsureDirectoryExists(PROFILES_PATH);

            // Generate each profile
            CreateShooterProfile();
            CreateShooterHybridProfile();
            CreateMMOProfile();
            CreateARPGClassicProfile();
            CreateARPGHybridProfile();
            CreateTwinStickProfile();
            CreateMOBAProfile();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ParadigmProfileGenerator] Created default profiles in {PROFILES_PATH}");
            
            // Open the folder
            var folder = AssetDatabase.LoadAssetAtPath<Object>(PROFILES_PATH);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void CreateShooterProfile()
        {
            var profile = ScriptableObject.CreateInstance<InputParadigmProfile>();
            profile.paradigm = InputParadigm.Shooter;
            profile.displayName = "Shooter/Souls";
            profile.description = "Mouse always controls camera. WASD moves relative to camera. Cursor locked.";

            profile.cursorFreeByDefault = false;
            profile.temporaryCursorFreeKey = KeyCode.None;
            profile.cameraOrbitMode = CameraOrbitMode.AlwaysOrbit;

            profile.wasdEnabled = true;
            profile.clickToMoveEnabled = false;
            profile.clickToMoveButton = ClickToMoveButton.None;
            profile.usePathfinding = false;
            profile.facingMode = MovementFacingMode.CameraForward;
            profile.adTurnsCharacter = false;

            profile.qeRotationEnabled = false;
            profile.edgePanEnabled = false;
            profile.scrollZoomEnabled = true;

            profile.compatibleCameraModes = new[] { CameraMode.ThirdPersonFollow };

            SaveProfile(profile, "Profile_Shooter");
        }

        private static void CreateShooterHybridProfile()
        {
            var profile = ScriptableObject.CreateInstance<InputParadigmProfile>();
            profile.paradigm = InputParadigm.Shooter;
            profile.displayName = "Shooter (Hybrid)";
            profile.description = "Mouse controls camera. Hold Alt to free cursor for UI/hover.";

            profile.cursorFreeByDefault = false;
            profile.temporaryCursorFreeKey = KeyCode.LeftAlt;
            profile.cameraOrbitMode = CameraOrbitMode.AlwaysOrbit;

            profile.wasdEnabled = true;
            profile.clickToMoveEnabled = false;
            profile.clickToMoveButton = ClickToMoveButton.None;
            profile.usePathfinding = false;
            profile.facingMode = MovementFacingMode.CameraForward;
            profile.adTurnsCharacter = false;

            profile.qeRotationEnabled = false;
            profile.edgePanEnabled = false;
            profile.scrollZoomEnabled = true;

            profile.compatibleCameraModes = new[] { CameraMode.ThirdPersonFollow };

            SaveProfile(profile, "Profile_ShooterHybrid");
        }

        private static void CreateMMOProfile()
        {
            var profile = ScriptableObject.CreateInstance<InputParadigmProfile>();
            profile.paradigm = InputParadigm.MMO;
            profile.displayName = "MMO/RPG";
            profile.description = "Free cursor. RMB hold for camera orbit. A/D turn, RMB+A/D strafe.";

            profile.cursorFreeByDefault = true;
            profile.temporaryCursorFreeKey = KeyCode.None;
            profile.cameraOrbitMode = CameraOrbitMode.ButtonHoldOrbit;

            profile.wasdEnabled = true;
            profile.clickToMoveEnabled = false;
            profile.clickToMoveButton = ClickToMoveButton.None;
            profile.usePathfinding = false;
            profile.facingMode = MovementFacingMode.MovementDirection;
            profile.adTurnsCharacter = true;

            profile.qeRotationEnabled = false;
            profile.edgePanEnabled = false;
            profile.scrollZoomEnabled = true;

            profile.compatibleCameraModes = new[] { CameraMode.ThirdPersonFollow };

            SaveProfile(profile, "Profile_MMO");
        }

        private static void CreateARPGClassicProfile()
        {
            var profile = ScriptableObject.CreateInstance<InputParadigmProfile>();
            profile.paradigm = InputParadigm.ARPG;
            profile.displayName = "ARPG (Classic)";
            profile.description = "Click to move. Isometric camera. No WASD. Diablo 2/3 style.";

            profile.cursorFreeByDefault = true;
            profile.temporaryCursorFreeKey = KeyCode.None;
            profile.cameraOrbitMode = CameraOrbitMode.KeyRotateOnly;

            profile.wasdEnabled = false;
            profile.clickToMoveEnabled = true;
            profile.clickToMoveButton = ClickToMoveButton.LeftButton;
            profile.usePathfinding = true;
            profile.facingMode = MovementFacingMode.MovementDirection; // Body faces movement, head IK looks at cursor
            profile.adTurnsCharacter = false;

            profile.qeRotationEnabled = true;
            profile.edgePanEnabled = false;
            profile.scrollZoomEnabled = true;
            profile.useScreenRelativeMovement = true;

            profile.compatibleCameraModes = new[] { CameraMode.IsometricFixed, CameraMode.IsometricRotatable };

            SaveProfile(profile, "Profile_ARPG_Classic");
        }

        private static void CreateARPGHybridProfile()
        {
            var profile = ScriptableObject.CreateInstance<InputParadigmProfile>();
            profile.paradigm = InputParadigm.ARPG;
            profile.displayName = "ARPG (Hybrid)";
            profile.description = "Click to move OR WASD. Isometric camera. Diablo 4 / Last Epoch style.";

            profile.cursorFreeByDefault = true;
            profile.temporaryCursorFreeKey = KeyCode.None;
            profile.cameraOrbitMode = CameraOrbitMode.KeyRotateOnly;

            profile.wasdEnabled = true;
            profile.clickToMoveEnabled = true;
            profile.clickToMoveButton = ClickToMoveButton.LeftButton;
            profile.usePathfinding = true;
            profile.facingMode = MovementFacingMode.MovementDirection; // Body faces movement, head IK looks at cursor
            profile.adTurnsCharacter = false;

            profile.qeRotationEnabled = true;
            profile.edgePanEnabled = false;
            profile.scrollZoomEnabled = true;
            profile.useScreenRelativeMovement = true;

            profile.compatibleCameraModes = new[] { CameraMode.IsometricFixed, CameraMode.IsometricRotatable };

            SaveProfile(profile, "Profile_ARPG_Hybrid");
        }

        private static void CreateTwinStickProfile()
        {
            var profile = ScriptableObject.CreateInstance<InputParadigmProfile>();
            profile.paradigm = InputParadigm.TwinStick;
            profile.displayName = "Twin-Stick";
            profile.description = "WASD to move, mouse to aim. Character always faces cursor. Hades style.";

            profile.cursorFreeByDefault = true;
            profile.temporaryCursorFreeKey = KeyCode.None;
            profile.cameraOrbitMode = CameraOrbitMode.FollowOnly;

            profile.wasdEnabled = true;
            profile.clickToMoveEnabled = false;
            profile.clickToMoveButton = ClickToMoveButton.None;
            profile.usePathfinding = false;
            profile.facingMode = MovementFacingMode.CursorDirection;
            profile.adTurnsCharacter = false;

            profile.qeRotationEnabled = false;
            profile.edgePanEnabled = false;
            profile.scrollZoomEnabled = true;
            profile.useScreenRelativeMovement = true;

            profile.compatibleCameraModes = new[] { CameraMode.IsometricFixed, CameraMode.IsometricRotatable };

            SaveProfile(profile, "Profile_TwinStick");
        }

        private static void CreateMOBAProfile()
        {
            var profile = ScriptableObject.CreateInstance<InputParadigmProfile>();
            profile.paradigm = InputParadigm.MOBA;
            profile.displayName = "MOBA";
            profile.description = "RMB to move. LMB to select. Top-down camera with edge pan.";

            profile.cursorFreeByDefault = true;
            profile.temporaryCursorFreeKey = KeyCode.None;
            profile.cameraOrbitMode = CameraOrbitMode.FollowOnly;

            profile.wasdEnabled = false;
            profile.clickToMoveEnabled = true;
            profile.clickToMoveButton = ClickToMoveButton.RightButton;
            profile.usePathfinding = true;
            profile.facingMode = MovementFacingMode.MovementDirection; // Body faces movement, head IK handles cursor look
            profile.adTurnsCharacter = false;

            profile.qeRotationEnabled = false;
            profile.edgePanEnabled = true;
            profile.scrollZoomEnabled = true;
            profile.useScreenRelativeMovement = true;

            profile.compatibleCameraModes = new[] { CameraMode.TopDownFixed };

            SaveProfile(profile, "Profile_MOBA");
        }

        private static void SaveProfile(InputParadigmProfile profile, string filename)
        {
            // IMPORTANT: Set the name property explicitly before saving
            // This ensures m_Name is serialized correctly in the asset file
            profile.name = filename;
            
            var path = $"{PROFILES_PATH}/{filename}.asset";
            
            // Check if exists
            var existing = AssetDatabase.LoadAssetAtPath<InputParadigmProfile>(path);
            if (existing != null)
            {
                // Preserve the name when copying
                EditorUtility.CopySerialized(profile, existing);
                existing.name = filename; // Ensure name is preserved after copy
                EditorUtility.SetDirty(existing);
                Debug.Log($"[ParadigmProfileGenerator] Updated: {path}");
            }
            else
            {
                AssetDatabase.CreateAsset(profile, path);
                Debug.Log($"[ParadigmProfileGenerator] Created: {path}");
            }
        }

        [MenuItem("Tools/DIG/Input/Open Paradigm Profiles Folder")]
        public static void OpenProfilesFolder()
        {
            EnsureDirectoryExists(PROFILES_PATH);
            
            var folder = AssetDatabase.LoadAssetAtPath<Object>(PROFILES_PATH);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
        }
    }
}
#endif
