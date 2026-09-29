# Restorium Emporium

A cozy mobile restoration game about a paper conservator whose workshop keeps receiving things it shouldn't.

**Shipaton 2026 — Next Gen Award submission**

- 📺 Demo video: [YOUR YOUTUBE URL]
- 🎮 Platform: Android (Unity)
- 🐱 Monetization: RevenueCat SDK

---

## What it is

You play Tracy, a paper conservator. Damaged posters arrive at your desk by mail, and you restore them using a process grounded in real conservation practice — surface cleaning, washing, deacidification, lining, mending, pressing, inpainting. Each step is a tactile, unhurried interaction. The game is quiet on purpose.

What surfaces as you work is the other half of the game. The posters span different decades and different printers, and the same details keep appearing across them: a recurring figure, a company that shouldn't have existed that long. Restoring a poster is also uncovering it. The mechanic and the story are the same gesture.

## Scope of this submission

This build is **Chapter 1, first poster** — one complete restoration cycle from arrival to finished piece, plus the desk hub and the monetization flow.

The full game is planned as 4 chapters, released monthly after launch. Cutting to a single complete loop was a deliberate decision: a finished vertical slice communicates the game better than several unfinished ones. What's here is meant to be representative of the final texture, not a prototype of it.

**In this build**
- Full restoration loop on poster 1, all stages
- Desk hub
- [Audio pass / title screen — delete whichever isn't in]
- RevenueCat purchase flow

**Not yet in**
- Posters 2–5 and the chapter finale
- The shop
- Rewarded ads at the press station (designed, not implemented — see below)

## How RevenueCat is used

RevenueCat handles a single non-consumable purchase, `[your product id]`, which grants the `[your entitlement id]` entitlement. The entitlement is checked on launch and unlocks [what it unlocks].

The build is configured against RevenueCat's **Test Store**, so the purchase flow can be exercised without a store account or a published listing.

### Why this monetization model

The design constraint was that monetization shouldn't fight the genre. This is a slow, quiet game about care and attention, and the two standard mobile patterns — interstitials on a timer, and energy systems that gate play — both work by creating irritation and selling relief from it. That is the opposite of what the game is for.

So the plan is:

- **Rewarded ads only, and only where waiting is already diegetic.** The press station involves a real wait: a lined poster sits under pressure while it dries. That wait exists whether or not an ad does. A player who wants to skip it can watch an ad; a player who doesn't is never interrupted. This is designed and placed, but not implemented in this build.
- **Interstitials at chapter boundaries only**, never mid-restoration.
- **One-time remove-ads purchase** rather than a subscription. A player who finishes a chapter a month doesn't have a recurring need, and charging them monthly for the absence of annoyance is a worse product.

RevenueCat's role is to make the purchase side of that work across stores without maintaining receipt-validation infrastructure, which matters for a solo developer shipping monthly updates.

## Architecture

- **Unity [version] / C#**
- **Per-poster ScriptableObjects.** Each poster is a data asset describing its damage profile, which restoration stages apply, the art layers for each stage, and the narrative beats attached to it. Adding a poster is authoring data, not writing code — which is what makes a monthly chapter cadence plausible for one person.
- **Separate scenes for the desk hub, inspection, and the restoration stations**, so each screen loads only what it needs on mobile.
- [Anything else you want to point at]

### Project layout

```
Assets/
  Scripts/        [describe]
  ScriptableObjects/Posters/   per-poster data assets
  Scenes/         [describe]
  Art/            [describe]
```

## Building

1. Unity [version]
2. Clone the repo and open the project.
3. Add your RevenueCat public SDK key on the `Purchases` GameObject in `[scene name]` (Inspector → RevenueCat API Key). The key is not committed. A Test Store key is enough to exercise the purchase flow.
4. Switch the build target to Android and build.

> The RevenueCat SDK does not run in the Unity Editor. The purchase flow has to be tested on a device build.

Dependencies are installed via OpenUPM (`com.revenuecat.purchases-unity`) and resolved by EDM4U on build.

## Roadmap

| | |
|---|---|
| Chapter 1 | Posters 1–5 — the workshop, and the first thing that doesn't add up |
| Chapters 2–4 | Monthly releases |
| After | *Symptoms of Stagnation*, a sequel following the investigator |

## Development

Development was documented publicly in a devlog series: [LINKS, or delete this section]

## License

Source code is licensed under the MIT License — see [LICENSE](LICENSE).

[If you split the licenses, add: Original art, audio, and narrative text are licensed separately under [LICENSE-ASSETS](LICENSE-ASSETS).]

## Credits

Written, designed, modeled, and programmed by Catarina Silva e Meirelles.

[Third-party asset attribution, if any]