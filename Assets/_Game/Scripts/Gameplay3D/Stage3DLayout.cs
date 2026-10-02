using System;

namespace Game
{
    public enum StageTapTarget { None, Column, TraySlot }

    /// <summary>
    /// Where things stand on the 3D stage, engine-free so it can be unit tested. The stage lies on the
    /// ground plane: belt-space (x, y) from <see cref="BeltGeometry"/> maps to world (x, 0, y), the picture is
    /// at the far end, then the belt, the tray and the queue columns nearest the camera.
    /// </summary>
    public sealed class Stage3DLayout
    {
        public const float TrackWidth = 0.78f;
        public const float ColumnGap = 1.12f;
        public const float RowGap = 0.98f;
        public const float SlotGap = 0.98f;
        public const int VisibleRows = 3;
        /// <summary>Height of the plane taps are projected onto (about a shooter's middle).</summary>
        public const float TapHeight = 0.3f;

        public readonly BeltGeometry Geometry;
        public readonly int Columns, Slots;
        public readonly float TrayZ, QueueTopZ;

        public Stage3DLayout(BeltGeometry geometry, int columns, int slots)
        {
            Geometry = geometry;
            Columns = Math.Max(1, columns);
            Slots = Math.Max(0, slots);
            TrayZ = geometry.Bottom - TrackWidth / 2f - 1.0f;
            QueueTopZ = TrayZ - 1.25f;
        }

        public float SlotX(int slot) => (slot - (Slots - 1) / 2f) * SlotGap;
        public float ColumnX(int column) => (column - (Columns - 1) / 2f) * ColumnGap;

        /// <summary>Z of a queue row; rows past the visible ones stack on the last.</summary>
        public float RowZ(int row) => QueueTopZ - Math.Min(Math.Max(row, 0), VisibleRows - 1) * RowGap;

        /// <summary>Nearest edge of the queue area (towards the camera).</summary>
        public float QueueBottomZ => RowZ(VisibleRows - 1) - RowGap / 2f;

        /// <summary>Half the width of everything below the belt (tray or queue, whichever is wider).</summary>
        public float LowerHalfWidth => Math.Max(Slots * SlotGap, Columns * ColumnGap) / 2f;

        /// <summary>What a tap at stage point (x, z) on the tap plane would press.</summary>
        public StageTapTarget HitTest(float x, float z, out int index)
        {
            index = -1;
            if (Slots > 0 && Math.Abs(z - TrayZ) <= 0.62f)
            {
                int slot = (int)Math.Round(x / SlotGap + (Slots - 1) / 2f);
                if (slot >= 0 && slot < Slots && Math.Abs(x - SlotX(slot)) <= SlotGap / 2f)
                {
                    index = slot;
                    return StageTapTarget.TraySlot;
                }
            }
            if (z <= QueueTopZ + 0.62f && z >= QueueBottomZ - 0.2f)
            {
                int column = (int)Math.Round(x / ColumnGap + (Columns - 1) / 2f);
                if (column >= 0 && column < Columns && Math.Abs(x - ColumnX(column)) <= ColumnGap / 2f)
                {
                    index = column;
                    return StageTapTarget.Column;
                }
            }
            return StageTapTarget.None;
        }
    }
}
