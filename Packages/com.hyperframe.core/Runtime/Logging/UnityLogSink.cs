#if UNITY_5_3_OR_NEWER
using System;

namespace HyperFrame.Core
{
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(LogLevel level, string tag, string message)
        {
            var line = string.IsNullOrEmpty(tag) ? message : $"[{tag}] {message}";
            switch (level)
            {
                case LogLevel.Warning: UnityEngine.Debug.LogWarning(line); break;
                case LogLevel.Error: UnityEngine.Debug.LogError(line); break;
                default: UnityEngine.Debug.Log(line); break;
            }
        }

        public void WriteException(Exception exception, string tag)
        {
            if (!string.IsNullOrEmpty(tag)) UnityEngine.Debug.LogError($"[{tag}] {exception.Message}");
            UnityEngine.Debug.LogException(exception);
        }
    }
}
#endif
