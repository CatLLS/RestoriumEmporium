// ============================================================
// CleaningScreen — the desk view for the dust / water / deacidifier stages.
// WHAT & WHY: The first three stages all play on one layout: the desk, the
//   poster in the middle, and a three-tool bar along the bottom. This owns that
//   layout and nothing else — the header text, the root the poster stack sits
//   under, and the root the tools sit on.
// KEY DECISIONS:
//   - Holds no rules. Coverage, tool validity and stage advancement all live in
//     RestorationController; this screen subscribes to StageStarted purely to
//     retitle the header. A screen that also decided when a stage ends would be
//     a second owner of the state machine.
//   - Exposes posterStackRoot and toolBarRoot as public properties so the flow
//     controller can parent the shared poster stack under whichever screen is
//     visible, instead of every screen owning its own copy of the poster.
//   - Everything else is inherited from RestorationScreenBase, so the two
//     restoration screens cannot drift apart in how they handle the header.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform Pos X / Pos Y with the anchor preset set to
// TOP-LEFT (click the anchor square, hold Alt+Shift, pick the top-left box).
// Pos Y is NEGATIVE: type -754 where the design says y = 754.
//
// A) THE SCREEN ROOT
// [ ] Right-click Canvas -> Create Empty. Name it exactly: CleaningScreen
// [ ] Rect Transform: anchor preset stretch/stretch, Left/Right/Top/Bottom = 0.
// [ ] Add Component -> Cleaning Screen (this script).
// [ ] Untick the checkbox at the very top-left of the Inspector to start the
//     object DISABLED. The router enables it.
//
// B) CHILDREN, in this order (order = draw order, first is behind)
// [ ] Right-click CleaningScreen -> UI -> Image. Name: Background
//       Anchor stretch/stretch, Left/Right/Top/Bottom = 0.
//       Source Image = Assets/Art/LinnenAssets/LinnenBackingBG(all)
// [ ] Right-click CleaningScreen -> UI -> Image. Name: Desk
//       Anchor top-left. Pos X = -43, Pos Y = -848, Width = 498, Height = 779.
//       Source Image = Assets/Art/LinnenAssets/LinnenBackingBG(all)
//       (Swap this for the dedicated desk art when it is exported.)
// [ ] Right-click CleaningScreen -> Create Empty. Name: PosterStackRoot
//       Anchor top-left. Pos X = 45, Pos Y = -127, Width = 322, Height = 577.
//       This stays EMPTY. The poster layer stack is parented here at runtime.
// [ ] Right-click CleaningScreen -> UI -> Text - TextMeshPro. Name: Header
//       Anchor top-left. Pos X = 120, Pos Y = -62, Width = 172, Height = 24.
//       Alignment = Center + Middle. Font Size = 18.
//       Do NOT add a Localized Text component here: this script sets the text,
//       because the key changes with every stage.
// [ ] Right-click CleaningScreen -> Create Empty. Name: ToolBarRoot
//       Anchor top-left. Pos X = 0, Pos Y = -754, Width = 412, Height = 76.
// [ ] Right-click ToolBarRoot -> UI -> Image. Name: ToolsBarBG
//       Anchor top-left. Pos X = 0, Pos Y = 0, Width = 412, Height = 76.
//       Source Image = Assets/Art/LinnenAssets/toolsBarBG
// [ ] The three tools sit ON TOP of the bar, so their Pos Y is measured from
//       the CleaningScreen root, not from ToolBarRoot. Create each of them as a
//       child of ToolBarRoot but then set Pos Y relative to the bar as shown:
//       Right-click ToolBarRoot -> UI -> Button - TextMeshPro. Name: DustRemoverButton
//         Anchor top-left. Pos X = 55, Pos Y = 21, Width = 66, Height = 140.
//         Image -> Source Image = Assets/Art/cleaningAssets/dustRemover
//         Delete its child Text (TMP). Add Component -> Button Sfx, Sfx = Tool Select.
//         Add Component -> Tutorial Anchor, Anchor Id = toolbar.dustRemover
//       Right-click ToolBarRoot -> UI -> Button - TextMeshPro. Name: WaterSprayButton
//         Anchor top-left. Pos X = 161, Pos Y = 32, Width = 86, Height = 118.
//         Image -> Source Image = Assets/Art/cleaningAssets/waterSpray
//         Delete its child Text (TMP). Add Component -> Button Sfx, Sfx = Tool Select.
//         Add Component -> Tutorial Anchor, Anchor Id = toolbar.waterSpray
//       Right-click ToolBarRoot -> UI -> Button - TextMeshPro. Name: DeacidifierButton
//         Anchor top-left. Pos X = 287, Pos Y = 21, Width = 58, Height = 115.
//         Image -> Source Image = Assets/Art/cleaningAssets/deacidifier
//         Delete its child Text (TMP). Add Component -> Button Sfx, Sfx = Tool Select.
//         Add Component -> Tutorial Anchor, Anchor Id = toolbar.deacidifier
//       (Those three Pos Y values are 733-754 = 21, 722-754 = 32 and
//        733-754 = 21, i.e. the design Y minus the bar Y, then negated by the
//        top-left anchor. If a tool looks off, check the anchor preset first.)
//
// C) MAKE THE TOOL BAR DRAW OVER THE PARTICLES
// [ ] Select ToolBarRoot -> Add Component -> Canvas.
//       Tick "Override Sorting". Sorting Layer = Default. Order in Layer = 2.
// [ ] Select ToolBarRoot -> Add Component -> Graphic Raycaster.
//       (A nested Canvas needs its own raycaster or its buttons stop responding.)
// [ ] This puts the bar at order 2, the main Canvas at 0, and leaves order 1
//     free for the particle systems (see ParticleBurstPool.cs).
//
// D) WIRE THE INSPECTOR (select CleaningScreen and drag these in)
// [ ] Restoration Source <- the GameFlow object (the one with RestorationController)
// [ ] Poster Stack Root  <- the PosterStackRoot child
// [ ] Tool Bar Root      <- the ToolBarRoot child
// [ ] Header Label       <- the Header child
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;

    [DisallowMultipleComponent]
    public class CleaningScreen : RestorationScreenBase
    {
        public override GameScreen Screen => GameScreen.Cleaning;
    }
}
