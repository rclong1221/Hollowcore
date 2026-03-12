using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Items;
using DIG.Targeting;
using DIG.Combat.Systems;
using DIG.Combat.Resolvers;

using Player.Components;
using DIG.Combat.Utility;
using DIG.Combat.Components;
using DIG.Surface;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Core weapon firing system.
    /// Handles fire rate, projectile spawning/hitscan, and triggers recoil/spread/ammo events via state updates.
    /// Runs on both client and server - client predicts firing locally for responsive animations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponAmmoSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct WeaponFireSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Complete any pending jobs that write to components we need (Health, DamageEvent, etc.)
            // Fixes race condition with KillZoneSystem's async KillZoneJob which writes to Health.
            state.CompleteDependency();

            float deltaTime = SystemAPI.Time.DeltaTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            double elapsedTime = SystemAPI.Time.ElapsedTime;
            var isServer = state.WorldUnmanaged.IsServer();

            // Get TargetData lookup for reading owner's targeting
            var targetDataLookup = SystemAPI.GetComponentLookup<TargetData>(true);
            // Owner transform lookup — weapons live at y=-1000, need player position for raycast origin
            var ownerTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            // Camera view config for crosshair convergence (camera offset correction)
            var viewConfigLookup = SystemAPI.GetComponentLookup<CameraViewConfig>(true);

            // EPIC 15.28: Lookups for hitscan damage application
            var hitboxLookup = SystemAPI.GetComponentLookup<Hitbox>(true);
            var hasHitboxesLookup = SystemAPI.GetComponentLookup<HasHitboxes>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
            var damageBufferLookup = SystemAPI.GetBufferLookup<DamageEvent>(false);
            var healthLookup = SystemAPI.GetComponentLookup<Health>(false); // writable — direct Health reduction fallback
            var damageableLinkLookup = SystemAPI.GetComponentLookup<DamageableLink>(true);
            var hitboxOwnerLinkLookup = SystemAPI.GetComponentLookup<HitboxOwnerLink>(true);
            var ammoLookup = SystemAPI.GetComponentLookup<WeaponAmmoState>(false);
            var damageProfileLookup = SystemAPI.GetComponentLookup<DamageProfile>(true);
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.IsValid ? networkTime.ServerTick.TickIndexForValidTick : 0;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Projectile registry for non-hitscan weapon spawning
            Entity registryEntity = Entity.Null;
            foreach (var (_, regEnt) in SystemAPI.Query<RefRO<ProjectileRegistrySingleton>>().WithEntityAccess())
            {
                registryEntity = regEnt;
                break;
            }
            var registryLookup = SystemAPI.GetBufferLookup<ProjectilePrefabElement>(true);
            var modifierLookup = SystemAPI.GetBufferLookup<WeaponModifier>(true);

            // Query weapons with Simulate tag - this is the NetCode standard pattern
            // Simulate is enabled for: local predicted entities, and server simulation
            // Server processes ALL predicted weapons, Client only processes locally-owned weapons
            foreach (var (fireConfig, fireState, action, request, transform, charItem, entity) in
                     SystemAPI.Query<RefRO<WeaponFireComponent>, RefRW<WeaponFireState>, RefRW<UsableAction>, RefRO<UseRequest>, RefRO<LocalTransform>, RefRO<CharacterItem>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                Entity owner = charItem.ValueRO.OwnerEntity;
                ref var stateRef = ref fireState.ValueRW;
                ref var actionRef = ref action.ValueRW;
                var config = fireConfig.ValueRO;

                // EPIC 15.29: Read base damage element from weapon's DamageProfile
                var themeElement = damageProfileLookup.HasComponent(entity)
                    ? damageProfileLookup[entity].Element
                    : DIG.Targeting.Theming.DamageType.Physical;
                var survivalElement = DamageTypeConverter.ToSurvival(themeElement);

                // Update time since last shot
                stateRef.TimeSinceLastShot += deltaTime;

                // Check Ammo (Optional dependency — read once via cached lookup)
                bool hasAmmoComp = ammoLookup.HasComponent(entity);
                var ammoState = hasAmmoComp ? ammoLookup[entity] : default;
                bool hasAmmo = !hasAmmoComp || (ammoState.AmmoCount > 0 && !ammoState.IsReloading);

                // Check Cooldown / Fire Rate
                float fireInterval = 1f / config.FireRate;
                bool canFire = stateRef.TimeSinceLastShot >= fireInterval && hasAmmo;

                // Determine desire to fire
                bool wantsFire = request.ValueRO.StartUse;
                if (!config.IsAutomatic)
                {
                    // Semi-auto: only fire if not currently firing (trigger reset)
                    wantsFire = request.ValueRO.StartUse && !stateRef.IsFiring;
                }

                if (wantsFire && canFire)
                {
                    // FIRE!
                    stateRef.IsFiring = true;
                    stateRef.TimeSinceLastShot = 0f;
                    stateRef.FireAnimationTimer = 0.15f;

                    // Decrement Ammo (client AND server for prediction)
                    // Server state is authoritative and will correct any misprediction via GhostField
                    if (hasAmmoComp)
                    {
                        ammoState.AmmoCount--;
                        ammoLookup[entity] = ammoState;
                    }

                    // Calculate fire direction from TargetData if available
                    float3 aimDir = float3.zero;

                    // Priority 1: Read from owner's TargetData component
                    Entity ownerEntity = charItem.ValueRO.OwnerEntity;
                    if (ownerEntity != Entity.Null && targetDataLookup.HasComponent(ownerEntity))
                    {
                        var targetData = targetDataLookup[ownerEntity];
                        if (math.lengthsq(targetData.AimDirection) > 0.01f)
                        {
                            aimDir = targetData.AimDirection;
                        }
                    }

                    // Priority 2: Fallback to UseRequest.AimDirection
                    if (math.lengthsq(aimDir) < 0.01f)
                    {
                        aimDir = request.ValueRO.AimDirection;
                    }

                    // Priority 3: Fallback to weapon forward
                    if (math.lengthsq(aimDir) < 0.01f)
                    {
                        aimDir = math.forward(transform.ValueRO.Rotation);
                    }
                    float3 fireDirection = math.normalize(aimDir);

                    // Crosshair convergence: camera may be offset from the hitscan ray origin
                    // (CameraSide, OTS shoulder, pivot height). Project a far point from the
                    // approximate camera position and recompute fire direction from player eye
                    // so the bullet hits where the crosshair is pointing.
                    if (ownerEntity != Entity.Null && viewConfigLookup.HasComponent(ownerEntity)
                        && ownerTransformLookup.HasComponent(ownerEntity))
                    {
                        var viewConfig = viewConfigLookup[ownerEntity];
                        float3 playerPos = ownerTransformLookup[ownerEntity].Position;
                        float3 pivotPos = playerPos + viewConfig.CombatPivotOffset;

                        // Approximate camera position: pivot + CombatCameraOffset in view space
                        quaternion camRot = quaternion.LookRotationSafe(fireDirection, math.up());
                        float3 worldCamOffset = math.mul(camRot, viewConfig.CombatCameraOffset);
                        float3 approxCamPos = pivotPos + worldCamOffset;

                        // Converge: project far point from camera, recompute direction from bullet origin
                        float3 bulletOrigin = playerPos + math.up() * 1.5f;
                        float3 convergencePoint = approxCamPos + fireDirection * 200f;
                        fireDirection = math.normalizesafe(convergencePoint - bulletOrigin, fireDirection);
                    }

                    if (SystemAPI.HasComponent<WeaponSpreadState>(entity))
                    {
                        var spreadState = SystemAPI.GetComponent<WeaponSpreadState>(entity);
                        float spreadRad = math.radians(spreadState.CurrentSpread);

                        var random = Unity.Mathematics.Random.CreateFromIndex((uint)(entity.Index + elapsedTime * 1000));
                        float2 spreadOffset = random.NextFloat2(-spreadRad, spreadRad);

                        float3 right = math.cross(math.up(), fireDirection);
                        float3 up = math.cross(fireDirection, right);
                        fireDirection = math.normalize(fireDirection + right * spreadOffset.x + up * spreadOffset.y);
                    }

                    // Hitscan (server-only for authoritative hit detection)
                    if (config.UseHitscan && isServer)
                    {
                        // Use owner (player) position — weapon entities live at storage position (y=-1000)
                        float3 rayOrigin;
                        if (ownerEntity != Entity.Null && ownerTransformLookup.HasComponent(ownerEntity))
                            rayOrigin = ownerTransformLookup[ownerEntity].Position + math.up() * 1.5f;
                        else
                            rayOrigin = transform.ValueRO.Position + math.up() * 1.5f;

                        var rayInput = new RaycastInput
                        {
                            Start = rayOrigin,
                            End = rayOrigin + fireDirection * config.Range,
                            Filter = CollisionFilter.Default
                        };

                        // Use CastRay with all hits to skip self-hits (owner's collider)
                        var allHits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
                        bool rayHitAnything = physicsWorld.CastRay(rayInput, ref allHits) && allHits.Length > 0;

                        // Find closest hit that isn't the owner or the owner's hitbox
                        Unity.Physics.RaycastHit hit = default;
                        bool foundValidHit = false;

                        if (rayHitAnything)
                        {
                            float closestDist = float.MaxValue;
                            for (int h = 0; h < allHits.Length; h++)
                            {
                                var candidate = allHits[h];
                                if (candidate.Entity == Entity.Null) continue;

                                // Skip self: direct hit on owner
                                if (candidate.Entity == ownerEntity) continue;

                                // Skip self: hit on owner's hitbox child
                                if (hitboxLookup.HasComponent(candidate.Entity) &&
                                    hitboxLookup[candidate.Entity].OwnerEntity == ownerEntity) continue;

                                float dist = candidate.Fraction;
                                if (dist < closestDist)
                                {
                                    closestDist = dist;
                                    hit = candidate;
                                    foundValidHit = true;
                                }
                            }
                        }

                        // AIM-ASSIST: If primary ray hit only environment (no damageable),
                        // cast a secondary ray at the nearest damageable target within a cone.
                        if (foundValidHit)
                        {
                            Entity primaryTarget = hit.Entity;
                            if (hitboxLookup.HasComponent(hit.Entity))
                                primaryTarget = hitboxLookup[hit.Entity].OwnerEntity;
                            bool primaryIsDamageable = healthLookup.HasComponent(primaryTarget) ||
                                damageBufferLookup.HasBuffer(primaryTarget) ||
                                hasHitboxesLookup.HasComponent(primaryTarget);

                            if (!primaryIsDamageable)
                            {
                                FindAimAssistTarget(ref state, rayInput.Start, fireDirection, ref ownerTransformLookup,
                                    out Entity aaTarget, out float3 aaCenter);
                                TryAimAssistRaycast(ref hit, ref foundValidHit, rayInput.Start,
                                    config.Range, ownerEntity, aaTarget, aaCenter,
                                    ref physicsWorld, ref hitboxLookup, ref healthLookup,
                                    ref damageBufferLookup, ref hasHitboxesLookup, ref damageableLinkLookup);
                            }
                        }
                        else
                        {
                            FindAimAssistTarget(ref state, rayInput.Start, fireDirection, ref ownerTransformLookup,
                                out Entity aaTarget, out float3 aaCenter);
                            TryAimAssistRaycast(ref hit, ref foundValidHit, rayInput.Start,
                                config.Range, ownerEntity, aaTarget, aaCenter,
                                ref physicsWorld, ref hitboxLookup, ref healthLookup,
                                ref damageBufferLookup, ref hasHitboxesLookup, ref damageableLinkLookup);
                        }

                        if (foundValidHit)
                        {
                            // EPIC 15.28: Server-authoritative hitscan hit logic
                            Entity targetEntity = hit.Entity;
                            float multiplier = 1.0f;
                            HitboxRegion hitRegion = HitboxRegion.Torso;

                            // Resolve hitbox → owner entity
                            if (hit.Entity != Entity.Null && hitboxLookup.HasComponent(hit.Entity))
                            {
                                var hitbox = hitboxLookup[hit.Entity];
                                targetEntity = hitbox.OwnerEntity;
                                multiplier = hitbox.DamageMultiplier;
                                hitRegion = hitbox.Region;
                            }

                            // Redirect compound collider hits from ROOT → CHILD (HitboxOwnerLink).
                            if (hitboxOwnerLinkLookup.HasComponent(targetEntity))
                                targetEntity = hitboxOwnerLinkLookup[targetEntity].HitboxOwner;

                            // Resolve hit entity → damageable root entity.
                            if (!damageBufferLookup.HasBuffer(targetEntity))
                            {
                                // Strategy 1: DamageableLink (fastest — direct baked reference to root)
                                if (damageableLinkLookup.HasComponent(targetEntity))
                                {
                                    Entity root = damageableLinkLookup[targetEntity].DamageableRoot;
                                    if (root != Entity.Null)
                                        targetEntity = root;
                                }

                                // Strategy 2: Parent chain walk (fallback for entities without DamageableLink)
                                if (!damageBufferLookup.HasBuffer(targetEntity) && !healthLookup.HasComponent(targetEntity))
                                {
                                    Entity cur = targetEntity;
                                    for (int d = 0; d < 5 && parentLookup.HasComponent(cur); d++)
                                    {
                                        cur = parentLookup[cur].Value;
                                        if (damageBufferLookup.HasBuffer(cur) || healthLookup.HasComponent(cur) || hasHitboxesLookup.HasComponent(cur))
                                        {
                                            targetEntity = cur;
                                            break;
                                        }
                                    }
                                }

                                // Strategy 3: Accept entity if it has Health or HasHitboxes
                                if (!damageBufferLookup.HasBuffer(targetEntity))
                                {
                                    bool isDamageable = healthLookup.HasComponent(targetEntity) || hasHitboxesLookup.HasComponent(targetEntity);
                                    if (!isDamageable)
                                        targetEntity = Entity.Null;
                                }

                                // Strategy 4: Hitbox-child fallback
                                if (!damageBufferLookup.HasBuffer(targetEntity) && !healthLookup.HasComponent(targetEntity))
                                {
                                    Entity betterTarget = Entity.Null;
                                    float bestDist = float.MaxValue;
                                    float bestMult = 1f;
                                    HitboxRegion bestRegion = HitboxRegion.Torso;

                                    for (int hh = 0; hh < allHits.Length; hh++)
                                    {
                                        var c = allHits[hh];
                                        if (c.Entity == Entity.Null || c.Entity == ownerEntity) continue;
                                        if (!hitboxLookup.HasComponent(c.Entity)) continue;

                                        var hb = hitboxLookup[c.Entity];
                                        if (hb.OwnerEntity == ownerEntity || hb.OwnerEntity == Entity.Null) continue;

                                        bool ownerHasHealth = healthLookup.HasComponent(hb.OwnerEntity);
                                        bool ownerHasBuf = damageBufferLookup.HasBuffer(hb.OwnerEntity);

                                        if ((ownerHasHealth || ownerHasBuf) && c.Fraction < bestDist)
                                        {
                                            betterTarget = hb.OwnerEntity;
                                            bestDist = c.Fraction;
                                            bestMult = hb.DamageMultiplier;
                                            bestRegion = hb.Region;
                                        }
                                    }

                                    if (betterTarget != Entity.Null)
                                    {
                                        targetEntity = betterTarget;
                                        multiplier = bestMult;
                                        hitRegion = bestRegion;
                                    }
                                }
                            }

                            float finalDamage = config.Damage * multiplier;

                            // Create DamageEvent for health
                            bool damageApplied = false;
                            if (finalDamage > 0 && targetEntity != Entity.Null && damageBufferLookup.HasBuffer(targetEntity))
                            {
                                ecb.AppendToBuffer(targetEntity, new DamageEvent
                                {
                                    Amount = finalDamage,
                                    SourceEntity = owner,
                                    HitPosition = hit.Position,
                                    ServerTick = currentTick,
                                    Type = survivalElement
                                });
                                damageApplied = true;
                            }
                            // FALLBACK: No DamageEvent buffer → directly reduce Health via writable lookup.
                            else if (finalDamage > 0 && targetEntity != Entity.Null && healthLookup.HasComponent(targetEntity))
                            {
                                var hp = healthLookup[targetEntity];
                                hp.Current = math.max(0f, hp.Current - finalDamage);
                                healthLookup[targetEntity] = hp;
                                damageApplied = true;
                            }

                            // Create PendingCombatHit for combat resolution
                            if (targetEntity != Entity.Null)
                            {
                                var combatHitEntity = ecb.CreateEntity();
                                ecb.AddComponent(combatHitEntity, new PendingCombatHit
                                {
                                    AttackerEntity = owner,
                                    TargetEntity = targetEntity,
                                    WeaponEntity = entity,
                                    HitPoint = hit.Position,
                                    HitNormal = hit.SurfaceNormal,
                                    HitDistance = math.distance(rayInput.Start, hit.Position),
                                    WasPhysicsHit = true,
                                    ResolverType = CombatResolverType.Hybrid,
                                    HitRegion = hitRegion,
                                    HitboxMultiplier = multiplier,
                                    DamagePreApplied = damageApplied,
                                    AttackDirection = fireDirection,
                                    WeaponData = new WeaponStats
                                    {
                                        BaseDamage = config.Damage,
                                        DamageType = themeElement,
                                        CanCrit = true
                                    }
                                });
                            }

                            // EPIC 15.24: Create environment hit request for surface impact VFX.
                            // HitscanImpactBridgeSystem picks these up and enqueues to SurfaceImpactQueue.
                            // Created for ALL hits (enemy and environment) — the bridge handles both.
                            {
                                var impactEntity = ecb.CreateEntity();
                                ecb.AddComponent(impactEntity, new EnvironmentHitRequest
                                {
                                    Position = hit.Position,
                                    Normal = hit.SurfaceNormal,
                                    Velocity = fireDirection * config.Range,
                                    ServerTick = currentTick
                                });
                            }
                        }
                        allHits.Dispose();
                    }
                    // Projectile spawn (server-only, non-hitscan weapons)
                    else if (!config.UseHitscan && isServer)
                    {
                        if (registryEntity != Entity.Null && registryLookup.HasBuffer(registryEntity))
                        {
                            var registry = registryLookup[registryEntity];
                            Entity prefabEntity = Entity.Null;
                            for (int r = 0; r < registry.Length; r++)
                            {
                                if (registry[r].PrefabIndex == config.ProjectilePrefabIndex)
                                {
                                    prefabEntity = registry[r].PrefabEntity;
                                    break;
                                }
                            }

                            if (prefabEntity != Entity.Null)
                            {
                                // Spawn position: owner eye height (weapons live at y=-1000 storage)
                                float3 spawnPos;
                                if (ownerEntity != Entity.Null && ownerTransformLookup.HasComponent(ownerEntity))
                                    spawnPos = ownerTransformLookup[ownerEntity].Position + math.up() * 1.5f;
                                else
                                    spawnPos = transform.ValueRO.Position + math.up() * 1.5f;

                                Entity projectile = ecb.Instantiate(prefabEntity);
                                ecb.SetComponent(projectile, LocalTransform.FromPositionRotation(
                                    spawnPos,
                                    quaternion.LookRotationSafe(fireDirection, math.up())
                                ));

                                // Compositional: read prefab-baked Projectile, set runtime Owner and weapon Damage
                                if (SystemAPI.HasComponent<Projectile>(prefabEntity))
                                {
                                    var prefabProjectile = SystemAPI.GetComponent<Projectile>(prefabEntity);
                                    prefabProjectile.Owner = ownerEntity;
                                    prefabProjectile.Damage = config.Damage;
                                    prefabProjectile.ElapsedTime = 0f;
                                    ecb.SetComponent(projectile, prefabProjectile);
                                }

                                // Compositional: read prefab-baked ProjectileMovement, set Velocity direction
                                if (SystemAPI.HasComponent<ProjectileMovement>(prefabEntity))
                                {
                                    var prefabMovement = SystemAPI.GetComponent<ProjectileMovement>(prefabEntity);
                                    float speed = math.length(prefabMovement.Velocity);
                                    if (speed < 0.1f) speed = math.max(config.Range * 2f, 50f);
                                    prefabMovement.Velocity = fireDirection * speed;
                                    ecb.SetComponent(projectile, prefabMovement);
                                }

                                // Compositional: reset ProjectileImpact bounce count
                                if (SystemAPI.HasComponent<ProjectileImpact>(prefabEntity))
                                {
                                    var prefabImpact = SystemAPI.GetComponent<ProjectileImpact>(prefabEntity);
                                    prefabImpact.CurrentBounces = 0;
                                    ecb.SetComponent(projectile, prefabImpact);
                                }

                                ecb.AddComponent<Simulate>(projectile);

                                // Stamp weapon's DamageProfile onto projectile
                                if (damageProfileLookup.HasComponent(entity))
                                {
                                    ecb.AddComponent(projectile, damageProfileLookup[entity]);
                                }

                                // Stamp weapon modifiers onto projectile
                                if (modifierLookup.HasBuffer(entity))
                                {
                                    var srcMods = modifierLookup[entity];
                                    var dstBuffer = ecb.AddBuffer<WeaponModifier>(projectile);
                                    for (int i = 0; i < srcMods.Length; i++)
                                        dstBuffer.Add(srcMods[i]);
                                }

                                // Set GhostOwner for OwnerPredicted ghosts
                                if (ownerEntity != Entity.Null && SystemAPI.HasComponent<GhostOwner>(ownerEntity))
                                {
                                    var ownerGhost = SystemAPI.GetComponent<GhostOwner>(ownerEntity);
                                    ecb.AddComponent(projectile, new GhostOwner { NetworkId = ownerGhost.NetworkId });
                                }
                            }
                        }
                    }

                    // Set Global Cooldown
                    actionRef.CooldownRemaining = fireInterval;
                }
                else
                {
                    // Decrement animation timer
                    if (stateRef.FireAnimationTimer > 0)
                    {
                        stateRef.FireAnimationTimer -= deltaTime;
                    }

                    // Only reset firing state when input released AND animation timer expired
                    if (!request.ValueRO.StartUse && stateRef.FireAnimationTimer <= 0)
                    {
                        stateRef.IsFiring = false;
                    }
                }
            }

            // EPIC 15.28: Playback ECB for hitscan damage/combat events
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Aim-assist target search: finds the nearest damageable entity within a cone.
        /// Must be an instance method because SystemAPI.Query requires instance context.
        /// </summary>
        private void FindAimAssistTarget(
            ref SystemState state,
            float3 rayStart,
            float3 fireDirection,
            ref ComponentLookup<LocalTransform> ownerTransformLookup,
            out Entity bestTarget,
            out float3 bestTargetCenter)
        {
            const float aimAssistConeRad = 0.12f; // ~7° half-angle cone
            const float aimAssistMaxDist = 50f;
            bestTarget = Entity.Null;
            bestTargetCenter = float3.zero;
            float bestDot = math.cos(aimAssistConeRad);

            foreach (var (aaH, aaHB, aaEnt) in SystemAPI.Query<RefRO<Health>, RefRO<HasHitboxes>>().WithEntityAccess())
            {
                if (!ownerTransformLookup.HasComponent(aaEnt)) continue;
                float3 tgtPos = ownerTransformLookup[aaEnt].Position + math.up();
                float3 toTgt = tgtPos - rayStart;
                float distToTgt = math.length(toTgt);
                if (distToTgt < 0.5f || distToTgt > aimAssistMaxDist) continue;
                float dotVal = math.dot(fireDirection, toTgt / distToTgt);
                if (dotVal > bestDot)
                {
                    bestDot = dotVal;
                    bestTarget = aaEnt;
                    bestTargetCenter = tgtPos;
                }
            }
        }

        /// <summary>
        /// Aim-assist raycast: casts a secondary ray at the pre-found target.
        /// Static — no SystemAPI calls, only lookups and physics.
        /// </summary>
        private static void TryAimAssistRaycast(
            ref Unity.Physics.RaycastHit hit,
            ref bool foundValidHit,
            float3 rayStart,
            float range,
            Entity ownerEntity,
            Entity aimAssistTarget,
            float3 aimAssistCenter,
            ref PhysicsWorldSingleton physicsWorld,
            ref ComponentLookup<Hitbox> hitboxLookup,
            ref ComponentLookup<Health> healthLookup,
            ref BufferLookup<DamageEvent> damageBufferLookup,
            ref ComponentLookup<HasHitboxes> hasHitboxesLookup,
            ref ComponentLookup<DamageableLink> damageableLinkLookup)
        {
            if (aimAssistTarget == Entity.Null)
                return;

            // Cast secondary ray at the aim-assist target
            float3 toAssist = math.normalize(aimAssistCenter - rayStart);
            var assistRay = new RaycastInput
            {
                Start = rayStart,
                End = rayStart + toAssist * range,
                Filter = CollisionFilter.Default
            };
            var assistHits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
            physicsWorld.CastRay(assistRay, ref assistHits);

            float assistClosest = float.MaxValue;
            for (int aa = 0; aa < assistHits.Length; aa++)
            {
                var c = assistHits[aa];
                if (c.Entity == Entity.Null || c.Entity == ownerEntity) continue;
                if (hitboxLookup.HasComponent(c.Entity) &&
                    hitboxLookup[c.Entity].OwnerEntity == ownerEntity) continue;

                Entity resolved = c.Entity;
                if (hitboxLookup.HasComponent(c.Entity))
                    resolved = hitboxLookup[c.Entity].OwnerEntity;
                if (damageableLinkLookup.HasComponent(resolved))
                {
                    Entity root = damageableLinkLookup[resolved].DamageableRoot;
                    if (root != Entity.Null) resolved = root;
                }

                bool isDamageable = healthLookup.HasComponent(resolved) ||
                    damageBufferLookup.HasBuffer(resolved) ||
                    hasHitboxesLookup.HasComponent(resolved);
                if (isDamageable && c.Fraction < assistClosest)
                {
                    assistClosest = c.Fraction;
                    hit = c;
                    foundValidHit = true;
                }
            }

            assistHits.Dispose();
        }
    }
}
