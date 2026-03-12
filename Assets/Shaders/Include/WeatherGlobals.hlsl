#ifndef WEATHER_GLOBALS_INCLUDED
#define WEATHER_GLOBALS_INCLUDED

// Global weather properties set by WeatherShaderSystem every frame.
// All default to 0.0 when the system is not running.
// Include this file in any shader that needs weather data.

float _TimeOfDay;           // 0.0 - 23.99
float _NormalizedTime;      // 0.0 - 1.0 (TimeOfDay / 24)
float _RainIntensity;       // 0.0 - 1.0
float _SnowIntensity;       // 0.0 - 1.0
float _FogDensity;          // 0.0 - 1.0
float _WindDirectionX;      // -1.0 to 1.0
float _WindDirectionY;      // -1.0 to 1.0
float _WindSpeed;           // 0.0 - 30.0 m/s
float _Temperature;         // Celsius
float _WeatherTransition;   // 0.0 - 1.0 (current transition progress)

#endif // WEATHER_GLOBALS_INCLUDED
