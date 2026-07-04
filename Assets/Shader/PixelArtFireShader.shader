// PixelArtFire.shader
// 像素风格火焰着色器 - 支持 URP 和内置渲染管线 (Unlit, Transparent)
Shader "Custom/PixelArtFire"
{
    Properties
    {
        // 火焰底部颜色（最热区域）
        _ColorDeep   ("Color Deep (Bottom)",  Color) = (1.0, 0.1, 0.0, 1.0)
        // 火焰中部颜色
        _ColorMid    ("Color Mid (Middle)",   Color) = (1.0, 0.6, 0.0, 1.0)
        // 火焰顶部颜色（最冷区域）
        _ColorTip    ("Color Tip (Top)",      Color) = (1.0, 1.0, 0.2, 1.0)

        // 动画速度（控制火焰闪烁快慢）
        _FireSpeed   ("Fire Speed",           Float) = 1.5
        // 像素化分辨率（值越小像素块越大，越有像素风格感）
        _PixelSize   ("Pixel Size",           Float) = 32.0
        // 火焰高度衰减系数（影响顶部消散范围）
        _FireHeight  ("Fire Height",          Float) = 1.2
        // 边缘柔化程度（越大边缘越软）
        _EdgeSoftness("Edge Softness",        Float) = 0.3
        // 噪声缩放（控制噪声纹理的平铺密度）
        _NoiseScale  ("Noise Scale",          Float) = 4.0

        // 可选外部噪声贴图（如留空则使用程序化噪声）
        [NoScaleOffset]
        _NoiseTex    ("Noise Texture (Optional)", 2D) = "white" {}
        // 是否使用外部噪声贴图（1 = 使用贴图，0 = 程序化）
        _UseNoiseTex ("Use Noise Texture",    Float) = 0.0
    }

    SubShader
    {
        // 透明渲染队列
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            // URP 兼容标记
            "RenderPipeline"  = ""
        }

        // 关闭深度写入，开启 Alpha 混合
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off   // 双面渲染，确保在各角度可见

        Pass
        {
            HLSLPROGRAM
            // 顶点着色器与片元着色器入口
            #pragma vertex   vert
            #pragma fragment frag

            // 兼容 URP / 内置管线的基础头文件
            #if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PIPELINE_URP)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #else
                #include "UnityCG.cginc"
            #endif

            // ─────────────────────────────────────────────
            // 属性声明（与 Properties 块一一对应）
            // ─────────────────────────────────────────────
            float4 _ColorDeep;
            float4 _ColorMid;
            float4 _ColorTip;

            float  _FireSpeed;
            float  _PixelSize;
            float  _FireHeight;
            float  _EdgeSoftness;
            float  _NoiseScale;

            sampler2D _NoiseTex;
            float     _UseNoiseTex;

            // ─────────────────────────────────────────────
            // 顶点输入 / 输出结构体
            // ─────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;   // 模型空间顶点
                float2 uv         : TEXCOORD0;  // 原始 UV
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION; // 裁剪空间顶点
                float2 uv         : TEXCOORD0;   // 传递给片元的 UV
            };

            // ═════════════════════════════════════════════
            //  程序化噪声函数（基于哈希的值噪声）
            //  不依赖任何外部贴图即可产生随机感火焰
            // ═════════════════════════════════════════════

            // 2D 哈希函数：将 UV 映射到 [0,1] 伪随机数
            float hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            // 平滑值噪声：双线性插值四个网格点哈希值
            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);   // 网格整数坐标
                float2 f = frac(uv);    // 网格内小数部分

                // 平滑插值曲线 (smoothstep 风格)
                float2 u = f * f * (3.0 - 2.0 * f);

                // 四个角点的哈希值
                float a = hash21(i + float2(0, 0));
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                // 双线性插值
                return lerp(lerp(a, b, u.x),
                            lerp(c, d, u.x),
                            u.y);
            }

            // 分形布朗运动（FBM）：叠加多层噪声增加细节感
            float fbm(float2 uv)
            {
                float value    = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                // 叠加 4 个倍频程（octave）
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    value     += amplitude * valueNoise(uv * frequency);
                    amplitude *= 0.5;   // 每层振幅减半
                    frequency *= 2.0;   // 每层频率加倍
                }
                return value;
            }

            // ═════════════════════════════════════════════
            //  UV 像素化函数
            //  将连续 UV 吸附到像素网格，产生像素块效果
            // ═════════════════════════════════════════════
            float2 pixelate(float2 uv, float pixelSize)
            {
                // 将 UV 量化到 pixelSize×pixelSize 网格
                // floor(uv * pixelSize) / pixelSize 即对齐到最近像素格
                return floor(uv * pixelSize) / pixelSize;
            }

            // ─────────────────────────────────────────────
            // 顶点着色器
            // ─────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 兼容 URP / 内置管线的裁剪空间变换
                #if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PIPELINE_URP)
                    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                #else
                    OUT.positionCS = UnityObjectToClipPos(IN.positionOS);
                #endif

                OUT.uv = IN.uv;
                return OUT;
            }

            // ─────────────────────────────────────────────
            // 片元着色器
            // ─────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // ── 步骤 1：像素化 UV ────────────────────
                // 先将 UV 吸附到像素网格，得到块状像素风格
                float2 pixUV = pixelate(IN.uv, _PixelSize);

                // ── 步骤 2：构建噪声采样 UV ──────────────
                // 在像素化 UV 基础上加入时间驱动的向上滚动
                // _Time.y 为运行秒数；向上滚动用 -time 偏移 Y
                float  time   = _Time.y * _FireSpeed;
                float2 noiseUV = pixUV * _NoiseScale
                               + float2(0.0, -time); // 向上滚动

                // ── 步骤 3：采样噪声值 ───────────────────
                float noiseVal;
                if (_UseNoiseTex > 0.5)
                {
                    // 使用外部噪声贴图（灰度图 R 通道）
                    noiseVal = tex2D(_NoiseTex, noiseUV).r;
                }
                else
                {
                    // 使用程序化 FBM 噪声
                    noiseVal = fbm(noiseUV);
                }

                // ── 步骤 4：火焰形状计算 ─────────────────
                // uv.y = 0 为底部（最热），uv.y = 1 为顶部（消散）
                float yCoord = IN.uv.y; // 使用原始 Y 以保证平滑衰减

                // 火焰强度 = 噪声值 - 高度衰减
                // 高度越高，减去的值越大，火焰越容易消散
                float heightFade  = yCoord * _FireHeight;
                float fireIntensity = noiseVal - heightFade;

                // 用 smoothstep 控制边缘柔化程度
                // EdgeSoftness 越大，过渡带越宽越柔和
                fireIntensity = smoothstep(0.0, _EdgeSoftness, fireIntensity);

                // ── 步骤 5：颜色渐变 ─────────────────────
                // 根据 uv.y（从底到顶）在三色间插值
                //   uv.y ≈ 0  →  _ColorDeep（深红热焰）
                //   uv.y ≈ 0.5 → _ColorMid  （橙黄）
                //   uv.y ≈ 1  →  _ColorTip  （淡黄/白）
                float  t         = saturate(yCoord); // 归一化高度 [0,1]
                float3 fireColor;
                if (t < 0.5)
                {
                    // 下半段：Deep → Mid
                    fireColor = lerp(_ColorDeep.rgb, _ColorMid.rgb, t * 2.0);
                }
                else
                {
                    // 上半段：Mid → Tip
                    fireColor = lerp(_ColorMid.rgb, _ColorTip.rgb, (t - 0.5) * 2.0);
                }

                // ── 步骤 6：Alpha 计算 ───────────────────
                // 底部完全不透明，顶部随 fireIntensity 消散
                // 额外乘以一个从底到顶线性淡出系数，确保顶端透明
                float alpha = fireIntensity * (1.0 - saturate(yCoord * _FireHeight));
                alpha = saturate(alpha);

                // ── 步骤 7：输出最终颜色 ─────────────────
                return half4(fireColor * fireIntensity, alpha);
            }

            ENDHLSL
        }
    }

    // 回退着色器（在不支持的平台使用半透明 Diffuse）
    FallBack "Transparent/Diffuse"
}
