# com.hyperframe.input

- **Gestures (IN-01)**: `GestureRecognizer` turns pointer samples into Tap, DoubleTap, LongPress, Hold, Swipe, Drag, Pinch. Plain C#, fully unit-tested.
- **Device driver**: `InputDriver` reads the Unity Input System (touch, mouse in the Editor, Escape = Android back). Presses that start over UI are not sent to gameplay.
- **2D routing (IN-02)**: `InputRouter2D` delivers taps to `ITappable` and drags to `IDraggable` / `IDropTarget` via Physics2D (topmost sorting order wins; invalid drops return `target == null`).
- **Input lock (IN-03)**: `InputLock.Acquire(reason)` is reference-counted and lists its holders (soft-lock debugging). Popups hold it automatically.
- **Thresholds (IN-04)**: `GestureSettings` (dp units, DPI-scaled) in a `GestureSettingsAsset`, referenced from `GameDefinition.gestures`.
- **Injection (IN-06)**: `IInputService.InjectTap / InjectDrag / InjectPointer* / InjectBack` go through the same recognizer and lock as a real finger, so bots and tests exercise real input paths.
