using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HyperFrame.Services
{
    /// <summary>Difficulty knobs passed to a generator. Kits read the keys they understand.</summary>
    [Serializable]
    public sealed class DifficultyParams
    {
        public float difficulty;               // 0..1
        public Dictionary<string, float> values = new Dictionary<string, float>();

        public float Get(string key, float fallback) => values != null && values.TryGetValue(key, out var v) ? v : fallback;
        public DifficultyParams Set(string key, float value) { values[key] = value; return this; }
    }

    /// <summary>Kit-specific procedural generator (LV-02).</summary>
    public interface ILevelGenerator<TLevel>
    {
        TLevel Generate(int seed, DifficultyParams parameters);
    }

    public sealed class SolveResult<TMove>
    {
        public bool Solvable;
        /// <summary>Shortest solution found (used for hints and bots, TS-02).</summary>
        public List<TMove> Moves = new List<TMove>();
        public int MinMoves => Solvable ? Moves.Count : -1;
        /// <summary>Kit-defined difficulty estimate (e.g. explored nodes, branching).</summary>
        public float DifficultyScore;
        public int NodesExplored;
        /// <summary>True when the search hit its budget without an answer (treated as unsolvable).</summary>
        public bool BudgetExceeded;
    }

    /// <summary>Kit-specific solver/validator (LV-03).</summary>
    public interface ILevelSolver<TLevel, TMove>
    {
        SolveResult<TMove> Solve(TLevel level, int maxNodes);
    }

    public sealed class LevelReportEntry
    {
        public int Index;
        public int Seed;
        public float Difficulty;
        public bool Solvable;
        public int MinMoves;
        public float DifficultyScore;
        public int NodesExplored;
        public string Error;
    }

    public sealed class LevelBatchReport
    {
        public readonly List<LevelReportEntry> Entries = new List<LevelReportEntry>();
        public int Count => Entries.Count;
        public int SolvableCount => Entries.FindAll(e => e.Solvable).Count;
        public bool AllSolvable => SolvableCount == Count;

        /// <summary>Average min-moves per block of blockSize levels (PRD Game #1 acceptance check).</summary>
        public List<double> AverageMovesPerBlock(int blockSize)
        {
            var result = new List<double>();
            for (int start = 0; start < Entries.Count; start += blockSize)
            {
                double sum = 0; int n = 0;
                for (int i = start; i < Math.Min(start + blockSize, Entries.Count); i++)
                {
                    if (!Entries[i].Solvable) continue;
                    sum += Entries[i].MinMoves;
                    n++;
                }
                result.Add(n > 0 ? sum / n : 0);
            }
            return result;
        }

        public string ToCsv()
        {
            var sb = new StringBuilder("index,seed,difficulty,solvable,min_moves,difficulty_score,nodes,error\n");
            foreach (var e in Entries)
                sb.AppendLine(string.Join(",", e.Index, e.Seed, e.Difficulty.ToString("0.###", CultureInfo.InvariantCulture),
                    e.Solvable ? 1 : 0, e.MinMoves, e.DifficultyScore.ToString("0.###", CultureInfo.InvariantCulture),
                    e.NodesExplored, Escape(e.Error)));
            return sb.ToString();
        }

        public string ToMarkdownSummary(int blockSize = 10)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Level batch report");
            sb.AppendLine();
            sb.AppendLine($"- Levels: {Count}");
            sb.AppendLine($"- Solvable: {SolvableCount}/{Count} {(AllSolvable ? "✅" : "❌")}");
            var blocks = AverageMovesPerBlock(blockSize);
            sb.AppendLine($"- Avg min moves per {blockSize}-level block: {string.Join(", ", blocks.ConvertAll(b => b.ToString("0.0", CultureInfo.InvariantCulture)))}");
            var failed = Entries.FindAll(e => !e.Solvable);
            if (failed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Unsolvable");
                foreach (var e in failed) sb.AppendLine($"- #{e.Index} seed {e.Seed}{(e.Error != null ? ": " + e.Error : "")}");
            }
            return sb.ToString();
        }

        static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "'") + "\"";
    }

    /// <summary>
    /// Batch "generate N → validate → report" (LV-07). Kits plug in their generator and solver;
    /// the editor/CLI command writes the report next to the levels.
    /// </summary>
    public static class LevelBatchValidator
    {
        public static LevelBatchReport Run<TLevel, TMove>(
            ILevelGenerator<TLevel> generator,
            ILevelSolver<TLevel, TMove> solver,
            int count,
            Func<int, DifficultyParams> difficultyForIndex,
            int baseSeed = 1000,
            int maxNodes = 200000,
            Action<int, TLevel, SolveResult<TMove>> onLevel = null)
        {
            var report = new LevelBatchReport();
            for (int i = 0; i < count; i++)
            {
                var p = difficultyForIndex(i);
                int seed = baseSeed + i;
                var entry = new LevelReportEntry { Index = i, Seed = seed, Difficulty = p.difficulty };
                try
                {
                    var level = generator.Generate(seed, p);
                    var result = solver.Solve(level, maxNodes);
                    entry.Solvable = result.Solvable;
                    entry.MinMoves = result.MinMoves;
                    entry.DifficultyScore = result.DifficultyScore;
                    entry.NodesExplored = result.NodesExplored;
                    if (result.BudgetExceeded) entry.Error = "solver budget exceeded";
                    onLevel?.Invoke(i, level, result);
                }
                catch (Exception e)
                {
                    entry.Error = e.Message;
                }
                report.Entries.Add(entry);
            }
            return report;
        }
    }
}
