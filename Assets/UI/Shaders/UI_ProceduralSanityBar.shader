Shader "DIG/UI/ProceduralSanityBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsUnstable ("Is Unstable", Float) = 0.0
        _IsInsane ("Is Insane", Float) = 0.0
        
        _ColorFull ("Color Full (Sane)", Color) = (0.8, 0.85, 0.9, 1)
        _ColorMid ("Color Mid", Color) = (0.6, 0.5, 0.7, 1)
        _ColorLow ("Color Low (Unstable)", Color) = (0.5, 0.2, 0.5, 1)
        _ColorInsane ("Color Insane", Color) = (0.3, 0.1, 0.4, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.08, 0.05, 0.1, 0.9)
        _BorderColor ("Border Color", Color) = (0.4, 0.3, 0.5, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.12
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _DistortionIntensity ("Distortion Intensity", Range(0, 0.1)) = 0.03
        _NoiseScale ("Noise Scale", Range(1, 20)) = 8.0
        _DesaturationAmount ("Desaturation", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };
            
            float _FillAmount, _IsUnstable, _IsInsane;
            float4 _ColorFull, _ColorMid, _ColorLow, _ColorInsane;
            float4 _BackgroundColor, _BorderColor;
            float _CornerRadius, _BorderWidth, _DistortionIntensity, _NoiseScale, _DesaturationAmount;
            
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }
            
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                           lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                
                // Distortion when insane
                float insanity = 1.0 - _FillAmount;
                float2 distort = float2(
                    noise(uv * _NoiseScale + _Time.y * 2.0),
                    noise(uv * _NoiseScale + 100.0 + _Time.y * 2.0)
                ) - 0.5;
                uv += distort * _DistortionIntensity * insanity;
                
                float2 centeredUV = uv - 0.5;
                
                float2 size = float2(0.5 - _CornerRadius * 0.125, 0.5 - _CornerRadius);
                float distOuter = sdRoundedBox(centeredUV, size, _CornerRadius);
                float distInner = sdRoundedBox(centeredUV, size - _BorderWidth, _CornerRadius - _BorderWidth * 0.5);
                
                float pixelWidth = fwidth(distOuter);
                float outerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distOuter);
                float innerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distInner);
                float borderMask = outerMask * (1.0 - innerMask);
                float fillMask = step(i.uv.x, _FillAmount) * innerMask;
                
                // Color gradient
                float4 fillColor;
                if (_FillAmount > 0.6) fillColor = lerp(_ColorMid, _ColorFull, (_FillAmount - 0.6) / 0.4);
                else if (_FillAmount > 0.3) fillColor = lerp(_ColorLow, _ColorMid, (_FillAmount - 0.3) / 0.3);
                else fillColor = lerp(_ColorInsane, _ColorLow, _FillAmount / 0.3);
                
                // Desaturation as sanity drops
                float grey = dot(fillColor.rgb, float3(0.299, 0.587, 0.114));
                fillColor.rgb = lerp(fillColor.rgb, float3(grey, grey, grey), _DesaturationAmount * insanity);
                
                // Flickering when unstable
                float flicker = noise(float2(_Time.y * 10.0, 0)) * _IsUnstable * 0.3;
                fillColor.rgb *= 1.0 - flicker;
                
                float4 finalColor = float4(0, 0, 0, 0);
                finalColor = lerp(finalColor, _BackgroundColor, innerMask * (1.0 - fillMask) * _BackgroundColor.a);
                finalColor = lerp(finalColor, fillColor, fillMask);
                finalColor = lerp(finalColor, _BorderColor, borderMask);
                finalColor.a *= outerMask;
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
