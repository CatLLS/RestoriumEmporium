// LocaleSource.Flow — strings owned by the core/flow agent.
// See LocaleSource.cs for the rules and key conventions.
// ---- UNITY EDITOR SETUP: nothing. ----

namespace RestoriumEmporium.EditorTools
{
    public static partial class LocaleSource
    {
        internal static readonly LocaleEntry[] Flow =
        {
            // CutscenePlayer's optional "SkipHint" label (only on skippable videos: the book transition).
            E("ui.cutscene.skipHint", "Toque para pular", "Tap to skip"),
        };
    }
}
