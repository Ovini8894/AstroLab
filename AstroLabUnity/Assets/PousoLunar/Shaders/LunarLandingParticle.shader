Shader "AstroLab/PousoLunar/Particle"
{
    Properties
    {
        [HDR] _BaseColor("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.color=input.color*_BaseColor; o.uv=input.uv; return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                half alpha=saturate(1-dot(p,p));
                return half4(input.color.rgb,input.color.a*alpha*alpha);
            }
            ENDHLSL
        }
    }
}
