using Breachpoint.Core.Map;
using Breachpoint.Core.Sim;
using Breachpoint.Core.Util;
using Breachpoint.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Gfx;

public sealed class Camera
{
    public float X, Y, Zoom = 1, Rot, TargetRot;
    public float Shake;
}

/// <summary>Everything the renderer needs for one frame.</summary>
public sealed class View
{
    public GameMap Map;
    public int Floor;
    public Camera Cam;
    public IReadOnlyList<Unit> Units = Array.Empty<Unit>();
    public IReadOnlyList<MissionItem> Items = Array.Empty<MissionItem>();
    public IReadOnlyList<Grenade> Grenades = Array.Empty<Grenade>();
    public IReadOnlyList<Cloud> Smokes = Array.Empty<Cloud>(), Gases = Array.Empty<Cloud>();
    public IReadOnlyList<HeardMarker> Heard = Array.Empty<HeardMarker>();
    public List<(Vector2 center, List<float> poly)> Vision = new();
    public List<CamSource> CamVision = new();
    public bool Night, Rain, ShowAll, ShowHints, ShowSpawns;
    public double GameTime, RealTime;
    public Texture2D VehicleTex;
    public Vector2 VehiclePos;
    public float VehicleAng;
    public Action<Painter, Matrix> Overlay;
}

public sealed class Particle
{
    public string Type;
    public float X, Y, X1, Y1, Vx, Vy, Life, Max, Size, Alpha = 0.3f, Ang;
    public int F;
    public Color Color;
    public bool Sup, Hidden, Checked, Stamp;
    public Team Team;
}

public sealed class Decal
{
    public string Type;
    public float X, Y, Size, Rot;
    public Color Color;
}

/// <summary>Draws a tactical map: floors, furniture, doors, units, effects, fog of war and extruded walls.</summary>
public sealed class MapRenderer
{
    private const int TS = GameMap.TS;
    private readonly BreachpointGame _g;
    private readonly GameMap _map;
    private readonly List<(Texture2D tex, int x, int y)>[] _base;
    private readonly List<Decal>[] _decals;
    private readonly List<WallTile>[] _walls;
    public readonly List<Particle> Particles = new();
    public readonly List<(float x, float y, int f, float t)> Flashes = new();
    public float Shake;
    public static bool DebugNoVision;
    private readonly Random _r = new();

    private struct WallTile { public int X, Y; public bool[] E; public bool High, Rock; public byte Furn; public Color? Tint; }

    public MapRenderer(BreachpointGame g, GameMap map)
    {
        _g = g; _map = map;
        int nf = map.Floors.Length;
        _base = new List<(Texture2D, int, int)>[nf];
        _decals = new List<Decal>[nf];
        _walls = new List<WallTile>[nf];
        for (int f = 0; f < nf; f++) _decals[f] = new List<Decal>();
    }

    public void Dispose()
    {
        foreach (var l in _base) if (l != null) foreach (var (t, _, _) in l) t.Dispose();
    }

    public void ClearEffects() { Particles.Clear(); Flashes.Clear(); foreach (var d in _decals) d.Clear(); }

    // ================= static base layer =================
    private static readonly Dictionary<byte, string> MatCol = new()
    {
        [Mat.Grass] = "#36502a", [Mat.Asphalt] = "#2c2e31", [Mat.Dirt] = "#5a4a35", [Mat.Gravel] = "#66625a", [Mat.Sidewalk] = "#80807b", [Mat.Wood] = "#6e4c2f",
        [Mat.TileFloor] = "#9ea6a8", [Mat.Carpet] = "#4a3e46", [Mat.Concrete] = "#62666a", [Mat.Marble] = "#cdc8be", [Mat.Metal] = "#4b5155", [Mat.Rock] = "#1d1b19",
    };

    public List<(Texture2D tex, int x, int y)> Base(int f)
    {
        if (_base[f] != null) return _base[f];
        var c = new Canvas(_map.W * TS, _map.H * TS);
        PaintBase(c, f);
        _base[f] = c.ToChunks(_g.GraphicsDevice, 1024);
        return _base[f];
    }

    private void PaintBase(Canvas c, int f)
    {
        var m = _map; var fl = m.Floors[f];
        var rng = new Rng(m.Seed * 7 + f + 1);
        for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
            {
                int k = y * m.W + x; byte t = fl.T[k];
                if (t == Tile.Void) continue;
                if (t == Tile.Wall && fl.Mat[k] == Mat.Rock) { FloorTile(c, x, y, Mat.Rock, rng, fl.Room[k]); continue; }
                FloorTile(c, x, y, fl.Mat[k], rng, fl.Room[k]);
                float px = x * TS, py = y * TS;
                if (t == Tile.Water)
                {
                    c.FillRect(px, py, TS, TS, C.Hex(f == 0 && m.Type == "shipyard" ? "#1d3a52" : "#3a8ab8"));
                    c.BeginPath(); c.MoveTo(px + (float)rng.Next() * 10, py + 10 + (float)rng.Next() * 10); c.QuadTo(px + 16, py + (float)rng.Next() * 30, px + 30, py + 12); c.Stroke(1, Color.White * 0.12f);
                }
                if (t == Tile.Stairs)
                {
                    c.FillRect(px, py, TS, TS, C.Hex("#4a4e52"));
                    for (int i = 0; i < 6; i++) c.FillRect(px + 2, py + 2 + i * 5, TS - 4, 3, C.Hex("#6a6e72"));
                    c.FillPoly(C.Accent, (px + 16, py + 6), (px + 22, py + 13), (px + 10, py + 13));
                    c.FillPoly(C.Accent, (px + 16, py + 26), (px + 22, py + 19), (px + 10, py + 19));
                }
                if (t == Tile.Fence)
                {
                    for (int i = 0; i < TS; i += 6) { c.StrokeLine(px + i, py, px + i + 6, py + TS, 1, C.Hex("#9aa0a4")); c.StrokeLine(px + i + 6, py, px + i, py + TS, 1, C.Hex("#9aa0a4")); }
                    c.FillRect(px + 14, py + 14, 4, 4, C.Hex("#7a8084"));
                }
            }
        var frng = new Rng(m.Seed * 13 + f);
        for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
            {
                int k = y * m.W + x; byte t = fl.T[k], fu = fl.Furn[k];
                if ((t == Tile.Furn || t == Tile.High) && fu != 0 && fu != Furn.Car && fu != Furn.Tree)
                {
                    if (fu == Furn.Container && m.Decor.Any(d => d.Type == "container" && x >= d.X && x < d.X + d.W && y >= d.Y && y < d.Y + d.H)) continue;
                    if (t == Tile.High && fu is Furn.Rack or Furn.Container) continue; // drawn as extruded blocks
                    Furniture(c, fu, x, y, frng);
                }
            }
        if (f == 0)
            foreach (var d in m.Decor.Where(d => d.Type == "car"))
            {
                bool horiz = d.W > d.H;
                var def = new Core.Data.VehicleDef("car", "car", 0, 0, (horiz ? d.W : d.H) * 0.95, (horiz ? d.H : d.W) * 0.9, d.Color, false);
                Art.Vehicle(c, def, new Core.State.VehicleInstance { Paint = d.Color, Lights = "none" }, (d.X + d.W / 2f) * TS, (d.Y + d.H / 2f) * TS, horiz ? 0 : MathF.PI / 2);
            }
        for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
                if (fl.Furn[y * m.W + x] == Furn.Tree) Furniture(c, Furn.Tree, x, y, frng);
    }

