using Microsoft.Xna.Framework;

namespace Breachpoint.Gfx;

/// <summary>Hex colour parsing and shading helpers (cached).</summary>
public static class Colors
{
    private static readonly Dictionary<string, Color> Cache = new();

    public static Color Hex(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Color.Magenta;
        if (Cache.TryGetValue(hex, out var c)) return c;
        var h = hex.TrimStart('#');
        if (h.Length == 3) h = string.Concat(h.Select(ch => new string(ch, 2)));
        uint n = Convert.ToUInt32(h.Length >= 6 ? h[..6] : h, 16);
        c = new Color((int)(n >> 16) & 255, (int)(n >> 8) & 255, (int)n & 255);
        Cache[hex] = c;
        return c;
    }

    public static Color Hex(string hex, float alpha) => Hex(hex) * alpha;

    /// <summary>amt &lt; 0 darkens, &gt; 0 lightens.</summary>
    public static Color Shade(Color c, float amt)
    {
        int F(int v) => (int)Math.Clamp(Math.Round(amt < 0 ? v * (1 + amt) : v + (255 - v) * amt), 0, 255);
        return new Color(F(c.R), F(c.G), F(c.B), c.A);
    }

    public static Color Shade(string hex, float amt) => Shade(Hex(hex), amt);

    public static Color A(this Color c, float a) => new Color(c.R, c.G, c.B) * a;

    public static readonly Color Bg = Hex("#0b0e12"), Panel = Hex("#121820"), Panel2 = Hex("#18202a"), Line = Hex("#26313d"), Text = Hex("#c9d3dc"),
        Dim = Hex("#7d8a96"), Accent = Hex("#e2a33a"), Accent2 = Hex("#3fb8a8"), Red = Hex("#d9534f"), Green = Hex("#5cb85c"), Blue = Hex("#4fc3f7"),
        Button = Hex("#1f2a36"), ButtonHot = Hex("#2a3847"), ButtonBorder = Hex("#33414f");

    public static string ToHex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
}
