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

            // 坑-032：`const float PI = …` 在移动端（gles3/vulkan）编译器报 syntax error，
            // 编辑器 D3D11 不暴露——shader 代码必须跑一次真机级编译验证
            #define PI 3.14159265

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
                float u = 0.5 + atan2(IN.dirWS.x, IN.dirWS.z) / (2.0 * PI);
                float v = 0.5 - asin(clamp(IN.dirWS.y, -1.0, 1.0)) / PI;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(u, v));
            }
            ENDHLSL
        }
    }
}
