Shader "FormulaSim/WetTrack"
{
    Properties
    {
        _MainTex        ("Track Albedo",         2D)     = "white" {}
        _NormalMap      ("Track Normal Map",     2D)     = "bump"  {}
        _PuddleNormal   ("Puddle Ripple Normal", 2D)     = "bump"  {}
        _MetallicGloss  ("Metallic (R) Smooth (A)", 2D) = "black" {}

        _PuddleCoverage ("Puddle Coverage",   Range(0,1)) = 0.0
        _TrackDarkening ("Track Darkening",   Range(0,1)) = 0.0
        _ReflectionStr  ("Reflection Strength",Range(0,1))= 0.0
        _TrackWetness   ("Track Wetness",     Range(0,1)) = 0.0
        _FlashIntensity ("Lightning Flash",   Range(0,1)) = 0.0
        _SkyTint        ("Sky Tint Color",    Color)    = (0.55, 0.72, 1.0, 1.0)

        _RippleSpeed    ("Ripple Speed",      Float)    = 0.04
        _RippleScale    ("Ripple Tiling",     Float)    = 3.2
        _SpecularPow    ("Specular Power",    Float)    = 96.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ LIGHTMAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);     SAMPLER(sampler_NormalMap);
            TEXTURE2D(_PuddleNormal);  SAMPLER(sampler_PuddleNormal);
            TEXTURE2D(_MetallicGloss); SAMPLER(sampler_MetallicGloss);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _PuddleCoverage;
                float  _TrackDarkening;
                float  _ReflectionStr;
                float  _TrackWetness;
                float  _FlashIntensity;
                float4 _SkyTint;
                float  _RippleSpeed;
                float  _RippleScale;
                float  _SpecularPow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                UNITY_FOG_COORDS(5)
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS  = TransformWorldToHClip(OUT.positionWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS   = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS) * IN.tangentOS.w;
                UNITY_TRANSFER_FOG(OUT, OUT.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // ── Base albedo ───────────────────────────────────────────────
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // ── Normal mapping ────────────────────────────────────────────
                half3 baseNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));

                // ── Puddle ripple normals (two animated layers) ───────────────
                float2 ripple1UV = IN.uv * _RippleScale + float2(t * _RippleSpeed, t * _RippleSpeed * 0.7);
                float2 ripple2UV = IN.uv * _RippleScale * 0.62 + float2(-t * _RippleSpeed * 0.6, t * _RippleSpeed * 0.9);
                half3  rn1 = UnpackNormal(SAMPLE_TEXTURE2D(_PuddleNormal, sampler_PuddleNormal, ripple1UV));
                half3  rn2 = UnpackNormal(SAMPLE_TEXTURE2D(_PuddleNormal, sampler_PuddleNormal, ripple2UV));
                half3  puddleNorm = normalize(rn1 + rn2);

                // Blend base normal with puddle ripple based on coverage × wetness
                float  wetBlend   = _PuddleCoverage * _TrackWetness;
                half3  finalNorm  = normalize(lerp(baseNormal, puddleNorm, wetBlend));

                // Transform to world space
                half3x3 TBN = half3x3(normalize(IN.tangentWS), normalize(IN.bitangentWS), normalize(IN.normalWS));
                half3    N  = normalize(mul(finalNorm, TBN));

                // ── Metallic / smoothness ─────────────────────────────────────
                half4 ms       = SAMPLE_TEXTURE2D(_MetallicGloss, sampler_MetallicGloss, IN.uv);
                // Wet asphalt becomes much smoother (puddles are nearly mirror-smooth)
                float smoothness = lerp(ms.a, 0.96, wetBlend);
                float metallic   = ms.r;

                // ── Track darkening from moisture ─────────────────────────────
                float darken = 1.0 - _TrackDarkening * _TrackWetness * 0.38;
                albedo.rgb *= darken;

                // ── PBR lighting (URP) ────────────────────────────────────────
                InputData inputData;
                ZERO_INITIALIZE(InputData, inputData);
                inputData.positionWS        = IN.positionWS;
                inputData.normalWS          = N;
                inputData.viewDirectionWS   = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord       = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord          = IN.fogCoord;

                SurfaceData surface;
                ZERO_INITIALIZE(SurfaceData, surface);
                surface.albedo      = albedo.rgb;
                surface.metallic    = metallic;
                surface.smoothness  = smoothness;
                surface.alpha       = 1.0;
                surface.occlusion   = 1.0;

                half4 lit = UniversalFragmentPBR(inputData, surface);

                // ── Puddle reflection (sky tint approximation) ────────────────
                half3 viewDir   = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float fresnel   = pow(1.0 - saturate(dot(viewDir, N)), 2.5);
                fresnel         = lerp(0.04, 1.0, fresnel);
                half3 reflected = lerp(lit.rgb, _SkyTint.rgb, fresnel * _ReflectionStr * _TrackWetness);

                // ── Lightning flash ────────────────────────────────────────────
                reflected = lerp(reflected, half3(1,1,1), _FlashIntensity * 0.7);

                UNITY_APPLY_FOG(IN.fogCoord, reflected);
                return half4(reflected, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
