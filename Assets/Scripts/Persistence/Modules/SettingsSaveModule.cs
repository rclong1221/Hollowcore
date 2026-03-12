using System.IO;
using Unity.Entities;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Serializes player settings/preferences.
    /// Does NOT interact with ECS ghost components — reads/writes managed state.
    /// </summary>
    public class SettingsSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Settings;
        public string DisplayName => "Settings";
        public int ModuleVersion => 1;

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            long start = w.BaseStream.Position;

            // Placeholder settings — expand with actual GameSettings fields
            w.Write(1f);   // MouseSensitivityX
            w.Write(1f);   // MouseSensitivityY
            w.Write(1f);   // MasterVolume
            w.Write(0.8f); // MusicVolume
            w.Write(1f);   // SFXVolume
            w.Write((byte)0); // InvertYAxis
            w.Write((byte)0); // CrouchToggle
            w.Write((byte)0); // ProneToggle

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            // Read all fields even if not applied
            float mouseSensX = r.ReadSingle();
            float mouseSensY = r.ReadSingle();
            float masterVol = r.ReadSingle();
            float musicVol = r.ReadSingle();
            float sfxVol = r.ReadSingle();
            bool invertY = r.ReadByte() != 0;
            bool crouchToggle = r.ReadByte() != 0;
            bool proneToggle = r.ReadByte() != 0;

            // Apply to managed settings when available
            // TODO: Wire to GameSettings singleton when it exists
        }
    }
}
