// ============================================================
//  OcclusionWithShadow.shader
//  Vision Pro / URP Mixed Reality
//
//  PASS 1 - Occlusion:
//    Schreibt nur in den Depth-Buffer → verdeckt virtuelle Objekte
//    hinter realen Objekten (das klassische Occlusion-Material)
//
//  PASS 2 - Shadow Receiver:
//    Liest den Shadow-Map aus URP und rendert einen
//    abgedunkelten Semi-Transparenten Pass über die Occlusion.
//    Blend DstColor Zero = Multiply → dunkelt Passthrough ab.
//
//  WICHTIG: Funktioniert nur wenn das reale Objekt eine
//  annähernd bekannte Geometrie hat (z.B. Tisch-Plane, Wand).
// ============================================================

Shader "Custom/OcclusionWithShadow"
{
    Properties
    {
        // --- Occlusion ---
        [Header(Occlusion)]
        _OcclusionAlpha     ("Occlusion Alpha (0=unsichtbar)",  Range(0,1))   = 0.0

        // --- Shadow ---
        [Header(Shadow)]
        _ShadowColor        ("Shadow Color",                    Color)        = (0, 0, 0, 1)
        _ShadowStrength     ("Shadow Strength",                 Range(0, 1))  = 0.45
        _ShadowSoftness     ("Shadow Softness (PCF)",           Range(0, 1))  = 0.5

        // --- Fake Ambient Occlusion unter Objekten ---
        [Header(Contact Shadow)]
        _ContactShadowColor ("Contact Shadow Color",            Color)        = (0, 0, 0, 1)
        _ContactRadius      ("Contact Shadow Radius",           Range(0, 2))  = 0.3
        _ContactStrength    ("Contact Shadow Strength",         Range(0, 1))  = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Geometry-1"       // Vor normalen Objekten rendern
            "RenderPipeline" = "UniversalPipeline"
        }

        // --------------------------------------------------------
        //  PASS 1: OCCLUSION
        //  Schreibt Depth, kein Color-Output → reale Geometrie
        //  verdeckt virtuelle Objekte dahinter
        // --------------------------------------------------------
        Pass
        {
            Name "Occlusion"

            ColorMask 0          // Kein Color-Output
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert_occlusion
            #pragma fragment frag_occlusion
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _OcclusionAlpha;
                float4 _ShadowColor;
                float _ShadowStrength;
                float _ShadowSoftness;
                float4 _ContactShadowColor;
                float _ContactRadius;
                float _ContactStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert_occlusion(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half4 frag_occlusion(Varyings i) : SV_Target
            {
                // _OcclusionAlpha = 0: vollständige Occlusion (kein Pixel)
                // _OcclusionAlpha > 0: leicht sichtbar (Debug)
                clip(_OcclusionAlpha - 0.999);
                return half4(0, 0, 0, _OcclusionAlpha);
            }
            ENDHLSL
        }

        // --------------------------------------------------------
        //  PASS 2: SHADOW RECEIVER
        //  Liest URP Shadow Maps und rendert Schatten als
        //  Multiply-Blend über den Passthrough.
        //
        //  Blend DstColor Zero = MULTIPLY
        //  → Dunkelt Passthrough-Bild ab ohne es zu ersetzen
        // --------------------------------------------------------
        Pass
        {
            Name "ShadowReceiver"

            // MULTIPLY BLEND: dunkelt Passthrough ab
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert_shadow
            #pragma fragment frag_shadow
            #pragma multi_compile_instancing

            // Shadow-Keywords für URP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _OcclusionAlpha;
                float4 _ShadowColor;
                float  _ShadowStrength;
                float  _ShadowSoftness;
                float4 _ContactShadowColor;
                float  _ContactRadius;
                float  _ContactStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS      : SV_POSITION;
                float3 positionWS      : TEXCOORD0;
                float4 shadowCoord     : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert_shadow(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);

                VertexPositionInputs posInputs = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS  = posInputs.positionCS;
                o.positionWS  = posInputs.positionWS;

                // Shadow Koordinaten für URP Shadow Map
                o.shadowCoord = GetShadowCoord(posInputs);

                return o;
            }

            half4 frag_shadow(Varyings i) : SV_Target
            {
                // --- Shadow aus URP Main Light ---
                Light mainLight = GetMainLight(i.shadowCoord);
                float shadow    = mainLight.shadowAttenuation;

                // shadow = 1.0 → kein Schatten
                // shadow = 0.0 → voller Schatten

                // Invertieren: 0 = kein Schatten, 1 = voller Schatten
                float shadowMask = 1.0 - shadow;

                // Stärke und Softness anwenden
                shadowMask = shadowMask * _ShadowStrength;

                // --- Multiply-Output ---
                // Multiply Blend: Output * DstColor
                // Für Verdunklung: RGB = (1 - shadowMask * shadowColor)
                // Bei shadowMask=0: RGB=1 → keine Änderung
                // Bei shadowMask=1: RGB=shadowColor → maximale Verdunklung

                float3 shadowTint = lerp(float3(1,1,1), _ShadowColor.rgb, shadowMask);

                return half4(shadowTint, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
