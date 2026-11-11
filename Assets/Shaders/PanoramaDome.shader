Shader "MemoryCorridor/PanoramaDome"
{
    Properties
    {
        _MainTex ("Panorama (Equirectangular)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+100" }
        Cull Front
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dirWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dirWS = normalize(TransformObjectToWorld(IN.positionOS.xyz));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                const float PI = 3.14159265;
                float u = 0.5 + atan2(IN.dirWS.x, IN.dirWS.z) / (2.0 * PI);
                float v = 0.5 - asin(clamp(IN.dirWS.y, -1.0, 1.0)) / PI;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(u, v));
            }
            ENDHLSL
        }
    }
}
