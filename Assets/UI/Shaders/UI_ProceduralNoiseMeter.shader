Shader "DIG/UI/ProceduralNoiseMeter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.0
        _IsLoud ("Is Loud", Float) = 0.0
        
        _ColorQuiet ("Color Quiet", Color) = (0.3, 0.5, 0.3, 1)
        _ColorModerate ("Color Moderate", Color) = (0.7, 0.7, 0.3, 1)
        _ColorLoud ("Color Loud", Color) = (0.9, 0.4, 0.2, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.08, 0.08, 0.08, 0.85)
        _BorderColor ("Border Color", Color) = (0.35, 0.35, 0.35, 1)
        
        _BarCount ("Bar Count", Range(5, 20)) = 10
        _BarGap ("Bar Gap", Range(0, 0.1)) = 0.02
        _WaveSpeed ("Wave Speed", Range(0, 10)) = 5.0
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
            
            float _FillAmount, _IsLoud;
            float4 _ColorQuiet, _ColorModerate, _ColorLoud;
            float4 _BackgroundColor, _BorderColor;
            float _BarCount, _BarGap, _WaveSpeed;
            
            float hash(float n) { return frac(sin(n) * 43758.5453); }
            
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
                
                // Vertical bars like audio meter
                int barIndex = (int)(uv.x * _BarCount);
                float barLocalX = frac(uv.x * _BarCount);
                
                // Gap between bars
                float barMask = step(_BarGap, barLocalX) * step(barLocalX, 1.0 - _BarGap);
                
                // Each bar has slight random height variation
                float barNoise = hash(barIndex + floor(_Time.y * _WaveSpeed)) * 0.2;
                float barThreshold = (float)barIndex / _BarCount;
                
                // Is this bar active?
                float isActive = step(barThreshold, _FillAmount + barNoise * _FillAmount);
                
                // Bar height varies with activity
                float barHeight = 0.3 + isActive * 0.5 + sin(_Time.y * _WaveSpeed + barIndex) * 0.1 * isActive;
                float heightMask = step(abs(uv.y - 0.5), barHeight * 0.5);
                
                // Color based on position (left = quiet, right = loud)
                float4 barColor;
                if (barThreshold < 0.4) barColor = _ColorQuiet;
                else if (barThreshold < 0.7) barColor = lerp(_ColorQuiet, _ColorModerate, (barThreshold - 0.4) / 0.3);
                else barColor = lerp(_ColorModerate, _ColorLoud, (barThreshold - 0.7) / 0.3);
                
                // Dim inactive bars
                barColor.rgb *= lerp(0.3, 1.0, isActive);
                
                // Loud pulse
                float loudPulse = sin(_Time.y * 10.0) * 0.2 * _IsLoud;
                barColor.rgb += loudPulse * _ColorLoud.rgb;
                
                float4 finalColor = lerp(_BackgroundColor, barColor, barMask * heightMask);
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
