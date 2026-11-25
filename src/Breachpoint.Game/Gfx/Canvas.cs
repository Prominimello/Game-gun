using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Breachpoint.Gfx;

/// <summary>
/// CPU rasterizer with an HTML-canvas style API (paths, arcs, curves, anti-aliased nonzero fills,
/// strokes, clipping, gradients). Used to paint procedural art into textures once, then drawn on the GPU.
/// Pixels are stored premultiplied, as MonoGame's AlphaBlend expects.
/// </summary>
public sealed class Canvas
{
    public readonly int W, H;
    public readonly Color[] Px;

    private struct Mat
    {
        public float A, B, C, D, E, F;
        public static Mat Identity => new() { A = 1, D = 1 };
        public Vector2 Apply(float x, float y) => new(A * x + C * y + E, B * x + D * y + F);
        public Mat Mul(Mat m) => new()
        {
            A = A * m.A + C * m.B, B = B * m.A + D * m.B, C = A * m.C + C * m.D, D = B * m.C + D * m.D,
            E = A * m.E + C * m.F + E, F = B * m.E + D * m.F + F,
        };
        public float Scale => MathF.Sqrt(MathF.Abs(A * D - B * C));
    }

    private sealed class State
    {
        public Mat M = Mat.Identity;
        public float Alpha = 1;
        public byte[] Clip;
        public State Copy() => new() { M = M, Alpha = Alpha, Clip = Clip };
    }

    private State _s = new();
    private readonly Stack<State> _stack = new();
    private readonly List<List<Vector2>> _subs = new();
    private List<Vector2> _cur;
    private float[] _cov;

    public Canvas(int w, int h)
    {
        W = Math.Max(1, w); H = Math.Max(1, h);
        Px = new Color[W * H];
        _cov = new float[W + 2];
    }

    public float GlobalAlpha { get => _s.Alpha; set => _s.Alpha = value; }

    // ---------------- transform stack ----------------
    public void Save() => _stack.Push(_s.Copy());
    public void Restore() { if (_stack.Count > 0) _s = _stack.Pop(); }
    public void Translate(float x, float y) => _s.M = _s.M.Mul(new Mat { A = 1, D = 1, E = x, F = y });
    public void Scale(float x, float y) => _s.M = _s.M.Mul(new Mat { A = x, D = y });
    public void Rotate(float a) { float c = MathF.Cos(a), s = MathF.Sin(a); _s.M = _s.M.Mul(new Mat { A = c, B = s, C = -s, D = c }); }
    public void ResetTransform() => _s.M = Mat.Identity;
    private float DevScale => Math.Max(0.01f, _s.M.Scale);

    // ---------------- path building ----------------
    public void BeginPath() { _subs.Clear(); _cur = null; }
    public void MoveTo(float x, float y) { _cur = new List<Vector2> { _s.M.Apply(x, y) }; _subs.Add(_cur); }
    public void LineTo(float x, float y) { if (_cur == null) MoveTo(x, y); else _cur.Add(_s.M.Apply(x, y)); }
    /// <summary>Closes the current subpath (adds the closing edge) and starts a fresh one on the next MoveTo/LineTo.</summary>
    public void ClosePath() { if (_cur != null && _cur.Count > 1) _cur.Add(_cur[0]); _cur = null; }

    public void QuadTo(float cx, float cy, float x, float y)
    {
        if (_cur == null) MoveTo(cx, cy);
        var inv = Invert(_cur[^1]);
        int n = Math.Clamp((int)(Vector2.Distance(inv, new Vector2(x, y)) * DevScale / 3), 6, 24);
        for (int i = 1; i <= n; i++)
        {
            float t = (float)i / n, u = 1 - t;
            LineTo(u * u * inv.X + 2 * u * t * cx + t * t * x, u * u * inv.Y + 2 * u * t * cy + t * t * y);
        }
    }

    private Vector2 Invert(Vector2 p)
    {
        var m = _s.M; float det = m.A * m.D - m.B * m.C;
        if (MathF.Abs(det) < 1e-9f) return p;
        float x = p.X - m.E, y = p.Y - m.F;
        return new Vector2((m.D * x - m.C * y) / det, (-m.B * x + m.A * y) / det);
    }

