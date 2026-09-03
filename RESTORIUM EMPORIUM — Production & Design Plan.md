# **RESTORIUM EMPORIUM — Agent Build Instructions**

Technical build spec for the coding agent. No story/lore here — only what's needed to build. Build in the **parts** below; the human will tell you which part to execute. **Part 1 is the MVP.**

---

## **0\. Project facts**

* **Engine:** Unity. **Language:** C\#. **Platform:** Android (Google Play). **Orientation:** Portrait.  
* **Fully 2D.** All art, UI, and audio already exist in the project. **Do not generate, placeholder, or replace assets** — reference what's there. If an asset you need seems missing, stop and ask; don't invent one.  
* **Monetization:** RevenueCat SDK (IAP \+ optional Virtual Currency). **Ads:** Google AdMob (Google Mobile Ads Unity plugin). These are separate systems — keep each behind a wrapper. I still need to add them to my unity file, currently this is a fresh new unity game project, the only thing installed is the android sdk. No setup or scenes have been added.  
* When wiring RevenueCat, follow RevenueCat's current official docs / their "use RevenueCat from your AI coding agent" codelab; verify SDK method signatures against the installed version (the API has been renamed across versions).

---

## **1\. Engineering principles (apply to every part)**

1. **Small, single-responsibility scripts.** One clear job per class. Prefer composition and interfaces over inheritance and over god-classes. If a file is growing past a few hundred lines or doing two jobs, split it.  
2. **Decouple with events and interfaces.** UI subscribes to systems; systems don't reach into UI. Ads and purchases sit behind interfaces (`IRewardedAd`, a purchase wrapper) so the core loop compiles and runs without them.  
3. **Data as assets, not code.** Per-poster content is a `PosterData` ScriptableObject (provided). Adding content such as more posters must never require a code change.  
4. **Persistence is mandatory and constant** (see §4). Assume the app can be killed at any instant (a call, an app switch). Never hold unsaved critical state (coins, restoration progress, tutorial step).  
5. **Mobile hygiene.** Handle `OnApplicationPause`/`OnApplicationFocus`; do file I/O off the hot path; keep per-frame allocations low; don't assume the app stays foregrounded.  
6. **Testable without paid services.** The whole core loop must run with no ad network and against RevenueCat's **Test Store** — never block progress on a live store.

---

## **2\. Mandatory comment convention (every script)**

Every script begins with **two comment blocks**, in this order:

// \============================================================  
// \<ScriptName\> — \<one-line summary\>  
// WHAT & WHY: what this does and why it exists.  
// KEY DECISIONS: notable design/implementation choices and why  
//   (e.g. "polls entitlements instead of a live listener because…").  
// \============================================================

// \---- UNITY EDITOR SETUP (required for this script to work, remember, right now there is nothing set up in the unity editor, no scene, no camera, no player, nothing yet.) \----  
// \[ \] Concrete step (e.g. "Attach to the 'Systems' GameObject")  
// \[ \] Concrete step (e.g. "Assign the JournalOverlay reference")  
// \[ \] Concrete step (e.g. "Set Product ID to match the dashboard")  
// \---------------------------------------------------------------

Keep the setup block a literal, tickable checklist of Editor actions a human must do (attach, assign references, create assets, set IDs, add scenes to Build Settings). Assume the human does not know unity very well. 

---

## **3\. Systems (modules to build, decoupled)**

