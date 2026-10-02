using System;
using System.Diagnostics;

namespace HyperFrame.Core
{
    public enum LogLevel { Verbose = 0, Debug = 1, Info = 2, Warning = 3, Error = 4, None = 5 }

    /// <summary>Where log lines go. The default writes to the Unity console.</summary>
    public interface ILogSink
    {
        void Write(LogLevel level, string tag, string message);
        void WriteException(Exception exception, string tag);
    }

    /// <summary>
    /// Framework logger (CORE-07). Verbose/Debug/Info calls are removed by the compiler in release
    /// player builds (no UNITY_EDITOR / DEVELOPMENT_BUILD / HF_VERBOSE_LOGS define), including the
    /// cost of building their message strings. Warn/Error/Exception are always kept.
    /// </summary>
    public static class HFLog
    {
        public static LogLevel MinLevel = LogLevel.Debug;

        static ILogSink _sink;
        public static ILogSink Sink
        {
            get => _sink ??= CreateDefaultSink();
            set => _sink = value;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("HF_VERBOSE_LOGS")]
        public static void Verbose(string tag, string message) => Write(LogLevel.Verbose, tag, message);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("HF_VERBOSE_LOGS")]
        public static void Debug(string tag, string message) => Write(LogLevel.Debug, tag, message);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("HF_VERBOSE_LOGS")]
        public static void Info(string tag, string message) => Write(LogLevel.Info, tag, message);

        public static void Warn(string tag, string message) => Write(LogLevel.Warning, tag, message);

        public static void Error(string tag, string message) => Write(LogLevel.Error, tag, message);

        public static void Exception(Exception exception, string tag = null)
        {
            if (MinLevel <= LogLevel.Error) Sink.WriteException(exception, tag);
        }

        static void Write(LogLevel level, string tag, string message)
        {
            if (level < MinLevel) return;
            Sink.Write(level, tag, message);
        }

        static ILogSink CreateDefaultSink()
        {
#if UNITY_5_3_OR_NEWER
            return new UnityLogSink();
#else
            return new ConsoleLogSink();
#endif
        }
    }

    /// <summary>Plain stdout sink, used outside Unity.</summary>
    public sealed class ConsoleLogSink : ILogSink
    {
        public void Write(LogLevel level, string tag, string message) =>
            Console.WriteLine($"[{level}] [{tag}] {message}");

        public void WriteException(Exception exception, string tag) =>
            Console.WriteLine($"[Exception] [{tag}] {exception}");
    }
}
