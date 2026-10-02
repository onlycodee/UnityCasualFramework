using System.Collections.Generic;
using HyperFrame.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Turns the shared camera into a perspective rig looking down at the stage: frames every stage point
    /// inside the screen (leaving room for the HUD), flies in at the start, breathes slightly while idle,
    /// pushes in on a win and shakes with trauma (smooth noise, decays on its own). The flow restores the
    /// camera when the level is left.
    /// </summary>
    public sealed class CameraRig3D
    {
        public readonly Camera Camera;
        readonly float _pitch;
        Vector3 _home;
        Quaternion _rotation;
        float _distance = 15f;
        Vector3 _introFrom;
        Quaternion _introFromRotation;
        float _introT = 1f, _introDuration = 1f;
        float _push, _pushTarget, _trauma;
        readonly float _seed;

        public CameraRig3D(Camera camera, float pitch, float fieldOfView, Color background)
        {
            Camera = camera;
            _pitch = pitch;
            _rotation = Quaternion.Euler(pitch, 0f, 0f);
            _seed = Random.value * 100f;
            if (camera == null) return;
            camera.orthographic = false;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
        }

        public float Pitch => _pitch;
        public float Distance => _distance;
        public Quaternion Rotation => Camera != null ? Camera.transform.rotation : _rotation;

        /// <summary>
        /// Places the camera so every point is inside the viewport margins (fractions of the screen),
        /// centred between them.
        /// </summary>
        public void Frame(IList<Vector3> points, float left, float right, float bottom, float top)
        {
            if (Camera == null || points.Count == 0) return;
            var min = points[0];
            var max = points[0];
            foreach (var p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            var centre = (min + max) * 0.5f;
            var forward = _rotation * Vector3.forward;
            var up = _rotation * Vector3.up;
            var side = _rotation * Vector3.right;
            var t = Camera.transform;
            t.rotation = _rotation;
            Vector2 offset = Vector2.zero;
            float targetX = (left + 1f - right) * 0.5f, targetY = (bottom + 1f - top) * 0.5f;

            Vector3 Place(float d) => centre - forward * d + side * offset.x + up * offset.y;
            bool Fits(float d)
            {
                t.position = Place(d);
                foreach (var p in points)
                {
                    var v = Camera.WorldToViewportPoint(p);
                    if (v.z <= 0.1f || v.x < left || v.x > 1f - right || v.y < bottom || v.y > 1f - top) return false;
                }
                return true;
            }

            for (int pass = 0; pass < 6; pass++)
            {
                float lo = 1f, hi = 120f;
                for (int i = 0; i < 32; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(mid)) hi = mid; else lo = mid;
                }
                _distance = hi;
                t.position = Place(_distance);
                float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
                foreach (var p in points)
                {
                    var v = Camera.WorldToViewportPoint(p);
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
                float depth = Vector3.Dot(centre - t.position, forward);
                float height = 2f * depth * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float width = height * Camera.aspect;
                offset.x += ((minX + maxX) * 0.5f - targetX) * width;
                offset.y += ((minY + maxY) * 0.5f - targetY) * height;
            }
            // The last re-centre can push an edge out by a hair; settle the distance once more.
            float a = 1f, b = 120f;
            for (int i = 0; i < 32; i++)
            {
                float mid = (a + b) * 0.5f;
                if (Fits(mid)) b = mid; else a = mid;
            }
            _distance = b;
            _home = Place(_distance);
            t.SetPositionAndRotation(_home, _rotation);
        }

        /// <summary>Fly in from higher and further away.</summary>
        public void PlayIntro(float duration)
        {
            _introFromRotation = Quaternion.Euler(_pitch + 14f, 0f, 0f);
            _introFrom = _home - (_introFromRotation * Vector3.forward) * (_distance * 0.45f) + Vector3.up * 1.5f;
            _introT = 0f;
            _introDuration = Mathf.Max(0.01f, duration);
            if (Camera != null) Camera.transform.SetPositionAndRotation(_introFrom, _introFromRotation);
        }

        /// <summary>Adds shake trauma (0–1); the shake is trauma² so small hits stay subtle.</summary>
        public void Shake(float trauma) => _trauma = Mathf.Min(1f, _trauma + trauma);

        /// <summary>Moves closer by this fraction of the framing distance (0 = home).</summary>
        public void PushIn(float amount) => _pushTarget = amount;

        public void Tick(float dt, float time)
        {
            if (Camera == null) return;
            var position = _home;
            var rotation = _rotation;
            if (_introT < 1f)
            {
                _introT = Mathf.Min(1f, _introT + dt / _introDuration);
                float e = Easing.Evaluate(Ease.InOutCubic, _introT);
                position = Vector3.LerpUnclamped(_introFrom, _home, e);
                rotation = Quaternion.Slerp(_introFromRotation, _rotation, e);
            }
            _push = Mathf.Lerp(_push, _pushTarget, 1f - Mathf.Exp(-dt * 3f));
            var forward = rotation * Vector3.forward;
            var up = rotation * Vector3.up;
            var side = rotation * Vector3.right;
            position += forward * (_push * _distance);
            // Idle breathing so the scene never looks frozen.
            position += side * (Mathf.Sin(time * 0.37f) * 0.05f) + up * (Mathf.Sin(time * 0.51f) * 0.035f);

            if (_trauma > 0f)
            {
                float s = _trauma * _trauma;
                float n = time * 28f;
                float x = Mathf.PerlinNoise(_seed, n) * 2f - 1f;
                float y = Mathf.PerlinNoise(_seed + 3.7f, n) * 2f - 1f;
                float roll = Mathf.PerlinNoise(_seed + 7.1f, n) * 2f - 1f;
                position += (side * x + up * y) * (0.32f * s);
                rotation *= Quaternion.Euler(0f, 0f, roll * 2.2f * s);
                _trauma = Mathf.Max(0f, _trauma - dt * 1.7f);
            }
            Camera.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
