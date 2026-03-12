Shader "DIG/UI/ProceduralCooldownBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount (Ready)", Range(0, 1)) = 1.0
        _IsReady ("Is Ready", Float) = 1.0
        
        _ColorReady ("Color Ready", Color) = (0.3, 0.9, 0.4, 1)
        _ColorCooldown ("Color Cooldown", Color) = (0.4, 0.4, 0.5, 0.8)
        _ColorFlash ("Color Flash", Color) = (1.0, 1.0, 1.0, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.1, 0.1, 0.12, 0.8)
        _BorderColor ("Border Color", Color) = (0.4, 0.4, 0.45, 1)
        
        _Radius ("Radius", Range(0.1, 0.5)) = 0.4
        _Thickness ("Thickness", Range(0.02, 0.2)) = 0.08
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.6
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
            
            float _FillAmount, _IsReady;
            float4 _ColorReady, _ColorCooldown, _ColorFlash;
            float4 _BackgroundColor, _BorderColor;
            float _Radius, _Thickness, _GlowIntensity;
            
            #define PI 3.14159265
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv - 0.5;
                
                // Circular cooldown (sweeps from top clockwise)
                float angle = atan2(uv.x, uv.y); // Top = 0
                float normalizedAngle = (angle + PI) / (2.0 * PI); // 0-1
                
                float dist = length(uv);
                float ringMask = smoothstep(_Radius + 0.01, _Radius, dist) * smoothstep(_Radius - _Thickness - 0.01, _Radius - _Thickness, dist);
                
                // Fill based on cooldown
                float fillMask = step(normalizedAngle, _FillAmount) * ringMask;
                float bgMask = ringMask * (1.0 - step(normalizedAngle, _FillAmount));
                
                float4 fillColor = lerp(_ColorCooldown, _ColorReady, _FillAmount);
                
                // Ready flash
                float flash = sin(_Time.y * 8.0) * 0.3 + 0.7;
                fillColor = lerp(fillColor, _ColorFlash, _IsReady * flash * 0.3);
                
                // Glow when ready
                float glow = saturate(1.0 - abs(dist - _Radius + _Thickness * 0.5) * 10.0) * _GlowIntensity * _IsReady;
                
                float4 finalColor = float4(0, 0, 0, 0);
                finalColor = lerp(finalColor, _BackgroundColor, bgMask * _BackgroundColor.a);
                finalColor = lerp(finalColor, fillColor, fillMask);
                finalColor.rgb += glow * _ColorReady.rgb;
                
                // Center dot
                float centerDot = smoothstep(0.05, 0.03, dist);
                finalColor = lerp(finalColor, fillColor, centerDot);
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
