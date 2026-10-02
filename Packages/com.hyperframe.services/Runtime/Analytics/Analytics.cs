using System;
using System.Collections.Generic;
using HyperFrame.Core;

namespace HyperFrame.Services
{
    public sealed class AnalyticsEvent
    {
        public string Name;
        public Dictionary<string, object> Parameters;
        public DateTime TimestampUtc;

        public override string ToString()
        {
            var parts = new List<string>();
            if (Parameters != null) foreach (var kv in Parameters) parts.Add($"{kv.Key}={kv.Value}");
            return $"{Name}({string.Join(", ", parts)})";
        }
    }

    /// <summary>A destination such as Firebase, GameAnalytics or an attribution SDK (AN-01). One adapter per SDK package.</summary>
    public interface IAnalyticsBackend
    {
        string Name { get; }
        void Send(AnalyticsEvent evt);
    }

    public interface IAnalyticsService
    {
        void Log(string eventName, Dictionary<string, object> parameters = null);
        void AddBackend(IAnalyticsBackend backend);
        bool Enabled { get; set; }
        event Action<AnalyticsEvent> Logged;
    }

    /// <summary>
    /// The standard event schema shared by all games (AN-02, PRD Appendix C).
    /// Add new events here first; the dispatcher warns about unknown events or missing parameters.
    /// </summary>
    public static class AnalyticsSchema
    {
        public const string SessionStart = "session_start";
        public const string LevelStart = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelFail = "level_fail";
        public const string TutorialStep = "tutorial_step";
        public const string BoosterUse = "booster_use";
        public const string AdRequest = "ad_request";
        public const string AdImpression = "ad_impression";
        public const string AdReward = "ad_reward";
        public const string IapPurchase = "iap_purchase";
        public const string CurrencyChange = "currency_change";
        public const string RemoteConfigApplied = "remote_config_applied";

        public static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            [SessionStart] = new[] { "session_number", "days_since_install" },
            [LevelStart] = new[] { "level_id", "level_index", "attempt", "difficulty" },
            [LevelComplete] = new[] { "level_id", "duration_s", "moves", "stars", "boosters_used" },
            [LevelFail] = new[] { "level_id", "duration_s", "moves", "reason" },
            [TutorialStep] = new[] { "step_id", "step_index" },
            [BoosterUse] = new[] { "booster_id", "level_id", "source" },
            [AdRequest] = new[] { "format", "placement", "network" },
            [AdImpression] = new[] { "format", "placement", "network" },
            [AdReward] = new[] { "format", "placement", "network" },
            [IapPurchase] = new[] { "product_id", "price", "currency" },
            [CurrencyChange] = new[] { "currency", "delta", "source", "balance" },
            [RemoteConfigApplied] = new[] { "config_version", "ab_group" },
        };

        /// <summary>Game-specific events registered at boot (name → required params).</summary>
        public static void RegisterCustom(string eventName, params string[] requiredParams) =>
            Required[eventName] = requiredParams ?? Array.Empty<string>();

