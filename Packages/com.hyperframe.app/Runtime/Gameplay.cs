using System;
using System.Collections.Generic;
using HyperFrame.Audio;
using HyperFrame.Core;
using HyperFrame.Feedback;
using HyperFrame.Input;
using HyperFrame.Services;
using UnityEngine;

namespace HyperFrame.App
{
    public struct LevelOutcome
    {
        public bool Won;
        /// <summary>0–3.</summary>
        public int Stars;
        public int Moves;
        public int BoostersUsed;
        /// <summary>Why the level was lost (e.g. "out_of_moves", "board_full").</summary>
        public string Reason;
        /// <summary>Filled by the flow.</summary>
        public float DurationSeconds;
    }

    /// <summary>Everything a gameplay implementation needs, handed over when a level starts.</summary>
    public sealed class GameplayContext
    {
        public int LevelIndex;
        public ILevelData Level;
        /// <summary>Parent for all level objects; destroyed when the level is left.</summary>
        public Transform WorldRoot;
        public Camera Camera;
        public IInputService Input;
        public InputRouter2D Router;
        public IFeedbackService Feedback;
        public IAudioService Audio;
        public TweenEngine Tweens;
        public IGameClock Clock;
        public IGameplayHud Hud;
    }

    /// <summary>What the gameplay can show on the standard HUD.</summary>
    public interface IGameplayHud
    {
        /// <summary>Short status line, e.g. "Moves: 12".</summary>
        void SetStatus(string text);
        RectTransform CoinTarget { get; }
    }

    /// <summary>
    /// One level being played. The game implements this (usually by deriving from <see cref="GameplayBase"/>);
    /// the framework handles everything around it (AR-4: games plug in only the Gameplay state).
    /// </summary>
    public interface IGameplay : IDisposable
    {
        void Begin(GameplayContext context);
        event Action<LevelOutcome> Finished;
        int Moves { get; }
        /// <summary>Ends the level immediately (debug console, tests).</summary>
        void ForceFinish(bool win);
    }

    /// <summary>Convenience base: call <see cref="Finish"/> once when the level is won or lost.</summary>
    public abstract class GameplayBase : IGameplay
    {
        public event Action<LevelOutcome> Finished;
        protected GameplayContext Context { get; private set; }
        public bool IsFinished { get; private set; }
        public int Moves { get; protected set; }

        public void Begin(GameplayContext context)
        {
            Context = context;
            OnBegin();
        }

        protected abstract void OnBegin();

        protected void Finish(LevelOutcome outcome)
        {
            if (IsFinished) return;
            IsFinished = true;
            outcome.Moves = outcome.Moves == 0 ? Moves : outcome.Moves;
            Finished?.Invoke(outcome);
        }

        public virtual void ForceFinish(bool win) =>
            Finish(new LevelOutcome { Won = win, Stars = win ? 3 : 0, Reason = win ? null : "debug" });

        public virtual void Dispose() { }
    }

    /// <summary>
    /// The game's plug-in asset, referenced from <see cref="GameDefinition.gameplay"/>. Subclass it in
    /// Assets/_Game/Scripts/Gameplay and create the asset via the CreateAssetMenu you declare.
    /// </summary>
    public abstract class GameplayModule : ScriptableObject
    {
        /// <summary>Creates the gameplay for one level attempt.</summary>
        public abstract IGameplay CreateGameplay();

        /// <summary>Levels source. Default: the GameDefinition's LevelSet, parsing JSON with <see cref="ParseLevel"/>.</summary>
        public virtual ILevelProvider CreateLevelProvider(GameDefinition definition)
        {
            if (definition.levels == null)
                throw new InvalidOperationException("GameDefinition.levels is empty and the GameplayModule does not override CreateLevelProvider.");
            return new LevelSetProvider(definition.levels, ParseLevel);
        }

        /// <summary>Parses a JSON level TextAsset into the game's level type.</summary>
        public virtual ILevelData ParseLevel(TextAsset json) =>
            throw new NotSupportedException($"{GetType().Name} does not parse JSON levels. Override ParseLevel.");

        /// <summary>Save migrations for this game's data (see ISaveMigration).</summary>
        public virtual IEnumerable<ISaveMigration> GetSaveMigrations() { yield break; }

        /// <summary>Called once at boot after framework services exist. Register game services here.</summary>
        public virtual void OnBoot() { }

        /// <summary>Coins granted for a win. Default: GameDefinition.winRewardCoins.</summary>
        public virtual int CalculateReward(LevelOutcome outcome, GameDefinition definition) => definition.winRewardCoins;

        /// <summary>Difficulty label for analytics level_start.</summary>
        public virtual string DescribeDifficulty(int levelIndex, ILevelData level) => "";
    }
}
