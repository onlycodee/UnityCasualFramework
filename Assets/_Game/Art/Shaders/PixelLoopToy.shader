// Pixel Loop 3D: glossy "toy plastic" look for voxels, shooters and the stage (Built-in render pipeline).
// Colour = _Color × vertex colour, with a soft rim light and an additive emission used for hit flashes.
// _Color and _Emission are per-instance so MaterialPropertyBlocks keep GPU instancing.
Shader "PixelLoop/Toy"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Emission ("Emission", Color) = (0,0,0,0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.55
        _Rim ("Rim strength", Range(0,2)) = 0.45
        _RimPower ("Rim power", Range(0.5,8)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0

        struct Input
        {
            float3 viewDir;
            float3 worldNormal;
            float4 color : COLOR;
        };

        half _Glossiness;
        half _Rim;
        half _RimPower;

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Emission)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = UNITY_ACCESS_INSTANCED_PROP(Props, _Color) * IN.color;
            fixed4 e = UNITY_ACCESS_INSTANCED_PROP(Props, _Emission);
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal)));
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Emission = c.rgb * pow(rim, _RimPower) * _Rim + e.rgb * e.a;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
