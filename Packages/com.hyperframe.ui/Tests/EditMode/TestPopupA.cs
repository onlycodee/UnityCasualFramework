using UnityEngine;

namespace HyperFrame.UI.Tests
{
    public sealed class TestPopupA : UIPopup
    {
        public int Shown;
        protected override void BuildPlaceholder(UIBuilder ui)
        {
            var panel = ui.Panel(transform, new Vector2(600, 600));
            ui.Button(panel, "btn_Ok", "OK");
        }
        protected override void OnCreated() => Bind("btn_Ok", () => Close("ok"));
        protected override void OnShow(object args) => Shown++;
    }
}
