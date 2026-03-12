namespace DIG.VFX
{
    /// <summary>
    /// EPIC 16.7: Well-known VFX type IDs for code references.
    /// Matches entries in VFXTypeDatabase. Systems use these constants for type-safe references.
    /// </summary>
    public static class VFXTypeIds
    {
        // ─── Combat ───
        public const int BulletImpactDefault = 1000;
        public const int BulletImpactMetal = 1001;
        public const int BulletImpactDirt = 1002;
        public const int BulletImpactFlesh = 1003;
        public const int BulletImpactWater = 1004;
        public const int MuzzleFlashRifle = 1010;
        public const int MuzzleFlashPistol = 1011;
        public const int MuzzleFlashShotgun = 1012;
        public const int ProjectileTrailDefault = 1020;
        public const int ProjectileTrailFire = 1021;
        public const int ProjectileTrailIce = 1022;

        // ─── Ability / Elemental ───
        public const int AbilityFireBurst = 2000;
        public const int AbilityIceBurst = 2001;
        public const int AbilityLightningStrike = 2002;
        public const int AbilityPoisonCloud = 2003;
        public const int AbilityHolySmite = 2004;
        public const int AbilityShadowBlast = 2005;
        public const int AbilityArcanePulse = 2006;
        public const int BuffApply = 2100;
        public const int DebuffApply = 2101;

        // ─── Death ───
        public const int DeathBloodSplatter = 3000;
        public const int DeathGibExplosion = 3001;
        public const int DeathDissolve = 3002;
        public const int DeathSoulRelease = 3003;

        // ─── Environment ───
        public const int FootstepDust = 4000;
        public const int FootstepWater = 4001;
        public const int WaterSplashSmall = 4010;
        public const int WaterSplashLarge = 4011;

        // ─── Interaction / UI ───
        public const int LootGlow = 5000;
        public const int PickupFlash = 5001;
        public const int InteractionComplete = 5002;
        public const int LevelUp = 5003;

        // ─── Ambient ───
        public const int AmbientDust = 6000;
        public const int AmbientFireflies = 6001;
        public const int AmbientEmber = 6002;
    }
}
