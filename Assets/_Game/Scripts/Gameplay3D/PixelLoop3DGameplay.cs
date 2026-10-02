using System.Collections.Generic;
using HyperFrame.App;
using HyperFrame.Core;
using HyperFrame.Feedback;
using HyperFrame.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Pixel Loop in 3D. Same rules (<see cref="BeltSim"/>) as the 2D version; this class builds a lit 3D
    /// stage under Context.WorldRoot (voxel picture on a board, conveyor rail with moving treads, tray, queue
    /// lanes, toy shooters), drives a perspective camera rig, turns taps into rays against the stage and
    /// turns sim events into animation, debris, flashes, sound and haptics.
    /// Belt space (x, y) maps to world (x, 0, y): the picture is far from the camera, the queue nearest.
    /// </summary>
    public sealed class PixelLoop3DGameplay : GameplayBase, IPixelLoopGameplay
    {
        const float GroundY = -0.42f;
        const float BulletSpeed = 15f;
        const float LabelHeight = 1.15f;

        readonly PixelLoop3DStyle _style;
        readonly Shader _toyShader, _unlitShader;
        readonly Dictionary<Shooter, ShooterView3D> _views = new Dictionary<Shooter, ShooterView3D>();
        readonly List<Bullet> _bullets = new List<Bullet>();
        readonly List<Floater> _floaters = new List<Floater>();
        readonly List<Vector3> _framePoints = new List<Vector3>();
        readonly List<Mesh> _levelMeshes = new List<Mesh>();

        PixelLevel _level;
        BeltSim _sim;
        BeltGeometry _geo;
        Stage3DLayout _layout;
        Color[] _palette;
        Look3D _look;
        Fx3D _fx;
        CameraRig3D _rig;
        Environment3D _environment;
        Transform _stage, _board, _trayRoot;
        MeshRenderer[] _voxels;
        float _voxelSize;
        MeshRenderer[] _treads;
        float _treadGap, _treadOffset;
        MeshRenderer[] _pads, _lights, _columnGlows;
        Transform _progressFill;
        TextMesh _progressLabel;
        float _progressWidth, _shownProgress;
        float _platformBottomZ;
        float _time, _lastHitTime = -1f, _autoTimer;
        int _combo;
        bool _ending, _subscribed;

        public PixelLoop3DGameplay(PixelLoop3DStyle style, Shader toyShader, Shader unlitShader)
        {
            _style = style ?? new PixelLoop3DStyle();
            _toyShader = toyShader;
            _unlitShader = unlitShader;
        }

        public BeltSim Sim => _sim;
        public PixelLevel Level => _level;

        // ── Setup ───────────────────────────────────────────────────────────────────────

        protected override void OnBegin()
        {
            _level = (PixelLevel)Context.Level;
            _sim = new BeltSim(_level);
            _geo = _sim.Geometry;
            _layout = new Stage3DLayout(_geo, _sim.Columns.Count, _level.traySlots);
            _palette = new Color[_level.colors.Length];
            for (int i = 0; i < _palette.Length; i++) _palette[i] = PixelLoopArt.Hex(_level.colors[i], Color.magenta);
            _look = new Look3D(_toyShader, _unlitShader);

            _stage = new GameObject("Stage3D").transform;
            _stage.SetParent(Context.WorldRoot, false);
            _environment = Environment3D.Capture();

            BuildLight();
            BuildPlatformAndBelt();
            BuildBoard();
            BuildTray();
            BuildQueue();
            BuildFloaters();
            _fx = new Fx3D(_look, _stage, FloorAt);

            var cam = Context.Camera != null ? Context.Camera : Camera.main;
            _rig = new CameraRig3D(cam, _style.cameraPitch, _style.fieldOfView, _style.background);
            BuildProgress();
            CollectFramePoints();
            _rig.Frame(_framePoints, 0.035f, 0.035f, 0.03f, _style.hudMargin);
            _rig.PlayIntro(1.1f);
            Environment3D.Apply(_style, _rig.Distance);
            BuildGround();

            _sim.Launched += OnLaunched;
            _sim.Fired += OnFired;
            _sim.Spent += OnSpent;
            _sim.Docked += OnDocked;
            _sim.Denied += OnDenied;
            _sim.Won += OnWon;
            _sim.Lost += OnLost;
            Context.Input.Gestures.Tapped += OnTap;
            _subscribed = true;

            var driver = Context.WorldRoot.gameObject.AddComponent<PixelLoopDriver>();
            driver.Init(Context.Clock, Tick);
            Context.Hud.SetStatus(_level.name);
        }

        MeshRenderer Box(string name, Transform parent, Vector3 centre, Vector3 size, Color color, Material material = null, Mesh mesh = null)
        {
            var mr = _look.Renderer(name, parent, mesh != null ? mesh : Mesh3D.Slab, material != null ? material : _look.Matte);
            mr.transform.localPosition = centre;
            mr.transform.localScale = size;
            _look.Tint(mr, color);
            return mr;
        }

        /// <summary>Meshes made for this level only; destroyed with it.</summary>
        Mesh Keep(Mesh mesh)
        {
            _levelMeshes.Add(mesh);
            return mesh;
        }

        void BuildLight()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(_stage, false);
            go.transform.rotation = Quaternion.Euler(_style.sunAngles.x, _style.sunAngles.y, 0f);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = _style.sunColor;
            sun.intensity = _style.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = _style.shadowStrength;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.3f;
        }

        void BuildGround()
        {
            var ground = _look.Renderer("Ground", _stage, Keep(Mesh3D.Ground(45f, _style.groundCentre, _style.background)), _look.Matte);
            ground.shadowCastingMode = ShadowCastingMode.Off;
            ground.transform.localPosition = new Vector3(0f, GroundY, (_geo.Top + _layout.QueueBottomZ) * 0.5f);
            _look.Tint(ground, Color.white);
        }

        void BuildPlatformAndBelt()
        {
            float tw = Stage3DLayout.TrackWidth;
            float outerL = _geo.Left - tw / 2f - 0.16f, outerR = _geo.Right + tw / 2f + 0.16f;
            float outerT = _geo.Top + tw / 2f + 0.16f;
            _platformBottomZ = _geo.Bottom - tw / 2f - 0.55f;
            float depth = -GroundY;
            Box("Platform", _stage, new Vector3((outerL + outerR) / 2f, -depth / 2f - 0.02f, (outerT + _platformBottomZ) / 2f),
                new Vector3(outerR - outerL, depth, outerT - _platformBottomZ), _style.platform);

            var rail = _look.Renderer("Rail", _stage, Keep(Mesh3D.BeltRail(_geo, tw, _style.track, _style.lip)), _look.Toy);
            _look.Tint(rail, Color.white);

            int count = Mathf.Max(8, Mathf.FloorToInt(_geo.Length / 0.36f));
            _treadGap = _geo.Length / count;
            _treads = new MeshRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var tread = _look.Renderer("Tread", _stage, Mesh3D.Slab, _look.Toy, shadows: false);
                tread.transform.localScale = new Vector3(0.09f, 0.035f, tw * 0.64f);
                _look.Tint(tread, _style.tread);
                _treads[i] = tread;
            }
            PlaceTreads();

            // Belt capacity lights in front of the entry (bottom-left).
            _lights = new MeshRenderer[_level.beltCapacity];
            for (int i = 0; i < _lights.Length; i++)
            {
                var light = _look.Renderer("CapacityLight", _stage, Mesh3D.Sphere, _look.Toy);
                light.transform.localPosition = new Vector3(_geo.Left - tw / 2f + 0.2f + i * 0.28f, 0.08f, _geo.Bottom - tw / 2f - 0.3f);
                light.transform.localScale = Vector3.one * 0.18f;
                _lights[i] = light;
            }
        }

        void BuildBoard()
        {
            float tw = Stage3DLayout.TrackWidth;
            float w = _geo.Right - _geo.Left - tw, h = _geo.Top - _geo.Bottom - tw;
            _board = new GameObject("Board").transform;
            _board.SetParent(_stage, false);
            Box("Card", _board, new Vector3(0f, -0.07f, 0f), new Vector3(w + 0.04f, 0.14f, h + 0.04f), _style.board);
            var ghosts = _look.Renderer("Ghosts", _board, Keep(Mesh3D.GhostTiles(_level, _geo, _palette, _style.ghostTint, _style.board)), _look.Matte, shadows: false);
            ghosts.receiveShadows = true;
            _look.Tint(ghosts, Color.white);

            _voxels = new MeshRenderer[_level.Width * _level.Height];
            _voxelSize = _geo.Cell * 0.9f;
            var centre = new Vector2(_level.Width / 2f, _level.Height / 2f);
            float maxDist = Mathf.Max(0.01f, centre.magnitude);
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                int color = _level.Cell(x, y);
                if (color < 0) continue;
                var mr = _look.Renderer($"Voxel_{x}_{y}", _board, Mesh3D.Voxel, _look.Toy);
                _look.Tint(mr, _palette[color]);
                var t = mr.transform;
                var rest = VoxelPosition(x, y);
                t.localPosition = rest + Vector3.up * 3f;
                t.localScale = Vector3.zero;
                _voxels[x + y * _level.Width] = mr;
                // Rain down from the centre outwards and bounce into place.
                float delay = 0.25f + Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) / maxDist * 0.55f;
                float size = _voxelSize;
                Context.Tweens.To(0f, 1f, 0.55f, k =>
                {
                    if (t == null) return;
                    t.localPosition = rest + Vector3.up * (3f * (1f - Easing.Evaluate(Ease.OutBounce, k)));
                    t.localScale = Vector3.one * (size * Mathf.Min(1f, k * 4f));
                }, Ease.Linear, delay: delay).SetTarget(t);
            }
        }

        Vector3 VoxelPosition(int x, int y)
        {
            _geo.CellCenter(x, y, out float cx, out float cy);
            return new Vector3(cx, _voxelSize * 0.5f, cy);
        }

        void BuildTray()
        {
            _trayRoot = new GameObject("Tray").transform;
            _trayRoot.SetParent(_stage, false);
            _trayRoot.localPosition = new Vector3(0f, 0f, _layout.TrayZ);
            float width = Mathf.Max(1f, _level.traySlots * Stage3DLayout.SlotGap + 0.3f);
            float depth = -GroundY;
            Box("TrayBase", _trayRoot, new Vector3(0f, -depth / 2f - 0.02f, 0f), new Vector3(width, depth, 1.12f), _style.platform);
            _pads = new MeshRenderer[_level.traySlots];
            for (int i = 0; i < _pads.Length; i++)
                _pads[i] = Box("Slot", _trayRoot, new Vector3(_layout.SlotX(i), -0.01f, 0f), new Vector3(0.84f, 0.04f, 0.84f), _style.slot, _look.Toy);
        }

        void BuildQueue()
        {
            int columns = _sim.Columns.Count;
            float depth = -GroundY;
            float zTop = _layout.QueueTopZ + 0.55f, zBottom = _layout.QueueBottomZ;
            _columnGlows = new MeshRenderer[columns];
            for (int c = 0; c < columns; c++)
            {
                float x = _layout.ColumnX(c);
                Box($"Lane_{c}", _stage, new Vector3(x, -depth / 2f - 0.02f, (zTop + zBottom) / 2f),
                    new Vector3(Stage3DLayout.ColumnGap * 0.88f, depth, zTop - zBottom), _style.platform);
                var glow = _look.Renderer($"FrontGlow_{c}", _stage, Mesh3D.Disc, _look.Additive, shadows: false);
                glow.transform.localPosition = new Vector3(x, 0.01f, _layout.RowZ(0));
                glow.transform.localScale = new Vector3(1.05f, 1f, 1.05f);
                _look.SetColor(glow, new Color(1f, 1f, 1f, 0f));
                _columnGlows[c] = glow;

                for (int r = 0; r < _sim.Columns[c].Count; r++)
                {
                    var shooter = _sim.Columns[c][r];
                    var view = new ShooterView3D(shooter, _stage, _palette[shooter.Color], _look);
                    var p = QueuePosition(c, r);
                    view.Root.localPosition = p + new Vector3(0f, -1.6f, 0f);
                    view.StartBlend(0.5f + r * 0.07f + c * 0.05f, 0f);
                    view.Key = KeyOf(shooter);
                    _views[shooter] = view;
                }
            }
        }

        void BuildProgress()
        {
            _progressWidth = (_geo.Right - _geo.Left) * 0.6f;
            var root = new GameObject("Progress").transform;
            root.SetParent(_stage, false);
            root.localPosition = new Vector3(-0.3f, 0.35f, _geo.Top + Stage3DLayout.TrackWidth / 2f + 0.75f);
            root.localRotation = Quaternion.Euler(_rig.Pitch, 0f, 0f); // faces the camera
            Box("ProgressBack", root, Vector3.zero, new Vector3(_progressWidth + 0.08f, 0.24f, 0.08f), _style.track, _look.Toy);
            var fill = Box("ProgressFill", root, new Vector3(0f, 0f, -0.03f), new Vector3(0.2f, 0.17f, 0.06f), _style.progress, _look.Toy);
            _look.Tint(fill, _style.progress, new Color(_style.progress.r, _style.progress.g, _style.progress.b, 0.45f));
            _progressFill = fill.transform;
            _progressLabel = PixelLoopArt.Label("ProgressLabel", root, "0%", 0.3f, Color.white, 0);
            _progressLabel.GetComponent<MeshRenderer>().sharedMaterial = _look.Text;
            _progressLabel.transform.localPosition = new Vector3(_progressWidth / 2f + 0.5f, 0f, -0.05f);
            UpdateProgress(0f);
        }

        void BuildFloaters()
        {
            var rng = new System.Random(Context.LevelIndex * 17 + 3);
            float half = Mathf.Max(_geo.Right, _layout.LowerHalfWidth) + 0.9f;
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var pos = new Vector3(side * (half + (float)rng.NextDouble() * 2.2f), 0.4f + (float)rng.NextDouble() * 1.6f,
                    Mathf.Lerp(_layout.QueueBottomZ, _geo.Top + 1.5f, (float)rng.NextDouble()));
                var mr = _look.Renderer("Floater", _stage, Mesh3D.Voxel, _look.Toy);
                var c = Color.Lerp(_palette[rng.Next(_palette.Length)], Color.white, 0.25f);
                _look.Tint(mr, c);
                float size = 0.22f + (float)rng.NextDouble() * 0.3f;
                mr.transform.localScale = Vector3.one * size;
                mr.transform.localPosition = pos;
                mr.transform.localRotation = Random.rotationUniform;
                _floaters.Add(new Floater
                {
                    T = mr.transform, Home = pos, Phase = (float)rng.NextDouble() * 6.28f,
                    Axis = Random.onUnitSphere, Spin = 20f + (float)rng.NextDouble() * 40f
                });
            }
        }

        void CollectFramePoints()
        {
            float tw = Stage3DLayout.TrackWidth;
            float l = _geo.Left - tw / 2f, r = _geo.Right + tw / 2f, top = _geo.Top + tw / 2f;
            float half = _layout.LowerHalfWidth + 0.15f;
            _framePoints.Clear();
            foreach (var y in new[] { 0f, 0.6f })
            {
                _framePoints.Add(new Vector3(l, y, top + 1.1f));
                _framePoints.Add(new Vector3(r, y, top + 1.1f));
                _framePoints.Add(new Vector3(l, y, _platformBottomZ));
                _framePoints.Add(new Vector3(r, y, _platformBottomZ));
                _framePoints.Add(new Vector3(-half, y, _layout.QueueBottomZ));
                _framePoints.Add(new Vector3(half, y, _layout.QueueBottomZ));
            }
            // Ammo badges above the nearest queue row.
            _framePoints.Add(new Vector3(-half, LabelHeight, _layout.RowZ(Stage3DLayout.VisibleRows - 1)));
            _framePoints.Add(new Vector3(half, LabelHeight, _layout.RowZ(Stage3DLayout.VisibleRows - 1)));
            for (int i = 0; i < _framePoints.Count; i++) _framePoints[i] = _stage.TransformPoint(_framePoints[i]);
        }

        Vector3 QueuePosition(int column, int row) => new Vector3(_layout.ColumnX(column), 0f, _layout.RowZ(row));

        float FloorAt(Vector3 p)
        {
            float tw = Stage3DLayout.TrackWidth;
            if (p.x >= _geo.Left - tw / 2f - 0.16f && p.x <= _geo.Right + tw / 2f + 0.16f &&
                p.z >= _platformBottomZ && p.z <= _geo.Top + tw / 2f + 0.16f) return 0f;
            if (Mathf.Abs(p.z - _layout.TrayZ) <= 0.56f && Mathf.Abs(p.x) <= _level.traySlots * Stage3DLayout.SlotGap / 2f + 0.15f) return 0f;
            if (p.z <= _layout.QueueTopZ + 0.55f && p.z >= _layout.QueueBottomZ && Mathf.Abs(p.x) <= _sim.Columns.Count * Stage3DLayout.ColumnGap / 2f)
                return 0f;
            return GroundY;
        }

        // ── Input ───────────────────────────────────────────────────────────────────────

        void OnTap(TapGesture tap)
        {
            if (IsFinished || _ending || !Context.Input.IsGameplayInputEnabled || _rig.Camera == null) return;
            var ray = _rig.Camera.ScreenPointToRay(new Vector3(tap.Position.x, tap.Position.y, 0f));
            var plane = new Plane(_stage.up, _stage.TransformPoint(new Vector3(0f, Stage3DLayout.TapHeight, 0f)));
            if (!plane.Raycast(ray, out float enter)) return;
            var local = _stage.InverseTransformPoint(ray.GetPoint(enter));
            switch (_layout.HitTest(local.x, local.z, out int index))
            {
                case StageTapTarget.Column:
                    _sim.TryLaunchColumn(index);
                    break;
                case StageTapTarget.TraySlot:
                    var s = _sim.Tray[index];
                    if (s != null) _sim.TryLaunch(s);
                    break;
            }
        }

        /// <summary>World point a player would tap to send this shooter (tests, bots).</summary>
        public Vector3 TapPointFor(Shooter s)
        {
            var local = s.State == ShooterState.Tray
                ? new Vector3(_layout.SlotX(s.Slot), Stage3DLayout.TapHeight, _layout.TrayZ)
                : new Vector3(_layout.ColumnX(s.Column), Stage3DLayout.TapHeight, _layout.RowZ(0));
            return _stage.TransformPoint(local);
        }

        // ── Frame ───────────────────────────────────────────────────────────────────────

        void Tick(float dt)
        {
            _time += dt;
            if (!IsFinished && !_ending)
            {
                _sim.Step(dt * PixelLoopDebug.SpeedBoost);
                if (PixelLoopDebug.AutoPlay) RunAutoPlay(dt);
            }

            _rig.Tick(dt, _time);
            var camRotation = _rig.Rotation;
            _treadOffset = (_treadOffset + _sim.Speed * PixelLoopDebug.SpeedBoost * dt) % _treadGap;
            PlaceTreads();
            foreach (var view in _views.Values) UpdateView(view, dt, camRotation);
            UpdateTrayLightsAndGlows();
            UpdateProgress(dt);
            foreach (var b in _bullets) b.Face(camRotation);
            foreach (var f in _floaters) f.Update(dt, _time);
            _fx.Tick(dt, camRotation);
        }

        void PlaceTreads()
        {
            for (int i = 0; i < _treads.Length; i++)
            {
                var p = _geo.At(i * _treadGap + _treadOffset);
                var t = _treads[i].transform;
                t.localPosition = new Vector3(p.X, 0.035f, p.Y);
                t.localRotation = Quaternion.Euler(0f, Mathf.Atan2(p.NX, p.NY) * Mathf.Rad2Deg, 0f);
            }
        }

        void UpdateTrayLightsAndGlows()
        {
            int used = _sim.TrayUsed;
            bool danger = !_sim.IsWon && used >= _level.traySlots - 1;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 9f);
            var warn = _style.warning;
            for (int i = 0; i < _pads.Length; i++)
            {
                if (_sim.IsLost) _look.Tint(_pads[i], Color.Lerp(_style.slot, warn, 0.6f), new Color(warn.r, warn.g, warn.b, 0.7f));
                else if (danger) _look.Tint(_pads[i], _style.slot, new Color(warn.r, warn.g, warn.b, 0.55f * pulse));
                else _look.Tint(_pads[i], _style.slot);
            }

            int load = _sim.BeltLoad;
            for (int i = 0; i < _lights.Length; i++)
            {
                bool on = i < load;
                var c = on ? (load >= _lights.Length ? warn : _style.progress) : _style.lip;
                _look.Tint(_lights[i], c, on ? new Color(c.r, c.g, c.b, 0.9f) : default);
            }

            // Soft pulse under every queue front that can be sent: "tap me".
            float breathe = 0.22f + 0.12f * Mathf.Sin(_time * 4f);
            for (int c = 0; c < _columnGlows.Length; c++)
            {
                var front = _sim.Front(c);
                bool ready = front != null && _sim.CanLaunch(front) && !_ending;
                var col = front != null ? _palette[front.Color] : Color.white;
                _look.SetColor(_columnGlows[c], new Color(col.r, col.g, col.b, ready ? breathe : 0f));
            }
        }

        void UpdateProgress(float dt)
        {
            _shownProgress = Mathf.MoveTowards(_shownProgress, _sim.Cleared, dt * 1.5f);
            float w = Mathf.Max(0.17f, _progressWidth * _shownProgress);
            _progressFill.localScale = new Vector3(w, 0.17f, 0.06f);
            _progressFill.localPosition = new Vector3(-_progressWidth / 2f + w / 2f, 0f, -0.03f);
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

        Vector3 TargetOf(Shooter s, out float yaw, out float scale)
        {
            yaw = 180f; // facing the player while waiting
            scale = 1f;
            switch (s.State)
            {
                case ShooterState.Queued:
                {
                    int row = _sim.Columns[s.Column].IndexOf(s);
                    if (row >= Stage3DLayout.VisibleRows) scale = 0f;
                    return QueuePosition(s.Column, row);
                }
                case ShooterState.Tray:
                    return new Vector3(_layout.SlotX(s.Slot), 0f, _layout.TrayZ);
                case ShooterState.Waiting:
                {
                    var p = _geo.At(0f);
                    yaw = Mathf.Atan2(p.NX, p.NY) * Mathf.Rad2Deg;
                    return new Vector3(p.X, 0.02f, p.Y);
                }
                default:
                {
                    var p = _geo.At(Mathf.Min(s.Distance, _geo.Length - 0.001f));
                    yaw = Mathf.Atan2(p.NX, p.NY) * Mathf.Rad2Deg;
                    return new Vector3(p.X, 0.02f, p.Y);
                }
            }
        }

        void UpdateView(ShooterView3D view, float dt, Quaternion camRotation)
        {
            var s = view.Shooter;
            int key = KeyOf(s);
            if (key != view.Key)
            {
                bool launch = view.Key < 3000 && key >= 3000;
                bool dock = key >= 2000 && key < 3000;
                view.StartBlend(launch ? BeltSim.HopSeconds : dock ? 0.45f : 0.18f, launch ? 1.1f : dock ? 0.9f : 0f);
                view.Key = key;
            }
            var target = TargetOf(s, out float yaw, out float scale);
            view.Animate(target, yaw, scale, dt, _time, camRotation);
        }

        // ── Sim events → feel ───────────────────────────────────────────────────────────

        void OnLaunched(Shooter s, bool fromTray)
        {
            Moves = _sim.Launches;
            Context.Audio.PlaySfx(PixelLoopSounds.Launch);
            Context.Feedback.Haptic(HapticType.Selection);
            if (!_views.TryGetValue(s, out var view)) return;
            view.Squash(Context.Tweens);
            var p = view.Root.localPosition;
            _fx.Ring(new Vector3(p.x, 0f, p.z), _palette[s.Color], 0.55f, 0.3f);
        }

        void OnFired(Shooter s, int x, int y, int color)
        {
            if (!_views.TryGetValue(s, out var view)) return;
            view.SetAmmo(s.Ammo, Context.Tweens);
            view.Recoil(Context.Tweens);
            Context.Audio.PlaySfx(PixelLoopSounds.Shot, 0.7f);

            var from = view.MuzzleLocal();
            var c = _palette[color];
            _fx.Flash(from, Color.Lerp(c, Color.white, 0.5f), 0.75f, 0.12f);
            var to = VoxelPosition(x, y);
            var bullet = RentBullet();
            bullet.Launch(from, to, c, _look);
            float duration = Mathf.Max(0.05f, Vector3.Distance(from, to) / BulletSpeed);
            int px = x, py = y;
            var bt = bullet.Root;
            Context.Tweens.To(0f, 1f, duration, k => { if (bt != null) bullet.Move(k); }, Ease.InQuad).SetTarget(bt)
                .OnComplete(() =>
                {
                    if (bt == null) return;
                    bullet.Hide();
                    PopVoxel(px, py, color);
                });
        }

        void PopVoxel(int x, int y, int color)
        {
            var mr = _voxels[x + y * _level.Width];
            if (mr == null) return;
            var t = mr.transform;
            var c = _palette[color];
            float size = _voxelSize;
            var rest = VoxelPosition(x, y);
            Context.Tweens.KillTarget(t);
            Context.Tweens.To(0f, 1f, 0.17f, k =>
            {
                if (t == null) return;
                if (k < 0.35f)
                {
                    float a = k / 0.35f;
                    t.localScale = Vector3.one * (size * (1f + 0.35f * a));
                    _look.Tint(mr, c, new Color(1f, 1f, 1f, a));
                }
                else
                {
                    float a = (k - 0.35f) / 0.65f;
                    t.localScale = Vector3.one * (size * 1.35f * (1f - a * a));
                }
            }, Ease.Linear).SetTarget(t).OnComplete(() => { if (t != null) t.gameObject.SetActive(false); });
            t.localPosition = rest;

            var world = rest; // board space equals stage space (the board is not offset)
            _fx.Debris(world, c, size * 0.36f, 7);
            _fx.Flash(world + Vector3.up * 0.1f, Color.Lerp(c, Color.white, 0.55f), size * 3.2f, 0.2f);
            _fx.Sparks(world, c, 4, 3.5f);
            _rig.Shake(0.035f);

            _combo = _time - _lastHitTime < 0.3f ? _combo + 1 : 0;
            _lastHitTime = _time;
            Context.Audio.PlaySfx(PixelLoopSounds.Hit(_combo / 3));
        }

        void OnSpent(Shooter s)
        {
            if (!_views.TryGetValue(s, out var view)) return;
            _views.Remove(s);
            var pos = view.Root.localPosition + Vector3.up * 0.35f;
            var color = _palette[s.Color];
            Context.Audio.PlaySfx(PixelLoopSounds.Spent);
            Context.Feedback.Haptic(HapticType.Light);
            view.Vanish(Context.Tweens);
            Context.Tweens.Delay(0.12f, () =>
            {
                if (_fx == null || _stage == null) return;
                _fx.Debris(pos, color, 0.15f, 14, 1.15f);
                _fx.Flash(pos, Color.white, 1.7f, 0.22f);
                _fx.Sparks(pos, color, 8, 5f);
                _fx.Ring(new Vector3(pos.x, 0f, pos.z), color, 1f, 0.45f);
                _rig.Shake(0.12f);
            });
        }

        void OnDocked(Shooter s)
        {
            Context.Audio.PlaySfx(PixelLoopSounds.Dock);
            Context.Feedback.Haptic(HapticType.Medium);
            if (s.Slot >= 0 && s.Slot < _pads.Length) Context.Feedback.Punch(_pads[s.Slot].transform, 0.25f, 0.25f);
            if (_sim.TrayUsed >= _level.traySlots - 1)
            {
                Context.Audio.PlaySfx(PixelLoopSounds.Warn);
                Context.Feedback.Shake(_trayRoot, 0.08f, 0.25f);
                _rig.Shake(0.2f);
            }
        }

        void OnDenied(Shooter s)
        {
            if (_sim.IsOver) return;
            Context.Audio.PlaySfx(PixelLoopSounds.Deny);
            Context.Feedback.Haptic(HapticType.Light);
            if (_views.TryGetValue(s, out var view)) view.Wobble(Context.Tweens);
            if (_sim.BeltFull)
                foreach (var l in _lights) Context.Feedback.Punch(l.transform, 0.6f, 0.22f);
        }

        void OnWon()
        {
            _ending = true;
            Context.Feedback.SlowMo(0.35f, 0.5f);
            Context.Tweens.Delay(0.45f, Reveal);
        }

        /// <summary>The finished picture rises back out of the board in a sweeping wave of light.</summary>
        void Reveal()
        {
            if (_stage == null) return;
            Context.Audio.PlaySfx(PixelLoopSounds.Reveal);
            Context.Feedback.Haptic(HapticType.Success);
            float span = _level.Width + _level.Height;
            float size = _voxelSize;
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                var mr = _voxels[x + y * _level.Width];
                if (mr == null) continue;
                var t = mr.transform;
                var c = _palette[_level.Cell(x, y)];
                var rest = VoxelPosition(x, y);
                float delay = (x + (_level.Height - 1 - y)) / span * 0.7f;
                t.gameObject.SetActive(true);
                t.localScale = Vector3.zero;
                Context.Tweens.KillTarget(t);
                Context.Tweens.To(0f, 1f, 0.6f, k =>
                {
                    if (t == null) return;
                    float rise = Easing.Evaluate(Ease.OutBack, Mathf.Min(1f, k * 1.5f));
                    t.localPosition = rest + Vector3.down * (size * (1f - rise));
                    t.localScale = Vector3.one * (size * Mathf.Clamp01(rise));
                    float glow = 1f - Mathf.Clamp01((k - 0.2f) / 0.8f);
                    _look.Tint(mr, c, new Color(1f, 1f, 1f, glow));
                }, Ease.Linear, delay: delay).SetTarget(t);
            }
            foreach (var view in _views.Values) view.Vanish(Context.Tweens);
            _views.Clear();
            _fx.Ring(Vector3.zero, Color.white, BeltGeometry.BoardSize * 0.7f, 0.8f);
            _fx.Sparks(new Vector3(0f, 0.5f, 0f), Color.white, 16, 6f);
            _rig.PushIn(0.1f);
            Context.Tweens.Delay(1.4f, () =>
                Finish(new LevelOutcome { Won = true, Stars = _sim.Stars, Moves = _sim.Launches }));
        }

        void OnLost(string reason)
        {
            _ending = true;
            Context.Audio.PlaySfx(PixelLoopSounds.Warn);
            Context.Feedback.Haptic(HapticType.Heavy);
            _rig.Shake(0.75f);
            Context.Feedback.Shake(_trayRoot, 0.15f, 0.45f);
            foreach (var view in _views.Values)
                if (view.Shooter.State == ShooterState.OnBelt) view.Wobble(Context.Tweens);
            Context.Tweens.Delay(1.0f, () => Finish(new LevelOutcome { Won = false, Reason = reason, Moves = _sim.Launches }));
        }

        Bullet RentBullet()
        {
            foreach (var b in _bullets)
                if (!b.InUse) return b;
            var bullet = new Bullet(_stage, _look);
            _bullets.Add(bullet);
            return bullet;
        }

        // ── Auto play (debug console) ───────────────────────────────────────────────────

        void RunAutoPlay(float dt)
        {
            _autoTimer -= dt;
            if (_autoTimer > 0f || _sim.BeltFull) return;
            _autoTimer = 0.35f;
            var best = BeltBot.Choose(_sim);
            if (best != null) _sim.TryLaunch(best);
        }

        public override void Dispose()
        {
            if (_subscribed)
            {
                Context.Input.Gestures.Tapped -= OnTap;
                _sim.Launched -= OnLaunched;
                _sim.Fired -= OnFired;
                _sim.Spent -= OnSpent;
                _sim.Docked -= OnDocked;
                _sim.Denied -= OnDenied;
                _sim.Won -= OnWon;
                _sim.Lost -= OnLost;
                _subscribed = false;
            }
            _environment.Restore();
            _fx?.Destroy();
            _look?.Destroy();
            _fx = null;
            foreach (var mesh in _levelMeshes) Object.Destroy(mesh);
            _levelMeshes.Clear();
            _views.Clear();
        }

        // ── Small view helpers ──────────────────────────────────────────────────────────

        sealed class Floater
        {
            public Transform T;
            public Vector3 Home, Axis;
            public float Phase, Spin;

            public void Update(float dt, float time)
            {
                if (T == null) return;
                T.localPosition = Home + new Vector3(0f, Mathf.Sin(time * 0.8f + Phase) * 0.25f, 0f);
                T.localRotation = Quaternion.AngleAxis(Spin * dt, Axis) * T.localRotation;
            }
        }

        sealed class Bullet
        {
            public readonly Transform Root;
            readonly MeshRenderer _core, _glow;
            readonly TrailRenderer _trail;
            readonly Look3D _look;
            Vector3 _from, _to;
            public bool InUse { get; private set; }

            public Bullet(Transform parent, Look3D look)
            {
                _look = look;
                Root = new GameObject("Bullet").transform;
                Root.SetParent(parent, false);
                _core = look.Renderer("Core", Root, Mesh3D.Sphere, look.Toy, shadows: false);
                _core.transform.localScale = Vector3.one * 0.16f;
                _glow = look.Renderer("Glow", Root, Mesh3D.Quad, look.Additive, shadows: false);
                _glow.transform.localScale = Vector3.one * 0.7f;
                _trail = Root.gameObject.AddComponent<TrailRenderer>();
                _trail.sharedMaterial = look.Trail;
                _trail.time = 0.1f;
                _trail.minVertexDistance = 0.03f;
                _trail.numCapVertices = 2;
                _trail.shadowCastingMode = ShadowCastingMode.Off;
                _trail.receiveShadows = false;
                _trail.alignment = LineAlignment.View;
                Root.gameObject.SetActive(false);
            }

            public void Launch(Vector3 from, Vector3 to, Color color, Look3D look)
            {
                InUse = true;
                _from = from;
                _to = to;
                Root.localPosition = from;
                look.Tint(_core, color, new Color(color.r * 0.5f + 0.5f, color.g * 0.5f + 0.5f, color.b * 0.5f + 0.5f, 1f));
                look.SetColor(_glow, new Color(color.r, color.g, color.b, 0.85f));
                _trail.startWidth = 0.13f;
                _trail.endWidth = 0f;
                _trail.startColor = Color.Lerp(color, Color.white, 0.3f);
                _trail.endColor = new Color(color.r, color.g, color.b, 0f);
                Root.gameObject.SetActive(true);
                _trail.Clear();
            }

            public void Move(float k) =>
                Root.localPosition = Vector3.LerpUnclamped(_from, _to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.12f);

            public void Face(Quaternion camera)
            {
                if (InUse && _glow != null) _glow.transform.rotation = camera;
            }

            public void Hide()
            {
                InUse = false;
                Root.gameObject.SetActive(false);
            }
        }
    }
}
