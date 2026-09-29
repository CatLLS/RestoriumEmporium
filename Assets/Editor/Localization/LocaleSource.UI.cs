// LocaleSource.UI — Batch 2 UI strings owned by the UI/tutorial agent (pause, settings,
// journal pages, finished repair). English is the Figma copy verbatim; pt-BR is a natural
// translation. Lines marked DRAFT were not in Figma and need the human's review.
// See LocaleSource.cs for the rules and key conventions.
// ---- UNITY EDITOR SETUP: nothing. Run Restorium -> Localization -> Rebuild Locale Tables after editing. ----

namespace RestoriumEmporium.EditorTools
{
    public static partial class LocaleSource
    {
        internal static readonly LocaleEntry[] UI =
        {
            // ---- Game Paused overlay (Figma GamePausedOverlay 283:177) ----
            // ui.pause.title / ui.pause.quit / ui.pause.resume live in LocaleSource.Legacy.
            E("ui.pause.journal", "Diário", "Journal"),
            E("ui.pause.settings", "Ajustes", "Settings"),
            E("ui.pause.continue", "Continuar", "Continue"),
            E("ui.pause.flavour",
                "Sem pressa. Os mistérios por trás destes papéis vão esperar a sua volta.",
                "Take your time, Mysteries that lie behind these papers shall wait for your return."),

            // ---- Settings overlay (Figma SettingsScene 300:298) ----
            E("ui.settings.title", "AJUSTES", "SETTINGS"),
            E("ui.settings.tracyLine",
                "Ah, o restorium precisa de uns ajustes? Foi mal, vamos mudar isso agora mesmo!",
                "Oh, the restorium needs adjusting? My bad, let's change it right away!"),
            E("ui.settings.language", "Idioma", "Language"),
            E("ui.settings.sfx", "Efeitos sonoros", "SFX Audio"),
            E("ui.settings.music", "Música", "Music Audio"),
            // Optional extra (DRAFT): reset progress with a confirmation step.
            E("ui.settings.resetProgress", "Apagar progresso", "Reset progress"),
            E("ui.settings.resetConfirm",
                "Apagar todo o progresso? Suas moedas, decorações e cartazes restaurados serão perdidos.",
                "Erase all progress? Your coins, decorations and restored posters will be lost."),
            E("ui.settings.resetYes", "Apagar", "Erase"),
            E("ui.settings.resetNo", "Cancelar", "Cancel"),

            // ---- Journal (Figma JounalPage 173:164) ----
            // ui.journal.restore / ui.journal.changePages live in LocaleSource.Legacy.
            E("ui.journal.continue", "Continuar", "Continue"),
            E("ui.journal.restored", "Restaurado", "Restored"),
            E("ui.journal.back", "Voltar para a bancada", "Go back to workbench"),
            E("ui.journal.lockedHint", "Restaure o cartaz anterior primeiro", "Restore the previous poster first"), // DRAFT

            // ---- Finished Repair (Figma FinishedRepairBG 342:497) ----
            // ui.finishedRepair.title / .continue live in LocaleSource.Legacy.
            E("ui.finishedRepair.double", "Dobrar recompensa", "Double Reward"),
            E("ui.finishedRepair.or", "ou", "or"),
            E("ui.finishedRepair.coins", "+{0}", "+{0}"),
        };
    }
}
