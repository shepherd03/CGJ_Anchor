Shader "Custom/ScreenSpaceEdgeFlame"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _CoreColor ("Core Color", Color) = (1, 0.12, 0.01, 1)
        _TipColor ("Tip Color", Color) = (1, 0.82, 0.06, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.5)) = 0.075
        _TailLength ("Tail Length", Range(0, 2)) = 0.42
        _TailDirection ("Tail Direction", Vector) = (0, 1, 0, 0)
        _NoiseScale ("Noise Scale", Range(0.1, 64)) = 9
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.72
        _FlowSpeed ("Flow Speed", Range(-10, 10)) = 2.4
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.035
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _TipColor;
                float4 _SpriteUVRect;
                float4 _SpriteBounds;
                float4 _TailDirection;
                float _OutlineWidth;
                float _TailLength;
                float _NoiseScale;
                float _NoiseAmount;
                float _FlowSpeed;
                float _AlphaCutoff;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 positionOS : TEXCOORD0; };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + 1), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll] for (int octave = 0; octave < 3; octave++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = p * 2.03 + 17.17;
                    amplitude *= 0.5;
                }
                return value;
            }

            float SampleMask(float2 localPosition)
            {
                float2 size = max(_SpriteBounds.zw, float2(0.0001, 0.0001));
                float2 spriteUV = (localPosition - _SpriteBounds.xy) / size + 0.5;
                float inside = step(0.0, spriteUV.x) * step(spriteUV.x, 1.0)
                             * step(0.0, spriteUV.y) * step(spriteUV.y, 1.0);
                float2 atlasUV = _SpriteUVRect.xy + spriteUV * _SpriteUVRect.zw;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV).a * inside;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionOS = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 direction = normalize(_TailDirection.xy + float2(0.00001, 0));
                float2 side = float2(-direction.y, direction.x);
                float2 flowUV = float2(dot(input.positionOS, side), dot(input.positionOS, direction));
                float noise = Fbm(flowUV * _NoiseScale - float2(0, _Time.y * _FlowSpeed));
                float detail = Fbm(flowUV * (_NoiseScale * 1.83) + float2(31.7, -_Time.y * _FlowSpeed * 1.37));
                float lateralWarp = (noise - 0.5) * _OutlineWidth * (1.0 + _NoiseAmount * 2.5);
                float2 warped = input.positionOS + side * lateralWarp;

                float originalMask = SampleMask(input.positionOS);
                float outlineMask = 0.0;
                const float d = 0.70710678;
                outlineMask = max(outlineMask, SampleMask(warped + float2( 1, 0) * _OutlineWidth));
                outlineMask = max(outlineMask, SampleMask(warped + float2(-1, 0) * _OutlineWidth));
                outlineMask = max(outlineMask, SampleMask(warped + float2(0,  1) * _OutlineWidth));
                outlineMask = max(outlineMask, SampleMask(warped + float2(0, -1) * _OutlineWidth));
                outlineMask = max(outlineMask, SampleMask(warped + float2( d,  d) * _OutlineWidth));
                outlineMask = max(outlineMask, SampleMask(warped + float2(-d,  d) * _OutlineWidth));
                outlineMask = max(outlineMask, SampleMask(warped + float2( d, -d) * _OutlineWidth));
                outlineMask = max(outlineMask, SampleMask(warped + float2(-d, -d) * _OutlineWidth));

                float tailReach = _TailLength * (0.28 + lerp(1.0, saturate(noise * 1.35), _NoiseAmount) * 0.72);
                float tailMask = 0.0;
                float tailPhase = 0.0;
                [unroll] for (int sampleIndex = 1; sampleIndex <= 8; sampleIndex++)
                {
                    float t = sampleIndex / 8.0;
                    float wave = sin(t * 9.0 + noise * 8.0 - _Time.y * _FlowSpeed) * _OutlineWidth * _NoiseAmount;
                    float2 samplePosition = warped - direction * (tailReach * t + _OutlineWidth) + side * wave;
                    float sampleAlpha = SampleMask(samplePosition);
                    tailMask = max(tailMask, sampleAlpha);
                    tailPhase = max(tailPhase, sampleAlpha * t);
                }

                float flameMask = saturate(max(outlineMask, tailMask) - originalMask);
                float breakup = smoothstep(0.12, 0.72, detail + (1.0 - tailPhase) * 0.28);
                flameMask *= lerp(1.0, breakup, _NoiseAmount * 0.8);
                clip(flameMask - _AlphaCutoff);

                half4 color = lerp(_CoreColor, _TipColor, saturate(tailPhase + noise * 0.18));
                color.rgb *= 1.0 + detail * 0.35;
                color.a *= flameMask;
                return color;
            }
            ENDHLSL
        }
    }
}
