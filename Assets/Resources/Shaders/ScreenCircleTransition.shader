Shader "Anchor/UI/ScreenCircleTransition"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _HoleCenter ("Hole Center", Vector) = (0.5, 0.5, 0, 0)
        _HoleRadius ("Hole Radius", Float) = 0
        _Softness ("Softness", Float) = 0.025
        _Aspect ("Aspect", Float) = 1.7777778
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _HoleCenter;
            float _HoleRadius;
            float _Softness;
            float _Aspect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 delta = i.uv - _HoleCenter.xy;
                delta.x *= _Aspect;

                float distanceFromCenter = length(delta);
                float softness = max(_Softness, 0.0001);
                float outsideCircle = smoothstep(_HoleRadius, _HoleRadius + softness, distanceFromCenter);

                fixed4 color = _Color * i.color;
                color.a *= outsideCircle;
                return color;
            }
            ENDCG
        }
    }

    FallBack Off
}