    private static void FloorTile(Canvas c, int x, int y, byte mat, Rng rng, int room)
    {
        float px = x * TS, py = y * TS;
        var col = C.Hex(MatCol.GetValueOrDefault(mat, "#555555"));
        if (mat == Mat.Carpet && room >= 0) col = C.Hex(new[] { "#4a3e46", "#3c4656", "#4c4a3a", "#3a4a40", "#5a3a36" }[room % 5]);
        if (mat == Mat.Wood && room >= 0) col = C.Hex(new[] { "#6e4c2f", "#7a5634", "#5e4128" }[room % 3]);
        c.FillRect(px, py, TS, TS, col);
        float R() => (float)rng.Next();
        switch (mat)
        {
            case Mat.Grass: for (int i = 0; i < 10; i++) c.FillRect(px + R() * TS, py + R() * TS, 2, 3, R() < 0.5f ? new Color(80, 110, 50) * 0.5f : new Color(30, 45, 20) * 0.5f); break;
            case Mat.Asphalt: for (int i = 0; i < 8; i++) c.FillRect(px + R() * TS, py + R() * TS, 2, 2, R() < 0.5f ? Color.White * 0.05f : Color.Black * 0.15f); break;
            case Mat.Dirt: case Mat.Gravel: for (int i = 0; i < 10; i++) c.FillCircle(px + R() * TS, py + R() * TS, R() * 1.8f + 0.5f, R() < 0.5f ? Color.White * 0.08f : Color.Black * 0.18f); break;
            case Mat.Sidewalk: c.BeginPath(); c.Rect(px + 0.5f, py + 0.5f, TS - 1, TS - 1); c.Stroke(1, Color.Black * 0.18f, true); break;
            case Mat.Wood: for (int i = 0; i < 4; i++) c.FillRect(px, py + i * 8, TS, 1, Color.Black * 0.18f); c.FillRect(px + ((x * 7 + y * 3) % 4) * 8, py, 1, TS, Color.White * 0.04f); break;
            case Mat.TileFloor: if ((x + y) % 2 == 0) c.FillRect(px, py, TS, TS, Color.White * 0.05f); else c.FillRect(px, py, TS, TS, Color.Black * 0.06f); c.FillRect(px, py + 15.5f, TS, 1, Color.Black * 0.15f); c.FillRect(px + 15.5f, py, 1, TS, Color.Black * 0.15f); break;
            case Mat.Carpet: for (int i = 0; i < 6; i++) c.FillRect(px + R() * TS, py + R() * TS, 3, 3, Color.White * 0.03f); break;
            case Mat.Concrete: c.FillRect(px + R() * 20, py + R() * 20, 12, 8, Color.Black * 0.06f); if ((x + y) % 4 == 0) { c.FillRect(px, py, TS, 1, Color.Black * 0.1f); c.FillRect(px, py, 1, TS, Color.Black * 0.1f); } break;
            case Mat.Marble: c.BeginPath(); c.MoveTo(px + R() * TS, py); c.QuadTo(px + R() * TS, py + TS / 2, px + R() * TS, py + TS); c.Stroke(1, new Color(120, 110, 100) * 0.25f); c.FillRect(px, py, TS, 1, Color.Black * 0.08f); break;
            case Mat.Metal: for (int i = 0; i < TS; i += 8) c.StrokeLine(px + i, py, px + i + 4, py + 4, 1, Color.White * 0.06f); break;
            case Mat.Rock: for (int i = 0; i < 6; i++) c.FillCircle(px + R() * TS, py + R() * TS, R() * 5, Color.White * 0.03f); break;
        }
    }