    public void Arc(float cx, float cy, float r, float a0, float a1, bool ccw = false) => Ellipse(cx, cy, r, r, 0, a0, a1, ccw);

    public void Ellipse(float cx, float cy, float rx, float ry, float rot, float a0, float a1, bool ccw = false)
    {
        float span = a1 - a0;
        if (!ccw) { if (span < 0) span += MathF.PI * 2 * (MathF.Ceiling(-span / (MathF.PI * 2))); if (span > MathF.PI * 2) span = MathF.PI * 2; }
        else { if (span > 0) span -= MathF.PI * 2 * MathF.Ceiling(span / (MathF.PI * 2)); if (span < -MathF.PI * 2) span = -MathF.PI * 2; }
        int n = Math.Clamp((int)(MathF.Max(rx, ry) * DevScale * MathF.Abs(span) / 2.5f), 8, 96);
        float cr = MathF.Cos(rot), sr = MathF.Sin(rot);
        for (int i = 0; i <= n; i++)
        {
            float a = a0 + span * i / n;
            float ex = MathF.Cos(a) * rx, ey = MathF.Sin(a) * ry;
            float x = cx + ex * cr - ey * sr, y = cy + ex * sr + ey * cr;
            if (i == 0 && _cur == null) MoveTo(x, y); else LineTo(x, y);
        }
    }

    public void Rect(float x, float y, float w, float h) { MoveTo(x, y); LineTo(x + w, y); LineTo(x + w, y + h); LineTo(x, y + h); ClosePath(); }

    public void RoundRect(float x, float y, float w, float h, float r)
    {
        r = MathF.Min(r, MathF.Min(MathF.Abs(w), MathF.Abs(h)) / 2);
        if (r <= 0.01f) { Rect(x, y, w, h); return; }
        _cur = null;
        Arc(x + w - r, y + r, r, -MathF.PI / 2, 0);
        Arc(x + w - r, y + h - r, r, 0, MathF.PI / 2);
        Arc(x + r, y + h - r, r, MathF.PI / 2, MathF.PI);
        Arc(x + r, y + r, r, MathF.PI, MathF.PI * 1.5f);
        ClosePath();
    }

    public void Circle(float cx, float cy, float r) { _cur = null; Arc(cx, cy, r, 0, MathF.PI * 2); ClosePath(); }
    public void EllipseShape(float cx, float cy, float rx, float ry, float rot = 0) { _cur = null; Ellipse(cx, cy, rx, ry, rot, 0, MathF.PI * 2); ClosePath(); }

    public void Poly(params (float x, float y)[] pts)
    {
        MoveTo(pts[0].x, pts[0].y);
        for (int i = 1; i < pts.Length; i++) LineTo(pts[i].x, pts[i].y);
        ClosePath();
    }

    // ---------------- convenience fills ----------------
    public void FillRect(float x, float y, float w, float h, Color c) { BeginPath(); Rect(x, y, w, h); Fill(c); }
    public void FillRoundRect(float x, float y, float w, float h, float r, Color c) { BeginPath(); RoundRect(x, y, w, h, r); Fill(c); }
    public void FillCircle(float x, float y, float r, Color c) { BeginPath(); Circle(x, y, r); Fill(c); }
    public void FillEllipse(float x, float y, float rx, float ry, float rot, Color c) { BeginPath(); EllipseShape(x, y, rx, ry, rot); Fill(c); }
    public void FillPoly(Color c, params (float x, float y)[] pts) { BeginPath(); Poly(pts); Fill(c); }
    public void StrokeLine(float x0, float y0, float x1, float y1, float w, Color c) { BeginPath(); MoveTo(x0, y0); LineTo(x1, y1); Stroke(w, c); }

    // ---------------- rasterization ----------------
    public void Fill(Color c) => Raster(_subs, (_, _) => c, true);
    public void Fill(Func<float, float, Color> paint) => Raster(_subs, paint, true);

