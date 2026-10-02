using UnityEngine;

namespace Game
{
    /// <summary>
    /// Procedural sprites for Pixel Loop (AI-14 placeholders that already look finished): bevelled tiles,
    /// shaded discs, soft glows and 9-sliced rounded panels. All white/grey so SpriteRenderer.color tints them.
    /// Built once per run and cached.
    /// </summary>
    public static class PixelLoopArt
    {
        static Sprite _tile, _disc, _glow, _panel, _dash, _gradient, _ring, _solid;
        static Material _trail;
        static Font _font;

        /// <summary>Rounded square with a top-left bevel highlight and a bottom-right shade. 1 world unit.</summary>
        public static Sprite Tile => _tile != null ? _tile : (_tile = MakeTile(64));
        /// <summary>Sphere-shaded disc with a specular spot. 1 world unit.</summary>
        public static Sprite Disc => _disc != null ? _disc : (_disc = MakeDisc(128));
        /// <summary>Soft radial glow, alpha falloff. 1 world unit.</summary>
        public static Sprite Glow => _glow != null ? _glow : (_glow = MakeGlow(64));
        /// <summary>Thin ring for shockwaves. 1 world unit.</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeRing(128, 0.09f));
        /// <summary>9-sliced rounded panel (use SpriteDrawMode.Sliced and set size).</summary>
        public static Sprite Panel => _panel != null ? _panel : (_panel = MakeRoundedRect(64, 24, 100f));
        /// <summary>Small 9-sliced pill for belt treads.</summary>
        public static Sprite Dash => _dash != null ? _dash : (_dash = MakeRoundedRect(32, 15, 200f));
        /// <summary>Vertical white→black gradient used for the background (tinted by two-colour lerp in alpha).</summary>
        public static Sprite Gradient => _gradient != null ? _gradient : (_gradient = MakeGradient(128));

        /// <summary>Plain white square, 1 world unit.</summary>
        public static Sprite Solid => _solid != null ? _solid
            : (_solid = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f));

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Material for trails (Sprites/Default is always in builds, unlike the legacy particle shaders).</summary>
        public static Material TrailMaterial
        {
            get
            {
                if (_trail != null) return _trail;
                _trail = new Material(Shader.Find("Sprites/Default")) { name = "PL_Trail" };
                return _trail;
            }
        }

        public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Color color, int order, float size = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static SpriteRenderer SlicedPanel(string name, Transform parent, Vector2 size, Color color, int order, Sprite sprite = null)
        {
            var sr = Sprite(name, parent, sprite != null ? sprite : Panel, color, order);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            return sr;
        }

        /// <summary>Crisp world-space label (legacy TextMesh with the built-in font), centred.</summary>
        public static TextMesh Label(string name, Transform parent, string text, float height, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = height * 10f / tm.fontSize; // TextMesh line height ≈ fontSize × characterSize / 10
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            mr.sortingOrder = order;
            return tm;
        }

        public static Color Hex(string hex, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        /// <summary>Same hue, darker (for outlines and barrels).</summary>
        public static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>Perceived brightness, to pick white or dark text on a colour.</summary>
        public static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        // ── Texture builders ────────────────────────────────────────────────────────────

        static Texture2D NewTexture(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        static float RoundedRectDistance(float x, float y, float halfW, float halfH, float radius)
        {
            // Signed distance to a rounded rectangle centred at 0 (negative inside).
            float qx = Mathf.Abs(x) - (halfW - radius);
            float qy = Mathf.Abs(y) - (halfH - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        static Sprite MakeTile(int size)
        {
            var tex = NewTexture(size, size, "PL_Tile");
            var px = new Color32[size * size];
            float half = size / 2f, radius = size * 0.2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f - half, fy = y + 0.5f - half;
                float d = RoundedRectDistance(fx, fy, half, half, radius);
                float a = Mathf.Clamp01(0.5f - d);
                // bevel: light from the top-left, a rim of shade at the bottom-right
                float edge = Mathf.Clamp01(-d / (size * 0.16f));
                float light = (fy - fx) / size; // -1..1, top-left positive
                float v = 0.86f + 0.12f * light;
                v = Mathf.Lerp(v + 0.1f * Mathf.Sign(light), v, edge);
                // gloss band on the upper half
                if (fy > size * 0.08f && edge > 0.35f) v += 0.06f * Mathf.Clamp01((fy - size * 0.08f) / (size * 0.3f));
                byte b = (byte)(Mathf.Clamp01(v) * 255f);
                px[y * size + x] = new Color32(b, b, b, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeDisc(int size)
        {
            var tex = NewTexture(size, size, "PL_Disc");
            var px = new Color32[size * size];
            float r = size / 2f;
            var spec = new Vector2(-0.35f, 0.4f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f - r) / r, (y + 0.5f - r) / r);
                float d = p.magnitude;
                float a = Mathf.Clamp01((1f - d) * r);
                float shade = 1f - 0.28f * Mathf.Clamp01((Vector2.Dot(p, new Vector2(0.6f, -0.8f)) + 0.2f));
                shade -= 0.12f * Mathf.SmoothStep(0.75f, 1f, d); // rim
                float s = Mathf.Clamp01(1f - (p - spec).magnitude / 0.32f);
                float v = Mathf.Clamp01(shade + s * s * 0.45f);
                byte b = (byte)(v * 255f);
                px[y * size + x] = new Color32(b, b, b, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeGlow(int size)
        {
            var tex = NewTexture(size, size, "PL_Glow");
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeRing(int size, float thickness)
        {
            var tex = NewTexture(size, size, "PL_Ring");
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - (1f - thickness)) / thickness);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeRoundedRect(int size, int radius, float pixelsPerUnit)
        {
            var tex = NewTexture(size, size, "PL_Panel");
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = radius + 1;
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeGradient(int height)
        {
            var tex = NewTexture(4, height, "PL_Gradient");
            var px = new Color32[4 * height];
            for (int y = 0; y < height; y++)
            {
                byte v = (byte)(255f * y / (height - 1));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, v);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, 4, height), new Vector2(0.5f, 0.5f), height);
        }
    }
}
