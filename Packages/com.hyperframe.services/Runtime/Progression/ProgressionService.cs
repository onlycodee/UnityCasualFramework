using System;
using System.Collections.Generic;

namespace HyperFrame.Services
{
    public interface IProgressionService
    {
        /// <summary>0-based index of the level the Play button starts.</summary>
        int CurrentLevelIndex { get; }
        int HighestUnlockedIndex { get; }
        int TotalStars { get; }
        /// <summary>Attempts at the current level since it was last completed (analytics "attempt").</summary>
        int AttemptsOnCurrent { get; }
        int GetStars(int levelIndex);
        bool IsUnlocked(int levelIndex);
        void RecordAttempt(int levelIndex);
        /// <summary>Marks a level complete, keeps the best star count and unlocks the next level.</summary>
        void CompleteLevel(int levelIndex, int stars);
        /// <summary>Selects a level (level select, debug jump). Unlocks it if needed.</summary>
        void SetCurrentLevel(int levelIndex);
        event Action Changed;
    }

    /// <summary>Linear level progression with stars (PG-01), persisted in the "progression" save section.</summary>
    public sealed class ProgressionService : IProgressionService
    {
        public const string Section = "progression";

        [Serializable]
        sealed class Data
        {
            public int current;
            public int highestUnlocked;
            public int attempts;
            public List<int> stars = new List<int>();
        }

        readonly ISaveService _save;
        readonly Data _data;
        public event Action Changed;

        public ProgressionService(ISaveService save)
        {
            _save = save;
            _data = save.Get<Data>(Section);
            if (_data.stars == null) _data.stars = new List<int>();
        }

        public int CurrentLevelIndex => _data.current;
        public int HighestUnlockedIndex => _data.highestUnlocked;
        public int AttemptsOnCurrent => _data.attempts;

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (var s in _data.stars) total += s;
                return total;
            }
        }

        public int GetStars(int levelIndex) =>
            levelIndex >= 0 && levelIndex < _data.stars.Count ? _data.stars[levelIndex] : 0;

        public bool IsUnlocked(int levelIndex) => levelIndex >= 0 && levelIndex <= _data.highestUnlocked;

        public void RecordAttempt(int levelIndex)
        {
            if (levelIndex == _data.current) _data.attempts++;
            Persist();
        }

        public void CompleteLevel(int levelIndex, int stars)
        {
            if (levelIndex < 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            while (_data.stars.Count <= levelIndex) _data.stars.Add(0);
            _data.stars[levelIndex] = Math.Max(_data.stars[levelIndex], Math.Max(0, Math.Min(3, stars)));
            _data.highestUnlocked = Math.Max(_data.highestUnlocked, levelIndex + 1);
            if (levelIndex == _data.current)
            {
                _data.current = levelIndex + 1;
                _data.attempts = 0;
            }
            Persist();
        }

        public void SetCurrentLevel(int levelIndex)
        {
            if (levelIndex < 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            _data.current = levelIndex;
            _data.highestUnlocked = Math.Max(_data.highestUnlocked, levelIndex);
            _data.attempts = 0;
            Persist();
        }

        void Persist()
        {
            _save.Set(Section, _data);
            Changed?.Invoke();
        }
    }
}
