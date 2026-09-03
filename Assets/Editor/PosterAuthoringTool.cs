// ============================================================
// PosterAuthoringTool — one menu click that authors every data asset the MVP needs.
// WHAT & WHY: Poster 1 needs six ToolData assets, six RestorationStageData assets,
//   one PosterData, sixteen TutorialStepData assets and a pt-BR LocaleTable — about
//   thirty ScriptableObjects with roughly fifty sprite references between them. The
//   human building this game has never opened Unity before, and hand-creating that
//   by right-clicking through Create menus is both miserable and impossible to get
//   right. This tool creates and wires all of it from the menu Restorium -> Create
//   Poster 1 Data, and is the ONLY place Portuguese copy is allowed to live in code,
//   because Portuguese copy is precisely what it is authoring.
// KEY DECISIONS:
//   - Idempotent by construction: every asset is LoadAssetAtPath'd first and only
//     CreateAsset'd when genuinely absent, then its fields are overwritten. Running
//     the menu twice updates in place and never produces "Poster01 1.asset". That
//     also means the human can safely re-run it after exporting new art.
//   - A missing sprite is a warning that names the exact expected path, never an
//     abort. posterFinal.png and friends are still being exported, and a half-built
//     data set the human can finish by hand beats no data set at all. The run
//     finishes and reports how many references it could not fill.
//   - The sprite loader distinguishes "the png is not on disk" from "the png is on
//     disk but is imported as a Texture rather than a Sprite", because those have
//     completely different fixes and the second one is the mistake a Unity beginner
//     actually makes. A separate, opt-in menu item fixes the import settings.
//   - Folders are created BEFORE StartAssetEditing. Creating folders inside a
//     Start/StopAssetEditing pair is unreliable, and asset creation into a folder
//     that does not exist yet fails silently.
//   - Stage assets are held in a local array and handed straight to PosterData
//     rather than re-loaded by path, because assets created inside a
//     StartAssetEditing block are not queryable until StopAssetEditing.
//   - The LocaleTable is rebuilt wholesale rather than merged. It is generated
//     content with one owner; merging would quietly keep stale keys alive after a
//     rename and leave the human debugging a string that no longer exists.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing to attach. This is an Editor-only tool.
// [ ] In the Unity menu bar at the top of the window, click
//     "Restorium" -> "Create Poster 1 Data". Wait for the progress bar.
// [ ] Open the Console window (menu Window -> General -> Console) and read the
//     summary line it prints. If it mentions missing sprites, it names the exact
//     file path it wanted for each one.
// [ ] If the Console complains that a png "is not imported as a Sprite", run
//     "Restorium" -> "Fix Art Import Settings (Sprites)" once, then run
//     "Create Poster 1 Data" again.
// [ ] After the run, look in the Project window under Assets/Data/. You should see
//     Tools/, Poster1/, Poster1/Stages/, Tutorial/ and Localization/ filled in.
// [ ] Drag Assets/Data/Poster1/Poster01.asset into RestorationController -> Poster.
// [ ] Drag Assets/Data/Localization/pt-BR.asset into LocalizationService -> Tables.
// [ ] Drag the sixteen Assets/Data/Tutorial/ assets, in their numbered order, into
//     TutorialController -> Steps.
// [ ] Drag the six Assets/Data/Tools/ assets into the tool bar's tool list.
// ---------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Localization;

    public static class PosterAuthoringTool
    {
        // ---- Folders -------------------------------------------------------
        private const string DataRoot = "Assets/Data";
        private const string ToolsFolder = DataRoot + "/Tools";
        private const string PosterFolder = DataRoot + "/Poster1";
        private const string StagesFolder = PosterFolder + "/Stages";
        private const string TutorialFolder = DataRoot + "/Tutorial";
        private const string LocalizationFolder = DataRoot + "/Localization";

        // ---- Art ------------------------------------------------------------
        private const string CleaningArt = "Assets/Art/cleaningAssets";
        private const string LinenArt = "Assets/Art/LinnenAssets";
        private const string Poster1Art = "Assets/Art/Posters/poster1";
        private const string JournalArt = "Assets/Art/journalAssets";

        private const string PosterId = "poster01";

        private static int _missingSprites;
        private static readonly List<string> MissingReport = new List<string>();

        // =====================================================================
        //  MENU ENTRY POINT
        // =====================================================================

        [MenuItem("Restorium/Create Poster 1 Data", false, 100)]
        public static void CreatePoster1Data()
        {
            _missingSprites = 0;
            MissingReport.Clear();

            // Folders first: creating them inside a Start/StopAssetEditing pair is
            // unreliable, and CreateAsset into a missing folder fails quietly.
            EnsureFolder(DataRoot);
            EnsureFolder(ToolsFolder);
            EnsureFolder(PosterFolder);
            EnsureFolder(StagesFolder);
            EnsureFolder(TutorialFolder);
            EnsureFolder(LocalizationFolder);

            AssetDatabase.StartAssetEditing();

            try
            {
                CreateTools();
                var stages = CreateStages();
                CreatePoster(stages);
                CreateTutorialSteps();
                CreateLocaleTable();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_missingSprites == 0)
            {
                Debug.Log(
                    "[PosterAuthoringTool] Done. Every data asset under Assets/Data/ was created or " +
                    "updated and every sprite reference was filled in. Nothing else to do here.");
                return;
            }

            Debug.LogWarning(
                $"[PosterAuthoringTool] Done, but {_missingSprites} sprite reference(s) could not be " +
                "filled. Everything else was created and saved — the data set is usable, those slots " +
                "are simply empty in the Inspector.\n" +
                "Missing:\n  " + string.Join("\n  ", MissingReport) + "\n" +
                "Export or re-import the files above, then run Restorium -> Create Poster 1 Data " +
                "again; it updates the existing assets instead of duplicating them.\n" +
                "NOTE: posterFinal.png is still being exported by hand, so it is expected to be " +
                "missing on the first run. That only leaves the pencil stage's 'To Sprite' and " +
                "PosterData's 'Final Sprite' empty.");
        }

        // =====================================================================
        //  TOOLS
        // =====================================================================

        private struct ToolDef
        {
            public string assetName;
            public ToolId id;
            public string nameKey;
            public string iconPath;
            public SfxId loopSfx;
            public float brushRadiusUv;
        }

        private static void CreateTools()
        {
            var defs = new[]
            {
                new ToolDef
                {
                    assetName = "01_DustRemover", id = ToolId.DustRemover, nameKey = "tool.dustRemover",
                    iconPath = CleaningArt + "/dustRemover.png", loopSfx = SfxId.ToolDustRemover,
                    brushRadiusUv = 0.12f
                },
                new ToolDef
                {
                    assetName = "02_WaterSpray", id = ToolId.WaterSpray, nameKey = "tool.waterSpray",
                    iconPath = CleaningArt + "/waterSpray.png", loopSfx = SfxId.ToolWaterSpray,
                    brushRadiusUv = 0.14f
                },
                new ToolDef
                {
                    assetName = "03_Deacidifier", id = ToolId.Deacidifier, nameKey = "tool.deacidifier",
                    iconPath = CleaningArt + "/deacidifier.png", loopSfx = SfxId.ToolDeacidifier,
                    brushRadiusUv = 0.14f
                },
                new ToolDef
                {
                    assetName = "04_Squeegee", id = ToolId.Squeegee, nameKey = "tool.squeegee",
                    iconPath = LinenArt + "/squeegee.png", loopSfx = SfxId.ToolSqueegee,
                    brushRadiusUv = 0.16f
                },
                new ToolDef
                {
                    assetName = "05_Roller", id = ToolId.Roller, nameKey = "tool.roller",
                    iconPath = LinenArt + "/roller.png", loopSfx = SfxId.ToolRoller,
                    brushRadiusUv = 0.18f
                },
                new ToolDef
                {
                    assetName = "06_Pencil", id = ToolId.Pencil, nameKey = "tool.pencil",
                    iconPath = LinenArt + "/pencil.png", loopSfx = SfxId.ToolPencil,
                    brushRadiusUv = 0.06f
                }
            };

            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                var asset = LoadOrCreate<ToolData>($"{ToolsFolder}/{def.assetName}.asset");

                asset.id = def.id;
                asset.nameKey = def.nameKey;
                asset.icon = LoadSprite(def.iconPath, $"{def.assetName} icon");
                asset.loopSfx = def.loopSfx;
                asset.brushRadiusUv = def.brushRadiusUv;

                EditorUtility.SetDirty(asset);
            }
        }

        // =====================================================================
        //  STAGES
        // =====================================================================

        private static RestorationStageData[] CreateStages()
        {
            var stages = new RestorationStageData[6];

            // 1 — dust
            stages[0] = BuildStage(
                assetName: "01_Dust", stageId: "dust", screen: GameScreen.Cleaning,
                tool: ToolId.DustRemover,
                fromPath: Poster1Art + "/posterBeforeDusting.png",
                toPath: Poster1Art + "/posterNoDust.png",
                coverage: 0.85f, invert: false, onComplete: StageTransition.None,
                tint: Color.white);

            // 2 — wash
            stages[1] = BuildStage(
                assetName: "02_Wash", stageId: "wash", screen: GameScreen.Cleaning,
                tool: ToolId.WaterSpray,
                fromPath: Poster1Art + "/posterNoDust.png",
                toPath: Poster1Art + "/posterYellowWet.png",
                coverage: 0.85f, invert: false, onComplete: StageTransition.None,
                tint: Color.white);

            // 3 — deacidify, then the tool bar swaps to the linen set
            stages[2] = BuildStage(
                assetName: "03_Deacidify", stageId: "deacidify", screen: GameScreen.Cleaning,
                tool: ToolId.Deacidifier,
                fromPath: Poster1Art + "/posterYellowWet.png",
                toPath: Poster1Art + "/posterWhiteWet.png",
                coverage: 0.85f, invert: false, onComplete: StageTransition.SwapToLinenTools,
                tint: Color.white);

            // 4 — squeegee, then the poster flips over
            stages[3] = BuildStage(
                assetName: "04_Squeegee", stageId: "squeegee", screen: GameScreen.LinenBackingFront,
                tool: ToolId.Squeegee,
                fromPath: Poster1Art + "/posterWhiteWet.png",
                toPath: Poster1Art + "/posterDry.png",
                coverage: 0.85f, invert: false, onComplete: StageTransition.FlipToBack,
                tint: Color.white);

            // 5 — adhesive on the back. From and To are the SAME sprite: the only
            // visible difference is the tint, which is how wet glue reads without
            // needing a second painting of the poster's back.
            stages[4] = BuildStage(
                assetName: "05_Adhesive", stageId: "adhesive", screen: GameScreen.LinenBackingBack,
                tool: ToolId.Roller,
                fromPath: LinenArt + "/PosterBack.png",
                toPath: LinenArt + "/PosterBack.png",
                coverage: 0.80f, invert: false, onComplete: StageTransition.FlipToFrontAndMount,
                tint: new Color(1f, 0.972f, 0.902f, 0.85f));

            // 6 — mend. Inverted: the pencil ERASES the dry layer to expose the
            // finished poster underneath, so drawing feels like adding line work.
            stages[5] = BuildStage(
                assetName: "06_Mend", stageId: "mend", screen: GameScreen.LinenBackingFinal,
                tool: ToolId.Pencil,
                fromPath: Poster1Art + "/posterDry.png",
                toPath: Poster1Art + "/posterFinal.png",
                coverage: 0.60f, invert: true, onComplete: StageTransition.GoToFinishedRepair,
                tint: Color.white);

            return stages;
        }

        private static RestorationStageData BuildStage(
            string assetName, string stageId, GameScreen screen, ToolId tool,
            string fromPath, string toPath, float coverage, bool invert,
            StageTransition onComplete, Color tint)
        {
            var asset = LoadOrCreate<RestorationStageData>($"{StagesFolder}/{assetName}.asset");

            asset.stageId = stageId;
            asset.screen = screen;
            asset.requiredTool = tool;
            asset.fromSprite = LoadSprite(fromPath, $"stage '{stageId}' From Sprite");
            asset.toSprite = LoadSprite(toPath, $"stage '{stageId}' To Sprite");
            asset.revealTint = tint;
            asset.invertMask = invert;
            asset.requiredCoverage = coverage;
            asset.brushRadiusOverride = 0f;
            asset.onComplete = onComplete;
            asset.titleKey = $"stage.{stageId}.title";
            asset.tutorialKey = $"stage.{stageId}.prompt";
            asset.completeSfx = SfxId.StageComplete;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // =====================================================================
        //  POSTER
        // =====================================================================

        private static void CreatePoster(RestorationStageData[] stages)
        {
            var asset = LoadOrCreate<PosterData>($"{PosterFolder}/Poster01.asset");

            asset.posterId = PosterId;
            asset.titleKey = "poster.poster01.title";
            asset.journalThumbnail = LoadSprite(
                JournalArt + "/posterBeforeDusting(30opacity,beforeRestoring).png",
                "PosterData Journal Thumbnail");
            asset.beforeSprite = LoadSprite(
                Poster1Art + "/posterBeforeDusting.png", "PosterData Before Sprite");
            asset.finalSprite = LoadSprite(
                Poster1Art + "/posterFinal.png", "PosterData Final Sprite");
            asset.backSprite = LoadSprite(
                LinenArt + "/PosterBack.png", "PosterData Back Sprite");
            asset.linenBackingSprite = LoadSprite(
                LinenArt + "/linnenBacking.png", "PosterData Linen Backing Sprite");
            asset.stages = stages;
            asset.coinReward = 50;

            EditorUtility.SetDirty(asset);
        }

        // =====================================================================
        //  TUTORIAL STEPS
        // =====================================================================

        private struct StepDef
        {
            public string assetName;
            public string stepId;
            public TracyMood mood;
            public string anchor;
            public bool gate;
            public float pointerDelay;
            public TutorialAdvance advance;
            public GameScreen screen;
            public ToolId tool;
            public float timeout;
        }

        private static void CreateTutorialSteps()
        {
            var defs = new[]
            {
                // ---- Journal ----
                Step("01_Welcome", "welcome", TracyMood.Happy, "", false, 0f,
                    TutorialAdvance.TapAnywhere, 25f),
                Step("02_TapRestore", "tapRestore", TracyMood.Still, "journal.restoreButton", true, 0.6f,
                    TutorialAdvance.TapTarget, 25f),

                // ---- Cleaning ----
                StepTool("03_PickDustRemover", "pickDustRemover", "toolbar.dustRemover",
                    ToolId.DustRemover, 25f),
                StepDrag("04_UseDustRemover", "useDustRemover", 90f),

                StepTool("05_PickWaterSpray", "pickWaterSpray", "toolbar.waterSpray",
                    ToolId.WaterSpray, 25f),
                StepDrag("06_UseWaterSpray", "useWaterSpray", 90f),

                StepTool("07_PickDeacidifier", "pickDeacidifier", "toolbar.deacidifier",
                    ToolId.Deacidifier, 25f),
                StepDrag("08_UseDeacidifier", "useDeacidifier", 90f),

                // ---- Linen backing ----
                StepTool("09_PickSqueegee", "pickSqueegee", "toolbar.squeegee",
                    ToolId.Squeegee, 25f),
                StepDrag("10_UseSqueegee", "useSqueegee", 90f),

                StepTool("11_PickRoller", "pickRoller", "toolbar.roller",
                    ToolId.Roller, 25f),
                StepDrag("12_UseRoller", "useRoller", 90f),

                StepTool("13_PickPencil", "pickPencil", "toolbar.pencil",
                    ToolId.Pencil, 25f),
                StepDrag("14_UsePencil", "usePencil", 120f),

                // ---- Finished repair ----
                Step("15_Congrats", "congrats", TracyMood.Happy, "", false, 0f,
                    TutorialAdvance.TapAnywhere, 20f),
                Step("16_TapContinue", "tapContinue", TracyMood.Happy,
                    "finishedRepair.continueButton", true, 0.6f, TutorialAdvance.TapTarget, 25f)
            };

            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                var asset = LoadOrCreate<TutorialStepData>($"{TutorialFolder}/{def.assetName}.asset");

                asset.stepId = def.stepId;
                asset.lineKey = $"tutorial.{def.stepId}";
                asset.mood = def.mood;
                asset.targetAnchorId = def.anchor;
                asset.gateInputToTarget = def.gate;
                asset.pointerDelay = def.pointerDelay;
                asset.advance = def.advance;
                asset.requiredScreen = def.screen;
                asset.requiredTool = def.tool;
                asset.autoAdvanceSeconds = def.timeout;

                EditorUtility.SetDirty(asset);
            }
        }

        private static StepDef Step(
            string assetName, string stepId, TracyMood mood, string anchor, bool gate,
            float pointerDelay, TutorialAdvance advance, float timeout)
        {
            return new StepDef
            {
                assetName = assetName, stepId = stepId, mood = mood, anchor = anchor,
                gate = gate, pointerDelay = pointerDelay, advance = advance,
                screen = GameScreen.None, tool = ToolId.None, timeout = timeout
            };
        }

        /// <summary>"Pick up this tool" beat: hand on the tool button, ends on ToolSelected.</summary>
        private static StepDef StepTool(
            string assetName, string stepId, string anchor, ToolId tool, float timeout)
        {
            return new StepDef
            {
                assetName = assetName, stepId = stepId, mood = TracyMood.Still, anchor = anchor,
                gate = true, pointerDelay = 0.5f, advance = TutorialAdvance.ToolSelected,
                screen = GameScreen.None, tool = tool, timeout = timeout
            };
        }

        /// <summary>
        /// "Now drag on the poster" beat: hand on the poster, ends on StageCompleted.
        /// Input is NOT gated — the player has to be able to drag freely across the
        /// whole poster, and gating adds nothing when the poster is the only target.
        /// </summary>
        private static StepDef StepDrag(string assetName, string stepId, float timeout)
        {
            return new StepDef
            {
                assetName = assetName, stepId = stepId, mood = TracyMood.Still,
                anchor = "poster.surface", gate = false, pointerDelay = 1.2f,
                advance = TutorialAdvance.StageCompleted, screen = GameScreen.None,
                tool = ToolId.None, timeout = timeout
            };
        }

        // =====================================================================
        //  LOCALISATION (pt-BR)
        // =====================================================================

        private static void CreateLocaleTable()
        {
            var table = LoadOrCreate<LocaleTable>($"{LocalizationFolder}/pt-BR.asset");

            table.localeCode = "pt-BR";
            table.displayName = "Portugues (Brasil)";
            table.entries.Clear();

            // ---- Title screen ----
            Add(table, "ui.title.newGame", "Novo jogo");
            Add(table, "ui.title.continue", "Continuar");
            Add(table, "ui.title.settings", "Ajustes");
            Add(table, "ui.title.quit", "Sair");

            // ---- Journal ----
            Add(table, "ui.journal.title", "Diário de restaurações");
            Add(table, "ui.journal.restore", "Restaurar");
            Add(table, "ui.journal.changePages", "Toque para mudar de página");
            Add(table, "ui.journal.locked", "Em breve");
            Add(table, "ui.journal.complete", "Concluído");

            // ---- Cleaning ----
            Add(table, "ui.cleaning.title", "Hora de limpar!");
            Add(table, "ui.cleaning.toolbar", "Ferramentas de limpeza");

            // ---- Linen backing ----
            Add(table, "ui.linenBacking.title", "Reforço de linho");
            Add(table, "ui.linenBacking.toolbar", "Ferramentas de reforço");

            // ---- Finished repair ----
            Add(table, "ui.finishedRepair.title", "Restauração concluída!");
            Add(table, "ui.finishedRepair.continue", "Continuar");
            Add(table, "ui.finishedRepair.backToWorkbench", "Voltar para a bancada");

            // ---- Dialogue ----
            Add(table, "ui.dialogue.tapToContinue", "toque para continuar");
            Add(table, "ui.dialogue.advance", "Toque para avançar o diálogo");

            // ---- Pause ----
            Add(table, "ui.pause.title", "Jogo pausado");
            Add(table, "ui.pause.resume", "Voltar ao jogo");
            Add(table, "ui.pause.quit", "Sair do jogo");

            // ---- Thanks for playing ----
            Add(table, "ui.thanks.title", "O fim?");
            Add(table, "ui.thanks.body",
                "Obrigada por testar o jogo! Peço desculpas por ser tão curto, estamos em fase de " +
                "desenvolvimento e ainda acertando os aspectos chave da gameplay. Em atualizações " +
                "futuras traremos mais posters e uma história misteriosa para vocês! Enquanto espera " +
                "desenvolvermos mais do jogo, que tal mandar um feedback do que achou do jogo? " +
                "encontrou algum bug? tem alguma ideia de como melhorar a gameplay? Sou toda ouvidos!");
            Add(table, "ui.thanks.continue", "Continuar");

            // ---- Tools ----
            Add(table, "tool.dustRemover", "Espanador");
            Add(table, "tool.waterSpray", "Borrifador");
            Add(table, "tool.deacidifier", "Desacidificante");
            Add(table, "tool.squeegee", "Rodo");
            Add(table, "tool.roller", "Rolo de cola");
            Add(table, "tool.pencil", "Lápis");

            // ---- Poster ----
            Add(table, "poster.poster01.title", "Cartaz nº 1");

            // ---- Stages ----
            Add(table, "stage.dust.title", "Tirando a poeira");
            Add(table, "stage.dust.prompt", "Passe o espanador por todo o cartaz até a poeira sumir.");

            Add(table, "stage.wash.title", "Lavagem");
            Add(table, "stage.wash.prompt", "Borrife água e cubra o cartaz inteirinho.");

            Add(table, "stage.deacidify.title", "Desacidificação");
            Add(table, "stage.deacidify.prompt", "Espalhe o desacidificante para o papel voltar a ser branco.");

            Add(table, "stage.squeegee.title", "Hora de secar");
            Add(table, "stage.squeegee.prompt", "Deslize o rodo de um lado ao outro para tirar a água.");

            Add(table, "stage.adhesive.title", "Rolando, rolando!");
            Add(table, "stage.adhesive.prompt", "Passe o rolo com cola por todo o verso do cartaz.");

            Add(table, "stage.mend.title", "Retoques finais");
            Add(table, "stage.mend.prompt", "Use o lápis para completar os pedacinhos que faltam.");

            // ---- Tutorial (Tracy's voice: warm, unhurried, never bossy) ----
            Add(table, "tutorial.welcome",
                "Oi, oi! Que bom te ver por aqui. Eu sou a Tracy, e este é o Restorium Emporium. " +
                "Vamos devolver a vida a um cartaz bem antigo?");
            Add(table, "tutorial.tapRestore",
                "Nosso primeiro trabalho está aqui no diário. Toque em Restaurar para começarmos.");

            Add(table, "tutorial.pickDustRemover",
                "Primeiro, a poeira. Pegue o espanador ali na barra de ferramentas.");
            Add(table, "tutorial.useDustRemover",
                "Agora deslize o dedo pelo cartaz, devagarinho, até tirar toda a poeira.");

            Add(table, "tutorial.pickWaterSpray",
                "Muito bem! Agora pegue o borrifador de água.");
            Add(table, "tutorial.useWaterSpray",
                "Umedeça o cartaz inteiro. Sem pressa, o papel agradece.");

            Add(table, "tutorial.pickDeacidifier",
                "O papel amarelou com o tempo. Pegue o desacidificante para a gente resolver isso.");
            Add(table, "tutorial.useDeacidifier",
                "Espalhe por tudo e veja o branquinho voltar. Essa é a minha parte favorita.");

            Add(table, "tutorial.pickSqueegee",
                "Hora de secar. Pegue o rodo, por favor.");
            Add(table, "tutorial.useSqueegee",
                "Deslize o rodo de um lado ao outro para tirar a água que sobrou.");

            Add(table, "tutorial.pickRoller",
                "Virei o cartaz para você. Agora pegue o rolo de cola.");
            Add(table, "tutorial.useRoller",
                "Passe cola por todo o verso — é ela que vai segurar o cartaz no linho.");

            Add(table, "tutorial.pickPencil",
                "Falta pouquinho! Pegue o lápis.");
            Add(table, "tutorial.usePencil",
                "Agora complete com carinho os pedacinhos que se perderam pelo caminho.");

            Add(table, "tutorial.congrats",
                "Olha só isso! Ficou lindo. Você tem mão para essa arte, viu?");
            Add(table, "tutorial.tapContinue",
                "Toque em Continuar quando quiser seguir.");

            table.Invalidate();
            EditorUtility.SetDirty(table);
        }

        private static void Add(LocaleTable table, string key, string value)
        {
            table.entries.Add(new LocaleTable.Entry { key = key, value = value });
        }

        // =====================================================================
        //  IMPORT SETTINGS HELPER (opt-in, separate menu item)
        // =====================================================================

        [MenuItem("Restorium/Fix Art Import Settings (Sprites)", false, 101)]
        public static void FixArtImportSettings()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" });
            var fixedCount = 0;

            AssetDatabase.StartAssetEditing();

            try
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    {
                        continue;
                    }

                    var needsType = importer.textureType != TextureImporterType.Sprite;

                    // Full Rect is required: a tight sprite mesh does not cover the whole
                    // rect, which breaks the reveal shader's UV mapping (RestorationStageData).
                    var needsMesh = importer.spriteImportMode != SpriteImportMode.Single
                                    || importer.spritePixelsPerUnit <= 0f;

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    var needsFullRect = settings.spriteMeshType != SpriteMeshType.FullRect;

                    if (!needsType && !needsMesh && !needsFullRect)
                    {
                        continue;
                    }

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            Debug.Log(
                $"[PosterAuthoringTool] Checked {guids.Length} texture(s) under Assets/Art and " +
                $"re-imported {fixedCount} of them as Sprite (2D and UI) with Mesh Type = Full Rect. " +
                "Now run Restorium -> Create Poster 1 Data.");
        }

        // =====================================================================
        //  SHARED HELPERS
        // =====================================================================

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (asset != null)
            {
                // Update in place. This is what makes re-running the menu safe.
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static Sprite LoadSprite(string assetPath, string usedFor)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            if (sprite != null)
            {
                return sprite;
            }

            _missingSprites++;

            if (File.Exists(assetPath))
            {
                // The png is there, it is just not a Sprite yet. Different problem,
                // different fix, and by far the more common one for a beginner.
                MissingReport.Add(
                    $"{usedFor} -> \"{assetPath}\" exists on disk but is NOT imported as a Sprite. " +
                    "Select it in the Project window, set Texture Type = \"Sprite (2D and UI)\" and " +
                    "Mesh Type = \"Full Rect\", press Apply — or just run " +
                    "Restorium -> Fix Art Import Settings (Sprites).");
            }
            else
            {
                MissingReport.Add(
                    $"{usedFor} -> \"{assetPath}\" is not on disk yet. Export it to exactly that path " +
                    "(the file name is case-sensitive) and re-run this menu item.");
            }

            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath);

            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            parent = parent.Replace('\\', '/');
            EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }
}
