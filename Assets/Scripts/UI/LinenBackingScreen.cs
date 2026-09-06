// ============================================================
// LinenBackingScreen — one script serving the front, back and final linen views.
// WHAT & WHY: The three linen stages share a background, a desk, and the same
//   three-tool bar; only the framing of the poster changes (front of the poster,
//   back of the poster, then the poster mounted on the linen and zoomed in).
//   One script with a Served Screen dropdown means three objects in the scene
//   and one file to maintain, instead of three near-identical classes.
// KEY DECISIONS:
//   - Served Screen is a serialised enum rather than three subclasses. The
//     router finds views by their Screen property, so each variant still needs
//     its own GameObject, but the behaviour is identical and duplicating a class
//     to change one return value would be pure ceremony.
//   - OnValidate clamps the dropdown to the three linen screens. The enum has
//     seven members and picking Journal here would silently steal the journal
//     route; failing loudly in the Editor is much cheaper than debugging that.
//   - linenFrame is optional and only used by the Final variant. Making it a
//     separate object rather than a mode on the poster stack keeps the zoomed-in
//     framing a layout fact the human can nudge, not a hard-coded number.
//   - Header, subscription and localisation all come from RestorationScreenBase.
//     This screen still implements no rules.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform Pos X / Pos Y with the anchor preset set to
// TOP-LEFT (click the anchor square, hold Alt+Shift, pick the top-left box).
// Pos Y is NEGATIVE: type -748 where the design says y = 748.
//
// You build THREE objects from this script. They are siblings under Canvas.
//
// A) LinenBackingFrontScreen
// [x] Right-click Canvas -> Create Empty. Name: LinenBackingFrontScreen
//       Anchor stretch/stretch, Left/Right/Top/Bottom = 0. Start it DISABLED
//       (untick the box at the top-left of the Inspector).
// [x] Add Component -> Linen Backing Screen. Set Served Screen = Linen Backing Front.
// [x] Right-click it -> UI -> Image. Name: Background
//       Anchor stretch/stretch, all offsets 0.
//       Source Image = Assets/Art/LinnenAssets/LinnenBackingBG(all)
// [x] Right-click it -> UI -> Image. Name: Desk
//       Anchor top-left. Pos X = -43, Pos Y = -848, Width = 498, Height = 779.
//       Source Image = Assets/Art/LinnenAssets/LinnenBackingBG(all)
// [x] Right-click it -> Create Empty. Name: PosterStackRoot
//       Anchor top-left. Pos X = 45, Pos Y = -127, Width = 322, Height = 577.
//       Leave it EMPTY; the poster stack is parented here at runtime.
// [x] Right-click it -> UI -> Text - TextMeshPro. Name: Header
//       Anchor top-left. Pos X = 98, Pos Y = -62, Width = 216, Height = 24.
//       Alignment = Center + Middle. Font Size = 18. No Localized Text here.
// [x] Right-click it -> Create Empty. Name: ToolBarRoot
//       Anchor top-left. Pos X = 0, Pos Y = -748, Width = 412, Height = 76.
// [x] Right-click ToolBarRoot -> UI -> Image. Name: ToolsBarBG
//       Anchor top-left. Pos X = 0, Pos Y = 0, Width = 412, Height = 76.
//       Source Image = Assets/Art/LinnenAssets/toolsBarBG
// [x] Right-click ToolBarRoot -> UI -> Button - TextMeshPro. Name: SqueegeeButton
//       Anchor top-left. Pos X = 19, Pos Y = 11, Width = 120, Height = 121.
//       Image -> Source Image = Assets/Art/LinnenAssets/squeegee
//       Delete its child Text (TMP). Add Component -> Button Sfx, Sfx = Tool Select.
//       Add Component -> Tutorial Anchor, Anchor Id = toolbar.squeegee
// [x] Right-click ToolBarRoot -> UI -> Button - TextMeshPro. Name: RollerButton
//       Anchor top-left. Pos X = 147, Pos Y = 0, Width = 136, Height = 135.
//       Image -> Source Image = Assets/Art/LinnenAssets/roller
//       Delete its child Text (TMP). Add Component -> Button Sfx, Sfx = Tool Select.
//       Add Component -> Tutorial Anchor, Anchor Id = toolbar.roller
// [x] Right-click ToolBarRoot -> UI -> Button - TextMeshPro. Name: PencilButton
//       Anchor top-left. Pos X = 315, Pos Y = -26, Width = 38, Height = 135.
//       Image -> Source Image = Assets/Art/LinnenAssets/pencil
//       Delete its child Text (TMP). Add Component -> Button Sfx, Sfx = Tool Select.
//       Add Component -> Tutorial Anchor, Anchor Id = toolbar.pencil
//       (Those Pos Y values are the design Y minus the bar Y of 748:
//        759-748 = 11, 748-748 = 0, 722-748 = -26.)
// [x] Select ToolBarRoot -> Add Component -> Canvas. Tick Override Sorting,
//       Sorting Layer = Default, Order in Layer = 2.
//       Then Add Component -> Graphic Raycaster.
// [x] Inspector wiring on LinenBackingFrontScreen:
//       Restoration Source <- the GameFlow object
//       Poster Stack Root  <- PosterStackRoot
//       Tool Bar Root      <- ToolBarRoot
//       Header Label       <- Header
//       Linen Frame        <- leave EMPTY
//
// B) LinenBackingBackScreen
// [x] Select LinenBackingFrontScreen -> Ctrl+D to duplicate.
//       Rename the copy to: LinenBackingBackScreen
// [x] Set Served Screen = Linen Backing Back.
// [x] Select its PosterStackRoot: Pos X = 46, Pos Y = -117,
//       Width = 321, Height = 574.
// [x] Select its Header: Pos X = 110, Pos Y = -69, Width = 192, Height = 24.
// [x] Everything else stays as duplicated. Re-check that Restoration Source,
//       Poster Stack Root, Tool Bar Root and Header Label now point at THIS
//       object children, not the front screen ones (duplication normally keeps
//       them internal, but verify — a cross-wired reference is invisible).
//
// C) LinenBackingFinalScreen
// [x] Duplicate LinenBackingFrontScreen again. Rename to: LinenBackingFinalScreen
// [x] Set Served Screen = Linen Backing Final.
// [x] Right-click it -> UI -> Image. Name: LinenFrame
//       Anchor top-left. Pos X = 19, Pos Y = -61, Width = 375, Height = 638.
//       Source Image = Assets/Art/LinnenAssets/linnenBacking
//       Drag LinenFrame so it sits ABOVE PosterStackRoot in the Hierarchy
//       (it must draw behind the poster).
// [x] Select its PosterStackRoot: Pos X = 47, Pos Y = -101,
//       Width = 318, Height = 567.
// [x] Inspector: drag LinenFrame into the Linen Frame field.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;

    [DisallowMultipleComponent]
    public class LinenBackingScreen : RestorationScreenBase
    {
        [Header("Variant")]
        [Tooltip("Which of the three linen views this object is. Must be one of " +
                 "Linen Backing Front / Back / Final, and unique in the scene.")]
        [SerializeField] private GameScreen servedScreen = GameScreen.LinenBackingFront;

        [Header("Final view only")]
        [Tooltip("The framed linen the poster is mounted on. Used by the Final " +
                 "variant; leave empty on the Front and Back variants.")]
        [SerializeField] private RectTransform linenFrame;

        public override GameScreen Screen => servedScreen;

        /// <summary>The linen frame rect, or null on the front and back variants.</summary>
        public RectTransform LinenFrame => linenFrame;

        private void OnValidate()
        {
            if (servedScreen == GameScreen.LinenBackingFront ||
                servedScreen == GameScreen.LinenBackingBack ||
                servedScreen == GameScreen.LinenBackingFinal)
            {
                return;
            }

            Debug.LogError(
                "[LinenBackingScreen] Served Screen must be LinenBackingFront, " +
                "LinenBackingBack or LinenBackingFinal. Resetting to Front.", this);

            servedScreen = GameScreen.LinenBackingFront;
        }
    }
}
