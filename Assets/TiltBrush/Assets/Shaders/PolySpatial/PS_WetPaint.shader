// PolySpatial-compatible WetPaint brush replacement
// Simple PBR lit, double-sided, vertex color
Shader "Brush/PolySpatial/WetPaint"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.7
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _BumpScale ("Normal Scale", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off  // Double-sided

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            float4 _MainTex_ST;
            float _Smoothness;
            float _Metallic;
            float _BumpScale;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.tangentWS = normInputs.tangentWS;
                output.bitangentWS = normInputs.bitangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                // Flip normal for back faces
                input.normalWS *= facing;

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 baseColor = input.color.rgb * texColor.rgb;
                
                // Normal mapping
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3x3 TBN = half3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                half3 normalWS = normalize(mul(normalTS, TBN));
                
                // Simple lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                SurfaceData surfData = (SurfaceData)0;
                surfData.albedo = baseColor;
                surfData.metallic = _Metallic;
                surfData.smoothness = _Smoothness;
                surfData.normalTS = normalTS;
                surfData.occlusion = 1.0;
                surfData.alpha = 1.0;

                return UniversalFragmentPBR(inputData, surfData);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
