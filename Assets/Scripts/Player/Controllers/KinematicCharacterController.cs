
using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Player.Systems;

namespace Player.Controllers
{
    public enum KinematicPreset { Default, Tight, Loose }

    [AddComponentMenu("Player/Controllers/Kinematic Character Controller")]
    public class KinematicCharacterController : MonoBehaviour
    {
        [Header("Presets")]
        public KinematicPreset preset = KinematicPreset.Default;

        [Header("Capsule")]
        public float radius = 0.4f;
        public float height = 2f;
        public float skinWidth = 0.02f;
        public float stepHeight = 0.3f;
        public LayerMask collisionMask = ~0;
        
        /// <summary>
        /// Effective collision mask that always excludes "Ignore Raycast" layer (layer 2).
        /// This prevents the controller from colliding with environment zone triggers.
        /// </summary>
        private int EffectiveCollisionMask => collisionMask & ~(1 << 2);

        [Header("Movement")]
        public float walkSpeed = 2f;
        public float runSpeed = 7f;
        public float acceleration = 20f;
        public float gravity = -9.81f;
        public float jumpForce = 5f;
        public bool pushRigidbodies = true;
        public float pushForce = 1.5f;
        // Safety: clamp per-frame displacement to avoid tunneling/teleports
        [Tooltip("Maximum allowed displacement (m) per second. Actual per-frame clamp = value * Time.deltaTime.")]
        public float maxDisplacementPerSecond = 10f;
        [Tooltip("Cap applied impulse magnitude when pushing rigidbodies to avoid explosive forces.")]
        public float maxPushImpulse = 5f;
        public float lookSensitivity = 2f;

        [Header("Kinematic Collider")]
        [Tooltip("If true, ensure a kinematic Rigidbody + CapsuleCollider is present and use them for robust penetration resolution.")]
        public bool useKinematicRigidbody = true;

        // Runtime references
        CapsuleCollider capsule = null;
        Rigidbody rb = null;

        // Push smoothing: accumulate impulses per-rigidbody and apply over multiple FixedUpdate steps
        struct PushData { public Vector3 accumulated; public Vector3 hitPoint; }
        Dictionary<Rigidbody, PushData> pendingPushes = new Dictionary<Rigidbody, PushData>();
        [Tooltip("Fraction of the accumulated impulse applied each FixedUpdate (0-1).")]
        public float pushApplyFraction = 0.5f;
        [Tooltip("Maximum total accumulated impulse allowed per rigidbody.")]
        public float maxAccumulatedPush = 10f;
        [Tooltip("How much accumulated impulse (magnitude) decays per second when idle.")]
        public float pushAccumulatedDecay = 5f;

        Vector3 velocity = Vector3.zero;
        float verticalVelocity = 0f;
        bool isCrouched = false;
        bool isStepping = false;
        Coroutine stepRoutine = null;

        [Header("Step Smoothing")]
        public float stepSmoothTime = 0.12f;

        void Reset()
        {
            ApplyPreset(preset);
        }

        void OnValidate()
        {
            ApplyPreset(preset);
            height = Mathf.Max(height, radius * 2f + 0.01f);
            stepHeight = Mathf.Clamp(stepHeight, 0f, height * 0.5f);
            UpdateColliderFromSettings();
        }

        void Awake()
        {
            // Ensure capsule collider exists
            capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = 1; // Y axis
            }

            // Ensure rigidbody exists when using kinematic path
            if (useKinematicRigidbody)
            {
                rb = GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                }
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            UpdateColliderFromSettings();
        }

        void UpdateColliderFromSettings()
        {
            if (capsule == null) return;
            capsule.radius = Mathf.Max(0.01f, radius - skinWidth);
            capsule.height = Mathf.Max(capsule.radius * 2f + 0.01f, height - skinWidth * 2f);
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
        }

        void ApplyPreset(KinematicPreset p)
        {
            switch (p)
            {
                case KinematicPreset.Tight:
                    walkSpeed = 1.6f; runSpeed = 5f; acceleration = 30f; gravity = -12f; jumpForce = 4f; break;
                case KinematicPreset.Loose:
                    walkSpeed = 2.5f; runSpeed = 8.5f; acceleration = 12f; gravity = -9.81f; jumpForce = 5.5f; break;
                default:
                    walkSpeed = 2f; runSpeed = 7f; acceleration = 20f; gravity = -9.81f; jumpForce = 5f; break;
            }
        }

