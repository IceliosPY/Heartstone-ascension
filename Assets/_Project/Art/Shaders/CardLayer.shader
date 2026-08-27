Shader "CoH/Card Layer"
{
    // One shader for every layer of a card.
    //
    // It is the pipeline's own sprite shading with one addition: a second
    // texture whose alpha multiplies the first. That is what lets a rectangular
    // painting sit inside an oval window without the painting being cut up, and
    // without a second renderer or a stencil pass.
    //
    // A layer with no mask leaves _MaskTex white, and the multiply costs one
    // sample and changes nothing. So the same material draws frames, gems and
    // artwork, and there is nothing to choose between at composition time.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _MaskTex ("Mask", 2D) = "white" {}
        [PerRendererData] _MaskST ("Mask scale and offset", Vector) = (1,1,0,0)
        _Color ("Tint", Color) = (1,1,1,1)

        // Sprite renderers set these; declared so Unity does not warn.
        [HideInInspector] _RendererColor ("Renderer Colour", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float4 _MaskST;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // The mask belongs to the layer's rectangle, not to the
                // picture. A painting scaled up to cover that rectangle
                // overflows it, and a mask that scaled with the painting would
                // crop the same shape however far it overflowed - which is to
                // say it would not crop at all. So the mask has its own
                // mapping, and the overflow falls outside it.
                float2 maskUV = input.uv * _MaskST.xy + _MaskST.zw;
                colour.a *= SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).a;

                colour.rgb *= colour.a;
                return colour;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
