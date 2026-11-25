using Breachpoint.Core.Data;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;
using Microsoft.Xna.Framework;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Gfx;

/// <summary>Procedural art painted onto a <see cref="Canvas"/>: portraits, weapons, unit sprites, vehicles, icons.</summary>
public static class Art
{
    private const float PI = MathF.PI;

    public static (string shirt, string pants, string carrier, string helmet, string glove, string patch, bool camo) Uniform(Look L)
    {
        var u = GameData.Uniform.GetValueOrDefault(L.Uniform ?? "swat", GameData.Uniforms[0]);
        return (L.Shirt ?? u.Shirt, L.Pants ?? u.Pants, L.Carrier ?? u.Carrier, u.Helmet, u.Glove, u.Patch, u.Camo);
    }

    public static void Camo(Canvas c, string[] colors, uint seed, float x, float y, float w, float h, float size)
    {
        var r = new Rng(seed);
        int n = (int)(w * h / (size * size) * 1.4f);
        for (int i = 0; i < n; i++)
            c.FillEllipse(x + (float)r.Next() * w, y + (float)r.Next() * h, size * (float)r.Range(0.5, 1.3), size * (float)r.Range(0.3, 0.9), (float)r.Next() * 3, C.Hex(colors[i % colors.Length]));
    }