        void Update()
        {
            // Read decoupled input
            float2 move = PlayerInputState.Move;
            float2 look = PlayerInputState.LookDelta;
            bool jump = PlayerInputState.Jump;
            bool crouch = PlayerInputState.Crouch;
            bool sprint = PlayerInputState.Sprint;

            // Yaw
            float yaw = look.x * lookSensitivity;
            transform.Rotate(0f, yaw, 0f);

            // If currently smoothing a step, skip movement (rotation still allowed)
            if (isStepping)
                return;

            // Local desired movement
            Vector3 desired = transform.forward * move.y + transform.right * move.x;
            desired = Vector3.ClampMagnitude(desired, 1f);

            float targetSpeed = sprint ? runSpeed : walkSpeed;
            Vector3 targetVelocity = desired * targetSpeed;

            // Smooth horizontal velocity
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 smoothed = Vector3.MoveTowards(horizontal, targetVelocity, acceleration * Time.deltaTime);

            // Crouch handling
            if (crouch != isCrouched)
            {
                isCrouched = crouch;
                height = isCrouched ? Mathf.Max(0.5f, height * 0.5f) : Mathf.Max(1f, height * 2f); // preserve some values if toggled from inspector
            }

            // Gravity & jump
            bool grounded = CheckGround(out RaycastHit groundHit);
            if (grounded)
            {
                if (jump)
                    verticalVelocity = jumpForce;
                else
                    verticalVelocity = -0.5f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            // Store velocity; actual position updates happen in FixedUpdate using Rigidbody.MovePosition when enabled
            velocity = new Vector3(smoothed.x, verticalVelocity, smoothed.z);
        }

        void FixedUpdate()
        {
            if (isStepping) return;

            float dt = Time.fixedDeltaTime;
            Vector3 displacement = velocity * dt;
            // Clamp per-fixed-step displacement
            float maxDisp = Mathf.Max(0.001f, maxDisplacementPerSecond) * dt;
            if (displacement.magnitude > maxDisp)
                displacement = displacement.normalized * maxDisp;

            // Horizontal probe movement with step-climb
            Vector3 horizontalDisp = new Vector3(displacement.x, 0f, displacement.z);
            if (horizontalDisp.sqrMagnitude > 0f)
            {
                if (!MoveWithCollision(horizontalDisp))
                {
                    if (TryStepUp(horizontalDisp))
                    {
                        // StepSmooth will perform the actual move
                    }
                }
            }

            // Vertical movement (apply after horizontal to allow stepping)
            Vector3 verticalDisp = new Vector3(0f, displacement.y, 0f);
            MoveVertical(verticalDisp);

            // Apply any accumulated push impulses (smoothed over frames)
            ApplyPendingPushes(dt);
            
            // Check for loot interaction proactively (ignores movement masks)
            ProactiveLootCollisionCheck();
        }

        private Collider[] _proactiveHitBuffer = new Collider[10];
        void ProactiveLootCollisionCheck()
        {
            Vector3 currentPos = rb != null ? rb.position : transform.position;
            Vector3 bottom = currentPos + Vector3.up * (skinWidth + 0.01f);
            Vector3 top = currentPos + Vector3.up * (height - skinWidth - 0.01f);
            
            // Broad phase check for anything nearby
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + 0.05f, _proactiveHitBuffer, -1, QueryTriggerInteraction.Collide);
            
            for (int i = 0; i < count; i++)
            {
                var col = _proactiveHitBuffer[i];
                if (col == null) continue;
                if (col.transform.root == transform.root) continue; // Ignore self

                var lootSim = col.GetComponentInParent<DIG.Voxel.Interaction.LootPhysicsSimulator>();
                if (lootSim != null)
                {
                     // Flatten push dir to horizontal
                     Vector3 pushDir = new Vector3(velocity.x, 0, velocity.z).normalized;
                     if (pushDir.sqrMagnitude < 0.01f) pushDir = transform.forward;
                     
                     // Apply impulse
                     lootSim.ApplyImpulse(pushDir * pushForce * Time.fixedDeltaTime * 60f);
                }
            }
        }

