namespace DIG.Shared
{
    /// <summary>
    /// Types of resources that can be collected.
    /// Moved to Shared to prevent circular dependencies between Survival and Interaction.
    /// </summary>
    public enum ResourceType : byte
    {
        None = 0,
        Stone = 1,
        Metal = 2,
        BioMass = 3,
        Crystal = 4,
        TitanBone = 5,
        ThermalGlass = 6,
        Isotope = 7
    }
}
