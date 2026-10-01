using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HyperFrame.Services
{
    /// <summary>Remote values with local defaults (RC-01). Real backend adapter arrives in Phase 2.</summary>
    public interface IRemoteConfig
    {
        string ConfigVersion { get; }
        int GetInt(string key, int fallback);
        float GetFloat(string key, float fallback);
        bool GetBool(string key, bool fallback);
        string GetString(string key, string fallback);
        Task FetchAsync();
    }

    /// <summary>Serves local defaults only; values can be overridden at runtime (debug console, tests).</summary>
    public sealed class LocalRemoteConfig : IRemoteConfig
    {
        readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        public string ConfigVersion => "local";

        public LocalRemoteConfig Set(string key, object value)
        {
            _values[key] = Convert.ToString(value, CultureInfo.InvariantCulture);
            return this;
        }

        public int GetInt(string key, int fallback) =>
            _values.TryGetValue(key, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public float GetFloat(string key, float fallback) =>
            _values.TryGetValue(key, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public bool GetBool(string key, bool fallback) =>
            _values.TryGetValue(key, out var s) && bool.TryParse(s, out var v) ? v : fallback;

        public string GetString(string key, string fallback) => _values.TryGetValue(key, out var s) ? s : fallback;

        public Task FetchAsync() => Task.CompletedTask;
    }
}
