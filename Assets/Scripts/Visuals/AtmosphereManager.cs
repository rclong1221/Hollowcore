using System;
using System.Collections.Generic;
using UnityEngine;
using DIG.Survival.Environment; // EnvironmentZoneType

namespace Visuals
{
    [System.Serializable]
    public struct AtmosphereProfile
    {
        public EnvironmentZoneType ZoneType;
        public GameObject FogVolume; // Local Volumetric Fog or Global Volume
        public Color AmbientColor;
    }

    /// <summary>
    /// Manages switching of atmosphere volumes based on player zone.
    /// Referenced by AtmosphereSystem.
    /// </summary>
    public class AtmosphereManager : MonoBehaviour
    {
        public static AtmosphereManager Instance { get; private set; }
        
        [Header("Profiles")]
        public List<AtmosphereProfile> Profiles = new List<AtmosphereProfile>();
        
        [Header("Settings")]
        public float TransitionSpeed = 1.0f;
        
        private EnvironmentZoneType _currentType;
        
        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;
        }
        
        public void SetAtmosphere(EnvironmentZoneType type)
        {
            if (_currentType == type) return;
            _currentType = type;
            
            // Apply (Direct switch for MVP, transitions later)
            foreach (var profile in Profiles)
            {
                bool active = profile.ZoneType == type;
                if (profile.FogVolume != null) 
                    profile.FogVolume.SetActive(active);
                
                // If active, maybe lerp render settings?
                if (active)
                {
                    // RenderSettings.ambientLight = profile.AmbientColor; // Global lighting
                }
            }
        }
    }
}
