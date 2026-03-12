namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Interface for file-level save format migrations.
    /// Transforms raw bytes from one format version to the next.
    /// Applied in sequence by SaveMigrationRunner before module deserialization.
    /// </summary>
    public interface IMigrationStep
    {
        int FromVersion { get; }
        int ToVersion { get; }
        byte[] Migrate(byte[] fileBytes);
    }
}
