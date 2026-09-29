// ============================================================
// CoinPackRow — one "300 coins | $ 0.99" button in The Golden Vault (Figma 624:50).
// WHAT & WHY: The three pack buttons are identical apart from their data, so each
//   is this small component: it holds the pack, draws the label and reports the
//   tap. BuyCoinsOverlay owns everything else (prices, purchase, status).
// KEY DECISIONS:
//   - The label is ONE TMP text with rich text: the coin amount is gold (#f5ba55)
//     and the rest is parchment (#c7c8bc), as in Figma. The words come from the
//     localized format "ui.coins.pack" ("{0} coins | {1}"); only the colour tag
//     is added here, so translators never see markup.
//   - The price is whatever the store says (already localized, e.g. "R$ 5,90"),
//     or the catalog's fallback label until then.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Built by Restorium/Scene/Build Coin Shop. By hand: a Button (redButton.png)
//     with a coin icon child and a TMP "Label" child (Rich Text on); add this
//     component and drag the Button and the Label in.
// ---------------------------------------------------------------

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Economy;
    using RestoriumEmporium.Localization;

    [DisallowMultipleComponent]
    public class CoinPackRow : MonoBehaviour
    {
        private const string PackKey = "ui.coins.pack";
        private const string AmountColour = "#f5ba55";

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        private string _price = string.Empty;

        /// <summary>Raised on tap with the bound pack.</summary>
        public event Action<CoinPack> Clicked;

        public CoinPack Pack { get; private set; }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        public void Bind(CoinPack pack)
        {
            Pack = pack;
            _price = pack != null ? pack.fallbackPriceLabel : string.Empty;
            Redraw();
        }

        public void SetPrice(string priceString)
        {
            if (!string.IsNullOrEmpty(priceString))
            {
                _price = priceString;
                Redraw();
            }
        }

        public void SetInteractable(bool value)
        {
            if (button != null)
            {
                button.interactable = value;
            }
        }

        /// <summary>Re-reads the localized format (call on LocaleChanged).</summary>
        public void Redraw()
        {
            if (label == null || Pack == null)
            {
                return;
            }

            var amount = $"<color={AmountColour}>{Pack.coins}</color>";
            var loc = Core.ServiceLocator.Get<ILocalizationService>();

            label.text = loc != null && loc.TryGet(PackKey, out _)
                ? loc.Format(PackKey, amount, _price)
                : $"{amount} coins | {_price}";
        }

        private void OnClick()
        {
            if (Pack != null)
            {
                Clicked?.Invoke(Pack);
            }
        }
    }
}
