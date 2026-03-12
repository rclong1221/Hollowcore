Shader "DIG/UI/ProceduralChargesBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CurrentCharges ("Current Charges", Float) = 3.0
        _MaxCharges ("Max Charges", Float) = 3.0
        _RechargeProgress ("Recharge Progress", Range(0, 1)) = 0.0
        
        _ColorFull ("Color Full", Color) = (0.4, 0.7, 1.0, 1)
        _ColorEmpty ("Color Empty", Color) = (0.2, 0.2, 0.25, 0.6)
        _ColorRecharge ("Color Recharge", Color) = (0.5, 0.6, 0.8, 0.8)
        
        _BackgroundColor ("Background Color", Color) = (0.08, 0.08, 0.1, 0.8)
        _BorderColor ("Border Color", Color) = (0.35, 0.35, 0.4, 1)
        
        _SegmentGap ("Segment Gap", Range(0, 0.1)) = 0.03
        _CornerRadius ("Corner Radius", Range(0, 0.3)) = 0.1
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
            
            float _CurrentCharges, _MaxCharges, _RechargeProgress;
            float4 _ColorFull, _ColorEmpty, _ColorRecharge;
            float4 _BackgroundColor, _BorderColor;
            float _SegmentGap, _CornerRadius, _GlowIntensity;
            
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
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
                
                int maxCharges = (int)_MaxCharges;
                int currentCharges = (int)_CurrentCharges;
                
                float segmentWidth = 1.0 / _MaxCharges;
                int segmentIndex = (int)(uv.x / segmentWidth);
                float segmentLocalX = frac(uv.x * _MaxCharges);
                
                // Gap between segments
                float gapMask = step(_SegmentGap, segmentLocalX) * step(segmentLocalX, 1.0 - _SegmentGap);
                float verticalMask = step(0.15, uv.y) * step(uv.y, 0.85);
                float segmentMask = gapMask * verticalMask;
                
                // Determine segment state
                bool isFull = segmentIndex < currentCharges;
                bool isRecharging = segmentIndex == currentCharges && _RechargeProgress > 0.0;
                
                float4 segmentColor;
                if (isFull)
                {
                    segmentColor = _ColorFull;
                    // Pulse effect on full charges
                    float pulse = sin(_Time.y * 2.0 + segmentIndex * 0.5) * 0.1 + 0.9;
                    segmentColor.rgb *= pulse;
                }
                else if (isRecharging)
                {
                    // Partial fill for recharging segment
                    float rechargeFill = step(segmentLocalX - _SegmentGap, _RechargeProgress * (1.0 - _SegmentGap * 2.0));
                    segmentColor = lerp(_ColorEmpty, _ColorRecharge, rechargeFill);
                }
                else
                {
                    segmentColor = _ColorEmpty;
                }
                
                // Glow on full charges
                float glow = _GlowIntensity * 0.3 * (isFull ? 1.0 : 0.0);
                segmentColor.rgb += glow;
                
                float4 finalColor = lerp(_BackgroundColor, segmentColor, segmentMask);
                
                // Border
                float borderDist = abs(uv.y - 0.5) - 0.35;
                float borderMask = step(borderDist, 0.02) * step(-0.02, borderDist);
                finalColor = lerp(finalColor, _BorderColor, borderMask * 0.5);
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
