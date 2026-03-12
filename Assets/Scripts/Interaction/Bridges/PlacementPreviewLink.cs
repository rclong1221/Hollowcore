using Unity.Mathematics;
using UnityEngine;
using DIG.Interaction.Systems;

namespace DIG.Interaction.Bridges
{
    /// <summary>
    /// EPIC 16.1 Phase 6: MonoBehaviour bridge for placement preview visuals.
    ///
    /// Follows the MinigameLink / InteractableHybridLink pattern:
    /// 1. Place on a scene GameObject
    /// 2. Assign preview prefab and valid/invalid materials
    /// 3. PlacementPreviewBridgeSystem calls ShowPreview/UpdatePreview/HidePreview
    ///
    /// The preview prefab should have MeshRenderer(s) — this link swaps their
    /// shared material to indicate valid (green) vs invalid (red) placement.
    /// </summary>
    public class PlacementPreviewLink : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Registry ID. Use 0 for default (most setups only need one).")]
        public int PreviewID = 0;

        [Tooltip("Prefab to instantiate as the placement preview")]
        public GameObject PreviewPrefab;

        [Header("Materials")]
        [Tooltip("Material applied when placement position is valid")]
        public Material ValidMaterial;

        [Tooltip("Material applied when placement position is invalid")]
        public Material InvalidMaterial;

        // Runtime state
        private GameObject _activeInstance;
        private Renderer[] _renderers;
        private bool _lastValid;

        private void OnEnable()
        {
            PlacementPreviewBridgeSystem.RegisterLink(PreviewID, this);
        }

        private void OnDisable()
        {
            PlacementPreviewBridgeSystem.UnregisterLink(PreviewID);
            HidePreview();
        }

        /// <summary>
        /// Called when the player enters placement mode.
        /// Instantiates the preview prefab and positions it.
        /// </summary>
        public void ShowPreview(float3 position, quaternion rotation, bool isValid)
        {
            HidePreview();

            if (PreviewPrefab == null)
                return;

            _activeInstance = Instantiate(PreviewPrefab,
                (Vector3)position, (Quaternion)rotation);

            _renderers = _activeInstance.GetComponentsInChildren<Renderer>();
            _lastValid = !isValid; // Force material update on first frame
            ApplyMaterial(isValid);
        }

        /// <summary>
        /// Called each frame while in placement mode to update position and validity.
        /// </summary>
        public void UpdatePreview(float3 position, quaternion rotation, bool isValid)
        {
            if (_activeInstance == null)
            {
                ShowPreview(position, rotation, isValid);
                return;
            }

            _activeInstance.transform.position = (Vector3)position;
            _activeInstance.transform.rotation = (Quaternion)rotation;

            if (isValid != _lastValid)
            {
                ApplyMaterial(isValid);
            }
        }

        /// <summary>
        /// Called when the player exits placement mode.
        /// Destroys the preview instance.
        /// </summary>
        public void HidePreview()
        {
            if (_activeInstance != null)
            {
                Destroy(_activeInstance);
                _activeInstance = null;
                _renderers = null;
            }
        }

        private void ApplyMaterial(bool isValid)
        {
            _lastValid = isValid;

            Material mat = isValid ? ValidMaterial : InvalidMaterial;
            if (mat == null || _renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].sharedMaterial = mat;
            }
        }

        /// <summary>Whether a preview is currently visible.</summary>
        public bool IsShowing => _activeInstance != null;
    }
}
