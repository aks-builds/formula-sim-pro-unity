Shader "FormulaSim/RainOverlay"
{
    // Full-screen rain overlay: streak drops, fog layer, lightning flash.
    // Applied as a screen-space blit in a custom URP Renderer Feature.

    Properties
    {
        _MainTex        ("Source",             2D)     = "white" {}
        _RainDropTex    ("Rain Drop Noise",    2D)     = "black" {}
        _RainOpacity    ("Rain Opacity",       Range(0,1)) = 0.0
        _FlashIntensity ("Lightning Flash",    Range(0,1)) = 0.0
        _StreakLength    ("Streak Length",      Range(0.005,0.05)) = 0.018
        _CarVelocity    ("Car Velocity (XY NDC)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            Name "RainOverlay"

            HLSLPROGRAM
            #pragma vertex   vert_full
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
            TEXTURE2D(_RainDropTex); SAMPLER(sampler_RainDropTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _RainOpacity;
                float  _FlashIntensity;
                float  _StreakLength;
                float4 _CarVelocity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert_full(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // Streak direction: slight downward + car velocity tilt
            float2 StreakDir(float2 vel)
            {
                float2 dir = normalize(float2(-0.05, -1.0) + vel * 0.4);
                return dir;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_RainOpacity < 0.005) return half4(0,0,0,0);

                float2 uv      = IN.uv;
                float  t       = _Time.y;
                float2 vel     = _CarVelocity.xy;
                float2 sDir    = StreakDir(vel);

                // Three depth layers
                float acc = 0;

                // Layer 1 — near (large, fast)
                float2 uv1 = uv * 1.0  + sDir * t * 0.90;
                float2 us1 = uv * 1.0  + sDir * (t * 0.90 - _StreakLength);
                float cell1   = SAMPLE_TEXTURE2D(_RainDropTex, sampler_RainDropTex, uv1).r;
                float streak1 = SAMPLE_TEXTURE2D(_RainDropTex, sampler_RainDropTex, us1).r;
                float drop1   = saturate(cell1 * 3.0 - 2.0);
                acc += (drop1 + saturate(streak1 - cell1 * 0.3) * 0.4) * 0.55;

                // Layer 2 — mid
                float2 uv2 = uv * 2.2  + sDir * t * 0.55;
                float2 us2 = uv * 2.2  + sDir * (t * 0.55 - _StreakLength * 0.7);
                float cell2   = SAMPLE_TEXTURE2D(_RainDropTex, sampler_RainDropTex, uv2).r;
                float streak2 = SAMPLE_TEXTURE2D(_RainDropTex, sampler_RainDropTex, us2).r;
                float drop2   = saturate(cell2 * 3.0 - 2.0);
                acc += (drop2 + saturate(streak2 - cell2 * 0.3) * 0.4) * 0.30;

                // Layer 3 — far (small, slow)
                float2 uv3 = uv * 4.5  + sDir * t * 0.28;
                float2 us3 = uv * 4.5  + sDir * (t * 0.28 - _StreakLength * 0.4);
                float cell3   = SAMPLE_TEXTURE2D(_RainDropTex, sampler_RainDropTex, uv3).r;
                float streak3 = SAMPLE_TEXTURE2D(_RainDropTex, sampler_RainDropTex, us3).r;
                float drop3   = saturate(cell3 * 3.0 - 2.0);
                acc += (drop3 + saturate(streak3 - cell3 * 0.3) * 0.4) * 0.14;

                acc = saturate(acc) * _RainOpacity;

                // Raindrops: blue-white tint
                half4 dropCol = half4(0.82, 0.88, 1.0, acc * 0.72);

                // Screen fog tint (heavier rain = more opacity)
                float fog     = _RainOpacity * 0.12;
                half4 fogCol  = half4(0.15, 0.18, 0.26, fog);

                // Lightning additive
                half4 flashCol = half4(1,1,1, _FlashIntensity * 0.50);

                // Composite
                half4 out_col  = fogCol;
                out_col.rgb    = lerp(out_col.rgb, dropCol.rgb, dropCol.a);
                out_col.a      = saturate(out_col.a + dropCol.a);
                out_col.rgb   += flashCol.rgb * flashCol.a;
                out_col.a      = saturate(out_col.a + flashCol.a);

                return out_col;
            }
            ENDHLSL
        }
    }
}
