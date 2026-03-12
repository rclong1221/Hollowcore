using System.IO;
using Unity.Entities;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Interface for modular save/load serializers.
    /// Each game subsystem implements one ISaveModule that knows how to
    /// read ECS state and write a self-describing binary block.
    /// </summary>
    public interface ISaveModule
    {
        /// <summary>
        /// Stable numeric identifier written in save block headers.
        /// MUST NOT CHANGE across versions once assigned.
        /// </summary>
        int TypeId { get; }

        /// <summary>Human-readable name for editor tooling and logging.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Current schema version. Increment when adding/removing/reordering fields.
        /// </summary>
        int ModuleVersion { get; }

        /// <summary>
        /// Read ECS state, write bytes. Returns byte count written (0 = skip this module).
        /// </summary>
        int Serialize(in SaveContext context, BinaryWriter writer);

        /// <summary>
        /// Read bytes, apply ECS state. blockVersion may differ from current ModuleVersion
        /// if the save file was created with an older schema.
        /// </summary>
        void Deserialize(in LoadContext context, BinaryReader reader, int blockVersion);

        /// <summary>
        /// True if unsaved changes exist since last save. Default returns true (always dirty).
        /// </summary>
        bool IsDirty(in SaveContext context) { return true; }
    }

    /// <summary>Context passed to ISaveModule.Serialize().</summary>
    public readonly struct SaveContext
    {
        public readonly EntityManager EntityManager;
        public readonly Entity PlayerEntity;
        public readonly int FormatVersion;
        public readonly float ElapsedPlaytime;
        public readonly uint ServerTick;

        public SaveContext(EntityManager em, Entity player, int formatVersion, float playtime, uint tick)
        {
            EntityManager = em;
            PlayerEntity = player;
            FormatVersion = formatVersion;
            ElapsedPlaytime = playtime;
            ServerTick = tick;
        }
    }

    /// <summary>Context passed to ISaveModule.Deserialize().</summary>
    public readonly struct LoadContext
    {
        public readonly EntityManager EntityManager;
        public readonly Entity PlayerEntity;
        public readonly EntityCommandBuffer ECB;
        public readonly int FormatVersion;

        public LoadContext(EntityManager em, Entity player, EntityCommandBuffer ecb, int formatVersion)
        {
            EntityManager = em;
            PlayerEntity = player;
            ECB = ecb;
            FormatVersion = formatVersion;
        }
    }
}