    private static void Furniture(Canvas c, byte type, int x, int y, Rng rng)
    {
        float px = x * TS, py = y * TS, cx = px + TS / 2f, cy = py + TS / 2f;
        void Rr(string col, float a, float b, float w, float h, float r = 2) => c.FillRoundRect(px + a, py + b, w, h, r, C.Hex(col));
        float R() => (float)rng.Next();
        c.FillRect(px + 4, py + 5, TS - 4, TS - 4, Color.Black * 0.3f);
        switch (type)
        {
            case Furn.Sofa: { var col = new[] { "#5a4a6a", "#6a3a2a", "#3a4a5a", "#4a5a3a" }[(int)(R() * 4)]; Rr(col, 2, 3, 28, 26, 5); c.FillRoundRect(px + 6, py + 7, 20, 18, 3, C.Shade(col, 0.12f)); c.FillRoundRect(px + 2, py + 3, 28, 6, 3, C.Shade(col, -0.2f)); break; }
            case Furn.Table: Rr("#6a4a2a", 3, 3, 26, 26, 3); Rr("#7d5a35", 5, 5, 22, 22, 2); c.FillCircle(cx - 4, cy, 3, C.Hex("#dddddd")); break;
            case Furn.Bed: Rr("#e0ddd5", 2, 2, 28, 28, 3); Rr(new[] { "#4a5a8a", "#8a4a4a", "#4a7a5a", "#7a6a4a" }[(int)(R() * 4)], 2, 12, 28, 18, 2); Rr("#ffffff", 6, 4, 20, 6, 3); break;
            case Furn.Desk: Rr("#5a3e26", 2, 4, 28, 22, 2); Rr("#1a1c20", 8, 6, 14, 4, 1); Rr("#3a7ab0", 9, 6.5f, 12, 3, 0); Rr("#dddddd", 18, 14, 8, 10, 0); break;
            case Furn.Shelf: Rr("#555a5e", 2, 6, 28, 20, 1); for (int i = 0; i < 5; i++) Rr(new[] { "#cc8844", "#4488cc", "#cc4444", "#88aa44", "#cccccc" }[(int)(R() * 5)], 4 + i * 5, 9 + (i % 2) * 7, 4, 6, 0.5f); break;
            case Furn.Counter: Rr("#a9adb0", 1, 1, 30, 30, 1); Rr("#c7cacc", 3, 3, 26, 26, 1); if (R() < 0.3f) c.FillCircle(cx, cy, 7, C.Hex("#8a9296")); break;
            case Furn.Crate: Rr("#7a5a32", 3, 3, 26, 26, 1); c.BeginPath(); c.Rect(px + 4, py + 4, 24, 24); c.Stroke(2, C.Hex("#4e3a1e"), true); c.StrokeLine(px + 4, py + 4, px + 28, py + 28, 2, C.Hex("#4e3a1e")); c.StrokeLine(px + 28, py + 4, px + 4, py + 28, 2, C.Hex("#4e3a1e")); break;
            case Furn.Cabinet: Rr("#3e2c1c", 2, 4, 28, 22, 1); c.FillRect(px + 15, py + 6, 1, 18, C.Hex("#6a5038")); c.FillRect(px + 12, py + 14, 2, 3, C.Hex("#cccccc")); c.FillRect(px + 18, py + 14, 2, 3, C.Hex("#cccccc")); break;
            case Furn.Lab: Rr("#8a9298", 1, 2, 30, 26, 1); for (int i = 0; i < 4; i++) c.FillCircle(px + 7 + R() * 18, py + 7 + R() * 14, 2.5f + R() * 2, C.Hex(new[] { "#33cc66", "#cc9933", "#3399cc", "#dddddd", "#cc33cc" }[(int)(R() * 5)]) * 0.8f); Rr("#eeeeee", 4, 20, 10, 6, 0); break;
            case Furn.Barrel: { var col = C.Hex(new[] { "#2a4a7a", "#7a2a2a", "#4a5a2a", "#555555" }[(int)(R() * 4)]); c.FillCircle(cx, cy, 12, col); c.BeginPath(); c.Circle(cx, cy, 9); c.Stroke(2, C.Shade(col, 0.25f), true); c.FillCircle(cx + 4, cy - 3, 2, C.Hex("#111111")); break; }
            case Furn.Toilet: c.FillEllipse(cx, cy + 3, 7, 9, 0, C.Hex("#f0f0ee")); Rr("#f0f0ee", 8, 4, 16, 6, 2); c.FillEllipse(cx, cy + 4, 4, 6, 0, C.Hex("#bbccdd")); break;
            case Furn.Tree:
            {
                c.FillCircle(cx + 5, cy + 6, 20, Color.Black * 0.3f);
                var g = C.Hex(new[] { "#23401e", "#2c4a22", "#1e3a1a" }[(int)(R() * 3)]);
                for (int i = 0; i < 6; i++) c.FillCircle(cx + (R() - 0.5f) * 16, cy + (R() - 0.5f) * 16, 10 + R() * 6, i < 3 ? g : C.Shade(g, 0.15f));
                break;
            }
            case Furn.Bush: { var g = C.Hex("#2f4f24"); for (int i = 0; i < 4; i++) c.FillCircle(cx + (R() - 0.5f) * 12, cy + (R() - 0.5f) * 12, 7 + R() * 4, i % 2 == 1 ? g : C.Shade(g, 0.12f)); break; }
            case Furn.Hay: Rr("#c8a84a", 2, 4, 28, 24, 2); for (int i = 0; i < 6; i++) c.StrokeLine(px + 4 + i * 4, py + 6, px + 6 + i * 4, py + 26, 1, new Color(120, 90, 30) * 0.6f); break;
            case Furn.Chair: Rr("#3a3a3a", 9, 9, 14, 14, 3); Rr("#2a2a2a", 9, 7, 14, 4, 1); break;
            case Furn.Plant: c.FillCircle(cx, cy, 8, C.Hex("#6a4a3a")); for (int i = 0; i < 7; i++) c.FillEllipse(cx + MathF.Cos(i) * 6, cy + MathF.Sin(i) * 6, 6, 3, i, C.Hex(i % 2 == 1 ? "#3a7a3a" : "#2a6a2a")); break;
            case Furn.Safe: Rr("#2e3236", 3, 3, 26, 26, 2); Rr("#4a5056", 5, 5, 22, 22, 1); c.FillCircle(cx, cy, 5, C.Hex("#aaaaaa")); break;
            case Furn.Bunk: Rr("#777777", 3, 2, 26, 28, 1); Rr("#9a9a8a", 5, 4, 22, 24, 1); Rr("#cccccc", 7, 5, 18, 5, 2); break;
            case Furn.Locker: for (int i = 0; i < 3; i++) Rr("#5a6670", 2 + i * 10, 6, 9, 20, 1); for (int i = 0; i < 3; i++) c.FillRect(px + 4 + i * 10, py + 10, 5, 1, C.Hex("#222222")); break;
            default: Rr("#555555", 4, 4, 24, 24, 2); break;
        }
    }

