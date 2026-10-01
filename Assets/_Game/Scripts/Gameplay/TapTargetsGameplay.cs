using System.Collections.Generic;
using HyperFrame.App;
using HyperFrame.Audio;
using HyperFrame.Feedback;
using HyperFrame.Input;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Dummy gameplay used to prove the framework end to end (Phase 0 exit criterion):
    /// every tap costs one move; tap all targets within the limit to win.
    /// </summary>
    public sealed class TapTargetsGameplay : GameplayBase
    {
        readonly Color[] _palette;
        readonly List<TapTarget> _targets = new List<TapTarget>();
        TapTargetsLevel _level;
        static Sprite _sprite;

        public TapTargetsGameplay(Color[] palette) => _palette = palette;

        /// <summary>Targets still on the board (bots and tests read this).</summary>
        public IReadOnlyList<TapTarget> Remaining => _targets;
        public int TapLimit => _level?.tapLimit ?? 0;

        protected override void OnBegin()
        {
            _level = (TapTargetsLevel)Context.Level;
            if (_sprite == null) _sprite = PlaceholderVfx.CircleSprite(128);

            for (int i = 0; i < _level.positions.Count; i++)
            {
                var go = new GameObject($"Target_{i + 1}");
                go.transform.SetParent(Context.WorldRoot, false);
                go.transform.localPosition = _level.positions[i];
                go.transform.localScale = Vector3.one * (TapTargetsLevel.TargetRadius * 2f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                sr.color = _palette.Length > 0 ? _palette[i % _palette.Length] : Color.white;
                sr.sortingOrder = 10;
                go.AddComponent<CircleCollider2D>().radius = 0.5f;
                var target = go.AddComponent<TapTarget>();
                target.Hit += OnTargetHit;
                _targets.Add(target);

                // pop-in
                var t = go.transform;
                var scale = t.localScale;
                t.localScale = Vector3.zero;
                Context.Tweens.To(0f, 1f, 0.3f, k => { if (t != null) t.localScale = scale * k; }, HyperFrame.Core.Ease.OutBack, delay: i * 0.05f)
                    .SetTarget(t);
            }

            Context.Input.Gestures.Tapped += OnAnyTap;
            UpdateHud();
        }

        void OnAnyTap(TapGesture tap)
        {
            if (IsFinished) return;
            Moves++;
            UpdateHud();
            // Evaluated after the router delivered the tap to a target (router subscribed first).
            if (_targets.Count == 0) return;
            if (Moves >= _level.tapLimit)
                Finish(new LevelOutcome { Won = false, Reason = "out_of_taps" });
        }

        void OnTargetHit(TapTarget target)
        {
            if (IsFinished || !_targets.Remove(target)) return;
            Context.Audio.PlaySfx(SoundIds.Pop);
            Context.Feedback.Burst(target.transform.position, target.GetComponent<SpriteRenderer>().color, 14);
            Context.Feedback.Haptic(HapticType.Light);
            Object.Destroy(target.gameObject);

            if (_targets.Count == 0)
            {
                int misses = Moves + 1 - _level.targetCount; // this tap's move is counted right after
                int stars = misses <= 0 ? 3 : misses <= 2 ? 2 : 1;
                Finish(new LevelOutcome { Won = true, Stars = stars, Moves = Moves + 1 });
            }
        }

        void UpdateHud() => Context.Hud.SetStatus($"Taps left: {Mathf.Max(0, _level.tapLimit - Moves)}");

        public override void Dispose()
        {
            if (Context?.Input != null) Context.Input.Gestures.Tapped -= OnAnyTap;
            _targets.Clear();
        }
    }

    /// <summary>A tappable target. Shakes when tapped after the level ended.</summary>
    public sealed class TapTarget : MonoBehaviour, ITappable
    {
        public event System.Action<TapTarget> Hit;
        public void OnTap(in PointerContext context) => Hit?.Invoke(this);
    }
}