    /// <summary>Stroke the current path with round joins.</summary>
    public void Stroke(float width, Color c, bool closed = false)
    {
        float hw = width * DevScale / 2;
        var polys = new List<List<Vector2>>();
        foreach (var sub in _subs)
        {
            if (sub.Count < 2) continue;
            int n = closed ? sub.Count : sub.Count - 1;
            for (int i = 0; i < n; i++)
            {
                var a = sub[i]; var b = sub[(i + 1) % sub.Count];
                var d = b - a; float len = d.Length();
                if (len < 1e-4f) continue;
                var nn = new Vector2(-d.Y, d.X) / len * hw;
                polys.Add(new List<Vector2> { a + nn, b + nn, b - nn, a - nn });
            }
            if (hw > 0.9f) foreach (var p in sub) polys.Add(CircleCW(p, hw));
        }
        Raster(polys, (_, _) => c, false);
    }

    private static List<Vector2> CircleCW(Vector2 c, float r)
    {
        int n = Math.Clamp((int)(r * 2), 6, 24);
        var l = new List<Vector2>(n);
        for (int i = 0; i < n; i++) { float a = -MathF.PI * 2 * i / n; l.Add(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r); }
        return l;
    }

    /// <summary>Intersect the clip region with the current path.</summary>
    public void Clip()
    {
        var mask = new byte[W * H];
        var old = _s.Clip;
        RasterTo(_subs, (x, y, a) =>
        {
            int k = y * W + x;
            float prev = old == null ? 1 : old[k] / 255f;
            mask[k] = (byte)Math.Clamp(a * prev * 255, 0, 255);
        });
        _s.Clip = mask;
    }

    private void Raster(List<List<Vector2>> polys, Func<float, float, Color> paint, bool _)
    {
        var clip = _s.Clip; float ga = _s.Alpha;
        RasterTo(polys, (x, y, a) =>
        {
            int k = y * W + x;
            if (clip != null) { a *= clip[k] / 255f; if (a <= 0) return; }
            var c = paint(x + 0.5f, y + 0.5f);
            float sa = c.A / 255f * a * ga;
            if (sa <= 0.002f) return;
            var d = Px[k];
            float ia = 1 - sa;
            Px[k] = new Color(
                (byte)Math.Clamp(c.R * sa + d.R * ia, 0, 255), (byte)Math.Clamp(c.G * sa + d.G * ia, 0, 255),
                (byte)Math.Clamp(c.B * sa + d.B * ia, 0, 255), (byte)Math.Clamp(255 * sa + d.A * ia, 0, 255));
        });
    }

    private struct Edge { public float X0, Y0, X1, Y1; public int Dir; }

