# Restorium Emporium

A cozy mobile restoration game about a paper conservator whose workshop keeps receiving things it shouldn't.

**RevenueCat Shipaton 2026 — Next Gen Award submission  ₍^. .^₎⟆**

- 📺 Demo video : https://www.youtube.com/watch?v=Ud0yYI8O5t0
- 🎮 Platform: Android (Unity 6, portrait)
- ⚞^. .^⚟ Monetization: RevenueCat SDK (coin packs on the RevenueCat Test Store)
- 🌐 Languages: English and Brazilian Portuguese 𓂃 ࣪˖ ִֶָ𐀔

•·················•·················•·················•────── .‿୨˚̣̣̣͙୧‿. ──────•·················•·················•·················•

## What it is

Damaged posters arrive at a restoration shop. You play Tracy, the paper conservator who repairs them, using a process grounded in real conservation practice: brushing, washing, deacidification, lining, mending, inpainting. Each stage is a slow, tactile interaction built around touch and sound.

Most cozy ASMR games stop there: sound and texture, and nothing underneath. This one has a story.

After the first restoration, Tracy goes to catalogue where the poster came from: a town called Lethe Falls. On paper, Lethe Falls doesn't exist, but you can't help but feel like the name is familiar to you.

Uncover this mystery by restoring the past and finding strange details in old posters in Restorium Emporium!

## Scope of this submission

This build is the **opening of Chapter 1: the first two posters**, the desk hub and shop they lead into, and the coin purchase flow.

The full game is planned as 4 chapters, released monthly after launch. Cutting to a small, complete loop was a deliberate decision: a finished vertical slice communicates the game better than several unfinished ones. What's here is meant to be representative of the final texture, not a prototype of it.

**In this build**
- Animated title screen, and an intro cutscene on first launch
- Poster 1: the full guided restoration (dust remover → water spray → deacidifier → squeegee → linen roller → pencil inpainting), ending in a cutscene and a before/after reveal
- Poster 2: the same loop with an extra sticker-removal close-up between dusting and washing
- Step-by-step tutorial with Tracy's dialogue (tap to continue) and a pointing hand
- Journal: one page per poster, which unlocks in order
- Desk hub: coin balance, the journal book, and an edit mode for dragging decorations around the desk
- Shop: decorations bought with coins earned from restorations (lamp, plant pot, book pile), previewed on the desk before buying
- Buy-coins screen: three coin packs sold through RevenueCat
- Settings (music and SFX volume, language, reset progress) and a pause menu
- Autosave on every meaningful change and when the app is backgrounded, so the game can be killed at any point and resumes mid-restoration

**Not yet in**
- Posters 3+ and the chapter finale
- Real ads. The "Double Reward" rewarded-ad button runs against a simulated ad in development builds and is hidden in release builds.
- The Remove Ads purchase. The button is visible but disabled in this build.

## How RevenueCat is used

RevenueCat sells three **consumable** coin packs from the offering `coins`:

| Product id | Coins |
|---|---|
| `coins_300` | 300 |
| `coins_900` | 900 |
| `coins_2000` | 2000 |

Coins buy cosmetic decorations for Tracy's desk. They never gate the restorations: posters unlock by finishing the previous one, and poster 1 pays exactly enough for the lamp the tutorial asks you to buy.

The build is configured against RevenueCat's **Test Store**, so the purchase flow can be exercised without a store account or a published listing. The Test Store shows its own purchase sheet with buttons to simulate success, failure or cancellation.

How it's wired:
- `RevenueCatCoinStore` (in its own assembly under `Assets/Integrations/RevenueCat`) is the only code that touches the SDK. The rest of the game talks to an `ICoinStore` interface, so it compiles and runs without RevenueCat.
- Prices on the buy-coins screen come from the store, localized to the player's currency.
- Coins are credited **exactly once per transaction**. Each credited transaction id is saved in the same write as the coins. On every launch, a reconcile pass credits any purchase that was paid for but never credited (for example, if the app was killed mid-purchase), and never credits one twice.
- In the Unity Editor, where the SDK can't run, a `SimulatedCoinStore` stands in.

