Shader "Custom/RoomGlowBreath"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BreathSpeed ("Breath Speed", Range(0, 4)) = 0.9
        _BreathAmount ("Breath Amount", Range(0, 0.5)) = 0.28
        _Brightness ("Brightness", Range(0, 2)) = 1.18
        _Phase ("Phase", Range(0, 1)) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _BreathSpeed;
                float _BreathAmount;
                float _Brightness;
                float _Phase;
            CBUFFER_END
            half4 _RendererColor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color * _RendererColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float wave = 0.5 + 0.5 * sin((_Time.y * _BreathSpeed + _Phase) * 6.2831853);
                float pulse = lerp(1.0 - _BreathAmount, 1.0 + _BreathAmount, wave);

                tex *= input.color * _Color;
                tex.rgb *= _Brightness * pulse;
                tex.a = saturate(tex.a * pulse);
                return tex;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _BreathSpeed;
                float _BreathAmount;
                float _Brightness;
                float _Phase;
            CBUFFER_END
            half4 _RendererColor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color * _RendererColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float wave = 0.5 + 0.5 * sin((_Time.y * _BreathSpeed + _Phase) * 6.2831853);
                float pulse = lerp(1.0 - _BreathAmount, 1.0 + _BreathAmount, wave);

                tex *= input.color * _Color;
                tex.rgb *= _Brightness * pulse;
                tex.a = saturate(tex.a * pulse);
                return tex;
            }
            ENDHLSL
        }
    }
}
