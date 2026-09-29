// ============================================================
// BuyCoinsOverlay — "The Golden Vault", the coin-pack store (Figma BuyCoins 623:26).
// WHAT & WHY: Opened from the Shop's "Buy More Coins" button. Shows the packs
//   from ICoinPurchaseService with the store's prices, buys one on tap and lets
//   the balance pill (a CoinBalanceLabel) show the new coins. A ModalOverlay (see
//   OverlayController.cs), so the fade, input blocking, stacking and Android back
//   key are already handled.
// KEY DECISIONS:
//   - Rows are pre-built in the scene (three, like Figma) and bound to the packs
//     in catalogue order on every open; a row with no pack is hidden. That keeps
//     the Figma layout exact without a layout group.
//   - The fallback price is shown first, then replaced by the store's localized
//     price when FetchPrices answers. In the Editor the fallback stays, which is
//     the Figma copy.
//   - While a purchase is in flight every row is non-interactable, so a double
//     tap cannot start two. The rows come back when the callback arrives, even
//     if the overlay was closed in between.
//   - Cancel is silent (the player chose it). Failed / Unavailable show one
//     status line; nothing is shown on success because the balance pill punches.
//   - Coins are credited by CoinPurchaseService, never here.
//   - "Remove Ads" is visible but not interactable this build (same as the
//     Shop's button). Terms / Privacy open RevenueCatConfig's URLs and are
//     disabled while their URL is empty.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Easiest: open Assets/Scenes/Game.unity and run Restorium -> Scene -> Build
// Coin Shop. It builds ONLY this overlay and sets two references. By hand:
// [ ] Under Canvas/Overlays create an empty object named BuyCoinsOverlay,
//     stretch/stretch, offsets 0. Add Canvas Group and this component. Leave it
//     ACTIVE; OverlayController hides it at runtime.
// [ ] Children per Docs/Batch2/FigmaLayout.md section 13 (background, Tracy,
//     stars, balance pill with CoinBalanceLabel, scrim, title, three rows with
//     CoinPackRow, fine print, Status label, Terms/Privacy/Remove Ads buttons,
//     plant, close X button).
// [ ] Drag: Close Button, Rows (3), Status Label, Terms / Privacy / Remove Ads
//     buttons, Config <- Assets/Data/Config/RevenueCatConfig.asset.
// [ ] Drag this object into OverlayController -> "Buy Coins", and the Overlays
//     object into ShopScreen -> "Overlays".
// ---------------------------------------------------------------

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Economy;
    using RestoriumEmporium.Localization;

    [DisallowMultipleComponent]
    public class BuyCoinsOverlay : ModalOverlay
    {
        private const string FailedKey = "ui.coins.failed";
        private const string UnavailableKey = "ui.coins.unavailable";

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Tooltip("The pack rows, top to bottom. Bound to CoinPackCatalog in order.")]
        [SerializeField] private CoinPackRow[] rows = new CoinPackRow[0];

        [Header("Links")]
        [SerializeField] private Button termsButton;
        [SerializeField] private Button privacyButton;

        [Tooltip("Visible but disabled this build.")]
        [SerializeField] private Button removeAdsButton;

        [Header("Status")]
        [Tooltip("One line under the packs for 'purchase failed' / 'store unavailable'. Empty otherwise.")]
        [SerializeField] private TMP_Text statusLabel;

        [Header("Data")]
        [Tooltip("Assets/Data/Config/RevenueCatConfig.asset (Terms / Privacy links).")]
        [SerializeField] private RevenueCatConfig config;

        private ICoinPurchaseService _service;
        private ILocalizationService _loc;
        private bool _subscribed;
        private int _pricesRequest;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnClose);
            }

            if (termsButton != null)
            {
                termsButton.onClick.AddListener(OnTerms);
            }

            if (privacyButton != null)
            {
                privacyButton.onClick.AddListener(OnPrivacy);
            }

            // Remove Ads arrives in a later build: visible, not clickable.
            if (removeAdsButton != null)
            {
                removeAdsButton.interactable = false;
            }

            foreach (var row in rows)
            {
                if (row != null)
                {
                    row.Clicked += OnRowClicked;
                }
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnClose);
            }

            if (termsButton != null)
            {
                termsButton.onClick.RemoveListener(OnTerms);
            }

            if (privacyButton != null)
            {
                privacyButton.onClick.RemoveListener(OnPrivacy);
            }

            foreach (var row in rows)
            {
                if (row != null)
                {
                    row.Clicked -= OnRowClicked;
                }
            }

            Unsubscribe();
        }

        protected override void OnOpened()
        {
            // Resolved lazily: Systems registers these in its own Awake.
            _service ??= ServiceLocator.Get<ICoinPurchaseService>();
            _loc ??= ServiceLocator.Get<ILocalizationService>();

            if (!_subscribed)
            {
                if (_service != null)
                {
                    _service.AvailabilityChanged += OnAvailabilityChanged;
                }

                if (_loc != null)
                {
                    _loc.LocaleChanged += OnLocaleChanged;
                }

                _subscribed = true;
            }

            if (termsButton != null)
            {
                termsButton.interactable = config != null && !string.IsNullOrEmpty(config.TermsUrl);
            }

            if (privacyButton != null)
            {
                privacyButton.interactable = config != null && !string.IsNullOrEmpty(config.PrivacyUrl);
            }

            BindRows();
            RefreshAvailability();
            RequestPrices();
        }

        protected override void OnClosed()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (_service != null)
            {
                _service.AvailabilityChanged -= OnAvailabilityChanged;
            }

            if (_loc != null)
            {
                _loc.LocaleChanged -= OnLocaleChanged;
            }

            _subscribed = false;
        }

        private void BindRows()
        {
            var packs = _service != null ? _service.Packs : null;

            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];

                if (row == null)
                {
                    continue;
                }

                var pack = packs != null && i < packs.Count ? packs[i] : null;
                row.gameObject.SetActive(pack != null);
                row.Bind(pack);
            }

            if (packs != null && packs.Count > rows.Length)
            {
                Debug.LogWarning($"[BuyCoinsOverlay] {packs.Count} packs but only {rows.Length} rows; " +
                                 "the extra packs are not shown.", this);
            }
        }

        private void RequestPrices()
        {
            if (_service == null)
            {
                return;
            }

            // Ignore an answer that arrives after a newer request (re-opened quickly).
            var request = ++_pricesRequest;

            _service.FetchPrices(prices =>
            {
                if (this == null || request != _pricesRequest || prices == null)
                {
                    return;
                }

                foreach (var row in rows)
                {
                    if (row != null && row.Pack != null && prices.TryGetValue(row.Pack.productId, out var price))
                    {
                        row.SetPrice(price);
                    }
                }
            });
        }

        private void RefreshAvailability()
        {
            var available = _service != null && _service.IsAvailable;
            SetRowsInteractable(available && !_service.IsPurchasing);
            SetStatus(available ? string.Empty : Text(UnavailableKey, "The store isn't available right now."));
        }

        private void OnRowClicked(CoinPack pack)
        {
            if (_service == null || pack == null || _service.IsPurchasing)
            {
                return;
            }

            SetRowsInteractable(false);
            SetStatus(string.Empty);

            _service.Buy(pack.productId, result =>
            {
                if (this == null)
                {
                    return;
                }

                SetRowsInteractable(_service.IsAvailable);

                switch (result.Status)
                {
                    case CoinPurchaseStatus.Failed:
                        SetStatus(Text(FailedKey, "The purchase didn't go through. Please try again."));
                        break;
                    case CoinPurchaseStatus.Unavailable:
                        SetStatus(Text(UnavailableKey, "The store isn't available right now."));
                        break;
                    default:
                        SetStatus(string.Empty); // success: the balance pill punches; cancel: silent
                        break;
                }
            });
        }

        private void OnAvailabilityChanged()
        {
            if (IsOpen)
            {
                RefreshAvailability();
                RequestPrices();
            }
        }

        private void OnLocaleChanged()
        {
            foreach (var row in rows)
            {
                if (row != null)
                {
                    row.Redraw();
                }
            }
        }

        private void SetRowsInteractable(bool value)
        {
            foreach (var row in rows)
            {
                if (row != null)
                {
                    row.SetInteractable(value);
                }
            }
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.text = text;
            }
        }

        private string Text(string key, string fallback)
        {
            return _loc != null && _loc.TryGet(key, out var value) ? value : fallback;
        }

        private void OnClose()
        {
            Controller?.CloseTop();
        }

        private void OnTerms()
        {
            OpenUrl(config != null ? config.TermsUrl : null);
        }

        private void OnPrivacy()
        {
            OpenUrl(config != null ? config.PrivacyUrl : null);
        }

        private static void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Application.OpenURL(url);
            }
        }
    }
}
