namespace DIG.Voxel.Fluids
{
    /// <summary>
    /// Types of fluids in the voxel world.
    /// </summary>
    public enum FluidType : byte
    {
        None = 0,
        Water = 1,
        Oil = 2,
        Lava = 3,
        ToxicGas = 4,
        Acid = 5
    }
    
    /// <summary>
    /// Types of damage fluids can deal.
    /// </summary>
    public enum FluidDamageType : byte
    {
        None = 0,
        Drowning = 1,
        Burning = 2,
        Toxic = 3,
        Corrosive = 4,
        Crushing = 5
    }
}
