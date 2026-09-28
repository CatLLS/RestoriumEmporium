// ============================================================
// GameSignals — tiny static event hub for moments the tutorial listens to.
// WHAT & WHY: A few gameplay moments happen inside scene views that the
//   tutorial has no business referencing (a sticker peeling, the desk hub
//   entering preview mode). Wiring an Inspector reference from the tutorial to
//   every such view would couple them both ways. These are fire-and-forget
//   notifications, so a static hub is the lightest correct tool.
// KEY DECISIONS:
//   - Only for NOTIFICATIONS with no reply and no state. Anything with state
//     (coins, ownership, progress) goes through a service interface instead.
//   - Listeners MUST unsubscribe in OnDisable/OnDestroy. Clear() is called by
//     GameBootstrap on every scene load as a safety net against leaks.
//   - Raise* methods exist so raising is one call and a null handler is safe.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Static class.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Core
{
    public static class GameSignals
    {
        /// <summary>A sticker came off. Argument: stickers still remaining.</summary>
        public static event Action<int> StickerPeeled;

        /// <summary>The desk hub entered preview mode for this itemId.</summary>
        public static event Action<string> PreviewOpened;

        /// <summary>Desk hub edit mode turned on (true) or off (false).</summary>
        public static event Action<bool> EditModeChanged;

        /// <summary>A modal overlay (pause, settings) opened (true) or closed (false).</summary>
        public static event Action<bool> ModalChanged;

        public static void RaiseStickerPeeled(int remaining) => StickerPeeled?.Invoke(remaining);
        public static void RaisePreviewOpened(string itemId) => PreviewOpened?.Invoke(itemId);
        public static void RaiseEditModeChanged(bool on) => EditModeChanged?.Invoke(on);
        public static void RaiseModalChanged(bool open) => ModalChanged?.Invoke(open);

        /// <summary>Drops every listener. Called by GameBootstrap when a scene loads.</summary>
        public static void Clear()
        {
            StickerPeeled = null;
            PreviewOpened = null;
            EditModeChanged = null;
            ModalChanged = null;
        }
    }
}
