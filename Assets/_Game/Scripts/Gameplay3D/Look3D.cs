using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Materials and renderer helpers for the 3D stage. The two shaders (PixelLoop/Toy, PixelLoop/Unlit) are
    /// referenced from the module asset so they ship in builds; if one is missing or unsupported the Built-in
    /// pipeline's Standard / Sprites shaders stand in, so the game still renders (less polished).
    /// </summary>
    public sealed class Look3D
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_Emission");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int GlossId = Shader.PropertyToID("_Glossiness");
        static readonly int RimId = Shader.PropertyToID("_Rim");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int ZTestId = Shader.PropertyToID("_ZTest");

        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly bool _toyShader;

        /// <summary>Glossy lit plastic (voxels, shooters, stage). Per-renderer colour via <see cref="Tint"/>.</summary>
        public readonly Material Toy;
        /// <summary>Same, but matte (stage surfaces, ground).</summary>
        public readonly Material Matte;
        /// <summary>Additive glow (flashes, sparks, rings, trails).</summary>
        public readonly Material Additive;
        /// <summary>Alpha-blended unlit (badges, shadows, soft shapes).</summary>
        public readonly Material Blended;
        /// <summary>Additive, untextured (bullet trails).</summary>
        public readonly Material Trail;
        /// <summary>Dark glossy disc behind ammo numbers.</summary>
        public readonly Material Badge;
        /// <summary>Text for world labels (ammo, progress), drawn after the blended badges behind it.</summary>
        public readonly Material Text;

        public Look3D(Shader toy, Shader unlit)
        {
            bool toyOk = toy != null && toy.isSupported;
            bool unlitOk = unlit != null && unlit.isSupported;
            _toyShader = toyOk;
            var lit = toyOk ? toy : Shader.Find("Standard");
            if (lit == null) lit = Shader.Find("Legacy Shaders/Diffuse");
            var flat = unlitOk ? unlit : Shader.Find("Sprites/Default");

            Toy = new Material(lit) { name = "PL3D_Toy", enableInstancing = true };
            Toy.SetFloat(GlossId, 0.62f);
            Toy.SetFloat(RimId, 0.45f);
            Matte = new Material(lit) { name = "PL3D_Matte", enableInstancing = true };
            Matte.SetFloat(GlossId, 0.18f);
            Matte.SetFloat(RimId, 0.12f);
            if (!toyOk)
            {
                // Standard: emission is a keyword + _EmissionColor.
                Toy.EnableKeyword("_EMISSION");
                Matte.SetFloat(GlossId, 0.1f);
            }

            Additive = Unlit(flat, "PL3D_Additive", BlendMode.SrcAlpha, BlendMode.One, CompareFunction.LessEqual, 3100);
            Blended = Unlit(flat, "PL3D_Blended", BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, CompareFunction.LessEqual, 3000);
            Text = new Material(PixelLoopArt.Font.material) { name = "PL3D_Text", renderQueue = 3150 };
            Font.textureRebuilt += OnFontRebuilt;
            Additive.mainTexture = PixelLoopArt.Glow.texture;
            Trail = Unlit(flat, "PL3D_Trail", BlendMode.SrcAlpha, BlendMode.One, CompareFunction.LessEqual, 3050);
            Badge = WithTexture(Blended, PixelLoopArt.Disc.texture, "PL3D_Badge");
        }

        static Material Unlit(Shader shader, string name, BlendMode src, BlendMode dst, CompareFunction zTest, int queue)
        {
            var m = new Material(shader) { name = name, enableInstancing = true, renderQueue = queue };
            m.SetFloat(SrcBlendId, (float)src);
            m.SetFloat(DstBlendId, (float)dst);
            m.SetFloat(ZWriteId, 0f);
            m.SetFloat(ZTestId, (float)zTest);
            return m;
        }

        /// <summary>A textured variant of an unlit material (ring, disc, panel sprites).</summary>
        public Material WithTexture(Material source, Texture texture, string name)
        {
            var m = new Material(source) { name = name, mainTexture = texture };
            return m;
        }

        public MeshRenderer Renderer(string name, Transform parent, Mesh mesh, Material material, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = shadows;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        /// <summary>Sets colour and emission (white flash, glow) on one renderer without a new material.</summary>
        public void Tint(Renderer renderer, Color color, Color emission = default)
        {
            if (renderer == null) return;
            _block.Clear();
            _block.SetColor(ColorId, color);
            if (_toyShader) _block.SetColor(EmissionId, emission);
            else _block.SetColor(EmissionColorId, emission * emission.a);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>Colour (with alpha) for unlit renderers.</summary>
        public void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            _block.Clear();
            _block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        // The dynamic font atlas can be rebuilt (new glyphs); keep the copied text material on the new texture.
        void OnFontRebuilt(Font font)
        {
            if (font == PixelLoopArt.Font && Text != null) Text.mainTexture = font.material.mainTexture;
        }

        public void Destroy()
        {
            Font.textureRebuilt -= OnFontRebuilt;
            Object.Destroy(Toy);
            Object.Destroy(Matte);
            Object.Destroy(Additive);
            Object.Destroy(Blended);
            Object.Destroy(Text);
            Object.Destroy(Trail);
            Object.Destroy(Badge);
        }
    }
}
