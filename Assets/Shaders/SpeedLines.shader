Shader "FormulaSim/SpeedLines"
{
    // Radial speed lines that intensify at high velocity.
    // Also used for DRS tunnel-vision effect.

    Properties
    {
        _Color       ("Line Color",    Color)       = (1,1,1,1)
        _Intensity   ("Intensity",     Range(0,1))  = 0.0
        _LineCount   ("Line Count",    Float)       = 24.0
        _LineWidth   ("Line Width",    Range(0.001,0.3)) = 0.04
        _Falloff     ("Radial Falloff",Range(0.1,2.0)) = 0.8
        _CenterX     ("Center X",      Range(0,1))  = 0.5
        _CenterY     ("Center Y",      Range(0,1))  = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_full
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float  _Intensity, _LineCount, _LineWidth, _Falloff, _CenterX, _CenterY;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Vary vert_full(Attr IN) { Vary O; O.pos=TransformObjectToHClip(IN.pos.xyz); O.uv=IN.uv; return O; }

            half4 frag(Vary IN) : SV_Target
            {
                if (_Intensity < 0.005) return half4(0,0,0,0);

                float2 center = float2(_CenterX, _CenterY);
                float2 d      = IN.uv - center;
                float  dist   = length(d);
                float  angle  = atan2(d.y, d.x) / (2.0 * 3.14159265);

                // Line pattern in polar angle space
                float frac  = frac(angle * _LineCount);
                float line  = smoothstep(0.0, _LineWidth, frac) * smoothstep(1.0, 1.0 - _LineWidth, frac);

                // Radial falloff: lines stronger near edge, transparent at centre
                float radFalloff = pow(dist, _Falloff);
                float alpha = line * radFalloff * _Intensity * _Color.a;

                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
