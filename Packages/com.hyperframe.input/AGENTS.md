# AGENTS.md — com.hyperframe.input

## Making something tappable or draggable
```csharp
public sealed class Tube : MonoBehaviour, ITappable          // needs a Collider2D
{
    public void OnTap(in PointerContext ctx) { /* ctx.WorldPosition */ }
}
public sealed class Piece : MonoBehaviour, IDraggable
{
    public bool CanDrag => true;
    public void OnDragStart(in PointerContext c) { }
    public void OnDrag(in PointerContext c) => transform.position = c.WorldPosition;
    public void OnDragEnd(in PointerContext c, IDropTarget target) { if (target == null) ReturnHome(); }
}
```
The flow creates an `InputRouter2D` for every level; gameplay gets it from `GameplayContext.Router`.

## Locking input during animations
```csharp
using (Context.Input.Lock.Acquire("ball_move")) { await MoveBall(); }   // always released, even on exceptions
```

## Driving input from tests / bots
```csharp
var input = ServiceLocator.Get<IInputService>();
input.InjectTap(screenPos);              // or AppDriver.TapWorld(worldPos)
input.InjectDrag(from, to);
input.InjectBack();
```

## Rules / pitfalls
- Never read `Touchscreen`/`Mouse`/`UnityEngine.Input` in game code; subscribe to `IInputService.Gestures` or use the router.
- A lock that is never released soft-locks the game. `AppDriver.Describe()` and the debug console list current holders.
- Taps always fire, even when they form a double tap. Handle `DoubleTapped` only if the game needs it.
