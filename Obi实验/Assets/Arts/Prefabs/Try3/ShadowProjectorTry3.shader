Shader "Custom/ShadowProjectorTry3"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _AlphaBoost ("Alpha Boost", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ShadowProjector"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _AlphaBoost;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float blur : TEXCOORD1;
                float blend : TEXCOORD2;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.blur = IN.color.g;
                OUT.blend = IN.color.b;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * _AlphaBoost;
                return half4(alpha, IN.blur, IN.blend, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
