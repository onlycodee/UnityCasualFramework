using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HyperFrame.Core
{
    /// <summary>
    /// Registry of cheat/debug commands shown by the debug console (DV-01, DV-02).
    /// Register calls are compiled out of release builds together with their lambdas, so games can
    /// register cheats anywhere without #if blocks.
    /// </summary>
    public static class DebugCommands
    {
        public sealed class Command
        {
            public string Category;
            public string Name;
            public Action Run;
            public Func<string> Label; // optional dynamic label (e.g. "Ads: ON")
        }

        static readonly List<Command> Items = new List<Command>();
        public static IReadOnlyList<Command> All => Items;
        public static event Action Changed;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("HF_CHEATS")]
        public static void Register(string category, string name, Action run, Func<string> label = null)
        {
            Items.RemoveAll(c => c.Category == category && c.Name == name);
            Items.Add(new Command { Category = category, Name = name, Run = run, Label = label });
            Changed?.Invoke();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("HF_CHEATS")]
        public static void Clear()
        {
            Items.Clear();
            Changed?.Invoke();
        }

        /// <summary>Runs a command by "Category/Name". Used by tests and the agent via MCP.</summary>
        public static bool Execute(string path)
        {
            foreach (var c in Items)
            {
                if ($"{c.Category}/{c.Name}" != path) continue;
                c.Run?.Invoke();
                return true;
            }
            return false;
        }
    }
}
