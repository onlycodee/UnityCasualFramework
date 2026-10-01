using System;
using System.Threading.Tasks;

namespace HyperFrame.Core
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Runs a task without awaiting it and logs any exception. Use instead of async void so errors
        /// are never silently lost.
        /// </summary>
        public static void Forget(this Task task, string tag = "Task")
        {
            if (task == null) return;
            if (task.IsCompleted)
            {
                if (task.IsFaulted) HFLog.Exception(task.Exception?.GetBaseException(), tag);
                return;
            }
            task.ContinueWith(t =>
            {
                if (t.IsFaulted) HFLog.Exception(t.Exception?.GetBaseException(), tag);
            }, TaskScheduler.Current);
        }
    }
}