* **GameManager / SceneFlow** — owns high-level state and scene/phase transitions; distinguishes **first-run** from normal flow.  
* **SaveManager** — single entry point for persistence; everything saveable registers here; flushes on change and on pause (see §4).  
* **DialogueSystem** — data-driven lines, tap-to-advance, portrait \+ text; raises "line finished" / "sequence finished" events.  
* **TutorialController** — scriptable steps; positions the provided **pointer asset** over a target, gates input to that target, advances on the expected tap. Must be trivially skippable/rerunnable and never soft-lock.  
* **JournalController** — the journal overlay; one page per poster with states `Locked / Available(dotted) / Complete`, rendered by **sprite swap**; a tap on an Available page starts that poster.  
* **RestorationController** — the state machine that runs the uniform station sequence (Brush & Dust → Wash → Deacidify → Linen Backing → Mend → Inpaint) over the phases Inspection → Cleaning → Repair; no fail states; raises "poster complete".  
* **InpaintController** — cycles fixed pre-set spots; masks editing to loss areas; eyedropper sampling with a swatch fallback.  
* **PlayerWallet** — coin balance \+ owned decorations; the shared economy seam (provided; extend, don't fork).  
* **PurchaseManager** — RevenueCat wrapper: entitlements (`remove_ads`), offerings, purchase, restore (provided; extend for coin pack / virtual currency in later parts).  
* **AdManager** — implements `IRewardedAd` with AdMob; load/show rewarded ads; report earned reward. Never involves RevenueCat.  
* **ShopController** — lists decorations, spends coins via `PlayerWallet`, and hosts the remove-ads purchase button (calls `PurchaseManager`).  
* **DecorationSystem** — applies owned decorations to the Workshop scene.

Keep these in separate files under `Assets/Scripts/`, grouped by folder (`Core`, `Restoration`, `Economy`, `UI`, `Tutorial`).

---

## **4\. Persistence (do this everywhere)**

* Route all durable state through **SaveManager**; persist to a JSON file in `Application.persistentDataPath`.  
* **Save triggers:** on every meaningful change (coins earned/spent, station completed, poster completed, tutorial step advanced, decoration bought) **and** in `OnApplicationPause(true)` and `OnApplicationFocus(false)`, and on quit.  
* **Resume safety:** persist enough that an interrupted restoration can resume (or at minimum never loses coins/progress). Reloading after a kill should drop the player back sensibly.  
* **Coins & the Google account (important):** Google Play stores *entitlements* (non-consumables/subscriptions), **not** an arbitrary coin balance. So a locally-tracked coin balance does **not** automatically survive reinstall/new device. Two correct options:  
  * **(MVP) Local wallet \+ constant save**, with a later **cloud backup** via Google Play Games Services *Saved Games* to tie it to the Google account. Simple, no backend; balance is app-managed (fine for cosmetic single-player currency).  
  * **(On-brand, later) RevenueCat Virtual Currency** as the balance source of truth: RC auto-grants coins on a validated consumable purchase, tracks the balance per RC customer across devices, and can grant coins from server-verified AdMob rewards. Note current spend routing goes through RC's API/webhooks and the feature is newer — more setup, but it directly solves account-tied balance and leans into the host's newest feature.  
  * **Recommendation:** Part 1 uses the local wallet (already built). Add cloud persistence (Saved Games or RC Virtual Currency) in Part 5, before public launch.  
* **Consumables:** a coin-pack IAP is a **consumable** product — configure it consumable in the dashboard so it can be repurchased, and grant coins only on the validated purchase callback (never client-trust real-money grants; RC validates). `remove_ads` is a **non-consumable** and must be **restorable**.

---

## **5\. Ads in Unity (answering "are ads a scene?")**

**No — ads are not a scene.** The AdMob SDK renders its own full-screen overlay on top of your game. Flow: initialize the SDK once → **load** a rewarded ad ahead of time → when the player taps "double my coins," **Show()** it → on the SDK's *user-earned-reward* callback, grant the coins. Wrap all of this behind `IRewardedAd` so the game runs without it and so the network is swappable. Use AdMob **test ad unit IDs** during development. For store-readiness, add the **UMP** consent flow (Part 6). RevenueCat is never involved in showing ads.

---

## **6\. Development parts**

Execute only the part the human names. Each part should end **playable and saved**.

* **Part 1 — Tutorial & first restoration (MVP).** Detailed in §7.  
* **Part 2 — Generalize the loop.** Convert the hardcoded first-run path into the normal **journal-driven** flow; build posters \#2–\#3; page unlock progression; first cipher types;   
* **Part 3 — Chapters 2–3.** Posters \#4–\#7; era transitions; more cipher types (redaction, substitution, mirror-text; blacklight is optional/stretch).  
* **Part 4 — Chapter 4 \+ payoff.** Posters \#8–\#10; the reveal beat (\#8); the assembled-phrase payoff; the finale cutscene.  
* **Part 5 — Economy & IAP depth.** Full decoration catalog; consumable **coin-pack IAP**; **account-tied balance** (Google Play Saved Games or RevenueCat Virtual Currency); remove-ads paywall polish.  
* **Part 6 — Store readiness.** AdMob UMP consent; privacy policy; Data Safety \+ content rating \+ ads declaration; performance pass; closed-testing build.

---

## **7\. Part 1 — MVP (detailed)**

Goal: a first-time player can go Title → restore Poster \#1 (guided) → land in the Workshop → see the journal reflect their progress (future posters locked) → buy a plant with earned coins — with everything saved across an app kill and the remove-ads purchase testable on the RevenueCat Test Store.

**Note — build order vs. play order.** You build the Workshop/dialogue/journal/shop scaffolding first, then wire the restoration that actually plays first. The first-run *play* sequence is: Title → Poster \#1 restoration → Workshop dialogue → journal → shop (plant). Build in the sub-steps below.

**1.1 — Title scene.** Play (new game → first-run flow) / Continue / Settings. Wire SaveManager so Continue is correct.

**1.2 — Workshop scaffolding.** Workshop scene (one screen); **DialogueSystem** (tap-to-advance) with an intro Tracy sequence; **JournalController** overlay opened by tapping the journal item; **Shop** scene shell reachable from the Workshop. **TutorialController** with the pointer asset, able to gate "tap to continue" and "tap the journal." No coin economy yet at this step.

**1.3 — Ad wrapper \+ remove-ads check.** Implement **AdManager : IRewardedAd** with AdMob (test IDs). Wire **PurchaseManager** to expose `AdsRemoved` from the `remove_ads` entitlement, configured against the **Test Store** so a test purchase of remove-ads can flip it. (Not a scene — see §5.)

**1.4 — Poster \#1 restoration cycle \+ tutorial.** **RestorationController** runs Inspection → Cleaning → Repair with the 6 uniform stations; the tutorial introduces each station one at a time via the pointer; no fail states. On completion: grant `PosterData.coinReward` to `PlayerWallet`, then show the **"watch to double"** option **only if `!AdsRemoved` and an ad is ready**; on earned reward, grant the bonus. (Use the provided `RestorationReward` seam.)

**1.5 — Journal updates on completion.** Poster \#1's page → **Complete** (before/after sprite); poster \#2+ pages → **Locked / "coming soon"** for this part. Sprite-swap by state; persist completion.

**1.6 — Workshop shop mini-tutorial.** Back in the Workshop, Tracy explains coins and the shop (dialogue \+ pointer). Player enters the **Shop**, buys a **plant** with earned coins via `ShopController` → `PlayerWallet.TryBuyDecoration`; on return, **DecorationSystem** shows the plant placed. This is the coin **spend** tutorial.

**1.7 — Persistence pass.** Confirm every state above saves on change and on `OnApplicationPause(true)`; killing and relaunching mid-flow does not lose coins, completion, decoration ownership, or tutorial position.

### **Part 1 — definition of done**

* New game runs Title → Poster \#1 (guided, no-fail) → Workshop → journal (poster \#1 Complete, \#2 "coming soon") → Shop (buy plant, it appears).  
* Coins earned on completion; ad-double works with a test ad; the double option **disappears** after a Test-Store remove-ads purchase.  
* App can be killed at any point without losing progress.  
* Every script carries both comment blocks (§2). No generated assets. Ads and IAP behind their wrappers.

