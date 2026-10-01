using UnityEngine;
using UnityEngine.UI;

namespace HyperFrame.UI
{
    public enum ThemeColor { Background, Panel, Primary, Secondary, Accent, Text, TextOnPrimary, Overlay, Positive, Negative }

    /// <summary>
    /// Per-game look (UI-11): colors, fonts and sprites. Views and placeholder UI read colors through
    /// <see cref="ThemedGraphic"/>, so swapping the theme reskins the whole UI without code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "HyperFrame/UI/Theme", fileName = "UITheme")]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Colors")]
        public Color background = new Color(0.15f, 0.17f, 0.25f);
        public Color panel = new Color(0.97f, 0.95f, 0.90f);
        public Color primary = new Color(0.20f, 0.70f, 0.35f);
        public Color secondary = new Color(0.25f, 0.55f, 0.90f);
        public Color accent = new Color(1.00f, 0.75f, 0.15f);
        public Color text = new Color(0.15f, 0.15f, 0.20f);
        public Color textOnPrimary = Color.white;
        public Color overlay = new Color(0f, 0f, 0f, 0.6f);
        public Color positive = new Color(0.25f, 0.80f, 0.40f);
        public Color negative = new Color(0.90f, 0.30f, 0.30f);

        [Header("Fonts")]
        [Tooltip("Font for legacy Text (placeholders). Empty = Unity built-in font.")]
        public Font font;
        [Tooltip("Optional TextMeshPro font asset for TMP labels in prefabs.")]
        public TMPro.TMP_FontAsset tmpFont;

        [Header("Sprites (optional; flat colors when empty)")]
        public Sprite buttonSprite;
        public Sprite panelSprite;

        [Header("Sizes")]
        public int titleFontSize = 72;
        public int bodyFontSize = 44;
        public Vector2 buttonSize = new Vector2(560f, 140f);

        public Color Get(ThemeColor role)
        {
            switch (role)
            {
                case ThemeColor.Background: return background;
                case ThemeColor.Panel: return panel;
                case ThemeColor.Primary: return primary;
                case ThemeColor.Secondary: return secondary;
                case ThemeColor.Accent: return accent;
                case ThemeColor.Text: return text;
                case ThemeColor.TextOnPrimary: return textOnPrimary;
                case ThemeColor.Overlay: return overlay;
                case ThemeColor.Positive: return positive;
                case ThemeColor.Negative: return negative;
                default: return Color.magenta;
            }
        }

        public Font ResolveFont() => font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static UITheme CreateDefault()
        {
            var theme = CreateInstance<UITheme>();
            theme.name = "DefaultTheme (runtime)";
            return theme;
        }
    }

    /// <summary>Colors a Graphic (Image/Text/TMP) from the active theme; re-applied when the theme changes.</summary>
    [DisallowMultipleComponent]
    public sealed class ThemedGraphic : MonoBehaviour
    {
        public ThemeColor role = ThemeColor.Primary;
        [Range(0f, 1f)] public float alpha = 1f;

        public void Apply(UITheme theme)
        {
            if (theme == null) return;
            var graphic = GetComponent<Graphic>();
            if (graphic == null) return;
            var c = theme.Get(role);
            c.a *= alpha;
            graphic.color = c;

            if (graphic is Text legacy) legacy.font = theme.ResolveFont();
            else if (graphic is TMPro.TMP_Text tmp && theme.tmpFont != null) tmp.font = theme.tmpFont;
            else if (graphic is Image image)
            {
                var sprite = role == ThemeColor.Panel ? theme.panelSprite
                    : (role == ThemeColor.Primary || role == ThemeColor.Secondary) ? theme.buttonSprite : null;
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                }
            }
        }
    }
}
