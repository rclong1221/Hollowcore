using Unity.Mathematics;
using Unity.Physics;

namespace DIG.Vision.Core
{
    /// <summary>
    /// Pure static utility for detection math and physics queries.
    /// Handles vision cones, proximity sensing, line-of-sight, and hearing.
    /// This is the swappable core of the detection system.
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// </summary>
    public static class DetectionQueryUtility
    {
        /// <summary>
        /// Checks if a target position is within a vision cone.
        /// </summary>
        /// <param name="sensorPos">World position of the sensor (eye position).</param>
        /// <param name="sensorForward">Normalized forward direction of the sensor.</param>
        /// <param name="targetPos">World position of the target.</param>
        /// <param name="halfAngleDegrees">Half-angle of the vision cone in degrees.</param>
        /// <param name="maxDistance">Maximum detection distance.</param>
        /// <returns>True if target is within cone and range.</returns>
        public static bool IsInCone(
            float3 sensorPos,
            float3 sensorForward,
            float3 targetPos,
            float halfAngleDegrees,
            float maxDistance)
        {
            float3 toTarget = targetPos - sensorPos;
            float distSq = math.lengthsq(toTarget);

            // Distance check (squared to avoid sqrt)
            if (distSq > maxDistance * maxDistance || distSq < 0.01f)
                return false;

            float3 toTargetNorm = math.normalize(toTarget);
            float dot = math.dot(sensorForward, toTargetNorm);
            float angleDeg = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));

            return angleDeg <= halfAngleDegrees;
        }

        /// <summary>
        /// Performs an occlusion raycast to determine if there is a clear line-of-sight
        /// between two points.
        /// </summary>
        /// <param name="physicsWorld">The physics world to raycast against.</param>
        /// <param name="eyePos">Start point (sensor eye position).</param>
        /// <param name="targetPos">End point (target detection point).</param>
        /// <param name="occlusionFilter">Collision filter defining what blocks vision.</param>
        /// <returns>True if nothing blocks the ray (clear LOS).</returns>
        public static bool HasLineOfSight(
            in PhysicsWorld physicsWorld,
            float3 eyePos,
            float3 targetPos,
            CollisionFilter occlusionFilter)
        {
            float3 direction = targetPos - eyePos;
            float distance = math.length(direction);

            if (distance < 0.01f)
                return true;

            // Offset start point slightly forward to avoid hitting the sensor's own collider
            // This prevents self-collision when the eye position is inside/on the edge of a collider
            const float StartOffset = 0.5f;
            float3 dirNorm = direction / distance;
            float3 offsetStart = eyePos + dirNorm * StartOffset;
            float offsetDistance = distance - StartOffset;

            // If we're too close after offset, consider LOS clear
            if (offsetDistance < 0.1f)
                return true;

            var rayInput = new RaycastInput
            {
                Start = offsetStart,
                End = targetPos,
                Filter = occlusionFilter
            };

            // If the ray hits something (occlusion geometry), LOS is blocked
            if (physicsWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit))
            {
                // LOS blocked by hit.Entity
                return false;
            }
            return true;
        }

        /// <summary>
        /// Combined check: distance → cone (or proximity) → line-of-sight.
        /// Applies stealth multiplier to reduce effective detection range.
        /// </summary>
        /// <param name="physicsWorld">The physics world to raycast against.</param>
        /// <param name="eyePos">World position of the sensor eye.</param>
        /// <param name="sensorForward">Normalized forward direction of the sensor.</param>
        /// <param name="targetPos">World position of the target detection point.</param>
        /// <param name="viewDistance">Base view distance of the sensor.</param>
        /// <param name="halfAngleDegrees">Half-angle of the vision cone in degrees.</param>
        /// <param name="proximityRadius">360° detection radius. 0 = disabled (cone only, default).</param>
        /// <param name="stealthMultiplier">Target's stealth modifier (1.0 = normal, 0.0 = invisible).</param>
        /// <param name="occlusionFilter">Collision filter defining what blocks vision.</param>
        /// <returns>True if the target is visible (in cone or proximity, in range, clear LOS).</returns>
        public static bool CanSee(
            in PhysicsWorld physicsWorld,
            float3 eyePos,
            float3 sensorForward,
            float3 targetPos,
            float viewDistance,
            float halfAngleDegrees,
            float proximityRadius,
            float stealthMultiplier,
            CollisionFilter occlusionFilter)
        {
            float effectiveDistance = viewDistance * math.clamp(stealthMultiplier, 0f, 1f);

            if (effectiveDistance <= 0f)
                return false;

            // Calculate distance and angle
            float3 toTarget = targetPos - eyePos;
            float dist = math.length(toTarget);
            float3 toTargetNorm = math.normalize(toTarget);
            float dot = math.dot(sensorForward, toTargetNorm);
            float angleDeg = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));

            // Check cone first (most common case)
            bool inCone = IsInCone(eyePos, sensorForward, targetPos, halfAngleDegrees, effectiveDistance);
            
            // Proximity check: 360° detection when within radius (for creatures with all-around sensing)
            // Only checked if proximityRadius > 0 (disabled by default)
            bool inProximity = proximityRadius > 0f && dist <= proximityRadius;
            
            if (!inCone && !inProximity)
            {
                return false;
            }

            // Line-of-sight check
            return HasLineOfSight(in physicsWorld, eyePos, targetPos, occlusionFilter);
        }
    }
}
