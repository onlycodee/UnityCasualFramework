using UnityEngine;
using UnityEngine.UI;

namespace HyperFrame.UI
{
    /// <summary>
    /// Builds simple themed uGUI hierarchies in code. Used for placeholder views so the game is
    /// playable before any UI art exists, and for framework overlays (toast, loading).
    /// Names follow the btn_/txt_/img_ convention so the view code is the same as with a prefab.
    /// </summary>
    public sealed class UIBuilder
    {
        public UITheme Theme { get; }

        public UIBuilder(UITheme theme) => Theme = theme != null ? theme : UITheme.CreateDefault();

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        Graphic Themed(Graphic g, ThemeColor role, float alpha = 1f)
        {
            var themed = g.gameObject.AddComponent<ThemedGraphic>();
            themed.role = role;
            themed.alpha = alpha;
            themed.Apply(Theme);
            return g;
        }

        /// <summary>A full-stretch colored background.</summary>
        public Image Background(Transform parent, ThemeColor role = ThemeColor.Background, string name = "img_Background")
        {
            var rt = CreateRect(name, parent);
            Stretch(rt);
            var image = rt.gameObject.AddComponent<Image>();
            Themed(image, role);
            return image;
        }

        /// <summary>A centered panel with a vertical layout; add content to the returned transform.</summary>
        public RectTransform Panel(Transform parent, Vector2 size, string name = "img_Panel", ThemeColor role = ThemeColor.Panel)
        {
            var rt = CreateRect(name, parent);
            rt.sizeDelta = size;
            var image = rt.gameObject.AddComponent<Image>();
            Themed(image, role);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 32f;
            layout.padding = new RectOffset(48, 48, 48, 48);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>A transparent vertical stack, centered, filling the parent.</summary>
        public RectTransform Column(Transform parent, string name = "Column", float spacing = 32f)
        {
            var rt = CreateRect(name, parent);
            Stretch(rt);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rt;
        }

        public RectTransform Row(Transform parent, string name = "Row", float spacing = 24f, float height = 140f)
        {
            var rt = CreateRect(name, parent);
            rt.sizeDelta = new Vector2(900f, height);
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rt;
        }

        public Text Label(Transform parent, string name, string text, int fontSize = 0, ThemeColor role = ThemeColor.Text, float width = 900f)
        {
            var rt = CreateRect(name, parent);
            int size = fontSize > 0 ? fontSize : Theme.bodyFontSize;
            rt.sizeDelta = new Vector2(width, size * 1.6f);
            var label = rt.gameObject.AddComponent<Text>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            Themed(label, role);
            return label;
        }

        public Text Title(Transform parent, string name, string text, ThemeColor role = ThemeColor.Text) =>
            Label(parent, name, text, Theme.titleFontSize, role);

        public Button Button(Transform parent, string name, string label, ThemeColor role = ThemeColor.Primary, Vector2? size = null)
        {
            var rt = CreateRect(name, parent);
            rt.sizeDelta = size ?? Theme.buttonSize;
            var image = rt.gameObject.AddComponent<Image>();
            Themed(image, role);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Label(rt, "txt_" + StripPrefix(name), label, 0, ThemeColor.TextOnPrimary, rt.sizeDelta.x);
            Stretch((RectTransform)text.transform);
            return button;
        }

        public Image Image(Transform parent, string name, Vector2 size, ThemeColor role = ThemeColor.Accent)
        {
            var rt = CreateRect(name, parent);
            rt.sizeDelta = size;
            var image = rt.gameObject.AddComponent<Image>();
            Themed(image, role);
            return image;
        }

        /// <summary>Fixed-size empty element for spacing inside layouts.</summary>
        public RectTransform Spacer(Transform parent, float height)
        {
            var rt = CreateRect("Spacer", parent);
            rt.sizeDelta = new Vector2(10f, height);
            return rt;
        }

        /// <summary>Pins an element to an edge/corner of its parent (outside of layout groups).</summary>
        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
        }

        static string StripPrefix(string name)
        {
            int i = name.IndexOf('_');
            return i >= 0 ? name.Substring(i + 1) : name;
        }

        // ── Label helpers that work for legacy Text and TextMeshPro ────────────────────

        public static void SetLabel(Transform t, string value)
        {
            var legacy = t.GetComponent<Text>();
            if (legacy != null) { legacy.text = value; return; }
            var tmp = t.GetComponent<TMPro.TMP_Text>();
            if (tmp != null) { tmp.text = value; return; }
            // A button name was passed: set its child label.
            legacy = t.GetComponentInChildren<Text>(true);
            if (legacy != null) { legacy.text = value; return; }
            tmp = t.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null) tmp.text = value;
        }

        public static string GetLabel(Transform t)
        {
            var legacy = t.GetComponentInChildren<Text>(true);
            if (legacy != null) return legacy.text;
            var tmp = t.GetComponentInChildren<TMPro.TMP_Text>(true);
            return tmp != null ? tmp.text : null;
        }
    }
}
