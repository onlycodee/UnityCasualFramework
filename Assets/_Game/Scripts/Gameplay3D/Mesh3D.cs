using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Procedural meshes for the 3D stage, so the game needs no model files: rounded boxes (voxels, slabs),
    /// spheres, cylinders, flat discs/quads, the belt rail extruded along its path, and the merged "ghost"
    /// tiles under the picture. Shared meshes are built once and cached.
    /// </summary>
    public static class Mesh3D
    {
        static Mesh _voxel, _sphere, _cylinder, _quad, _disc, _slab;

        /// <summary>Unit cube (−0.5…0.5) with rounded edges, radius 0.14.</summary>
        public static Mesh Voxel => _voxel != null ? _voxel : (_voxel = RoundedBox(Vector3.one, 0.14f, 3, "PL_Voxel"));
        /// <summary>Unit box with small rounded edges, for slabs and pads that get stretched.</summary>
        public static Mesh Slab => _slab != null ? _slab : (_slab = RoundedBox(Vector3.one, 0.06f, 2, "PL_Slab"));
        /// <summary>Sphere of diameter 1.</summary>
        public static Mesh Sphere => _sphere != null ? _sphere : (_sphere = MakeSphere(24, 16));
        /// <summary>Cylinder along +Y, diameter 1, height 1, centred.</summary>
        public static Mesh Cylinder => _cylinder != null ? _cylinder : (_cylinder = MakeCylinder(20));
        /// <summary>Quad facing −Z (towards a camera looking along +Z), 1×1, with UVs.</summary>
        public static Mesh Quad => _quad != null ? _quad : (_quad = MakeQuad());
        /// <summary>Flat disc on XZ facing +Y, diameter 1, white centre fading to the rim colour via vertex alpha.</summary>
        public static Mesh Disc => _disc != null ? _disc : (_disc = MakeDisc(40));

        public static Mesh RoundedBox(Vector3 size, float radius, int segments, string name)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            var half = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
            var inner = half - Vector3.one * radius;
            int n = Mathf.Max(1, segments) * 2 + 1; // odd so the flat middle has its own strip

            void Face(Vector3 normal, Vector3 u, Vector3 v)
            {
                int start = verts.Count;
                for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    // Points on the face of a cube (−1…1), packed towards the edges so curvature is smooth.
                    float a = Bias(i / (float)n), b = Bias(j / (float)n);
                    var p = normal + u * (a * 2f - 1f) + v * (b * 2f - 1f);
                    var cube = Vector3.Scale(p, half);
                    var core = new Vector3(Mathf.Clamp(cube.x, -inner.x, inner.x), Mathf.Clamp(cube.y, -inner.y, inner.y),
                        Mathf.Clamp(cube.z, -inner.z, inner.z));
                    var dir = cube - core;
                    var nrm = dir.sqrMagnitude > 1e-8f ? dir.normalized : normal;
                    verts.Add(core + nrm * radius);
                    normals.Add(nrm);
                }
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int k = start + j * (n + 1) + i;
                    tris.Add(k); tris.Add(k + n + 1); tris.Add(k + 1);
                    tris.Add(k + 1); tris.Add(k + n + 1); tris.Add(k + n + 2);
                }
            }

            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.right, Vector3.back);
            Face(Vector3.forward, Vector3.left, Vector3.up);
            Face(Vector3.back, Vector3.right, Vector3.up);
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.back, Vector3.up);
            return Build(name, verts, normals, tris, null, null);
        }

        // Pushes samples towards 0 and 1 so the rounded edge gets most of the vertices.
        static float Bias(float t)
        {
            float s = t * 2f - 1f;
            s = Mathf.Sign(s) * (1f - Mathf.Pow(1f - Mathf.Abs(s), 1.6f));
            return s * 0.5f + 0.5f;
        }

        static Mesh MakeSphere(int lon, int lat)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                    verts.Add(n * 0.5f);
                    normals.Add(n);
                }
            }
            for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int k = y * (lon + 1) + x;
                tris.Add(k); tris.Add(k + 1); tris.Add(k + lon + 1);
                tris.Add(k + 1); tris.Add(k + lon + 2); tris.Add(k + lon + 1);
            }
            return Build("PL_Sphere", verts, normals, tris, null, null);
        }

        static Mesh MakeCylinder(int sides)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(n * 0.5f + Vector3.down * 0.5f); normals.Add(n);
                verts.Add(n * 0.5f + Vector3.up * 0.5f); normals.Add(n);
            }
            for (int i = 0; i < sides; i++)
            {
                int k = i * 2;
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                tris.Add(k + 2); tris.Add(k + 1); tris.Add(k + 3);
            }
            foreach (float y in new[] { 0.5f, -0.5f })
            {
                var up = new Vector3(0f, Mathf.Sign(y), 0f);
                int centre = verts.Count;
                verts.Add(new Vector3(0f, y, 0f)); normals.Add(up);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f)); normals.Add(up);
                }
                for (int i = 0; i < sides; i++)
                {
                    if (y > 0f) { tris.Add(centre); tris.Add(centre + i + 2); tris.Add(centre + i + 1); }
                    else { tris.Add(centre); tris.Add(centre + i + 1); tris.Add(centre + i + 2); }
                }
            }
            return Build("PL_Cylinder", verts, normals, tris, null, null);
        }

        static Mesh MakeQuad()
        {
            var verts = new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            var normals = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uvs = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            var tris = new List<int> { 0, 2, 1, 1, 2, 3 };
            return Build("PL_Quad", verts, normals, tris, uvs, null);
        }

        static Mesh MakeDisc(int sides)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var normals = new List<Vector3> { Vector3.up };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var colors = new List<Color> { Color.white };
            var tris = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                colors.Add(new Color(1f, 1f, 1f, 0f));
            }
            for (int i = 0; i < sides; i++) { tris.Add(0); tris.Add(i + 2); tris.Add(i + 1); }
            return Build("PL_Disc", verts, normals, tris, uvs, colors);
        }

        /// <summary>
        /// Large ground disc with a radial colour gradient (centre → edge) in vertex colours, for a lit
        /// material whose _Color is white.
        /// </summary>
        public static Mesh Ground(float radius, Color centre, Color edge)
        {
            const int rings = 12, sides = 48;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            verts.Add(Vector3.zero); normals.Add(Vector3.up); colors.Add(centre);
            for (int r = 1; r <= rings; r++)
            {
                float k = r / (float)rings;
                var c = Color.Lerp(centre, edge, Mathf.SmoothStep(0f, 1f, k));
                for (int i = 0; i < sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius * k));
                    normals.Add(Vector3.up);
                    colors.Add(c);
                }
            }
            for (int i = 0; i < sides; i++) { tris.Add(0); tris.Add(1 + (i + 1) % sides); tris.Add(1 + i); }
            for (int r = 1; r < rings; r++)
            for (int i = 0; i < sides; i++)
            {
                int a = 1 + (r - 1) * sides + i, b = 1 + (r - 1) * sides + (i + 1) % sides;
                int c = a + sides, d = b + sides;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            return Build("PL_Ground", verts, normals, tris, null, colors);
        }

        /// <summary>
        /// The conveyor bed: a U-shaped profile (outer lip, floor, inner lip) swept around the belt path,
        /// with hard edges between profile faces. Lips get <paramref name="lip"/>, the floor <paramref name="floor"/>.
        /// </summary>
        public static Mesh BeltRail(BeltGeometry geo, float width, Color floor, Color lip)
        {
            // Profile across the belt: u = outward offset from the belt centre line, h = height.
            float w = width / 2f;
            var profile = new[]
            {
                new Vector2(-w - 0.02f, -0.30f), new Vector2(-w - 0.02f, 0.12f), // inner wall (towards board)
                new Vector2(-w + 0.08f, 0.12f), new Vector2(-w + 0.08f, 0.02f), // inner lip top, inside
                new Vector2(w - 0.08f, 0.02f),                                   // floor
                new Vector2(w - 0.08f, 0.12f), new Vector2(w + 0.02f, 0.12f),   // outer lip
                new Vector2(w + 0.02f, -0.30f),                                  // outer wall
            };
            var segColors = new[] { lip, lip, lip, floor, lip, lip, lip };

            int samples = Mathf.Max(48, Mathf.CeilToInt(geo.Length / 0.08f));
            var frames = new (Vector3 p, Vector3 outward)[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                var bp = geo.At(geo.Length * i / samples);
                frames[i] = (new Vector3(bp.X, 0f, bp.Y), new Vector3(-bp.NX, 0f, -bp.NY));
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int s = 0; s < profile.Length - 1; s++)
            {
                var a = profile[s];
                var b = profile[s + 1];
                var dir = (b - a).normalized;
                // Profile normal: rotate the edge direction so it faces away from the solid (to the left of travel).
                var pn = new Vector2(-dir.y, dir.x);
                int start = verts.Count;
                for (int i = 0; i <= samples; i++)
                {
                    var (p, o) = frames[i];
                    var n = (o * pn.x + Vector3.up * pn.y).normalized;
                    verts.Add(p + o * a.x + Vector3.up * a.y); normals.Add(n); colors.Add(segColors[s]);
                    verts.Add(p + o * b.x + Vector3.up * b.y); normals.Add(n); colors.Add(segColors[s]);
                }
                for (int i = 0; i < samples; i++)
                {
                    int k = start + i * 2;
                    tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                    tris.Add(k + 1); tris.Add(k + 2); tris.Add(k + 3);
                }
            }
            return Build("PL_BeltRail", verts, normals, tris, null, colors);
        }

        /// <summary>
        /// One mesh of thin flat tiles where the picture's pixels are (the faint print left on the board
        /// once a voxel is shot away).
        /// </summary>
        public static Mesh GhostTiles(PixelLevel level, BeltGeometry geo, Color[] palette, float tint, Color board)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            float h = geo.Cell * 0.43f;
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                int c = level.Cell(x, y);
                if (c < 0) continue;
                geo.CellCenter(x, y, out float cx, out float cy);
                var col = Color.Lerp(board, palette[c], tint);
                int k = verts.Count;
                verts.Add(new Vector3(cx - h, 0.004f, cy - h));
                verts.Add(new Vector3(cx + h, 0.004f, cy - h));
                verts.Add(new Vector3(cx - h, 0.004f, cy + h));
                verts.Add(new Vector3(cx + h, 0.004f, cy + h));
                for (int i = 0; i < 4; i++) { normals.Add(Vector3.up); colors.Add(col); }
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                tris.Add(k + 1); tris.Add(k + 2); tris.Add(k + 3);
            }
            return Build("PL_Ghosts", verts, normals, tris, null, colors);
        }

        // Makes every triangle wind so its face normal agrees with its vertex normals (front faces outward),
        // which keeps hand-built sweeps correct whatever direction the path runs.
        static void FixWinding(Mesh mesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f)
                {
                    int tmp = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = tmp;
                }
            }
            mesh.triangles = t;
        }

        static Mesh Build(string name, List<Vector3> verts, List<Vector3> normals, List<int> tris, List<Vector2> uvs, List<Color> colors)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            if (uvs != null) mesh.SetUVs(0, uvs);
            if (colors != null) mesh.SetColors(colors);
            else
            {
                var white = new List<Color>(verts.Count);
                for (int i = 0; i < verts.Count; i++) white.Add(Color.white);
                mesh.SetColors(white);
            }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            FixWinding(mesh);
            return mesh;
        }
    }
}