    // ===================== Portrait =====================
    public static void Portrait(Canvas c, Operator op, bool gear = true)
    {
        var L = op.Look; var uc = Uniform(L);
        var skin = C.Hex(GameData.Skin[Math.Clamp(L.Skin, 0, GameData.Skin.Length - 1)]);
        var hairC = C.Hex(GameData.HairColors[Math.Clamp(L.HairColor, 0, GameData.HairColors.Length - 1)]);
        uint seed = MathUtil.Hash(op.Id ?? op.Name);
        c.BeginPath(); c.Rect(0, 0, c.W, c.H); c.Fill(c.Linear(0, 0, 0, c.H, (0, C.Hex("#2a3440")), (1, C.Hex("#10151b"))));
        for (int i = 0; i < c.H; i += 4) c.FillRect(0, i, c.W, 1, Color.White * 0.03f);
        float s = c.W / 100f;
        c.Save();
        c.Translate(0, c.H - 100 * s); c.Scale(s, s);
        float build = new[] { 0.88f, 1f, 1.08f, 1.18f }[Math.Clamp(L.Build, 0, 3)];
        bool fem = op.Gender == "f";
        float sw = 42 * build * (fem ? 0.9f : 1);
        // torso
        c.BeginPath(); c.MoveTo(50 - sw, 100); c.QuadTo(50 - sw, 72, 36, 68); c.LineTo(64, 68); c.QuadTo(50 + sw, 72, 50 + sw, 100); c.ClosePath();
        c.Fill(C.Hex(uc.shirt));
        if (uc.camo) { c.Save(); c.Clip(); Camo(c, new[] { "#5e5a3d", "#a8946b", "#3f3b2b", "#71704f" }, seed, 0, 60, 100, 40, 6); c.Restore(); }
        if (gear)
        {
            var car = C.Hex(uc.carrier);
            c.FillRoundRect(50 - 24 * build, 76, 48 * build, 30, 4, car);
            for (int i = 0; i < 3; i++) c.FillRoundRect(50 - 20 * build + i * 14 * build, 86, 11 * build, 14, 2, C.Shade(car, -0.25f));
            c.FillRect(50 - 24 * build, 76, 48 * build, 3, C.Shade(car, 0.15f));
            c.FillRect(50 - 20 * build, 68, 7, 10, car); c.FillRect(50 + 13 * build, 68, 7, 10, car);
            if (!string.IsNullOrEmpty(uc.patch)) c.FillRoundRect(42, 79, 16, 4, 1, C.Hex("#d8d8d0") * 0.85f);
            if (L.Patch > 0)
            {
                float px = 50 - sw + 6, py = 80;
                string[] pc = { "#000000", "#2b4a7a", "#7a6a2b", "#8a2b2b", "#333333", "#222222", "#eeeeee" };
                c.FillRoundRect(px, py, 9, 7, 1.5f, C.Hex(pc[Math.Clamp(L.Patch, 0, 6)]));
                if (L.Patch == 1) { c.FillRect(px + 1, py + 4, 7, 1, C.Hex("#cc3333")); c.FillRect(px + 1, py + 2, 7, 1, Color.White); }
                if (L.Patch == 6) { c.FillRect(px + 3.5f, py + 1, 2, 5, C.Hex("#cc3333")); c.FillRect(px + 2, py + 2.5f, 5, 2, C.Hex("#cc3333")); }
                if (L.Patch == 5) c.FillCircle(px + 4.5f, py + 3, 2, C.Hex("#dddddd"));
            }
        }
        // neck & head
        c.FillRect(42, 56, 16, 16, C.Shade(skin, -0.12f));
        float[] faceWs = { 17, 19, 19.5f, 16, 20, 18 }; float[] jaws = { 0.75f, 0.95f, 1, 0.7f, 1, 0.85f };
        int fi = Math.Clamp(L.Face, 0, 5);
        float fw = faceWs[fi] * (fem ? 0.93f : 1), jaw = jaws[fi];
        float hx = 50, hy = 40;
        c.BeginPath();
        c.MoveTo(hx - fw, hy - 6); c.QuadTo(hx - fw, hy - 28, hx, hy - 28); c.QuadTo(hx + fw, hy - 28, hx + fw, hy - 6);
        c.QuadTo(hx + fw, hy + 12, hx + fw * jaw * 0.7f, hy + 18); c.QuadTo(hx, hy + 26 - (fem ? 2 : 0), hx - fw * jaw * 0.7f, hy + 18);
        c.QuadTo(hx - fw, hy + 12, hx - fw, hy - 6); c.ClosePath();
        c.Fill(skin);
        c.FillEllipse(hx - fw, hy, 3, 5, 0, skin); c.FillEllipse(hx + fw, hy, 3, 5, 0, skin);
        c.FillEllipse(hx + fw * 0.5f, hy + 4, fw * 0.4f, 18, 0, Color.Black * 0.08f);
        bool helmet = gear && L.Helmet && uc.helmet != null && op.Loadout.Helmet != "none";
        if (!helmet && !(L.Balaclava && gear)) Hair(c, L.Hair, hairC, hx, hy, fw);
        float ey = hy - 4;
        c.FillEllipse(hx - 7, ey, 3.4f, 1.9f, 0, C.Hex("#f4f1ea")); c.FillEllipse(hx + 7, ey, 3.4f, 1.9f, 0, C.Hex("#f4f1ea"));
        string[] irises = { "#4a3020", "#2d5a8a", "#3d6b3a", "#6b5a3a", "#222222" };
        var iris = C.Hex(irises[seed % 5]);
        c.FillCircle(hx - 6.6f, ey, 1.6f, iris); c.FillCircle(hx + 7.4f, ey, 1.6f, iris);
        c.FillCircle(hx - 6.6f, ey, 0.7f, Color.Black); c.FillCircle(hx + 7.4f, ey, 0.7f, Color.Black);
        int tilt = (int)((seed >> 3) % 3) - 1;
        c.StrokeLine(hx - 11, ey - 5 + tilt * 0.5f, hx - 3, ey - 5.5f, fem ? 1.2f : 1.9f, hairC);
        c.StrokeLine(hx + 3, ey - 5.5f, hx + 11, ey - 5 + tilt * 0.5f, fem ? 1.2f : 1.9f, hairC);
        c.BeginPath(); c.MoveTo(hx, ey + 1); c.LineTo(hx - 2, ey + 10); c.LineTo(hx + 1.5f, ey + 11); c.Stroke(1, C.Shade(skin, -0.35f));
        c.BeginPath(); c.MoveTo(hx - 5, ey + 17); c.QuadTo(hx, ey + 18 + seed % 2, hx + 5, ey + 17); c.Stroke(fem ? 1.6f : 1.2f, fem ? C.Hex("#9a4a4a") : C.Shade(skin, -0.45f));
        if (!(L.Balaclava && gear)) Facial(c, L.Facial, hairC, hx, hy, fw, jaw);
        if (L.Balaclava && gear)
        {
            c.BeginPath();
            c.MoveTo(hx - fw - 3, hy + 30); c.LineTo(hx - fw - 3, hy - 8); c.QuadTo(hx - fw - 2, hy - 31, hx, hy - 31);
            c.QuadTo(hx + fw + 2, hy - 31, hx + fw + 3, hy - 8); c.LineTo(hx + fw + 3, hy + 30); c.ClosePath();
            c.MoveTo(hx + 13, ey - 7); c.LineTo(hx + 13, ey + 4); c.LineTo(hx - 13, ey + 4); c.LineTo(hx - 13, ey - 7); c.ClosePath();
            c.Fill(C.Hex("#16181b"));
        }
        if (helmet)
        {
            var hc = C.Hex(uc.helmet);
            c.BeginPath();
            c.MoveTo(hx - fw - 4, hy - 6); c.QuadTo(hx - fw - 4, hy - 36, hx, hy - 36); c.QuadTo(hx + fw + 4, hy - 36, hx + fw + 4, hy - 6);
            c.LineTo(hx + fw + 1, hy - 6); c.LineTo(hx + fw - 2, hy - 14); c.LineTo(hx - fw + 2, hy - 14); c.LineTo(hx - fw - 1, hy - 6); c.ClosePath();
            c.Fill(hc);
            c.FillEllipse(hx - 6, hy - 28, 8, 3, -0.3f, C.Shade(hc, 0.12f));
            c.FillRect(hx - fw - 3, hy - 16, 4, 9, C.Shade(hc, -0.3f)); c.FillRect(hx + fw - 1, hy - 16, 4, 9, C.Shade(hc, -0.3f));
            c.FillRoundRect(hx - 5, hy - 36, 10, 5, 1, C.Hex("#111111"));
        }
        if (gear && L.EyePro > 0)
        {
            if (L.EyePro == 2)
            {
                c.FillRoundRect(hx - 15, ey - 6, 30, 11, 4, C.Hex("#1e1e1e") * 0.95f);
                c.FillRoundRect(hx - 13, ey - 4.5f, 26, 8, 3, new Color(170, 200, 220) * 0.55f);
                c.FillRect(hx - fw - 3, ey - 2, 6, 3, C.Hex("#111111")); c.FillRect(hx + fw - 3, ey - 2, 6, 3, C.Hex("#111111"));
            }
            else
            {
                var lens = L.EyePro == 3 ? C.Hex("#0f0f14") * 0.88f : new Color(200, 220, 235) * 0.35f;
                c.FillRoundRect(hx - 13, ey - 4, 11.5f, 7, 2.5f, lens); c.FillRoundRect(hx + 1.5f, ey - 4, 11.5f, 7, 2.5f, lens);
                c.StrokeLine(hx - 1.5f, ey - 2, hx + 1.5f, ey - 2, 0.8f, C.Hex("#222222"));
                c.StrokeLine(hx - 13, ey - 2, hx - fw, ey - 1, 0.8f, C.Hex("#222222"));
                c.StrokeLine(hx + 13, ey - 2, hx + fw, ey - 1, 0.8f, C.Hex("#222222"));
            }
        }
        if (gear && L.Headset == 2)
        {
            c.FillRoundRect(hx - fw - 6, hy - 8, 8, 15, 3, C.Hex("#2a2d2a")); c.FillRoundRect(hx + fw - 2, hy - 8, 8, 15, 3, C.Hex("#2a2d2a"));
            c.BeginPath(); c.MoveTo(hx - fw + 1, hy + 5); c.QuadTo(hx - 12, hy + 16, hx - 6, hy + 15); c.Stroke(1.2f, C.Hex("#222222"));
        }
        else if (gear && L.Headset == 1)
        {
            c.FillCircle(hx + fw, hy, 2.2f, C.Hex("#222222"));
            c.StrokeLine(hx + fw, hy + 2, hx + fw - 2, hy + 22, 0.7f, C.Hex("#333333"));
        }
        c.Restore();
    }

