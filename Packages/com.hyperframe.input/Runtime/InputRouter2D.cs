using System.Collections.Generic;
using UnityEngine;

namespace HyperFrame.Input
{
    public struct PointerContext
    {
        public int PointerId;
        public Vector2 ScreenPosition;
        public Vector3 WorldPosition;
    }

    /// <summary>Put on a GameObject with a Collider2D to receive taps.</summary>
    public interface ITappable
    {
        void OnTap(in PointerContext context);
    }

    /// <summary>Put on a GameObject with a Collider2D to make it draggable.</summary>
    public interface IDraggable
    {
        bool CanDrag { get; }
        void OnDragStart(in PointerContext context);
        void OnDrag(in PointerContext context);
        /// <summary>target is the accepting drop target, or null (return to origin).</summary>
        void OnDragEnd(in PointerContext context, IDropTarget target);
    }

    /// <summary>Put on a GameObject with a Collider2D to accept drops.</summary>
    public interface IDropTarget
    {
        bool CanAccept(IDraggable draggable);
        void OnDrop(IDraggable draggable, in PointerContext context);
    }

    /// <summary>
    /// Routes gestures to 2D objects (IN-02): Tap → ITappable, Drag → IDraggable / IDropTarget,
    /// using Physics2D point queries from the given camera. Respects the input lock via the service.
    /// </summary>
    public sealed class InputRouter2D : System.IDisposable
    {
        readonly IInputService _input;
        readonly System.Func<Camera> _camera;
        readonly List<Collider2D> _hits = new List<Collider2D>();
        readonly ContactFilter2D _filter;
        IDraggable _dragging;
        Component _draggingComponent;

        public InputRouter2D(IInputService input, System.Func<Camera> camera = null, int layerMask = Physics2D.DefaultRaycastLayers)
        {
            _input = input;
            _camera = camera ?? (() => Camera.main);
            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(layerMask);
            _input.Gestures.Tapped += OnTapped;
            _input.Gestures.Dragged += OnDragged;
        }

        public void Dispose()
        {
            _input.Gestures.Tapped -= OnTapped;
            _input.Gestures.Dragged -= OnDragged;
        }

        PointerContext Context(int id, Vector2 screen)
        {
            var cam = _camera();
            var world = cam != null ? cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z)) : (Vector3)screen;
            world.z = 0f;
            return new PointerContext { PointerId = id, ScreenPosition = screen, WorldPosition = world };
        }

        /// <summary>Topmost component of type T under a world point (highest sorting order wins).</summary>
        public T Pick<T>(Vector2 world, Component ignore = null) where T : class
        {
            _hits.Clear();
            Physics2D.OverlapPoint(world, _filter, _hits);
            T best = null;
            int bestOrder = int.MinValue;
            foreach (var hit in _hits)
            {
                if (ignore != null && hit.transform.IsChildOf(ignore.transform)) continue;
                var candidate = hit.GetComponentInParent<T>();
                if (candidate == null) continue;
                var renderer = hit.GetComponentInParent<SpriteRenderer>();
                int order = renderer != null ? renderer.sortingOrder : 0;
                if (order > bestOrder) { best = candidate; bestOrder = order; }
            }
            return best;
        }

        void OnTapped(TapGesture tap)
        {
            var ctx = Context(tap.PointerId, tap.Position);
            Pick<ITappable>(ctx.WorldPosition)?.OnTap(ctx);
        }

        void OnDragged(DragGesture drag)
        {
            var ctx = Context(drag.PointerId, drag.Position);
            switch (drag.Phase)
            {
                case GesturePhase.Began:
                    var startCtx = Context(drag.PointerId, drag.Start);
                    var draggable = Pick<IDraggable>(startCtx.WorldPosition);
                    if (draggable == null || !draggable.CanDrag) return;
                    _dragging = draggable;
                    _draggingComponent = draggable as Component;
                    _dragging.OnDragStart(ctx);
                    break;
                case GesturePhase.Moved:
                    _dragging?.OnDrag(ctx);
                    break;
                case GesturePhase.Ended:
                    if (_dragging == null) return;
                    var target = Pick<IDropTarget>(ctx.WorldPosition, _draggingComponent);
                    if (target != null && !target.CanAccept(_dragging)) target = null;
                    target?.OnDrop(_dragging, ctx);
                    _dragging.OnDragEnd(ctx, target);
                    _dragging = null;
                    _draggingComponent = null;
                    break;
                case GesturePhase.Cancelled:
                    _dragging?.OnDragEnd(ctx, null);
                    _dragging = null;
                    _draggingComponent = null;
                    break;
            }
        }
    }
}