    // ================= walls =================
    private List<WallTile> Walls(int f)
    {
        if (_walls[f] != null) return _walls[f];
        var m = _map; var fl = m.Floors[f]; var list = new List<WallTile>();
        int T(int x, int y) => m.InBounds(x, y) ? fl.T[y * m.W + x] : -1;
        var decorCols = new Dictionary<(int, int), Color>();
        foreach (var d in m.Decor.Where(d => d.Type == "container"))
            for (int yy = d.Y; yy < d.Y + d.H; yy++) for (int xx = d.X; xx < d.X + d.W; xx++) decorCols[(xx, yy)] = C.Hex(d.Color);
        for (int y = 0; y < m.H; y++)
            for (int x = 0; x < m.W; x++)
            {
                int k = y * m.W + x; byte v = fl.T[k];
                if (v == Tile.Wall && fl.Mat[k] != Mat.Rock)
                    list.Add(new WallTile { X = x, Y = y, E = new[] { T(x, y - 1) != Tile.Wall, T(x + 1, y) != Tile.Wall, T(x, y + 1) != Tile.Wall, T(x - 1, y) != Tile.Wall } });
                else if (v == Tile.High && fl.Furn[k] != Furn.Tree)
                    list.Add(new WallTile
                    {
                        X = x, Y = y, High = true, Furn = fl.Furn[k], Tint = decorCols.TryGetValue((x, y), out var dc) ? dc : null,
                        E = new[] { T(x, y - 1) != Tile.High, T(x + 1, y) != Tile.High, T(x, y + 1) != Tile.High, T(x - 1, y) != Tile.High },
                    });
                else if (v == Tile.Wall && fl.Mat[k] == Mat.Rock)
                {
                    int[] n = { T(x, y - 1), T(x + 1, y), T(x, y + 1), T(x - 1, y) };
                    if (n.Any(q => q != Tile.Wall && q != -1)) list.Add(new WallTile { X = x, Y = y, Rock = true, E = n.Select(q => q != Tile.Wall && q != -1).ToArray() });
                }
            }
        return _walls[f] = list;
    }

    // ================= effects =================
    public void Spawn(FxEvent e)
    {
        Particle Add(string type, float life, float size = 2) { var p = new Particle { Type = type, X = (float)e.X, Y = (float)e.Y, F = e.F, Life = life, Max = life, Size = size }; Particles.Add(p); return p; }
        float Rn() => (float)_r.NextDouble();
        switch (e.Type)
        {
            case "muzzle":
            {
                var p = Add("flash", 0.06f); p.Ang = (float)e.Ang; p.Sup = e.Sup;
                var s = Add("smoke", 0.6f, 4); s.Vx = MathF.Cos((float)e.Ang) * 20; s.Vy = MathF.Sin((float)e.Ang) * 20; s.Alpha = 0.2f; s.Color = C.Hex("#999999");
                break;
            }
            case "shell": { var p = Add("shell", 0.35f); float sp = 60 + Rn() * 50; p.Vx = MathF.Cos((float)e.Ang) * sp; p.Vy = MathF.Sin((float)e.Ang) * sp; p.Color = C.Hex("#d9b25a"); p.Stamp = true; break; }
            case "tracer": { var p = Add("tracer", 0.07f); p.X1 = (float)e.X1; p.Y1 = (float)e.Y1; p.Team = e.Team; break; }
            case "impact":
            {
                var col = e.Kind == "metal" ? C.Hex("#ffd27a") : e.Kind is "door" or "furn" or "wood" ? C.Hex("#8a6a40") : C.Hex("#a8a49c");
                for (int i = 0; i < 5; i++) { float a = (float)e.Ang + MathF.PI + (Rn() - 0.5f) * 2; var p = Add("spark", 0.25f, e.Kind == "metal" ? 1.5f : 2); float sp = 60 + Rn() * 120; p.Vx = MathF.Cos(a) * sp; p.Vy = MathF.Sin(a) * sp; p.Color = col; }
                var s = Add("smoke", 0.5f, 5); s.Alpha = 0.25f; s.Color = C.Hex("#bbbbbb");
                AddDecal(e.F, new Decal { Type = "hole", X = (float)e.X, Y = (float)e.Y, Size = 1.6f, Color = new Color(20, 20, 20) * 0.7f });
                break;
            }
            case "blood":
                for (int i = 0; i < 7; i++) { float a = (float)e.Ang + (Rn() - 0.5f) * 1.2f; var p = Add("drop", 0.25f + Rn() * 0.2f, 1.5f + Rn() * 1.5f); float sp = 40 + Rn() * 140; p.Vx = MathF.Cos(a) * sp; p.Vy = MathF.Sin(a) * sp; p.Color = new Color(110, 10, 10) * 0.8f; p.Stamp = true; }
                break;
            case "pool": { var p = Add("pool", 4, 14 + Rn() * 6); p.X += (Rn() - 0.5f) * 6; p.Y += (Rn() - 0.5f) * 6; break; }
            case "glass": for (int i = 0; i < 10; i++) { float a = Rn() * MathF.PI * 2; var p = Add("drop", 0.3f, 1.5f); float sp = 30 + Rn() * 80; p.Vx = MathF.Cos(a) * sp; p.Vy = MathF.Sin(a) * sp; p.Color = new Color(170, 210, 230) * 0.8f; p.Stamp = true; } break;
            case "splinters":
                for (int i = 0; i < 14; i++) { float a = Rn() * MathF.PI * 2; var p = Add("drop", 0.35f, 2); float sp = 40 + Rn() * 120; p.Vx = MathF.Cos(a) * sp; p.Vy = MathF.Sin(a) * sp; p.Color = C.Hex("#8a6a40"); p.Stamp = true; }
                { var s = Add("smoke", 1, 14); s.Alpha = 0.35f; s.Color = C.Hex("#aaaaaa"); }
                break;
            case "explosion":
                Add("fire", 0.35f, (float)(e.R > 0 ? e.R : 50));
                for (int i = 0; i < 10; i++) { float a = Rn() * MathF.PI * 2; var s = Add("smoke", 2 + Rn(), 16 + Rn() * 10); s.Max = 3; s.Vx = MathF.Cos(a) * 40; s.Vy = MathF.Sin(a) * 40; s.Alpha = 0.5f; s.Color = C.Hex("#666666"); }
                AddDecal(e.F, new Decal { Type = "scorch", X = (float)e.X, Y = (float)e.Y, Size = 36, Color = Color.Black * 0.55f });
                Shake = Math.Max(Shake, 10);
                break;
            case "flash":
                Add("bang", 0.25f, e.Big ? 140 : 60);
                for (int i = 0; i < 6; i++) { var s = Add("smoke", 2, 10); s.Vx = (Rn() - 0.5f) * 50; s.Vy = (Rn() - 0.5f) * 50; s.Alpha = 0.35f; s.Color = C.Hex("#cccccc"); }
                Flashes.Add(((float)e.X, (float)e.Y, e.F, 0));
                Shake = Math.Max(Shake, 5);
                AddDecal(e.F, new Decal { Type = "scorch", X = (float)e.X, Y = (float)e.Y, Size = 9, Color = Color.Black * 0.35f });
                break;
        }
    }