        bool MoveWithCollision(Vector3 disp)
        {
            float moveDistance = disp.magnitude;
            if (moveDistance <= 0.0001f || float.IsNaN(moveDistance)) return true;

            Vector3 dir = disp / moveDistance;
            Vector3 currentPos = rb != null ? rb.position : transform.position;
            Vector3 bottom = currentPos + Vector3.up * (skinWidth + 0.01f);
            Vector3 top = currentPos + Vector3.up * (height - skinWidth - 0.01f);

            if (Physics.CapsuleCast(bottom, top, radius, dir, out RaycastHit hit, moveDistance + skinWidth, EffectiveCollisionMask, QueryTriggerInteraction.Ignore))
            {
                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                
                // PUSH LOGIC
                if (pushRigidbodies && hit.rigidbody != null)
                {
                    float impulseMag = Mathf.Min(pushForce, maxPushImpulse);
                    AccumulatePush(hit.rigidbody, dir * impulseMag, hit.point);
                }
                
                // Note: Loot interaction is now handled via ProactiveLootCollisionCheck in FixedUpdate

                if (slopeAngle <= 90f && slopeAngle <= 45f)
                {
                    // treat as low obstacle or gentle slope; move up small amount if needed

                    // Slide along plane
                    Vector3 projected = Vector3.ProjectOnPlane(disp, hit.normal);
                    Vector3 slide = projected.normalized * Mathf.Max(0f, moveDistance - hit.distance);
                    Vector3 target = currentPos + projected.normalized * hit.distance + hit.normal * (skinWidth + 0.001f);
                    if (rb != null) SafeMovePosition(target); else transform.position = target;
                    return false;
                }
                else
                {
                    // steep surface; block movement
                    return false;
                }
            }

            // no hit: safe to move
            Vector3 newPos = currentPos + disp;
            if (rb != null) SafeMovePosition(newPos); else transform.position = newPos;
            // Try to resolve small penetrations after moving
            ResolveOverlaps();
            return true;
        }

