namespace HyperFrame.UI.Tests
{
    public sealed class TestScreenB : UIScreen
    {
        protected override void BuildPlaceholder(UIBuilder ui)
        {
            ui.Background(transform);
            ui.Label(transform, "txt_Title", "B");
        }
    }
}
