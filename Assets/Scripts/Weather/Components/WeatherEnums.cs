namespace DIG.Weather
{
    public enum WeatherType : byte
    {
        Clear = 0,
        PartlyCloudy = 1,
        Cloudy = 2,
        LightRain = 3,
        HeavyRain = 4,
        Thunderstorm = 5,
        LightSnow = 6,
        HeavySnow = 7,
        Fog = 8,
        Sandstorm = 9
    }

    public enum Season : byte
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    public enum TimeOfDayPeriod : byte
    {
        Night = 0,       // 0:00 - 5:00
        Dawn = 1,        // 5:00 - 7:00
        Morning = 2,     // 7:00 - 10:00
        Midday = 3,      // 10:00 - 14:00
        Afternoon = 4,   // 14:00 - 17:00
        Dusk = 5,        // 17:00 - 19:00
        Evening = 6,     // 19:00 - 22:00
        LateNight = 7    // 22:00 - 0:00
    }
}
