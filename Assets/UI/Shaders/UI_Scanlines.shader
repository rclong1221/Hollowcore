Shader "DIG/UI/Scanlines"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScanlineColor ("Scanline Color", Color) = (0, 0, 0, 0.1)
        _ScanlineCount ("Scanline Count", Range(10, 500)) = 100
        _ScanlineSpeed ("Scanline Speed", Range(0, 10)) = 0.5
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.3
        _NoiseIntensity ("Noise Intensity", Range(0, 0.2)) = 0.02
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.3
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.1)) = 0.02
        
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
            "Queue" = "Transparent+100" 
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
            float4 _ScanlineColor;
            float _ScanlineCount;
            float _ScanlineSpeed;
            float _ScanlineIntensity;
            float _NoiseIntensity;
            float _VignetteIntensity;
            float _FlickerIntensity;
            
            // Hash function for noise
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            
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
                
                // Scrolling scanlines
                float scanline = sin((uv.y + _Time.y * _ScanlineSpeed) * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                scanline = pow(scanline, 2.0) * _ScanlineIntensity;
                
                // Film grain noise
                float noise = hash(uv * 1000.0 + _Time.y * 100.0) * _NoiseIntensity;
                
                // Vignette (darker at edges)
                float2 vignetteUV = uv - 0.5;
                float vignette = 1.0 - dot(vignetteUV, vignetteUV) * _VignetteIntensity * 2.0;
                vignette = saturate(vignette);
                
                // Random flicker
                float flicker = 1.0 - hash(float2(_Time.y * 10.0, 0)) * _FlickerIntensity;
                
                // Combine effects
                float4 color = _ScanlineColor;
                color.a = (scanline + noise) * (1.0 - vignette * 0.5);
                color.rgb *= flicker;
                
                return color * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "UI/Default"
}
