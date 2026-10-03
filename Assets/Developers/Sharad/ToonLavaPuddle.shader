Shader "Custom/ToonLavaPuddle"
{
    Properties
    {
        _BaseMap ("Fire Noise Texture", 2D) = "white" {}
        [HDR] _CoreColor ("Inner Hot Magma (HDR)", Color) = (2.5, 2.0, 0.5, 1)
        [HDR] _LavaColor ("Middle Lava (HDR)", Color) = (2.0, 0.4, 0.05, 1)
        _CrustColor ("Outer Charred Crust", Color) = (0.1, 0.08, 0.08, 1)
        _NoiseTiling ("Noise Tiling", Float) = 2.0
        _NoiseScrollSpeed ("Lava Flow Speed", Float) = 0.5
        _Dissolve ("Cooldown / Dissolve", Range(0, 1)) = 0.0

        [Header(Unity Toon Shader UTS Double Shade Controls)]
        _BaseColor_Step ("Base Color (Core) Step", Range(0, 1)) = 0.65
        _BaseShade_Feather ("Base/Shade Feather", Range(0.0001, 1)) = 0.05
        _ShadeColor_Step ("1st Shade (Lava) Step", Range(0, 1)) = 0.35
        _1st_ShadeColor_Feather ("1st/2nd Shade Feather", Range(0.0001, 1)) = 0.05
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
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
                float _NoiseScrollSpeed;
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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Dual-layer scrolling noise for dynamic churning liquid lava
                float time = _Time.y * _NoiseScrollSpeed;
                float2 uv1 = input.uv * _NoiseTiling + float2(time * 0.15, time * 0.10);
                float2 uv2 = input.uv * (_NoiseTiling * 1.35) + float2(-time * 0.10, time * 0.12);

                float noise1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1).r;
                float noise2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv2).r;
                float noise = (noise1 + noise2) * 0.5;

                // 2. Circular Radial Mask (distance from UV center [0, 1])
                // Center = 0.0, Inscribed circle = 1.0, Corners ≈ 1.414
                float dist = length(input.uv - 0.5) * 2.0;

                // Modulate radial boundary with noise to form an organic blob puddle
                float edgeWarp = (noise - 0.5) * 0.25;
                float organicRadius = dist + edgeWarp;

                // Smoothstep falloff ensuring the blob never touches the square quad edges
                float blobMask = 1.0 - smoothstep(0.70, 0.92, organicRadius);
                blobMask *= (1.0 - smoothstep(0.92, 0.98, dist)); // Strict cutoff before dist reaches 1.0

                // 3. Heat Calculation & Cooldown logic
                // Center is hot; noise adds churning temperature variation
                float rawHeat = (1.0 - dist * 0.65) * 0.5 + noise * 0.5;
                float dissolve = saturate(_Dissolve);

                // Cooling: as _Dissolve increases, temperature drops into charred crust
                float heat = saturate(rawHeat - dissolve * 1.25);

                // 4. Unity Toon Shader (UTS) DoubleShadeWithFeather System
                // Step defines threshold; Feather controls edge softness
                // Core (_CoreColor) = Base Color
                // Lava (_LavaColor) = 1st Shade Color
                // Crust (_CrustColor) = 2nd Shade Color
                float baseFeather = max(0.0001, _BaseShade_Feather);
                float baseColor_Step_level = saturate((heat - _BaseColor_Step) / baseFeather);

                float shadeFeather = max(0.0001, _1st_ShadeColor_Feather);
                float shadeColor_Step_level = saturate((heat - _ShadeColor_Step) / shadeFeather);

                // UTS Layered Color Blending: 2nd Shade (Crust) -> 1st Shade (Lava) -> Base Color (Core)
                half3 finalColor = lerp(_CrustColor.rgb, _LavaColor.rgb, shadeColor_Step_level);
                finalColor = lerp(finalColor, _CoreColor.rgb, baseColor_Step_level);

                // 5. Alpha Erosion
                // As _Dissolve approaches 1, puddle erodes from edges inward to 0
                float alphaErosion = blobMask - dissolve * 1.15;
                half finalAlpha = half(smoothstep(0.01, 0.15, alphaErosion));

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