    private static void Hair(Canvas c, int style, Color col, float hx, float hy, float fw)
    {
        void Top(float alpha = 1)
        {
            c.BeginPath();
            c.MoveTo(hx - fw - 1, hy - 4); c.QuadTo(hx - fw - 2, hy - 32, hx, hy - 31); c.QuadTo(hx + fw + 2, hy - 32, hx + fw + 1, hy - 4);
            c.LineTo(hx + fw - 1, hy - 12); c.QuadTo(hx, hy - 24, hx - fw + 1, hy - 12); c.ClosePath();
            c.Fill(col * alpha);
        }
        switch (style)
        {
            case 0: Top(0.55f); break;
            case 8: break;
            case 1: case 2: Top(); break;
            case 3: Top(); c.FillEllipse(hx + 6, hy - 25, 10, 5, 0.3f, col); break;
            case 4: Top(); c.FillEllipse(hx, hy - 28, fw, 6, 0, col); break;
            case 5: Top(); c.FillCircle(hx, hy - 34, 7, col); break;
            case 6: Top(); c.FillEllipse(hx + fw + 3, hy + 6, 4, 14, -0.2f, col); break;
            case 7: Top(); c.FillRect(hx - fw - 3, hy - 10, 6, 34, col); c.FillRect(hx + fw - 3, hy - 10, 6, 34, col); break;
            case 9: for (int i = 0; i < 14; i++) c.FillCircle(hx - fw + (i % 7) * fw / 3, hy - 22 - (i / 7) * 6 - MathF.Sin(i) * 2, 5, col); break;
            case 10: Top(); for (int i = 0; i < 5; i++) c.FillRect(hx - fw + i * fw / 2, hy - 30, 3, 10, col); c.FillRect(hx + fw - 2, hy - 8, 4, 30, col); break;
            default: Top(); break;
        }
    }

    private static void Facial(Canvas c, int f, Color col, float hx, float hy, float fw, float jaw)
    {
        if (f <= 0) return;
        float ey = hy - 4;
        if (f == 1)
        {
            c.BeginPath(); c.MoveTo(hx - fw + 1, hy + 2); c.QuadTo(hx - fw, hy + 14, hx - fw * jaw * 0.7f, hy + 18); c.QuadTo(hx, hy + 27, hx + fw * jaw * 0.7f, hy + 18);
            c.QuadTo(hx + fw, hy + 14, hx + fw - 1, hy + 2); c.LineTo(hx + 8, hy + 10); c.LineTo(hx - 8, hy + 10); c.ClosePath(); c.Fill(col * 0.28f);
        }
        if (f >= 2) { c.BeginPath(); c.MoveTo(hx - 7, ey + 15); c.QuadTo(hx, ey + 12, hx + 7, ey + 15); c.LineTo(hx + 6, ey + 16.5f); c.QuadTo(hx, ey + 14, hx - 6, ey + 16.5f); c.ClosePath(); c.Fill(col); }
        if (f == 3) c.FillPoly(col, (hx - 4, ey + 19), (hx + 4, ey + 19), (hx + 2, ey + 26), (hx - 2, ey + 26));
        if (f >= 4)
        {
            float ex = f == 5 ? 3 : 0, ex2 = f == 5 ? 6 : 0;
            c.BeginPath(); c.MoveTo(hx - fw + 1, hy); c.QuadTo(hx - fw, hy + 16, hx - fw * jaw * 0.6f, hy + 22 + ex); c.QuadTo(hx, hy + 32 + ex2, hx + fw * jaw * 0.6f, hy + 22 + ex);
            c.QuadTo(hx + fw, hy + 16, hx + fw - 1, hy); c.LineTo(hx + fw - 4, hy + 4); c.QuadTo(hx + 6, hy + 10, hx + 6, ey + 19); c.LineTo(hx - 6, ey + 19);
            c.QuadTo(hx - 6, hy + 10, hx - fw + 4, hy + 4); c.ClosePath(); c.Fill(col * (f == 4 ? 0.75f : 0.95f));
        }
    }

