Shader "DIG/UI/FrostedGlass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.1, 0.1, 0.15, 0.85)
        _BlurSize ("Blur Size", Range(0, 10)) = 3.0
        _BorderColor ("Border Color", Color) = (1, 1, 1, 0.1)
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _BorderRadius ("Border Radius", Range(0, 0.5)) = 0.1
        _NoiseScale ("Noise Scale", Range(0, 100)) = 50
        _NoiseStrength ("Noise Strength", Range(0, 0.1)) = 0.02
        
        // Stencil for UI
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
            "PreviewType" = "Plane"
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
        
        // Grab the screen behind the object
        GrabPass { "_GrabTexture" }
        
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
                float4 grabPos : TEXCOORD1;
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize;
            float4 _TintColor;
            float _BlurSize;
            float4 _BorderColor;
            float _BorderWidth;
            float _BorderRadius;
            float _NoiseScale;
            float _NoiseStrength;
            
            // Simple noise function
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            
            // Rounded rectangle SDF
            float roundedRectSDF(float2 p, float2 size, float radius)
            {
                float2 q = abs(p) - size + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.color = v.color;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.grabPos.xy / i.grabPos.w;
                
                // Add subtle noise distortion for frosted look
                float2 noiseOffset = float2(
                    noise(i.uv * _NoiseScale + _Time.y * 0.5),
                    noise(i.uv * _NoiseScale + 100 + _Time.y * 0.5)
                ) * _NoiseStrength;
                
                // Blur sampling (box blur approximation)
                float4 blur = float4(0, 0, 0, 0);
                float2 texelSize = _GrabTexture_TexelSize.xy * _BlurSize;
                
                // 9-sample blur
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2(-1, -1) * texelSize);
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2( 0, -1) * texelSize);
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2( 1, -1) * texelSize);
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2(-1,  0) * texelSize);
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2( 0,  0) * texelSize);
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2( 1,  0) * texelSize);
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2(-1,  1) * texelSize);
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2( 0,  1) * texelSize);
                blur += tex2D(_GrabTexture, uv + noiseOffset + float2( 1,  1) * texelSize);
                blur /= 9.0;
                
                // Apply tint
                float4 color = lerp(blur, _TintColor, _TintColor.a);
                color.a = _TintColor.a;
                
                // Rounded rectangle mask
                float2 rectUV = i.uv - 0.5;
                float dist = roundedRectSDF(rectUV, float2(0.5, 0.5), _BorderRadius);
                
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
