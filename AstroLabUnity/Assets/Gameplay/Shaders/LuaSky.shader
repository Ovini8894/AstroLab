Shader "AstroLab/LuaSky"
{
    Properties
    {
        [NoScaleOffset] _Tex ("Céu estrelado", Cube) = "grey" {}
        _Tint ("Cor", Color) = (.5,.5,.5,1)
        _Exposure ("Exposição", Range(0,8)) = 1
        _Rotation ("Rotação", Range(0,360)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            UNITY_DECLARE_TEXCUBE(_Tex);
            half4 _Tex_HDR, _Tint;
            half _Exposure, _Rotation;
            struct Varying { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
            Varying vert(appdata_base v)
            {
                Varying o;o.position=UnityObjectToClipPos(v.vertex);
                float angle=_Rotation*UNITY_PI/180;
                o.direction=float3(cos(angle)*v.vertex.x+sin(angle)*v.vertex.z,v.vertex.y,-sin(angle)*v.vertex.x+cos(angle)*v.vertex.z);
                return o;
            }
            half4 frag(Varying i):SV_Target
            {
                float3 direction=normalize(i.direction);direction.y=abs(direction.y);
                half3 color=DecodeHDR(UNITY_SAMPLE_TEXCUBE(_Tex,direction),_Tex_HDR);
                return half4(color*_Tint.rgb*unity_ColorSpaceDouble.rgb*_Exposure,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
