using UnityEngine;

namespace DIG.Swarm.Rendering
{
    /// <summary>
    /// EPIC 16.2 Phase 4: Managed singleton holding mesh, material, and VAT texture references
    /// for GPU-instanced swarm rendering. Attach to a scene GameObject.
    ///
    /// ECS systems can't hold managed references (Mesh, Material, Texture2D),
    /// so this MonoBehaviour provides them via static singleton access.
    /// </summary>
    public class SwarmRenderConfigManaged : MonoBehaviour
    {
        public static SwarmRenderConfigManaged Instance { get; private set; }

        [Header("Mesh LODs")]
        [Tooltip("Full detail mesh (0-30m)")]
        public Mesh FullMesh;
        [Tooltip("Reduced mesh ~50% polys (30-80m)")]
        public Mesh ReducedMesh;
        [Tooltip("Billboard quad (80-200m)")]
        public Mesh BillboardMesh;

        [Header("Materials")]
        [Tooltip("Material using SwarmVAT shader for full/reduced mesh")]
        public Material SwarmMaterial;
        [Tooltip("Material for billboard rendering")]
        public Material BillboardMaterial;

        [Header("Vertex Animation Textures")]
        [Tooltip("Position VAT: rows=frames, cols=vertices, RGB=XYZ")]
        public Texture2D PositionVAT;
        [Tooltip("Normal VAT: same layout as position")]
        public Texture2D NormalVAT;

        [Header("Animation Clips")]
        [Tooltip("Number of frames per animation clip")]
        public int[] ClipFrameCounts = { 30, 24, 18, 12, 8 }; // Idle, Walk, Run, Attack, Die
        [Tooltip("Frames per second for VAT playback")]
        public float VATFrameRate = 30f;
        [Tooltip("Total frames across all clips in VAT texture")]
        public int TotalVATFrames = 92;

        [Header("Rendering")]
        [Tooltip("Max render distance for swarm particles")]
        public float MaxRenderDistance = 200f;
        [Tooltip("Distance for LOD transition: full → reduced")]
        public float LODDistance1 = 30f;
        [Tooltip("Distance for LOD transition: reduced → billboard")]
        public float LODDistance2 = 80f;
        [Tooltip("Max distance for shadow casting")]
        public float ShadowDistance = 30f;
        [Tooltip("Enable shadow casting for near particles")]
        public bool CastShadows = true;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Get the VAT frame range for a given animation clip index.
        /// Returns (startFrame, frameCount) for shader sampling.
        /// </summary>
        public (int startFrame, int frameCount) GetClipRange(int clipIndex)
        {
            if (ClipFrameCounts == null || clipIndex < 0 || clipIndex >= ClipFrameCounts.Length)
                return (0, 1);

            int start = 0;
            for (int i = 0; i < clipIndex; i++)
                start += ClipFrameCounts[i];

            return (start, ClipFrameCounts[clipIndex]);
        }
    }
}