    private void AddDecal(int f, Decal d)
    {
        if (f < 0 || f >= _decals.Length) return;
        var l = _decals[f];
        l.Add(d);
        if (l.Count > 1600) l.RemoveRange(0, 200);
    }

    public void UpdateParticles(float dt)
    {
        foreach (var p in Particles)
        {
            p.Life -= dt; p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vx *= 0.9f; p.Vy *= 0.9f;
            if (p.Type == "smoke") p.Size += dt * 10;
            if (p.Life <= 0)
            {
                if (p.Type == "pool") AddDecal(p.F, new Decal { Type = "pool", X = p.X, Y = p.Y, Size = p.Size, Rot = p.X % 3, Color = new Color(90, 5, 5) * 0.8f });
                else if (p.Stamp) AddDecal(p.F, new Decal { Type = p.Type == "shell" ? "shell" : "dot", X = p.X, Y = p.Y, Size = p.Size, Color = p.Color, Rot = p.Vx });
            }
        }
        Particles.RemoveAll(p => p.Life <= 0);
        for (int i = 0; i < Flashes.Count; i++) Flashes[i] = (Flashes[i].x, Flashes[i].y, Flashes[i].f, Flashes[i].t + dt);
        Flashes.RemoveAll(f => f.t > 1.2f);
        if (Shake > 0) Shake = Math.Max(0, Shake - dt * 25);
    }

    // ================= main draw =================
    public Matrix WorldMatrix(Camera cam, int pxW, int pxH, float sx = 0, float sy = 0) =>
        Matrix.CreateTranslation(-cam.X, -cam.Y, 0) * Matrix.CreateRotationZ(cam.Rot) * Matrix.CreateScale(cam.Zoom, cam.Zoom, 1) * Matrix.CreateTranslation(pxW / 2f + sx, pxH / 2f + sy, 0);

    public static Vector2 ToScreen(Matrix m, float x, float y) => Vector2.Transform(new Vector2(x, y), m);

