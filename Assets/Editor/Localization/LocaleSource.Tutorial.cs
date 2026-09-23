// LocaleSource.Tutorial — Tracy's lines for the Batch 2 tutorial sequences.
// Owned by the UI/tutorial agent. Keys: tutorial.<sequenceId>.<stepId>.
// The first_restoration lines keep their MVP keys (tutorial.<stepId>) in LocaleSource.Legacy.
// EVERY line here is a DRAFT (English from the brief, pt-BR natural) — the human should review.
// Tracy's voice: warm, a little flustered, never bossy.
// ---- UNITY EDITOR SETUP: nothing. Run Restorium -> Localization -> Rebuild Locale Tables after editing. ----

namespace RestoriumEmporium.EditorTools
{
    public static partial class LocaleSource
    {
        internal static readonly LocaleEntry[] Tutorial =
        {
            // ---- deskhub_lamp (full-body Tracy in the desk hub) ----
            E("tutorial.deskhub_lamp.welcome",
                "Bem-vindo à minha oficina! É aqui que a mágica acontece... bom, quase toda.",
                "Welcome to my workshop! This is where the magic happens... well, most of it."),
            E("tutorial.deskhub_lamp.dark",
                "Hmm... está bem escurinho aqui nessa mesa, né? Eu preciso muito de uma luminária nova.",
                "Hmm... it's awfully dark over this desk, isn't it? I really need a new lamp."),
            E("tutorial.deskhub_lamp.fake",
                "Vai ver foi por isso que eu não percebi que o último cartaz era falso... " +
                "Com uma luz dessas, qualquer um deixaria passar! Né?",
                "Maybe that's why I didn't notice the last poster was a fake... " +
                "With lighting this poor, anyone could have missed it! Right?"),
            E("tutorial.deskhub_lamp.tapShop",
                "Vamos escolher uma! Toque na bolsa para abrir a loja.",
                "Let's pick one out! Tap the bag to open the shop."),
            E("tutorial.deskhub_lamp.tapLamp",
                "Ah, essa luminária é perfeita — e as moedas que você acabou de ganhar pagam certinho. Toque nela!",
                "Ooh, that lamp is perfect — and the coins you just earned cover it exactly. Tap it!"),
            E("tutorial.deskhub_lamp.drag",
                "Olha só como ficaria! Arraste para onde quiser na mesa.",
                "Here's how it would look! Drag it wherever you like on the desk."),
            E("tutorial.deskhub_lamp.buy",
                "Gostou do lugar? Toque em Comprar item e ela é nossa!",
                "Happy with the spot? Tap Buy Item and it's ours!"),
            E("tutorial.deskhub_lamp.happy",
                "Bem melhor! Agora sim eu consigo ver cada detalhezinho.",
                "Much better! Now I can finally see every little detail."),
            E("tutorial.deskhub_lamp.editMode",
                "Se quiser mudar as coisas de lugar depois, é só tocar neste botão para entrar no modo de edição.",
                "If you ever want to move things around, just tap this button to enter edit mode."),
            E("tutorial.deskhub_lamp.tapBook",
                "Agora vamos abrir o diário. Toque no livro — tenho a impressão de que chegou trabalho novo.",
                "Now, let's open the journal. Tap the book — I have a feeling new work has arrived."),

            // ---- journal_page2 ----
            E("tutorial.journal_page2.newPoster",
                "Olha! Chegou um cartaz novo enquanto a gente estava ocupada.",
                "Look! A new poster arrived while we were busy."),
            E("tutorial.journal_page2.nextPage",
                "Toque na seta para virar a página.",
                "Tap the arrow to turn the page."),
            E("tutorial.journal_page2.restore",
                "Aqui está ele. Toque em Restaurar quando estiver pronto.",
                "There it is. Tap Restore whenever you're ready."),

            // ---- stickers ----
            E("tutorial.stickers.explain",
                "Ai, ai, alguém encheu esse cartaz de adesivos! Vamos ter que tirar um por um.",
                "Oh dear, someone covered this poster in stickers! We'll have to peel them off one by one."),
            E("tutorial.stickers.peel",
                "Toque em um adesivo para descolar. Com cuidado!",
                "Tap a sticker to peel it off. Gently!"),
            E("tutorial.stickers.rest",
                "Isso mesmo! Agora os outros.",
                "Just like that! Now the rest of them."),
        };
    }
}
