Shader "DIG/UI/ProceduralInfectionBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.0
        _IsSpreading ("Is Spreading", Float) = 0.0
        _IsCritical ("Is Critical", Float) = 0.0
        
        _ColorClean ("Color Clean", Color) = (0.7, 0.85, 0.7, 1)
        _ColorMild ("Color Mild", Color) = (0.6, 0.75, 0.3, 1)
        _ColorModerate ("Color Moderate", Color) = (0.5, 0.6, 0.2, 1)
        _ColorCritical ("Color Critical", Color) = (0.3, 0.5, 0.1, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.08, 0.1, 0.05, 0.85)
        _BorderColor ("Border Color", Color) = (0.4, 0.5, 0.3, 1)
        _VeinColor ("Vein Color", Color) = (0.2, 0.4, 0.1, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.12
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.018
        _VeinIntensity ("Vein Intensity", Range(0, 1)) = 0.5
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
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
            
            float _FillAmount, _IsSpreading, _IsCritical;
            float4 _ColorClean, _ColorMild, _ColorModerate, _ColorCritical;
            float4 _BackgroundColor, _BorderColor, _VeinColor;
            float _CornerRadius, _BorderWidth, _VeinIntensity, _PulseSpeed;
            
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }
            
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            
            // Organic vein pattern
            float veins(float2 uv, float time)
            {
                float v = 0.0;
                float2 p = uv * 15.0;
                for (int i = 0; i < 3; i++)
                {
                    float t = time * 0.5 + float(i) * 1.5;
                    float2 dir = float2(cos(t), sin(t * 0.7));
                    v += abs(sin(dot(p, dir) * 2.0 + t)) * 0.3;
                }
                return saturate(1.0 - v);
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
                float2 centeredUV = uv - 0.5;
                
                float2 size = float2(0.5 - _CornerRadius * 0.125, 0.5 - _CornerRadius);
                float distOuter = sdRoundedBox(centeredUV, size, _CornerRadius);
                float distInner = sdRoundedBox(centeredUV, size - _BorderWidth, _CornerRadius - _BorderWidth * 0.5);
                
                float pixelWidth = fwidth(distOuter);
                float outerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distOuter);
                float innerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distInner);
                float borderMask = outerMask * (1.0 - innerMask);
                
                // Infection fills from right to left (inverted)
                float fillMask = step(1.0 - uv.x, _FillAmount) * innerMask;
                
                // Color gradient (more infected = greener/sicker)
                float4 fillColor;
                if (_FillAmount < 0.3) fillColor = lerp(_ColorClean, _ColorMild, _FillAmount / 0.3);
                else if (_FillAmount < 0.6) fillColor = lerp(_ColorMild, _ColorModerate, (_FillAmount - 0.3) / 0.3);
                else fillColor = lerp(_ColorModerate, _ColorCritical, (_FillAmount - 0.6) / 0.4);
                
                // Vein overlay
                float vein = veins(uv, _Time.y) * _VeinIntensity * _FillAmount;
                fillColor.rgb = lerp(fillColor.rgb, _VeinColor.rgb, vein * fillMask);
                
                // Spreading pulse
                float pulse = sin(_Time.y * _PulseSpeed) * 0.2 + 0.8;
                fillColor.rgb *= lerp(1.0, pulse, _IsSpreading);
                
                // Critical throb
                float throb = sin(_Time.y * _PulseSpeed * 2.0) * 0.3 + 0.7;
                fillColor.rgb *= lerp(1.0, throb, _IsCritical);
                
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
