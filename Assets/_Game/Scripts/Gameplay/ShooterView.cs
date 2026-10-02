using HyperFrame.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The on-screen shooter: a shaded ball in its colour with a barrel that turns towards the picture and
    /// its ammo on the front. Movement between places (queue → belt → tray) is blended so nothing ever snaps.
    /// </summary>
    public sealed class ShooterView
    {
        const float Size = 0.74f;
        const float BarrelRest = 0.4f;

        public readonly Shooter Shooter;
        public readonly Transform Root;
        /// <summary>Last placement key (see PixelLoopGameplay.KeyOf); a change starts a blend.</summary>
        public int Key;

        readonly Transform _visual, _pivot, _barrel, _label;
        readonly SpriteRenderer _muzzle;
        readonly TextMesh _text, _shadow;
        Vector3 _from;
        float _blendT = 1f, _blendDuration = 0.2f, _arc;
        float _angle = 90f, _scale = 1f;

        public ShooterView(Shooter shooter, Transform parent, Color color)
        {
            Shooter = shooter;
            Root = new GameObject($"Shooter_{shooter.Id}").transform;
            Root.SetParent(parent, false);
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(Root, false);
            _visual.localScale = Vector3.one * Size;

            var shadow = PixelLoopArt.Sprite("Shadow", _visual, PixelLoopArt.Glow, new Color(0f, 0f, 0f, 0.45f), 16, 1.5f);
            shadow.transform.localPosition = new Vector3(0.05f, -0.12f, 0f);
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_visual, false);
            _pivot.localRotation = Quaternion.Euler(0f, 0f, _angle);
            var barrel = PixelLoopArt.SlicedPanel("Barrel", _pivot, new Vector2(0.5f, 0.3f), PixelLoopArt.Shade(color, 0.62f), 17, PixelLoopArt.Dash);
            _barrel = barrel.transform;
            _barrel.localPosition = new Vector3(BarrelRest, 0f, 0f);
            _muzzle = PixelLoopArt.Sprite("Muzzle", _pivot, PixelLoopArt.Glow, new Color(1f, 1f, 1f, 0f), 22, 0.7f);
            _muzzle.transform.localPosition = new Vector3(0.7f, 0f, 0f);

            PixelLoopArt.Sprite("Outline", _visual, PixelLoopArt.Disc, PixelLoopArt.Shade(color, 0.55f), 18, 1.1f);
            PixelLoopArt.Sprite("Body", _visual, PixelLoopArt.Disc, color, 19, 1f);

            bool light = PixelLoopArt.Luma(color) > 0.72f;
            var ink = light ? new Color(0.16f, 0.14f, 0.28f) : Color.white;
            _label = new GameObject("Ammo").transform;
            _label.SetParent(_visual, false);
            _label.localPosition = new Vector3(0f, -0.02f, 0f);
            _shadow = PixelLoopArt.Label("Shadow", _label, "", 0.5f, new Color(0f, 0f, 0f, light ? 0f : 0.35f), 20);
            _shadow.transform.localPosition = new Vector3(0.03f, -0.04f, 0f);
            _text = PixelLoopArt.Label("Text", _label, "", 0.5f, ink, 21);
            SetAmmo(shooter.Ammo, null);
        }

        public void StartBlend(float duration, float arc)
        {
            _from = Root.localPosition;
            _blendT = 0f;
            _blendDuration = Mathf.Max(0.01f, duration);
            _arc = arc;
        }

        public void Animate(Vector3 target, float angle, float scale, float dt)
        {
            if (Root == null) return;
            var pos = target;
            if (_blendT < 1f)
            {
                _blendT = Mathf.Min(1f, _blendT + dt / _blendDuration);
                float e = Easing.Evaluate(Ease.OutCubic, _blendT);
                pos = Vector3.Lerp(_from, target, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * _arc);
            }
            Root.localPosition = pos;
            _angle = Mathf.LerpAngle(_angle, angle, 1f - Mathf.Exp(-dt * 16f));
            _pivot.localRotation = Quaternion.Euler(0f, 0f, _angle);
            _scale = Mathf.MoveTowards(_scale, scale, dt * 6f);
            Root.localScale = Vector3.one * _scale;
        }

        /// <summary>Where bullets leave the barrel, in the parent's space.</summary>
        public Vector3 MuzzleLocal()
        {
            float a = _angle * Mathf.Deg2Rad;
            return Root.localPosition + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (0.62f * Size * _scale);
        }

        /// <summary>Updates the number; with tweens, the label pops.</summary>
        public void SetAmmo(int ammo, TweenEngine tweens)
        {
            string s = ammo.ToString();
            _text.text = s;
            _shadow.text = s;
            float k = s.Length >= 3 ? 0.72f : 1f;
            _label.localScale = Vector3.one * k;
            if (tweens == null) return;
            var label = _label;
            tweens.KillTarget(label);
            tweens.To(0f, 1f, 0.14f, t => { if (label != null) label.localScale = Vector3.one * k * (1f + 0.3f * Mathf.Sin(t * Mathf.PI)); },
                Ease.Linear).SetTarget(label);
        }

        /// <summary>Barrel kicks back and the muzzle flashes.</summary>
        public void Recoil(TweenEngine tweens)
        {
            var barrel = _barrel;
            var muzzle = _muzzle;
            tweens.KillTarget(barrel);
            tweens.To(0f, 1f, 0.12f, t =>
            {
                if (barrel == null) return;
                barrel.localPosition = new Vector3(BarrelRest - 0.14f * (1f - t), 0f, 0f);
                muzzle.color = new Color(1f, 1f, 1f, 0.9f * (1f - t));
                muzzle.transform.localScale = Vector3.one * (0.45f + 0.5f * t);
            }, Ease.OutQuad).SetTarget(barrel);
        }

        /// <summary>Quick squash-and-stretch (launch).</summary>
        public void Squash(TweenEngine tweens)
        {
            var v = _visual;
            tweens.KillTarget(v, complete: true);
            tweens.To(0f, 1f, 0.3f, t =>
            {
                if (v == null) return;
                float s = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 0.22f;
                v.localScale = new Vector3(Size * (1f - s), Size * (1f + s), 1f);
            }, Ease.Linear).SetTarget(v).OnComplete(() => { if (v != null) v.localScale = Vector3.one * Size; });
        }

        /// <summary>Side-to-side "no" shake (tap refused, level lost).</summary>
        public void Wobble(TweenEngine tweens)
        {
            var v = _visual;
            tweens.KillTarget(v, complete: true);
            tweens.To(0f, 1f, 0.32f, t =>
            {
                if (v != null) v.localPosition = new Vector3(Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 0.12f, 0f, 0f);
            }, Ease.Linear).SetTarget(v).OnComplete(() => { if (v != null) v.localPosition = Vector3.zero; });
        }

        /// <summary>Puff up and pop, then destroy.</summary>
        public void Vanish(TweenEngine tweens)
        {
            var root = Root;
            float start = _scale;
            tweens.KillTarget(_visual, complete: true);
            tweens.To(0f, 1f, 0.22f, t =>
            {
                if (root == null) return;
                float s = t < 0.4f ? Mathf.Lerp(start, start * 1.3f, t / 0.4f) : Mathf.Lerp(start * 1.3f, 0f, (t - 0.4f) / 0.6f);
                root.localScale = Vector3.one * s;
            }, Ease.Linear).SetTarget(root).OnComplete(() => { if (root != null) Object.Destroy(root.gameObject); });
        }
    }
}
