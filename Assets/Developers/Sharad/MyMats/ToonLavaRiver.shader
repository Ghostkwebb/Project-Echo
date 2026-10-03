Shader "Custom/ToonLavaRiver"
{
    Properties
    {
        _BaseMap ("Fire Noise Texture", 2D) = "white" {}
        [HDR] _CoreColor ("Inner Hot Magma (HDR)", Color) = (3.0, 2.5, 0.8, 1)
        [HDR] _LavaColor ("Middle Magma (HDR)", Color) = (2.5, 0.6, 0.05, 1)
        _CrustColor ("Charred Edge Rock", Color) = (0.08, 0.06, 0.06, 1)
        _NoiseTiling ("Noise Tiling", Float) = 3.0
        _FlowSpeed ("Lava Flow Speed", Float) = 0.8
        _EdgeJaggedness ("Edge Noise Erosion", Range(0.1, 1.0)) = 0.6
        _Dissolve ("Cooldown Fade", Range(0, 1)) = 0.0

        [Header(Unity Toon Shader UTS Controls)]
        _BaseColor_Step ("Core Step", Range(0, 1)) = 0.68
        _BaseShade_Feather ("Core Feather", Range(0.0001, 0.5)) = 0.04
        _ShadeColor_Step ("Lava Step", Range(0, 1)) = 0.30
        _1st_ShadeColor_Feather ("Lava Feather", Range(0.0001, 0.5)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _CoreColor;
                float4 _LavaColor;
                float4 _CrustColor;
                float _NoiseTiling;
                float _FlowSpeed;
                float _EdgeJaggedness;
                float _Dissolve;
                float _BaseColor_Step;
                float _BaseShade_Feather;
                float _ShadeColor_Step;
                float _1st_ShadeColor_Feather;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Dual-layer flowing magma noise along UV.x
                float flow = _Time.y * _FlowSpeed;
                float2 flowUV1 = float2(input.uv.x * _NoiseTiling + flow, input.uv.y * _NoiseTiling * 0.4);
                float2 flowUV2 = float2(input.uv.x * (_NoiseTiling * 1.45) + flow * 1.3, input.uv.y * _NoiseTiling * 0.6);

                float n1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowUV1).r;
                float n2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowUV2).r;
                float noise = (n1 + n2) * 0.5;

                // 2. Normalized distance from ribbon center: 0.0 at center (UV.y=0.5), 1.0 at outer edges (UV.y=0 or 1)
                float widthDist = abs(input.uv.y - 0.5) * 2.0;

                // 3. Wide Magma Profile (Central 75% dominated by blinding core + saturated fire)
                // Flatter center falloff ensures magma stays dominant across the fissure
                float centerShape = 1.0 - pow(widthDist, 2.0);
                float rawHeat = centerShape * 0.85 + (noise - 0.5) * 0.35;
                float dissolve = saturate(_Dissolve);
                float heat = saturate(rawHeat - dissolve * 1.25);

                // 4. UTS DoubleShade Banding
                float baseFeather = max(0.0001, _BaseShade_Feather);
                float coreLevel = saturate((heat - _BaseColor_Step) / baseFeather);

                float shadeFeather = max(0.0001, _1st_ShadeColor_Feather);
                float lavaLevel = saturate((heat - _ShadeColor_Step) / shadeFeather);

                half3 finalColor = lerp(_CrustColor.rgb, _LavaColor.rgb, lavaLevel);
                finalColor = lerp(finalColor, _CoreColor.rgb, coreLevel);

                // 5. Jagged Burning-Edge Alpha Cutout
                // Uses noise erosion so the ribbon breaks organically into transparent void at edges
                float edgeFalloff = 1.0 - widthDist;
                float jaggedAlpha = (edgeFalloff * 1.5) + (noise - 0.5) * _EdgeJaggedness - (dissolve * 1.2);
                float finalAlpha = saturate(smoothstep(0.05, 0.30, jaggedAlpha));
                finalAlpha *= saturate(edgeFalloff * 12.0); // Hard cutoff strictly preventing edge bleed

                // Multiply TrailRenderer vertex color (color over lifetime fade)
                finalColor *= input.color.rgb;
                finalAlpha *= input.color.a;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
