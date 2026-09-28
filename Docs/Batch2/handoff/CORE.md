# CORE agent — Batch 2 handoff

## 1. Files created / changed

| File | What it does |
|---|---|
| `Assets/Scripts/Core/SaveMigration.cs` (new) | Plain C#. `MigrateInPlace(SaveData, Action<string>)`: v1 -> v2 (poster entry / activePosterId, completed => claimed + 100 coins, tutorial -> sequences, `intro` seen when `hasPlayedBefore`), clears the v1 fields, stamps v2. Only file that reads the `[Obsolete]` fields. |
| `Assets/Scripts/Core/SaveManager.cs` (changed) | `Parse()` now calls `SaveMigration.MigrateInPlace` then `EnsureCollections()`; a migrated save is marked dirty (written next LateUpdate through the existing atomic path). "Newer version resets" kept. |
| `Assets/Scripts/Core/PosterProgressService.cs` (new) | Plain C# `IPosterProgress` over `SaveData.posters`. |
| `Assets/Scripts/Economy/PlayerWallet.cs` (new) | Plain C# `IWallet` over `SaveData.coins`. Save before notify; saturates; clamps negative; raises `CoinsChanged(balance, 0)` on `ISaveService.Reloaded`. |
| `Assets/Scripts/Economy/DecorationInventory.cs` (new) | Plain C# `IDecorationInventory`. One disk write per purchase (record added in memory, then `IWallet.TrySpend` writes both; rolled back if refused). |
| `Assets/Scripts/Economy/SimulatedRewardedAd.cs` (new) | `MonoBehaviour, IRewardedAd`. `IsReady = Debug.isDebugBuild` (Editor + Development builds), earns after 1.5 s realtime; release: never ready. Exactly one callback per Show. |
| `Assets/Scripts/Core/GameBootstrap.cs` (changed) | Registers, in order: ISaveService -> ILocalizationService -> IAudioService -> IPosterProgress -> IWallet -> IDecorationInventory -> IRewardedAd (AddComponent `SimulatedRewardedAd` if Systems has no IRewardedAd). Unregisters all in OnDestroy. |
| `Assets/Scripts/Core/SceneLoader.cs` (changed) | Calls `GameSignals.Clear()` just before scene activation (both load paths). See deviation D1. |
| `Assets/Scripts/Cinematics/CutscenePlayer.cs` (new) | `ICutscenePlayer`: runtime RenderTexture at clip size, RawImage + AspectRatioFitter Envelope, CanvasGroup fade 0.25 s, blocks raycasts, skip tap (IPointerClickHandler), exactly-once onFinished (end / skip / error / prepare timeout 10 s / length+3 s), pauses music, releases RT after fade-out. |
| `Assets/Scripts/Core/GameFlowController.cs` (rewritten, same GUID/file) | Multi-poster flow per contract §1 / §5. |
| `Assets/Scripts/Audio/IAudioService.cs` (changed) | + `void SetMusicPaused(bool paused)`. See deviation D2. |
| `Assets/Scripts/Audio/AudioManager.cs` (changed) | Implements `SetMusicPaused` (Pause/UnPause, PlayMusic while paused stays paused); + `public AudioMixerGroup MusicMixerGroup { get; }`. |
| `Assets/Editor/Localization/LocaleSource.Flow.cs` | `ui.cutscene.skipHint`. |
| `Assets/Scripts/Tests/EditMode/Logic/SaveMigrationTests.cs`, `PosterProgressTests.cs`, `WalletTests.cs`, `DecorationInventoryTests.cs` (new) | NUnit, no UnityEngine, each with its own nested in-memory `FakeSave`. |

## 2. Public API implemented

All §5 "CORE agent provides" signatures are implemented exactly:

