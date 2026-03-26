Shader "Custom/FakeGlowMR"
{
    Properties
    {
        _BaseColor      ("Base Color",         Color)  = (1, 1, 1, 1)
        _EmissionColor  ("Emission Color",     Color)  = (0, 0.5, 1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 20)) = 3.0

        // Fresnel (Rim) Glow
        _FresnelPower   ("Fresnel Power",      Range(0.1, 10)) = 2.0
        _FresnelScale   ("Fresnel Scale",      Range(0, 5))    = 1.5

        // Pulsing Animation
        _PulseSpeed     ("Pulse Speed",        Range(0, 10))   = 1.5
        _PulseAmount    ("Pulse Amount",       Range(0, 1))    = 0.3

        // Für Mesh-Glow Overlay: Alpha-Kontrolle
        _AlphaFalloff   ("Alpha Falloff",      Range(0.1, 5))  = 1.5
        
        [Toggle] _UseFresnelOnly ("Fresnel Only (Glow Mesh)", Float) = 0
    }

    SubShader
    {
        // Additive Blending = echter Glow-Effekt, addiert Licht
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+10"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FakeGlow"
            
            // ADDITIVE BLENDING - Kern des Fake-Bloom-Tricks
            Blend One One
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float  _EmissionStrength;
                float  _FresnelPower;
                float  _FresnelScale;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _AlphaFalloff;
                float  _UseFresnelOnly;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);

                float3 posWS  = TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);

                // --- Fresnel (Rim Glow) ---
                // NdotV = 0 an Kanten, 1 in der Mitte
                float NdotV     = saturate(dot(N, V));
                float fresnel   = _FresnelScale * pow(1.0 - NdotV, _FresnelPower);

                // --- Pulse Animation ---
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                // --- Emission ---
                float3 emission = _EmissionColor.rgb * _EmissionStrength * pulse;

                // Fresnel-Only-Mode: nur Rim leuchtet (für Overlay-Glow-Mesh)
                float3 color;
                float  alpha;
                
                if (_UseFresnelOnly > 0.5)
                {
                    color = emission * fresnel;
                    alpha = pow(fresnel, _AlphaFalloff);
                }
                else
                {
                    // Kombination: Basis + Fresnel-Boost an Kanten
                    float3 base  = _BaseColor.rgb * emission;
                    float  rim   = fresnel * _FresnelScale;
                    color = base + _EmissionColor.rgb * rim * pulse;
                    alpha = _BaseColor.a;
                }

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
