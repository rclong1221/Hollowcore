namespace DIG.Combat.Resources
{
    /// <summary>
    /// EPIC 16.8 Phase 0: Data for a single resource pool. 32 bytes, blittable, Burst-safe.
    /// </summary>
    public struct ResourceSlot
    {
        public float Current;
        public float Max;
        public float RegenRate;
        public float RegenDelay;
        public float LastDrainTime;
        public float DecayRate;
        public float GenerateAmount;
        public ResourceFlags Flags;
        public ResourceType Type;
        // 2 bytes padding (struct alignment) = 32 bytes total
    }
}
