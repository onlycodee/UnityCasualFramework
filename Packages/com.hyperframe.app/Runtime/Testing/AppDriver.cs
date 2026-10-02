using System;
using System.Collections;
using HyperFrame.Core;
using HyperFrame.Input;
using HyperFrame.UI;
using UnityEngine;

namespace HyperFrame.App.Testing
{
    /// <summary>
    /// Helpers to drive the running app from PlayMode tests, bots, or the agent (via MCP execute):
    /// wait for flow states, press buttons by name, tap world objects through input injection.
    /// All waits are coroutines usable inside [UnityTest].
    /// </summary>
    public static class AppDriver
    {
        public static HyperFrameApp App => HyperFrameApp.Instance;
        public static GameFlow Flow => App != null ? App.Flow : null;
        public static IUIService UI => ServiceLocator.Get<IUIService>();

        /// <summary>Waits until predicate is true or fails the wait after timeoutSeconds (real time).</summary>
        public static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds, string what, Action<string> onTimeout = null)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    var message = $"Timed out after {timeoutSeconds}s waiting for {what}. {Describe()}";
                    if (onTimeout != null) onTimeout(message);
                    else throw new TimeoutException(message);
                    yield break;
                }
                yield return null;
            }
        }

        public static IEnumerator WaitForReady(float timeout = 10f) =>
            WaitUntil(() => App != null && (App.IsReady || App.BootFailed), timeout, "app boot");

        public static IEnumerator WaitForState(GameFlowState state, float timeout = 10f) =>
            WaitUntil(() => Flow != null && Flow.State == state, timeout, $"flow state {state}");

        /// <summary>Waits until a view of type T is visible and its show transition has finished.</summary>
        public static IEnumerator WaitForView<T>(float timeout = 10f) where T : UIView =>
            WaitUntil(() => IsInteractable<T>(), timeout, $"{typeof(T).Name} visible");

        public static bool IsInteractable<T>() where T : UIView
        {
            if (!ServiceLocator.TryGet<IUIService>(out var ui)) return false;
            var view = ui.GetView<T>();
            return view.IsVisible && view.Group != null && view.Group.interactable;
        }

        /// <summary>Waits for view T to be interactable, then presses a button on it.</summary>
        public static IEnumerator Click<T>(string buttonName, float timeout = 10f) where T : UIView
        {
            yield return WaitForView<T>(timeout);
            if (!UI.GetView<T>().Click(buttonName))
                throw new InvalidOperationException($"Could not click {typeof(T).Name}.{buttonName}. {Describe()}");
        }

        /// <summary>Taps a world position through the input service (respects the input lock like a finger would).</summary>
        public static void TapWorld(Vector3 worldPosition, Camera camera = null)
        {
            camera = camera != null ? camera : Camera.main;
            var screen = camera.WorldToScreenPoint(worldPosition);
            ServiceLocator.Get<IInputService>().InjectTap(new Vector2(screen.x, screen.y));
        }

        /// <summary>One-line snapshot of app state for failure messages and agent reports.</summary>
        public static string Describe()
        {
            if (App == null) return "App: not running.";
            var sb = new System.Text.StringBuilder();
            sb.Append($"State={Flow?.State}, ready={App.IsReady}, bootFailed={App.BootFailed}");
            if (ServiceLocator.TryGet<IUIService>(out var ui))
                sb.Append($", screen={ui.CurrentScreen?.GetType().Name}, popup={ui.TopPopup?.GetType().Name}, queued={ui.QueuedPopupCount}, loading={ui.IsLoading}");
            if (ServiceLocator.TryGet<IInputService>(out var input))
                sb.Append($", inputLocked={input.Lock.IsLocked} [{string.Join(",", input.Lock.Reasons)}]");
            if (ServiceLocator.TryGet<IGameClock>(out var clock)) sb.Append($", paused={clock.IsPaused}");
            return sb.ToString();
        }
    }
}