    // ===================== Weapon side view =====================
    private sealed class Part { public string Role, Kind; public float[] A; public (float, float)[] P; }

    private static List<Part> WeaponParts(WeaponInstance w, out float len, out float minX)
    {
        var b = GameData.Weapon[w.Base]; var g = b.Geo; var att = w.Att;
        var P = new List<Part>();
        void R(string role, float x, float y, float ww, float hh, float r = 1.5f) => P.Add(new Part { Role = role, Kind = "r", A = new[] { x, y, ww, hh, r } });
        void Pl(string role, params (float, float)[] pts) => P.Add(new Part { Role = role, Kind = "p", P = pts });
        void Ci(string role, float x, float y, float r) => P.Add(new Part { Role = role, Kind = "c", A = new[] { x, y, r } });
        string At(string slot) => att.TryGetValue(slot, out var v) ? v : null;
        if (g.Pistol)
        {
            float L = (float)g.Recv * 2.6f;
            Pl("poly", (-6, 4), (L - 6, 4), (L - 6, 10), (8, 10), (2, 30), (-10, 30), (-6, 10));
            R("body", -12, -6, L + 2, 11, 2);
            for (int i = 0; i < 5; i++) R("dark", -9 + i * 3, -4, 1.2f, 7, 0);
            R("dark", L - 14, -2, 8, 3, 1);
            Pl("dark", (6, 10), (12, 10), (14, 17), (6, 17));
            if (At("mag") == "extmag") R("poly", -10, 29, 11, 9, 1);
            if (At("optic") == "rmr") { R("metal", 2, -12, 14, 7, 2); R("glass", 4, -11, 3, 5, 0); }
            if (At("side") != null) { R("dark", L * 0.45f, 10, 16, 8, 2); if (At("side") != "laser") Ci("light", L * 0.45f + 16, 14, 3); if (At("side") != "light") Ci("laser", L * 0.45f + 3, 16, 1.6f); }
            if (At("muzzle") == "psupp") R("dark", L - 10, -6, 42, 10, 3);
            else if (At("muzzle") == "comp") R("metal", L - 10, -6, 9, 10, 1);
            len = L + 40; minX = -14;
            return P;
        }
        float Rv = (float)g.Recv * 1.55f, Hd = (float)g.Hand * 1.55f, barrel = (float)g.Barrel * 1.55f;
        if (At("barrel") == "shortb") barrel *= 0.55f;
        if (At("barrel") == "longb") barrel *= 1.3f;
        float x0 = 0, x1 = Rv;
        string stock = At("stock") switch { "pdw" => "pdw", "precstock" => "prec", "ctr" => "ctr", _ => g.Stock };
        bool bulky = g.Bulky;
        if (stock is "collapsible" or "ctr")
        {
            R("dark", -34, -2, 36, 5, 1);
            Pl(g.Wood ? "wood" : "poly", (-58, -6), (-28, -6), (-26, 0), (-26, 10), (-34, 14), (-58, 16));
            if (stock == "ctr") R("poly", -54, -10, 18, 5, 2);
        }
        else if (stock == "folding") { Pl("poly", (-56, -5), (-4, -4), (-4, 4), (-20, 4), (-48, 14), (-56, 14)); R("dark", -48, -1, 26, 4, 1); }
        else if (stock == "fixed") Pl(g.Wood ? "wood" : "poly", (-66, -6), (-2, -4), (-2, 6), (-24, 10), (-66, 20));
        else if (stock is "chassis" or "prec") { Pl("body", (-70, -8), (-2, -5), (-2, 6), (-30, 8), (-50, 22), (-70, 22)); R("dark", -66, -14, 16, 6, 2); }
        else if (stock == "pdw") { R("dark", -26, -1, 26, 3, 1); R("dark", -26, 1, 4, 12, 1); R("poly", -30, 0, 6, 16, 2); }
        else if (stock == "bullpup") Pl("body", (-36, -8), (0, -8), (0, 14), (-36, 18));
        if (g.Ak) { Pl("body", (x0, -8), (x1, -8), (x1 + 4, -2), (x1, 6), (x0, 6)); R("dark", x0 + 4, -11, Rv - 8, 3, 1); }
        else R("body", x0, -8, Rv, bulky ? 17 : 14, 2);
        R("dark", x0 + Rv * 0.35f, -5, Rv * 0.3f, 4, 1);
        if (g.Rail || At("optic") != null) R("dark", x0 + 2, -12, Rv + Hd - 2, 4, 0.5f);
        if (g.Bolt) { R("metal", x0 + Rv * 0.25f, -11, 10, 3, 1); Ci("metal", x0 + Rv * 0.25f + 4, 0, 3); }
        if (stock != "bullpup" && g.MagT != "grip")
        {
            float ang = At("grip") == "steep" ? 4 : 10;
            Pl(At("grip") == "rubber" ? "rubber" : "poly", (x0 + 6, 6), (x0 + 17, 6), (x0 + 14 - ang * 0.3f, 30), (x0 + 3 - ang, 30));
        }
        Pl("dark", (x0 + 17, 6), (x0 + 30, 6), (x0 + 28, 14), (x0 + 18, 14));
        string mt = g.MagT;
        float magExt = At("mag") == "extmag" ? 1.4f : At("mag") == "drum" ? 2 : 1;
        float mx = x0 + Rv * 0.52f;
        if (At("mag") == "drum" && mt is "stanag" or "curved" or "curvedsmg" or "translucent") { R("dark", mx - 3, 6, 12, 8, 1); Ci("poly", mx + 3, 30, 15); Ci("dark", mx + 3, 30, 4); }
        else if (mt is "stanag" or "translucent")
        {
            Pl(mt == "translucent" ? "trans" : "mag", (mx - 2, 6), (mx + 12, 6), (mx + 15, 6 + 28 * magExt), (mx + 1, 8 + 28 * magExt));
            if (At("mag") == "coupler") Pl("mag2", (mx + 4, 8), (mx + 16, 8), (mx + 19, 34), (mx + 7, 36));
        }
        else if (mt is "curved" or "curvedsmg")
        {
            float k = mt == "curvedsmg" ? 0.8f : 1;
            Pl("mag", (mx - 2, 6), (mx + 12, 6), (mx + 18, 6 + 16 * k * magExt), (mx + 22, 6 + 30 * k * magExt), (mx + 8, 10 + 32 * k * magExt), (mx + 4, 8 + 16 * k * magExt));
        }
        else if (mt == "box") R("mag", mx - 2, 6, 15, 20 * magExt + 4, 1.5f);
        else if (mt == "tube") R("dark", x1, 4, Math.Min(barrel + Hd, ((float)g.Hand + 20) * 1.55f) * (At("mag") == "extmag" ? 1.2f : 0.85f), 6, 2);
        else if (mt == "grip") Pl("poly", (x0 + 8, 6), (x0 + 20, 6), (x0 + 17, 30 + 10 * (magExt - 1)), (x0 + 4, 30 + 10 * (magExt - 1)));
        else if (mt == "top") R("trans", x0 - 20, -14, Rv + 10, 6, 2);
        else if (mt == "internal") R("dark", mx, 6, 14, 5, 1);
        float hd = Hd;
        if (At("hand") == "shorthg") hd *= 0.7f;
        if (At("hand") == "longhg") hd *= 1.25f;
        if (hd > 0)
        {
            R(g.Wood && At("hand") == null ? "wood" : At("hand") == "mlok" ? "bodyalt" : "body", x1, -7, hd, bulky ? 14 : 12, 2);
            if (At("hand") == "mlok" || g.Rail) for (float i = 8; i < hd - 6; i += 9) R("dark", x1 + i, -3, 5, 3, 1);
        }
        float bx = x1 + Math.Max(hd, 4), bl = Math.Max(6, barrel - hd * 0.5f);
        R("metal", bx - 2, -3, bl + 2, At("barrel") == "heavyb" ? 6 : 4, 0.5f);
        float mzx = bx + bl;
        string mz = At("muzzle");
        if (mz == null && b.Cat != "sg") R("dark", mzx, -4, 8, 6, 1);
        if (mz is "supp" or "psupp") { R("dark", mzx - 2, -6, mz == "supp" ? 46 : 36, 10, 3); for (int i = 6; i < 40; i += 8) R("metal", mzx + i, -6, 1, 10, 0); }
        if (mz == "brake") { R("metal", mzx, -5, 13, 8, 1); R("dark", mzx + 3, -5, 2, 8, 0); R("dark", mzx + 8, -5, 2, 8, 0); }
        if (mz == "comp") { R("metal", mzx, -5, 11, 8, 1); Ci("dark", mzx + 7, -3, 1.3f); }
        string optic = At("optic");
        if (optic == null) { R("dark", bx - 4, -16, 3, 8, 0); R("dark", x0 + 4, -16, 6, 5, 1); }
        float ox = x0 + Rv * 0.25f;
        if (optic == "rds") { R("dark", ox, -22, 18, 11, 3); R("glass", ox + 14, -20, 3, 7, 1); }
        if (optic == "holo") { R("dark", ox, -24, 30, 13, 2); R("glass", ox + 4, -23, 22, 8, 1); }
        if (optic == "prism") { R("dark", ox - 2, -25, 38, 13, 3); Ci("glass", ox + 36, -18, 5); R("body", ox + 10, -30, 10, 5, 1); }
        if (optic is "lpvo" or "scope")
        {
            float ln = optic == "scope" ? 80 : 58, rr = optic == "scope" ? 8 : 6;
            R("dark", ox - 10, -16, 8, 4, 0); R("dark", ox + ln - 24, -16, 8, 4, 0);
            R("dark", ox - 14, -16 - rr * 2, ln, rr * 1.6f, 3); R("dark", ox - 20, -18 - rr * 2.2f, 14, rr * 2.4f, 3); R("dark", ox + ln - 22, -18 - rr * 2.2f, 16, rr * 2.4f, 3);
            R("metal", ox + ln * 0.35f, -24 - rr * 2, 8, 6, 1);
            Ci("glass", ox + ln - 6, -16 - rr * 1.2f, rr * 0.9f);
        }
        float ux = x1 + hd * 0.45f;
        string under = At("under");
        if (under == "vfg") R("poly", ux, 5, 9, 22, 3);
        if (under == "afg") Pl("poly", (ux - 10, 5), (ux + 14, 5), (ux + 14, 10), (ux - 4, 14));
        if (under == "stop") R("poly", ux + hd * 0.25f, 5, 5, 6, 2);
        if (under == "bipod")
        {
            float bpx = ux + hd * 0.3f;
            R("dark", bpx, 5, 10, 5, 1);
            Pl("metal", (bpx + 2, 9), (bpx + 5, 9), (bpx - 10, 38), (bpx - 13, 38));
            Pl("metal", (bpx + 5, 9), (bpx + 8, 9), (bpx + 18, 38), (bpx + 15, 38));
        }
        string side = At("side");
        if (side != null) { float sx = x1 + Math.Max(10, hd - 26); R("dark", sx, 0, 22, 9, 2); if (side != "laser") Ci("light", sx + 22, 4.5f, 3.6f); if (side != "light") Ci("laser", sx + 4, 6, 1.6f); }
        if (At("sling") != null) P.Insert(0, new Part { Role = "sling", Kind = "sl", A = new[] { -50f, 10, x1 + 10, 6 } });
        len = mzx + 50; minX = -72;
        return P;
    }

