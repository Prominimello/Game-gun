using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Breachpoint.Gfx;

public enum FontKind { Regular, Bold, Mono }
public enum Align { Left, Center, Right }

/// <summary>
/// Immediate-mode GPU drawing: sprites, solid triangles/polygons and text, batched automatically.
/// All coordinates are in the space of the current transform (screen pixels or world units).
/// </summary>
public sealed class Painter
{
    public readonly GraphicsDevice GD;
    public readonly SpriteBatch SB;
    private readonly BasicEffect _fx;
    public readonly Texture2D Pixel, Disc, Glow;
    private readonly FontSystem _regular, _bold, _mono;
    private readonly List<VertexPositionColor> _tris = new(4096);
    private Matrix _transform = Matrix.Identity;
    private BlendState _blend = BlendState.AlphaBlend;
    private DepthStencilState _depth = DepthStencilState.None;
    private bool _spritesOpen;
    private Rectangle? _clip;
    private static readonly RasterizerState ScissorOn = new() { CullMode = CullMode.None, ScissorTestEnable = true };

    public static readonly DepthStencilState StencilWrite = new()
    {
        StencilEnable = true, StencilFunction = CompareFunction.Always, StencilPass = StencilOperation.Replace, ReferenceStencil = 1, DepthBufferEnable = false,
    };
    public static readonly DepthStencilState StencilOutside = new()
    {
        StencilEnable = true, StencilFunction = CompareFunction.NotEqual, StencilPass = StencilOperation.Keep, ReferenceStencil = 1, DepthBufferEnable = false,
    };
    public static readonly BlendState NoColor = new() { ColorWriteChannels = ColorWriteChannels.None };

    public Painter(GraphicsDevice gd)
    {
        GD = gd;
        SB = new SpriteBatch(gd);
        _fx = new BasicEffect(gd) { VertexColorEnabled = true, TextureEnabled = false, LightingEnabled = false };
        Pixel = new Texture2D(gd, 1, 1);
        Pixel.SetData(new[] { Color.White });
        Disc = MakeDisc(gd, 128, false);
        Glow = MakeDisc(gd, 128, true);
        var settings = new FontSystemSettings { FontResolutionFactor = 1, KernelWidth = 0, KernelHeight = 0 };
        _regular = new FontSystem(settings); _regular.AddFont(Resource("DejaVuSans.ttf"));
        _bold = new FontSystem(settings); _bold.AddFont(Resource("DejaVuSans-Bold.ttf"));
        _mono = new FontSystem(settings); _mono.AddFont(Resource("DejaVuSansMono.ttf"));
    }

    private static byte[] Resource(string name)
    {
        using var s = typeof(Painter).Assembly.GetManifestResourceStream("Breachpoint.Assets." + name)
            ?? throw new InvalidOperationException("Missing embedded font " + name);
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static Texture2D MakeDisc(GraphicsDevice gd, int n, bool soft)
    {
        var data = new Color[n * n];
        float c = (n - 1) / 2f, r = n / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = soft ? MathF.Max(0, 1 - d / r) : Math.Clamp(r - d, 0, 1);
                if (soft) a *= a;
                data[y * n + x] = Color.White * a;
            }
        var t = new Texture2D(gd, n, n);
        t.SetData(data);
        return t;
    }

    public SpriteFontBase Font(FontKind kind, float size)
    {
        int s = Math.Max(6, (int)Math.Round(size));
        return (kind switch { FontKind.Bold => _bold, FontKind.Mono => _mono, _ => _regular }).GetFont(s);
    }

    // ---------------- state ----------------
    public void Begin(Matrix transform, BlendState blend = null, DepthStencilState depth = null)
    {
        Flush();
        _transform = transform;
        _blend = blend ?? BlendState.AlphaBlend;
        _depth = depth ?? DepthStencilState.None;
    }

    public void Begin() => Begin(Matrix.Identity);

    /// <summary>Restrict drawing to a screen-pixel rectangle (null = no clipping).</summary>
    public void SetClip(Rectangle? r)
    {
        Flush();
        _clip = r;
        if (r.HasValue)
        {
            var vp = GD.Viewport.Bounds;
            GD.ScissorRectangle = Rectangle.Intersect(r.Value, vp);
        }
    }
    public Rectangle? Clip => _clip;
    public Matrix Transform => _transform;

    public void Flush()
    {
        if (_spritesOpen) { SB.End(); _spritesOpen = false; }
        FlushTris();
    }

    public void End() => Flush();

