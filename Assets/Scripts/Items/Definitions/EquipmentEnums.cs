namespace DIG.Items.Definitions
{
    /// <summary>
    /// Defines how a weapon is gripped/held.
    /// Determines off-hand slot availability and animation behavior.
    /// </summary>
    public enum GripType
    {
        /// <summary>
        /// Weapon uses one hand only. Off-hand fully available for shields, etc.
        /// </summary>
        OneHanded = 0,
        
        /// <summary>
        /// Weapon requires both hands. Off-hand always suppressed.
        /// </summary>
        TwoHanded = 1,
        
        /// <summary>
        /// Weapon can be one or two-handed depending on context.
        /// Off-hand suppression is context-dependent.
        /// </summary>
        Versatile = 2
    }

    /// <summary>
    /// Defines how a weapon's primary action is used.
    /// </summary>
    public enum UseStyle
    {
        /// <summary>
        /// Single press triggers one action (pistol shot, sword swing).
        /// </summary>
        SingleUse = 0,
        
        /// <summary>
        /// Repeated presses chain into combos (melee combos).
        /// </summary>
        ComboChain = 1,
        
        /// <summary>
        /// Hold to channel continuous action (flamethrower, beam).
        /// </summary>
        HoldChannel = 2,
        
        /// <summary>
        /// Press to toggle on/off (flashlight, shield block).
        /// </summary>
        Toggle = 3,
        
        /// <summary>
        /// Hold to charge, release to fire (bow draw, charged attack).
        /// </summary>
        ChargeRelease = 4,
        
        /// <summary>
        /// Automatic fire while held (machine gun).
        /// </summary>
        Automatic = 5
    }

    /// <summary>
    /// Defines how a slot is rendered.
    /// </summary>
    public enum SlotRenderMode
    {
        /// <summary>
        /// Item always visible when equipped.
        /// </summary>
        AlwaysVisible = 0,
        
        /// <summary>
        /// Item only visible when actively selected.
        /// </summary>
        OnlyWhenEquipped = 1,
        
        /// <summary>
        /// Item visible in holster/sheath when not selected.
        /// </summary>
        Holstered = 2
    }

    /// <summary>
    /// Condition for a suppression rule.
    /// </summary>
    public enum SuppressionCondition
    {
        /// <summary>
        /// Target slot has any item equipped.
        /// </summary>
        HasItem = 0,
        
        /// <summary>
        /// Target slot has a two-handed weapon.
        /// </summary>
        HasTwoHanded = 1,
        
        /// <summary>
        /// Target slot has an item of a specific category.
        /// </summary>
        HasCategory = 2,
        
        /// <summary>
        /// Target slot has an item with specific GripType.
        /// </summary>
        HasGripType = 3
    }

    /// <summary>
    /// Action to take when suppression condition is met.
    /// </summary>
    public enum SuppressionAction
    {
        /// <summary>
        /// Hide the slot's visuals entirely.
        /// </summary>
        Hide = 0,
        
        /// <summary>
        /// Disable the slot but keep visuals.
        /// </summary>
        Disable = 1,
        
        /// <summary>
        /// Override with a different animation/state.
        /// </summary>
        Override = 2
    }
}
