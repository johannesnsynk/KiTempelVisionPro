// PolySpatial-compatible LightWire brush replacement
// Simulates blinking lights via emission pulsing (no vertex displacement)
Shader "Brush/PolySpatial/LightWire"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.4
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 3.0
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 5.0
        _LightDensity ("Light Density", Range(1, 20)) = 7.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back

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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            float4 _MainTex_ST;
            float _Smoothness;
            float _Metallic;
            float4 _EmissionColor;
            float _EmissionStrength;
            float _PulseSpeed;
            float _LightDensity;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Simulate light segments along UV.x
                float envelope = sin(fmod(input.uv.x * 2.0, 1.0) * 3.14159);
                float isLight = step(envelope, 0.15);
                
                // Animate: lights travel along the wire
                float lightIndex = fmod(input.uv.x * 2.0 + 0.5, _LightDensity);
                float timeIndex = fmod(_Time.y * _PulseSpeed, _LightDensity);
                float delta = abs(lightIndex - timeIndex);
                float onAmount = 1.0 - saturate(delta * 1.5);
                
                // Color cycling per light segment (R, G, B)
                int colorIndex = (int)fmod(input.uv.x * 2.0 + 0.5, 3.0);
                half3 lightColor = input.color.rgb;
                if (colorIndex == 0) lightColor *= half3(0.2, 0.2, 1.0);
                else if (colorIndex == 1) lightColor *= half3(1.0, 0.2, 0.2);
                else lightColor *= half3(0.2, 1.0, 0.2);
                
                // Base color: dark wire
                half3 baseColor = (1.0 - isLight) * input.color.rgb * texColor.rgb * 0.2;
                
                // Emission: colored pulsing lights
                half3 emission = isLight * lightColor * onAmount * _EmissionStrength * _EmissionColor.rgb;
                
                // Simple lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                SurfaceData surfData = (SurfaceData)0;
                surfData.albedo = baseColor;
                surfData.emission = emission;
                surfData.metallic = _Metallic;
                surfData.smoothness = _Smoothness;
                surfData.occlusion = 1.0;
                surfData.alpha = 1.0;

                return UniversalFragmentPBR(inputData, surfData);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
