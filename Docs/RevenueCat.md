# RevenueCat coin packs ("The Golden Vault")

Players buy coins in three consumable packs through RevenueCat. During development purchases go through RevenueCat's **Test Store**: no Google Play or App Store setup is needed, and no real money is charged.

## Where the API key goes

**`Assets/Data/Config/RevenueCatConfig.asset`** (select it in the Project window):

| Field | Key | Used by |
|---|---|---|
| Test Store Api Key | `test_…` (RevenueCat → Apps & providers → Test Store) | The Editor and **Development builds** only |
| Google Play Api Key | `goog_…` | Release Android builds (later, once Play Console is set up) |
| Apple Api Key | `appl_…` | Release iOS builds (later) |

- **Only public SDK keys go here.** A secret key (`sk_…`) must never be in the project. `StoreKeyPolicy` refuses one at runtime, and the asset logs an error in the Inspector if one is pasted. If a secret key is ever committed, rotate it in the dashboard.
- Public SDK keys are meant to ship inside the app, so committing them is fine.
- **A Test Store key only works in Development builds.** RevenueCat deliberately crashes release builds that use a `test_` key. The game won't pass one to the SDK in a release build: the coin screen just says the store is unavailable. So when testing on a phone, tick **File → Build Profiles → Development Build**.

The same asset has the offering id (`coins`) and the Terms / Privacy URLs. A link stays disabled while its URL is empty.

## Products

| Product id | Coins | Price in Figma | Type |
|---|---|---|---|
| `coins_300` | 300 | $0.99 | Consumable |
| `coins_900` | 900 | $1.99 | Consumable |
| `coins_2000` | 2000 | $3.00 | Consumable |

- `Assets/Data/Catalogs/CoinPackCatalog.asset` decides **how many coins** each product id grants and the order of the rows.
- The **price shown** comes from the store: localized, for example "R$ 5,90". The catalog's fallback label ("$ 0.99") is shown until the store answers, and always in the Editor.
- The ids must match the dashboard exactly.

## Dashboard setup (one time)

1. Create a project in RevenueCat (e.g. "Restorium Emporium").
2. **Apps & providers** → in the Test configuration section, create a **Test Store**. Copy its public API key (`test_…`) into `RevenueCatConfig.asset` → Test Store Api Key.
3. **Product catalog → Products**: create three Test Store products. Use the ids above, type **Consumable**, and the prices above.
4. **Product catalog → Offerings**: create an offering with identifier **`coins`** and add one package per product. Any package identifier is fine; custom ones are fine. Making it the **current** offering is also recommended, as a fallback.

## Unity setup (one time)

The SDK (RevenueCat Unity 9.11.1) and External Dependency Manager are already in the repo under `Assets/RevenueCat` and `Assets/ExternalDependencyManager`.

1. **Restorium → Store → Add RevenueCat to Systems prefab**.
   - It adds the `Purchases` component with **Use Runtime Setup** ticked, plus `RevenueCatCoinStore`, to `Assets/Prefabs/Systems.prefab`.
   - It fills GameBootstrap → Coin Pack Catalog.
   - It only touches that prefab.
2. Open `Assets/Scenes/Game.unity` and run **Restorium → Scene → Build Coin Shop**. Save when asked.
   - It builds only `Canvas/Overlays/BuyCoinsOverlay`.
   - It sets two references: OverlayController → Buy Coins, and ShopScreen → Overlays.
   - Nothing else in the scene changes. Do **not** run "Build Batch 2 Scene Objects" for this.
3. Optional: **Restorium → Rebuild Catalogs & Locale Tables**. The new `ui.coins.*` strings are already in the tables; this only re-syncs them from `LocaleSource.Store.cs`.
4. Android dependencies:
   - In Player Settings → Publishing Settings, tick **Custom Main Gradle Template** and **Custom Gradle Properties Template**.
   - Then run **Assets → External Dependency Manager → Android Resolver → Force Resolve**. This pulls `purchases-hybrid-common` into the Gradle build.

## How it works

```
BuyCoinsOverlay ──Buy──▶ ICoinPurchaseService (CoinPurchaseService)
                              │  takes the money            │  credits coins once per transaction
                              ▼                             ▼
                         ICoinStore                     IWallet.Add(coins, "iap:<productId>")
             ┌────────────────┴───────────────┐
   RevenueCatCoinStore (device)     SimulatedCoinStore (Editor)
```

- **Editor**: the RevenueCat SDK can't run there, so `SimulatedCoinStore` stands in. Purchases succeed after a short delay. To test the other paths, change **Outcome** on the `Systems` object while playing.
- **Device**:
  - `RevenueCatCoinStore` configures the SDK with the key that `StoreKeyPolicy` allows for this build.
  - It reads prices from the `coins` offering, then buys the matching package.
  - The Test Store shows its own sheet, with buttons to simulate success, failure or cancel.
- **Exactly-once coins**:
  - Every credited transaction id is recorded in `SaveData.grantedIapTransactions`, saved in the same write as the coins.
  - On every launch, `Reconcile()` credits any past purchase that was paid for but never credited, for example if the app was killed mid-purchase. It never credits one twice.
  - The ledger survives Reset Progress.

## Before a real release (not needed for Test Store)

- Create the same three products as **in-app products** in Google Play Console, add a Google Play app in RevenueCat, attach the products to the `coins` offering, and put the `goog_…` key in the config.
- If minification is turned on, add `-keep class com.revenuecat.** { *; }` to the Proguard rules.
- RevenueCat asks for the Android activity `launchMode` to be `standard` or `singleTop`. Check the built manifest before shipping with Google Play billing.
- The fine print ("Restore purchases any time from Settings") comes from Figma. Consumable coins can't be restored and there is no Restore button yet. Revisit this copy, or add Restore when "Remove Ads" (a non-consumable) ships.