    private static void TracePart(Canvas c, Part p)
    {
        c.BeginPath();
        if (p.Kind == "r") c.RoundRect(p.A[0], p.A[1], p.A[2], p.A[3], p.A[4]);
        else if (p.Kind == "p") c.Poly(p.P);
        else if (p.Kind == "c") c.Circle(p.A[0], p.A[1], p.A[2]);
    }

    private static Color RoleColor(string role, Color body, bool camo, string finish) => role switch
    {
        "body" => body, "bodyalt" => C.Shade(body, 0.08f), "poly" => finish == "black" || !camo ? C.Shade(body, -0.1f) : C.Hex("#2a2b2d"),
        "dark" => C.Hex("#1a1b1d"), "metal" => C.Hex("#3d4043"), "glass" => C.Hex("#6fb7d8"), "wood" => C.Hex("#6b4425"), "rubber" => C.Hex("#151515"),
        "mag" => C.Hex("#232426"), "mag2" => C.Hex("#1c1d1f"), "trans" => new Color(140, 140, 120) * 0.85f, "light" => C.Hex("#f3f0c8"), "laser" => C.Hex("#ee3333"),
        _ => C.Hex("#333333"),
    };

    /// <summary>Paints the weapon centred at (cx, cy). yaw rotates it around the vertical axis (pseudo-3D).</summary>
    public static void Weapon(Canvas c, WeaponInstance w, float cx, float cy, float scale, float yaw = 0, bool wear = true)
    {
        var fin = GameData.Finish.GetValueOrDefault(w.Finish ?? "black", GameData.Finishes[0]);
        var body = C.Hex(w.Finish == "custom" ? w.Camo[0] : fin.Color);
        string[] camo = w.Finish == "custom" ? new[] { w.Camo[1], C.ToHex(C.Shade(w.Camo[0], -0.3f)), C.ToHex(C.Shade(w.Camo[1], 0.2f)) } : fin.Camo;
        bool hasCamo = camo != null && camo.Length > 0;
        var parts = WeaponParts(w, out float len, out float minX);
        float mid = (len + minX) / 2, cyaw = MathF.Cos(yaw), depth = MathF.Sin(yaw) * 7;
        if (MathF.Abs(cyaw) < 0.08f) cyaw = 0.08f * MathF.Sign(cyaw == 0 ? 1 : cyaw);
        uint seed = MathUtil.Hash(w.Uid ?? w.Base);
        c.Save();
        c.Translate(cx, cy); c.Scale(scale * cyaw, scale); c.Translate(-mid, 0);
        c.Save(); c.Translate(depth / MathF.Max(0.2f, MathF.Abs(cyaw)) * 0.6f, -MathF.Abs(depth) * 0.25f);
        foreach (var p in parts) { if (p.Kind == "sl") continue; TracePart(c, p); c.Fill(Color.Black * 0.55f); }
        c.Restore();
        foreach (var p in parts)
            if (p.Kind == "sl") { c.BeginPath(); c.MoveTo(p.A[0], p.A[1]); c.QuadTo((p.A[0] + p.A[2]) / 2, 46, p.A[2], p.A[3]); c.Stroke(3, C.Hex("#3a3a2e")); }
        foreach (var p in parts)
        {
            if (p.Kind == "sl") continue;
            TracePart(c, p);
            var col = RoleColor(p.Role, body, hasCamo, w.Finish);
            c.Fill(col);
            bool camoPart = hasCamo && (p.Role is "body" or "bodyalt" || (p.Role == "poly" && hasCamo));
            c.Save(); c.Clip();
            if (camoPart) Camo(c, camo, seed, -80, -40, len + 80, 80, 7);
            c.BeginPath(); c.Rect(-100, -60, len + 120, 120);
            c.Fill(c.Linear(0, -30, 0, 40, (0, Color.White * 0.12f), (0.4f, Color.Transparent), (1, Color.Black * 0.25f)));
            c.Restore();
            TracePart(c, p); c.Stroke(0.6f, Color.Black * 0.5f, true);
        }
        if (wear)
        {
            var r = new Rng(seed + 7);
            float wearAmt = (float)(100 - w.Condition) / 100f + 0.08f;
            foreach (var p in parts)
            {
                if (p.Role != "body") continue;
                TracePart(c, p); c.Save(); c.Clip();
                for (int i = 0; i < 18 * wearAmt; i++) { float x = (float)r.Range(-60, len), y = (float)r.Range(-20, 20); c.StrokeLine(x, y, x + (float)r.Range(-6, 6), y + (float)r.Range(-2, 2), 0.5f, new Color(200, 200, 190) * 0.35f); }
                for (int i = 0; i < 60 * wearAmt; i++) c.FillCircle((float)r.Range(-60, len), (float)r.Range(-20, 30), (float)r.Range(0.4, 1.6), new Color(90, 70, 40) * 0.25f);
                c.Restore();
            }
        }
        c.Restore();
    }

