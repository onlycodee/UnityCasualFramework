using UnityEngine;

namespace HyperFrame.App
{
    /// <summary>
    /// Everything a gameplay may change on the shared camera (projection, placement, clear settings).
    /// The flow captures it before a level begins and restores it when the level is left, so a game can
    /// turn the camera into a 3D perspective rig without leaking that into Home or the next level.
    /// </summary>
    public struct CameraSnapshot
    {
        public bool Valid;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool Orthographic;
        public float OrthographicSize;
        public float FieldOfView;
        public float NearClip;
        public float FarClip;
        public CameraClearFlags ClearFlags;
        public Color Background;
        public int CullingMask;

        public static CameraSnapshot Capture(Camera camera)
        {
            if (camera == null) return default;
            var t = camera.transform;
            return new CameraSnapshot
            {
                Valid = true,
                Position = t.localPosition,
                Rotation = t.localRotation,
                Orthographic = camera.orthographic,
                OrthographicSize = camera.orthographicSize,
                FieldOfView = camera.fieldOfView,
                NearClip = camera.nearClipPlane,
                FarClip = camera.farClipPlane,
                ClearFlags = camera.clearFlags,
                Background = camera.backgroundColor,
                CullingMask = camera.cullingMask,
            };
        }

        public void Restore(Camera camera)
        {
            if (!Valid || camera == null) return;
            var t = camera.transform;
            t.localPosition = Position;
            t.localRotation = Rotation;
            camera.orthographic = Orthographic;
            camera.orthographicSize = OrthographicSize;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = NearClip;
            camera.farClipPlane = FarClip;
            camera.clearFlags = ClearFlags;
            camera.backgroundColor = Background;
            camera.cullingMask = CullingMask;
        }
    }
}
