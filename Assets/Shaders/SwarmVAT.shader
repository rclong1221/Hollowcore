Shader "DIG/SwarmVAT"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PositionVAT ("Position VAT", 2D) = "black" {}
        _NormalVAT ("Normal VAT", 2D) = "bump" {}
        _VATFrameCount ("Total VAT Frames", Float) = 92
        _VATVertexCount ("Vertex Count", Float) = 1000
        _BoundsMin ("VAT Bounds Min", Vector) = (-1,-1,-1,0)
        _BoundsMax ("VAT Bounds Max", Vector) = (1,2,1,0)
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma multi_compile_instancing
        #pragma target 3.5

        sampler2D _MainTex;
        sampler2D _PositionVAT;
        sampler2D _NormalVAT;

        float _VATFrameCount;
        float _VATVertexCount;
        float4 _BoundsMin;
        float4 _BoundsMax;
        fixed4 _Color;
        half _Metallic;
        half _Smoothness;

        // Per-instance animation params set by SwarmRenderSystem via MaterialPropertyBlock
        // x = clipIndex, y = animTime (0-1), z = startFrame, w = frameCount
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _SwarmAnimParams)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct Input
        {
            float2 uv_MainTex;
        };

        void vert(inout appdata_full v)
        {
            #ifdef UNITY_INSTANCING_ENABLED
            float4 animParams = UNITY_ACCESS_INSTANCED_PROP(Props, _SwarmAnimParams);
            float startFrame = animParams.z;
            float frameCount = animParams.w;
            float animTime = animParams.y;

            if (frameCount > 0 && _VATVertexCount > 0)
            {
                // Calculate current frame
                float currentFrame = startFrame + animTime * (frameCount - 1);

                // UV into VAT texture
                // U = vertex index / vertex count
                // V = frame / total frames
                float u = (v.vertex.x * 0.5 + 0.5); // Remap vertex index via texcoord1 if available
                float vid = v.texcoord1.x; // Use second UV channel for vertex ID
                if (vid > 0)
                    u = vid / _VATVertexCount;

                float vCoord = currentFrame / _VATFrameCount;

                // Sample position from VAT
                float4 vatPos = tex2Dlod(_PositionVAT, float4(u, vCoord, 0, 0));

                // Decode from [0,1] to world bounds
                float3 decodedPos = lerp(_BoundsMin.xyz, _BoundsMax.xyz, vatPos.xyz);

                v.vertex.xyz = decodedPos;

                // Sample normal from VAT
                float4 vatNormal = tex2Dlod(_NormalVAT, float4(u, vCoord, 0, 0));
                v.normal = vatNormal.xyz * 2.0 - 1.0;
            }
            #endif
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = c.a;
        }
        ENDCG
    }

    // Fallback for non-instancing: standard diffuse
    FallBack "Diffuse"
}
