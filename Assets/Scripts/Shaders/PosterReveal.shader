// ============================================================
// PosterReveal — UI shader that draws a sprite through a painted reveal mask.
// WHAT & WHY: The whole restoration mechanic is "show the next poster state
//   wherever the player has scrubbed". That is one multiply on alpha: the top
//   Image's own alpha times the mask value under that pixel. Everything else in
//   this file is Unity's stock UI-Default, kept byte-for-byte so that Masks,
//   RectMask2D clipping, the Canvas stencil and alpha-clipping all behave
//   exactly as they do on any other UI element.
// KEY DECISIONS:
//   - Derived from UI-Default rather than written from scratch. The _Stencil*
//     properties, UNITY_UI_CLIP_RECT and UNITY_UI_ALPHACLIP are not decoration:
//     without them the poster would ignore any Mask above it in the hierarchy
//     and would punch through a RectMask2D, which is the classic "my custom UI
//     shader breaks scrolling" bug.
//   - _Invert is a float, not a shader keyword. The pencil stage needs the mask
//     read backwards (paint erases the top layer instead of revealing it). One
//     lerp does that branchlessly, with no second shader variant and therefore
//     no runtime compile hitch when that stage starts.
//   - The mask is sampled with the SAME uv as _MainTex. That is only correct
//     while the sprite's quad UVs run 0..1 across the poster, which is exactly
//     why the import settings below insist on Mesh Type = Full Rect and no
//     sprite atlas. Tight meshes shrink the quad to the opaque pixels and
//     re-map its UVs, and an atlas re-maps them into the atlas page; either one
//     makes the painted mask land somewhere other than under the finger.
//   - CGPROGRAM / UnityCG.cginc, not HLSL / URP includes. Canvas geometry is
//     rendered by the UI system, not by the URP forward pass, so the built-in
//     UI path is the correct one under URP - Unity's own UI-Default works this
//     way and this shader must match it.
//   - The mask texture is R8 and marked linear on the CPU side, so mask.r is
//     coverage in 0..1 with no colour-space conversion applied to it.
// ============================================================
//
// ---- UNITY EDITOR SETUP (required for this shader to work) ----
// [ ] IMPORT THE POSTER ART CORRECTLY FIRST. Select every sprite in
//     Assets/Art/Posters/poster1/ (posterBeforeDusting, posterNoDust,
//     posterYellowWet, posterWhiteWet, posterDry, posterFinal) and
//     Assets/Art/LinnenAssets/PosterBack, then in the Inspector set:
//       Texture Type = Sprite (2D and UI)
//       Sprite Mode  = Single
//       Mesh Type    = Full Rect     <-- REQUIRED, the default "Tight" breaks
//                                        the UV mapping this shader depends on
//       Wrap Mode    = Clamp
//     Press Apply. Do NOT put these sprites in a Sprite Atlas.
// [ ] Create the material: right-click in Assets/Art -> Create -> Material.
//     Name it "M_PosterReveal".
// [ ] With M_PosterReveal selected, set the Shader dropdown at the very top of
//     the Inspector to: Restorium -> UI -> PosterReveal
// [ ] Leave every field on the material alone. Reveal Mask stays empty and
//     Invert Mask stays 0; PosterLayerStack copies this material at runtime and
//     fills both in per stage. Editing them here has no effect in play mode.
// [ ] Drag M_PosterReveal into the Material field of the poster's "TopLayer"
//     Image (see the PosterLayerStack checklist).
// [ ] If the poster renders solid white in the Scene view, the material's
//     Reveal Mask is empty - that is expected outside play mode.
// ---------------------------------------------------------------

Shader "Restorium/UI/PosterReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _MaskTex ("Reveal Mask (R8)", 2D) = "black" {}
        _Invert ("Invert Mask", Float) = 0

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
                float2 maskcoord     : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _MaskTex;

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Invert;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                // The mask is authored in poster space and must NOT pick up the
                // sprite's tiling/offset, so it uses the raw quad uv.
                OUT.maskcoord = v.texcoord;

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // The whole mechanic, in one line: reveal where painted, or erase
                // where painted when _Invert is 1 (the pencil stage).
                half reveal = tex2D(_MaskTex, IN.maskcoord).r;
                color.a *= lerp(reveal, 1.0h - reveal, _Invert);

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
