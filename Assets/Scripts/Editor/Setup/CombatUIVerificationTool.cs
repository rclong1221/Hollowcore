using UnityEngine;
using UnityEditor;

namespace DIG.Editor.Setup
{
    /// <summary>
    /// EPIC 16.11: Quick diagnostic tool that checks if all required Combat UI
    /// MonoBehaviours are present in the active scene. Reports pass/fail for each.
    /// Menu: DIG > Diagnostics > Verify Combat UI Setup
    /// </summary>
    public static class CombatUIVerificationTool
    {
        [MenuItem("DIG/Diagnostics/Verify Combat UI Setup")]
        public static void VerifyCombatUISetup()
        {
            int warnings = 0;
            int passes = 0;

            Debug.Log("=== Combat UI Verification (EPIC 16.11) ===");

            // 1. Damage Number Provider
            var damageAdapter = Object.FindFirstObjectByType<DIG.Combat.UI.DamageNumberAdapterBase>();
            if (damageAdapter != null)
            {
                Debug.Log($"[PASS] DamageNumberAdapter: {damageAdapter.GetType().Name} on '{damageAdapter.gameObject.name}'");

                // Check if feedback profile is assigned
                var so = new SerializedObject(damageAdapter);
                var profileProp = so.FindProperty("feedbackProfile");
                if (profileProp != null && profileProp.objectReferenceValue != null)
                {
                    Debug.Log($"  [PASS] DamageFeedbackProfile assigned: {profileProp.objectReferenceValue.name}");
                    passes++;
                }
                else
                {
                    Debug.LogWarning("  [WARN] DamageFeedbackProfile is NOT assigned on the adapter. " +
                        "Damage numbers will use fallback colors. Use DIG > Setup > Create Damage Feedback System.");
                    warnings++;
                }
                passes++;
            }
            else
            {
                Debug.LogWarning("[MISS] No DamageNumberAdapterBase subclass in scene. " +
                    "ALL damage numbers will be silently dropped. " +
                    "Add DamageNumbersProAdapter to your Canvas or use DIG > Setup > Combat UI.");
                warnings++;
            }

            // 2. CombatUIBootstrap
            var bootstrap = Object.FindFirstObjectByType<DIG.Combat.UI.CombatUIBootstrap>();
            if (bootstrap != null)
            {
                Debug.Log($"[PASS] CombatUIBootstrap on '{bootstrap.gameObject.name}'");
                passes++;
            }
            else
            {
                Debug.LogWarning("[MISS] CombatUIBootstrap not in scene. " +
                    "Hitmarkers, directional damage, combo counter, and kill feed will NOT work.");
                warnings++;
            }

            // 3. ShaderHealthBarSync (player health bar)
            var healthBarSync = Object.FindFirstObjectByType<global::Combat.UI.ShaderHealthBarSync>();
            if (healthBarSync != null)
            {
                Debug.Log($"[PASS] ShaderHealthBarSync on '{healthBarSync.gameObject.name}'");
                passes++;
            }
            else
            {
                Debug.LogWarning("[MISS] ShaderHealthBarSync not in scene. Player health bar will not display.");
                warnings++;
            }

            // 4. EnemyHealthBarPool
            var healthBarPool = Object.FindFirstObjectByType<DIG.Combat.UI.WorldSpace.EnemyHealthBarPool>();
            if (healthBarPool != null)
            {
                Debug.Log($"[PASS] EnemyHealthBarPool on '{healthBarPool.gameObject.name}'");
                passes++;
            }
            else
            {
                Debug.LogWarning("[MISS] EnemyHealthBarPool not in scene. Enemy floating health bars will not display.");
                warnings++;
            }

            // Summary
            Debug.Log($"=== Verification Complete: {passes} passed, {warnings} warnings ===");
            if (warnings > 0)
            {
                Debug.LogWarning($"[CombatUI] {warnings} issue(s) found. " +
                    "Use DIG > Setup > Combat UI to fix missing components.");
            }
            else
            {
                Debug.Log("[CombatUI] All required components present.");
            }
        }
    }
}
