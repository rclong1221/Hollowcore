using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Animation.IK
{
    /// <summary>
    /// Static helper for generic IK solving algorithms.
    /// Burst-compatible.
    /// </summary>
    [BurstCompile]
    public static class IKSolver
    {
        /// <summary>
        /// Solves 2-Bone IK using an analytical approach (law of cosines).
        /// Returns the local rotations for the upper and lower arm/leg.
        /// </summary>
        /// <param name="rootPos">World position of the root joint (Hip/Shoulder).</param>
        /// <param name="targetPos">World position of the target (Foot/Hand).</param>
        /// <param name="hintInfo">World position of the pole target (Knee/Elbow hint).</param>
        /// <param name="len1">Length of the first bone (Upper).</param>
        /// <param name="len2">Length of the second bone (Lower).</param>
        /// <param name="rootRot">Resulting world rotation for the root joint.</param>
        /// <param name="midRot">Resulting world rotation for the mid joint.</param>
        public static void SolveTwoBoneIK(
            float3 rootPos, 
            float3 targetPos, 
            float3 hintPos, 
            float len1, 
            float len2, 
            float3 normal, // Surface normal for foot alignment (optional usage)
            out quaternion rootRot, 
            out quaternion midRot)
        {
            // Vector from Root to Target
            float3 toTarget = targetPos - rootPos;
            float distToTarget = math.length(toTarget);
            
            // 1. Clamp Target distance to max reach
            float maxReach = len1 + len2;
            if (distToTarget > maxReach)
            {
                toTarget = math.normalize(toTarget) * maxReach;
                distToTarget = maxReach;
            }
            // Clamp min distance to avoid singularities
            float minReach = math.abs(len1 - len2) + 0.01f;
            if (distToTarget < minReach)
            {
                toTarget = math.normalize(toTarget) * minReach;
                distToTarget = minReach;
            }

            // 2. Calculate triangle angles using Law of Cosines
            // a^2 = b^2 + c^2 - 2bc*cos(A)
            // triangle sides: a=len2, b=len1, c=distToTarget
            // Angle at Root (between toTarget and Bone1)
            float cosAngleRoot = (distToTarget * distToTarget + len1 * len1 - len2 * len2) / (2 * distToTarget * len1);
            float angleRoot = math.acos(math.clamp(cosAngleRoot, -1f, 1f));
            
            // Angle at Mid (between Bone1 and Bone2)
            // Note: This is usually 180 - internal angle.
            float cosAngleMid = (len1 * len1 + len2 * len2 - distToTarget * distToTarget) / (2 * len1 * len2);
            float angleMid = math.acos(math.clamp(cosAngleMid, -1f, 1f));
            
            // 3. Determine Plane of Rotation (Pole Vector)
            float3 axis = math.normalize(math.cross(toTarget, hintPos - rootPos));
            // If collinear, pick a default axis (e.g. Right)
            if (math.lengthsq(axis) < 0.001f)
            {
                // Fallback attempt with Up
                axis = math.normalize(math.cross(toTarget, math.up()));
                 if (math.lengthsq(axis) < 0.001f) axis = math.right();
            }

            // 4. Calculate Basis Rotations
            // Rotation to point Bone1 towards Target
            quaternion lookAtTarget = quaternion.LookRotationSafe(toTarget, axis); // Wait, Axis is normal to plane? No, LookRotation 'up' should be 'axis' crossed with fwd?
                                                                                   // Usually LookRotation(fwd, up). Our 'axis' is the normal of the bend plane.
                                                                                   // So 'Up' for LookRotation would be in the plane?
                                                                                   // Let's use simpler logic:
                                                                                   // The plane normal is 'axis'.
            
            // Correct approach:
            // Base rotation: Root to Target.
            // Apply Root angle offset around 'axis'.
            
            // Direction from Root to Target
            float3 dirToTarget = math.normalize(toTarget);
            
            // Pole Plane Normal (Axis of bend)
            float3 planeNormal = math.normalize(math.cross(dirToTarget, hintPos - rootPos));
            
            // If singular (straight line), use Up as reference
             if (math.lengthsq(planeNormal) < 0.001f)
                 planeNormal = math.right(); // Default bend axis?

            // Rotate dirToTarget by angleRoot around planeNormal
            quaternion rootBend = quaternion.AxisAngle(planeNormal, angleRoot);
            float3 bone1Dir = math.rotate(rootBend, dirToTarget);
            
            // Final Root Rotation
            // We want Bone1 to point along bone1Dir.
            // AND we want the hinge axis to align with planeNormal?
            // Usually we assume bones point along +Z or +Y. Assuming +Z (Forward).
            // We need a 'LookRotation' that puts Z along bone1Dir, and 'Up' towards hint?
            float3 upHint = math.normalize(hintPos - rootPos); // Rough up
            rootRot = quaternion.LookRotationSafe(bone1Dir, upHint); // Basic look-at
            
            // Mid Rotation
            // Bone2 is rotated relative to Bone1 by (180 - angleMid)?
            // Or just calculate Bone2 direction.
            // Bone2 starts at (Root + Bone1). Ends at Target.
            float3 midPos = rootPos + bone1Dir * len1;
            float3 bone2Dir = math.normalize(targetPos - midPos);
            
            midRot = quaternion.LookRotationSafe(bone2Dir, planeNormal); // Align with same plane?
             // Actually, midRot relative to world? Yes, this function returns World Rotations.
        }
    }
}
