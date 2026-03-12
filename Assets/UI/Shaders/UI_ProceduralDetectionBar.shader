Shader "DIG/UI/ProceduralDetectionBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.0
        _IsAlerted ("Is Alerted", Float) = 0.0
        _IsSpotted ("Is Spotted", Float) = 0.0
        
        _ColorHidden ("Color Hidden", Color) = (0.3, 0.5, 0.3, 1)
        _ColorAlerted ("Color Alerted", Color) = (0.9, 0.7, 0.2, 1)
        _ColorSpotted ("Color Spotted", Color) = (0.95, 0.2, 0.2, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.05, 0.05, 0.05, 0.9)
        _EyeColor ("Eye Color", Color) = (0.1, 0.1, 0.1, 1)
        _PupilColor ("Pupil Color", Color) = (0.0, 0.0, 0.0, 1)
        
        _EyeOpenAmount ("Eye Open Amount", Range(0, 1)) = 0.0
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
            
            float _FillAmount, _IsAlerted, _IsSpotted;
            float4 _ColorHidden, _ColorAlerted, _ColorSpotted;
            float4 _BackgroundColor, _EyeColor, _PupilColor;
            float _EyeOpenAmount;
            
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
                
                // Eye shape (almond/ellipse that opens based on detection)
                float eyeOpen = _FillAmount; // 0 = closed, 1 = fully open
                
                // Outer eye shape
                float2 eyeScale = float2(1.0, 0.5);
                float eyeDist = length(uv * eyeScale);
                float eyeMask = smoothstep(0.4, 0.38, eyeDist);
                
                // Eyelid (closes from top and bottom)
                float lidClosed = 1.0 - eyeOpen;
                float upperLid = smoothstep(0.0, 0.2, uv.y + 0.25 * lidClosed);
                float lowerLid = smoothstep(0.0, 0.2, -uv.y + 0.25 * lidClosed);
                float lidMask = upperLid * lowerLid;
                
                // Iris
                float irisDist = length(uv * float2(1.2, 1.0));
                float irisMask = smoothstep(0.2, 0.18, irisDist) * lidMask;
                
                // Pupil (dilates when spotted)
                float pupilSize = lerp(0.08, 0.12, _IsSpotted);
                float pupilDist = length(uv * float2(1.2, 1.0));
                float pupilMask = smoothstep(pupilSize, pupilSize - 0.02, pupilDist) * lidMask;
                
                // Color based on detection state
                float4 eyeballColor = _EyeColor;
                float4 irisColor;
                if (_IsSpotted > 0.5) irisColor = _ColorSpotted;
                else if (_IsAlerted > 0.5) irisColor = _ColorAlerted;
                else irisColor = _ColorHidden;
                
                // Spotted pulse
                float spottedPulse = sin(_Time.y * 12.0) * 0.3 * _IsSpotted;
                irisColor.rgb += spottedPulse;
                
                // Compose eye
                float4 finalColor = _BackgroundColor;
                finalColor = lerp(finalColor, eyeballColor, eyeMask * lidMask);
                finalColor = lerp(finalColor, irisColor, irisMask);
                finalColor = lerp(finalColor, _PupilColor, pupilMask);
                
                // Highlight
                float2 highlightPos = uv - float2(-0.05, 0.05);
                float highlight = smoothstep(0.04, 0.02, length(highlightPos)) * lidMask * eyeOpen;
                finalColor.rgb += highlight * 0.5;
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
