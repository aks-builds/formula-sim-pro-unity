Shader "FormulaSim/CarBody"
{
    // PBR car body shader with:
    //   • Livery color injection (primary / secondary zones via mask)
    //   • Carbon fibre detail layer
    //   • Brake glow on rims (heat-based emission)
    //   • DRS shimmer emission
    //   • Night headlight rim glow
    //   • Damage darkening (scratches)

    Properties
    {
        _MainTex        ("Body Albedo",         2D)     = "white" {}
        _NormalMap      ("Body Normal",         2D)     = "bump"  {}
        _MetallicGloss  ("Metallic(R) Smooth(A)",2D)   = "black" {}
        _EmissionMap    ("Emission Map",        2D)     = "black" {}
        _LiveryMask     ("Livery Zone Mask (R=primary,G=secondary)", 2D) = "black" {}
        _CarbonTex      ("Carbon Fibre Detail", 2D)    = "grey"  {}

        [HDR] _PrimaryColor   ("Livery Primary",   Color) = (1,0,0,1)
        [HDR] _SecondaryColor ("Livery Secondary", Color) = (1,1,1,1)
        [HDR] _EmissionColor  ("Base Emission",    Color) = (0,0,0,0)
        [HDR] _BrakeGlowColor ("Brake Glow",       Color) = (1,0.3,0.05,1)
        [HDR] _DrsGlowColor   ("DRS Shimmer",      Color) = (0.3,0.8,1.0,1)
        [HDR] _HeadlightColor ("Headlight Rim",    Color) = (1,0.95,0.8,1)

        _BrakeHeat      ("Brake Heat",        Range(0,1)) = 0.0
        _DrsActive      ("DRS Active",        Range(0,1)) = 0.0
        _NightMode      ("Night Mode",        Range(0,1)) = 0.0
        _DamageAmount   ("Damage Amount",     Range(0,1)) = 0.0
        _CarbonBlend    ("Carbon Blend",      Range(0,1)) = 0.25
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);     SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MetallicGloss); SAMPLER(sampler_MetallicGloss);
            TEXTURE2D(_EmissionMap);   SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_LiveryMask);    SAMPLER(sampler_LiveryMask);
            TEXTURE2D(_CarbonTex);     SAMPLER(sampler_CarbonTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _PrimaryColor, _SecondaryColor, _EmissionColor;
                half4  _BrakeGlowColor, _DrsGlowColor, _HeadlightColor;
                float  _BrakeHeat, _DrsActive, _NightMode, _DamageAmount, _CarbonBlend;
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
                // ── Albedo + livery injection ─────────────────────────────────
                half4 base      = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 mask      = SAMPLE_TEXTURE2D(_LiveryMask, sampler_LiveryMask, IN.uv);
                half4 carbon    = SAMPLE_TEXTURE2D(_CarbonTex, sampler_CarbonTex, IN.uv * 8.0);

                // Overlay livery colors where mask channels are active
                half3 albedo = base.rgb;
                albedo       = lerp(albedo, _PrimaryColor.rgb,   mask.r);
                albedo       = lerp(albedo, _SecondaryColor.rgb, mask.g);
                // Blend in carbon fibre on unpainted zones
                albedo       = lerp(albedo, carbon.rgb * albedo, _CarbonBlend * (1.0 - mask.r - mask.g));
                // Damage darkens and desaturates
                albedo       = lerp(albedo, albedo * 0.55, _DamageAmount * base.a);

                // ── Normal ────────────────────────────────────────────────────
                half3 n     = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                half3x3 TBN = half3x3(normalize(IN.tangentWS), normalize(IN.bitangentWS), normalize(IN.normalWS));
                half3   N   = normalize(mul(n, TBN));

                // ── Metallic / Smoothness ─────────────────────────────────────
                half4 ms = SAMPLE_TEXTURE2D(_MetallicGloss, sampler_MetallicGloss, IN.uv);
                // Livery paint is high-gloss clearcoat
                float smoothness = lerp(ms.a, 0.95, mask.r + mask.g);

                // ── Emission ──────────────────────────────────────────────────
                half3 emissMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb;
                // Brake glow on rim areas (use emission map blue channel for rim mask)
                half3 brakeGlow = _BrakeGlowColor.rgb * _BrakeHeat * emissMap.b * 3.5;
                // DRS shimmer on rear wing (emission map green channel)
                half drsFlicker = sin(_Time.y * 18.0) * 0.15 + 0.85;
                half3 drsGlow   = _DrsGlowColor.rgb * _DrsActive * emissMap.g * drsFlicker * 2.0;
                // Night headlight rim
                half3 headlight = _HeadlightColor.rgb * _NightMode * emissMap.r * 4.0;

                half3 emission  = _EmissionColor.rgb * emissMap + brakeGlow + drsGlow + headlight;

                // ── PBR lighting ──────────────────────────────────────────────
                InputData inputData;
                ZERO_INITIALIZE(InputData, inputData);
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = N;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord        = IN.fogCoord;

                SurfaceData surface;
                ZERO_INITIALIZE(SurfaceData, surface);
                surface.albedo     = albedo;
                surface.metallic   = ms.r;
                surface.smoothness = smoothness;
                surface.emission   = emission;
                surface.occlusion  = 1.0;
                surface.alpha      = 1.0;

                half4 lit = UniversalFragmentPBR(inputData, surface);
                UNITY_APPLY_FOG(IN.fogCoord, lit.rgb);
                return lit;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
