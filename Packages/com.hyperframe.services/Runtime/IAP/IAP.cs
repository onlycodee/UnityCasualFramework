using System.Collections.Generic;
using System.Threading.Tasks;

namespace HyperFrame.Services
{
    public enum PurchaseResult { Success, Cancelled, Failed, AlreadyOwned }

    public static class Products
    {
        public const string RemoveAds = "remove_ads";
    }

    public interface IIAPService
    {
        bool IsOwned(string productId);
        string GetPriceString(string productId);
        Task<PurchaseResult> Purchase(string productId);
        Task<bool> RestorePurchases();
    }

    /// <summary>Editor/test store (IAP-01 mock). Purchases succeed unless <see cref="NextResult"/> says otherwise.</summary>
    public sealed class MockIAPService : IIAPService
    {
        readonly HashSet<string> _owned = new HashSet<string>();
        readonly IAnalyticsService _analytics;
        public PurchaseResult NextResult = PurchaseResult.Success;

        public MockIAPService(IAnalyticsService analytics = null) => _analytics = analytics;

        public bool IsOwned(string productId) => _owned.Contains(productId);
        public string GetPriceString(string productId) => "$0.99 (mock)";

        public Task<PurchaseResult> Purchase(string productId)
        {
            if (_owned.Contains(productId)) return Task.FromResult(PurchaseResult.AlreadyOwned);
            if (NextResult == PurchaseResult.Success)
            {
                _owned.Add(productId);
                _analytics?.IapPurchase(productId, 0.99m, "USD");
            }
            return Task.FromResult(NextResult);
        }

        public Task<bool> RestorePurchases() => Task.FromResult(true);
    }
}
