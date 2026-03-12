using Unity.Mathematics;
using Unity.Entities;

public partial struct PlayerMovementSystem
{
    /// <summary>
    /// Applies friction to the velocity vector
    /// </summary>
    private static void ApplyFriction(ref float3 currentVel, float friction, float deltaTime, float targetSpeed,
        float surfaceFrictionMultiplier = 1.0f)
    {
        float speed = math.length(currentVel);
        if (speed < 0.001f) return;

        // Decelerate
        // Using targetSpeed as 'StopSpeed' for control calculation
        float control = math.max(speed, targetSpeed);
        float drop = control * (friction * surfaceFrictionMultiplier) * deltaTime;

        // Only scale if we have speed to avoid NaN
        float newSpeed = math.max(0, speed - drop);
        if (newSpeed != speed)
        {
            newSpeed /= speed;
            currentVel *= newSpeed;
        }
    }
}
