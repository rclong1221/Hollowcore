using System;
using System.IO;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Synchronous file reader with magic byte and CRC32 validation.
    /// Attempts .tmp recovery if primary file is missing.
    /// </summary>
    public static class SaveFileReader
    {
        /// <summary>
        /// Reads a save file into a byte array. Returns null if file doesn't exist or is invalid.
        /// Attempts .tmp recovery if primary file is missing.
        /// </summary>
        public static byte[] ReadFile(string filePath)
        {
            if (File.Exists(filePath))
                return File.ReadAllBytes(filePath);

            // Attempt .tmp recovery
            string tmpPath = filePath + ".tmp";
            if (File.Exists(tmpPath))
            {
                Debug.LogWarning($"[SaveFileReader] Primary file missing, attempting .tmp recovery: {tmpPath}");
                byte[] tmpData = File.ReadAllBytes(tmpPath);
                if (ValidateCRC32(tmpData))
                {
                    // Promote .tmp to primary
                    try
                    {
                        File.Move(tmpPath, filePath);
                        Debug.Log($"[SaveFileReader] Successfully recovered .tmp file to {filePath}");
                        return tmpData;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SaveFileReader] Failed to promote .tmp: {e.Message}");
                        return tmpData;
                    }
                }
                else
                {
                    Debug.LogError($"[SaveFileReader] .tmp file failed CRC32 validation: {tmpPath}");
                    return null;
                }
            }

            return null;
        }

        /// <summary>Validates the magic bytes at the start of the file.</summary>
        public static bool ValidateMagic(byte[] data, uint expectedMagic)
        {
            if (data == null || data.Length < 4) return false;
            uint magic = BitConverter.ToUInt32(data, 0);
            return magic == expectedMagic;
        }

        /// <summary>
        /// Validates the CRC32 checksum stored at offset 0x50.
        /// The checksum covers all bytes after the checksum field (offset 0x54 to end).
        /// </summary>
        public static bool ValidateCRC32(byte[] data)
        {
            if (data == null || data.Length < SaveBinaryConstants.HeaderSize)
                return false;

            uint storedChecksum = BitConverter.ToUInt32(data, SaveBinaryConstants.CRC32Offset);
            uint computed = ComputeCRC32(data, SaveBinaryConstants.CRC32Offset + 4, data.Length - (SaveBinaryConstants.CRC32Offset + 4));
            return storedChecksum == computed;
        }

        /// <summary>Precomputed CRC32 lookup table (polynomial 0xEDB88320).</summary>
        private static readonly uint[] CRC32Table = InitCRC32Table();

        private static uint[] InitCRC32Table()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ 0xEDB88320;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }
            return table;
        }

        /// <summary>Computes CRC32 over a byte range using precomputed lookup table.</summary>
        public static uint ComputeCRC32(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFF;
            int end = offset + length;
            if (end > data.Length) end = data.Length;
            for (int i = offset; i < end; i++)
                crc = (crc >> 8) ^ CRC32Table[(crc ^ data[i]) & 0xFF];
            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>Patches the CRC32 checksum into a byte array at the standard offset.</summary>
        public static void PatchCRC32(byte[] data)
        {
            if (data == null || data.Length < SaveBinaryConstants.HeaderSize) return;
            uint crc = ComputeCRC32(data, SaveBinaryConstants.CRC32Offset + 4, data.Length - (SaveBinaryConstants.CRC32Offset + 4));
            byte[] crcBytes = BitConverter.GetBytes(crc);
            Buffer.BlockCopy(crcBytes, 0, data, SaveBinaryConstants.CRC32Offset, 4);
        }
    }
}
