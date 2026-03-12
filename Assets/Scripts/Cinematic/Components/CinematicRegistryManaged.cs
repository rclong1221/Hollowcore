using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Playables;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Managed singleton holding cinematic definition lookups and
    /// active playback references. Stored as IComponentData via AddComponentObject.
    /// </summary>
    public class CinematicRegistryManaged : IComponentData
    {
        /// <summary>CinematicId -> CinematicDefinitionSO lookup.</summary>
        public Dictionary<int, CinematicDefinitionSO> Definitions;

        /// <summary>Currently playing PlayableDirector (null if idle).</summary>
        public PlayableDirector ActiveDirector;

        /// <summary>Dedicated cinematic camera (disabled when idle).</summary>
        public Camera CinematicCamera;

        /// <summary>Instantiated camera rig GameObject for cleanup.</summary>
        public GameObject CameraRigInstance;

        /// <summary>Whether bootstrap has completed initialization.</summary>
        public bool IsInitialized;
    }
}
