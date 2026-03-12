using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Applies file-level migration steps in sequence to bring
    /// old save files up to the current format version.
    /// </summary>
    public static class SaveMigrationRunner
    {
        private static readonly List<IMigrationStep> _steps = new();

        /// <summary>Register a migration step. Call during bootstrap.</summary>
        public static void Register(IMigrationStep step)
        {
            _steps.Add(step);
            _steps.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
        }

        public static void Clear() => _steps.Clear();

        /// <summary>
        /// Applies all registered migration steps from the file's version
        /// up to the target version. Returns the migrated byte array.
        /// </summary>
        public static byte[] MigrateToLatest(byte[] fileBytes, int fileVersion, int targetVersion)
        {
            if (fileVersion >= targetVersion)
                return fileBytes;

            byte[] current = fileBytes;
            int currentVersion = fileVersion;

            foreach (var step in _steps)
            {
                if (step.FromVersion == currentVersion && step.ToVersion <= targetVersion)
                {
                    try
                    {
                        current = step.Migrate(current);
                        currentVersion = step.ToVersion;
                        Debug.Log($"[SaveMigration] Migrated V{step.FromVersion} → V{step.ToVersion}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SaveMigration] Failed V{step.FromVersion}→V{step.ToVersion}: {e.Message}");
                        return current;
                    }
                }

                if (currentVersion >= targetVersion)
                    break;
            }

            if (currentVersion < targetVersion)
                Debug.LogWarning($"[SaveMigration] Incomplete: reached V{currentVersion}, target was V{targetVersion}");

            return current;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _steps.Clear();
        }
    }
}
