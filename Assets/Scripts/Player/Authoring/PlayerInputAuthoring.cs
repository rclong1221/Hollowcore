using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Player.Components;

namespace Player.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Player/Authoring/Player Input Authoring")]
    public class PlayerInputAuthoring : MonoBehaviour
    {
        [Header("Input Settings")]
        [Tooltip("Mouse sensitivity for camera rotation")]
        [Range(0.1f, 5.0f)]
        public float MouseSensitivity = 1.0f;
        
        [Tooltip("Mouse sensitivity when aiming down sights")]
        [Range(0.1f, 2.0f)]
        public float MouseSensitivityADS = 0.5f;
        
        [Tooltip("Input dead zone threshold")]
        [Range(0.0f, 0.5f)]
        public float DeadZone = 0.1f;
        
        [Tooltip("Invert Y axis for camera look")]
        public bool InvertY = false;
    }

    public class PlayerInputAuthoringBaker : Baker<PlayerInputAuthoring>
    {
        public override void Bake(PlayerInputAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add an empty PlayerInput component for NetCode/Prediction systems to write into
            AddComponent<PlayerInput>(entity);

            // Initialize the hybrid PlayerInputComponent for MonoBehaviour-driven systems
            AddComponent(entity, new PlayerInputComponent
            {
                Move = new float2(0f, 0f),
                LookDelta = new float2(0f, 0f),
                ZoomDelta = 0f,
                Jump = 0,
                Crouch = 0,
                Sprint = 0,
                DodgeRoll = 0
            });

            // Add input settings
            AddComponent(entity, new PlayerInputSettings
            {
                MouseSensitivity = authoring.MouseSensitivity,
                MouseSensitivityADS = authoring.MouseSensitivityADS,
                DeadZone = authoring.DeadZone,
                InvertY = authoring.InvertY
            });
        }
    }
}
