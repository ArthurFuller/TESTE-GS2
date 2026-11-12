Shader "GS2/Weather Particles"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Shape ("0 Rain, 1 Splash, 2 Smoke", Range(0,2)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4 _Color;
                float _Shape;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float alpha;
                if (_Shape < 0.5)
                {
                    float width = exp(-p.x * p.x * 55.0);
                    alpha = width * (1.0 - smoothstep(0.7, 1.0, abs(p.y)));
                }
                else if (_Shape < 1.5)
                {
                    float radius = length(p);
                    float ripple = exp(-pow((radius - 0.62) * 15.0, 2.0));
                    float crown = exp(-pow((radius - 0.27) * 18.0, 2.0)) * 0.32;
                    float droplets =
                        exp(-dot(p - float2(-0.39, 0.30), p - float2(-0.39, 0.30)) * 190.0) +
                        exp(-dot(p - float2(0.42, 0.23), p - float2(0.42, 0.23)) * 190.0) +
                        exp(-dot(p - float2(-0.18, 0.52), p - float2(-0.18, 0.52)) * 190.0) +
                        exp(-dot(p - float2(0.17, 0.50), p - float2(0.17, 0.50)) * 190.0);
                    alpha = saturate(ripple + crown + droplets * 0.55);
                }
                else
                {
                    float radius = length(p);
                    float wisps = 0.7 + 0.3 * sin(p.x * 6.0 + p.y * 8.0 + _Time.y * 0.7) *
                        sin(p.x * 9.0 - p.y * 5.0 - _Time.y * 0.5);
                    alpha = (1.0 - smoothstep(0.3, 1.0, radius)) * wisps;
                }
                return half4(input.color.rgb, input.color.a * saturate(alpha));
            }
            ENDHLSL
        }
    }
}
