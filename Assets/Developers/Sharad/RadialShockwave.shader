Shader "Custom/RadialShockwave"
{
    Properties
    {
        [Header(Cel Shaded Color Bands)]
        [HDR] _CoreColor ("Inner Core Band (Hot HDR)", Color) = (3.0, 2.5, 1.5, 1.0)
        [HDR] _BaseColor ("Main Energy Band (HDR)", Color) = (1.0, 0.45, 0.05, 1.0)
        [HDR] _RimColor  ("Outer Rim Band (HDR)", Color) = (0.8, 0.1, 0.0, 1.0)

        [Header(Band Layout)]
        _CorePosition ("Core Band Center", Range(0.1, 0.9)) = 0.55
        _CoreWidth    ("Core Band Width", Range(0.01, 0.5)) = 0.15
        _RimWidth     ("Outer Rim Width", Range(0.01, 0.5)) = 0.22

        [Header(Hard Ring Geometry Edges)]
        _InnerEdge ("Inner Edge Cutoff", Range(0.0, 0.5)) = 0.15
        _OuterEdge ("Outer Edge Cutoff", Range(0.5, 1.0)) = 0.85
        _EdgeSmoothing ("Anti-Aliasing Sharpness", Range(0.001, 0.05)) = 0.006

        [Header(Anime Noise and Serration)]
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseSpeed ("Noise Scroll Speed", Float) = 2.0
        _NoiseStrength ("Jagged Serration", Range(0, 1)) = 0.25

        [Header(Anime Dissolve)]
        _Dissolve ("Erosion / Cutoff", Range(0, 1)) = 0.0
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

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _CoreColor;
                half4  _BaseColor;
                half4  _RimColor;
                float4 _NoiseTex_ST;
                float  _CorePosition;
                float  _CoreWidth;
                float  _RimWidth;
                float  _InnerEdge;
                float  _OuterEdge;
                float  _EdgeSmoothing;
                float  _NoiseSpeed;
                float  _NoiseStrength;
                float  _Dissolve;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                half4  color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                half4  color        : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Anti-aliasing threshold window (provides sharp cel edges without pixel shimmering)
                float aa = max(_EdgeSmoothing, 0.001);

                // 2. Sample scrolling noise texture across time along radial expansion axis
                float2 noiseUV = TRANSFORM_TEX(input.uv, _NoiseTex) + float2(0.0, -_NoiseSpeed * _Time.y);
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                // 3. Apply noise distortion to radial UV.y coordinate (produces jagged anime energy teeth)
                float distortedUVy = input.uv.y + (noise - 0.5) * _NoiseStrength;

                // 4. Hard-edged inner & outer geometry cutoffs (Dragon Ball / Guilty Gear style ring silhouette)
                float innerMask = smoothstep(_InnerEdge - aa, _InnerEdge + aa, distortedUVy);
                float outerMask = 1.0 - smoothstep(_OuterEdge - aa, _OuterEdge + aa, distortedUVy);
                float ringMask = saturate(innerMask * outerMask);

                // 5. Normalized position across visible ring width (0.0 = inner boundary, 1.0 = outer boundary)
                float ringSpan = max(_OuterEdge - _InnerEdge, 0.0001);
                float t = saturate((distortedUVy - _InnerEdge) / ringSpan);

                // 6. Stepped Cel-Shaded Bands
                // Outer Rim Band (crisp border line along the leading edge)
                float rimStep = smoothstep(1.0 - _RimWidth - aa, 1.0 - _RimWidth + aa, t);

                // Hot Core Band (intense blast center line that blooms)
                float distToCore = abs(t - _CorePosition);
                float coreStep = 1.0 - smoothstep(_CoreWidth * 0.5 - aa, _CoreWidth * 0.5 + aa, distToCore);

                // Compose hard-edged stepped color bands
                half3 bandColor = _BaseColor.rgb;
                bandColor = lerp(bandColor, _RimColor.rgb, rimStep);
                bandColor = lerp(bandColor, _CoreColor.rgb, coreStep);

                // 7. Anime Dissolve / Erosion (eats ring into anime energy shreds over lifetime)
                float dissolveProgress = saturate(_Dissolve + (1.0 - input.color.a));
                float erosionMask = smoothstep(dissolveProgress - aa, dissolveProgress + aa, noise);

                // 8. Multiply with vertex color (RGB) and vertex alpha / ring mask (Alpha)
                half3 finalRGB = bandColor * input.color.rgb;
                half finalAlpha = _BaseColor.a * ringMask * erosionMask * input.color.a;

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
