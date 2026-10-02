using System;

namespace Game
{
    public enum BeltSide { Bottom, Right, Top, Left }

    /// <summary>A point on the belt: position (board-centred world units) and the inward direction.</summary>
    public struct BeltPoint
    {
        public float X, Y;
        /// <summary>Unit vector pointing from the belt towards the board.</summary>
        public float NX, NY;
    }

    /// <summary>A place on the belt where a shooter lines up with a row or column of the picture.</summary>
    public struct BeltStop
    {
        public float Distance;
        public BeltSide Side;
        /// <summary>Column (Bottom/Top) or row (Left/Right).</summary>
        public int Lane;
    }

    /// <summary>
    /// Shape of the conveyor: a rounded rectangle around the picture, travelled anticlockwise from the
    /// start of the bottom edge (the entry, bottom-left). The picture is scaled to fit
    /// <see cref="BoardSize"/> so every level has the same footprint on screen.
    /// </summary>
    public sealed class BeltGeometry
    {
        public const float BoardSize = 5f;
        public const float Margin = 0.62f;
        public const float CornerRadius = 0.5f;
        /// <summary>Minimum distance between two shooters on the belt.</summary>
        public const float Spacing = 0.9f;

        public readonly int Width, Height;
        public readonly float Cell;
        public readonly float BoardWidth, BoardHeight;
        public readonly float Length;
        public readonly BeltStop[] Stops;

        readonly float _left, _right, _bottom, _top, _straightX, _straightY, _arc;

        public BeltGeometry(int width, int height)
        {
            Width = width;
            Height = height;
            Cell = BoardSize / Math.Max(width, height);
            BoardWidth = Cell * width;
            BoardHeight = Cell * height;
            _left = -BoardWidth / 2f - Margin;
            _right = BoardWidth / 2f + Margin;
            _bottom = -BoardHeight / 2f - Margin;
            _top = BoardHeight / 2f + Margin;
            _straightX = _right - _left - 2f * CornerRadius;
            _straightY = _top - _bottom - 2f * CornerRadius;
            _arc = (float)(Math.PI * CornerRadius / 2.0);
            Length = 2f * _straightX + 2f * _straightY + 4f * _arc;

            Stops = new BeltStop[2 * width + 2 * height];
            int k = 0;
            float lead = Margin - CornerRadius; // straight part before the first lane
            float sideRight = _straightX + _arc;
            float sideTop = sideRight + _straightY + _arc;
            float sideLeft = sideTop + _straightX + _arc;
            for (int x = 0; x < width; x++) Stops[k++] = Stop(lead + (x + 0.5f) * Cell, BeltSide.Bottom, x);
            for (int y = 0; y < height; y++) Stops[k++] = Stop(sideRight + lead + (y + 0.5f) * Cell, BeltSide.Right, y);
            for (int x = width - 1; x >= 0; x--) Stops[k++] = Stop(sideTop + lead + (width - 1 - x + 0.5f) * Cell, BeltSide.Top, x);
            for (int y = height - 1; y >= 0; y--) Stops[k++] = Stop(sideLeft + lead + (height - 1 - y + 0.5f) * Cell, BeltSide.Left, y);
        }

        static BeltStop Stop(float d, BeltSide side, int lane) => new BeltStop { Distance = d, Side = side, Lane = lane };

        public float Left => _left;
        public float Right => _right;
        public float Bottom => _bottom;
        public float Top => _top;

        /// <summary>Centre of pixel (x, y), y = 0 at the bottom.</summary>
        public void CellCenter(int x, int y, out float cx, out float cy)
        {
            cx = -BoardWidth / 2f + (x + 0.5f) * Cell;
            cy = -BoardHeight / 2f + (y + 0.5f) * Cell;
        }

        /// <summary>Position on the belt at a distance from the entry (wraps around).</summary>
        public BeltPoint At(float distance)
        {
            float d = distance % Length;
            if (d < 0f) d += Length;
            float r = CornerRadius;

            if (d < _straightX) return Point(_left + r + d, _bottom, 0f, 1f);
            d -= _straightX;
            if (d < _arc) return Corner(_right - r, _bottom + r, -90f, d);
            d -= _arc;
            if (d < _straightY) return Point(_right, _bottom + r + d, -1f, 0f);
            d -= _straightY;
            if (d < _arc) return Corner(_right - r, _top - r, 0f, d);
            d -= _arc;
            if (d < _straightX) return Point(_right - r - d, _top, 0f, -1f);
            d -= _straightX;
            if (d < _arc) return Corner(_left + r, _top - r, 90f, d);
            d -= _arc;
            if (d < _straightY) return Point(_left, _top - r - d, 1f, 0f);
            d -= _straightY;
            return Corner(_left + r, _bottom + r, 180f, Math.Min(d, _arc));
        }

        static BeltPoint Point(float x, float y, float nx, float ny) => new BeltPoint { X = x, Y = y, NX = nx, NY = ny };

        BeltPoint Corner(float cx, float cy, float startDegrees, float along)
        {
            double a = (startDegrees + along / _arc * 90.0) * Math.PI / 180.0;
            float ox = (float)Math.Cos(a), oy = (float)Math.Sin(a);
            return Point(cx + ox * CornerRadius, cy + oy * CornerRadius, -ox, -oy);
        }
    }
}
