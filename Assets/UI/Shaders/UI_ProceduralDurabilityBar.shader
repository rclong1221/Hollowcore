Shader "DIG/UI/ProceduralDurabilityBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsLow ("Is Low", Float) = 0.0
        _IsBroken ("Is Broken", Float) = 0.0
        
        _ColorFull ("Color Full", Color) = (0.7, 0.7, 0.75, 1)
        _ColorMid ("Color Mid", Color) = (0.6, 0.55, 0.4, 1)
        _ColorLow ("Color Low", Color) = (0.7, 0.4, 0.2, 1)
        _ColorBroken ("Color Broken", Color) = (0.5, 0.2, 0.15, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.1, 0.1, 0.1, 0.85)
        _BorderColor ("Border Color", Color) = (0.4, 0.4, 0.4, 1)
        _CrackColor ("Crack Color", Color) = (0.15, 0.1, 0.1, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.1
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _CrackIntensity ("Crack Intensity", Range(0, 1)) = 0.6
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
            
            float _FillAmount, _IsLow, _IsBroken;
            float4 _ColorFull, _ColorMid, _ColorLow, _ColorBroken;
            float4 _BackgroundColor, _BorderColor, _CrackColor;
            float _CornerRadius, _BorderWidth, _CrackIntensity;
            
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }
            
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            
            // Crack pattern
            float cracks(float2 uv)
            {
                float c = 0.0;
                float2 p = uv * 20.0;
                float2 cellId = floor(p);
                float2 f = frac(p);
                
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 cellPoint = hash(cellId + neighbor) * 0.5 + 0.25 + neighbor;
                        float d = length(f - cellPoint);
                        c = max(c, smoothstep(0.1, 0.0, d));
                    }
                }
                return c;
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
                float fillMask = step(uv.x, _FillAmount) * innerMask;
                
                // Color gradient
                float4 fillColor;
                if (_FillAmount > 0.5) fillColor = lerp(_ColorMid, _ColorFull, (_FillAmount - 0.5) * 2.0);
                else if (_FillAmount > 0.25) fillColor = lerp(_ColorLow, _ColorMid, (_FillAmount - 0.25) * 4.0);
                else fillColor = lerp(_ColorBroken, _ColorLow, _FillAmount * 4.0);
                
                // Crack overlay when low
                float crack = cracks(uv) * _CrackIntensity * (1.0 - _FillAmount);
                fillColor.rgb = lerp(fillColor.rgb, _CrackColor.rgb, crack * _IsLow);
                
                // Broken flicker
                float brokenFlicker = sin(_Time.y * 15.0) * 0.3 * _IsBroken;
                fillColor.rgb *= 1.0 - brokenFlicker;
                
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
