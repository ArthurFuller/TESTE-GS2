Shader "UI/CardHoverOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ArtTex ("Card Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glow ("Glow", Range(0,1)) = 0
        _SampleWidth ("Outline Width", Range(0.001,0.08)) = 0.012
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };
            fixed4 _Color;
            sampler2D _ArtTex;
            float _Glow;
            float _SampleWidth;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 art = tex2D(_ArtTex, i.uv) * i.color;
                if (_Glow <= 0.001)
                {
                    #ifdef UNITY_UI_CLIP_RECT
                    art.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                    #endif
                    return art;
                }
                float2 stepUV = float2(_SampleWidth, _SampleWidth);
                float alpha = tex2D(_ArtTex, i.uv).a;
                float4 nearAlpha = float4(
                    tex2D(_ArtTex, i.uv + float2(stepUV.x, 0)).a,
                    tex2D(_ArtTex, i.uv - float2(stepUV.x, 0)).a,
                    tex2D(_ArtTex, i.uv + float2(0, stepUV.y)).a,
                    tex2D(_ArtTex, i.uv - float2(0, stepUV.y)).a);
                float4 diagonalAlpha = float4(
                    tex2D(_ArtTex, i.uv + stepUV).a,
                    tex2D(_ArtTex, i.uv - stepUV).a,
                    tex2D(_ArtTex, i.uv + float2(stepUV.x, -stepUV.y)).a,
                    tex2D(_ArtTex, i.uv + float2(-stepUV.x, stepUV.y)).a);
                float2 farStep = stepUV * 2.5;
                float farAlpha =
                    tex2D(_ArtTex, i.uv + float2(farStep.x, 0)).a +
                    tex2D(_ArtTex, i.uv - float2(farStep.x, 0)).a +
                    tex2D(_ArtTex, i.uv + float2(0, farStep.y)).a +
                    tex2D(_ArtTex, i.uv - float2(0, farStep.y)).a;
                float nearest = min(min(nearAlpha.x, nearAlpha.y),
                    min(nearAlpha.z, nearAlpha.w));
                float inner = saturate(alpha - nearest) * 0.85;
                float outer = (1.0 - alpha) * saturate(
                    dot(nearAlpha, float4(0.16, 0.16, 0.16, 0.16)) +
                    dot(diagonalAlpha, float4(0.07, 0.07, 0.07, 0.07)) +
                    farAlpha * 0.035);
                float glow = saturate(max(inner, outer) * _Glow) * i.color.a;
                art.rgb = lerp(art.rgb, float3(1, 1, 1), glow);
                art.a = max(art.a, glow);
                #ifdef UNITY_UI_CLIP_RECT
                art.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return art;
            }
            ENDCG
        }
    }
}
