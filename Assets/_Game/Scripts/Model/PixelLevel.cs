using System;
using System.Collections.Generic;
using HyperFrame.Services;
using Newtonsoft.Json;

namespace Game
{
    /// <summary>One shooter in the queue: a palette index and how many shots it carries.</summary>
    [Serializable]
    public struct ShooterSpec
    {
        public int color;
        public int ammo;

        public ShooterSpec(int color, int ammo)
        {
            this.color = color;
            this.ammo = ammo;
        }

        public override string ToString() => $"c{color}x{ammo}";
    }

    /// <summary>
    /// A Pixel Loop level (JSON in Assets/_Game/Levels): a pixel-art picture plus the rules knobs.
    /// Plain C# with no engine types so the rules and level checks run anywhere.
    /// <code>
    /// { "id": "pl_001", "name": "Heart", "colors": ["#E84855", "#FFB3BA"],
    ///   "rows": [".00.00.", "0100000", ...],     // top row first; '.' = empty, '0'-'9' / 'a'-'z' = palette index
    ///   "queueColumns": 2, "beltCapacity": 4, "traySlots": 5, "maxAmmo": 20, "loopSeconds": 4.5 }
    /// </code>
    /// When "shooters" is empty the queue is planned from the picture (<see cref="ShooterPlanner"/>), which
    /// guarantees the level can be cleared by sending shooters one at a time in queue order.
    /// </summary>
    [Serializable]
    public sealed class PixelLevel : ILevelData
    {
        public string id = "";
        public string name = "";
        public string[] colors = Array.Empty<string>();
        public string[] rows = Array.Empty<string>();
        /// <summary>Queue columns at the bottom; only the front shooter of each column can be sent.</summary>
        public int queueColumns = 3;
        /// <summary>Shooters allowed on the belt at once.</summary>
        public int beltCapacity = 5;
        /// <summary>Waiting slots for shooters that finish a loop with ammo left. A full tray loses the level.</summary>
        public int traySlots = 5;
        /// <summary>Ammo cap when planning shooters.</summary>
        public int maxAmmo = 20;
        /// <summary>Seconds for one full loop of the belt.</summary>
        public float loopSeconds = 4.5f;
        /// <summary>Planner variety (which colour goes next among good options).</summary>
        public int seed = 1;
        /// <summary>Explicit queue, in order. Empty = planned from the picture.</summary>
        public List<ShooterSpec> shooters = new List<ShooterSpec>();

        public string Id => id;

        [JsonIgnore] public int Width { get; private set; }
        [JsonIgnore] public int Height { get; private set; }

        int[] _cells; // x + y * Width, y = 0 is the bottom row; -1 = empty

        public static PixelLevel FromJson(string json)
        {
            var level = JsonConvert.DeserializeObject<PixelLevel>(json) ?? throw new FormatException("Empty level JSON.");
            level.Build();
            return level;
        }

        /// <summary>Parses rows into the grid and fills the queue when none is given. Called by FromJson.</summary>
        public PixelLevel Build()
        {
            if (rows == null || rows.Length == 0) throw new FormatException($"Level '{id}' has no rows.");
            Height = rows.Length;
            Width = 0;
            foreach (var row in rows) Width = Math.Max(Width, row.Length);
            _cells = new int[Width * Height];
            for (int r = 0; r < Height; r++)
            {
                int y = Height - 1 - r;
                for (int x = 0; x < Width; x++)
                {
                    char c = x < rows[r].Length ? rows[r][x] : '.';
                    int index = ColorIndex(c);
                    if (index >= colors.Length)
                        throw new FormatException($"Level '{id}' row {r} uses colour '{c}' but has {colors.Length} colours.");
                    _cells[x + y * Width] = index;
                }
            }
            queueColumns = Math.Max(1, queueColumns);
            beltCapacity = Math.Max(1, beltCapacity);
            traySlots = Math.Max(1, traySlots);
            maxAmmo = Math.Max(1, maxAmmo);
            loopSeconds = Math.Max(1f, loopSeconds);
            if (shooters == null || shooters.Count == 0) shooters = ShooterPlanner.Plan(this);
            return this;
        }

        static int ColorIndex(char c)
        {
            if (c == '.' || c == ' ') return -1;
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'z') return 10 + c - 'a';
            throw new FormatException($"Unknown pixel character '{c}'.");
        }

        /// <summary>Palette index at (x, y) with y = 0 at the bottom, or -1.</summary>
        public int Cell(int x, int y) => _cells[x + y * Width];

        /// <summary>A copy of the grid for a simulation.</summary>
        public int[] CopyCells() => (int[])_cells.Clone();

        public int PixelCount
        {
            get
            {
                int n = 0;
                foreach (var c in _cells) if (c >= 0) n++;
                return n;
            }
        }

        public int CountColor(int color)
        {
            int n = 0;
            foreach (var c in _cells) if (c == color) n++;
            return n;
        }

        /// <summary>Queue column i holds shooters i, i + columns, i + 2·columns… (front first).</summary>
        public List<List<int>> QueueColumns()
        {
            var columns = new List<List<int>>();
            for (int c = 0; c < queueColumns; c++) columns.Add(new List<int>());
            for (int i = 0; i < shooters.Count; i++) columns[i % queueColumns].Add(i);
            return columns;
        }
    }
}
