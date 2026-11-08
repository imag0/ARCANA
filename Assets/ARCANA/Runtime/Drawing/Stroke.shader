Shader "ARCANA/Stroke"
{
    Properties { _BaseColor("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 positionOS:POSITION; half4 color:COLOR; };
            struct Output { float4 positionCS:SV_POSITION; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            Output Vert(Input v){Output o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color*_BaseColor;return o;}
            half4 Frag(Output i):SV_Target {return i.color;}
            ENDHLSL
        }
    }
}
