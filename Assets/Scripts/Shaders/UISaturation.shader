// ============================================================
// UISaturation — UI shader that drains the colour out of a sprite on demand.
// WHAT & WHY: The tool bar has to say "this is the tool you can use right now"
//   without words. The usable tool sits at full colour; every other tool drops
//   to greyscale. Unity's UI has no built-in saturation control, so this is the
//   smallest shader that adds one, built on Unity's stock UI-Default so that
//   Masks, RectMask2D clipping and the Canvas stencil keep working normally.
// KEY DECISIONS:
//   - A shader property instead of a second set of desaturated PNGs. Two
//     variants per tool would be twelve sprites to keep in sync, twice the
//     atlas space, and a hard cut between states. One float animates smoothly
//     and costs a dot product.
//   - Rec. 601 luma weights (0.299, 0.587, 0.114) rather than a flat average.
//     A flat average makes the wooden tool handles muddy and the brush bristles
//     read lighter than they should; the perceptual weights keep the greyscale
//     looking like the same object with the colour removed.
//   - Saturation is applied to rgb only and never touches alpha, so a dimmed
//     tool keeps its exact silhouette and stays as clickable as a lit one.
//   - Values above 1 are allowed on purpose (the property range goes to 2), so
//     the newly-unlocked tool can be briefly over-saturated as a "you can use
//     this now" pulse without needing a second material or a tint animation.
//   - CGPROGRAM / UnityCG.cginc, not HLSL / URP includes. Canvas geometry is
//     drawn by the UI system rather than the URP forward pass, so the built-in
//     UI path is the correct one under URP - Unity's own UI-Default works this
//     way and this shader matches it.
// ============================================================
//
// ---- UNITY EDITOR SETUP (required for this shader to work) ----
// [ ] Import the tool icons correctly first. Select all six of
//     Assets/Art/cleaningAssets/dustRemover, waterSpray, deacidifier and
//     Assets/Art/LinnenAssets/squeegee, roller, pencil, then in the Inspector
//     set:
//       Texture Type = Sprite (2D and UI)
//       Sprite Mode  = Single
//       Mesh Type    = Full Rect
//       Wrap Mode    = Clamp
//     Press Apply.
// [ ] Create the material: right-click in Assets/Art -> Create -> Material.
//     Name it exactly "M_UISaturation".
// [ ] With M_UISaturation selected, set the Shader dropdown at the very top of
//     the Inspector to: Restorium -> UI -> Saturation
// [ ] Leave Saturation at 1. ToolButton copies this material at runtime and
//     animates the copy, so editing the value here changes nothing in play mode.
// [ ] Drag M_UISaturation into the Material field of each tool icon's Image,
//     and into the "Saturation Material" field on each ToolButton component.
// [ ] Sanity check without pressing Play: temporarily drag the slider on
//     M_UISaturation to 0. Every icon using it should turn grey in the Scene
//     view. Drag it back to 1 when you are done.
// ---------------------------------------------------------------

Shader "Restorium/UI/Saturation"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Saturation ("Saturation", Range(0, 2)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Saturation;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Perceptual luma, then blend back towards the original colour.
                // _Saturation 0 = fully grey, 1 = untouched, >1 = boosted.
                half luma = dot(color.rgb, half3(0.299h, 0.587h, 0.114h));
                color.rgb = lerp(luma.xxx, color.rgb, _Saturation);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