Full details, including the dashboard setup, are in [Docs/RevenueCat.md](Docs/RevenueCat.md).

### Why this monetization model

The design constraint was that monetization shouldn't fight the genre. This is a slow, quiet game about care and attention, and the two standard mobile patterns (interstitials on a timer, and energy systems that gate play) both work by creating irritation and selling relief from it. That is the opposite of what this game's audience enjoys.

So the plan is:

- **Coins are for decoration, never for progress.** The shop is a way to make the workshop feel like yours. Buying coins is a shortcut to a nicer desk, not to the next poster.
- **Rewarded ads only, and only when the player asks for one.** After a restoration, the player can choose to watch an ad to double that poster's coins. A player who doesn't want to is never interrupted. The flow is built and testable with a simulated ad. The real ad network is not integrated yet.
- **One-time Remove Ads purchase** rather than a subscription. A player who finishes a chapter a month doesn't have a recurring need, and charging them monthly for the absence of annoyance is a worse product.

RevenueCat's role is to make the purchase side of that work across stores without maintaining receipt-validation infrastructure, which matters for a solo developer shipping monthly updates.

## Architecture

- **Unity 6000.5.3f1 (Unity 6.5), C#, URP 2D, uGUI + TextMesh Pro, Input System**
- **Content is data, not code.** Each poster is a `PosterData` ScriptableObject with its sprites, completion cutscene, coin reward and an ordered list of `RestorationStageData` stages (which tool, which layers, how much of the poster has to be covered). Shop items, tools and tutorial steps are ScriptableObjects too. Editor menus under **Restorium** generate them (`Posters > Create or Update All Posters`, `Shop > New Shop Item`, and so on). Adding a poster or a decoration is authoring data, which is what makes a monthly chapter cadence plausible for one person.
- **Three scenes.** `Title` owns the persistent `Systems` object. `Game` holds every gameplay screen (journal, cleaning, sticker removal, linen backing, finished repair, desk hub, shop) as a canvas screen switched by a `ScreenRouter`, plus overlays for settings, pause and buying coins. `ThanksForPlaying` is the end card.
- **Services behind interfaces**, registered by `GameBootstrap` in a small service locator: saves, poster progress, wallet, decoration inventory, audio, localization, cutscenes, rewarded ads and the coin store. Anything that needs a paid service has a simulated stand-in, so the whole loop runs in the Editor.
- **Restoration by reveal masks.** Dragging a tool paints into a mask texture, and a shader blends from one poster layer to the next. A stage completes when enough of the poster is covered.
- **Versioned JSON saves** in `Application.persistentDataPath`, with a migration step, written on every change and on pause/focus loss.
- **Localization** through string tables (`Assets/Data/Localization/en.asset`, `pt-BR.asset`), switchable at runtime from Settings.
- **168 EditMode unit tests** covering save migration, coins, unlock order, shop purchases, IAP crediting, stickers, journal rules, tutorial triggers and reveal-mask maths.

### Project layout

```
Assets/
  Scripts/              runtime code, by system: Core, Restoration, Tutorial, UI,
                        Economy, Decor, Audio, Localization, Cinematics, FX, Shaders, Tests
  Integrations/RevenueCat/   the RevenueCat bridge (separate assembly)
  Editor/               Restorium menu tools: poster/shop/tutorial authoring, catalog
                        and locale builders, scene builders and a scene validator
  Data/                 ScriptableObject content: Poster1/, Poster2/ (with their stages),
                        ShopItems/, Tools/, Tutorial/, Catalogs/, Localization/, Config/
  Scenes/               Title, Game, ThanksForPlaying
  Art/                  sprites exported from Figma, by screen
  Audio/                music and sound effects
  videos/               cutscenes (tracysc1, tracysc2) and the open-book transition
  RevenueCat/, ExternalDependencyManager/   RevenueCat Unity SDK 9.11.1 and EDM4U
Docs/                   RevenueCat.md, and Batch2/ (architecture contract, Figma layout, audits)
SETUP.md, checklist.md  step-by-step Unity Editor setup guides
```

