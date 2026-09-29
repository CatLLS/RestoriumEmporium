// ============================================================
// RevenueCatConfig — the RevenueCat PUBLIC SDK keys and store links, in one asset.
// WHAT & WHY: The keys have to live somewhere a build can read them. RevenueCat's
//   public SDK keys (test_..., goog_..., appl_...) are designed to ship inside
//   the app, so committing them is fine. SECRET keys (sk_...) are never allowed:
//   StoreKeyPolicy refuses them at runtime and OnValidate shouts in the Inspector.
//   Docs/RevenueCat.md explains where each key comes from.
// KEY DECISIONS:
//   - A ScriptableObject rather than fields on the store component, so the key
//     is in one obvious file (Assets/Data/Config/RevenueCatConfig.asset) and not
//     buried in the Systems prefab.
//   - Lives in the Runtime assembly (plain strings, no SDK types), so the coin
//     screen can read the Terms / Privacy links without depending on the SDK.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Select Assets/Data/Config/RevenueCatConfig.asset and paste the Test Store
//     public key into "Test Store Api Key". See Docs/RevenueCat.md.
// [ ] Never paste a key that starts with sk_ (secret). Only public SDK keys.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Economy
{
    [CreateAssetMenu(fileName = "RevenueCatConfig", menuName = "Restorium/RevenueCat Config")]
    public class RevenueCatConfig : ScriptableObject
    {
        [Header("Public SDK keys (never sk_ secret keys)")]
        [Tooltip("RevenueCat Test Store public key, starts with test_. Used ONLY in Development builds " +
                 "and the Editor: the SDK crashes release builds that use it, on purpose.")]
        [SerializeField] private string testStoreApiKey = string.Empty;

        [Tooltip("Google Play public key, starts with goog_. Used by release Android builds.")]
        [SerializeField] private string googlePlayApiKey = string.Empty;

        [Tooltip("App Store public key, starts with appl_. Used by release iOS builds.")]
        [SerializeField] private string appleApiKey = string.Empty;

        [Header("Catalog")]
        [Tooltip("Offering that holds the coin packs. Falls back to the Current offering if missing.")]
        [SerializeField] private string offeringId = "coins";

        [Header("Links on the coin screen (empty = link disabled)")]
        [SerializeField] private string termsUrl = string.Empty;
        [SerializeField] private string privacyUrl = string.Empty;

        public string OfferingId => offeringId;
        public string TermsUrl => termsUrl;
        public string PrivacyUrl => privacyUrl;

        /// <summary>The key for this build, or false with a reason. See StoreKeyPolicy.</summary>
        public bool TryChooseKey(out string key, out string problem)
        {
            var platformKey = Application.platform == RuntimePlatform.IPhonePlayer ||
                              Application.platform == RuntimePlatform.OSXPlayer
                ? appleApiKey
                : googlePlayApiKey;

            return StoreKeyPolicy.TryChoose(testStoreApiKey, platformKey, Debug.isDebugBuild, out key, out problem);
        }

        private void OnValidate()
        {
            if (StoreKeyPolicy.IsSecretKey(testStoreApiKey) || StoreKeyPolicy.IsSecretKey(googlePlayApiKey) ||
                StoreKeyPolicy.IsSecretKey(appleApiKey))
            {
                Debug.LogError("[RevenueCatConfig] A SECRET key (sk_...) was pasted here. Remove it before " +
                               "committing and rotate it in the RevenueCat dashboard. Only public SDK keys " +
                               "(test_, goog_, appl_) belong in the app.", this);
            }

            if (StoreKeyPolicy.IsTestStoreKey(googlePlayApiKey) || StoreKeyPolicy.IsTestStoreKey(appleApiKey))
            {
                Debug.LogWarning("[RevenueCatConfig] A test_ key is in a platform field. It goes in " +
                                 "'Test Store Api Key'; release builds will refuse it.", this);
            }
        }
    }
}
