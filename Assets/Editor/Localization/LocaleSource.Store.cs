// LocaleSource.Store — strings for The Golden Vault coin store (Figma BuyCoins 623:26).
// See LocaleSource.cs for the rules and key conventions.
// English is the Figma copy verbatim; pt-BR is a natural translation.
// Lines marked DRAFT were not in Figma and need a human review.
// ---- UNITY EDITOR SETUP: nothing. Run Restorium -> Rebuild Catalogs & Locale Tables after edits. ----

namespace RestoriumEmporium.EditorTools
{
    public static partial class LocaleSource
    {
        internal static readonly LocaleEntry[] Store =
        {
            E("ui.coins.title", "O Cofre Dourado", "The Golden Vault"),
            // Formatted by CoinPackRow: {0} = amount (coloured in code), {1} = store price.
            E("ui.coins.pack", "{0} moedas | {1}", "{0} coins | {1}"),
            E("ui.coins.finePrint",
                "cobrado na sua conta. Restaure compras quando quiser nas Configurações",
                "charged to your account. Restore purchases any time from Settings"),
            E("ui.coins.terms", "Termos", "Terms"),
            E("ui.coins.privacy", "Privacidade", "Privacy"),
            E("ui.coins.removeAds", "Remover anúncios", "Remove Ads"),
            // DRAFT (not in Figma)
            E("ui.coins.failed", "A compra não foi concluída. Tente novamente.",
                "The purchase didn't go through. Please try again."),
            E("ui.coins.unavailable", "A loja não está disponível agora.",
                "The store isn't available right now."),
        };
    }
}
