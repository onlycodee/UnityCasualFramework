using System;
using System.Collections.Generic;

namespace Game
{
    public enum ShooterState { Queued, Waiting, OnBelt, Tray, Spent }

    public sealed class Shooter
    {
        public int Id;
        public int Color;
        public int Ammo;
        public ShooterState State;
        /// <summary>Queue column it started in.</summary>
        public int Column;
        /// <summary>Tray slot while in the tray, else -1.</summary>
        public int Slot = -1;
        /// <summary>Distance travelled on the current loop.</summary>
        public float Distance;
        internal int NextStop;
        internal float WaitTime;
        /// <summary>Loops completed with ammo left (sent to the tray).</summary>
        public int Docks;
    }

    /// <summary>
    /// The rules of Pixel Loop, frame-rate independent and engine-free.
    /// <list type="bullet">
    /// <item>Tap the front shooter of a queue column (or a shooter in the tray) to send it onto the belt.</item>
    /// <item>The belt carries it once around the picture. Each time it lines up with a row or column it looks
    /// inward; if the first pixel there has its colour, it fires and that pixel is destroyed.</item>
    /// <item>Out of ammo: the shooter is gone. Loop finished with ammo left: it parks in the tray.
    /// No free tray slot: the level is lost.</item>
    /// <item>Clear every pixel to win.</item>
    /// </list>
    /// </summary>
    public sealed class BeltSim
    {
        /// <summary>Seconds a sent shooter takes to hop from the queue onto the belt.</summary>
        public const float HopSeconds = 0.28f;

        public readonly PixelLevel Level;
        public readonly BeltGeometry Geometry;
        public readonly List<Shooter> Shooters = new List<Shooter>();
        public readonly List<List<Shooter>> Columns = new List<List<Shooter>>();
        public readonly Shooter[] Tray;
        /// <summary>Shooters on the belt, front (furthest along) first.</summary>
        public readonly List<Shooter> Belt = new List<Shooter>();
        readonly List<Shooter> _waiting = new List<Shooter>();
        readonly int[] _cells;
        readonly int[] _remainingByColor;

        public float Speed { get; }
        public int Remaining { get; private set; }
        public int Total { get; }
        public bool IsOver => IsWon || IsLost;
        public bool IsWon { get; private set; }
        public bool IsLost { get; private set; }
        public string LoseReason { get; private set; }
        public int Launches { get; private set; }
        public int Docks { get; private set; }
        /// <summary>3 if no shooter came back with ammo, 2 for one or two returns, else 1.</summary>
        public int Stars => Docks == 0 ? 3 : Docks <= 2 ? 2 : 1;

        /// <summary>A shooter left its column or the tray. Args: shooter, fromTray.</summary>
        public event Action<Shooter, bool> Launched;
        /// <summary>A waiting shooter got onto the belt.</summary>
        public event Action<Shooter> Entered;
        /// <summary>Args: shooter, pixel x, pixel y, colour.</summary>
        public event Action<Shooter, int, int, int> Fired;
        public event Action<Shooter> Spent;
        public event Action<Shooter> Docked;
        /// <summary>A tap that could not launch (belt full, not the front, level over). Args: shooter.</summary>
        public event Action<Shooter> Denied;
        public event Action Won;
        public event Action<string> Lost;

        public BeltSim(PixelLevel level)
        {
            Level = level;
            Geometry = new BeltGeometry(level.Width, level.Height);
            Speed = Geometry.Length / level.loopSeconds;
            _cells = level.CopyCells();
            _remainingByColor = new int[Math.Max(1, level.colors.Length)];
            foreach (var c in _cells)
                if (c >= 0) { Remaining++; _remainingByColor[c]++; }
            Total = Remaining;

            var columns = level.QueueColumns();
            for (int col = 0; col < columns.Count; col++)
            {
                var list = new List<Shooter>();
                foreach (int index in columns[col])
                {
                    var spec = level.shooters[index];
                    var s = new Shooter { Id = index, Color = spec.color, Ammo = spec.ammo, Column = col, State = ShooterState.Queued };
                    list.Add(s);
                    Shooters.Add(s);
                }
                Columns.Add(list);
            }
            Shooters.Sort((a, b) => a.Id.CompareTo(b.Id));
            Tray = new Shooter[level.traySlots];
        }

