// ============================================================
// ICoinStore — the thing that actually takes the player's money for a coin pack.
// WHAT & WHY: The real store is RevenueCat (RevenueCatCoinStore, in its own
//   assembly next to the SDK under Assets/Integrations/RevenueCat). The SDK does
//   not run in the Unity Editor, so the game talks to this small interface and
//   the Editor gets SimulatedCoinStore instead. The store only moves money; it
//   never touches coins. CoinPurchaseService turns a successful purchase into
//   coins, exactly once per transaction.
// KEY DECISIONS:
//   - Callbacks, not async/Task: the RevenueCat Unity SDK is callback-based and
//     calls back on the main thread, and so does everything else in this game.
//   - Every callback is invoked EXACTLY once per call, including when the store
//     is not configured (Status Unavailable / an error string). Callers rely on
//     that to re-enable their buttons.
//   - Plain data types with no UnityEngine reference, so CoinPurchaseService and
//     its tests stay plain C#.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by RevenueCatCoinStore (device builds) and
//     SimulatedCoinStore (Editor / Development builds); GameBootstrap picks one.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Economy
{
    public interface ICoinStore
    {
        /// <summary>True once the store is configured and can sell.</summary>
        bool IsReady { get; }

        /// <summary>Raised when IsReady changes.</summary>
        event Action ReadyChanged;

        /// <summary>
        /// Looks up the store's localized prices. <c>done(products, error)</c>: the
        /// list holds only products the store knows; error is null on success.
        /// </summary>
        void FetchProducts(IReadOnlyList<string> productIds, Action<IReadOnlyList<CoinStoreProduct>, string> done);

        /// <summary>Starts a purchase. The store shows its own confirmation UI.</summary>
        void Purchase(string productId, Action<CoinPurchaseResult> done);

        /// <summary>
        /// Every one-time purchase this store user has made, newest last. Used to
        /// grant coins for a purchase that was paid for but never granted (app
        /// killed between the payment and the callback).
        /// </summary>
        void FetchPastTransactions(Action<IReadOnlyList<CoinTransaction>, string> done);
    }

    public enum CoinPurchaseStatus
    {
        Success = 0,
        Cancelled = 1,
        Failed = 2,
        Unavailable = 3
    }

    /// <summary>A product as the store sells it: id plus the price string to show ("$0.99", "R$ 5,90").</summary>
    public readonly struct CoinStoreProduct
    {
        public readonly string ProductId;
        public readonly string PriceString;

        public CoinStoreProduct(string productId, string priceString)
        {
            ProductId = productId;
            PriceString = priceString;
        }
    }

    /// <summary>One completed one-time purchase.</summary>
    public readonly struct CoinTransaction
    {
        public readonly string TransactionId;
        public readonly string ProductId;

        public CoinTransaction(string transactionId, string productId)
        {
            TransactionId = transactionId;
            ProductId = productId;
        }
    }

    public sealed class CoinPurchaseResult
    {
        public readonly CoinPurchaseStatus Status;
        public readonly string ProductId;

        /// <summary>The store's transaction id. Only set on Success.</summary>
        public readonly string TransactionId;

        /// <summary>Developer-facing detail for the log. Never shown to the player.</summary>
        public readonly string Message;

        private CoinPurchaseResult(CoinPurchaseStatus status, string productId, string transactionId, string message)
        {
            Status = status;
            ProductId = productId;
            TransactionId = transactionId;
            Message = message;
        }

        public static CoinPurchaseResult Success(string productId, string transactionId) =>
            new CoinPurchaseResult(CoinPurchaseStatus.Success, productId, transactionId, null);

        public static CoinPurchaseResult Cancelled(string productId) =>
            new CoinPurchaseResult(CoinPurchaseStatus.Cancelled, productId, null, null);

        public static CoinPurchaseResult Failed(string productId, string message) =>
            new CoinPurchaseResult(CoinPurchaseStatus.Failed, productId, null, message);

        public static CoinPurchaseResult Unavailable(string productId, string message) =>
            new CoinPurchaseResult(CoinPurchaseStatus.Unavailable, productId, null, message);

        public override string ToString() =>
            $"{Status} product='{ProductId}' tx='{TransactionId}' {Message}";
    }
}