```csharp
// Core
public static class SaveMigration { public static bool MigrateInPlace(SaveData data, Action<string> warn);
    public const int V1PosterReward = 100; public const string FirstRestorationSequence = "first_restoration";
    public const string IntroCutsceneId = "intro"; }
public sealed class PosterProgressService : IPosterProgress { public PosterProgressService(ISaveService save, Action<string> warn); }
// Economy
public sealed class PlayerWallet : IWallet { public PlayerWallet(ISaveService save, Action<string> warn); }
public sealed class DecorationInventory : IDecorationInventory { public DecorationInventory(ISaveService save, IWallet wallet, Action<string> warn); }
public class SimulatedRewardedAd : MonoBehaviour, IRewardedAd
// Cinematics
public class CutscenePlayer : MonoBehaviour, ICutscenePlayer, IPointerClickHandler
// GameFlowController
public PosterCatalog Posters { get; }
public PosterData ActivePoster { get; }
public PosterData LastCompletedPoster { get; }   // falls back to last completed poster in catalogue order after a relaunch
public bool DeskHubUnlocked { get; }
public event Action<GameScreen> FlowScreenRequested;
public void StartOrContinue(PosterData poster);  // refuses locked or already-completed posters (warning)
public void OpenJournal();
public void OpenJournalFromDesk();
public void GoToDeskHub();                       // before any completion: warns and shows Journal
public void GoToShop();
public void QuitToTitle();
public bool CanDoubleReward { get; }
public void RequestDoubleReward(Action<bool> onDone);  // onDone(true) only when coins were added
public void GoToThanksForPlaying();              // legacy, unused
```

Behaviour notes the UI needs:
- **Rewards are written atomically:** `TryMarkRewardClaimed/Doubled` set the flag and call `SaveSoon()`; the following `wallet.Add` does the one synchronous `Save()` of both. A kill can neither lose nor double-pay. `GameFlowController.Start` also pays any reward left pending.
- Routing: `OpenJournal` (pause) and the end of every video use `router.GoNow`; everything else uses `router.Go` (1 s hold). CutscenePlayer calls `onFinished` while the overlay still covers the screen and then fades out, so the new screen is revealed by the fade. `IsPlaying`/`PlayingChanged(false)` only drop after the fade.
- While a cutscene runs, `StartOrContinue / OpenJournal / OpenJournalFromDesk / GoToDeskHub / GoToShop` are ignored (double-tap guard).
- Start order: services -> `hasPlayedBefore = true` -> pay pending rewards -> intro (forced, if `intro` unseen; marked seen after) -> resume in-progress `activePosterId` / DeskHub if any poster completed / Journal.
- Poster completion: `MarkCompleted` -> pay `coinReward` -> if `completionCutscene` set and `complete:<posterId>` unseen: play forced, mark seen, `GoNow(FinishedRepair)`; else `Go(FinishedRepair)`.

### Deviations (flag loudly)
- **D1 — GameSignals.Clear() is NOT on `SceneManager.sceneLoaded`.** Unity runs the new scene's Awake/OnEnable *before* `sceneLoaded`, so clearing there would wipe the subscriptions TutorialController/others just made in OnEnable. Clear is done by `SceneLoader` right before `allowSceneActivation = true` (and before the no-SceneLoader `LoadScene` fallback). Same safety net, no wiped listeners. GameBootstrap has no sceneLoaded hook.
- **D2 — `IAudioService` gained `void SetMusicPaused(bool paused)`.** The contract says CutscenePlayer pauses music "via IAudioService", and the interface had no pause. Only `AudioManager` implements IAudioService today; **any test fake another agent writes for IAudioService must add this method.**
- **D3 — extra optional CutscenePlayer fields** beyond §5: `AudioSource videoAudioSource`, `Canvas overlayCanvas`, plus timing values. The five §5 fields exist with the exact names.
- **D4 — extra GameFlowController field** `bool bookCutsceneSkippable = true` (contract silent on whether the book video is skippable; story videos are forced as required).

## 3. Scene wiring for the builder

