// ============================================================
// StoreKeyPolicy — which RevenueCat API key a build may use, and which it must refuse.
// WHAT & WHY: Two mistakes here are expensive. A SECRET key (sk_...) in a shipped
//   app hands anyone full control of the RevenueCat project. A TEST STORE key
//   (test_...) in a release build makes the RevenueCat SDK crash the app on purpose,
//   and Unity cannot opt out of that. This one rule decides the key before the SDK
//   ever sees it, so neither mistake can reach a player.
// KEY DECISIONS:
//   - Development builds (Debug.isDebugBuild: the Editor, or "Development Build"
//     ticked) use the Test Store key when one is set; otherwise the platform key.
//   - Release builds never use a test_ key, from either field. The store is then
//     simply unavailable (the coin screen says so) instead of crashing.
//   - sk_ keys are refused everywhere; the message says to rotate the key.
//   - Plain C#, so the rules are unit-tested (CoinPurchaseServiceTests).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Used by RevenueCatConfig / RevenueCatCoinStore.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Economy
{
    public static class StoreKeyPolicy
    {
        public const string SecretPrefix = "sk_";
        public const string TestStorePrefix = "test_";

        public static bool IsSecretKey(string key) =>
            !string.IsNullOrEmpty(key) && key.Trim().StartsWith(SecretPrefix, StringComparison.Ordinal);

        public static bool IsTestStoreKey(string key) =>
            !string.IsNullOrEmpty(key) && key.Trim().StartsWith(TestStorePrefix, StringComparison.Ordinal);

        /// <summary>
        /// Picks the key to configure the SDK with. False (and a reason for the log)
        /// when no safe key exists for this build.
        /// </summary>
        public static bool TryChoose(string testStoreKey, string platformKey, bool isDebugBuild,
            out string key, out string problem)
        {
            testStoreKey = testStoreKey?.Trim() ?? string.Empty;
            platformKey = platformKey?.Trim() ?? string.Empty;

            var candidate = isDebugBuild && testStoreKey.Length > 0 ? testStoreKey : platformKey;
            key = null;

            if (candidate.Length == 0)
            {
                problem = isDebugBuild
                    ? "No RevenueCat key set. Paste the Test Store public key (test_...) into RevenueCatConfig."
                    : "No RevenueCat platform key set for this release build (goog_... / appl_...).";
                return false;
            }

            if (IsSecretKey(candidate))
            {
                problem = "A SECRET RevenueCat key (sk_...) is in RevenueCatConfig. It was NOT used. Remove it " +
                          "from the project and rotate it in the RevenueCat dashboard; apps only take public keys.";
                return false;
            }

            if (!isDebugBuild && IsTestStoreKey(candidate))
            {
                problem = "A Test Store key (test_...) cannot be used in a release build (the SDK would crash " +
                          "the app on purpose). Tick Development Build, or set the platform key.";
                return false;
            }

            key = candidate;
            problem = null;
            return true;
        }
    }
}
