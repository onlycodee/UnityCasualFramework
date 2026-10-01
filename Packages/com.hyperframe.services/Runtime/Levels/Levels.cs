using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperFrame.Services
{
    /// <summary>Any level: a stable ID plus whatever the game's Kit needs.</summary>
    public interface ILevelData
    {
        string Id { get; }
    }

    /// <summary>Base for level ScriptableObjects (LV-01). Game/Kit level types derive from it.</summary>
    public abstract class LevelAsset : ScriptableObject, ILevelData
    {
        [SerializeField] string id = "";
        public string Id => string.IsNullOrEmpty(id) ? name : id;
    }

    public interface ILevelProvider
    {
        /// <summary>Number of designed levels. Indexes past it are endless-mode levels.</summary>
        int Count { get; }
        ILevelData Get(int index);
    }

    /// <summary>
    /// Ordered list of levels for a game: ScriptableObject levels and/or JSON TextAssets (parsed by the
    /// game's GameplayModule). After the last level, play loops from <see cref="loopFromIndex"/> (LV-06).
    /// </summary>
    [CreateAssetMenu(menuName = "HyperFrame/Levels/Level Set", fileName = "LevelSet")]
    public sealed class LevelSet : ScriptableObject
    {
        public List<LevelAsset> levels = new List<LevelAsset>();
        public List<TextAsset> jsonLevels = new List<TextAsset>();
        [Tooltip("When the player passes the last level, continue from this index (endless loop).")]
        public int loopFromIndex = 0;
    }

    /// <summary>Generic loader over a LevelSet (LV-01). JSON levels come after asset levels.</summary>
    public sealed class LevelSetProvider : ILevelProvider
    {
        readonly LevelSet _set;
        readonly Func<TextAsset, ILevelData> _parseJson;
        readonly Dictionary<int, ILevelData> _parsed = new Dictionary<int, ILevelData>();

        public LevelSetProvider(LevelSet set, Func<TextAsset, ILevelData> parseJson = null)
        {
            _set = set ?? throw new ArgumentNullException(nameof(set));
            _parseJson = parseJson;
        }

        public int Count => _set.levels.Count + _set.jsonLevels.Count;

        public ILevelData Get(int index)
        {
            if (Count == 0) throw new InvalidOperationException($"LevelSet '{_set.name}' is empty.");
            int i = LevelIndexing.Wrap(index, Count, _set.loopFromIndex);
            if (i < _set.levels.Count) return _set.levels[i];
            if (_parsed.TryGetValue(i, out var cached)) return cached;
            if (_parseJson == null) throw new InvalidOperationException("JSON levels need a parser (GameplayModule.ParseLevel).");
            var data = _parseJson(_set.jsonLevels[i - _set.levels.Count]);
            _parsed[i] = data;
            return data;
        }
    }

    /// <summary>Levels produced by a function, e.g. a seeded generator.</summary>
    public sealed class FuncLevelProvider : ILevelProvider
    {
        readonly Func<int, ILevelData> _create;
        public int Count { get; }
        public FuncLevelProvider(int count, Func<int, ILevelData> create) { Count = count; _create = create; }
        public ILevelData Get(int index) => _create(index);
    }

    public static class LevelIndexing
    {
        /// <summary>Maps any non-negative play index onto [0, count) looping from loopFrom.</summary>
        public static int Wrap(int index, int count, int loopFrom)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index < count) return index;
            loopFrom = Math.Max(0, Math.Min(loopFrom, count - 1));
            int span = count - loopFrom;
            return loopFrom + (index - count) % span;
        }
    }
}
