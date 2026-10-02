using HyperFrame.App;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The 2D presentation of Pixel Loop (flat sprites, orthographic camera). Levels, sounds and debug
    /// commands come from <see cref="PixelLoopModuleBase"/>; <see cref="PixelLoop3DModule"/> is the 3D one.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Pixel Loop Module", fileName = "PixelLoopModule")]
    public sealed class PixelLoopModule : PixelLoopModuleBase
    {
        public PixelLoopStyle style = new PixelLoopStyle();

        public override IGameplay CreateGameplay() => new PixelLoopGameplay(style);
    }

    /// <summary>Colours of the stage (AR-3: look-and-feel is data). Pixel colours come from each level.</summary>
    [System.Serializable]
    public sealed class PixelLoopStyle
    {
        public Color backgroundTop = new Color(0.22f, 0.19f, 0.42f);
        public Color backgroundBottom = new Color(0.09f, 0.08f, 0.20f);
        public Color board = new Color(0.96f, 0.95f, 0.99f);
        public Color boardShadow = new Color(0f, 0f, 0f, 0.35f);
        public Color track = new Color(0.17f, 0.15f, 0.29f);
        public Color tread = new Color(0.33f, 0.30f, 0.52f);
        public Color slot = new Color(1f, 1f, 1f, 0.10f);
        public Color warning = new Color(1f, 0.32f, 0.36f);
        public Color progress = new Color(0.45f, 0.92f, 0.62f);
        public Color bokeh = new Color(0.65f, 0.55f, 1f, 0.10f);
    }
}