        bool TryStepUp(Vector3 horizontalDisp)
        {
            // Probe upward by stepHeight and forward to see if supply is clear
            Vector3 currentPos = rb != null ? rb.position : transform.position;
            Vector3 upOffset = Vector3.up * stepHeight;
            Vector3 probePos = currentPos + upOffset;
            Vector3 bottom = probePos + Vector3.up * (skinWidth + 0.01f);
            Vector3 top = probePos + Vector3.up * (height - skinWidth - 0.01f);
            float moveDistance = horizontalDisp.magnitude;
            Vector3 dir = horizontalDisp.normalized;

            // foot probe: ensure there's support under new position
            Vector3 footCenter = probePos + dir * (moveDistance + radius);
            bool headClear = !Physics.CheckCapsule(top, bottom, radius, EffectiveCollisionMask, QueryTriggerInteraction.Ignore);

            if (Physics.CapsuleCast(bottom, top, radius, dir, out RaycastHit hit, moveDistance + skinWidth, EffectiveCollisionMask, QueryTriggerInteraction.Ignore))
            {
                // blocked at stepped-up height
                return false;
            }

            // ensure head clear at final position
            Vector3 finalBottom = currentPos + upOffset + Vector3.up * (skinWidth + 0.01f);
            Vector3 finalTop = currentPos + upOffset + Vector3.up * (height - skinWidth - 0.01f);
            if (Physics.CheckCapsule(finalBottom, finalTop, radius, EffectiveCollisionMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // compute final position and start smoothing coroutine
            Vector3 tentativePos = currentPos + upOffset + horizontalDisp;

            // drop down to find ground under tentative position
            float groundY = tentativePos.y;
            if (Physics.Raycast(tentativePos + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit2, stepHeight + 0.5f, EffectiveCollisionMask, QueryTriggerInteraction.Ignore))
            {
                groundY = groundHit2.point.y + 0.01f;
            }

            Vector3 finalPos = new Vector3(tentativePos.x, groundY, tentativePos.z);

            // start smoothing coroutine
            if (stepRoutine != null)
                StopCoroutine(stepRoutine);
            stepRoutine = StartCoroutine(StepSmooth(finalPos, stepSmoothTime));
            return true;
        }

        System.Collections.IEnumerator StepSmooth(Vector3 targetPos, float time)
        {
            isStepping = true;
            Vector3 start = rb != null ? rb.position : transform.position;
            float t = 0f;
            // zero vertical velocity while stepping
            verticalVelocity = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float alpha = Mathf.SmoothStep(0f, 1f, t / time);
                Vector3 pos = Vector3.Lerp(start, targetPos, alpha);
                if (rb != null) SafeMovePosition(pos); else transform.position = pos;
                yield return null;
            }
            if (rb != null) SafeMovePosition(targetPos); else transform.position = targetPos;
            isStepping = false;
            stepRoutine = null;
        }

        void MoveVertical(Vector3 disp)
        {
            if (Mathf.Abs(disp.y) < 0.0001f || float.IsNaN(disp.y)) return;

            Vector3 dir = disp.y > 0f ? Vector3.up : Vector3.down;
            float dist = Mathf.Abs(disp.y);

            Vector3 currentPos = rb != null ? rb.position : transform.position;
            Vector3 bottom = currentPos + Vector3.up * (skinWidth + 0.01f);
            Vector3 top = currentPos + Vector3.up * (height - skinWidth - 0.01f);

            // For downward movement, perform an extended capsule sweep to avoid tunneling
            float dt = rb != null ? Time.fixedDeltaTime : Time.deltaTime;
            float sweepExtra = Mathf.Max(dist, Mathf.Max(0.1f, maxDisplacementPerSecond * dt));
            if (dir == Vector3.down)
                sweepExtra = Mathf.Max(sweepExtra, stepHeight + 0.05f);

            if (Physics.CapsuleCast(bottom, top, radius, dir, out RaycastHit hit, dist + skinWidth + sweepExtra, EffectiveCollisionMask, QueryTriggerInteraction.Ignore))
            {
                // collided; place just before contact
                float move = Mathf.Max(0f, hit.distance - skinWidth);
                Vector3 target = currentPos + dir * move;
                if (rb != null) SafeMovePosition(target); else transform.position += dir * move;
                // zero vertical velocity when hitting ground
                if (dir == Vector3.down)
                    verticalVelocity = 0f;

                // if hitting rigidbody, accumulate push (capped)
                if (pushRigidbodies && hit.rigidbody != null)
                {
                    float impulseMag = Mathf.Min(pushForce, maxPushImpulse);
                    AccumulatePush(hit.rigidbody, dir * impulseMag, hit.point);
                }
                // Also check for manual physics simulator (Loot)
                var lootSim = hit.collider.GetComponent<DIG.Voxel.Interaction.LootPhysicsSimulator>();
                if (lootSim != null)
                {
                     // Simplified push for loot
                     lootSim.ApplyImpulse(dir * pushForce * Time.deltaTime * 60f); // Scale for frame time
                }
                // attempt to pop out of any small penetrations
                ResolveOverlaps();
            }
            else
            {
                Vector3 newPos = currentPos + disp;
                if (rb != null) SafeMovePosition(newPos); else transform.position = newPos;
                ResolveOverlaps();
            }
        }

        // Robust overlap resolver: uses the attached CapsuleCollider + Physics.ComputePenetration when available
        void ResolveOverlaps()
        {
            Vector3 currentPos = rb != null ? rb.position : transform.position;

            // If we have a real CapsuleCollider, use ComputePenetration for accurate separation
            if (capsule != null && rb != null)
            {
                Collider[] overlaps = Physics.OverlapCapsule(currentPos + Vector3.up * (skinWidth + 0.01f), currentPos + Vector3.up * (height - skinWidth - 0.01f), radius, EffectiveCollisionMask, QueryTriggerInteraction.Ignore);
                if (overlaps == null || overlaps.Length == 0) return;

                int iterations = 0;
                foreach (var other in overlaps)
                {
                    if (other == null || other == capsule) continue;
                    if (Physics.ComputePenetration(capsule, rb.position, rb.rotation, other, other.transform.position, other.transform.rotation, out Vector3 dir, out float dist))
                    {
                        if (dist > 0.0001f)
                        {
                            Vector3 sep = dir * (dist + 0.001f);
                            SafeMovePosition(rb.position + sep);
                        }
                    }
                    iterations++;
                    if (iterations > 6) break;
                }
                return;
            }

            // Fallback simple resolver (no capsule collider present)
            Vector3 bottom = currentPos + Vector3.up * (skinWidth + 0.01f);
            Vector3 top = currentPos + Vector3.up * (height - skinWidth - 0.01f);
            Collider[] overlaps2 = Physics.OverlapCapsule(bottom, top, radius, EffectiveCollisionMask, QueryTriggerInteraction.Ignore);
            if (overlaps2 == null || overlaps2.Length == 0) return;
            Vector3 mid = (bottom + top) * 0.5f;
            int it = 0;
            foreach (var col in overlaps2)
            {
                if (col == null) continue;
                Vector3 closest = col.ClosestPoint(mid);
                Vector3 away = mid - closest;
                float dist = away.magnitude;
                float penetration = radius - dist;
                if (penetration > 0.0005f)
                {
                    Vector3 pushDir = dist > 0.0001f ? away.normalized : Vector3.up;
                    if (rb != null) SafeMovePosition(rb.position + pushDir * (penetration + 0.001f)); else transform.position += pushDir * (penetration + 0.001f);
                    it++;
                    if (it > 4) break;
                }
            }
        }

        private void SafeMovePosition(Vector3 pos)
        {
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
            {
                return;
            }
            if (float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z))
            {
                return;
            }
            rb.MovePosition(pos);
        }

