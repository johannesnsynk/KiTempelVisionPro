// ============================================================
//  ProjectedBlobShadow.shader
//  Vision Pro / URP Mixed Reality
//
//  FALLBACK wenn kein Directional Light verfügbar:
//  Klassischer "Blob Shadow" - eine Plane unter dem Objekt
//  mit weichem radialem Schatten-Gradient.
//
//  Blend DstColor Zero = Multiply → dunkelt Passthrough ab
//
//  Setup: Plane-Mesh unter das virtuelle Objekt legen,
//  dieses Material drauf. Plane = reale Oberfläche (Tisch etc.)
// ============================================================

Shader "Custom/ProjectedBlobShadow"
{
    Properties
    {
        _ShadowColor    ("Shadow Color",        Color)       = (0.05, 0.05, 0.1, 1)
        _ShadowStrength ("Shadow Strength",     Range(0, 1)) = 0.5
        _Radius         ("Shadow Radius",       Range(0.01, 1)) = 0.4
        _Softness       ("Edge Softness",       Range(0.01, 1)) = 0.5
        _OffsetX        ("Offset X (Lichtdir)", Range(-1, 1))= 0.1
        _OffsetY        ("Offset Y (Lichtdir)", Range(-1, 1))= 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent-1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BlobShadow"
            Blend DstColor Zero   // Multiply
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
                float  _ShadowStrength;
                float  _Radius;
                float  _Softness;
                float  _OffsetX;
                float  _OffsetY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv         = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // UV von 0..1 → -1..1 zentrieren
                float2 centered = (i.uv - 0.5) * 2.0;

                // Lichtrichtungs-Offset (verschiebt Schatten)
                centered -= float2(_OffsetX, _OffsetY);

                // Radialer Abstand vom Zentrum
                float dist = length(centered);

                // Weicher Falloff: smoothstep von Radius nach außen
                float shadow = 1.0 - smoothstep(_Radius - _Softness, _Radius + _Softness, dist);

                // Stärke anwenden
                shadow *= _ShadowStrength;

                // Multiply Output: 1 = keine Änderung, ShadowColor = dunkel
                float3 col = lerp(float3(1,1,1), _ShadowColor.rgb, shadow);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
