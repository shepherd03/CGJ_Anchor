// =============================================================================
//  FireOutlinePost.shader
//  Screen-space outline + fire distortion post effect for the built-in 2D
//  pipeline (NOT URP). Consumes a mask RenderTexture (the target objects
//  rendered to alpha by a secondary camera) and composites an animated,
//  noise-distorted fire outline onto the screen.
//
//  Used by: SpineFireOutline.cs  (attached to the main camera)
// =============================================================================
Shader "Hidden/FireOutlinePost"
{
    Properties
    {
        _MainTex        ("Screen",          2D) = "" {}
        _MaskTex        ("Mask",            2D) = "" {}
        _FireColorTex   ("Fire Color Ramp", 2D) = "white" {}
        _OutlineWidth   ("Outline Width",   Float) = 3
        _OutlineColor   ("Outline Core Color", Color) = (0.55, 0.08, 0.0, 1)
        _OutlineHardness("Outline Core Hardness", Range(0,1)) = 0.8
        _DistortStrength("Distortion Strength",  Float) = 0.012
        _FireSpeed      ("Fire Speed",      Float) = 1.5
        _FireScale      ("Fire Noise Scale", Float) = 6.0
        _FireIntensity  ("Fire Intensity",  Range(0,3)) = 1.2
        _FireTint       ("Fire Tint",       Color) = (1, 0.6, 0.2, 1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;        float4 _MainTex_TexelSize;
            sampler2D _MaskTex;        float4 _MaskTex_TexelSize;
            sampler2D _FireColorTex;
            float  _OutlineWidth;
            float  _OutlineHardness;
            float  _DistortStrength;
            float  _FireSpeed;
            float  _FireScale;
            float  _FireIntensity;
            fixed4 _OutlineColor;
            fixed4 _FireTint;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 uvMask : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos    = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                o.uvMask = v.uv;
            #if UNITY_UV_STARTS_AT_TOP
                // The mask RT is rendered by a normal camera (top-origin on D3D);
                // flip its V so it aligns with the screen source.
                o.uvMask.y = 1.0 - o.uvMask.y;
            #endif
                return o;
            }

            // ---- cheap value noise -------------------------------------------------
            float rand(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = rand(i);
                float b = rand(i + float2(1.0, 0.0));
                float c = rand(i + float2(0.0, 1.0));
                float d = rand(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // sample mask alpha at uv with offset
            #define MASK(o) tex2D(_MaskTex, uvM + (o)).a

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv  = i.uv;
                float2 uvM = i.uvMask;
                float2 te  = _MaskTex_TexelSize.xy;

                // ---- animated wobble so the outline breathes like flame -------------
                float t  = _Time.y * _FireSpeed;
                float n1 = noise(uvM * _FireScale            + float2(0.0, -t * 2.0));
                float n2 = noise(uvM * (_FireScale * 1.7)     + float2(13.7, -t * 1.3));
                float2 wob = float2(n1 - 0.5, n2 - 0.5) * _DistortStrength;
                uvM += wob;

                // ---- outline band: cross + diagonals over a range of radii ---------
                int   W   = (int)clamp(_OutlineWidth, 1.0, 8.0);
                float m   = step(0.5, tex2D(_MaskTex, uvM).a);   // 1 = solid, 0 = empty
                float outer = 0.0;   // outside the object but within W texels of the edge
                float inner = 0.0;   // inside  the object but within W texels of the edge

                [loop]
                for (int r = 1; r <= W; r++)
                {
                    float2 e = te * r;
                    float sR  = MASK( float2( e.x, 0.0));
                    float sL  = MASK( float2(-e.x, 0.0));
                    float sU  = MASK( float2(0.0,  e.y));
                    float sD  = MASK( float2(0.0, -e.y));
                    float sUR = MASK( float2( e.x,  e.y));
                    float sUL = MASK( float2(-e.x,  e.y));
                    float sDR = MASK( float2( e.x, -e.y));
                    float sDL = MASK( float2(-e.x, -e.y));

                    float mx = max(max(max(sR, sL), max(sU, sD)), max(max(sUR, sUL), max(sDR, sDL)));
                    float mn = min(min(min(sR, sL), min(sU, sD)), min(min(sUR, sUL), min(sDR, sDL)));

                    if (m  < 0.5 && step(0.5, mx) > 0.5) outer = 1.0;
                    if (m  > 0.5 && step(0.5, mn) < 0.5) inner = 1.0;
                }

                float band = saturate(outer + inner);

                // ---- fire field: flicker that drifts upward -------------------------
                float flicker = n1 * 0.6 + n2 * 0.4;
                float fireShape = saturate(flicker);                          // 0..1
                float fireAlpha = smoothstep(0.25, 0.9, fireShape) * band;    // only on the band
                // ramp the color along U of the fire texture (red -> orange -> yellow -> white)
                float2 fireUV = float2(frac(fireShape * 1.2 + t * 0.5), 0.5);
                fixed4 fireCol = tex2D(_FireColorTex, fireUV) * _FireTint;
                fireCol.a *= fireAlpha;

                // ---- composite over the screen -------------------------------------
                fixed4 screen = tex2D(_MainTex, uv);
                float3 col = screen.rgb;

                // dark/orange core line beneath the fire so the flame reads against any bg
                col = lerp(col, _OutlineColor.rgb, band * _OutlineHardness);

                // additive fire glow
                col += fireCol.rgb * fireCol.a * _FireIntensity * 2.0;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}

