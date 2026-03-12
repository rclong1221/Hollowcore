using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using DIG.CameraSystem;
using UnityEngine;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Interface for pluggable death camera phases.
    /// Each phase controls the camera and UI during its active period.
    /// </summary>
    public interface IDeathCameraPhase
    {
        DeathCameraPhaseType PhaseType { get; }
        void Enter(DeathCameraContext context);
        void Update(float deltaTime);
        void Exit();
        bool IsComplete { get; }
        bool CanSkip { get; }
        void Skip();
    }

    /// <summary>
    /// EPIC 18.13: Context passed to each phase containing all data needed for the death experience.
    /// Built once by the orchestrator on death, shared across all phases.
    /// </summary>
    public class DeathCameraContext
    {
        /// <summary>The local player entity (dead).</summary>
        public Entity LocalPlayerEntity;

        /// <summary>The entity that killed the local player (may be Entity.Null).</summary>
        public Entity KillerEntity;

        /// <summary>World position where the kill happened.</summary>
        public float3 KillPosition;

        /// <summary>Ghost ID of the killer (0 if no killer).</summary>
        public ushort KillerGhostId;

        /// <summary>Display name of the killer.</summary>
        public string KillerName;

        /// <summary>Damage contributors from RecentAttackerElement buffer.</summary>
        public readonly List<DamageContributor> DamageContributors = new();

        /// <summary>Total respawn delay from DeathState.</summary>
        public float RespawnDelay;

        /// <summary>Elapsed time when death occurred.</summary>
        public float DeathTime;

        /// <summary>The active death camera configuration.</summary>
        public DeathCameraConfigSO Config;

        /// <summary>The Unity camera to drive.</summary>
        public Camera TargetCamera;

        /// <summary>The gameplay camera mode that was active before death (for respawn restore).</summary>
        public ICameraMode PreviousCamera;

        /// <summary>The gameplay camera mode enum (resolved from PreviousCamera). Falls back to ThirdPersonFollow.</summary>
        public CameraMode GameplayMode;

        /// <summary>The gameplay CameraConfig (if PreviousCamera was CameraModeBase). May be null.</summary>
        public CameraConfig GameplayCameraConfig;

        /// <summary>Zoom level of the gameplay camera at the time of death (0-1). -1 = not captured.</summary>
        public float GameplayZoomLevel = -1f;

        /// <summary>Camera offset from player position at death (Camera.main.position - playerPos).</summary>
        public float3 CapturedCameraOffset;

        /// <summary>Camera rotation at death time.</summary>
        public quaternion CapturedCameraRotation = quaternion.identity;

        /// <summary>Camera FOV at death time.</summary>
        public float CapturedFOV;

        /// <summary>Whether camera state was successfully captured at death.</summary>
        public bool HasCapturedCameraState;

        /// <summary>Ghost IDs of alive players (refreshed by spectator phase).</summary>
        public readonly List<ushort> AlivePlayerGhostIds = new();

        /// <summary>
        /// Seconds remaining until respawn. Updated by orchestrator each frame.
        /// </summary>
        public float RespawnTimeRemaining;
    }

    /// <summary>
    /// EPIC 18.13: A single entry in the damage breakdown.
    /// </summary>
    public struct DamageContributor
    {
        public string Name;
        public float DamageDealt;
        public float TimeAgo;
    }
}
