Shader "DIG/UI/ProceduralStaminaBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsDraining ("Is Draining", Float) = 0.0
        _IsRecovering ("Is Recovering", Float) = 0.0
        _IsEmpty ("Is Empty", Float) = 0.0
        
        _ColorFull ("Color Full", Color) = (0.95, 0.77, 0.06, 1)
        _ColorMid ("Color Mid", Color) = (0.85, 0.65, 0.05, 1)
        _ColorLow ("Color Low", Color) = (0.75, 0.45, 0.05, 1)
        _ColorEmpty ("Color Empty", Color) = (0.4, 0.3, 0.1, 0.5)
        _ColorRecovery ("Color Recovery", Color) = (0.6, 0.9, 0.3, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.1, 0.08, 0.05, 0.85)
        _BorderColor ("Border Color", Color) = (0.5, 0.4, 0.2, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.12
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.018
        
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.4
        _ShineSpeed ("Shine Speed", Range(0, 5)) = 2.0
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 4.0
        _SmoothFill ("Smooth Fill (0=Chunky, 1=Smooth)", Range(0, 1)) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float _FillAmount;
            float _IsDraining;
            float _IsRecovering;
            float _IsEmpty;
            
            float4 _ColorFull;
            float4 _ColorMid;
            float4 _ColorLow;
            float4 _ColorEmpty;
            float4 _ColorRecovery;
            float4 _BackgroundColor;
            float4 _BorderColor;
            
            float _CornerRadius;
            float _BorderWidth;
            float _GlowIntensity;
            float _ShineSpeed;
            float _PulseSpeed;
            float _SmoothFill;
            
            // Signed distance function for rounded rectangle
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }
            
            // Hash for noise
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453);
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
                
                // Aspect ratio for proper rounded corners (assuming 8:1 ratio)
                float2 size = float2(0.5 - _CornerRadius * 0.125, 0.5 - _CornerRadius);
                float cornerR = _CornerRadius;
                
                // Calculate distances
                float distOuter = sdRoundedBox(centeredUV, size, cornerR);
                float distInner = sdRoundedBox(centeredUV, size - _BorderWidth, cornerR - _BorderWidth * 0.5);
                
                // Anti-aliased edges
                float pixelWidth = fwidth(distOuter);
                float outerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distOuter);
                float innerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distInner);
                float borderMask = outerMask * (1.0 - innerMask);
                
                // Fill mask based on smooth fill mode
                float fillEdge = lerp(_FillAmount, _FillAmount, _SmoothFill);
                float fillMask = step(uv.x, fillEdge) * innerMask;
                
                // Calculate fill color based on fill amount with smooth gradients
                float4 fillColor;
                if (_FillAmount > 0.6)
                {
                    fillColor = lerp(_ColorMid, _ColorFull, (_FillAmount - 0.6) / 0.4);
                }
                else if (_FillAmount > 0.3)
                {
                    fillColor = lerp(_ColorLow, _ColorMid, (_FillAmount - 0.3) / 0.3);
                }
                else
                {
                    fillColor = lerp(_ColorEmpty, _ColorLow, _FillAmount / 0.3);
                }
                
                // Override with recovery color when recovering
                float recoveryPulse = sin(_Time.y * _PulseSpeed * 2.0) * 0.3 + 0.7;
                fillColor = lerp(fillColor, _ColorRecovery * recoveryPulse, _IsRecovering * 0.6);
                
                // Add horizontal gradient for depth
                float depthGradient = lerp(0.85, 1.15, uv.y);
                fillColor.rgb *= depthGradient;
                
                // Add shine effect (sweeping highlight)
                float shinePos = frac(_Time.y * _ShineSpeed * 0.2);
                float shine = saturate(1.0 - abs(uv.x - shinePos) * 8.0);
                shine *= smoothstep(0.0, 0.2, uv.y) * smoothstep(1.0, 0.6, uv.y);
                shine *= (1.0 - _IsDraining) * (1.0 - _IsEmpty); // No shine when draining or empty
                fillColor.rgb += shine * 0.3;
                
                // Draining pulse effect (subtle warning when using stamina)
                float drainPulse = sin(_Time.y * _PulseSpeed) * 0.15 + 0.85;
                fillColor.rgb *= lerp(1.0, drainPulse, _IsDraining);
                
                // Empty pulse (more urgent when fully depleted)
                float emptyPulse = sin(_Time.y * _PulseSpeed * 1.5) * 0.25 + 0.75;
                fillColor.rgb *= lerp(1.0, emptyPulse, _IsEmpty);
                
                // Inner glow near the fill edge
                float edgeDist = abs(uv.x - _FillAmount);
                float edgeGlow = saturate(1.0 - edgeDist * 15.0) * _GlowIntensity * 0.5;
                edgeGlow *= innerMask * step(uv.x, _FillAmount + 0.05);
                
                // Background behind the fill
                float bgMask = innerMask * (1.0 - fillMask);
                
                // Border with subtle glow
                float borderGlow = saturate(1.0 - distOuter * 8.0) * _GlowIntensity * 0.3;
                float4 borderColor = _BorderColor;
                borderColor.rgb += borderGlow * fillColor.rgb;
                
                // Compose final color
                float4 finalColor = float4(0, 0, 0, 0);
                
                // Background layer
                finalColor = lerp(finalColor, _BackgroundColor, bgMask * _BackgroundColor.a);
                
                // Fill layer
                finalColor = lerp(finalColor, fillColor, fillMask);
                
                // Edge glow layer
                finalColor.rgb += edgeGlow * fillColor.rgb;
                
                // Border layer
                finalColor = lerp(finalColor, borderColor, borderMask);
                
                // Overall alpha
                finalColor.a *= outerMask;
                
                return finalColor;
            }
            ENDCG
        }
    }
    
    FallBack "UI/Default"
}
