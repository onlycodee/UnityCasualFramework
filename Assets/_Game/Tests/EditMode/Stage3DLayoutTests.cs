using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>The 3D stage layout: taps on a column or tray slot land on it, and the areas never overlap.</summary>
    public class Stage3DLayoutTests
    {
        static Stage3DLayout Layout(int width = 10, int height = 12, int columns = 3, int slots = 5) =>
            new Stage3DLayout(new BeltGeometry(width, height), columns, slots);

        [TestCase(1, 4)]
        [TestCase(2, 5)]
        [TestCase(3, 5)]
        [TestCase(4, 7)]
        public void EveryColumnAndSlot_IsHitAtItsCentre(int columns, int slots)
        {
            var layout = Layout(columns: columns, slots: slots);
            for (int c = 0; c < columns; c++)
            for (int row = 0; row < Stage3DLayout.VisibleRows; row++)
            {
                Assert.AreEqual(StageTapTarget.Column, layout.HitTest(layout.ColumnX(c), layout.RowZ(row), out int index), $"column {c} row {row}");
                Assert.AreEqual(c, index);
            }
            for (int s = 0; s < slots; s++)
            {
                Assert.AreEqual(StageTapTarget.TraySlot, layout.HitTest(layout.SlotX(s), layout.TrayZ, out int index), $"slot {s}");
                Assert.AreEqual(s, index);
            }
        }

        [Test]
        public void TrayAndQueue_AreBelowTheBelt_InThatOrder()
        {
            var geo = new BeltGeometry(10, 12);
            var layout = new Stage3DLayout(geo, 3, 5);
            Assert.Less(layout.TrayZ, geo.Bottom - Stage3DLayout.TrackWidth / 2f);
            Assert.Less(layout.QueueTopZ, layout.TrayZ);
            Assert.Less(layout.QueueBottomZ, layout.RowZ(Stage3DLayout.VisibleRows - 1));
        }

        [Test]
        public void TapsOnTheBoardOrBetweenAreas_HitNothing()
        {
            var layout = Layout();
            Assert.AreEqual(StageTapTarget.None, layout.HitTest(0f, 0f, out _), "board centre");
            Assert.AreEqual(StageTapTarget.None, layout.HitTest(0f, (layout.TrayZ + layout.QueueTopZ) / 2f, out _), "gap between tray and queue");
            Assert.AreEqual(StageTapTarget.None, layout.HitTest(layout.ColumnX(2) + Stage3DLayout.ColumnGap, layout.QueueTopZ, out _), "right of the last column");
            Assert.AreEqual(StageTapTarget.None, layout.HitTest(0f, layout.QueueBottomZ - 1f, out _), "below the queue");
        }

        [Test]
        public void RowsPastTheVisibleOnes_StackOnTheLast()
        {
            var layout = Layout();
            Assert.AreEqual(layout.RowZ(Stage3DLayout.VisibleRows - 1), layout.RowZ(Stage3DLayout.VisibleRows + 3));
        }
    }
}
