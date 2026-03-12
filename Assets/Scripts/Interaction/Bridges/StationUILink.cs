using UnityEngine;
using DIG.Interaction.Systems;

namespace DIG.Interaction.Bridges
{
    /// <summary>
    /// EPIC 16.1 Phase 2: MonoBehaviour for station UI registration.
    ///
    /// Place on a station GameObject alongside StationAuthoring.
    /// Registers with StationSessionBridgeSystem's static registry on Enable.
    /// When the player enters a session with a matching SessionID, the bridge
    /// system calls OpenUI() / CloseUI().
    ///
    /// Follows the same pattern as InteractableHybridLink.
    /// </summary>
    public class StationUILink : MonoBehaviour
    {
        [Header("Station UI Configuration")]
        [Tooltip("Must match the SessionID on StationAuthoring")]
        public int SessionID;

        [Tooltip("UI prefab to instantiate when session opens")]
        public GameObject UIPrefab;

        [Header("Runtime")]
        [Tooltip("Currently active UI instance (if any)")]
        [SerializeField] private GameObject _activeUI;

        private void OnEnable()
        {
            StationSessionBridgeSystem.RegisterStationUI(SessionID, this);
        }

        private void OnDisable()
        {
            CloseUI();
            StationSessionBridgeSystem.UnregisterStationUI(SessionID);
        }

        /// <summary>
        /// Open the station UI. Called by StationSessionBridgeSystem when player enters session.
        /// </summary>
        public void OpenUI(SessionType sessionType)
        {
            if (_activeUI != null)
                return; // Already open

            if (UIPrefab == null)
                return;

            _activeUI = Instantiate(UIPrefab);

            // Position world-space UI near the station
            if (sessionType == SessionType.WorldSpace)
            {
                _activeUI.transform.position = transform.position + Vector3.up * 2f;
            }
        }

        /// <summary>
        /// Close the station UI. Called by StationSessionBridgeSystem when player exits session.
        /// </summary>
        public void CloseUI()
        {
            if (_activeUI != null)
            {
                Destroy(_activeUI);
                _activeUI = null;
            }
        }

        /// <summary>
        /// Whether the UI is currently open.
        /// </summary>
        public bool IsUIOpen => _activeUI != null;
    }
}
