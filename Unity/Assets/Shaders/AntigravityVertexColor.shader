Shader "DortCuce/AntigravityVertexColor"
{
    Properties
    {
        _Color ("Fallback / Tint", Color) = (1,1,1,1)
        _UseVertexColor ("Use Vertex Color", Range(0,1)) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        half _UseVertexColor;
        half _Glossiness;

        struct Input
        {
            float4 color : COLOR;
        };

        void surf(Input IN, inout SurfaceOutputStandard output)
        {
            fixed3 authored = lerp(_Color.rgb, IN.color.rgb, _UseVertexColor);
            output.Albedo = authored;
            output.Metallic = 0;
            output.Smoothness = _Glossiness;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
