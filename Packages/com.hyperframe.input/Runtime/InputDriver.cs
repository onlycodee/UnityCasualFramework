using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HyperFrame.Input
{
    /// <summary>
    /// Reads pointers from the Unity Input System (touch first, mouse in the Editor) and feeds
    /// <see cref="InputService"/>. Presses that start over UI are not sent to gameplay.
    /// Escape (Android back) raises BackPressed.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class InputDriver : MonoBehaviour
    {
        InputService _service;
        readonly System.Collections.Generic.HashSet<int> _tracked = new System.Collections.Generic.HashSet<int>();

        public static InputDriver Create(InputService service, Transform parent)
        {
            var go = new GameObject("InputDriver");
            go.transform.SetParent(parent, false);
            var driver = go.AddComponent<InputDriver>();
            driver._service = service;
            return driver;
        }

        void Update()
        {
            if (_service == null) return;
#if ENABLE_INPUT_SYSTEM
            var touchscreen = Touchscreen.current;
            bool anyTouch = false;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    int id = touch.touchId.ReadValue();
                    var phase = touch.phase.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
                    anyTouch = true;
                    Feed(id, touch.position.ReadValue(),
                        touch.press.wasPressedThisFrame,
                        touch.press.wasReleasedThisFrame || phase == UnityEngine.InputSystem.TouchPhase.Canceled);
                }
            }
            var mouse = Mouse.current;
            if (!anyTouch && mouse != null)
            {
                Feed(-1, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame);
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _service.OnBack();
#elif ENABLE_LEGACY_INPUT_MANAGER
            Feed(-1, UnityEngine.Input.mousePosition, UnityEngine.Input.GetMouseButtonDown(0), UnityEngine.Input.GetMouseButtonUp(0));
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) _service.OnBack();
#endif
            _service.Update();
        }

        void Feed(int id, Vector2 position, bool pressed, bool released)
        {
            if (pressed)
            {
                if (IsOverUI(id)) return;
                _tracked.Add(id);
                _service.OnPointerDown(id, position);
            }
            else if (_tracked.Contains(id))
            {
                if (released)
                {
                    _tracked.Remove(id);
                    _service.OnPointerUp(id, position);
                }
                else
                {
                    _service.OnPointerMove(id, position);
                }
            }
        }

        static bool IsOverUI(int pointerId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return pointerId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(pointerId);
        }
    }
}
