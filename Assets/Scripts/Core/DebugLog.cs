using UnityEngine;

/// <summary>
/// Centralized debug logging system with compile-time flags
/// Define DEBUG_LOG_[CATEGORY] symbols in Player Settings to enable specific logging categories
/// Or define DEBUG_LOG_ALL to enable all logging
/// </summary>
public static class DebugLog
{
    // ===== INPUT LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_INPUT"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogInput(object message)
    {
        Debug.Log($"[INPUT] {message}");
    }

    // ===== MOVEMENT LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_MOVEMENT"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogMovement(object message)
    {
        Debug.Log($"[MOVEMENT] {message}");
    }

    [System.Diagnostics.Conditional("DEBUG_LOG_MOVEMENT"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogMovementWarning(object message)
    {
        Debug.LogWarning($"[MOVEMENT] {message}");
    }

    // ===== GROUND CHECK LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_GROUND_CHECK"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogGroundCheck(object message)
    {
        Debug.Log($"[GROUND_CHECK] {message}");
    }

    // ===== STATE LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_STATE"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogState(object message)
    {
        Debug.Log($"[STATE] {message}");
    }

    // ===== STANCE LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_STANCE"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogStance(object message)
    {
        Debug.Log($"[STANCE] {message}");
    }

    // ===== STAMINA LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_STAMINA"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogStamina(object message)
    {
        Debug.Log($"[STAMINA] {message}");
    }

    // ===== CAMERA LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_CAMERA"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogCamera(object message)
    {
        Debug.Log($"[CAMERA] {message}");
    }

    [System.Diagnostics.Conditional("DEBUG_LOG_CAMERA"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogCameraWarning(object message)
    {
        Debug.LogWarning($"[CAMERA] {message}");
    }

    // ===== NETWORK LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_NETWORK"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogNetwork(object message)
    {
        Debug.Log($"[NETWORK] {message}");
    }

    // ===== PHYSICS LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_PHYSICS"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogPhysics(object message)
    {
        Debug.Log($"[PHYSICS] {message}");
    }

    // ===== SPAWNING LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_SPAWNING"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogSpawning(object message)
    {
        Debug.Log($"[SPAWNING] {message}");
    }

    // ===== GENERAL SYSTEM LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_SYSTEMS"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogSystem(object message)
    {
        Debug.Log($"[SYSTEM] {message}");
    }

    [System.Diagnostics.Conditional("DEBUG_LOG_SYSTEMS"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogSystemWarning(object message)
    {
        Debug.LogWarning($"[SYSTEM] {message}");
    }

    // ===== ENTITY LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_ENTITIES"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogEntity(object message)
    {
        Debug.Log($"[ENTITY] {message}");
    }

    // ===== COMBAT LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_COMBAT"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogCombat(object message)
    {
        Debug.Log($"[COMBAT] {message}");
    }

    [System.Diagnostics.Conditional("DEBUG_LOG_COMBAT"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogCombatWarning(object message)
    {
        Debug.LogWarning($"[COMBAT] {message}");
    }

    // ===== ITEMS LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_ITEMS"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogItems(object message)
    {
        Debug.Log($"[ITEMS] {message}");
    }

    // ===== AI LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_AI"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogAI(object message)
    {
        Debug.Log($"[AI] {message}");
    }

    // ===== WEAPONS LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_WEAPONS"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogWeapons(object message)
    {
        Debug.Log($"[WEAPONS] {message}");
    }

    // ===== AGGRO LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_AGGRO"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogAggro(object message)
    {
        Debug.Log($"[AGGRO] {message}");
    }

    // ===== SURFACE LOGGING =====
    [System.Diagnostics.Conditional("DEBUG_LOG_SURFACE"), System.Diagnostics.Conditional("DEBUG_LOG_ALL")]
    public static void LogSurface(object message)
    {
        Debug.Log($"[SURFACE] {message}");
    }
}
