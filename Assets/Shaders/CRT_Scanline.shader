Shader "Custom/UI/CRT_Scanlines_UI_DynamicOverlay_Fixed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        // _Color 的 RGB 决定扫描线颜色，Alpha 决定最大不透明度
        _Color ("Scanline Color & Max Alpha", Color) = (0.0, 1.0, 0.0, 0.8) 
        
        // CRT Parameters
        _ScanlineDensity ("Scanline Density (Line Count)", Range(10.0, 1000.0)) = 150.0 
        _ScanlineIntensity ("Scanline Intensity (Darkness)", Range(0.0, 1.0)) = 0.8
        _ScanlineSpeed ("Scanline Speed (Scroll/sec)", Range(0.0, 2.0)) = 0.1 

        // UI Properties (用于正确的 Canvas 渲染)
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
        [Toggle] _MaskSoft ("Soft Masking", Float) = 0
    }
    SubShader
    {
        Tags
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True" 
        }

        Cull Off
        Lighting Off
        ZWrite Off
        // 标准混合模式：允许下面的 UI 元素透过
        Blend SrcAlpha OneMinusSrcAlpha 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ PIXELSNAP_ON
            #pragma multi_compile_local _ _MASKSOFT_ON

            #include "UnityCG.cginc"
            #include "UnityUI.cginc" 

            // ==========================================================
            // 结构体定义 (确保 v2f 在 vert 函数之前定义，修复编译错误)
            // ==========================================================
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; 
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; 
            };
            
            // ==========================================================
            // 属性定义
            // ==========================================================

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            float _ScanlineDensity;
            float _ScanlineIntensity;
            float _ScanlineSpeed;


            // ==========================================================
            // 顶点着色器
            // ==========================================================

            v2f vert (appdata v)
            {
                v2f o;
                
                #ifdef PIXELSNAP_ON
                    v.vertex = UnityPixelSnap(v.vertex);
                #endif
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color; 
                return o;
            }

            // ==========================================================
            // 片元着色器
            // ==========================================================

            fixed4 frag (v2f i) : SV_Target
            {
                // --- 1. 动态扫描线计算 ---
                
                // 1.1 引入时间偏移，并使用 frac() 确保值在 [0, 1] 之间循环
                float timeOffset = frac(_Time.y * _ScanlineSpeed); 
                
                // 1.2 计算带有时间偏移的 Y 坐标
                // 向下滚动，并将 UV 坐标拉伸到指定的密度
                float yCoord = (i.uv.y - timeOffset) * _ScanlineDensity; 
                
                // 1.3 扫描线图案 (0.0 到 1.0 的波形)
                float scan = pow(frac(yCoord), 5.0); // 锐化线条
                scan = 1.0 - scan; // 反转：1.0 为亮，0.0 为线

                // 1.4 计算最终亮度 (finalScan: 亮 1.0 -> 暗 0.0)
                // lerp 确保暗处不会完全变黑，保留最低亮度
                float finalScan = lerp(1.0 - _ScanlineIntensity, 1.0, scan);

                // --- 2. 转换为叠加层 Alpha ---

                // 2.1. 计算黑暗度 (Darkness Factor)
                // Darkness Factor (darkness): 扫描线暗处趋近于 1.0，亮处趋近于 0.0
                float darkness = 1.0 - finalScan;

                // 2.2. 计算最终透明度 (Alpha)
                // 最终 Alpha = 黑暗度 * Raw Image/材质设置的最大 Alpha
                // 结果：暗区绘制出颜色，亮区变透明
                float finalAlpha = darkness * i.color.a; 

                // --- 3. 最终输出 ---
                
                // 输出颜色：用户设置的颜色 (RGB)
                // 输出透明度：计算出的动态 Alpha
                fixed4 col = fixed4(i.color.rgb, finalAlpha);
                
                // 应用载体纹理的 Alpha（用于 Image masking 或边缘控制）
                col.a *= tex2D(_MainTex, i.uv).a;

                return col;
            }
            ENDCG
        }
    }
}