        public int Cell(int x, int y) => _cells[x + y * Level.Width];
        /// <summary>Copy of the remaining picture (hints, bots).</summary>
        public int[] SnapshotCells() => (int[])_cells.Clone();
        /// <summary>Shooters launched but still waiting for room at the belt entry.</summary>
        public IReadOnlyList<Shooter> Waiting => _waiting;
        public int RemainingOf(int color) => color >= 0 && color < _remainingByColor.Length ? _remainingByColor[color] : 0;
        /// <summary>Shooters on the belt or waiting to enter it.</summary>
        public int BeltLoad => Belt.Count + _waiting.Count;
        public bool BeltFull => BeltLoad >= Level.beltCapacity;
        public float Cleared => Total == 0 ? 1f : 1f - (float)Remaining / Total;
        public int TrayUsed
        {
            get
            {
                int n = 0;
                foreach (var s in Tray) if (s != null) n++;
                return n;
            }
        }

        public Shooter Front(int column) => column >= 0 && column < Columns.Count && Columns[column].Count > 0 ? Columns[column][0] : null;

        /// <summary>True if tapping this shooter would send it now.</summary>
        public bool CanLaunch(Shooter s) =>
            !IsOver && s != null && !BeltFull &&
            ((s.State == ShooterState.Queued && Front(s.Column) == s) || s.State == ShooterState.Tray);

        /// <summary>Player tap on a shooter. Returns true if it was sent.</summary>
        public bool TryLaunch(Shooter s)
        {
            if (!CanLaunch(s))
            {
                if (s != null) Denied?.Invoke(s);
                return false;
            }
            bool fromTray = s.State == ShooterState.Tray;
            if (fromTray)
            {
                Tray[s.Slot] = null;
                s.Slot = -1;
            }
            else Columns[s.Column].RemoveAt(0);

            s.State = ShooterState.Waiting;
            s.Distance = 0f;
            s.NextStop = 0;
            s.WaitTime = 0f;
            _waiting.Add(s);
            Launches++;
            Launched?.Invoke(s, fromTray);
            AdmitWaiting();
            return true;
        }

        public bool TryLaunchColumn(int column) => TryLaunch(Front(column));

        bool EntryClear()
        {
            foreach (var s in Belt)
                if (s.Distance < BeltGeometry.Spacing || s.Distance > Geometry.Length - BeltGeometry.Spacing) return false;
            return true;
        }

        void AdmitWaiting()
        {
            while (_waiting.Count > 0 && _waiting[0].WaitTime >= HopSeconds && EntryClear())
            {
                var s = _waiting[0];
                _waiting.RemoveAt(0);
                s.State = ShooterState.OnBelt;
                Belt.Add(s); // newest is last = furthest behind
                Entered?.Invoke(s);
            }
        }

        /// <summary>Advances the belt by dt seconds of game time.</summary>
        public void Step(float dt)
        {
            if (IsOver || dt <= 0f) return;
            // Sub-step so a long frame can never skip past a whole lane.
            float maxMove = Geometry.Cell * 0.5f;
            int steps = Math.Max(1, (int)Math.Ceiling(Speed * dt / maxMove));
            float sub = dt / steps;
            for (int i = 0; i < steps && !IsOver; i++) StepOnce(sub);
        }

