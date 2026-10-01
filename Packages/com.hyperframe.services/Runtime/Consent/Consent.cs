using System.Threading.Tasks;

namespace HyperFrame.Services
{
    public enum ConsentStatus { Unknown, Granted, Denied, NotRequired }

    /// <summary>GDPR/UMP + iOS ATT flow (CN-01). Tracking SDKs initialize only after this resolves.</summary>
    public interface IConsentService
    {
        ConsentStatus Status { get; }
        Task<ConsentStatus> RequestAsync();
    }

    /// <summary>Resolves immediately with a fixed status. Real UMP/ATT adapter arrives in Phase 2.</summary>
    public sealed class MockConsentService : IConsentService
    {
        readonly ConsentStatus _result;
        public ConsentStatus Status { get; private set; } = ConsentStatus.Unknown;
        public MockConsentService(ConsentStatus result = ConsentStatus.NotRequired) => _result = result;
        public Task<ConsentStatus> RequestAsync()
        {
            Status = _result;
            return Task.FromResult(Status);
        }
    }
}
