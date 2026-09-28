// LocaleSource.Legacy — the MVP strings (moved out of PosterAuthoringTool) with English added.
// Owned by the UI/tutorial agent. See LocaleSource.cs for the rules and key conventions.
// KEY NAMES ARE FROZEN: scenes and assets already reference every key below. Values
// may change (Batch 2 updated a few to the Figma copy, marked "B2").
// Stage/poster/tool keys are listed here because the MVP created them; if the
// restoration area file (LocaleSource.Restoration.cs) redefines one, that later
// definition wins and LocaleTableBuilder prints a duplicate notice.
// ---- UNITY EDITOR SETUP: nothing. Run Restorium -> Localization -> Rebuild Locale Tables after editing. ----

namespace RestoriumEmporium.EditorTools
{
    public static partial class LocaleSource
    {
        internal static readonly LocaleEntry[] Legacy =
        {
            // ---- Title screen ----
            E("ui.title.newGame", "Novo jogo", "New Game"),
            E("ui.title.continue", "Continuar", "Continue"),
            E("ui.title.settings", "Ajustes", "Settings"),
            E("ui.title.quit", "Sair", "Quit"),

            // ---- Journal ----
            E("ui.journal.title", "Diário de restaurações", "Restoration Journal"),
            E("ui.journal.restore", "Restaurar", "Restore"),
            E("ui.journal.changePages", "Toque para mudar de página", "Click to change pages"), // B2: Figma copy
            E("ui.journal.locked", "Em breve", "Coming soon"),
            E("ui.journal.complete", "Concluído", "Completed"),

            // ---- Cleaning ----
            E("ui.cleaning.title", "Hora de limpar!", "Time to clean!"),
            E("ui.cleaning.toolbar", "Ferramentas de limpeza", "Cleaning tools"),

            // ---- Linen backing ----
            E("ui.linenBacking.title", "Reforço de linho", "Linen backing"),
            E("ui.linenBacking.toolbar", "Ferramentas de reforço", "Backing tools"),

            // ---- Finished repair ----
            E("ui.finishedRepair.title", "Restauração concluída!", "Restoration Complete!"), // B2: Figma copy
            E("ui.finishedRepair.continue", "Continuar", "Continue"),
            E("ui.finishedRepair.backToWorkbench", "Voltar para a bancada", "Go back to workbench"),

            // ---- Dialogue ----
            E("ui.dialogue.tapToContinue", "toque para continuar", "tap to continue"),
            E("ui.dialogue.advance", "Toque para avançar o diálogo", "Tap to advance the dialogue"),

            // ---- Pause ----
            E("ui.pause.title", "JOGO PAUSADO", "GAME PAUSED"), // B2: Figma copy
            E("ui.pause.resume", "Voltar ao jogo", "Continue Restoration"),
            E("ui.pause.quit", "Sair", "Quit"), // B2: Figma copy ("Quit")

            // ---- Thanks for playing ----
            E("ui.thanks.title", "O fim?", "The end?"),
            E("ui.thanks.body",
                "Obrigada por testar o jogo! Peço desculpas por ser tão curto, estamos em fase de " +
                "desenvolvimento e ainda acertando os aspectos chave da gameplay. Em atualizações " +
                "futuras traremos mais posters e uma história misteriosa para vocês! Enquanto espera " +
                "desenvolvermos mais do jogo, que tal mandar um feedback do que achou do jogo? " +
                "encontrou algum bug? tem alguma ideia de como melhorar a gameplay? Sou toda ouvidos!",
                "Thank you for testing the game! Sorry it's so short: we're still in development and " +
                "fine-tuning the key parts of the gameplay. Future updates will bring more posters and " +
                "a mysterious story for you! While you wait for us to build more of the game, how about " +
                "sending us some feedback? Found a bug? Have an idea to improve the gameplay? I'm all ears!"),
            E("ui.thanks.continue", "Continuar", "Continue"),

            // ---- Tools ----
            E("tool.dustRemover", "Espanador", "Duster"),
            E("tool.waterSpray", "Borrifador", "Water spray"),
            E("tool.deacidifier", "Desacidificante", "Deacidifier"),
            E("tool.squeegee", "Rodo", "Squeegee"),
            E("tool.roller", "Rolo de cola", "Glue roller"),
            E("tool.pencil", "Lápis", "Pencil"),

            // ---- Poster ----
            E("poster.poster01.title", "Visite Lethe Falls", "Visit Lethe Falls"),

            // ---- Stages ----
            E("stage.dust.title", "Tirando a poeira", "Dusting off"),
            E("stage.dust.prompt", "Passe o espanador por todo o cartaz até a poeira sumir.",
                "Sweep the duster over the whole poster until the dust is gone."),
            E("stage.wash.title", "Lavagem", "Washing"),
            E("stage.wash.prompt", "Borrife água e cubra o cartaz inteirinho.",
                "Spray water until the whole poster is covered."),
            E("stage.deacidify.title", "Desacidificação", "Deacidifying"),
            E("stage.deacidify.prompt", "Espalhe o desacidificante para o papel voltar a ser branco.",
                "Spread the deacidifier so the paper turns white again."),
            E("stage.squeegee.title", "Hora de secar", "Time to dry"),
            E("stage.squeegee.prompt", "Deslize o rodo de um lado ao outro para tirar a água.",
                "Slide the squeegee from side to side to push the water out."),
            E("stage.adhesive.title", "Rolando, rolando!", "Rolling, rolling!"),
            E("stage.adhesive.prompt", "Passe o rolo com cola por todo o verso do cartaz.",
                "Roll glue over the whole back of the poster."),
            E("stage.mend.title", "Retoques finais", "Final touches"),
            E("stage.mend.prompt", "Use o lápis para completar os pedacinhos que faltam.",
                "Use the pencil to fill in the missing bits."),

            // ---- Tutorial: first_restoration (the 16 MVP steps keep their key names) ----
            // Tracy's voice: warm, unhurried, never bossy.
            E("tutorial.welcome",
                "Oi, oi! Que bom te ver por aqui. Eu sou a Tracy, e este é o Restorium Emporium. " +
                "Vamos devolver a vida a um cartaz bem antigo?",
                "Hi, hi! So glad to see you here. I'm Tracy, and this is the Restorium Emporium. " +
                "Shall we bring a very old poster back to life?"),
            E("tutorial.tapRestore",
                "Nosso primeiro trabalho está aqui no diário. Toque em Restaurar para começarmos.",
                "Our first job is right here in the journal. Tap Restore and let's begin."),
            E("tutorial.pickDustRemover",
                "Primeiro, a poeira. Pegue o espanador ali na barra de ferramentas.",
                "First, the dust. Pick up the duster from the toolbar."),
            E("tutorial.useDustRemover",
                "Agora deslize o dedo pelo cartaz, devagarinho, até tirar toda a poeira.",
                "Now slide your finger over the poster, nice and slow, until all the dust is gone."),
            E("tutorial.pickWaterSpray",
                "Muito bem! Agora pegue o borrifador de água.",
                "Very good! Now pick up the water spray."),
            E("tutorial.useWaterSpray",
                "Umedeça o cartaz inteiro. Sem pressa, o papel agradece.",
                "Dampen the whole poster. No rush, the paper will thank you."),
            E("tutorial.pickDeacidifier",
                "O papel amarelou com o tempo. Pegue o desacidificante para a gente resolver isso.",
                "The paper has yellowed over time. Grab the deacidifier and we'll fix that."),
            E("tutorial.useDeacidifier",
                "Espalhe por tudo e veja o branquinho voltar. Essa é a minha parte favorita.",
                "Spread it everywhere and watch the white come back. This is my favourite part."),
            E("tutorial.pickSqueegee",
                "Hora de secar. Pegue o rodo, por favor.",
                "Time to dry. The squeegee, please."),
            E("tutorial.useSqueegee",
                "Deslize o rodo de um lado ao outro para tirar a água que sobrou.",
                "Slide the squeegee from side to side to push out the leftover water."),
            E("tutorial.pickRoller",
                "Virei o cartaz para você. Agora pegue o rolo de cola.",
                "I flipped the poster over for you. Now pick up the glue roller."),
            E("tutorial.useRoller",
                "Passe cola por todo o verso — é ela que vai segurar o cartaz no linho.",
                "Roll glue over the whole back — that's what will hold the poster to the linen."),
            E("tutorial.pickPencil",
                "Falta pouquinho! Pegue o lápis.",
                "Almost there! Pick up the pencil."),
            E("tutorial.usePencil",
                "Agora complete com carinho os pedacinhos que se perderam pelo caminho.",
                "Now lovingly fill in the little pieces that got lost along the way."),
            // B2: steps 15/16 now mention the coins and that Continue leads to the workshop. (DRAFT — review)
            E("tutorial.congrats",
                "Olha só isso! Ficou lindo. E o trabalho rendeu umas moedinhas pra gente — viu ali?",
                "Look at that! It's beautiful. And the job earned us a few coins, too — see them there?"),
            E("tutorial.tapContinue",
                "Toque em Continuar e vamos para a minha oficina. Quero te mostrar uma coisa!",
                "Tap Continue and let's head to my workshop. There's something I want to show you!"),
        };
    }
}
