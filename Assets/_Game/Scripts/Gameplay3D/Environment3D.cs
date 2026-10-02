using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Global look settings the 3D stage changes (ambient light, fog, shadow distance). Captured before the
    /// level and restored when it ends, so Home and the 2D module are untouched.
    /// </summary>
    public struct Environment3D
    {
        AmbientMode _ambientMode;
        Color _sky, _equator, _ground, _ambientLight;
        bool _fog;
        FogMode _fogMode;
        Color _fogColor;
        float _fogStart, _fogEnd, _shadowDistance;
        bool _valid;

        public static Environment3D Capture() => new Environment3D
        {
            _ambientMode = RenderSettings.ambientMode,
            _sky = RenderSettings.ambientSkyColor,
            _equator = RenderSettings.ambientEquatorColor,
            _ground = RenderSettings.ambientGroundColor,
            _ambientLight = RenderSettings.ambientLight,
            _fog = RenderSettings.fog,
            _fogMode = RenderSettings.fogMode,
            _fogColor = RenderSettings.fogColor,
            _fogStart = RenderSettings.fogStartDistance,
            _fogEnd = RenderSettings.fogEndDistance,
            _shadowDistance = QualitySettings.shadowDistance,
            _valid = true,
        };

        public static void Apply(PixelLoop3DStyle style, float cameraDistance)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = style.ambientSky;
            RenderSettings.ambientEquatorColor = style.ambientEquator;
            RenderSettings.ambientGroundColor = style.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = style.background;
            RenderSettings.fogStartDistance = cameraDistance + 2f;
            RenderSettings.fogEndDistance = cameraDistance + 26f;
            QualitySettings.shadowDistance = cameraDistance + 12f;
        }

        public void Restore()
        {
            if (!_valid) return;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _sky;
            RenderSettings.ambientEquatorColor = _equator;
            RenderSettings.ambientGroundColor = _ground;
            RenderSettings.ambientLight = _ambientLight;
            RenderSettings.fog = _fog;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogStartDistance = _fogStart;
            RenderSettings.fogEndDistance = _fogEnd;
            QualitySettings.shadowDistance = _shadowDistance;
        }
    }
}