    // ===================== Top-down unit sprite =====================
    /// <summary>Paints a unit centred in the canvas, facing +X. Scale s pixels per world unit.</summary>
    public static void UnitSprite(Canvas c, Core.Sim.UnitLook look, string pose, string weaponCat, bool shield, bool suppressed, bool isOp, bool armor, float s)
    {
        float cx = c.W / 2f, cy = c.H / 2f;
        c.Save(); c.Translate(cx, cy); c.Scale(s, s);
        var skin = C.Hex(look.Skin ?? "#c99a70");
        if (pose is "dead" or "downed")
        {
            c.Rotate(PI);
            c.FillRoundRect(-16, -7, 22, 14, 6, C.Hex(look.Shirt));
            if (isOp && look.Carrier != null) c.FillRoundRect(-12, -6, 13, 12, 3, C.Hex(look.Carrier));
            c.FillRoundRect(-30, -6, 16, 5, 2, C.Hex(look.Pants)); c.FillRoundRect(-30, 1, 16, 5, 2, C.Hex(look.Pants));
            c.FillCircle(9, 0, 5.5f, look.Helmet != null ? C.Hex(look.Helmet) : skin);
            if (pose == "dead") c.FillRect(-12, -2, 4, 4, Color.Transparent);
            c.Restore();
            return;
        }
        c.FillEllipse(2, 3, 11, 9, 0, Color.Black * 0.35f);
        bool kneel = pose is "kneel" or "bound" or "secured";
        if (kneel) c.Scale(0.85f, 0.85f);
        bool hasW = weaponCat != null && pose is "normal";
        if (hasW)
        {
            float L = weaponCat switch { "hg" => 14, "smg" => 20, "ar" => 26, "br" => 30, "sg" => 27, "sr" => 34, _ => 22 };
            c.FillRect(4, -1.6f, L, 3.2f, C.Hex("#101112"));
            if (weaponCat != "hg") c.FillRect(-2, -1.2f, 8, 2.4f, C.Hex("#101112"));
            if (suppressed) c.FillRect(L + 2, -2, 8, 4, C.Hex("#101112"));
            var glove = look.Glove != null ? C.Hex(look.Glove) : skin;
            c.FillCircle(weaponCat == "hg" ? 9 : 14, 0, 2.8f, glove);
            c.FillCircle(5, weaponCat == "hg" ? 0 : 3, 2.6f, glove);
        }
        if (shield)
        {
            c.BeginPath(); c.Arc(-2, 0, 15, -1.0f, 1.0f); c.Arc(-2, 0, 11.5f, 1.0f, -1.0f, true); c.ClosePath();
            c.Fill(C.Hex("#26292d")); c.Stroke(1, C.Hex("#555555"), true);
            c.FillRect(11, -3, 2, 6, C.Hex("#7ec8e3"));
        }
        c.FillEllipse(0, 0, 6.5f, 9 + look.Build * 0.6f, 0, C.Hex(look.Shirt));
        if (look.Camo) { c.FillRect(-4, -8, 3, 5, new Color(60, 55, 35) * 0.6f); c.FillRect(1, 2, 4, 5, new Color(60, 55, 35) * 0.6f); c.FillRect(-3, 3, 4, 4, new Color(170, 150, 105) * 0.5f); }
        if (look.Carrier != null && isOp) { c.FillRoundRect(-5.5f, -7, 11, 14, 2.5f, C.Hex(look.Carrier)); c.FillRect(1, -6, 3, 12, Color.Black * 0.3f); }
        else if (armor) c.FillRoundRect(-4.5f, -6, 9, 12, 2, C.Hex("#2a2d2a"));
        if (pose == "surrender") { c.FillCircle(3, -10.5f, 2.6f, skin); c.FillCircle(3, 10.5f, 2.6f, skin); }
        if (pose is "restrained" or "bound" or "secured") { c.FillCircle(-6, -2.5f, 2.2f, skin); c.FillCircle(-6, 2.5f, 2.2f, skin); c.FillRect(-7.2f, -1, 2.4f, 2, C.Hex("#f2f2f2")); }
        if (!hasW && pose == "normal" && !shield) { c.FillCircle(2, -8, 2.4f, skin); c.FillCircle(2, 8, 2.4f, skin); }
        c.FillCircle(1, 0, 5.2f, skin);
        if (look.Helmet != null)
        {
            c.FillCircle(0.5f, 0, 5.6f, C.Hex(look.Helmet));
            c.FillCircle(-0.5f, -1.5f, 2.5f, Color.White * 0.12f);
            c.FillRect(3.5f, -1.2f, 2, 2.4f, C.Hex("#111111"));
        }
        else if (look.Hair != null) { c.BeginPath(); c.Arc(-0.5f, 0, 5, PI * 0.35f, PI * 1.65f); c.ClosePath(); c.Fill(C.Hex(look.Hair)); }
        if (look.Balaclava && look.Helmet == null) c.FillCircle(1, 0, 5.3f, C.Hex("#18191b"));
        c.Restore();
    }

