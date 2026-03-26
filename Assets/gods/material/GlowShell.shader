// ============================================================
//  GlowShell.shader
//  Vision Pro / URP Mixed Reality
//
//  Der Kern-Trick: Im Vertex Shader wird jeder Vertex
//  entlang seiner Weltspace-Normalen nach außen verschoben.
//  → Das Mesh "wächst" gleichmäßig in alle Richtungen.
//
//  Diesen Shader auf ein DUPLIKAT des Original-Mesh legen
//  (gleiche Geometrie, nur dieser Shader).
//  Cull Front → nur die nach außen zeigenden Flächen sichtbar.
//
//  Kombination mit Fresnel: Kanten leuchten stärker,
//  Mitte ist transparent → wirkt wie echter Glow-Halo.
// ============================================================

Shader "Custom/GlowShell"
{
    Properties
    {
        [Header(Glow)]
        _GlowColor      ("Glow Color",          Color)       = (0, 0.6, 1, 1)
        _GlowStrength   ("Glow Strength",        Range(0, 20)) = 5.0

        [Header(Shell Expansion)]
        _ShellOffset    ("Shell Offset (Expand)", Range(0, 0.5)) = 0.05
        // Tipp: Wert = gewünschte Glow-Breite in Metern

        [Header(Falloff)]
        _FresnelPower   ("Fresnel Falloff",      Range(0.1, 8)) = 2.0
        // Hoch = schmale Kante, Niedrig = breiter Glow

        [Header(Animation)]
        _PulseSpeed     ("Pulse Speed",          Range(0, 10)) = 1.5
        _PulseAmount    ("Pulse Amount",         Range(0, 1))  = 0.25

        [Header(Multi Shell)]
        [Toggle(_MULTI_SHELL)] _MultiShell ("Multi Shell (weicher)", Float) = 0
        _Shell2Offset   ("Shell 2 Offset",       Range(0, 1))  = 0.15
        _Shell2Strength ("Shell 2 Strength",     Range(0, 1))  = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+5"
            "RenderPipeline" = "UniversalPipeline"
        }

        // --------------------------------------------------------
        //  PASS 1: INNER SHELL
        //  Nah am Mesh, hell und scharf
        // --------------------------------------------------------
        Pass
        {
            Name "GlowShell_Inner"

            Blend One One          // Additive
            ZWrite Off
            ZTest LEqual
            Cull Front             // ← Wichtig: Innenseite cullen
                                   //   nur Außenkante sichtbar

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _MULTI_SHELL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float  _GlowStrength;
                float  _ShellOffset;
                float  _FresnelPower;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _MultiShell;
                float  _Shell2Offset;
                float  _Shell2Strength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                // ← KERN-TRICK: Vertex entlang Normalen verschieben
                float3 expandedPos = i.positionOS.xyz + i.normalOS * _ShellOffset;

                o.positionCS = TransformObjectToHClip(expandedPos);
                o.normalWS   = TransformObjectToWorldNormal(i.normalOS);

                float3 posWS = TransformObjectToWorld(expandedPos);
                o.viewDirWS  = GetWorldSpaceNormalizeViewDir(posWS);

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);

                // Fresnel: 0 an Kanten (nach außen zeigend), 1 in Mitte
                // Mit Cull Front: Wir sehen nur Außenflächen
                // → NdotV nah 0 = wir schauen fast senkrecht auf die Fläche
                float NdotV   = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Pulse
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                float3 color = _GlowColor.rgb * _GlowStrength * fresnel * pulse;

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // --------------------------------------------------------
        //  PASS 2: OUTER SHELL (optional, weicher Halo)
        //  Größere Expansion, schwächer → weicher Falloff
        // --------------------------------------------------------
        Pass
        {
            Name "GlowShell_Outer"

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex   vert2
            #pragma fragment frag2
            #pragma multi_compile_instancing
            #pragma shader_feature_local _MULTI_SHELL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float  _GlowStrength;
                float  _ShellOffset;
                float  _FresnelPower;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _MultiShell;
                float  _Shell2Offset;
                float  _Shell2Strength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert2(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                // Größere Expansion für äußere Shell
                float3 expandedPos = i.positionOS.xyz + i.normalOS * _Shell2Offset;

                o.positionCS = TransformObjectToHClip(expandedPos);
                o.normalWS   = TransformObjectToWorldNormal(i.normalOS);

                float3 posWS = TransformObjectToWorld(expandedPos);
                o.viewDirWS  = GetWorldSpaceNormalizeViewDir(posWS);

                return o;
            }

            half4 frag2(Varyings i) : SV_Target
            {
                // Outer Shell nur rendern wenn Toggle aktiv
                #ifndef _MULTI_SHELL
                    return half4(0,0,0,0);
                #endif

                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);

                float NdotV   = saturate(dot(N, V));
                // Breiterer Falloff für äußere Shell
                float fresnel = pow(1.0 - NdotV, max(0.5, _FresnelPower * 0.4));

                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed + 0.5);

                float3 color = _GlowColor.rgb * _GlowStrength * _Shell2Strength * fresnel * pulse;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