    private void RasterTo(List<List<Vector2>> polys, Action<int, int, float> plot)
    {
        var edges = new List<Edge>();
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in polys)
        {
            if (p.Count < 2) continue;
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[i]; var b = p[(i + 1) % p.Count];
                minX = MathF.Min(minX, a.X); maxX = MathF.Max(maxX, a.X); minY = MathF.Min(minY, a.Y); maxY = MathF.Max(maxY, a.Y);
                if (MathF.Abs(a.Y - b.Y) < 1e-6f) continue;
                edges.Add(a.Y < b.Y ? new Edge { X0 = a.X, Y0 = a.Y, X1 = b.X, Y1 = b.Y, Dir = 1 } : new Edge { X0 = b.X, Y0 = b.Y, X1 = a.X, Y1 = a.Y, Dir = -1 });
            }
        }
        if (edges.Count == 0) return;
        int y0 = Math.Max(0, (int)MathF.Floor(minY)), y1 = Math.Min(H - 1, (int)MathF.Ceiling(maxY));
        int x0 = Math.Max(0, (int)MathF.Floor(minX)), x1 = Math.Min(W - 1, (int)MathF.Ceiling(maxX));
        if (x0 > x1 || y0 > y1) return;
        var xs = new List<(float x, int dir)>(32);
        for (int y = y0; y <= y1; y++)
        {
            Array.Clear(_cov, x0, x1 - x0 + 2);
            for (int s = 0; s < 4; s++)
            {
                float sy = y + (s + 0.5f) / 4f;
                xs.Clear();
                foreach (var e in edges)
                    if (sy >= e.Y0 && sy < e.Y1) xs.Add((e.X0 + (sy - e.Y0) * (e.X1 - e.X0) / (e.Y1 - e.Y0), e.Dir));
                if (xs.Count < 2) continue;
                xs.Sort((a, b) => a.x.CompareTo(b.x));
                int wind = 0; float start = 0;
                foreach (var (x, dir) in xs)
                {
                    int prev = wind; wind += dir;
                    if (prev == 0 && wind != 0) start = x;
                    else if (prev != 0 && wind == 0) Span(start, x, 0.25f, x0, x1);
                }
            }
            for (int x = x0; x <= x1; x++)
            {
                float a = _cov[x];
                if (a > 0.003f) plot(x, y, MathF.Min(1, a));
            }
        }
    }

    private void Span(float a, float b, float w, int lo, int hi)
    {
        a = MathF.Max(a, lo); b = MathF.Min(b, hi + 1);
        if (b <= a) return;
        int ia = (int)MathF.Floor(a), ib = (int)MathF.Floor(b);
        if (ia == ib) { _cov[ia] += (b - a) * w; return; }
        _cov[ia] += (ia + 1 - a) * w;
        for (int i = ia + 1; i < ib; i++) _cov[i] += w;
        if (ib <= hi) _cov[ib] += (b - ib) * w;
    }

    // ---------------- gradients (device space) ----------------
    public Func<float, float, Color> Linear(float x0, float y0, float x1, float y1, params (float t, Color c)[] stops)
    {
        var p0 = _s.M.Apply(x0, y0); var p1 = _s.M.Apply(x1, y1);
        var d = p1 - p0; float l2 = Math.Max(1e-6f, d.LengthSquared());
        return (x, y) => Sample(stops, Vector2.Dot(new Vector2(x, y) - p0, d) / l2);
    }

    public Func<float, float, Color> Radial(float cx, float cy, float r, params (float t, Color c)[] stops)
    {
        var c = _s.M.Apply(cx, cy); float rr = r * DevScale;
        return (x, y) => Sample(stops, Vector2.Distance(new Vector2(x, y), c) / Math.Max(0.01f, rr));
    }

    private static Color Sample((float t, Color c)[] stops, float t)
    {
        if (t <= stops[0].t) return stops[0].c;
        for (int i = 1; i < stops.Length; i++)
            if (t <= stops[i].t)
            {
                float k = (t - stops[i - 1].t) / Math.Max(1e-6f, stops[i].t - stops[i - 1].t);
                var a = stops[i - 1].c; var b = stops[i].c;
                return new Color((byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k), (byte)(a.A + (b.A - a.A) * k));
            }
        return stops[^1].c;
    }

    // ---------------- output ----------------
    public Texture2D ToTexture(GraphicsDevice gd)
    {
        var t = new Texture2D(gd, W, H);
        t.SetData(Px);
        return t;
    }

    /// <summary>Split into square textures no larger than chunk pixels (keeps us under mobile texture limits).</summary>
    public List<(Texture2D tex, int x, int y)> ToChunks(GraphicsDevice gd, int chunk = 1024)
    {
        var o = new List<(Texture2D, int, int)>();
        for (int cy = 0; cy < H; cy += chunk)
            for (int cx = 0; cx < W; cx += chunk)
            {
                int w = Math.Min(chunk, W - cx), h = Math.Min(chunk, H - cy);
                var data = new Color[w * h];
                for (int y = 0; y < h; y++) Array.Copy(Px, (cy + y) * W + cx, data, y * w, w);
                var t = new Texture2D(gd, w, h);
                t.SetData(data);
                o.Add((t, cx, cy));
            }
        return o;
    }

    /// <summary>Box-filter downsample by 2 (for supersampled art).</summary>
    public Canvas Half()
    {
        var c = new Canvas(W / 2, H / 2);
        for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                var a = Px[(y * 2) * W + x * 2]; var b = Px[(y * 2) * W + x * 2 + 1]; var d = Px[(y * 2 + 1) * W + x * 2]; var e = Px[(y * 2 + 1) * W + x * 2 + 1];
                c.Px[y * c.W + x] = new Color((a.R + b.R + d.R + e.R) / 4, (a.G + b.G + d.G + e.G) / 4, (a.B + b.B + d.B + e.B) / 4, (a.A + b.A + d.A + e.A) / 4);
            }
        return c;
    }
}
