Shader "FormulaSim/NightTrackGlow"
{
    // Applied to circuit light sprites and track-side floodlight panels.
    // HDR emission creates real bloom in URP post-processing.
    // Also used for the hotel-section light trails in Abu Dhabi / Singapore.

    Properties
    {
        _MainTex      ("Sprite",          2D)      = "white"  {}
        [HDR] _GlowColor ("Glow Color",  Color)   = (1,0.95,0.8,1)
        _GlowIntensity ("Glow Intensity",Range(0,8)) = 3.0
        _PulseSpeed   ("Pulse Speed",    Range(0,4)) = 0.0
        _PulseAmount  ("Pulse Amount",   Range(0,1)) = 0.0
        _FlickerSpeed ("Flicker Speed",  Range(0,30)) = 0.0
        _FlickerThresh("Flicker Threshold",Range(0.5,1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _GlowColor;
                float  _GlowIntensity;
                float  _PulseSpeed, _PulseAmount;
                float  _FlickerSpeed, _FlickerThresh;
            CBUFFER_END

            struct Attr { float4 pos:POSITION; float2 uv:TEXCOORD0; half4 col:COLOR; };
            struct Vary { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; half4 col:COLOR; };

            Vary vert(Attr IN)
            {
                Vary O;
                O.pos = TransformObjectToHClip(IN.pos.xyz);
                O.uv  = TRANSFORM_TEX(IN.uv, _MainTex);
                O.col = IN.col;
                return O;
            }

            half4 frag(Vary IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float t = _Time.y;

                // Pulse (slow sin wave — e.g. for safety car lights)
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseAmount;

                // Flicker (high-freq noise — fluorescent / sodium arc lamp effect)
                float flicker = 1.0;
                if (_FlickerSpeed > 0.01)
                {
                    float fn = sin(t * _FlickerSpeed * 3.7) * sin(t * _FlickerSpeed * 2.3);
                    flicker  = step(_FlickerThresh, abs(fn)) * 0.3 + 0.7;
                }

                float intensity = _GlowIntensity * pulse * flicker;
                half3 glow = _GlowColor.rgb * intensity * tex.rgb;

                return half4(glow, tex.a * IN.col.a * _GlowColor.a);
            }
            ENDHLSL
        }
    }
}
