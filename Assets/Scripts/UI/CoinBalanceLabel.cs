// ============================================================
// CoinBalanceLabel — keeps one text label showing the player's coin balance.
// WHAT & WHY: The coin count appears on the desk hub, in preview mode, in the
//   shop and (maybe) on other screens. Instead of every screen re-implementing
//   "read IWallet, listen for changes, redraw", any label just gets this
//   component. Reusable by every agent / screen.
// KEY DECISIONS:
//   - Listens to IWallet.CoinsChanged while enabled (subscribe in OnEnable,
//     unsubscribe in OnDisable), and redraws on enable, so a label on a hidden
//     screen costs nothing and is correct the moment it is shown.
//   - Services are resolved lazily: OnEnable can run before the Systems object
//     registered the wallet, so Start retries once (same pattern as LocalizedText).
//   - Only redraws when the balance actually changes: the int->string conversion
//     is the only allocation and it happens on a coin change, never per frame.
//   - Optional "punch" scale when coins are gained or spent, on unscaled time, so
//     it still animates while the game is paused under an overlay.
//   - No wallet (scene opened directly) shows "0" instead of a blank label.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Select the TextMeshPro label that should show the coin number
//     (e.g. DeskHubScreen/NormalUI/CoinsBadge/CoinsLabel).
// [ ] Add Component -> Coin Balance Label. The Label field fills itself with the
//     TMP text on the same object; you can also drag another label in.
// [ ] Optional: tick "Punch On Change" (on by default) for a small bounce.
// [ ] Do NOT add a Localized Text component to the same label.
// ---------------------------------------------------------------

using System.Collections;
using TMPro;
using UnityEngine;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Economy;

    [DisallowMultipleComponent]
    public class CoinBalanceLabel : MonoBehaviour
    {
        [Tooltip("The label that shows the number. Defaults to the TMP text on this object.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Bounce the label a little when the balance changes.")]
        [SerializeField] private bool punchOnChange = true;

        [Tooltip("How big the bounce gets (1 = none).")]
        [SerializeField] private float punchScale = 1.2f;

        [Tooltip("Bounce length in seconds (unscaled time).")]
        [SerializeField] private float punchSeconds = 0.25f;

        private IWallet _wallet;
        private bool _subscribed;
        private int _shown = int.MinValue;
        private Coroutine _punch;
        private Vector3 _baseScale = Vector3.one;

        /// <summary>The balance currently drawn.</summary>
        public int Shown => _shown;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponent<TMP_Text>();
            }

            if (label != null)
            {
                _baseScale = label.rectTransform.localScale;
            }
        }

        private void OnEnable()
        {
            Bind();
            Redraw(false);
        }

        private void Start()
        {
            if (_wallet != null)
            {
                return;
            }

            Bind();

            if (_wallet == null)
            {
                Debug.LogWarning("[CoinBalanceLabel] No IWallet registered; showing 0.", this);
            }

            Redraw(false);
        }

        private void OnDisable()
        {
            if (_subscribed && _wallet != null)
            {
                _wallet.CoinsChanged -= OnCoinsChanged;
            }

            _subscribed = false;

            if (_punch != null)
            {
                StopCoroutine(_punch);
                _punch = null;
            }

            if (label != null)
            {
                label.rectTransform.localScale = _baseScale;
            }
        }

        private void Bind()
        {
            if (_subscribed)
            {
                return;
            }

            if (_wallet == null)
            {
                _wallet = ServiceLocator.Get<IWallet>();
            }

            if (_wallet == null)
            {
                return;
            }

            _wallet.CoinsChanged += OnCoinsChanged;
            _subscribed = true;
        }

        private void OnCoinsChanged(int balance, int delta) => Redraw(true);

        /// <summary>Forces a redraw from the wallet. Safe to call any time.</summary>
        public void Redraw(bool animate)
        {
            if (label == null)
            {
                return;
            }

            var coins = _wallet != null ? _wallet.Coins : 0;

            if (coins == _shown)
            {
                return;
            }

            _shown = coins;
            label.text = coins.ToString();

            if (animate && punchOnChange && isActiveAndEnabled)
            {
                if (_punch != null)
                {
                    StopCoroutine(_punch);
                }

                _punch = StartCoroutine(Punch());
            }
        }

        private IEnumerator Punch()
        {
            var rt = label.rectTransform;
            var t = 0f;
            var duration = Mathf.Max(0.01f, punchSeconds);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                rt.localScale = _baseScale * Mathf.Lerp(1f, punchScale, k);
                yield return null;
            }

            rt.localScale = _baseScale;
            _punch = null;
        }
    }
}
