Shader "DIG/UI/ProceduralInteractionBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.0
        _IsActive ("Is Active", Float) = 0.0
        
        _ColorProgress ("Color Progress", Color) = (0.3, 0.7, 1.0, 1)
        _ColorComplete ("Color Complete", Color) = (0.3, 0.9, 0.4, 1)
        _ColorBackground ("Color Background", Color) = (0.15, 0.15, 0.18, 0.9)
        _ColorBorder ("Color Border", Color) = (0.4, 0.4, 0.45, 1)
        
        _Radius ("Radius", Range(0.1, 0.5)) = 0.35
        _Thickness ("Thickness", Range(0.02, 0.15)) = 0.06
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.5
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
            
            float _FillAmount, _IsActive;
            float4 _ColorProgress, _ColorComplete, _ColorBackground, _ColorBorder;
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
                
                // Circular progress (top, clockwise)
                float angle = atan2(uv.x, uv.y);
                float normalizedAngle = (angle + PI) / (2.0 * PI);
                
                float dist = length(uv);
                
                // Ring mask
                float outerEdge = smoothstep(_Radius + 0.01, _Radius, dist);
                float innerEdge = smoothstep(_Radius - _Thickness - 0.01, _Radius - _Thickness, dist);
                float ringMask = outerEdge * innerEdge;
                
                // Progress fill
                float progressMask = step(normalizedAngle, _FillAmount) * ringMask;
                float bgMask = ringMask * (1.0 - step(normalizedAngle, _FillAmount));
                
                // Color
                float4 progressColor = lerp(_ColorProgress, _ColorComplete, _FillAmount);
                
                // Pulse when active
                float pulse = sin(_Time.y * 6.0) * 0.15 + 0.85;
                progressColor.rgb *= lerp(1.0, pulse, _IsActive);
                
                // Complete flash
                float isComplete = step(0.99, _FillAmount);
                float completeFlash = sin(_Time.y * 10.0) * 0.3 + 0.7;
                progressColor.rgb += isComplete * completeFlash * 0.3;
                
                // Glow
                float glow = saturate(1.0 - abs(dist - _Radius + _Thickness * 0.5) * 15.0) * _GlowIntensity * _FillAmount;
                
                float4 finalColor = float4(0, 0, 0, 0);
                finalColor = lerp(finalColor, _ColorBackground, bgMask * _ColorBackground.a);
                finalColor = lerp(finalColor, progressColor, progressMask);
                finalColor.rgb += glow * progressColor.rgb * _IsActive;
                
                // Border ring
                float borderDist = abs(dist - _Radius + _Thickness * 0.5);
                float borderMask = smoothstep(0.015, 0.005, abs(borderDist - _Thickness * 0.5)) * ringMask;
                finalColor = lerp(finalColor, _ColorBorder, borderMask * 0.5);
                
                // Only visible when active or has progress
                finalColor.a *= max(_IsActive, step(0.01, _FillAmount));
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
