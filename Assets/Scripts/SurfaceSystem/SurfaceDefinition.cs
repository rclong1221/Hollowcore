using UnityEngine;
using System.Collections.Generic;

namespace SurfaceSystem
{
    /// <summary>
    /// Defines the audio and visual effects for a specific surface type (e.g., Metal, Wood, Flesh).
    /// Used by the SurfaceManager to spawn effects on impact.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSurfaceDefinition", menuName = "DIG/Surface System/Surface Definition")]
    public class SurfaceDefinition : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("List of PhysicMaterial names that map to this surface.")]
        public List<string> PhysicMaterialNames = new List<string>();

        [Header("Audio")]
        [Tooltip("Random selection of audio clips to play on impact.")]
        public List<AudioClip> ImpactSounds = new List<AudioClip>();
        [Tooltip("Random selection of audio clips to play for footsteps.")]
        public List<AudioClip> FootstepSounds = new List<AudioClip>();

        [Header("Visual Effects")]
        [Tooltip("Prefab to spawn for generic impacts (e.g., bullet hit particles).")]
        public GameObject ImpactEffectPrefab;
        [Tooltip("Prefab to spawn for slash/melee impacts.")]
        public GameObject SlashEffectPrefab;
        
        [Header("Decals")]
        [Tooltip("Decal material/prefab to place on this surface.")]
        public Material DecalMaterial;
        [Tooltip("Random selection of decal textures (if using a shared material).")]
        public List<Texture2D> DecalTextures = new List<Texture2D>();
    }
}
