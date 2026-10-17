// Minimal project-owned TMP-compatible SDF UI shader; no TMP resource-package dependency.
Shader "TextMeshPro/Mobile/Distance Field"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _FaceColor ("Face Color", Color) = (1,1,1,1)
        _FaceDilate ("Face Dilate", Range(-1,1)) = 0
        _OutlineWidth ("Outline", Float) = 0
        _OutlineSoftness ("Softness", Float) = 0
        _WeightNormal ("Normal Weight", Float) = 0
        _WeightBold ("Bold Weight", Float) = 0.5
        _GradientScale ("Gradient", Float) = 9
        _TextureWidth ("Atlas Width", Float) = 1024
        _TextureHeight ("Atlas Height", Float) = 1024
        _ScaleRatioA ("Scale Ratio", Float) = 1
        _ScaleX ("Scale X", Float) = 1
        _ScaleY ("Scale Y", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            fixed4 _FaceColor;
            float _FaceDilate;
            float4 _ClipRect;
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 local:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color * _FaceColor; o.local = v.vertex.xy; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float distance = tex2D(_MainTex, i.uv).a;
                float width = max(fwidth(distance), 0.005);
                fixed4 color = i.color;
                color.a *= smoothstep(0.5 - width - _FaceDilate * 0.1, 0.5 + width - _FaceDilate * 0.1, distance);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.local, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