    // ===================== Vehicle (top-down) =====================
    public static void Vehicle(Canvas c, VehicleDef def, VehicleInstance inst, float x, float y, float ang, string paintOverride = null)
    {
        float L = (float)def.Len * 32, Wd = (float)def.Wid * 32;
        c.Save(); c.Translate(x, y); c.Rotate(ang);
        var paint = C.Hex(paintOverride ?? inst?.Paint ?? def.Color);
        c.FillRoundRect(-L / 2 + 4, -Wd / 2 + 5, L, Wd, 10, Color.Black * 0.4f);
        c.FillRoundRect(-L / 2, -Wd / 2, L, Wd, 12, paint);
        c.BeginPath(); c.RoundRect(-L / 2, -Wd / 2, L, Wd, 12); c.Stroke(1.5f, Color.Black * 0.5f, true);
        foreach (float sx in new[] { -L * 0.32f, L * 0.3f })
            foreach (int sy in new[] { -1, 1 }) c.FillRoundRect(sx - 9, sy * Wd / 2 - (sy > 0 ? 4 : 2), 18, 6, 2, C.Hex("#0c0c0c"));
        c.FillRoundRect(L * 0.12f, -Wd / 2 + 6, L * 0.14f, Wd - 12, 4, C.Hex("#1b2733"));
        c.FillRoundRect(-L * 0.42f, -Wd / 2 + 7, L * 0.08f, Wd - 14, 3, C.Hex("#16202a"));
        c.FillRoundRect(-L * 0.33f, -Wd / 2 + 5, L * 0.44f, Wd - 10, 5, C.Shade(paint, def.Armored ? -0.1f : 0.08f));
        if (def.Armored) for (float i = -L * 0.3f; i < L * 0.1f; i += 10) c.StrokeLine(i, -Wd / 2 + 6, i, Wd / 2 - 6, 1, Color.Black * 0.4f);
        var acc = inst?.Accessories ?? new List<string>();
        if (acc.Contains("pushbar")) c.FillRect(L / 2 - 2, -Wd / 2 + 8, 6, Wd - 16, C.Hex("#111111"));
        if (acc.Contains("ladder"))
        {
            c.BeginPath(); c.Rect(-L * 0.3f, -Wd / 2 + 9, L * 0.38f, Wd - 18); c.Stroke(2, C.Hex("#888888"), true);
            for (float i = -L * 0.3f; i < L * 0.08f; i += 8) c.StrokeLine(i, -Wd / 2 + 9, i, Wd / 2 - 9, 2, C.Hex("#888888"));
        }
        string lights = inst?.Lights ?? "none";
        if (lights is "full" or "hidden")
        {
            float lx = lights == "full" ? L * 0.02f : L * 0.12f;
            if (lights == "full") c.FillRect(L * 0.02f, -Wd / 2 + 6, 6, Wd - 12, C.Hex("#222222"));
            c.FillRect(lx, -Wd / 2 + 7, 5, (Wd - 14) / 2, C.Hex("#ff2a2a"));
            c.FillRect(lx, 0, 5, (Wd - 14) / 2, C.Hex("#2a6aff"));
        }
        c.FillRect(L / 2 - 4, -Wd / 2 + 4, 3, 7, C.Hex("#fff6c8")); c.FillRect(L / 2 - 4, Wd / 2 - 11, 3, 7, C.Hex("#fff6c8"));
        c.Restore();
    }