        /// <summary>Returns null when valid, otherwise a description of the problem.</summary>
        public static string Validate(string name, IReadOnlyDictionary<string, object> parameters)
        {
            if (!Required.TryGetValue(name, out var required))
                return $"Event '{name}' is not in the schema. Add it to AnalyticsSchema (or RegisterCustom) first.";
            foreach (var key in required)
                if (parameters == null || !parameters.ContainsKey(key))
                    return $"Event '{name}' is missing parameter '{key}'.";
            return null;
        }
    }

    /// <summary>Typed helpers for the standard events. Prefer these over raw Log calls.</summary>
    public static class AnalyticsExtensions
    {
        public static void SessionStart(this IAnalyticsService a, int sessionNumber, int daysSinceInstall) =>
            a.Log(AnalyticsSchema.SessionStart, new Dictionary<string, object>
            { ["session_number"] = sessionNumber, ["days_since_install"] = daysSinceInstall });

        public static void LevelStart(this IAnalyticsService a, string levelId, int levelIndex, int attempt, string difficulty) =>
            a.Log(AnalyticsSchema.LevelStart, new Dictionary<string, object>
            { ["level_id"] = levelId, ["level_index"] = levelIndex, ["attempt"] = attempt, ["difficulty"] = difficulty ?? "" });

        public static void LevelComplete(this IAnalyticsService a, string levelId, float durationS, int moves, int stars, int boostersUsed) =>
            a.Log(AnalyticsSchema.LevelComplete, new Dictionary<string, object>
            { ["level_id"] = levelId, ["duration_s"] = Round(durationS), ["moves"] = moves, ["stars"] = stars, ["boosters_used"] = boostersUsed });

        public static void LevelFail(this IAnalyticsService a, string levelId, float durationS, int moves, string reason) =>
            a.Log(AnalyticsSchema.LevelFail, new Dictionary<string, object>
            { ["level_id"] = levelId, ["duration_s"] = Round(durationS), ["moves"] = moves, ["reason"] = reason ?? "" });

        public static void TutorialStep(this IAnalyticsService a, string stepId, int stepIndex) =>
            a.Log(AnalyticsSchema.TutorialStep, new Dictionary<string, object> { ["step_id"] = stepId, ["step_index"] = stepIndex });

        public static void BoosterUse(this IAnalyticsService a, string boosterId, string levelId, string source) =>
            a.Log(AnalyticsSchema.BoosterUse, new Dictionary<string, object>
            { ["booster_id"] = boosterId, ["level_id"] = levelId, ["source"] = source });

        public static void Ad(this IAnalyticsService a, string eventName, AdFormat format, string placement, string network) =>
            a.Log(eventName, new Dictionary<string, object>
            { ["format"] = format.ToString().ToLowerInvariant(), ["placement"] = placement, ["network"] = network });

        public static void IapPurchase(this IAnalyticsService a, string productId, decimal price, string currency) =>
            a.Log(AnalyticsSchema.IapPurchase, new Dictionary<string, object>
            { ["product_id"] = productId, ["price"] = price, ["currency"] = currency });

        public static void CurrencyChange(this IAnalyticsService a, CurrencyChangedEvent e) =>
            a.Log(AnalyticsSchema.CurrencyChange, new Dictionary<string, object>
            { ["currency"] = e.Currency, ["delta"] = e.Delta, ["source"] = e.Source, ["balance"] = e.Balance });

        static double Round(float v) => Math.Round(v, 2);
    }

    /// <summary>Validates events against the schema and fans them out to every backend (AN-01).</summary>
    public sealed class AnalyticsDispatcher : IAnalyticsService
    {
        readonly List<IAnalyticsBackend> _backends = new List<IAnalyticsBackend>();
        public bool Enabled { get; set; } = true;
        /// <summary>Throw on schema errors instead of warning (tests).</summary>
        public bool StrictSchema { get; set; }
        public event Action<AnalyticsEvent> Logged;

        public void AddBackend(IAnalyticsBackend backend)
        {
            if (backend != null && !_backends.Contains(backend)) _backends.Add(backend);
        }

        public void Log(string eventName, Dictionary<string, object> parameters = null)
        {
            var problem = AnalyticsSchema.Validate(eventName, parameters);
            if (problem != null)
            {
                if (StrictSchema) throw new ArgumentException(problem);
                HFLog.Warn("Analytics", problem);
            }
            if (!Enabled) return;

            var evt = new AnalyticsEvent
            {
                Name = eventName,
                Parameters = parameters ?? new Dictionary<string, object>(),
                TimestampUtc = DateTime.UtcNow
            };
            foreach (var b in _backends)
            {
                try { b.Send(evt); }
                catch (Exception e) { HFLog.Exception(e, "Analytics." + b.Name); }
            }
            Logged?.Invoke(evt);
        }
    }

    /// <summary>Keeps the last N events in memory: used by tests and the dev overlay (AN-03).</summary>
    public sealed class InMemoryAnalyticsBackend : IAnalyticsBackend
    {
        readonly int _capacity;
        public readonly List<AnalyticsEvent> Events = new List<AnalyticsEvent>();
        public string Name => "memory";

        public InMemoryAnalyticsBackend(int capacity = 200) => _capacity = capacity;

        public void Send(AnalyticsEvent evt)
        {
            Events.Add(evt);
            if (Events.Count > _capacity) Events.RemoveAt(0);
        }

        public int Count(string name) => Events.FindAll(e => e.Name == name).Count;
        public AnalyticsEvent Last(string name) => Events.FindLast(e => e.Name == name);
    }

    /// <summary>Logs events to the console (Editor/dev builds).</summary>
    public sealed class LogAnalyticsBackend : IAnalyticsBackend
    {
        public string Name => "log";
        public void Send(AnalyticsEvent evt) => HFLog.Debug("Analytics", evt.ToString());
    }
}
