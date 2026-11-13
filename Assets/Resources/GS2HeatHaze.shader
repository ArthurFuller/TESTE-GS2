Shader "GS2/Heat Haze"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Distortion ("Distortion", Range(0,0.015)) = 0.005
        _Speed ("Wave Speed", Range(0,5)) = 1.4
        _Opacity ("Opacity", Range(0,1)) = 0.55
        _Warmth ("Warmth", Range(0,0.3)) = 0.09
        _WarmColor ("Warm Color", Color) = (1,0.77,0.49,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Distortion;
                float _Speed;
                float _Opacity;
                float _Warmth;
                half4 _WarmColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float mask = 1.0 - smoothstep(0.12, 0.65, input.uv.y);
                float wave = sin(input.uv.y * 130.0 - _Time.y * _Speed * 3.0 +
                    sin(input.uv.x * 23.0 + _Time.y * _Speed) * 2.0);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 offset = float2(wave, wave * 0.25) * _Distortion * mask;
                half3 sceneColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture,
                    sampler_CameraOpaqueTexture, screenUV + offset).rgb;
                sceneColor = lerp(sceneColor, sceneColor * _WarmColor.rgb, _Warmth);
                return half4(sceneColor, _Opacity * mask * input.color.a);
            }
            ENDHLSL
        }
    }
}