        void StepOnce(float dt)
        {
            foreach (var w in _waiting) w.WaitTime += dt;
            float move = Speed * dt;
            // Front shooters act first so they claim a pixel before the ones behind them.
            for (int i = 0; i < Belt.Count && !IsOver; i++)
            {
                var s = Belt[i];
                s.Distance += move;
                var stops = Geometry.Stops;
                while (!IsOver && s.NextStop < stops.Length && stops[s.NextStop].Distance <= s.Distance && s.Ammo > 0)
                {
                    TryFire(s, stops[s.NextStop]);
                    s.NextStop++;
                }
                if (s.Ammo <= 0)
                {
                    s.State = ShooterState.Spent;
                    Belt.RemoveAt(i--);
                    Spent?.Invoke(s);
                    continue;
                }
                if (IsOver) return;
                if (s.Distance >= Geometry.Length)
                {
                    Belt.RemoveAt(i--);
                    Dock(s);
                    if (IsOver) return;
                }
            }
            AdmitWaiting();
            CheckStuck();
        }

        void TryFire(Shooter s, BeltStop stop)
        {
            if (!FirstInLane(stop, out int x, out int y)) return;
            int color = _cells[x + y * Level.Width];
            if (color != s.Color) return;
            _cells[x + y * Level.Width] = -1;
            _remainingByColor[color]--;
            Remaining--;
            s.Ammo--;
            Fired?.Invoke(s, x, y, color);
            if (Remaining == 0)
            {
                IsWon = true;
                Won?.Invoke();
            }
        }

        /// <summary>The first pixel a shooter at this stop can see, looking inward.</summary>
        public bool FirstInLane(BeltStop stop, out int x, out int y) => FindFirst(_cells, Level.Width, Level.Height, stop, out x, out y);

        static bool FindFirst(int[] grid, int w, int h, BeltStop stop, out int x, out int y)
        {
            switch (stop.Side)
            {
                case BeltSide.Bottom:
                    x = stop.Lane;
                    for (y = 0; y < h; y++) if (grid[x + y * w] >= 0) return true;
                    break;
                case BeltSide.Top:
                    x = stop.Lane;
                    for (y = h - 1; y >= 0; y--) if (grid[x + y * w] >= 0) return true;
                    break;
                case BeltSide.Right:
                    y = stop.Lane;
                    for (x = w - 1; x >= 0; x--) if (grid[x + y * w] >= 0) return true;
                    break;
                default:
                    y = stop.Lane;
                    for (x = 0; x < w; x++) if (grid[x + y * w] >= 0) return true;
                    break;
            }
            x = y = -1;
            return false;
        }

        void Dock(Shooter s)
        {
            for (int i = 0; i < Tray.Length; i++)
            {
                if (Tray[i] != null) continue;
                Tray[i] = s;
                s.Slot = i;
                s.State = ShooterState.Tray;
                s.Docks++;
                Docks++;
                Docked?.Invoke(s);
                return;
            }
            Lose("tray_full");
        }

        void CheckStuck()
        {
            if (IsOver || Belt.Count > 0 || _waiting.Count > 0) return;
            foreach (var column in Columns) if (column.Count > 0) return;
            foreach (var s in Tray) if (s != null) return;
            Lose("out_of_shooters");
        }

        void Lose(string reason)
        {
            if (IsOver) return;
            IsLost = true;
            LoseReason = reason;
            Lost?.Invoke(reason);
        }

        /// <summary>
        /// Shots a lone shooter of this colour lands in one loop over <paramref name="cells"/> (same rules as
        /// <see cref="Step"/>). With apply = false the grid is left untouched.
        /// </summary>
        public static int LoopHits(int[] cells, BeltGeometry geometry, int color, int ammo, bool apply)
        {
            var grid = apply ? cells : (int[])cells.Clone();
            int hits = 0;
            foreach (var stop in geometry.Stops)
            {
                if (hits >= ammo) break;
                if (!FindFirst(grid, geometry.Width, geometry.Height, stop, out int x, out int y)) continue;
                int i = x + y * geometry.Width;
                if (grid[i] != color) continue;
                grid[i] = -1;
                hits++;
            }
            return hits;
        }
    }
}
