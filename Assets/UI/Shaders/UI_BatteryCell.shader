Shader "DIG/UI/BatteryCell"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _CellCount ("Cell Count", Range(1, 20)) = 10
        _CellGap ("Cell Gap", Range(0, 0.1)) = 0.02
        
        // Colors
        _ColorFull ("Color Full", Color) = (0.2, 0.8, 1, 1)
        _ColorLow ("Color Low", Color) = (1, 0.6, 0.2, 1)
        _ColorEmpty ("Color Empty/Off", Color) = (0.2, 0.2, 0.2, 0.5)
        _ColorCritical ("Color Critical", Color) = (1, 0.2, 0.2, 1)
        
        // State
        _IsOn ("Is On", Range(0, 1)) = 1
        _IsLowBattery ("Is Low Battery", Range(0, 1)) = 0
        _IsFlickering ("Is Flickering", Range(0, 1)) = 0
        _FlickerSpeed ("Flicker Speed", Range(1, 50)) = 15
        
        // Visual
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.5
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.15
        _BackgroundColor ("Background Color", Color) = (0, 0, 0, 0.6)
        _BorderColor ("Border Color", Color) = (1, 1, 1, 0.15)
        _BorderWidth ("Border Width", Range(0, 0.05)) = 0.01
        
        // Stencil
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            float _FillAmount;
            float _CellCount;
            float _CellGap;
            float4 _ColorFull;
            float4 _ColorLow;
            float4 _ColorEmpty;
            float4 _ColorCritical;
            float _IsOn;
            float _IsLowBattery;
            float _IsFlickering;
            float _FlickerSpeed;
            float _GlowIntensity;
            float _CornerRadius;
            float4 _BackgroundColor;
            float4 _BorderColor;
            float _BorderWidth;
            
            float roundedRectSDF(float2 p, float2 size, float radius)
            {
                float2 q = abs(p) - size + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }
            
            // Pseudo-random for flicker
            float hash(float n) { return frac(sin(n) * 43758.5453); }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 rectUV = uv - 0.5;
                
                // Outer rounded rectangle
                float dist = roundedRectSDF(rectUV, float2(0.5, 0.5), _CornerRadius);
                
                // Background
                float4 color = _BackgroundColor;
                
                // Calculate which cell we're in
                float cellWidth = 1.0 / _CellCount;
                float cellIndex = floor(uv.x / cellWidth);
                float cellLocalX = frac(uv.x / cellWidth);
                
                // Cell padding (create gaps between cells)
                float cellPadding = _CellGap / cellWidth;
                float inCell = step(cellPadding, cellLocalX) * step(cellLocalX, 1.0 - cellPadding);
                
                // Vertical padding
                float vertPadding = 0.15;
                float inCellVert = step(vertPadding, uv.y) * step(uv.y, 1.0 - vertPadding);
                inCell *= inCellVert;
                
                // Check if this cell should be filled
                float filledCells = ceil(_FillAmount * _CellCount);
                float isFilled = step(cellIndex, filledCells - 1) * step(0, _FillAmount - 0.001);
                
                // Get fill color based on state
                float4 fillColor = _ColorFull;
                if (_IsLowBattery > 0.5)
                    fillColor = lerp(_ColorLow, _ColorCritical, _FillAmount < 0.1 ? 1 : 0);
                
                // Dim when off
                fillColor.rgb *= lerp(0.3, 1.0, _IsOn);
                
                // Flicker effect
                if (_IsFlickering > 0.5)
                {
                    float flicker1 = sin(_Time.y * _FlickerSpeed * 7.3);
                    float flicker2 = sin(_Time.y * _FlickerSpeed * 13.7);
                    float flicker3 = sin(_Time.y * _FlickerSpeed * 23.1);
                    float flickerAmount = (flicker1 + flicker2 * 0.5 + flicker3 * 0.25) / 1.75;
                    flickerAmount = flickerAmount * 0.5 + 0.5; // 0 to 1
                    fillColor.a *= lerp(0.3, 1.0, flickerAmount);
                }
                
                // Apply cell color
                float4 cellColor = lerp(_ColorEmpty, fillColor, isFilled);
                
                // Add subtle glow to filled cells
                if (isFilled > 0.5)
                {
                    float glow = (1.0 - abs(cellLocalX - 0.5) * 2.0) * _GlowIntensity;
                    cellColor.rgb += fillColor.rgb * glow * 0.3;
                }
                
                // Combine
                color = lerp(color, cellColor, inCell * step(dist, 0));
                
                // Border
                float border = smoothstep(0.0, -0.01, dist) - smoothstep(-_BorderWidth, -_BorderWidth - 0.01, dist);
                color = lerp(color, _BorderColor, border * _BorderColor.a);
                
                // Alpha mask
                color.a *= smoothstep(0.01, -0.01, dist);
                
                return color * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "UI/Default"
}
