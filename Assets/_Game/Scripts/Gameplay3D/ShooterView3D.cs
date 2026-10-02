using HyperFrame.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The 3D shooter: a glossy toy turret in its colour (round body, belly band, barrel, two eyes) on a dark
    /// base, with its ammo on a badge floating above that always faces the camera. Moves between places
    /// (queue → belt → tray) are blended along a hop arc, and it turns smoothly to face where it shoots.
    /// </summary>
    public sealed class ShooterView3D
    {
        const float BarrelRest = 0.3f;

        public readonly Shooter Shooter;
        public readonly Transform Root;
        /// <summary>Last placement key (see PixelLoop3DGameplay.KeyOf); a change starts a blend.</summary>
        public int Key;

        readonly Look3D _look;
        readonly Color _color;
        readonly Transform _visual, _pivot, _barrel, _label, _badge;
        readonly MeshRenderer _body;
        readonly TextMesh _text;
        readonly float _phase;
        Vector3 _from;
        float _blendT = 1f, _blendDuration = 0.2f, _arc;
        float _yaw = 180f, _scale = 1f, _flash;

        public ShooterView3D(Shooter shooter, Transform parent, Color color, Look3D look)
        {
            Shooter = shooter;
            _look = look;
            _color = color;
            _phase = shooter.Id * 1.7f;
            Root = new GameObject($"Shooter_{shooter.Id}").transform;
            Root.SetParent(parent, false);
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(Root, false);

            var dark = new Color(0.16f, 0.14f, 0.26f);
            var baseDisc = look.Renderer("Base", _visual, Mesh3D.Cylinder, look.Toy);
            baseDisc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            baseDisc.transform.localScale = new Vector3(0.6f, 0.1f, 0.6f);
            look.Tint(baseDisc, dark);

            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_visual, false);
            _pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);

            _body = look.Renderer("Body", _pivot, Mesh3D.Sphere, look.Toy);
            _body.transform.localPosition = new Vector3(0f, 0.37f, 0f);
            _body.transform.localScale = new Vector3(0.56f, 0.52f, 0.56f);
            look.Tint(_body, color);

            var band = look.Renderer("Band", _pivot, Mesh3D.Cylinder, look.Toy);
            band.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            band.transform.localScale = new Vector3(0.58f, 0.07f, 0.58f);
            look.Tint(band, PixelLoopArt.Shade(color, 0.62f));

            _barrel = new GameObject("Barrel").transform;
            _barrel.SetParent(_pivot, false);
            _barrel.localPosition = new Vector3(0f, 0.34f, BarrelRest);
            var tube = look.Renderer("Tube", _barrel, Mesh3D.Cylinder, look.Toy);
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tube.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
            look.Tint(tube, PixelLoopArt.Shade(color, 0.5f));
            var tip = look.Renderer("Tip", _barrel, Mesh3D.Cylinder, look.Toy);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            tip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tip.transform.localScale = new Vector3(0.19f, 0.06f, 0.19f);
            look.Tint(tip, Color.Lerp(color, Color.white, 0.55f));

            foreach (float side in new[] { -1f, 1f })
            {
                var eye = look.Renderer("Eye", _pivot, Mesh3D.Sphere, look.Toy, shadows: false);
                eye.transform.localPosition = new Vector3(0.1f * side, 0.47f, 0.19f);
                eye.transform.localScale = Vector3.one * 0.11f;
                look.Tint(eye, Color.white);
                var pupil = look.Renderer("Pupil", _pivot, Mesh3D.Sphere, look.Toy, shadows: false);
                pupil.transform.localPosition = new Vector3(0.1f * side, 0.475f, 0.235f);
                pupil.transform.localScale = Vector3.one * 0.055f;
                look.Tint(pupil, dark);
            }

            _label = new GameObject("Ammo").transform;
            _label.SetParent(Root, false);
            _label.localPosition = new Vector3(0f, 0.92f, 0f);
            var badge = look.Renderer("Badge", _label, Mesh3D.Quad, look.Badge, shadows: false);
            _badge = badge.transform;
            _badge.localScale = Vector3.one * 0.46f;
            look.SetColor(badge, new Color(dark.r, dark.g, dark.b, 0.9f));
            _text = PixelLoopArt.Label("Text", _label, "", 0.3f, Color.white, 0);
            _text.GetComponent<MeshRenderer>().sharedMaterial = look.Text;
            _text.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            SetAmmo(shooter.Ammo, null);
        }

        public void StartBlend(float duration, float arc)
        {
            _from = Root.localPosition;
            _blendT = 0f;
            _blendDuration = Mathf.Max(0.01f, duration);
            _arc = arc;
        }

        public bool Blending => _blendT < 1f;

        /// <param name="yaw">Facing, degrees around Y (0 = +Z, towards the far end of the board).</param>
        public void Animate(Vector3 target, float yaw, float scale, float dt, float time, Quaternion camera)
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
            _yaw = Mathf.LerpAngle(_yaw, yaw, 1f - Mathf.Exp(-dt * 14f));
            _pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            _scale = Mathf.MoveTowards(_scale, scale, dt * 6f);
            Root.localScale = Vector3.one * _scale;

            // Idle life: a gentle breathing bob.
            float bob = Mathf.Sin(time * 3.1f + _phase) * 0.018f;
            _pivot.localPosition = new Vector3(0f, bob, 0f);

            _label.rotation = camera;
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt * 5f);
                _look.Tint(_body, _color, new Color(1f, 1f, 1f, _flash * 0.8f));
            }
        }

        /// <summary>Where bullets leave the barrel, in the parent's space.</summary>
        public Vector3 MuzzleLocal()
        {
            var dir = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            return Root.localPosition + (Vector3.up * 0.34f + dir * 0.5f) * _scale;
        }

        /// <summary>Updates the number; with tweens, the badge pops.</summary>
        public void SetAmmo(int ammo, TweenEngine tweens)
        {
            _text.text = ammo.ToString();
            float k = ammo >= 100 ? 0.72f : 1f;
            _text.transform.localScale = Vector3.one * k;
            if (tweens == null) return;
            var label = _label;
            tweens.KillTarget(label);
            tweens.To(0f, 1f, 0.16f, t => { if (label != null) label.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t * Mathf.PI)); },
                Ease.Linear).SetTarget(label);
        }

        /// <summary>Barrel kicks back, the body flinches and flashes.</summary>
        public void Recoil(TweenEngine tweens)
        {
            var barrel = _barrel;
            var visual = _visual;
            tweens.KillTarget(barrel);
            tweens.To(0f, 1f, 0.14f, t =>
            {
                if (barrel == null) return;
                barrel.localPosition = new Vector3(0f, 0.34f, BarrelRest - 0.12f * (1f - t));
                float s = (1f - t) * 0.1f;
                visual.localScale = new Vector3(1f + s, 1f - s, 1f + s);
            }, Ease.OutQuad).SetTarget(barrel).OnComplete(() => { if (visual != null) visual.localScale = Vector3.one; });
            _flash = Mathf.Max(_flash, 0.45f);
        }

        /// <summary>Squash and stretch (launch).</summary>
        public void Squash(TweenEngine tweens)
        {
            var v = _visual;
            tweens.KillTarget(v, complete: true);
            tweens.To(0f, 1f, 0.34f, t =>
            {
                if (v == null) return;
                float s = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 0.26f;
                v.localScale = new Vector3(1f - s, 1f + s, 1f - s);
            }, Ease.Linear).SetTarget(v).OnComplete(() => { if (v != null) v.localScale = Vector3.one; });
            _flash = 0.6f;
        }

        /// <summary>Side-to-side "no" shake (tap refused, level lost).</summary>
        public void Wobble(TweenEngine tweens)
        {
            var v = _visual;
            tweens.KillTarget(v, complete: true);
            tweens.To(0f, 1f, 0.36f, t =>
            {
                if (v != null) v.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 14f);
            }, Ease.Linear).SetTarget(v).OnComplete(() => { if (v != null) v.localRotation = Quaternion.identity; });
        }

        /// <summary>Puff up white-hot and pop, then destroy.</summary>
        public void Vanish(TweenEngine tweens)
        {
            var root = Root;
            float start = _scale;
            _look.Tint(_body, _color, new Color(1f, 1f, 1f, 0.9f));
            tweens.KillTarget(_visual, complete: true);
            tweens.To(0f, 1f, 0.24f, t =>
            {
                if (root == null) return;
                float s = t < 0.45f ? Mathf.Lerp(start, start * 1.35f, t / 0.45f) : Mathf.Lerp(start * 1.35f, 0f, (t - 0.45f) / 0.55f);
                root.localScale = Vector3.one * s;
            }, Ease.Linear).SetTarget(root).OnComplete(() => { if (root != null) Object.Destroy(root.gameObject); });
        }
    }
}