        void AccumulatePush(Rigidbody target, Vector3 impulse, Vector3 hitPoint)
        {
            if (target == null) return;
            if (!pendingPushes.TryGetValue(target, out PushData data))
            {
                data = new PushData { accumulated = Vector3.zero, hitPoint = hitPoint };
            }
            data.accumulated += impulse;
            // cap total accumulated impulse
            if (data.accumulated.magnitude > maxAccumulatedPush)
            {
                data.accumulated = data.accumulated.normalized * maxAccumulatedPush;
            }
            // update last hit point
            data.hitPoint = hitPoint;
            pendingPushes[target] = data;
        }

        void ApplyPendingPushes(float dt)
        {
            if (pendingPushes.Count == 0) return;
            var keys = new List<Rigidbody>(pendingPushes.Keys);
            foreach (var rbTarget in keys)
            {
                if (rbTarget == null) { pendingPushes.Remove(rbTarget); continue; }
                PushData data = pendingPushes[rbTarget];
                // fraction to apply this FixedUpdate
                Vector3 toApply = data.accumulated * Mathf.Clamp01(pushApplyFraction);
                if (toApply.sqrMagnitude > 0f)
                {
                    rbTarget.AddForceAtPosition(toApply, data.hitPoint, ForceMode.Impulse);
                    data.accumulated -= toApply;
                }
                // apply decay to remaining accumulated impulse
                float mag = data.accumulated.magnitude;
                if (mag > 0.0001f)
                {
                    float decayAmt = pushAccumulatedDecay * dt;
                    float newMag = Mathf.Max(0f, mag - decayAmt);
                    if (newMag <= 0.0001f)
                    {
                        pendingPushes.Remove(rbTarget);
                        continue;
                    }
                    data.accumulated = data.accumulated.normalized * newMag;
                }
                // remove if tiny
                if (data.accumulated.magnitude <= 0.001f)
                    pendingPushes.Remove(rbTarget);
                else
                    pendingPushes[rbTarget] = data;
            }
        }
        bool CheckGround(out RaycastHit hit)
        {
            Vector3 origin = (rb != null ? rb.position : transform.position) + Vector3.up * 0.1f;
            if (Physics.SphereCast(origin, radius, Vector3.down, out hit, 0.15f + skinWidth, EffectiveCollisionMask, QueryTriggerInteraction.Ignore))
                return true;
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 basePos = rb != null ? rb.position : transform.position;
            Vector3 bottom = basePos + Vector3.up * (skinWidth + 0.01f);
            Vector3 top = basePos + Vector3.up * (height - skinWidth - 0.01f);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawLine(bottom + Vector3.left * radius, top + Vector3.left * radius);
            Gizmos.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
        }
    }
}
