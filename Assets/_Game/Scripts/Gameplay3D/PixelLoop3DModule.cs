using HyperFrame.App;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The 3D presentation of Pixel Loop: voxel pictures, a moving conveyor, toy shooters, real lights and
    /// shadows and a perspective camera. Same rules, levels and sounds as the 2D <see cref="PixelLoopModule"/>;
    /// choose either in GameDefinition.gameplay.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Pixel Loop 3D Module", fileName = "PixelLoop3DModule")]
    public sealed class PixelLoop3DModule : PixelLoopModuleBase
    {
        public PixelLoop3DStyle style = new PixelLoop3DStyle();
        [Tooltip("PixelLoop/Toy (Assets/_Game/Art/Shaders). Referenced here so it is included in builds.")]
        public Shader toyShader;
        [Tooltip("PixelLoop/Unlit (Assets/_Game/Art/Shaders).")]
        public Shader unlitShader;

        public override IGameplay CreateGameplay() => new PixelLoop3DGameplay(style, toyShader, unlitShader);
    }

    /// <summary>Look of the 3D stage (AR-3: look-and-feel is data). Pixel colours come from each level.</summary>
    [System.Serializable]
    public sealed class PixelLoop3DStyle
    {
        [Header("Scene")]
        public Color background = new Color(0.10f, 0.08f, 0.21f);
        public Color groundCentre = new Color(0.30f, 0.25f, 0.52f);
        public Color platform = new Color(0.22f, 0.19f, 0.38f);
        public Color board = new Color(0.95f, 0.94f, 0.99f);
        [Range(0f, 1f)] public float ghostTint = 0.22f;
        public Color track = new Color(0.12f, 0.11f, 0.21f);
        public Color lip = new Color(0.36f, 0.33f, 0.58f);
        public Color tread = new Color(0.56f, 0.52f, 0.84f);
        public Color slot = new Color(0.14f, 0.12f, 0.25f);
        public Color warning = new Color(1f, 0.32f, 0.36f);
        public Color progress = new Color(0.45f, 0.92f, 0.62f);

        [Header("Light")]
        public Color sunColor = new Color(1f, 0.95f, 0.88f);
        public float sunIntensity = 1.15f;
        public Vector2 sunAngles = new Vector2(52f, -32f);
        [Range(0f, 1f)] public float shadowStrength = 0.6f;
        public Color ambientSky = new Color(0.62f, 0.62f, 0.86f);
        public Color ambientEquator = new Color(0.40f, 0.36f, 0.60f);
        public Color ambientGround = new Color(0.16f, 0.14f, 0.27f);

        [Header("Camera")]
        [Range(30f, 85f)] public float cameraPitch = 56f;
        [Range(20f, 60f)] public float fieldOfView = 34f;
        [Tooltip("Screen fraction kept free at the top for the HUD.")]
        public float hudMargin = 0.13f;
    }
}
