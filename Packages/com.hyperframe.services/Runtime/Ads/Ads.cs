using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HyperFrame.Services
{
    public enum AdFormat { Interstitial, Rewarded, Banner }
    public enum AdResult { Completed, Skipped, Failed, NotReady, Disabled }

    public interface IAdsService
    {
        bool IsReady(AdFormat format);
        Task<AdResult> ShowInterstitial(string placement);
        /// <summary>Grant the reward only when the result is Completed.</summary>
        Task<AdResult> ShowRewarded(string placement);
        void ShowBanner();
        void HideBanner();
        /// <summary>Set by the "No Ads" purchase. Disables interstitials and banners, never rewarded ads.</summary>
        bool AdsRemoved { get; set; }
    }

    /// <summary>
    /// Editor/test ads (AD-03). By default every ad returns <see cref="NextResult"/>. Set
    /// <see cref="Presenter"/> to show a popup with Complete / Skip / Fail buttons instead.
    /// Logs ad_request / ad_impression / ad_reward like a real adapter would.
    /// </summary>
    public sealed class MockAdsService : IAdsService
    {
        readonly IAnalyticsService _analytics;
        public AdResult NextResult = AdResult.Completed;
        public bool Ready = true;
        public bool AdsRemoved { get; set; }
        public bool BannerVisible { get; private set; }
        public Func<AdFormat, string, Task<AdResult>> Presenter;
        public readonly List<string> Shown = new List<string>();

        public MockAdsService(IAnalyticsService analytics = null) => _analytics = analytics;

        public bool IsReady(AdFormat format) => Ready && !(AdsRemoved && format != AdFormat.Rewarded);

        public Task<AdResult> ShowInterstitial(string placement) => Show(AdFormat.Interstitial, placement);
        public Task<AdResult> ShowRewarded(string placement) => Show(AdFormat.Rewarded, placement);

        async Task<AdResult> Show(AdFormat format, string placement)
        {
            if (AdsRemoved && format != AdFormat.Rewarded) return AdResult.Disabled;
            _analytics?.Ad(AnalyticsSchema.AdRequest, format, placement, "mock");
            if (!Ready) return AdResult.NotReady;
            var result = Presenter != null ? await Presenter(format, placement) : NextResult;
            if (result == AdResult.Completed || result == AdResult.Skipped)
            {
                Shown.Add($"{format}:{placement}");
                _analytics?.Ad(AnalyticsSchema.AdImpression, format, placement, "mock");
            }
            if (format == AdFormat.Rewarded && result == AdResult.Completed)
                _analytics?.Ad(AnalyticsSchema.AdReward, format, placement, "mock");
            return result;
        }

        public void ShowBanner() => BannerVisible = !AdsRemoved;
        public void HideBanner() => BannerVisible = false;
    }
}
