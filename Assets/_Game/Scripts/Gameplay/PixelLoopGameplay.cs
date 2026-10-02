using System;
using System.Collections.Generic;
using HyperFrame.App;
using HyperFrame.Core;
using HyperFrame.Feedback;
using HyperFrame.Input;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Pixel Loop: send colour shooters around a conveyor to shoot a pixel-art picture apart.
    /// Rules live in <see cref="BeltSim"/>; this class builds the 2D stage under Context.WorldRoot, feeds
    /// taps to the sim and turns its events into animation, particles, sound and haptics.
    /// </summary>
    public sealed class PixelLoopGameplay : GameplayBase
    {
        /// <summary>Debug console toggles (Game/…). The bot plays the best available shooter.</summary>
        public static bool AutoPlay;
        public static float SpeedBoost = 1f;

        // Stage layout, in stage units (the stage is scaled down on narrow screens).
        const float StageY = 1.2f;
        const float StageWidth = 7.1f;
        const float TrackWidth = 0.78f;
        const float ShooterSize = 0.74f;
        const float ColumnGap = 1.12f;
        const float RowGap = 0.92f;
        const int VisibleRows = 4;
        const float SlotGap = 0.98f;
        const float BulletSpeed = 22f;

        readonly PixelLoopStyle _style;
        readonly Dictionary<Shooter, ShooterView> _views = new Dictionary<Shooter, ShooterView>();
        readonly List<Bullet> _bulletPool = new List<Bullet>();
        readonly List<Bokeh> _bokeh = new List<Bokeh>();

        PixelLevel _level;
        BeltSim _sim;
        BeltGeometry _geo;
        Color[] _palette;
        Transform _stage, _board, _trayRoot;
        SpriteRenderer[] _pixels;
        Vector3 _pixelScale;
        SpriteRenderer[] _treads;
        float _treadGap, _treadOffset;
        SpriteRenderer[] _slots, _lights;
        SpriteRenderer _progressFill;
        TextMesh _progressLabel;
        float _progressWidth, _shownProgress;
        float _trayY, _queueTopY;
        float _time, _lastHitTime = -1f, _autoTimer;
        int _combo;
        bool _ending;

        public PixelLoopGameplay(PixelLoopStyle style) => _style = style ?? new PixelLoopStyle();

        public BeltSim Sim => _sim;
        public PixelLevel Level => _level;

        // ── Setup ───────────────────────────────────────────────────────────────────────

        protected override void OnBegin()
        {
            _level = (PixelLevel)Context.Level;
            _sim = new BeltSim(_level);
            _geo = _sim.Geometry;
            _palette = new Color[_level.colors.Length];
            for (int i = 0; i < _palette.Length; i++) _palette[i] = PixelLoopArt.Hex(_level.colors[i], Color.magenta);

            BuildBackground();
            _stage = new GameObject("Stage").transform;
            _stage.SetParent(Context.WorldRoot, false);
            _stage.localPosition = new Vector3(0f, StageY, 0f);
            var cam = Context.Camera != null ? Context.Camera : Camera.main;
            float viewWidth = cam != null ? cam.orthographicSize * cam.aspect * 2f : StageWidth;
            _stage.localScale = Vector3.one * Mathf.Min(1f, viewWidth / StageWidth);

            BuildBelt();
            BuildBoard();
            BuildTray();
            BuildQueue();
            BuildProgress();

            _sim.Launched += OnLaunched;
            _sim.Fired += OnFired;
            _sim.Spent += OnSpent;
            _sim.Docked += OnDocked;
            _sim.Denied += OnDenied;
            _sim.Won += OnWon;
            _sim.Lost += OnLost;

            var driver = Context.WorldRoot.gameObject.AddComponent<PixelLoopDriver>();
            driver.Init(Context.Clock, Tick);
            Context.Hud.SetStatus(_level.name);
        }

        void BuildBackground()
        {
            var cam = Context.Camera != null ? Context.Camera : Camera.main;
            float h = cam != null ? cam.orthographicSize * 2f + 1f : 17f;
            float w = cam != null ? h * cam.aspect + 1f : 10f;
            var root = new GameObject("Background").transform;
            root.SetParent(Context.WorldRoot, false);
            var baseSr = PixelLoopArt.Sprite("Base", root, PixelLoopArt.Solid, _style.backgroundBottom, -100);
            baseSr.transform.localScale = new Vector3(w, h, 1f);
            var grad = PixelLoopArt.Sprite("Gradient", root, PixelLoopArt.Gradient, _style.backgroundTop, -99);
            grad.transform.localScale = new Vector3(w / (4f / 128f), h, 1f);

            var rng = new System.Random(Context.LevelIndex * 31 + 7);
            for (int i = 0; i < 9; i++)
            {
                float size = 1.5f + (float)rng.NextDouble() * 3f;
                var sr = PixelLoopArt.Sprite("Bokeh", root, PixelLoopArt.Glow, _style.bokeh, -90, size);
                var b = new Bokeh
                {
                    Renderer = sr,
                    Position = new Vector2(((float)rng.NextDouble() - 0.5f) * w, ((float)rng.NextDouble() - 0.5f) * h),
                    Speed = 0.15f + (float)rng.NextDouble() * 0.25f,
                    Phase = (float)rng.NextDouble() * 6.28f,
                    Height = h,
                };
                sr.transform.localPosition = b.Position;
                _bokeh.Add(b);
            }
        }

        void BuildBelt()
        {
            float w = _geo.Right - _geo.Left, h = _geo.Top - _geo.Bottom;
            var shadow = PixelLoopArt.SlicedPanel("Shadow", _stage, new Vector2(w + TrackWidth + 0.2f, h + TrackWidth + 0.2f), _style.boardShadow, 0);
            shadow.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            PixelLoopArt.SlicedPanel("Track", _stage, new Vector2(w + TrackWidth, h + TrackWidth), _style.track, 1);
            PixelLoopArt.SlicedPanel("TrackInner", _stage, new Vector2(w - TrackWidth + 0.12f, h - TrackWidth + 0.12f),
                PixelLoopArt.Shade(_style.track, 0.7f), 2);

            int count = Mathf.Max(8, Mathf.FloorToInt(_geo.Length / 0.34f));
            _treadGap = _geo.Length / count;
            _treads = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var sr = PixelLoopArt.SlicedPanel("Tread", _stage, new Vector2(TrackWidth * 0.62f, 0.09f), _style.tread, 3, PixelLoopArt.Dash);
                _treads[i] = sr;
            }
            PlaceTreads();

            // Belt capacity lights next to the entry (bottom-left).
            _lights = new SpriteRenderer[_level.beltCapacity];
            for (int i = 0; i < _lights.Length; i++)
            {
                var sr = PixelLoopArt.Sprite("CapacityLight", _stage, PixelLoopArt.Disc, _style.tread, 4, 0.16f);
                sr.transform.localPosition = new Vector3(_geo.Left - TrackWidth / 2f + 0.15f + i * 0.22f, _geo.Bottom - TrackWidth / 2f - 0.2f, 0f);
                _lights[i] = sr;
            }
        }

        void BuildBoard()
        {
            float w = _geo.Right - _geo.Left - TrackWidth, h = _geo.Top - _geo.Bottom - TrackWidth;
            PixelLoopArt.SlicedPanel("Board", _stage, new Vector2(w, h), _style.board, 3);
            _board = new GameObject("Pixels").transform;
            _board.SetParent(_stage, false);

            _pixels = new SpriteRenderer[_level.Width * _level.Height];
            _pixelScale = Vector3.one * (_geo.Cell * 0.94f);
            var center = new Vector2(_level.Width / 2f, _level.Height / 2f);
            float maxDist = center.magnitude;
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                int color = _level.Cell(x, y);
                if (color < 0) continue;
                var sr = PixelLoopArt.Sprite($"Px_{x}_{y}", _board, PixelLoopArt.Tile, _palette[color], 5);
                _geo.CellCenter(x, y, out float cx, out float cy);
                var t = sr.transform;
                t.localPosition = new Vector3(cx, cy, 0f);
                t.localScale = Vector3.zero;
                _pixels[x + y * _level.Width] = sr;
                // Ripple in from the centre.
                float delay = 0.05f + Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist * 0.45f;
                var target = _pixelScale;
                Context.Tweens.To(0f, 1f, 0.35f, k => { if (t != null) t.localScale = target * k; }, Ease.OutBack, delay: delay).SetTarget(t);
            }
        }

        void BuildTray()
        {
            _trayY = _geo.Bottom - TrackWidth / 2f - 0.95f;
            _trayRoot = new GameObject("Tray").transform;
            _trayRoot.SetParent(_stage, false);
            _trayRoot.localPosition = new Vector3(0f, _trayY, 0f);
            _slots = new SpriteRenderer[_level.traySlots];
            for (int i = 0; i < _slots.Length; i++)
            {
                var sr = PixelLoopArt.SlicedPanel("Slot", _trayRoot, new Vector2(0.86f, 0.86f), _style.slot, 4);
                sr.transform.localPosition = new Vector3(SlotX(i), 0f, 0f);
                var zone = sr.gameObject.AddComponent<BoxCollider2D>();
                zone.size = new Vector2(0.95f, 0.95f);
                int slot = i;
                sr.gameObject.AddComponent<PixelLoopTapZone>().Tapped = () => OnTrayTapped(slot);
                _slots[i] = sr;
            }
        }

        void BuildQueue()
        {
            _queueTopY = _trayY - 1.12f;
            int columns = _sim.Columns.Count;
            for (int c = 0; c < columns; c++)
            {
                var zone = new GameObject($"Column_{c}");
                zone.transform.SetParent(_stage, false);
                zone.transform.localPosition = new Vector3(ColumnX(c), _queueTopY - RowGap * (VisibleRows - 1) / 2f, 0f);
                zone.AddComponent<BoxCollider2D>().size = new Vector2(ColumnGap * 0.96f, RowGap * VisibleRows);
                int column = c;
                zone.AddComponent<PixelLoopTapZone>().Tapped = () => OnColumnTapped(column);

                for (int r = 0; r < _sim.Columns[c].Count; r++)
                {
                    var shooter = _sim.Columns[c][r];
                    var view = new ShooterView(shooter, _stage, _palette[shooter.Color]);
                    var p = QueuePosition(c, r);
                    view.Root.localPosition = p + new Vector3(0f, -3f, 0f);
                    view.StartBlend(0.45f + r * 0.06f + c * 0.04f, 0f);
                    view.Key = KeyOf(shooter);
                    _views[shooter] = view;
                }
            }
        }

        void BuildProgress()
        {
            float y = _geo.Top + TrackWidth / 2f + 0.42f;
            _progressWidth = (_geo.Right - _geo.Left) * 0.62f;
            var bg = PixelLoopArt.SlicedPanel("ProgressBg", _stage, new Vector2(_progressWidth, 0.2f), new Color(1f, 1f, 1f, 0.12f), 4, PixelLoopArt.Dash);
            bg.transform.localPosition = new Vector3(-0.35f, y, 0f);
            _progressFill = PixelLoopArt.SlicedPanel("ProgressFill", _stage, new Vector2(0.2f, 0.2f), _style.progress, 5, PixelLoopArt.Dash);
            _progressFill.transform.localPosition = new Vector3(-0.35f - _progressWidth / 2f + 0.1f, y, 0f);
            _progressLabel = PixelLoopArt.Label("ProgressLabel", _stage, "0%", 0.34f, Color.white, 5);
            _progressLabel.transform.localPosition = new Vector3(-0.35f + _progressWidth / 2f + 0.55f, y, 0f);
        }

        float SlotX(int i) => (i - (_level.traySlots - 1) / 2f) * SlotGap;
        float ColumnX(int c) => (c - (_sim.Columns.Count - 1) / 2f) * ColumnGap;

        Vector3 QueuePosition(int column, int row) =>
            new Vector3(ColumnX(column), _queueTopY - Mathf.Min(row, VisibleRows - 1) * RowGap, 0f);

        // ── Input ───────────────────────────────────────────────────────────────────────

        void OnColumnTapped(int column)
        {
            if (IsFinished || _ending || !Context.Input.IsGameplayInputEnabled) return;
            _sim.TryLaunchColumn(column);
        }

        void OnTrayTapped(int slot)
        {
            if (IsFinished || _ending || !Context.Input.IsGameplayInputEnabled) return;
            var s = _sim.Tray[slot];
            if (s != null) _sim.TryLaunch(s);
        }

        /// <summary>World point a player would tap to send this shooter (tests, bots).</summary>
        public Vector3 TapPointFor(Shooter s)
        {
            if (s.State == ShooterState.Tray) return _stage.TransformPoint(new Vector3(SlotX(s.Slot), _trayY, 0f));
            return _stage.TransformPoint(QueuePosition(s.Column, 0));
        }

        /// <summary>Next shooter of the planned order still waiting to be sent (queue or tray).</summary>
        public Shooter NextPlanned()
        {
            Shooter best = null;
            foreach (var s in _sim.Shooters)
                if ((s.State == ShooterState.Queued || s.State == ShooterState.Tray) && (best == null || s.Id < best.Id))
                    best = s;
            return best;
        }

        // ── Frame ───────────────────────────────────────────────────────────────────────

        void Tick(float dt)
        {
            _time += dt;
            if (!IsFinished && !_ending)
            {
                _sim.Step(dt * SpeedBoost);
                if (AutoPlay) RunAutoPlay(dt);
            }

            _treadOffset = (_treadOffset + _sim.Speed * SpeedBoost * dt) % _treadGap;
            PlaceTreads();
            foreach (var view in _views.Values) UpdateView(view, dt);
            UpdateTrayAndLights();
            UpdateProgress(dt);
            foreach (var b in _bokeh) b.Update(dt, _time);
        }

        void PlaceTreads()
        {
            for (int i = 0; i < _treads.Length; i++)
            {
                var p = _geo.At(i * _treadGap + _treadOffset);
                var t = _treads[i].transform;
                t.localPosition = new Vector3(p.X, p.Y, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.NY, p.NX) * Mathf.Rad2Deg);
            }
        }

        void UpdateTrayAndLights()
        {
            int used = _sim.TrayUsed;
            bool danger = !_sim.IsWon && used >= _level.traySlots - 1;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 9f);
            for (int i = 0; i < _slots.Length; i++)
            {
                var c = _style.slot;
                if (_sim.IsLost) c = Color.Lerp(_style.slot, _style.warning, 0.75f);
                else if (danger) c = Color.Lerp(_style.slot, new Color(_style.warning.r, _style.warning.g, _style.warning.b, 0.5f), pulse);
                _slots[i].color = c;
            }
            int load = _sim.BeltLoad;
            for (int i = 0; i < _lights.Length; i++)
                _lights[i].color = i < load ? (load >= _lights.Length ? _style.warning : _style.progress) : _style.tread;
        }

        void UpdateProgress(float dt)
        {
            _shownProgress = Mathf.MoveTowards(_shownProgress, _sim.Cleared, dt * 1.5f);
            float w = Mathf.Max(0.2f, _progressWidth * _shownProgress);
            _progressFill.size = new Vector2(w, 0.2f);
            var p = _progressFill.transform.localPosition;
            p.x = -0.35f - _progressWidth / 2f + w / 2f;
            _progressFill.transform.localPosition = p;
            _progressLabel.text = $"{Mathf.RoundToInt(_shownProgress * 100f)}%";
        }

        // ── Shooter views ───────────────────────────────────────────────────────────────

        int KeyOf(Shooter s)
        {
            switch (s.State)
            {
                case ShooterState.Queued: return 1000 + _sim.Columns[s.Column].IndexOf(s);
                case ShooterState.Tray: return 2000 + s.Slot;
                case ShooterState.Waiting: return 3000;
                case ShooterState.OnBelt: return 4000;
                default: return 5000;
            }
        }

        Vector3 TargetOf(Shooter s, out float angle, out float scale)
        {
            angle = 90f;
            scale = 1f;
            switch (s.State)
            {
                case ShooterState.Queued:
                {
                    int row = _sim.Columns[s.Column].IndexOf(s);
                    if (row >= VisibleRows) scale = 0f;
                    return QueuePosition(s.Column, row);
                }
                case ShooterState.Tray:
                    return new Vector3(SlotX(s.Slot), _trayY, 0f);
                case ShooterState.Waiting:
                {
                    var p = _geo.At(0f);
                    return new Vector3(p.X, p.Y, 0f);
                }
                default:
                {
                    var p = _geo.At(Mathf.Min(s.Distance, _geo.Length - 0.001f));
                    angle = Mathf.Atan2(p.NY, p.NX) * Mathf.Rad2Deg;
                    return new Vector3(p.X, p.Y, 0f);
                }
            }
        }

        void UpdateView(ShooterView view, float dt)
        {
            var s = view.Shooter;
            int key = KeyOf(s);
            if (key != view.Key)
            {
                bool launch = view.Key < 3000 && key >= 3000;
                bool dock = key >= 2000 && key < 3000;
                view.StartBlend(launch ? BeltSim.HopSeconds : dock ? 0.4f : 0.15f, launch ? 0.9f : dock ? 0.6f : 0f);
                view.Key = key;
            }
            var target = TargetOf(s, out float angle, out float scale);
            view.Animate(target, angle, scale, dt);
        }

        // ── Sim events → feel ───────────────────────────────────────────────────────────

        void OnLaunched(Shooter s, bool fromTray)
        {
            Moves = _sim.Launches;
            Context.Audio.PlaySfx(PixelLoopSounds.Launch);
            Context.Feedback.Haptic(HapticType.Selection);
            if (_views.TryGetValue(s, out var view)) view.Squash(Context.Tweens);
        }

        void OnFired(Shooter s, int x, int y, int color)
        {
            if (!_views.TryGetValue(s, out var view)) return;
            view.SetAmmo(s.Ammo, Context.Tweens);
            view.Recoil(Context.Tweens);
            Context.Audio.PlaySfx(PixelLoopSounds.Shot, 0.7f);

            var from = view.MuzzleLocal();
            _geo.CellCenter(x, y, out float cx, out float cy);
            var to = new Vector3(cx, cy, 0f);
            var bullet = RentBullet();
            bullet.Launch(from, to, _palette[color], _stage.lossyScale.x);
            float duration = Mathf.Max(0.05f, Vector3.Distance(from, to) / BulletSpeed);
            int px = x, py = y;
            var bt = bullet.Root;
            Context.Tweens.To(0f, 1f, duration, k => { if (bt != null) bullet.Move(k); }, Ease.InQuad).SetTarget(bt)
                .OnComplete(() =>
                {
                    if (bt == null) return;
                    bullet.Hide();
                    PopPixel(px, py, color);
                });
        }

        void PopPixel(int x, int y, int color)
        {
            var sr = _pixels[x + y * _level.Width];
            if (sr == null) return;
            var t = sr.transform;
            var c = _palette[color];
            var baseScale = _pixelScale;
            Context.Tweens.KillTarget(t);
            Context.Tweens.To(0f, 1f, 0.2f, k =>
            {
                if (t == null) return;
                if (k < 0.3f)
                {
                    float a = k / 0.3f;
                    t.localScale = baseScale * (1f + 0.4f * a);
                    sr.color = Color.Lerp(c, Color.white, a);
                }
                else
                {
                    float a = (k - 0.3f) / 0.7f;
                    t.localScale = baseScale * (1.4f * (1f - a * a));
                    sr.color = Color.Lerp(Color.white, c, a);
                }
            }, Ease.Linear).SetTarget(t).OnComplete(() => { if (t != null) t.gameObject.SetActive(false); });

            Context.Feedback.Burst(t.position, c, 7);
            Context.Feedback.Shake(_board, 0.03f, 0.08f);

            _combo = _time - _lastHitTime < 0.3f ? _combo + 1 : 0;
            _lastHitTime = _time;
            Context.Audio.PlaySfx(PixelLoopSounds.Hit(_combo / 3));
        }

        void OnSpent(Shooter s)
        {
            if (!_views.TryGetValue(s, out var view)) return;
            _views.Remove(s);
            var pos = view.Root.position;
            var color = _palette[s.Color];
            Context.Audio.PlaySfx(PixelLoopSounds.Spent);
            Context.Feedback.Haptic(HapticType.Light);
            Context.Tweens.Delay(0.12f, () =>
            {
                Context.Feedback.Burst(pos, color, 16);
                Context.Feedback.Burst(pos, Color.white, 6);
            });
            Shockwave(view.Root.localPosition, color);
            view.Vanish(Context.Tweens);
        }

        void OnDocked(Shooter s)
        {
            Context.Audio.PlaySfx(PixelLoopSounds.Dock);
            Context.Feedback.Haptic(HapticType.Medium);
            if (_sim.TrayUsed >= _level.traySlots - 1)
            {
                Context.Audio.PlaySfx(PixelLoopSounds.Warn);
                Context.Feedback.Shake(_trayRoot, 0.08f, 0.25f);
            }
        }

        void OnDenied(Shooter s)
        {
            if (_sim.IsOver) return;
            Context.Audio.PlaySfx(PixelLoopSounds.Deny);
            Context.Feedback.Haptic(HapticType.Light);
            if (_views.TryGetValue(s, out var view)) view.Wobble(Context.Tweens);
            if (_sim.BeltFull)
                foreach (var l in _lights) Context.Feedback.Punch(l.transform, 0.5f, 0.2f);
        }

        void OnWon()
        {
            _ending = true;
            Context.Feedback.SlowMo(0.35f, 0.5f);
            Context.Tweens.Delay(0.45f, Reveal);
        }

        /// <summary>The finished picture pops back in with a white shine sweeping across it.</summary>
        void Reveal()
        {
            Context.Audio.PlaySfx(PixelLoopSounds.Reveal);
            Context.Feedback.Haptic(HapticType.Success);
            float span = _level.Width + _level.Height;
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                var sr = _pixels[x + y * _level.Width];
                if (sr == null) continue;
                var t = sr.transform;
                var c = _palette[_level.Cell(x, y)];
                var baseScale = _pixelScale;
                float delay = (x + (_level.Height - 1 - y)) / span * 0.6f;
                t.gameObject.SetActive(true);
                t.localScale = Vector3.zero;
                sr.color = Color.white;
                Context.Tweens.KillTarget(t);
                Context.Tweens.To(0f, 1f, 0.45f, k =>
                {
                    if (t == null) return;
                    t.localScale = baseScale * Easing.Evaluate(Ease.OutBack, Mathf.Min(1f, k * 1.6f));
                    sr.color = Color.Lerp(Color.white, c, Mathf.Clamp01((k - 0.25f) / 0.75f));
                }, Ease.Linear, delay: delay).SetTarget(t);
            }
            foreach (var view in _views.Values) view.Vanish(Context.Tweens);
            _views.Clear();
            Context.Feedback.Punch(_stage, 0.04f, 0.4f);
            Context.Tweens.Delay(1.25f, () =>
                Finish(new LevelOutcome { Won = true, Stars = _sim.Stars, Moves = _sim.Launches }));
        }

        void OnLost(string reason)
        {
            _ending = true;
            Context.Audio.PlaySfx(PixelLoopSounds.Warn);
            Context.Feedback.Haptic(HapticType.Heavy);
            Context.Feedback.CameraShake(0.25f, 0.4f);
            Context.Feedback.Shake(_trayRoot, 0.15f, 0.45f);
            foreach (var view in _views.Values)
                if (view.Shooter.State == ShooterState.OnBelt) view.Wobble(Context.Tweens);
            Context.Tweens.Delay(1.0f, () => Finish(new LevelOutcome { Won = false, Reason = reason, Moves = _sim.Launches }));
        }

        void Shockwave(Vector3 localPos, Color color)
        {
            var sr = PixelLoopArt.Sprite("Shockwave", _stage, PixelLoopArt.Ring, color, 23, 0.3f);
            var t = sr.transform;
            t.localPosition = localPos;
            Context.Tweens.To(0f, 1f, 0.35f, k =>
            {
                if (t == null) return;
                t.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.6f, k);
                sr.color = new Color(color.r, color.g, color.b, 1f - k);
            }, Ease.OutCubic).SetTarget(t).OnComplete(() => { if (t != null) UnityEngine.Object.Destroy(t.gameObject); });
        }

        Bullet RentBullet()
        {
            foreach (var b in _bulletPool)
                if (!b.InUse) return b;
            var bullet = new Bullet(_stage);
            _bulletPool.Add(bullet);
            return bullet;
        }

        // ── Auto play (debug console) ───────────────────────────────────────────────────

        void RunAutoPlay(float dt)
        {
            _autoTimer -= dt;
            if (_autoTimer > 0f || _sim.BeltFull) return;
            _autoTimer = 0.35f;
            var cells = _sim.SnapshotCells();
            Shooter best = null;
            int bestHits = 0;
            void Consider(Shooter s)
            {
                if (s == null || !_sim.CanLaunch(s)) return;
                foreach (var b in _sim.Belt) if (b.Color == s.Color) return; // let the one on the belt work first
                int hits = BeltSim.LoopHits(cells, _geo, s.Color, s.Ammo, apply: false);
                if (hits > bestHits) { best = s; bestHits = hits; }
            }
            foreach (var s in _sim.Tray) Consider(s);
            for (int c = 0; c < _sim.Columns.Count; c++) Consider(_sim.Front(c));
            if (best != null) _sim.TryLaunch(best);
        }

        public override void Dispose()
        {
            if (_sim == null) return;
            _sim.Launched -= OnLaunched;
            _sim.Fired -= OnFired;
            _sim.Spent -= OnSpent;
            _sim.Docked -= OnDocked;
            _sim.Denied -= OnDenied;
            _sim.Won -= OnWon;
            _sim.Lost -= OnLost;
            _views.Clear();
        }

        // ── Small view helpers ──────────────────────────────────────────────────────────

        sealed class Bokeh
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public float Speed, Phase, Height;

            public void Update(float dt, float time)
            {
                if (Renderer == null) return;
                Position.y += Speed * dt;
                if (Position.y > Height / 2f + 2f) Position.y -= Height + 4f;
                Renderer.transform.localPosition = new Vector3(Position.x + Mathf.Sin(time * 0.4f + Phase) * 0.4f, Position.y, 0f);
            }
        }

        sealed class Bullet
        {
            public readonly Transform Root;
            readonly SpriteRenderer _core, _glow;
            readonly TrailRenderer _trail;
            Vector3 _from, _to;
            public bool InUse { get; private set; }

            public Bullet(Transform parent)
            {
                Root = new GameObject("Bullet").transform;
                Root.SetParent(parent, false);
                _glow = PixelLoopArt.Sprite("Glow", Root, PixelLoopArt.Glow, Color.white, 25, 0.55f);
                _core = PixelLoopArt.Sprite("Core", Root, PixelLoopArt.Disc, Color.white, 26, 0.17f);
                _trail = Root.gameObject.AddComponent<TrailRenderer>();
                _trail.sharedMaterial = PixelLoopArt.TrailMaterial;
                _trail.time = 0.08f;
                _trail.minVertexDistance = 0.02f;
                _trail.sortingOrder = 24;
                _trail.numCapVertices = 2;
                Root.gameObject.SetActive(false);
            }

            public void Launch(Vector3 from, Vector3 to, Color color, float worldScale)
            {
                InUse = true;
                _from = from;
                _to = to;
                Root.localPosition = from;
                _core.color = Color.Lerp(color, Color.white, 0.55f);
                _glow.color = new Color(color.r, color.g, color.b, 0.75f);
                _trail.startWidth = 0.14f * worldScale;
                _trail.endWidth = 0f;
                _trail.startColor = color;
                _trail.endColor = new Color(color.r, color.g, color.b, 0f);
                Root.gameObject.SetActive(true);
                _trail.Clear();
            }

            public void Move(float k) => Root.localPosition = Vector3.LerpUnclamped(_from, _to, k);

            public void Hide()
            {
                InUse = false;
                Root.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Drives the gameplay every frame with pause- and slow-mo-aware game time (IGameClock).</summary>
    public sealed class PixelLoopDriver : MonoBehaviour
    {
        IGameClock _clock;
        Action<float> _tick;
        float _last = -1f;

        public void Init(IGameClock clock, Action<float> tick)
        {
            _clock = clock;
            _tick = tick;
        }

        void Update()
        {
            if (_clock == null || _tick == null) return;
            float now = _clock.GameTime;
            if (_last < 0f) _last = now;
            float dt = Mathf.Min(now - _last, 0.1f);
            _last = now;
            _tick(dt);
        }
    }

    /// <summary>A tappable area (queue column, tray slot).</summary>
    public sealed class PixelLoopTapZone : MonoBehaviour, ITappable
    {
        public Action Tapped;
        public void OnTap(in PointerContext context) => Tapped?.Invoke();
    }
}