    public void Draw(Painter P, View v)
    {
        var gd = P.GD;
        int W = gd.Viewport.Width, H = gd.Viewport.Height;
        float sx = Shake > 0 ? ((float)_r.NextDouble() - 0.5f) * Shake : 0, sy = Shake > 0 ? ((float)_r.NextDouble() - 0.5f) * Shake : 0;
        var M = WorldMatrix(v.Cam, W, H, sx, sy);
        var labels = new List<(string text, Vector2 pos, Color col, float size)>();
        float t = (float)v.RealTime;
        P.Begin(M);
        if (v.Floor > 0 && _map.Type != "bunker") foreach (var (tex, x, y) in Base(0)) P.ImageF(tex, x, y, 1, 1, Color.White * 0.28f);
        foreach (var (tex, x, y) in Base(v.Floor)) P.ImageF(tex, x, y, 1, 1);
        foreach (var d in _decals[v.Floor])
        {
            switch (d.Type)
            {
                case "pool": P.Circle(d.X, d.Y, d.Size, d.Color); break;
                case "scorch": P.GlowCircle(d.X, d.Y, d.Size * 1.4f, d.Color); break;
                case "shell": P.RotRect(d.X, d.Y, d.Size, d.Size * 0.5f, d.Size / 2, d.Size / 4, d.Rot, d.Color); break;
                default: P.FillRect(d.X - d.Size / 2, d.Y - d.Size / 2, d.Size, d.Size, d.Color); break;
            }
        }
        if (v.VehicleTex != null && v.Floor == 0)
        {
            P.Sprite(v.VehicleTex, v.VehiclePos.X, v.VehiclePos.Y, v.VehicleAng);
            if (Math.Floor(t * 6) % 2 == 0) P.GlowCircle(v.VehiclePos.X, v.VehiclePos.Y, 50, new Color(255, 40, 40, 0) * 0.25f);
            else P.GlowCircle(v.VehiclePos.X, v.VehiclePos.Y, 50, new Color(40, 80, 255, 0) * 0.25f);
        }
        if (v.ShowSpawns)
            foreach (var s in _map.Spawns)
            {
                float cx = (s.X + 0.5f) * TS, cy = (s.Y + 0.5f) * TS;
                P.DashedRing(cx, cy, 2.5f * TS, 1.5f / v.Cam.Zoom * 1.5f, C.Accent * 0.7f, 24);
                labels.Add(("ENTRY " + s.Name.ToUpperInvariant(), new Vector2(cx, cy - 2.9f * TS), C.Accent, 12));
            }
        DrawWindows(P, v.Floor);
        DrawDoors(P, v.Floor, labels);
        // items
        foreach (var it in v.Items)
        {
            if (it.F != v.Floor || it.Collected) continue;
            if (!it.Seen && !v.ShowAll) foreach (var (_, poly) in v.Vision) if (PointInPoly(it.X, it.Y, poly)) { it.Seen = true; break; }
            if (!it.Seen && !v.ShowAll) continue;
            if (it.Kind != "drop") P.Ring((float)it.X, (float)it.Y, 14 + MathF.Sin(t * 4) * 2, 1.5f, it.Kind == "evidence" ? C.Blue * 0.8f : C.Accent * 0.8f);
            P.Sprite(_g.Art.ItemIcon(it.Type), (float)it.X, (float)it.Y, 0, 0.5f);
        }
        foreach (var gcl in v.Gases) if (gcl.F == v.Floor) P.GlowCircle((float)gcl.X, (float)gcl.Y, (float)gcl.R * 1.2f, new Color(200, 210, 150) * 0.45f);
        // units (bodies first)
        foreach (var u in v.Units.Where(u => u.Floor == v.Floor && !u.Escaped && !u.Evacuated && (u.Team == Team.Op || u.Visible || v.ShowAll))
                     .OrderBy(u => u.Dead || u.Downed || u.Incap ? 0 : 1))
            DrawUnit(P, u, t, labels);
        if (v.ShowHints)
            foreach (var u in v.Units)
                if (u.Floor == v.Floor && u.Team == Team.Enemy && u.Alive && !u.Visible && !u.Restrained) P.Ring((float)u.X, (float)u.Y, 8, 1.5f, new Color(255, 80, 80) * 0.35f);
        foreach (var g in v.Grenades)
        {
            if (g.F != v.Floor) continue;
            P.Circle((float)g.X + 2, (float)g.Y + 3, 3.5f, Color.Black * 0.4f);
            var gc = g.Type switch { "flash" => C.Hex("#cfd6d0"), "smoke" => C.Hex("#6a8a5a"), "gas" => C.Hex("#b0b05a"), _ => C.Hex("#d06040") };
            P.Circle((float)g.X, (float)(g.Y - g.Z * 0.6), 3.5f, gc);
        }
        DrawParticles(P, v.Floor);
        foreach (var s in v.Smokes)
        {
            if (s.F != v.Floor) continue;
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f + t * 0.15f, r = (float)s.R * 0.45f;
                P.Circle((float)s.X + MathF.Cos(a) * r, (float)s.Y + MathF.Sin(a) * r, (float)s.R * 0.7f, new Color(190, 195, 190) * 0.42f);
            }
        }
        // fog of war (stencil)
        if (!v.ShowAll)
        {
            P.Begin(M, Painter.NoColor, Painter.StencilWrite);
            if (!DebugNoVision) foreach (var (c, poly) in v.Vision) P.Fan(c, poly, Color.White);
            foreach (var cs in v.CamVision) P.Circle((float)cs.X, (float)cs.Y, 6 * TS, Color.White);
            P.Begin(Matrix.Identity, BlendState.AlphaBlend, Painter.StencilOutside);
            P.FillRect(0, 0, W, H, v.Night ? new Color(2, 4, 8) * 0.9f : new Color(5, 8, 12) * 0.66f);
            if (v.Night)
            {
                P.Begin(M, BlendState.Additive);
                foreach (var (c, poly) in v.Vision) P.Fan(c, poly, new Color(40, 38, 25) * 0.25f);
            }
        }
        // walls (screen space extrusion)
        P.Begin(Matrix.Identity);
        DrawWalls(P, v, M, W, H);
        // lights
        P.Begin(M, BlendState.Additive);
        foreach (var p in Particles)
        {
            if (p.F != v.Floor) continue;
            float k = p.Life / p.Max;
            if (p.Type == "flash")
            {
                if (!p.Checked) { p.Checked = true; p.Hidden = !v.ShowAll && !v.Vision.Any(q => PointInPoly(p.X, p.Y, q.poly)); }
                if (p.Hidden) continue;
                P.GlowCircle(p.X, p.Y, p.Sup ? 10 : 34, new Color(255, 230, 150) * (0.9f * k));
                if (!p.Sup) P.RotRect(p.X, p.Y, 14, 6, 0, 3, p.Ang, new Color(255, 240, 200) * k);
            }
            if (p.Type == "fire") P.GlowCircle(p.X, p.Y, p.Size, new Color(255, 160, 60) * k);
            if (p.Type == "bang") P.GlowCircle(p.X, p.Y, p.Size, Color.White * k);
        }
        // UI overlay in world space
        P.Begin(M);
        v.Overlay?.Invoke(P, M);
        // heard markers & last known positions
        foreach (var h in v.Heard)
        {
            if (h.F != v.Floor) continue;
            float age = (float)(v.GameTime - h.T); if (age < 0 || age > 2.5f) continue;
            for (int i = 0; i < 3; i++) P.Ring((float)h.X, (float)h.Y, 10 + age * 40 + i * 10, 2 / v.Cam.Zoom, new Color(255, 120, 80) * (0.7f * (1 - age / 2.5f)));
        }
        if (!v.ShowAll)
            foreach (var u in v.Units)
            {
                if (u.Team != Team.Enemy || u.Visible || !u.LastSeen.HasValue || u.Dead || u.Restrained || u.Escaped) continue;
                var ls = u.LastSeen.Value;
                if (ls.F != v.Floor || v.GameTime - ls.T > 25) continue;
                P.DashedRing((float)ls.X, (float)ls.Y, 10, 1.5f, new Color(255, 80, 80) * 0.55f, 10);
                labels.Add(("?", new Vector2((float)ls.X, (float)ls.Y - 7), new Color(255, 100, 100) * 0.8f, 12));
            }
        // screen-space labels and effects
        P.Begin(Matrix.Identity);
        foreach (var (text, pos, col, size) in labels)
        {
            var sp = ToScreen(M, pos.X, pos.Y);
            P.Text(text, sp.X, sp.Y - size * _g.Gui.S * 0.6f, size * _g.Gui.S * Math.Clamp(v.Cam.Zoom, 0.7f, 1.3f), col, FontKind.Bold, Align.Center);
        }
        if (v.Rain)
        {
            var rc = new Color(170, 190, 220) * 0.25f;
            for (int i = 0; i < 160; i++)
            {
                float x = (float)((i * 97 + t * 900 * (1 + (i % 3) * 0.2)) % (W + 100)) - 50, y = (float)((i * 211 + t * 1300) % (H + 100)) - 50;
                P.Line(x, y, x - 4, y + 14, 1, rc);
            }
        }
        foreach (var fl in Flashes)
        {
            if (fl.f != v.Floor) continue;
            var sp = ToScreen(M, fl.x, fl.y);
            float d = Vector2.Distance(sp, new Vector2(W / 2f, H / 2f));
            float a = Math.Max(0, 1 - fl.t / 1.2f) * Math.Max(0.15f, 1 - d / Math.Max(W, H)) * 0.7f;
            P.FillRect(0, 0, W, H, Color.White * a);
        }
        P.End();
    }

    private void DrawUnit(Painter P, Unit u, float t, List<(string, Vector2, Color, float)> labels)
    {
        string pose = u.Dead || u.Incap ? "dead" : u.Downed ? "downed" : u.Restrained ? (u.Team == Team.Civ ? "secured" : "restrained")
            : u.HostageBound ? "bound" : (u.Surrendered || (u.Ai != null && u.Ai.State == "surrender")) ? "surrender" : u.Kneel ? "kneel" : "normal";
        var w = u.W;
        var tex = _g.Art.Unit(u.Look, pose, pose == "normal" ? u.WeaponCat : null, u.Shield && pose == "normal", w != null && w.St.Suppressed, u.Team == Team.Op, u.Armor > 0);
        float x = (float)u.X, y = (float)u.Y;
        float rot = (float)u.Angle;
        P.Sprite(tex, x, y, rot, 0.5f);
        if (u.Team == Team.Op && !u.Dead) P.Ring(x, y, 12, 2, C.Hex(u.Color));
        if (u.Downed && !u.Dead && u.Team == Team.Op) P.Ring(x, y, 16, 2, u.Stable ? C.Green : (Math.Floor(t * 4) % 2 == 1 ? C.Red : C.Hex("#7a2020")));
        if (u.Stun > 0 && !u.Dead)
            for (int i = 0; i < 3; i++) { float a = t * 5 + i * 2.1f; P.Ring(x + MathF.Cos(a) * 7, y - 16 + MathF.Sin(a) * 3, 1.6f, 1.5f, C.Hex("#ffeb3b"), 0, MathF.PI * 2, 8); }
        if (u.Team == Team.Enemy && u.Ai != null)
        {
            if (u.Ai.State == "surrender" && !u.Restrained) labels.Add(("SURRENDER", new Vector2(x, y - 20), C.Accent, 10));
            if (u.Ai.Guard != null && u.Ai.ExecT >= 0 && u.Alive && !u.Restrained && u.Ai.State != "surrender" && u.Visible)
                labels.Add(($"⚠ {Math.Max(0, u.Ai.ExecT):0}", new Vector2(x, y - 22), C.Hex("#ff4040"), 12));
        }
        if (u.Hvt && !u.Dead) { P.DashedRing(x, y, 15, 1.5f, C.Hex("#ff4040"), 12); labels.Add(("HVT", new Vector2(x, y + 22), C.Hex("#ff6060"), 9)); }
        if (u.Kind == "hostage" && u.Alive && u.Ai.State != "secured") labels.Add(("HOSTAGE", new Vector2(x, y + 20), C.Blue, 9));
    }

    private void DrawWindows(Painter P, int f)
    {
        var fl = _map.Floors[f];
        for (int y = 0; y < _map.H; y++)
            for (int x = 0; x < _map.W; x++)
            {
                byte v = fl.T[y * _map.W + x];
                if (v != Tile.Window && v != Tile.BrokenWindow) continue;
                float px = x * TS, py = y * TS;
                bool horiz = _map.Get(f, x - 1, y) is Tile.Wall or Tile.Window || _map.Get(f, x + 1, y) == Tile.Wall;
                var glass = v == Tile.Window ? new Color(150, 200, 230) * 0.8f : new Color(150, 200, 230) * 0.25f;
                if (horiz) { P.FillRect(px, py + 11, TS, 10, C.Hex("#5a5650")); P.FillRect(px + 1, py + 14, TS - 2, 4, glass); }
                else { P.FillRect(px + 11, py, 10, TS, C.Hex("#5a5650")); P.FillRect(px + 14, py + 1, 4, TS - 2, glass); }
            }
    }

    private char DoorAxis(Door d)
    {
        bool IsW(byte v) => v is Tile.Wall or Tile.Window or Tile.BrokenWindow or Tile.Door;
        if (IsW(_map.Get(d.F, d.X - 1, d.Y)) && IsW(_map.Get(d.F, d.X + 1, d.Y))) return 'h';
        if (IsW(_map.Get(d.F, d.X, d.Y - 1)) && IsW(_map.Get(d.F, d.X, d.Y + 1))) return 'v';
        return IsW(_map.Get(d.F, d.X - 1, d.Y)) || IsW(_map.Get(d.F, d.X + 1, d.Y)) ? 'h' : 'v';
    }

    private void DrawDoors(Painter P, int f, List<(string, Vector2, Color, float)> labels)
    {
        foreach (var d in _map.Doors)
        {
            if (d.F != f) continue;
            char ax = DoorAxis(d);
            float px = d.X * TS, py = d.Y * TS;
            P.FillRect(px, py, TS, TS, C.Hex("#4a4440"));
            float target = d.State == DoorState.Closed ? 0 : 1;
            d.Anim += (target - d.Anim) * 0.25f;
            if (d.State == DoorState.Broken)
            {
                P.RotRect(px + TS / 2f, py + TS / 2f, 22, 5, 14, 2.5f, ax == 'h' ? 0.5f : 2.1f, C.Hex("#5a3e22"));
                P.RotRect(px + TS / 2f, py + TS / 2f, 12, 3, 4, 1.5f, ax == 'h' ? 1.7f : 3.3f, C.Hex("#5a3e22"));
                continue;
            }
            var col = d.Heavy ? C.Hex("#5a6068") : C.Hex("#7a5530");
            float open = d.Anim * MathF.PI / 2 * d.Hinge;
            if (ax == 'h')
            {
                float hx = d.Hinge > 0 ? px : px + TS, hy = py + TS / 2f;
                float baseAng = d.Hinge > 0 ? 0 : MathF.PI;
                P.RotRect(hx, hy, TS, 6, 0, 3, baseAng + open, col);
            }
            else
            {
                float hx = px + TS / 2f, hy = d.Hinge > 0 ? py : py + TS;
                float baseAng = d.Hinge > 0 ? MathF.PI / 2 : -MathF.PI / 2;
                P.RotRect(hx, hy, TS, 6, 0, 3, baseAng - open, col);
            }
            if (d.State == DoorState.Closed && d.KnownLocked && (d.Locked || d.Barricaded))
                labels.Add((d.Barricaded ? "✖" : "LOCKED", new Vector2(px + TS / 2f, py + TS / 2f), d.Barricaded ? C.Red : C.Accent, 9));
        }
    }

    private void DrawWalls(Painter P, View v, Matrix M, int W, int H)
    {
        var list = Walls(v.Floor);
        float cx = W / 2f, cy = H / 2f;
        float h = 0.038f * Math.Clamp(v.Cam.Zoom, 0.7f, 1.5f);
        var wallSide = C.Hex("#8d8a84"); var wallTop = C.Hex("#cfcac0");
        Vector2 Top(Vector2 p, float hh) => new(cx + (p.X - cx) * (1 + hh), cy + (p.Y - cy) * (1 + hh));
        var tops = new List<(Vector2[] q, Color c)>(list.Count);
        float[] shades = { 0.75f, 0.55f, 0.45f, 0.65f };
        foreach (var wt in list)
        {
            float x0 = wt.X * TS, y0 = wt.Y * TS;
            var p = new[] { ToScreen(M, x0, y0), ToScreen(M, x0 + TS, y0), ToScreen(M, x0 + TS, y0 + TS), ToScreen(M, x0, y0 + TS) };
            if (p.All(q => q.X < -60 || q.X > W + 60 || q.Y < -60 || q.Y > H + 60)) continue;
            float hh = wt.High ? h * (wt.Furn == Furn.Container ? 0.9f : 0.75f) : wt.Rock ? h * 0.5f : h;
            var top = p.Select(q => Top(q, hh)).ToArray();
            Color baseC = wt.High ? (wt.Tint ?? (wt.Furn == Furn.Rack ? C.Hex("#46525c") : C.Hex("#6a6a6a"))) : wt.Rock ? C.Hex("#2a2725") : wallSide;
            for (int i = 0; i < 4; i++)
            {
                if (!wt.E[i]) continue;
                int a = i, b = (i + 1) % 4;
                P.Quad(p[a], p[b], top[b], top[a], C.Shade(baseC, -1 + shades[i]));
            }
            var tc = wt.High ? (wt.Tint.HasValue ? C.Shade(wt.Tint.Value, 0.1f) : wt.Furn == Furn.Rack ? C.Hex("#5a6672") : C.Hex("#7a7a7a")) : wt.Rock ? C.Hex("#24211f") : wallTop;
            tops.Add((top, tc));
        }
        foreach (var (q, c) in tops) P.Quad(q[0], q[1], q[2], q[3], c);
    }

    private void DrawParticles(Painter P, int f)
    {
        foreach (var p in Particles)
        {
            if (p.F != f) continue;
            float k = p.Life / p.Max;
            switch (p.Type)
            {
                case "tracer": P.Line(p.X, p.Y, p.X1, p.Y1, 1.4f, p.Team == Team.Op ? new Color(255, 230, 160) * k : new Color(255, 150, 110) * k); break;
                case "spark": case "drop": case "shell": P.FillRect(p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size, p.Color * Math.Min(1, k * 2)); break;
                case "smoke": P.Circle(p.X, p.Y, p.Size, p.Color * (p.Alpha * k)); break;
                case "pool": { float s = p.Size * (1 - k * 0.8f); P.Circle(p.X, p.Y, s, new Color(90, 5, 5) * 0.8f); break; }
            }
        }
    }

    public static bool PointInPoly(double px, double py, List<float> pts)
    {
        bool inside = false;
        int n = pts.Count / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = pts[i * 2], yi = pts[i * 2 + 1], xj = pts[j * 2], yj = pts[j * 2 + 1];
            if (((yi > py) != (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi)) inside = !inside;
        }
        return inside;
    }

    /// <summary>Blueprint-style whole-map preview for the briefing screen.</summary>
    public void DrawPreview(Painter P, int floor, float x, float y, float w, float h, Gui gui = null)
    {
        float s = Math.Min(w / (_map.W * TS), h / (_map.H * TS)) * 0.96f;
        float ox = x + (w - _map.W * TS * s) / 2, oy = y + (h - _map.H * TS * s) / 2;
        var M = Matrix.CreateScale(s, s, 1) * Matrix.CreateTranslation(ox, oy, 0);
        P.Begin(M);
        if (floor > 0 && _map.Type != "bunker") foreach (var (tex, tx, ty) in Base(0)) P.ImageF(tex, tx, ty, 1, 1, Color.White * 0.25f);
        foreach (var (tex, tx, ty) in Base(floor)) P.ImageF(tex, tx, ty, 1, 1);
        P.FillRect(0, 0, _map.W * TS, _map.H * TS, new Color(20, 60, 110) * 0.35f);
        var fl = _map.Floors[floor];
        for (int yy = 0; yy < _map.H; yy++)
            for (int xx = 0; xx < _map.W; xx++)
            {
                byte t = fl.T[yy * _map.W + xx];
                if (t == Tile.Wall && fl.Mat[yy * _map.W + xx] != Mat.Rock) P.FillRect(xx * TS, yy * TS, TS, TS, C.Hex("#cfe3f5"));
                if (t == Tile.Window) P.FillRect(xx * TS + 8, yy * TS + 8, 16, 16, C.Hex("#6fb7d8"));
                if (t == Tile.High && fl.Furn[yy * _map.W + xx] != Furn.Tree) P.FillRect(xx * TS + 2, yy * TS + 2, TS - 4, TS - 4, new Color(160, 190, 215) * 0.6f);
            }
        foreach (var d in _map.Doors) if (d.F == floor) P.FillRect(d.X * TS + 6, d.Y * TS + 6, TS - 12, TS - 12, d.Exterior ? C.Accent : C.Hex("#8fb3d0"));
        foreach (var sp in _map.Spawns) P.Circle((sp.X + 0.5f) * TS, (sp.Y + 0.5f) * TS, 22, C.Accent);
        P.Begin();
        float fs = gui?.S ?? 1;
        foreach (var sp in _map.Spawns)
        {
            var p = ToScreen(M, (sp.X + 0.5f) * TS, (sp.Y + 0.5f) * TS);
            P.Text(sp.Name[..1], p.X, p.Y - 8 * fs, 13 * fs, Color.Black, FontKind.Bold, Align.Center);
        }
        foreach (var b in _map.Buildings.Where(b => b.Name.Length > 0 && b.Floors.Contains(floor)))
        {
            var p = ToScreen(M, (b.X + b.W / 2f) * TS, (b.Y - 0.9f) * TS);
            P.Text(b.Name.ToUpperInvariant(), p.X, p.Y, 11 * fs, Color.White * 0.85f, FontKind.Bold, Align.Center);
        }
    }
}
