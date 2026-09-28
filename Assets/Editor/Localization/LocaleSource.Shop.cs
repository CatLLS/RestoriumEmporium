// LocaleSource.Shop — strings owned by the shop/desk-hub agent.
// See LocaleSource.cs for the rules and key conventions.
// English is the Figma copy verbatim; pt-BR is a natural translation.
// Figma's curly apostrophe (’, U+2019) is written as a straight ' because the TMP
// font atlases are generated for Latin-1 only (SETUP.md Part 2) and would show a box.
// Lines marked DRAFT were not in Figma and need a human review.
// ---- UNITY EDITOR SETUP: nothing. Run Restorium -> Rebuild Catalogs & Locale Tables after edits. ----

namespace RestoriumEmporium.EditorTools
{
    public static partial class LocaleSource
    {
        internal static readonly LocaleEntry[] Shop =
        {
            // ---- Desk hub: shared by Edit and Preview panels (formatted from code) ----
            E("ui.desk.selected", "Selecionado: {0}", "Selected: {0}"),
            E("ui.desk.desc", "Desc.: {0}", "Desc.: {0}"),

            // ---- Desk hub: Edit mode ----
            E("ui.edit.title", "Modo de Edição", "Edit Mode"),
            E("ui.edit.hint", "Selecione um item para mudar a posição dele.", "Select an item to edit its position."),
            E("ui.edit.dragHint", "Toque e arraste o item para posicioná-lo", "Click and drag around the item to position it"),
            E("ui.edit.place", "Colocar Item", "Place Item"),
            E("ui.edit.undo", "Desfazer", "Undo"),
            E("ui.edit.or", "ou", "or"),
            E("ui.edit.backToWorkshop", "Voltar à oficina", "Back to workshop"),

            // ---- Desk hub: Preview mode ----
            E("ui.preview.title", "Modo de Prévia", "Preview Mode"),
            E("ui.preview.dragHint", "Toque e arraste o item para posicioná-lo", "Click and drag around the item to position it"),
            E("ui.preview.price", "Preço: {0} moedas", "Price: {0} coins"),
            E("ui.preview.buy", "Comprar Item", "Buy Item"),
            E("ui.preview.giveUp", "Desistir", "Give up"),
            // DRAFT (not in Figma)
            E("ui.preview.notEnoughCoins",
                "Ainda não tenho moedas suficientes... Restaurar outro pôster deve ajudar!",
                "Not enough coins yet... restoring another poster should help!"),

            // ---- Shop ----
            E("ui.shop.back", "Voltar à bancada", "Go back to workbench"),
            E("ui.shop.titleTop", "Empório da", "Tracy's"),          // pt-BR split DRAFT
            E("ui.shop.titleMain", "Tracy", "Emporium Shop"),        // pt-BR split DRAFT
            E("ui.shop.removeAds", "Remover anúncios", "Remove ads"),
            E("ui.shop.buyMoreCoins", "Comprar Moedas", "Buy More Coins"),
            E("ui.shop.tabDecor", "Decoração", "Décor"),
            E("ui.shop.tabMisc", "Diversos", "Misc."),
            // DRAFT (not in Figma)
            E("ui.shop.owned", "Comprado", "Owned"),
            E("ui.shop.empty", "Mais itens em breve!", "More items coming soon!"),

            // ---- Shop items (ShopItemData.nameKey / descriptionKey) ----
            E("shop.item.lamp.name", "Luminária", "Lamp"),
            // DRAFT: the Figma file has no lamp description.
            E("shop.item.lamp.desc",
                "Uma luminária quentinha e bem clara para a bancada. Com essa luz, você nunca mais vai deixar um pôster falso passar despercebido!",
                "A warm, bright lamp for the workbench. With light like this, you'll never miss that a poster is a fake again!"),

            E("shop.item.plant.name", "Vaso de Planta", "Plant Pot"),
            E("shop.item.plant.desc",
                "Este vaso de planta lindinho foi encontrado numa loja perto da oficina, é muito bonito!",
                "This pretty plant pot décor was found at a store near the workshop, it's really pretty!"),

            E("shop.item.books.name", "Pilha de Livros", "Book Pile"),
            E("shop.item.books.desc",
                "Você começou a ler livros de mistério sobre viagem no tempo, então comprou uma pilha para deixar na oficina!",
                "You've recently gotten into reading mystery books about time travel, so you got yourself a pile to keep at the workshop!"),
        };
    }
}