### Systems (Title scene) — nothing new
`GameBootstrap` creates PosterProgressService / PlayerWallet / DecorationInventory in code and `AddComponent<SimulatedRewardedAd>()` on `Systems` at runtime. Optional: add `SimulatedRewardedAd` to `Systems` by hand (fields `earnDelaySeconds` 1.5, `simulateEarned` true).

### Game scene — `GameFlow` (existing object, existing `GameFlowController` component — keep its GUID, do not re-add)
| Field | Assign |
|---|---|
| `router` (ScreenRouter) | `GameFlow` |
| `restorationSource` (MonoBehaviour) | `GameFlow`'s `RestorationController` |
| `cutsceneSource` (MonoBehaviour) | the `CutscenePlayer` object's `CutscenePlayer` component (optional; found via ServiceLocator if empty) |
| `posters` (PosterCatalog) | `Assets/Data/Catalogs/PosterCatalog.asset` |
| `introCutscene` (VideoClip) | `Assets/videos/tracysc1.mp4` |
| `bookOpenCutscene` (VideoClip) | `Assets/videos/openBookTransition.mp4` |
| `bookCutsceneSkippable` | true |

`tracysc2.mp4` is **not** on GameFlowController: it is `Poster01.asset -> completionCutscene` (RESTORATION agent's PosterAuthoringTool sets it). Remove the old serialized `poster` field value (it no longer exists; Unity drops it). Remove any old persistent On Click entries pointing to `GameFlowController.StartRestoration` / `GoToThanksForPlaying`. `RestorationController.beginOnStart` must be **false** (the flow calls BeginPoster).
`ScreenRouter.screens` must include DeskHubScreen, ShopScreen, StickerRemovalScreen in addition to the existing six.

### Game scene — `CutscenePlayer` (new; LAST child of the main Canvas so it is topmost)
```
Canvas (main)
└── CutscenePlayer          RectTransform stretch-stretch, all offsets 0
    │  Components: Canvas (overrideSorting = true, sortingOrder = 1000),
    │              GraphicRaycaster, CanvasGroup (alpha 0, interactable false, blocksRaycasts false),
    │              VideoPlayer (playOnAwake false, isLooping false, source VideoClip, clip EMPTY,
    │                           renderMode RenderTexture, targetTexture EMPTY — all set by script),
    │              AudioSource (playOnAwake false, loop false, spatialBlend 0,
    │                           Output = Assets/Audio/MainMixer.mixer -> "Music" group),
    │              CutscenePlayer
    ├── Backdrop            Image, color #000000 alpha 1, sprite none, raycastTarget TRUE, stretch-stretch 0
    ├── VideoSurface        RawImage (texture EMPTY, color white, raycastTarget TRUE), centred anchors (0.5,0.5),
    │                       AspectRatioFitter (aspectMode = EnvelopeParent, aspectRatio 0.5625)
    └── SkipHint (INACTIVE) TMP_Text, bottom-centre anchor, ~y +48 px, size ~18, white, centre aligned,
                            raycastTarget false, + LocalizedText key "ui.cutscene.skipHint"
```
`CutscenePlayer` serialized fields:
| Field | Assign |
|---|---|
| `videoSurface` (RawImage) | `CutscenePlayer/VideoSurface` |
| `videoPlayer` (VideoPlayer) | `CutscenePlayer` |
| `group` (CanvasGroup) | `CutscenePlayer` |
| `fitter` (AspectRatioFitter) | `CutscenePlayer/VideoSurface` |
| `skipHint` (TMP_Text, optional) | `CutscenePlayer/SkipHint` |
| `videoAudioSource` (AudioSource) | `CutscenePlayer` |
| `overlayCanvas` (Canvas) | `CutscenePlayer` |
| `fadeSeconds` 0.25, `timeoutPaddingSeconds` 3, `prepareTimeoutSeconds` 10, `minSecondsBeforeSkip` 0.5, `skipHintDelaySeconds` 1 | defaults |

The object must stay **active**; it hides itself (Canvas disabled, alpha 0) while idle. No art files needed. No TutorialAnchor ids on these objects.

**Video audio routing (why):** VideoPlayer -> AudioSource -> MainMixer/Music, and the game music is paused while the video plays. Cutscene audio is a long scored bed (same category as music), so the Music slider is what a player expects to control it; SFX stays for short feedback. If the AudioSource's Output is left empty the script borrows `AudioManager.MusicMixerGroup`; if that is also missing it sets the source volume from `SaveData.musicVolume`.

## 4. Editor menu items
None added. Order for the human: RESTORATION agent's `Restorium/Posters/Create or Update Poster 1 Data` (sets `completionCutscene = tracysc2`) -> `.../Poster 2 Data` -> `Restorium/Rebuild Catalogs & Locale Tables` (builds PosterCatalog + locale tables incl. `ui.cutscene.skipHint`) -> assign GameFlowController fields.

## 5. Localization keys added (LocaleSource.Flow.cs)
| key | pt-BR | en |
|---|---|---|
| `ui.cutscene.skipHint` | Toque para pular | Tap to skip |

## 6. Tests
`Assets/Scripts/Tests/EditMode/Logic/SaveMigrationTests.cs` (12), `PosterProgressTests.cs` (20: unlock order, begin/resume, SaveSoon vs Save, completion first-time flag, reward claim/double idempotence, flag+coins in one write, reset, null save), `WalletTests.cs` (13 incl. TestCases), `DecorationInventoryTests.cs` (13: success single write, not-enough-coins no change, already-owned, invalid, free item, no wallet, clamping, placement, lamp affordable with poster reward). Pure NUnit, no UnityEngine; run in Unity Test Runner (EditMode) or `dotnet test Tools/logictests`. I verified them with a temporary project containing only these files + their sources: **58/58 passed**. (The shared `Tools/logictests` project currently fails to build because of other agents' test files referencing UI/Localization/Tutorial namespaces that it does not include.)

## 7. Needs from others / open questions
- **RESTORATION:** `RestorationController` must drop all save I/O (still reads `IsRestorationInProgress`/v1 fields at the time of writing — the only Runtime compile errors besides UI's) and `beginOnStart` should default to false / be unticked. `Poster01.completionCutscene = tracysc2`. VideoClip import (H.264, portrait ~720x1280 recommended for mobile memory: the RenderTexture is allocated at clip size).
- **UI:** TutorialController should subscribe in OnEnable (safe with D1) and wait on `ICutscenePlayer.PlayingChanged`. Any IAudioService fake must implement `SetMusicPaused` (D2). FinishedRepairScreen: refresh `CanDoubleReward` after `RequestDoubleReward` completes (it goes false while the ad is in flight and after doubling).
- **LEAD:** `SaveMigration.IntroCutsceneId` duplicates `CutsceneIds.Intro` as a literal because `ICutscenePlayer.cs` imports `UnityEngine.Video` (a test pins the value). Consider moving `CutsceneIds` to its own UnityEngine-free file.
- Replays: a completed poster cannot be restarted (`StartOrContinue` refuses; journal shows "Restored" disabled). `BeginOrResume` supports a replay at stage 0 (keeps completed/claimed) if that is ever wanted, but such a replay is not resumed after a relaunch.
- If the app is killed on Finished Repair, relaunch goes to DeskHub (double-reward chance for that poster is lost, base reward is safe).

## 8. Optimisation ideas
- Transcode the videos to 720x1280 H.264 (~2–3 Mbps): tracysc1 is 37 MB; a 1080x1920 ARGB32 RenderTexture is 8 MB of GPU memory and decode cost is high on low-end Android.
- Consider streaming the videos from `StreamingAssets` via URL instead of VideoClip import to keep them out of the APK's compressed asset bundle (faster install/patching); CutscenePlayer would need a URL overload.
- The router's 1 s hold also applies to button presses (Shop, DeskHub); if it feels laggy, consider a `GoSoon(screen, seconds)` overload with a shorter hold for button-driven navigation.
