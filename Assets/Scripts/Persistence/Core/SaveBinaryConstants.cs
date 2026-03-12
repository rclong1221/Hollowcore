namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Binary format constants for .dig and .digw save files.
    /// </summary>
    public static class SaveBinaryConstants
    {
        /// <summary>Player save file magic: "DIGS" (0x44 0x49 0x47 0x53).</summary>
        public const uint MagicPlayer = 0x53474944;

        /// <summary>World save file magic: "DIGW" (0x44 0x49 0x47 0x57).</summary>
        public const uint MagicWorld = 0x57474944;

        /// <summary>End-of-file marker: "DEND" (0x44 0x45 0x4E 0x44).</summary>
        public const uint EOFMarker = 0x444E4544;

        /// <summary>Current file format version.</summary>
        public const int CurrentFormatVersion = 1;

        /// <summary>Offset in the file header where CRC32 checksum is stored.</summary>
        public const int CRC32Offset = 0x50;

        /// <summary>Size of the fixed header (before module blocks).</summary>
        public const int HeaderSize = 0x56;

        /// <summary>Size of each module block header (TypeId + ModuleVersion + DataLength).</summary>
        public const int ModuleBlockHeaderSize = 10;

        /// <summary>Player name field size in header (FixedString64Bytes, UTF-8, null-padded).</summary>
        public const int PlayerNameFieldSize = 64;

        /// <summary>GZip compression flag for world data blocks.</summary>
        public const byte CompressionNone = 0x00;
        public const byte CompressionGZip = 0x01;
    }
}
