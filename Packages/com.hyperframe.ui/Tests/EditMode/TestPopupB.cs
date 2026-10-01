using UnityEngine;

namespace HyperFrame.UI.Tests
{
    public sealed class TestPopupB : UIPopup
    {
        protected override void BuildPlaceholder(UIBuilder ui) => ui.Panel(transform, new Vector2(600, 600));
    }
}
