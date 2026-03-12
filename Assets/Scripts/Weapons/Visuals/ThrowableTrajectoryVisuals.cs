using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Weapons;
using DIG.Items;
using DIG.Shared;
using Unity.Transforms;

namespace DIG.Weapons.Visuals
{
    /// <summary>
    /// Bridges ECS Throwable State to the standalone ProjectileTrajectory visualizer.
    /// Shows trajectory arc while charging a throwable weapon.
    /// </summary>
    public class ThrowableTrajectoryVisuals : MonoBehaviour
    {
        [Tooltip("Reference to the standalone Projectile Trajectory script")]
        public ProjectileTrajectory projectileTrajectory;

        private EntityManager _entityManager;
        private Entity _localPlayerEntity;
        private Entity _currentThrowableWeapon;
        private float _searchCooldown;
        private int _lastActiveSlot = -1;

        private void Start()
        {
            // Find the CLIENT world specifically (not server)
            foreach (var world in World.All)
            {
                if (world.IsCreated && world.Name.Contains("Client"))
                {
                    _entityManager = world.EntityManager;
                    break;
                }
            }

            // Fallback to first non-Editor world if no Client world found
            if (_entityManager == default)
            {
                foreach (var world in World.All)
                {
                    if (world.IsCreated && !world.Name.Contains("Editor") && !world.Name.Contains("Server"))
                    {
                        _entityManager = world.EntityManager;
                        break;
                    }
                }
            }

            if (_entityManager == default)
            {
                Debug.LogError("[ThrowableTrajectoryVisuals] No valid ECS world found!");
            }

            if (projectileTrajectory == null)
            {
                Debug.LogError("[ThrowableTrajectoryVisuals] ProjectileTrajectory reference is NULL! Assign it in inspector.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (_entityManager == default || !_entityManager.World.IsCreated)
            {
                HideTrajectory();
                return;
            }

            // Periodically search for local player if not found
            if (_localPlayerEntity == Entity.Null || !_entityManager.Exists(_localPlayerEntity))
            {
                _searchCooldown -= Time.deltaTime;
                if (_searchCooldown <= 0f)
                {
                    FindLocalPlayer();
                    _searchCooldown = 0.5f;
                }
                HideTrajectory();
                return;
            }

            FindEquippedThrowable();
            UpdateTrajectory();
        }

        private void FindLocalPlayer()
        {
            if (_entityManager == default) return;

            var entities = _entityManager.GetAllEntities(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                bool hasGhostOwnerIsLocal = _entityManager.HasComponent<GhostOwnerIsLocal>(entity);
                if (hasGhostOwnerIsLocal)
                {
                    bool isEnabled = _entityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity);
                    bool hasEquipBuffer = _entityManager.HasBuffer<EquippedItemElement>(entity);

                    if (isEnabled && hasEquipBuffer)
                    {
                        _localPlayerEntity = entity;
                        entities.Dispose();
                        return;
                    }
                }
            }
            entities.Dispose();
        }

        private void FindEquippedThrowable()
        {
            _currentThrowableWeapon = Entity.Null;
            int myNetworkId = -1;
            int activeSlot = -1;

            // Get Player Data (ActiveSlot, NetworkId, Buffer)
            if (_entityManager.HasComponent<ActiveSlotIndex>(_localPlayerEntity))
            {
                activeSlot = _entityManager.GetComponentData<ActiveSlotIndex>(_localPlayerEntity).Value;
                _lastActiveSlot = activeSlot;
            }
            if (_entityManager.HasComponent<GhostOwner>(_localPlayerEntity))
            {
                myNetworkId = _entityManager.GetComponentData<GhostOwner>(_localPlayerEntity).NetworkId;
            }

            // Priority: Check Active Slot in EquippedBuffer
            if (_entityManager.HasBuffer<EquippedItemElement>(_localPlayerEntity))
            {
                var buffer = _entityManager.GetBuffer<EquippedItemElement>(_localPlayerEntity);
                for (int i = 0; i < buffer.Length; i++)
                {
                    var element = buffer[i];
                    if (element.QuickSlot == activeSlot && _entityManager.HasComponent<ThrowableState>(element.ItemEntity))
                    {
                        _currentThrowableWeapon = element.ItemEntity;
                        return;
                    }
                }
            }

            // Fallback: Search all entities for ANY owned throwable
            var entities = _entityManager.GetAllEntities(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                if (!_entityManager.HasComponent<ThrowableState>(entity) ||
                    !_entityManager.HasComponent<ThrowableAction>(entity))
                    continue;

                bool isOwned = false;

                // Ownership Check
                if (myNetworkId >= 0 && _entityManager.HasComponent<GhostOwner>(entity))
                {
                    if (_entityManager.GetComponentData<GhostOwner>(entity).NetworkId == myNetworkId) isOwned = true;
                }
                if (!isOwned && _entityManager.HasComponent<CharacterItem>(entity) &&
                    _entityManager.GetComponentData<CharacterItem>(entity).OwnerEntity == _localPlayerEntity)
                {
                    isOwned = true;
                }
                if (!isOwned && _entityManager.HasComponent<GhostOwnerIsLocal>(entity) &&
                    _entityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity))
                {
                    isOwned = true;
                }

                if (isOwned)
                {
                    // Priority: IsCharging > First Found
                    bool isCharging = _entityManager.GetComponentData<ThrowableState>(entity).IsCharging;

                    if (isCharging)
                    {
                        _currentThrowableWeapon = entity;
                        entities.Dispose();
                        return;
                    }

                    if (_currentThrowableWeapon == Entity.Null)
                    {
                        _currentThrowableWeapon = entity;
                    }
                }
            }

            entities.Dispose();
        }