## Building

### Requirements

- **Unity 6000.5.3f1**, installed through Unity Hub with the **Android Build Support** module (including OpenJDK and the Android SDK & NDK tools).
- An Android phone running Android 8.0 (API 26) or later. The project targets API 36, ARM64, IL2CPP.

### Steps

1. **Clone and open.** Clone the repo, then in Unity Hub choose **Add project from disk** and pick the `RestoriumEmporium` folder. If Hub offers to install 6000.5.3f1, let it. The first import takes several minutes. When it finishes, the Console should have no red errors.
2. **Try it in the Editor (optional).** Open `Assets/Scenes/Title.unity` and press Play. Always start from `Title`, because it creates the `Systems` object the other scenes depend on. In the Editor, coin purchases go through the simulated store and the Double Reward button uses a simulated ad.
3. **RevenueCat key.** A public Test Store key (`test_…`) is already committed in `Assets/Data/Config/RevenueCatConfig.asset`, so no setup is needed to try the purchase flow. To point the game at your own RevenueCat project instead, follow [Docs/RevenueCat.md](Docs/RevenueCat.md): create a Test Store, add the three `coins_*` products as consumables, put them in an offering called `coins`, and paste your `test_` key into that asset. Only public SDK keys go in the project. Never commit a secret `sk_` key.
4. **Select the Android profile.** Open **File > Build Profiles**, select the **Android** profile, and click **Switch Platform** if it isn't active yet. The scene list is already set: `Title` (index 0), `Game`, `ThanksForPlaying`.
5. **Leave Development Build ticked.** RevenueCat deliberately crashes release builds that use a `test_` key. The game protects against this by not starting the store at all in a release build, so a release build would just show the store as unavailable. Development Build is ticked in the committed profile.
6. **Resolve Android dependencies (first build on a new machine).** Run **Assets > External Dependency Manager > Android Resolver > Force Resolve** and wait for it to finish. This pulls RevenueCat's native Android library into the Gradle build. The custom Gradle templates it needs are already in `Assets/Plugins/Android`.
7. **Build.** Click **Build** to produce an APK, or plug in a phone with USB debugging on and click **Build And Run**.

> The RevenueCat SDK does not run in the Unity Editor. The real purchase flow can only be tested on a device build.

### Testing tips

- **Unit tests:** **Window > General > Test Runner > EditMode > Run All**. They need no scene setup.
- **Start over:** in the game, open Settings and use the red **Reset progress** button. It keeps language, volume and the purchase ledger, so bought coins are never lost or credited twice.
- **Detailed Editor setup:** [SETUP.md](SETUP.md) covers the original scene setup and has a troubleshooting table. [checklist.md](checklist.md) covers everything added for poster 2, the desk hub and the shop.

## Roadmap

| | |
|---|---|
| Chapter 1 | Posters 1–5 |
| Chapters 2–4 | Monthly releases |

## License

Source code is licensed under the MIT License — see [LICENSE](LICENSE).

Original art, audio, and narrative text are licensed separately under [LICENSE-ASSETS](LICENSE-ASSETS).

## Credits

Written, designed, illustrated, and directed by Catarina Silva e Meirelles.

Third-party:
- [RevenueCat Unity SDK](https://github.com/RevenueCat/purchases-unity) (MIT)
- [External Dependency Manager for Unity](https://github.com/googlesamples/unity-jar-resolver) (Apache 2.0)
- [Special Elite](https://fonts.google.com/specimen/Special+Elite) font by Astigmatic (Apache 2.0)
- Music and sound effects from [Pixabay](https://pixabay.com/) (Lilliben, freesound_community, Universfield, Yuliana Yurukova, Dragon Studio, SoundReality)

.𖥔 ݁ ˖   ✦    ‧₊˚ ⋅
CatLLS
