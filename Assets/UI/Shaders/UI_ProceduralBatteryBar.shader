Shader "DIG/UI/ProceduralBatteryBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsOn ("Is On", Float) = 1.0
        _IsFlickering ("Is Flickering", Float) = 0.0
        _IsLowBattery ("Is Low Battery", Float) = 0.0
        
        _ColorFull ("Color Full", Color) = (0.4, 0.8, 1.0, 1)
        _ColorMid ("Color Mid", Color) = (0.6, 0.6, 0.2, 1)
        _ColorLow ("Color Low", Color) = (0.9, 0.4, 0.1, 1)
        _ColorOff ("Color Off", Color) = (0.2, 0.2, 0.25, 0.6)
        
        _BackgroundColor ("Background Color", Color) = (0.08, 0.08, 0.1, 0.9)
        _BorderColor ("Border Color", Color) = (0.3, 0.3, 0.35, 1)
        _CellGapColor ("Cell Gap Color", Color) = (0.05, 0.05, 0.07, 1)
        
        _CellCount ("Cell Count", Range(1, 20)) = 10
        _CellGap ("Cell Gap", Range(0, 0.1)) = 0.015
        _CornerRadius ("Corner Radius", Range(0, 0.3)) = 0.1
        _BorderWidth ("Border Width", Range(0, 0.05)) = 0.015
        
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.6
        _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 8.0
        _SmoothFill ("Smooth Fill (0=Chunky, 1=Smooth)", Range(0, 1)) = 0.0
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
            float _IsOn;
            float _IsFlickering;
            float _IsLowBattery;
            
            float4 _ColorFull;
            float4 _ColorMid;
            float4 _ColorLow;
            float4 _ColorOff;
            float4 _BackgroundColor;
            float4 _BorderColor;
            float4 _CellGapColor;
            
            float _CellCount;
            float _CellGap;
            float _CornerRadius;
            float _BorderWidth;
            float _GlowIntensity;
            float _FlickerSpeed;
            float _SmoothFill;
            
            // Pseudo-random for flicker
            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // SDF for rounded box
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
                float2 centeredUV = uv - 0.5;
                
                // Outer shell SDF
                float2 outerSize = float2(0.48, 0.48);
                float distOuter = sdRoundedBox(centeredUV, outerSize, _CornerRadius);
                float distInner = sdRoundedBox(centeredUV, outerSize - _BorderWidth, _CornerRadius - _BorderWidth * 0.5);
                
                float pixelWidth = fwidth(distOuter);
                float outerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distOuter);
                float innerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distInner);
                float borderMask = outerMask * (1.0 - innerMask);
                
                // Calculate which cell this pixel is in
                float cellWidth = (1.0 - _CellGap * (_CellCount + 1)) / _CellCount;
                float cellStart = _CellGap;
                
                int cellIndex = -1;
                float inCell = 0.0;
                
                for (int c = 0; c < 20; c++)
                {
                    if (c >= (int)_CellCount) break;
                    
                    float cStart = cellStart + c * (cellWidth + _CellGap);
                    float cEnd = cStart + cellWidth;
                    
                    if (uv.x >= cStart && uv.x <= cEnd)
                    {
                        cellIndex = c;
                        
                        // Check if within cell's vertical bounds (with padding)
                        float vPadding = 0.15;
                        if (uv.y >= vPadding && uv.y <= 1.0 - vPadding)
                        {
                            inCell = 1.0;
                        }
                        break;
                    }
                }
                
                // Determine if cell is filled
                // Chunky mode: cell is either full or empty
                // Smooth mode: cell can be partially filled
                float cellFillThreshold = (float)(cellIndex + 1) / _CellCount;
                float cellStartThreshold = (float)cellIndex / _CellCount;
                
                // Chunky fill: cell is full if fill >= cell threshold
                float chunkyFill = step(cellFillThreshold, _FillAmount + 0.001);
                
                // Smooth fill: cell fills progressively
                float cellLocalFill = saturate((_FillAmount - cellStartThreshold) / (cellFillThreshold - cellStartThreshold));
                float smoothFill = cellLocalFill;
                
                // Blend between modes
                float isFilled = lerp(chunkyFill, smoothFill, _SmoothFill) * step(0, cellIndex);
                
                // Cell color based on fill level
                float4 cellColor;
                if (_FillAmount > 0.5)
                {
                    cellColor = lerp(_ColorMid, _ColorFull, (_FillAmount - 0.5) * 2.0);
                }
                else if (_FillAmount > 0.25)
                {
                    cellColor = lerp(_ColorLow, _ColorMid, (_FillAmount - 0.25) * 4.0);
                }
                else
                {
                    cellColor = _ColorLow;
                }
                
                // Apply on/off state
                if (_IsOn < 0.5)
                {
                    cellColor = _ColorOff;
                    isFilled = inCell; // Show all cells as "off"
                }
                
                // Flickering effect
                if (_IsFlickering > 0.5 && _IsOn > 0.5)
                {
                    float flicker = sin(_Time.y * _FlickerSpeed) * 
                                    sin(_Time.y * _FlickerSpeed * 1.7) * 
                                    sin(_Time.y * _FlickerSpeed * 2.3);
                    flicker = flicker * 0.5 + 0.5;
                    
                    // Random per-cell flicker for low battery
                    if (_IsLowBattery > 0.5)
                    {
                        float cellFlicker = rand(float2(cellIndex, floor(_Time.y * 5.0)));
                        flicker *= step(0.3, cellFlicker);
                    }
                    
                    cellColor.rgb *= 0.5 + flicker * 0.5;
                    cellColor.a *= 0.7 + flicker * 0.3;
                }
                
                // Low battery pulse
                if (_IsLowBattery > 0.5 && _IsOn > 0.5)
                {
                    float pulse = sin(_Time.y * 4.0) * 0.5 + 0.5;
                    cellColor = lerp(cellColor, _ColorLow, pulse * 0.3);
                }
                
                // Cell inner glow/highlight
                float cellHighlight = 0.0;
                if (inCell > 0.5 && isFilled > 0.5)
                {
                    // Top highlight
                    cellHighlight = smoothstep(0.7, 0.85, uv.y) * 0.2;
                    // Bottom shadow
                    float bottomShadow = smoothstep(0.3, 0.15, uv.y) * 0.15;
                    cellColor.rgb += cellHighlight;
                    cellColor.rgb -= bottomShadow;
                }
                
                // Build final color
                float4 result = float4(0, 0, 0, 0);
                
                // Background
                result = lerp(result, _BackgroundColor, innerMask);
                
                // Cell gaps (darker than background)
                result = lerp(result, _CellGapColor, innerMask * (1.0 - inCell));
                
                // Filled cells
                float cellMask = inCell * isFilled * innerMask;
                result = lerp(result, cellColor, cellMask);
                
                // Empty cells (dim)
                float emptyCellMask = inCell * (1.0 - isFilled) * innerMask * _IsOn;
                float4 emptyColor = _ColorOff * 0.5;
                result = lerp(result, emptyColor, emptyCellMask);
                
                // Glow from filled cells
                if (_IsOn > 0.5)
                {
                    float glowMask = exp(-distInner * 15.0) * _GlowIntensity * _FillAmount;
                    result.rgb += cellColor.rgb * glowMask * 0.3;
                }
                
                // Border
                float4 borderColorFinal = _BorderColor;
                if (_IsLowBattery > 0.5 && _IsOn > 0.5)
                {
                    float pulse = sin(_Time.y * 4.0) * 0.5 + 0.5;
                    borderColorFinal = lerp(_BorderColor, _ColorLow, pulse * 0.4);
                }
                result = lerp(result, borderColorFinal, borderMask);
                
                // Outer glow
                if (_IsOn > 0.5)
                {
                    float outerGlow = exp(-distOuter * 25.0) * _GlowIntensity * 0.4 * _FillAmount;
                    outerGlow *= (1.0 - outerMask);
                    result.rgb += cellColor.rgb * outerGlow;
                    result.a = max(result.a, outerGlow * 0.3);
                }
                
                return result;
            }
            ENDCG
        }
    }
}