        private void UpdateTrajectory()
        {
            if (_currentThrowableWeapon == Entity.Null)
            {
                HideTrajectory();
                return;
            }

            var throwableState = _entityManager.GetComponentData<ThrowableState>(_currentThrowableWeapon);
            var throwableAction = _entityManager.GetComponentData<ThrowableAction>(_currentThrowableWeapon);

            if (throwableState.IsCharging)
            {
                ShowTrajectory();

                float force = math.lerp(throwableAction.MinForce, throwableAction.MaxForce, throwableState.ChargeProgress);
                float3 throwDir = math.normalizesafe(throwableState.AimDirection, math.forward());
                float3 velocity = throwDir * force;

                // Determine trajectory start position - prefer actual hand position
                Vector3 startPos = Vector3.zero;
                bool useSocketPosition = false;

                // Try to get actual hand position from SocketPositionData
                if (_localPlayerEntity != Entity.Null && _entityManager.Exists(_localPlayerEntity) &&
                    _entityManager.HasComponent<SocketPositionData>(_localPlayerEntity))
                {
                    var socketData = _entityManager.GetComponentData<SocketPositionData>(_localPlayerEntity);
                    if (socketData.IsValid)
                    {
                        startPos = (Vector3)socketData.MainHandPosition + (Vector3)throwDir * 0.3f;
                        useSocketPosition = true;
                    }
                }

                // Fallback: Use camera position or player position + offset
                if (!useSocketPosition)
                {
                    if (Camera.main != null)
                    {
                        startPos = Camera.main.transform.position + (Camera.main.transform.forward * 0.8f);
                    }
                    else
                    {
                        float3 originPos = transform.position;
                        if (_localPlayerEntity != Entity.Null && _entityManager.Exists(_localPlayerEntity) &&
                            _entityManager.HasComponent<LocalTransform>(_localPlayerEntity))
                        {
                            originPos = _entityManager.GetComponentData<LocalTransform>(_localPlayerEntity).Position;
                        }
                        startPos = (Vector3)originPos + Vector3.up * 1.5f + (Vector3)throwDir * 0.8f;
                    }
                }

                projectileTrajectory.SimulateTrajectory(startPos, velocity, 9.81f, 0.1f);
            }
            else
            {
                HideTrajectory();
            }
        }

        private void ShowTrajectory()
        {
            if (projectileTrajectory != null && !projectileTrajectory.gameObject.activeSelf)
            {
                projectileTrajectory.gameObject.SetActive(true);
            }
        }

        private void HideTrajectory()
        {
            if (projectileTrajectory != null && projectileTrajectory.gameObject.activeSelf)
            {
                projectileTrajectory.gameObject.SetActive(false);
            }
        }
    }
}
