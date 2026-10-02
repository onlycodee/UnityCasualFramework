using UnityEngine;

namespace HyperFrame.Input
{
    public enum GesturePhase { Began, Moved, Ended, Cancelled }
    public enum SwipeDirection { Left, Right, Up, Down }

    public struct TapGesture { public int PointerId; public Vector2 Position; public float Time; }
    public struct DoubleTapGesture { public int PointerId; public Vector2 Position; }
    public struct LongPressGesture { public int PointerId; public Vector2 Position; }

    /// <summary>Began when a press is held still past the long-press time; Ended on release.</summary>
    public struct HoldGesture { public int PointerId; public GesturePhase Phase; public Vector2 Position; public float Duration; }

    public struct SwipeGesture
    {
        public int PointerId;
        public Vector2 Start, End;
        public SwipeDirection Direction;
        /// <summary>Pixels per second.</summary>
        public float Speed;
    }

    public struct DragGesture
    {
        public int PointerId;
        public GesturePhase Phase;
        public Vector2 Start, Position, Delta;
    }

    public struct PinchGesture
    {
        public GesturePhase Phase;
        public Vector2 Center;
        /// <summary>Current distance / distance at start (1 = unchanged).</summary>
        public float Scale;
        /// <summary>Scale change since the previous Moved event.</summary>
        public float ScaleDelta;
    }
}
