using System;
using System.Collections.Generic;
using HyperFrame.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyperFrame.Services
{
    /// <summary>
    /// Upgrades a save from <see cref="FromVersion"/> to FromVersion + 1 by editing the raw JSON.
    /// Root shape: { "version": n, "sections": { "&lt;section&gt;": { ... } } }.
    /// </summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        void Apply(JObject root);
    }

    /// <summary>A migration from a delegate.</summary>
    public sealed class SaveMigration : ISaveMigration
    {
        readonly Action<JObject> _apply;
        public int FromVersion { get; }
        public SaveMigration(int fromVersion, Action<JObject> apply) { FromVersion = fromVersion; _apply = apply; }
        public void Apply(JObject root) => _apply(root);
    }

    public interface ISaveService
    {
        int SchemaVersion { get; }
        bool IsDirty { get; }
        /// <summary>Returns a section, or a new T when absent.</summary>
        T Get<T>(string section) where T : new();
        void Set<T>(string section, T value);
        bool Has(string section);
        void Delete(string section);
        void Load();
        void Save();
        /// <summary>Saves only if something changed since the last save.</summary>
        void SaveIfDirty();
        void DeleteAll();
        event Action Saved;
    }

    /// <summary>
    /// JSON save with a schema version and ordered migrations (SV-01). Each system stores its data in a
    /// named section. A corrupt or unreadable file is backed up as "&lt;key&gt;.corrupt" and the game
    /// starts fresh instead of crashing. Autosaves on app pause and quit when given an event bus.
    /// </summary>
    public sealed class SaveService : ISaveService, IDisposable
    {
        const string VersionKey = "version";
        const string SectionsKey = "sections";

        readonly ISaveStorage _storage;
        readonly string _fileKey;
        readonly SortedDictionary<int, ISaveMigration> _migrations = new SortedDictionary<int, ISaveMigration>();
        readonly JsonSerializer _serializer;
        readonly EventSubscriptions _subscriptions = new EventSubscriptions();
        JObject _sections = new JObject();

        public int SchemaVersion { get; }
        public bool IsDirty { get; private set; }
        public event Action Saved;

        public SaveService(ISaveStorage storage, int schemaVersion = 1, IEnumerable<ISaveMigration> migrations = null,
            IEventBus events = null, string fileKey = "save")
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            SchemaVersion = Math.Max(1, schemaVersion);
            _fileKey = fileKey;
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            });
            if (migrations != null)
                foreach (var m in migrations) _migrations[m.FromVersion] = m;
            if (events != null)
            {
                _subscriptions.Add(events.Subscribe<AppPauseEvent>(e => { if (e.Paused) SaveIfDirty(); }));
                _subscriptions.Add(events.Subscribe<AppQuitEvent>(_ => SaveIfDirty()));
            }
        }

        public T Get<T>(string section) where T : new()
        {
            var token = _sections[section];
            if (token == null || token.Type == JTokenType.Null) return new T();
            try { return token.ToObject<T>(_serializer) ?? new T(); }
            catch (Exception e)
            {
                HFLog.Error("Save", $"Section '{section}' could not be read as {typeof(T).Name}: {e.Message}. Using defaults.");
                return new T();
            }
        }

        public void Set<T>(string section, T value)
        {
            _sections[section] = value == null ? JValue.CreateNull() : JToken.FromObject(value, _serializer);
            IsDirty = true;
        }

        public bool Has(string section) => _sections[section] != null;

        public void Delete(string section)
        {
            if (_sections.Remove(section)) IsDirty = true;
        }

        public void Load()
        {
            _sections = new JObject();
            IsDirty = false;
            if (!_storage.TryRead(_fileKey, out var json) || string.IsNullOrWhiteSpace(json)) return;

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception e)
            {
                Quarantine(json, $"unreadable JSON: {e.Message}");
                return;
            }

            int version = root.Value<int?>(VersionKey) ?? 1;
            if (version > SchemaVersion)
            {
                Quarantine(json, $"save version {version} is newer than this build ({SchemaVersion})");
                return;
            }

            while (version < SchemaVersion)
            {
                if (!_migrations.TryGetValue(version, out var migration))
                {
                    Quarantine(json, $"no migration from version {version}");
                    return;
                }
                try { migration.Apply(root); }
                catch (Exception e)
                {
                    Quarantine(json, $"migration from {version} failed: {e.Message}");
                    return;
                }
                version++;
                root[VersionKey] = version;
                IsDirty = true; // write the upgraded file back
            }

            _sections = root[SectionsKey] as JObject ?? new JObject();
        }

        void Quarantine(string json, string reason)
        {
            HFLog.Error("Save", $"Save file reset ({reason}). Backup written to '{_fileKey}.corrupt'.");
            try { _storage.Write(_fileKey + ".corrupt", json); }
            catch (Exception e) { HFLog.Exception(e, "Save"); }
            _sections = new JObject();
            IsDirty = true;
        }

        public void Save()
        {
            var root = new JObject
            {
                [VersionKey] = SchemaVersion,
                ["savedAtUtc"] = DateTime.UtcNow.ToString("o"),
                [SectionsKey] = _sections
            };
            try
            {
                _storage.Write(_fileKey, root.ToString(Formatting.None));
                IsDirty = false;
                Saved?.Invoke();
            }
            catch (Exception e)
            {
                HFLog.Exception(e, "Save");
            }
        }

        public void SaveIfDirty()
        {
            if (IsDirty) Save();
        }

        public void DeleteAll()
        {
            _sections = new JObject();
            _storage.Delete(_fileKey);
            IsDirty = false;
        }

        public void Dispose() => _subscriptions.Dispose();
    }
}
