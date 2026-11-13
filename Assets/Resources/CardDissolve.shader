Shader "UI/CardDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Destruicao", Range(0,1)) = 0
        _NoiseScale ("Densidade das formas", Range(3,30)) = 8
        _EdgeSoftness ("Suavidade do recorte", Range(0.001,0.15)) = 0.025
        [HideInInspector] _RectSize ("Card size", Vector) = (140,140,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 local : TEXCOORD1;
            };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Cutoff;
            float _NoiseScale;
            float _EdgeSoftness;
            float4 _RectSize;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float lower = lerp(hash(cell), hash(cell + float2(1, 0)), f.x);
                float upper = lerp(hash(cell + float2(0, 1)),
                    hash(cell + float2(1, 1)), f.x);
                return lerp(lower, upper, f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                o.local = v.vertex.xy / max(_RectSize.xy, float2(1, 1)) + 0.5;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * i.color;
                clip(color.a - 0.001);
                float2 uv = saturate(i.local);
                float noise = valueNoise(uv * _NoiseScale) * 0.8 +
                    valueNoise(uv * _NoiseScale * 2.0) * 0.2;
                float visible = smoothstep(_Cutoff - _EdgeSoftness,
                    _Cutoff + _EdgeSoftness, noise);
                if (_Cutoff <= 0.001) visible = 1.0;
                if (_Cutoff >= 0.999) visible = 0.0;
                color.a *= visible;
                return color;
            }
            ENDCG
        }
    }
}
