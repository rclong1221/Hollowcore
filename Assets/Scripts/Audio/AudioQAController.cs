using UnityEngine;
using Audio.Systems;

namespace Audio.QA
{
    /// <summary>
    /// Runtime controller for the Audio_QA test scene.
    /// Displays telemetry and provides manual test triggers.
    /// </summary>
    public class AudioQAController : MonoBehaviour
    {
        [Header("References")]
        public AudioManager AudioManager;
        public VFXManager VFXManager;
        public SurfaceMaterialRegistry Registry;

        [Header("Test Settings")]
        public Transform TestPosition;
        public int[] TestMaterialIds = { 0, 1, 2 };

        [Header("Debug Display")]
        public bool ShowTelemetryGUI = true;

        private int _currentMaterialIndex = 0;
        private string _lastAction = "None";

        void Start()
        {
            if (AudioManager == null)
                AudioManager = FindAnyObjectByType<AudioManager>();
            if (VFXManager == null)
                VFXManager = FindAnyObjectByType<VFXManager>();
            if (Registry == null)
                Registry = Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");

            if (TestPosition == null)
                TestPosition = transform;

            AudioTelemetry.ResetCounters();
        }

        void Update()
        {
            // Manual test triggers
            if (Input.GetKeyDown(KeyCode.F1)) TestFootstep();
            if (Input.GetKeyDown(KeyCode.F2)) TestLanding();
            if (Input.GetKeyDown(KeyCode.F3)) TestJump();
            if (Input.GetKeyDown(KeyCode.F4)) TestRoll();
            if (Input.GetKeyDown(KeyCode.F5)) TestDive();
            if (Input.GetKeyDown(KeyCode.F6)) TestSlide();
            if (Input.GetKeyDown(KeyCode.F7)) TestClimb();
            if (Input.GetKeyDown(KeyCode.F8)) CycleMaterial();
            if (Input.GetKeyDown(KeyCode.F9)) ResetTelemetry();
            if (Input.GetKeyDown(KeyCode.F10)) TestVFX();
        }

        void TestFootstep()
        {
            int matId = GetCurrentMaterialId();
            // Stance: 0=Standing, 1=Crouching, 3=Running
            AudioManager?.PlayFootstep(matId, TestPosition.position, 0);
            _lastAction = $"Footstep (mat={matId})";
            AudioTelemetry.LogFootstep(matId, TestPosition.position);
        }

        void TestLanding()
        {
            int matId = GetCurrentMaterialId();
            // Landing uses footstep with high intensity (or could use dedicated landing clips)
            AudioManager?.PlayFootstep(matId, TestPosition.position, 0);
            _lastAction = $"Landing (mat={matId})";
            AudioTelemetry.LogLanding(matId, 1f, TestPosition.position);
        }

        void TestJump()
        {
            int matId = GetCurrentMaterialId();
            AudioManager?.PlayJump(matId, TestPosition.position, 1f);
            _lastAction = $"Jump (mat={matId})";
            AudioTelemetry.LogActionEvent("Jump", matId, TestPosition.position);
        }

        void TestRoll()
        {
            int matId = GetCurrentMaterialId();
            AudioManager?.PlayRoll(matId, TestPosition.position, 1f);
            _lastAction = $"Roll (mat={matId})";
            AudioTelemetry.LogActionEvent("Roll", matId, TestPosition.position);
        }

        void TestDive()
        {
            int matId = GetCurrentMaterialId();
            AudioManager?.PlayDive(matId, TestPosition.position, 1f);
            _lastAction = $"Dive (mat={matId})";
            AudioTelemetry.LogActionEvent("Dive", matId, TestPosition.position);
        }

        void TestSlide()
        {
            int matId = GetCurrentMaterialId();
            AudioManager?.PlaySlide(matId, TestPosition.position, 1f);
            _lastAction = $"Slide (mat={matId})";
            AudioTelemetry.LogActionEvent("Slide", matId, TestPosition.position);
        }

        void TestClimb()
        {
            int matId = GetCurrentMaterialId();
            AudioManager?.PlayClimb(matId, TestPosition.position);
            _lastAction = $"Climb (mat={matId})";
            AudioTelemetry.LogActionEvent("Climb", matId, TestPosition.position);
        }

        void TestVFX()
        {
            int matId = GetCurrentMaterialId();
            VFXManager?.PlayVFXForMaterial(matId, TestPosition.position);
            _lastAction = $"VFX (mat={matId})";
        }

        void CycleMaterial()
        {
            _currentMaterialIndex = (_currentMaterialIndex + 1) % TestMaterialIds.Length;
            int matId = GetCurrentMaterialId();
            string matName = GetMaterialName(matId);
            _lastAction = $"Material: {matName} (ID={matId})";
        }

        void ResetTelemetry()
        {
            AudioTelemetry.ResetCounters();
            _lastAction = "Telemetry Reset";
        }

        int GetCurrentMaterialId()
        {
            if (TestMaterialIds == null || TestMaterialIds.Length == 0) return 0;
            return TestMaterialIds[_currentMaterialIndex % TestMaterialIds.Length];
        }

        string GetMaterialName(int id)
        {
            if (Registry != null && Registry.TryGetById(id, out var mat))
                return mat.DisplayName ?? $"ID_{id}";
            return $"ID_{id}";
        }

        void OnGUI()
        {
            if (!ShowTelemetryGUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 350, 400));
            GUILayout.BeginVertical("box");

            GUILayout.Label("=== Audio QA Controller ===", GUI.skin.box);
            GUILayout.Space(5);

            // Current state
            int matId = GetCurrentMaterialId();
            string matName = GetMaterialName(matId);
            GUILayout.Label($"Current Material: {matName} (ID={matId})");
            GUILayout.Label($"Last Action: {_lastAction}");

            GUILayout.Space(10);
            GUILayout.Label("=== Controls ===");
            GUILayout.Label("F1: Footstep  |  F2: Landing  |  F3: Jump");
            GUILayout.Label("F4: Roll  |  F5: Dive  |  F6: Slide  |  F7: Climb");
            GUILayout.Label("F8: Cycle Material  |  F9: Reset  |  F10: VFX");

            GUILayout.Space(10);
            GUILayout.Label("=== Telemetry ===");
            GUILayout.Label(AudioTelemetry.GetSummary());

            if (VFXManager != null)
            {
                GUILayout.Space(5);
                GUILayout.Label($"VFX Spawns: {VFXManager.TotalSpawnsThisSession}");
                GUILayout.Label($"VFX Pool Hits: {VFXManager.PoolHitsThisSession}");
                GUILayout.Label($"VFX Culled: {VFXManager.CulledThisSession}");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
