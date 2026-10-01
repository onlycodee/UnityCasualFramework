namespace HyperFrame.UI.Tests
{
    public sealed class TestScreenA : UIScreen
    {
        protected override void BuildPlaceholder(UIBuilder ui) => ui.Background(transform);
    }
}
