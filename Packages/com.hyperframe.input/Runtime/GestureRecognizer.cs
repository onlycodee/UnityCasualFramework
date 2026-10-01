using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperFrame.Input
{
    /// <summary>
    /// Turns raw pointer samples into gestures (IN-01). Plain C#: fed by <see cref="InputService"/> from
    /// real devices or from injected input, so tests and bots go through exactly the same logic.
    /// One primary pointer drives tap/drag/swipe/long-press; a second pointer switches to pinch.
    /// Taps always fire; a second quick tap additionally fires DoubleTap.
    /// </summary>
    public sealed class GestureRecognizer
    {
        sealed class Pointer
        {
            public int Id;
            public Vector2 Start, Position;
            public float StartTime;
            public bool Dragging, LongPressed;
        }

        readonly Dictionary<int, Pointer> _pointers = new Dictionary<int, Pointer>();
        GestureSettings _settings;
        float _pxPerDp;
        int _primaryId = -1;
        bool _pinching;
        float _pinchStartDistance, _pinchLastScale;
        float _lastTapTime = float.NegativeInfinity;
        Vector2 _lastTapPos;

        public event Action<TapGesture> Tapped;
        public event Action<DoubleTapGesture> DoubleTapped;
        public event Action<LongPressGesture> LongPressed;
        public event Action<HoldGesture> Held;
        public event Action<SwipeGesture> Swiped;
        public event Action<DragGesture> Dragged;
        public event Action<PinchGesture> Pinched;

        public GestureRecognizer(GestureSettings settings = null, float dpi = 160f)
        {
            Configure(settings ?? new GestureSettings(), dpi);
        }

        public GestureSettings Settings => _settings;
        public int ActivePointerCount => _pointers.Count;
        public bool IsDragging => _primaryId >= 0 && _pointers.TryGetValue(_primaryId, out var p) && p.Dragging;

        public void Configure(GestureSettings settings, float dpi)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _pxPerDp = (dpi > 1f ? dpi : 160f) / 160f;
        }

        float Px(float dp) => dp * _pxPerDp;

        public void PointerDown(int id, Vector2 position, float time)
        {
            if (_pointers.ContainsKey(id)) PointerUp(id, position, time);
            var p = new Pointer { Id = id, Start = position, Position = position, StartTime = time };
            _pointers[id] = p;

            if (_pointers.Count == 1)
            {
                _primaryId = id;
                return;
            }
            if (_pointers.Count == 2 && !_pinching) BeginPinch();
        }

        public void PointerMove(int id, Vector2 position, float time)
        {
            if (!_pointers.TryGetValue(id, out var p)) return;
            var previous = p.Position;
            p.Position = position;

            if (_pinching)
            {
                UpdatePinch(GesturePhase.Moved);
                return;
            }
            if (id != _primaryId) return;

            if (!p.Dragging && !p.LongPressed && (position - p.Start).magnitude > Px(_settings.tapSlopDp))
            {
                p.Dragging = true;
                Dragged?.Invoke(new DragGesture { PointerId = id, Phase = GesturePhase.Began, Start = p.Start, Position = position, Delta = position - p.Start });
                return;
            }
            if (p.Dragging)
                Dragged?.Invoke(new DragGesture { PointerId = id, Phase = GesturePhase.Moved, Start = p.Start, Position = position, Delta = position - previous });
            else if (p.LongPressed && (position - p.Start).magnitude > Px(_settings.tapSlopDp))
            {
                // Holding then moving turns the hold into a drag.
                Held?.Invoke(new HoldGesture { PointerId = id, Phase = GesturePhase.Ended, Position = position, Duration = time - p.StartTime });
                p.LongPressed = false;
                p.Dragging = true;
                Dragged?.Invoke(new DragGesture { PointerId = id, Phase = GesturePhase.Began, Start = p.Start, Position = position, Delta = position - p.Start });
            }
        }

        public void PointerUp(int id, Vector2 position, float time)
        {
            if (!_pointers.TryGetValue(id, out var p)) return;
            p.Position = position;

            if (_pinching)
            {
                _pointers.Remove(id);
                if (_pointers.Count < 2)
                {
                    EndPinch(GesturePhase.Ended);
                    _primaryId = -1; // remaining finger does nothing until lifted
                }
                if (_pointers.Count == 0) _primaryId = -1;
                return;
            }

            _pointers.Remove(id);
            if (id != _primaryId) return;
            _primaryId = -1;

            float duration = time - p.StartTime;
            float distance = (position - p.Start).magnitude;

            if (p.Dragging)
            {
                Dragged?.Invoke(new DragGesture { PointerId = id, Phase = GesturePhase.Ended, Start = p.Start, Position = position, Delta = Vector2.zero });
            }
            if (p.LongPressed)
            {
                Held?.Invoke(new HoldGesture { PointerId = id, Phase = GesturePhase.Ended, Position = position, Duration = duration });
                return;
            }
            if (duration <= _settings.swipeMaxDuration && distance >= Px(_settings.swipeMinDistanceDp))
            {
                var d = position - p.Start;
                var dir = Mathf.Abs(d.x) > Mathf.Abs(d.y)
                    ? (d.x > 0 ? SwipeDirection.Right : SwipeDirection.Left)
                    : (d.y > 0 ? SwipeDirection.Up : SwipeDirection.Down);
                Swiped?.Invoke(new SwipeGesture { PointerId = id, Start = p.Start, End = position, Direction = dir, Speed = distance / Mathf.Max(duration, 0.0001f) });
                return;
            }
            if (!p.Dragging && duration <= _settings.tapMaxDuration && distance <= Px(_settings.tapSlopDp))
            {
                Tapped?.Invoke(new TapGesture { PointerId = id, Position = position, Time = time });
                if (time - _lastTapTime <= _settings.doubleTapMaxInterval &&
                    (position - _lastTapPos).magnitude <= Px(_settings.doubleTapMaxDistanceDp))
                {
                    DoubleTapped?.Invoke(new DoubleTapGesture { PointerId = id, Position = position });
                    _lastTapTime = float.NegativeInfinity; // a triple tap is not two double taps
                }
                else
                {
                    _lastTapTime = time;
                    _lastTapPos = position;
                }
            }
        }

        /// <summary>Call every frame; detects long presses for pointers that are not moving.</summary>
        public void Update(float time)
        {
            if (_pinching || _primaryId < 0 || !_pointers.TryGetValue(_primaryId, out var p)) return;
            if (p.Dragging || p.LongPressed) return;
            if (time - p.StartTime < _settings.longPressDuration) return;
            p.LongPressed = true;
            LongPressed?.Invoke(new LongPressGesture { PointerId = p.Id, Position = p.Position });
            Held?.Invoke(new HoldGesture { PointerId = p.Id, Phase = GesturePhase.Began, Position = p.Position, Duration = time - p.StartTime });
        }

        /// <summary>Cancels everything in progress (e.g. when input gets locked). Drags/holds receive Cancelled.</summary>
        public void Cancel()
        {
            if (_pinching) EndPinch(GesturePhase.Cancelled);
            if (_primaryId >= 0 && _pointers.TryGetValue(_primaryId, out var p))
            {
                if (p.Dragging)
                    Dragged?.Invoke(new DragGesture { PointerId = p.Id, Phase = GesturePhase.Cancelled, Start = p.Start, Position = p.Position });
                if (p.LongPressed)
                    Held?.Invoke(new HoldGesture { PointerId = p.Id, Phase = GesturePhase.Cancelled, Position = p.Position });
            }
            _pointers.Clear();
            _primaryId = -1;
        }

        void BeginPinch()
        {
            if (_primaryId >= 0 && _pointers.TryGetValue(_primaryId, out var p))
            {
                if (p.Dragging)
                    Dragged?.Invoke(new DragGesture { PointerId = p.Id, Phase = GesturePhase.Cancelled, Start = p.Start, Position = p.Position });
                if (p.LongPressed)
                    Held?.Invoke(new HoldGesture { PointerId = p.Id, Phase = GesturePhase.Cancelled, Position = p.Position });
                p.Dragging = p.LongPressed = false;
            }
            _pinching = true;
            GetTwo(out var a, out var b);
            _pinchStartDistance = Mathf.Max((a.Position - b.Position).magnitude, 1f);
            _pinchLastScale = 1f;
            Pinched?.Invoke(new PinchGesture { Phase = GesturePhase.Began, Center = (a.Position + b.Position) * 0.5f, Scale = 1f, ScaleDelta = 0f });
        }

        void UpdatePinch(GesturePhase phase)
        {
            GetTwo(out var a, out var b);
            float scale = (a.Position - b.Position).magnitude / _pinchStartDistance;
            Pinched?.Invoke(new PinchGesture { Phase = phase, Center = (a.Position + b.Position) * 0.5f, Scale = scale, ScaleDelta = scale - _pinchLastScale });
            _pinchLastScale = scale;
        }

        void EndPinch(GesturePhase phase)
        {
            _pinching = false;
            Pinched?.Invoke(new PinchGesture { Phase = phase, Scale = _pinchLastScale, ScaleDelta = 0f });
        }

        void GetTwo(out Pointer a, out Pointer b)
        {
            a = b = null;
            foreach (var p in _pointers.Values)
            {
                if (a == null) a = p;
                else { b = p; break; }
            }
            if (b == null) b = a;
        }
    }
}
