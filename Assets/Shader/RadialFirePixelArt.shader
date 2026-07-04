Shader "Custom/EdgeFlowFire"
{
    Properties
    {
        _MainTex        ("精灵纹理 Sprite Texture", 2D)            = "white" {}
        _EdgeWidth      ("边缘宽度 Edge Width",      Range(0.001, 0.1)) = 0.008
        _NoiseScale     ("噪声缩放 Noise Scale",     Float)             = 8.0
        _FlowSpeed      ("流动速度 Flow Speed",      Float)             = 1.5
        _WindDir        ("风向 Wind Direction",       Vector)            = (1,0,0,0)
        _WindStrength   ("风力 Wind Strength",        Range(0,2))        = 0.5
        _FireIntensity  ("火焰强度 Fire Intensity",   Range(0,3))        = 1.2
        _CoreGlow       ("核心辉光 Core Glow",        Range(0,3))        = 1.5
        _Color1         ("颜色1 Color 1",             Color)             = (0.5, 0.0, 0.1, 1)
        _Color2         ("颜色2 Color 2",             Color)             = (0.9, 0.1, 0.0, 1)
        _Color3         ("颜色3 Color 3",             Color)             = (0.2, 0.1, 0.7, 1)
        _Color4         ("颜色4 Color 4",             Color)             = (1.0, 0.9, 0.1, 1)
        _Color5         ("颜色5 Color 5",             Color)             = (0.1, 0.1, 0.1, 1)
        _Color6         ("颜色6 Color 6",             Color)             = (0.9, 0.9, 0.9, 1)
        _PixelSize      ("像素尺寸 Pixel Size",       Float)             = 128.0
    }

    SubShader
    {
        // -----------------------------------------------------------
        // 渲染队列：透明；关闭深度写入；双面渲染
        // -----------------------------------------------------------
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target   3.0
            #pragma vertex   vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // -------------------------------------------------------
            // 属性声明
            // -------------------------------------------------------
            sampler2D _MainTex;
            float4    _MainTex_ST;

            float  _EdgeWidth;
            float  _NoiseScale;
            float  _FlowSpeed;
            float4 _WindDir;
            float  _WindStrength;
            float  _FireIntensity;
            float  _CoreGlow;

            fixed4 _Color1;
            fixed4 _Color2;
            fixed4 _Color3;
            fixed4 _Color4;
            fixed4 _Color5;
            fixed4 _Color6;

            float  _PixelSize;

            // -------------------------------------------------------
            // 顶点输入 / 输出结构体
            // -------------------------------------------------------
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            // -------------------------------------------------------
            // 顶点着色器
            // 手动 UV 变换，不使用 TRANSFORM_TEX 宏
            // -------------------------------------------------------
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 手动应用 Tiling 和 Offset
                o.uv  = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            // -------------------------------------------------------
            // 噪声函数：rand — 伪随机哈希（Shadertoy 风格 HLSL 转换）
            // -------------------------------------------------------
            float rand(float2 n)
            {
                // cos + sin 双重混合，增加随机性
                return frac(sin(cos(dot(n, float2(12.9898, 12.1414)))) * 83758.5453);
            }

            // -------------------------------------------------------
            // 噪声函数：noise — 双线性插值平滑噪声
            // -------------------------------------------------------
            float noise(float2 n)
            {
                float2 ip = floor(n);          // 整数部分（格子索引）
                float2 fp = frac(n);           // 小数部分（格子内位置）

                // smoothstep 平滑过渡曲线（三次 Hermite）
                float2 u = smoothstep(0.0, 1.0, fp);

                // 4 个角落的随机值双线性混合
                float a = rand(ip);
                float b = rand(ip + float2(1.0, 0.0));
                float c = rand(ip + float2(0.0, 1.0));
                float d = rand(ip + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // -------------------------------------------------------
            // 噪声函数：fbm — 分形布朗运动（5 个倍频程）
            // -------------------------------------------------------
            float fbm(float2 n)
            {
                float total    = 0.0;   // 累计噪声值
                float amplitude = 1.0;  // 当前倍频程振幅
                float totalAmp  = 0.0;  // 振幅总和（用于归一化）

                // 5 个倍频程叠加，频率递增、振幅递减
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    total    += noise(n) * amplitude;
                    totalAmp += amplitude;
                    n        += n * 1.7;       // 频率倍增（约 2.7 倍）
                    amplitude *= 0.47;         // 振幅衰减
                }

                // 归一化到 [0, 1]
                return total / totalAmp;
            }

            // -------------------------------------------------------
            // 调色板：6 色线性分段插值（Shadertoy 火焰渐变）
            // -------------------------------------------------------
            float3 firePalette(float t)
            {
                // 将 t 映射到 [0, 5] 的分段区间
                t = saturate(t) * 5.0;

                float3 c1 = _Color1.rgb;
                float3 c2 = _Color2.rgb;
                float3 c3 = _Color3.rgb;
                float3 c4 = _Color4.rgb;
                float3 c5 = _Color5.rgb;
                float3 c6 = _Color6.rgb;

                // 分段线性插值（5 段 × 2 端点 = 6 色）
                float3 col = c1;

                // 段 0→1: c1 → c2
                col = lerp(col, lerp(c1, c2, saturate(t - 0.0)), step(0.0, t) * step(t, 1.0));
                // 段 1→2: c2 → c3
                col = lerp(col, lerp(c2, c3, saturate(t - 1.0)), step(1.0, t) * step(t, 2.0));
                // 段 2→3: c3 → c4
                col = lerp(col, lerp(c3, c4, saturate(t - 2.0)), step(2.0, t) * step(t, 3.0));
                // 段 3→4: c4 → c5
                col = lerp(col, lerp(c4, c5, saturate(t - 3.0)), step(3.0, t) * step(t, 4.0));
                // 段 4→5: c5 → c6
                col = lerp(col, lerp(c5, c6, saturate(t - 4.0)), step(4.0, t) * step(t, 5.0));

                return col;
            }

            // -------------------------------------------------------
            // 片元着色器
            // -------------------------------------------------------
            fixed4 frag(v2f i) : SV_Target
            {
                // ===================================================
                // 1. 采样主纹理，获取当前像素的颜色与 Alpha
                // ===================================================
                float4 mainTex = tex2D(_MainTex, i.uv);
                float  a0      = mainTex.a;   // 原始 Alpha

                // ===================================================
                // 2. 8 邻域 Alpha 边缘检测
                //    edge = max(0, a0 - 邻居alpha) 的最大值，[0,1]
                // ===================================================
                // 8 个方向偏移（标准化坐标，乘以 _EdgeWidth）
                float2 dirs[8];
                dirs[0] = float2( 1.0,  0.0);
                dirs[1] = float2(-1.0,  0.0);
                dirs[2] = float2( 0.0,  1.0);
                dirs[3] = float2( 0.0, -1.0);
                dirs[4] = float2( 0.707,  0.707);
                dirs[5] = float2(-0.707,  0.707);
                dirs[6] = float2( 0.707, -0.707);
                dirs[7] = float2(-0.707, -0.707);

                float edge = 0.0;

                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    float2 offset     = dirs[k] * _EdgeWidth;
                    float  neighborA  = tex2D(_MainTex, i.uv + offset).a;
                    // 当前像素有 alpha，而邻居没有 → 边缘
                    edge = max(edge, max(0.0, a0 - neighborA));
                }

                edge = saturate(edge);  // 钳制到 [0,1]

                // ===================================================
                // 3. FBM 噪声：像素化 UV + 风向流动
                // ===================================================
                // 像素化：将 UV 对齐到像素网格（减少高频闪烁）
                float2 pixUV = floor(i.uv * _PixelSize) / _PixelSize;

                // 风向流动偏移（随时间沿风向平移）
                float2 windDir  = normalize(_WindDir.xy + float2(0.0001, 0.0)); // 防止零向量
                float2 windFlow = windDir * _Time.y * _FlowSpeed * _WindStrength;

                // FBM 采样坐标
                float2 noiseUV  = pixUV * _NoiseScale + windFlow;
                float  noiseVal = fbm(noiseUV);   // [0, 1]

                // ===================================================
                // 4. 火焰强度 = 边缘 × 噪声调制
                // ===================================================
                float intensity = edge * (0.5 + 0.5 * noiseVal);

                // ===================================================
                // 5. 通过调色板映射火焰颜色（以噪声强度为参数）
                // ===================================================
                float3 color = firePalette(intensity);

                // ===================================================
                // 6. 核心辉光叠加（边缘最强处呈亮白/橙色高光）
                // ===================================================
                color += float3(1.0, 0.6, 0.1) * pow(edge, 3.0) * _CoreGlow;

                // ===================================================
                // 7. 火焰 Alpha：仅在有原始像素（a0 > 0）的边缘产生
                // ===================================================
                float fireAlpha = saturate(edge * intensity * _FireIntensity) * a0;

                // ===================================================
                // 8. 最终合成 — 关键修复：
                //    · 原始精灵图片作为底层完整显示（mainTex.rgb × a0）
                //    · 火焰颜色叠加在边缘（color.rgb × fireAlpha）
                //    · 最终 Alpha = 原始Alpha + 火焰Alpha 的饱和值
                // ===================================================
                float3 finalColor = mainTex.rgb * a0 + color.rgb * fireAlpha;
                float  finalAlpha = saturate(a0 + fireAlpha);

                return fixed4(finalColor, finalAlpha);
            }

            ENDCG
        }
    }

    FallBack "UI/Default"
}