    // ===================== Item icons =====================
    public static void Item(Canvas c, string type, float x, float y)
    {
        c.Save(); c.Translate(x, y);
        switch (type)
        {
            case "docs": c.Rotate(0.2f); c.FillRect(-7, -9, 14, 18, C.Hex("#eee8d8")); c.FillRect(-5, -7, 14, 18, C.Hex("#ddd5c0")); for (int i = 0; i < 5; i++) c.FillRect(-3, -4 + i * 3, 9, 1, C.Hex("#888888")); break;
            case "laptop": c.FillRect(-10, -7, 20, 14, C.Hex("#3a3d42")); c.FillRect(-8, -5, 16, 9, C.Hex("#5ab0ff") * 0.7f); break;
            case "phone": c.FillRect(-3.5f, -6, 7, 12, C.Hex("#111111")); c.FillRect(-2.5f, -4.5f, 5, 7, C.Hex("#33aa88")); break;
            case "drugs": for (int i = 0; i < 4; i++) c.FillRect(-10 + (i % 2) * 10, -8 + (i / 2) * 9, 9, 8, C.Hex("#f2f2ea")); c.BeginPath(); c.Rect(-10, -8, 19, 17); c.Stroke(1, C.Hex("#cc99aa"), true); break;
            case "cash": for (int i = 0; i < 3; i++) c.FillRect(-9 + i * 6, -6 + (i % 2) * 2, 6, 11, C.Hex("#5e8a4a")); c.FillRect(-9, -1, 18, 2, C.Hex("#e8e0c0")); break;
            case "guns": c.FillRect(-12, -8, 24, 16, C.Hex("#5a4a2e")); c.FillRect(-10, -5, 20, 2.5f, C.Hex("#111111")); c.FillRect(-10, 0, 18, 2.5f, C.Hex("#111111")); break;
            case "weapon": c.Rotate(0.6f); c.FillRect(-9, -1.5f, 18, 3, C.Hex("#111111")); c.FillRect(-4, 0, 3, 5, C.Hex("#111111")); break;
        }
        c.Restore();
    }
}