    private void Sprites()
    {
        if (_tris.Count > 0) FlushTris();
        if (!_spritesOpen)
        {
            SB.Begin(SpriteSortMode.Deferred, _blend, SamplerState.LinearClamp, _depth, _clip.HasValue ? ScissorOn : RasterizerState.CullNone, null, _transform);
            _spritesOpen = true;
        }
    }

    private void Tris()
    {
        if (_spritesOpen) { SB.End(); _spritesOpen = false; }
    }

    private void FlushTris()
    {
        if (_tris.Count == 0) return;
        var vp = GD.Viewport;
        _fx.World = _transform;
        _fx.View = Matrix.Identity;
        _fx.Projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);
        GD.BlendState = _blend;
        GD.DepthStencilState = _depth;
        GD.RasterizerState = _clip.HasValue ? ScissorOn : RasterizerState.CullNone;
        var arr = _tris.ToArray();
        foreach (var pass in _fx.CurrentTechnique.Passes)
        {
            pass.Apply();
            for (int i = 0; i < arr.Length; i += 30000)
                GD.DrawUserPrimitives(PrimitiveType.TriangleList, arr, i, Math.Min(10000, (arr.Length - i) / 3));
        }
        _tris.Clear();
    }

    // ---------------- primitives ----------------
    public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        Tris();
        _tris.Add(new VertexPositionColor(new Vector3(a, 0), col));
        _tris.Add(new VertexPositionColor(new Vector3(b, 0), col));
        _tris.Add(new VertexPositionColor(new Vector3(c, 0), col));
    }

    public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col) { Tri(a, b, c, col); Tri(a, c, d, col); }

    /// <summary>Horizontal or vertical gradient rectangle.</summary>
    public void Gradient(float x, float y, float w, float h, Color c0, Color c1, bool horizontal)
    {
        Tris();
        var a = new Vector3(x, y, 0); var b = new Vector3(x + w, y, 0); var c = new Vector3(x + w, y + h, 0); var d = new Vector3(x, y + h, 0);
        Color ca = c0, cb = horizontal ? c1 : c0, cc = c1, cd = horizontal ? c0 : c1;
        _tris.Add(new VertexPositionColor(a, ca)); _tris.Add(new VertexPositionColor(b, cb)); _tris.Add(new VertexPositionColor(c, cc));
        _tris.Add(new VertexPositionColor(a, ca)); _tris.Add(new VertexPositionColor(c, cc)); _tris.Add(new VertexPositionColor(d, cd));
    }

    /// <summary>Fan-triangulated polygon around a centre point (works for star-shaped polygons such as vision cones).</summary>
    public void Fan(Vector2 center, IReadOnlyList<float> xy, Color col)
    {
        int n = xy.Count / 2;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Tri(center, new Vector2(xy[i * 2], xy[i * 2 + 1]), new Vector2(xy[j * 2], xy[j * 2 + 1]), col);
        }
    }

    public void FillRect(float x, float y, float w, float h, Color c)
    {
        Sprites();
        SB.Draw(Pixel, new Vector2(x, y), null, c, 0, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0);
    }

    public void Rect(float x, float y, float w, float h, Color c, float t = 1)
    {
        FillRect(x, y, w, t, c); FillRect(x, y + h - t, w, t, c);
        FillRect(x, y + t, t, h - 2 * t, c); FillRect(x + w - t, y + t, t, h - 2 * t, c);
    }

    public void Line(float x0, float y0, float x1, float y1, float thick, Color c)
    {
        float dx = x1 - x0, dy = y1 - y0, len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f) return;
        Sprites();
        SB.Draw(Pixel, new Vector2(x0, y0), null, c, MathF.Atan2(dy, dx), new Vector2(0, 0.5f), new Vector2(len, thick), SpriteEffects.None, 0);
    }

    public void DashedLine(float x0, float y0, float x1, float y1, float thick, Color c, float dash = 4, float gap = 4)
    {
        float dx = x1 - x0, dy = y1 - y0, len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f) return;
        float ux = dx / len, uy = dy / len;
        for (float s = 0; s < len; s += dash + gap)
        {
            float e = MathF.Min(len, s + dash);
            Line(x0 + ux * s, y0 + uy * s, x0 + ux * e, y0 + uy * e, thick, c);
        }
    }

    /// <summary>Solid rectangle rotated about (ox, oy) given in the rectangle's local pixels.</summary>
    public void RotRect(float x, float y, float w, float h, float ox, float oy, float rot, Color c)
    {
        Sprites();
        SB.Draw(Pixel, new Vector2(x, y), null, c, rot, new Vector2(ox / Math.Max(0.001f, w), oy / Math.Max(0.001f, h)), new Vector2(w, h), SpriteEffects.None, 0);
    }

    public void Circle(float cx, float cy, float r, Color c)
    {
        Sprites();
        SB.Draw(Disc, new Vector2(cx, cy), null, c, 0, new Vector2(64, 64), r / 64f, SpriteEffects.None, 0);
    }

    public void GlowCircle(float cx, float cy, float r, Color c)
    {
        Sprites();
        SB.Draw(Glow, new Vector2(cx, cy), null, c, 0, new Vector2(64, 64), r / 64f, SpriteEffects.None, 0);
    }

    public void Ring(float cx, float cy, float r, float thick, Color c, float a0 = 0, float a1 = MathF.PI * 2, int seg = 0)
    {
        if (seg <= 0) seg = Math.Clamp((int)(r * Math.Abs(a1 - a0) / 3), 12, 96);
        float ri = r - thick / 2, ro = r + thick / 2;
        for (int i = 0; i < seg; i++)
        {
            float t0 = a0 + (a1 - a0) * i / seg, t1 = a0 + (a1 - a0) * (i + 1) / seg;
            var p0 = new Vector2(cx + MathF.Cos(t0) * ri, cy + MathF.Sin(t0) * ri);
            var p1 = new Vector2(cx + MathF.Cos(t0) * ro, cy + MathF.Sin(t0) * ro);
            var p2 = new Vector2(cx + MathF.Cos(t1) * ro, cy + MathF.Sin(t1) * ro);
            var p3 = new Vector2(cx + MathF.Cos(t1) * ri, cy + MathF.Sin(t1) * ri);
            Quad(p0, p1, p2, p3, c);
        }
    }

    public void DashedRing(float cx, float cy, float r, float thick, Color c, int dashes = 16)
    {
        float step = MathF.PI * 2 / dashes;
        for (int i = 0; i < dashes; i++) Ring(cx, cy, r, thick, c, i * step, i * step + step * 0.55f, 4);
    }

    public void Sprite(Texture2D tex, float x, float y, float rot = 0, float scale = 1, Color? color = null, Vector2? origin = null)
    {
        Sprites();
        SB.Draw(tex, new Vector2(x, y), null, color ?? Color.White, rot, origin ?? new Vector2(tex.Width / 2f, tex.Height / 2f), scale, SpriteEffects.None, 0);
    }

    public void Image(Texture2D tex, float x, float y, float w, float h, Color? color = null)
    {
        Sprites();
        SB.Draw(tex, new Rectangle((int)x, (int)y, (int)w, (int)h), color ?? Color.White);
    }

    public void ImageF(Texture2D tex, float x, float y, float sx, float sy, Color? color = null)
    {
        Sprites();
        SB.Draw(tex, new Vector2(x, y), null, color ?? Color.White, 0, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0);
    }

    // ---------------- text ----------------
    public Vector2 Measure(string s, float size, FontKind kind = FontKind.Regular) => string.IsNullOrEmpty(s) ? Vector2.Zero : Font(kind, size).MeasureString(s);

    public void Text(string s, float x, float y, float size, Color c, FontKind kind = FontKind.Regular, Align align = Align.Left)
    {
        if (string.IsNullOrEmpty(s)) return;
        var f = Font(kind, size);
        if (align != Align.Left)
        {
            float w = f.MeasureString(s).X;
            x -= align == Align.Center ? w / 2 : w;
        }
        Sprites();
        SB.DrawString(f, s, new Vector2(MathF.Round(x), MathF.Round(y)), c);
    }

    /// <summary>Word-wrapped text; returns height used.</summary>
    public float TextWrap(string s, float x, float y, float width, float size, Color c, FontKind kind = FontKind.Regular, bool draw = true)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var f = Font(kind, size);
        float lh = size * 1.3f, cy = y;
        foreach (var para in s.Split('\n'))
        {
            var words = para.Split(' ');
            string line = "";
            foreach (var w in words)
            {
                string test = line.Length == 0 ? w : line + " " + w;
                if (f.MeasureString(test).X > width && line.Length > 0)
                {
                    if (draw) { Sprites(); SB.DrawString(f, line, new Vector2(MathF.Round(x), MathF.Round(cy)), c); }
                    cy += lh; line = w;
                }
                else line = test;
            }
            if (draw) { Sprites(); SB.DrawString(f, line, new Vector2(MathF.Round(x), MathF.Round(cy)), c); }
            cy += lh;
        }
        return cy - y;
    }
}
