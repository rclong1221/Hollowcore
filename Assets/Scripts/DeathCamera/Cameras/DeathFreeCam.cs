using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using DIG.CameraSystem;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Free fly camera for death spectator mode.
    /// WASD movement, mouse look, Shift for fast movement.
    /// Implements ICameraMode for CameraModeProvider integration.
    /// </summary>
    public class DeathFreeCam : MonoBehaviour, ICameraMode
    {
        private CameraMode _reportedMode = CameraMode.ThirdPersonFollow;
        public CameraMode Mode => _reportedMode;

        private float _moveSpeed = 10f;
        private float _fastMultiplier = 3f;
        private float _mouseSensitivity = 2f;
        private float _yaw;
        private float _pitch;
        private float _fov = 60f;

        public bool SupportsOrbitRotation => true;
        public bool UsesCursorAiming => false;

        public void Configure(float moveSpeed, float fastMultiplier, float sensitivity, float fov)
        {
            _moveSpeed = moveSpeed;
            _fastMultiplier = fastMultiplier;
            _mouseSensitivity = sensitivity;
            _fov = fov;
        }

        /// <summary>Set the reported CameraMode to match the gameplay paradigm (cosmetic).</summary>
        public void SetReportedMode(CameraMode mode) { _reportedMode = mode; }

        /// <summary>Set initial position and rotation (e.g., from previous camera).</summary>
        public void SetInitialPose(Vector3 position, float yaw, float pitch)
        {
            transform.position = position;
            _yaw = yaw;
            _pitch = pitch;
        }

        public void Initialize(CameraConfig config) { }

        public void UpdateCamera(float deltaTime)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // Sprint
            bool sprint = kb != null && kb.leftShiftKey.isPressed;
            float speed = _moveSpeed * (sprint ? _fastMultiplier : 1f);

            // Mouse look
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * _mouseSensitivity * 0.1f;
                _pitch -= delta.y * _mouseSensitivity * 0.1f;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // WASD + Q/E movement
            float3 move = float3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed) move.z -= 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.eKey.isPressed) move.y += 1f;
                if (kb.qKey.isPressed) move.y -= 1f;
            }

            float3 worldMove = rotation * (Vector3)move;
            transform.position += (Vector3)(worldMove * speed * deltaTime);
            transform.rotation = rotation;
        }

        public Transform GetCameraTransform() => transform;

        public Plane GetAimPlane()
        {
            return new Plane(Vector3.up, transform.position);
        }

        public float3 TransformMovementInput(float2 input)
        {
            var rotation = Quaternion.Euler(0f, _yaw, 0f);
            return rotation * new Vector3(input.x, 0f, input.y);
        }

        public float3 TransformAimInput(float2 cursorScreenPos)
        {
            return transform.position + transform.forward * 100f;
        }

        public void SetTarget(Entity entity, Transform visualTransform = null) { }

        public void SetZoom(float zoomLevel) { }
        public float GetZoom() => 0.5f;

        public void Shake(float intensity, float duration) { }

        public void HandleRotationInput(float2 rotationInput)
        {
            _yaw += rotationInput.x * _mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - rotationInput.y * _mouseSensitivity, -89f, 89f);
        }
    }
}
