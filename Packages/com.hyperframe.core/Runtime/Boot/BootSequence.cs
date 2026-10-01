using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HyperFrame.Core
{
    public enum BootStepStatus { Succeeded, Failed, TimedOut, Skipped }

    public readonly struct BootStepResult
    {
        public readonly string Name;
        public readonly BootStepStatus Status;
        public readonly bool Critical;
        public readonly double DurationMs;
        public readonly Exception Error;

        public BootStepResult(string name, BootStepStatus status, bool critical, double durationMs, Exception error)
        {
            Name = name;
            Status = status;
            Critical = critical;
            DurationMs = durationMs;
            Error = error;
        }

        public override string ToString() =>
            $"{Name}: {Status} ({DurationMs:0} ms){(Error != null ? " — " + Error.Message : "")}";
    }

    public sealed class BootReport
    {
        public readonly List<BootStepResult> Steps = new List<BootStepResult>();

        /// <summary>False when a critical step failed or timed out.</summary>
        public bool Success { get; internal set; } = true;
        public double TotalMs { get; internal set; }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Boot {(Success ? "succeeded" : "FAILED")} in {TotalMs:0} ms");
            foreach (var s in Steps) sb.AppendLine("  " + s);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Runs initialization steps in declared order (CORE-01). Each step has a timeout. A failing
    /// non-critical step is reported and boot continues (e.g. analytics offline); a failing critical
    /// step stops the sequence and the remaining steps are reported as Skipped.
    /// </summary>
    public sealed class BootSequence
    {
        struct Entry
        {
            public string Name;
            public Func<CancellationToken, Task> Run;
            public TimeSpan Timeout;
            public bool Critical;
        }

        readonly List<Entry> _steps = new List<Entry>();
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        /// <summary>Adds an async step.</summary>
        public BootSequence Add(string name, Func<CancellationToken, Task> run, bool critical = true, TimeSpan? timeout = null)
        {
            _steps.Add(new Entry
            {
                Name = name,
                Run = run ?? throw new ArgumentNullException(nameof(run)),
                Timeout = timeout ?? DefaultTimeout,
                Critical = critical
            });
            return this;
        }

        /// <summary>Adds a synchronous step.</summary>
        public BootSequence Add(string name, Action run, bool critical = true)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            return Add(name, _ => { run(); return Task.CompletedTask; }, critical);
        }

        public int Count => _steps.Count;

        /// <summary>Called after each step, e.g. to drive a loading bar: (index, count, result).</summary>
        public event Action<int, int, BootStepResult> StepCompleted;

        public async Task<BootReport> RunAsync(CancellationToken cancellation = default)
        {
            var report = new BootReport();
            var total = Stopwatch.StartNew();
            bool aborted = false;

            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                BootStepResult result;
                if (aborted || cancellation.IsCancellationRequested)
                {
                    result = new BootStepResult(step.Name, BootStepStatus.Skipped, step.Critical, 0, null);
                }
                else
                {
                    result = await RunStep(step, cancellation);
                    if (result.Status != BootStepStatus.Succeeded)
                    {
                        if (step.Critical)
                        {
                            aborted = true;
                            report.Success = false;
                            HFLog.Error("Boot", $"Critical step failed: {result}");
                        }
                        else
                        {
                            HFLog.Warn("Boot", $"Optional step failed, continuing: {result}");
                        }
                    }
                }
                report.Steps.Add(result);
                StepCompleted?.Invoke(i, _steps.Count, result);
            }

            if (cancellation.IsCancellationRequested) report.Success = false;
            report.TotalMs = total.Elapsed.TotalMilliseconds;
            HFLog.Info("Boot", report.ToString());
            return report;
        }

        static async Task<BootStepResult> RunStep(Entry step, CancellationToken outer)
        {
            var sw = Stopwatch.StartNew();
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(outer))
            {
                Task work;
                try { work = step.Run(cts.Token) ?? Task.CompletedTask; }
                catch (Exception e)
                {
                    return new BootStepResult(step.Name, BootStepStatus.Failed, step.Critical, sw.Elapsed.TotalMilliseconds, e);
                }

                var timeout = Task.Delay(step.Timeout, cts.Token);
                var finished = await Task.WhenAny(work, timeout);
                if (finished != work)
                {
                    cts.Cancel(); // ask the step to stop
                    return new BootStepResult(step.Name, BootStepStatus.TimedOut, step.Critical, sw.Elapsed.TotalMilliseconds,
                        new TimeoutException($"{step.Name} exceeded {step.Timeout.TotalSeconds:0.#}s"));
                }
                cts.Cancel(); // stop the timeout delay

                try
                {
                    await work;
                    return new BootStepResult(step.Name, BootStepStatus.Succeeded, step.Critical, sw.Elapsed.TotalMilliseconds, null);
                }
                catch (Exception e)
                {
                    return new BootStepResult(step.Name, BootStepStatus.Failed, step.Critical, sw.Elapsed.TotalMilliseconds, e);
                }
            }
        }
    }
}
