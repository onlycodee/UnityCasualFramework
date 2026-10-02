using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The 3D game-feel effects, pooled and stepped by the gameplay with game time (so pause and slow-mo
    /// apply): voxel debris that bounces on the board, additive flashes, sparks and flat shockwave rings.
    /// </summary>
    public sealed class Fx3D
    {
        const float Gravity = -16f;

        sealed class Piece
        {
            public Transform T;
            public MeshRenderer R;
            public Vector3 Velocity, Axis;
            public float Spin, Size, Life, Age, StartSize, EndSize;
            public Color Color;
            public int Kind; // 0 debris, 1 flash, 2 spark, 3 ring
            public bool Alive;
        }

        readonly Look3D _look;
        readonly Transform _root;
        readonly Func<Vector3, float> _floor;
        readonly Material _ring;
        readonly List<Piece> _pieces = new List<Piece>();
        readonly System.Random _rng = new System.Random(7);
        Quaternion _billboard = Quaternion.identity;

        /// <param name="floor">Height of the surface under a world point (debris bounces on it).</param>
        public Fx3D(Look3D look, Transform root, Func<Vector3, float> floor)
        {
            _look = look;
            _root = root;
            _floor = floor;
            _ring = look.WithTexture(look.Additive, PixelLoopArt.Ring.texture, "PL3D_Ring");
        }

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        Vector3 RandomDirection()
        {
            var v = new Vector3(Rand(-1f, 1f), Rand(-1f, 1f), Rand(-1f, 1f));
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.up;
        }

        Piece Rent(int kind)
        {
            foreach (var p in _pieces)
                if (!p.Alive && p.Kind == kind) { p.Alive = true; p.T.gameObject.SetActive(true); return p; }
            Piece piece;
            switch (kind)
            {
                case 0:
                    piece = new Piece { R = _look.Renderer("Debris", _root, Mesh3D.Voxel, _look.Toy) };
                    break;
                case 3:
                    piece = new Piece { R = _look.Renderer("Ring", _root, Mesh3D.Quad, _ring, shadows: false) };
                    break;
                default:
                    piece = new Piece { R = _look.Renderer(kind == 1 ? "Flash" : "Spark", _root, Mesh3D.Quad, _look.Additive, shadows: false) };
                    break;
            }
            piece.T = piece.R.transform;
            piece.Kind = kind;
            piece.Alive = true;
            _pieces.Add(piece);
            return piece;
        }

        /// <summary>Little cubes of the voxel's colour burst out and bounce on whatever is below.</summary>
        public void Debris(Vector3 position, Color color, float size, int count, float power = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Rent(0);
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y) * 0.6f + 0.4f;
                p.Velocity = new Vector3(dir.x * Rand(1.5f, 3.6f), dir.y * Rand(3f, 5.5f), dir.z * Rand(1.5f, 3.6f)) * power;
                p.Axis = RandomDirection();
                p.Spin = Rand(360f, 900f);
                p.StartSize = size * Rand(0.6f, 1.15f);
                p.Size = p.StartSize;
                p.Life = Rand(0.65f, 1.05f);
                p.Age = 0f;
                p.Color = Color.Lerp(color, Color.white, Rand(0f, 0.2f));
                p.T.localPosition = position + RandomDirection() * size * 0.5f;
                p.T.localRotation = UnityEngine.Random.rotationUniform;
                p.T.localScale = Vector3.one * p.Size;
                _look.Tint(p.R, p.Color, new Color(1f, 1f, 1f, 0.35f));
            }
        }

        /// <summary>A soft additive flash that blooms and fades (impacts, muzzle, pops).</summary>
        public void Flash(Vector3 position, Color color, float size, float life = 0.18f)
        {
            var p = Rent(1);
            p.T.localPosition = position;
            p.StartSize = size * 0.4f;
            p.EndSize = size;
            p.Life = life;
            p.Age = 0f;
            p.Color = color;
            p.Velocity = Vector3.zero;
            Step(p, 0f);
        }

        /// <summary>Hot little glows that fly out, fall and shrink.</summary>
        public void Sparks(Vector3 position, Color color, int count, float speed = 4f)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Rent(2);
                var dir = RandomDirection();
                dir.y = Mathf.Abs(dir.y);
                p.Velocity = dir * Rand(speed * 0.5f, speed);
                p.StartSize = Rand(0.12f, 0.24f);
                p.EndSize = 0f;
                p.Life = Rand(0.25f, 0.5f);
                p.Age = 0f;
                p.Color = Color.Lerp(color, Color.white, 0.5f);
                p.T.localPosition = position;
                Step(p, 0f);
            }
        }

        /// <summary>A flat ring that races outward on the ground and fades.</summary>
        public void Ring(Vector3 position, Color color, float radius, float life = 0.4f)
        {
            var p = Rent(3);
            p.T.localPosition = position + Vector3.up * 0.02f;
            p.T.localRotation = Quaternion.Euler(90f, 0f, 0f);
            p.StartSize = radius * 0.3f;
            p.EndSize = radius * 2f;
            p.Life = life;
            p.Age = 0f;
            p.Color = color;
            Step(p, 0f);
        }

        /// <param name="cameraRotation">Flashes and sparks face the camera.</param>
        public void Tick(float dt, Quaternion cameraRotation)
        {
            _billboard = cameraRotation;
            foreach (var p in _pieces)
            {
                if (!p.Alive) continue;
                if (p.T == null) { p.Alive = false; continue; }
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Alive = false;
                    p.T.gameObject.SetActive(false);
                    continue;
                }
                Step(p, dt);
            }
        }

        void Step(Piece p, float dt)
        {
            float k = Mathf.Clamp01(p.Age / p.Life);
            switch (p.Kind)
            {
                case 0:
                {
                    p.Velocity.y += Gravity * dt;
                    var pos = p.T.localPosition + p.Velocity * dt;
                    float floor = _floor(pos) + p.Size * 0.5f;
                    if (pos.y < floor)
                    {
                        pos.y = floor;
                        if (p.Velocity.y < 0f) p.Velocity.y = -p.Velocity.y * 0.38f;
                        p.Velocity.x *= 0.7f;
                        p.Velocity.z *= 0.7f;
                        p.Spin *= 0.6f;
                    }
                    p.T.localPosition = pos;
                    p.T.localRotation = Quaternion.AngleAxis(p.Spin * dt, p.Axis) * p.T.localRotation;
                    float shrink = k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f;
                    p.Size = p.StartSize * shrink;
                    p.T.localScale = Vector3.one * p.Size;
                    if (k > 0.05f && k < 0.12f) _look.Tint(p.R, p.Color); // the hot white edge cools off
                    break;
                }
                case 1:
                {
                    float e = 1f - (1f - k) * (1f - k);
                    p.T.localScale = Vector3.one * Mathf.Lerp(p.StartSize, p.EndSize, e);
                    p.T.rotation = _billboard;
                    _look.SetColor(p.R, new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * (1f - k)));
                    break;
                }
                case 2:
                {
                    p.Velocity.y += Gravity * 0.5f * dt;
                    p.Velocity *= 1f - Mathf.Min(1f, 2.5f * dt);
                    p.T.localPosition += p.Velocity * dt;
                    p.T.localScale = Vector3.one * Mathf.Lerp(p.StartSize, p.EndSize, k);
                    p.T.rotation = _billboard;
                    _look.SetColor(p.R, new Color(p.Color.r, p.Color.g, p.Color.b, 1f - k * k));
                    break;
                }
                default:
                {
                    float e = 1f - (1f - k) * (1f - k) * (1f - k);
                    p.T.localScale = Vector3.one * Mathf.Lerp(p.StartSize, p.EndSize, e);
                    _look.SetColor(p.R, new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * (1f - k)));
                    break;
                }
            }
        }

        public void Destroy() => UnityEngine.Object.Destroy(_ring);
    }
}
