using System;
using HyperFrame.Core;
using HyperFrame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HyperFrame.Feedback
{
    /// <summary>Game-feel presets callable in one line (FB-01).</summary>
    public interface IFeedbackService
    {
        void Shake(Transform target, float strength = 0.15f, float duration = 0.3f);
        void CameraShake(float strength = 0.2f, float duration = 0.3f);
        void SquashStretch(Transform target, float amount = 0.25f, float duration = 0.3f);
        void Punch(Transform target, float amount = 0.2f, float duration = 0.25f);
        /// <summary>Particle burst at a world position (pooled placeholder VFX).</summary>
        void Burst(Vector3 worldPosition, Color color, int count = 16);
        /// <summary>Spawns a VFX prefab from the pool; it returns itself when finished.</summary>
        void Spawn(GameObject vfxPrefab, Vector3 worldPosition);
        void Confetti();
        /// <summary>Coins fly from a screen position to a UI target (e.g. the wallet counter).</summary>
        void CoinFly(Vector2 fromScreen, RectTransform target, int coins = 8, Action onEachArrive = null, Action onDone = null);
        void SlowMo(float timeScale = 0.3f, float durationSeconds = 0.5f);
        void Haptic(HapticType type);
    }

    public sealed class FeedbackService : IFeedbackService
    {
        readonly TweenEngine _tweens;
        readonly GameClock _clock;
        readonly IPoolService _pools;
        readonly IUIService _ui;
        readonly IHapticsService _haptics;
        readonly Func<Camera> _camera;
        GameObject _burstPrefab, _confettiPrefab;
        Sprite _coinSprite;
        TweenHandle _slowMo;

        public FeedbackService(TweenEngine tweens, GameClock clock, IPoolService pools, IUIService ui,
            IHapticsService haptics, Func<Camera> camera = null)
        {
            _tweens = tweens;
            _clock = clock;
            _pools = pools;
            _ui = ui;
            _haptics = haptics;
            _camera = camera ?? (() => Camera.main);
        }

        public void Shake(Transform target, float strength = 0.15f, float duration = 0.3f)
        {
            if (target == null) return;
            _tweens.KillTarget(target, complete: true);
            var origin = target.localPosition;
            var rng = new System.Random(target.GetEntityId().GetHashCode());
            _tweens.To(1f, 0f, duration, k =>
            {
                if (target == null) return;
                var offset = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, 0f) * (strength * k);
                target.localPosition = origin + offset;
            }, Ease.Linear).SetTarget(target).OnComplete(() => { if (target != null) target.localPosition = origin; });
        }

        public void CameraShake(float strength = 0.2f, float duration = 0.3f)
        {
            var cam = _camera();
            if (cam != null) Shake(cam.transform, strength, duration);
        }

        public void SquashStretch(Transform target, float amount = 0.25f, float duration = 0.3f)
        {
            if (target == null) return;
            _tweens.KillTarget(target, complete: true);
            var baseScale = target.localScale;
            _tweens.To(0f, 1f, duration, t =>
            {
                if (target == null) return;
                // squash then overshoot back: sin envelope with decay
                float s = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * amount;
                target.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z);
            }, Ease.Linear).SetTarget(target).OnComplete(() => { if (target != null) target.localScale = baseScale; });
        }

        public void Punch(Transform target, float amount = 0.2f, float duration = 0.25f)
        {
            if (target == null) return;
            _tweens.KillTarget(target, complete: true);
            var baseScale = target.localScale;
            _tweens.To(0f, 1f, duration, t =>
            {
                if (target != null) target.localScale = baseScale * (1f + Mathf.Sin(t * Mathf.PI) * amount);
            }, Ease.OutQuad).SetTarget(target).OnComplete(() => { if (target != null) target.localScale = baseScale; });
        }

        public void Burst(Vector3 worldPosition, Color color, int count = 16)
        {
            if (_burstPrefab == null) _burstPrefab = PlaceholderVfx.CreateBurstTemplate();
            var go = _pools.Spawn(_burstPrefab, worldPosition, Quaternion.identity);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = color;
            ps.Emit(count);
        }

        public void Spawn(GameObject vfxPrefab, Vector3 worldPosition)
        {
            if (vfxPrefab == null) return;
            var go = _pools.Spawn(vfxPrefab, worldPosition, Quaternion.identity);
            if (go.GetComponent<ParticleSystem>() != null && go.GetComponent<PooledParticles>() == null)
                go.AddComponent<PooledParticles>();
        }

        public void Confetti()
        {
            if (_confettiPrefab == null) _confettiPrefab = PlaceholderVfx.CreateConfettiTemplate();
            var cam = _camera();
            var top = cam != null ? cam.ViewportToWorldPoint(new Vector3(0.5f, 1.05f, -cam.transform.position.z)) : Vector3.up * 6f;
            top.z = 0f;
            _pools.Spawn(_confettiPrefab, top, Quaternion.identity);
        }

        public void CoinFly(Vector2 fromScreen, RectTransform target, int coins = 8, Action onEachArrive = null, Action onDone = null)
        {
            var layer = _ui.Root.OverlayLayer;
            Vector2 start = _ui.Root.ScreenToOverlay(fromScreen);
            Vector2 end = target != null
                ? _ui.Root.ScreenToOverlay(RectTransformUtility.WorldToScreenPoint(null, target.position))
                : start + new Vector2(0f, 600f);
            if (_coinSprite == null) _coinSprite = PlaceholderVfx.CircleSprite(64);
            int arrived = 0;
            coins = Mathf.Max(1, coins);

            for (int i = 0; i < coins; i++)
            {
                var rt = UIBuilder.CreateRect("Coin", layer);
                rt.sizeDelta = new Vector2(72f, 72f);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = _coinSprite;
                img.color = _ui.Theme.accent;
                img.raycastTarget = false;
                var scatter = start + UnityEngine.Random.insideUnitCircle * 120f;
                var control = Vector2.Lerp(scatter, end, 0.5f) + new Vector2(UnityEngine.Random.Range(-250f, 250f), 200f);
                rt.anchoredPosition = start;

                _tweens.To(0f, 1f, 0.6f + i * 0.05f, t =>
                {
                    if (rt == null) return;
                    // quadratic bezier: start→scatter quickly, then arc to target
                    var a = Vector2.Lerp(scatter, control, t);
                    var b = Vector2.Lerp(control, end, t);
                    rt.anchoredPosition = t < 0.15f ? Vector2.Lerp(start, scatter, t / 0.15f) : Vector2.Lerp(a, b, t);
                }, Ease.InOutQuad, TimeMode.Unscaled, delay: i * 0.03f).OnComplete(() =>
                {
                    if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
                    onEachArrive?.Invoke();
                    if (++arrived == coins)
                    {
                        if (target != null) Punch(target, 0.15f, 0.2f);
                        onDone?.Invoke();
                    }
                });
            }
        }

        public void SlowMo(float timeScale = 0.3f, float durationSeconds = 0.5f)
        {
            _slowMo.Kill();
            _clock.TimeScale = timeScale;
            _slowMo = _tweens.Delay(durationSeconds, () => _clock.TimeScale = 1f, TimeMode.Unscaled);
        }

        public void Haptic(HapticType type) => _haptics?.Play(type);
    }
}
