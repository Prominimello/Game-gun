using Breachpoint.Core.Util;

namespace Breachpoint.Core.Map;

/// <summary>Procedural generation of buildings, sites and multi-floor layouts.</summary>
public static class MapGenerator
{
    private sealed record Rect(int X, int Y, int W, int H);
    private sealed record Split(int F, bool Vert, int P, int A, int B);

    private static GameMap Make(int w, int h, int nf, byte ground, string[] labels = null)
    {
        var m = new GameMap { W = w, H = h, Floors = new FloorLayer[nf] };
        for (int f = 0; f < nf; f++)
        {
            var L = new FloorLayer { T = new byte[w * h], Mat = new byte[w * h], Furn = new byte[w * h], Room = new short[w * h], Door = new short[w * h] };
            Array.Fill(L.T, f == 0 ? Tile.Ground : Tile.Void);
            Array.Fill(L.Mat, f == 0 ? ground : Mat.Concrete);
            Array.Fill(L.Room, (short)-1);
            Array.Fill(L.Door, (short)-1);
            m.Floors[f] = L;
        }
        m.Labels = labels ?? Enumerable.Range(0, nf).Select(i => i == 0 ? "Ground" : i == 1 ? "Upper" : $"Floor {i + 1}").ToArray();
        return m;
    }

    private static void SetMat(GameMap m, int f, int x, int y, byte v) { if (m.InBounds(x, y)) m.Floors[f].Mat[m.Idx(x, y)] = v; }
    private static void SetFurn(GameMap m, int f, int x, int y, byte v) { if (m.InBounds(x, y)) m.Floors[f].Furn[m.Idx(x, y)] = v; }

    private static void FillRect(GameMap m, int f, int x, int y, int w, int h, byte t, byte? mat = null)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
            {
                m.Set(f, xx, yy, t);
                if (mat.HasValue) SetMat(m, f, xx, yy, mat.Value);
            }
    }

    private static Door AddDoor(GameMap m, int f, int x, int y, Rng rng, bool locked = false, bool barricaded = false, bool exterior = false, bool heavy = false)
    {
        var d = new Door { Id = m.Doors.Count, F = f, X = x, Y = y, Locked = locked, Barricaded = barricaded, Exterior = exterior, Heavy = heavy, Hinge = rng.Chance(0.5) ? 1 : -1 };
        m.Doors.Add(d);
        m.Set(f, x, y, Tile.Door);
        m.Floors[f].Door[m.Idx(x, y)] = (short)d.Id;
        return d;
    }

    private static void Bsp(GameMap m, int f, int x, int y, int w, int h, int minS, Rng rng, int depth, List<Room> rooms, List<Split> splits, int maxArea)
    {
        bool canV = w >= minS * 2 + 1, canH = h >= minS * 2 + 1;
        if ((!canV && !canH) || (depth > 1 && w * h <= maxArea && rng.Chance(0.55)))
        {
            rooms.Add(new Room { F = f, X = x, Y = y, W = w, H = h });
            return;
        }
        bool vert = canV && (!canH || (w > h ? rng.Chance(0.8) : w == h ? rng.Chance(0.5) : rng.Chance(0.2)));
        if (vert)
        {
            int p = rng.Int(x + minS, x + w - 1 - minS);
            for (int yy = y; yy < y + h; yy++) m.Set(f, p, yy, Tile.Wall);
            splits.Add(new Split(f, true, p, y, y + h - 1));
            Bsp(m, f, x, y, p - x, h, minS, rng, depth + 1, rooms, splits, maxArea);
            Bsp(m, f, p + 1, y, x + w - 1 - p, h, minS, rng, depth + 1, rooms, splits, maxArea);
        }
        else
        {
            int p = rng.Int(y + minS, y + h - 1 - minS);
            for (int xx = x; xx < x + w; xx++) m.Set(f, xx, p, Tile.Wall);
            splits.Add(new Split(f, false, p, x, x + w - 1));
            Bsp(m, f, x, y, w, p - y, minS, rng, depth + 1, rooms, splits, maxArea);
            Bsp(m, f, x, p + 1, w, y + h - 1 - p, minS, rng, depth + 1, rooms, splits, maxArea);
        }
    }

    private static void DoorsForSplits(GameMap m, List<Split> splits, Rng rng, double lockChance = 0.12)
    {
        foreach (var s in splits)
        {
            var cands = new List<(int, int)>();
            for (int q = s.A; q <= s.B; q++)
            {
                var (x, y) = s.Vert ? (s.P, q) : (q, s.P);
                if (m.Get(s.F, x, y) != Tile.Wall) continue;
                byte a = s.Vert ? m.Get(s.F, x - 1, y) : m.Get(s.F, x, y - 1);
                byte b = s.Vert ? m.Get(s.F, x + 1, y) : m.Get(s.F, x, y + 1);
                if (a == Tile.Floor && b == Tile.Floor) cands.Add((x, y));
            }
            if (cands.Count == 0) continue;
            var (dx, dy) = rng.Pick(cands);
            var d = AddDoor(m, s.F, dx, dy, rng, locked: rng.Chance(lockChance));
            if (rng.Chance(0.35) && !d.Locked) d.State = DoorState.Open;
        }
    }

    private static readonly (int, int)[] Orth = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>Flood fill over passable tiles. Returns visited mask and count.</summary>
    public static (bool[] seen, int count) Flood(GameMap m, int f, int sx, int sy)
    {
        var seen = new bool[m.W * m.H];
        var st = new Stack<(int, int)>();
        st.Push((sx, sy)); seen[m.Idx(sx, sy)] = true;
        int n = 0;
        while (st.Count > 0)
        {
            var (x, y) = st.Pop(); n++;
            foreach (var (dx, dy) in Orth)
            {
                int nx = x + dx, ny = y + dy;
                if (!m.InBounds(nx, ny)) continue;
                int k = m.Idx(nx, ny);
                if (seen[k]) continue;
                byte t = m.Floors[f].T[k];
                if (GameMap.Passable(t) || t == Tile.Window) { seen[k] = true; st.Push((nx, ny)); }
            }
        }
        return (seen, n);
    }

    private static void EnsureConnected(GameMap m, int f, int sx, int sy, Rect r, Rng rng)
    {
        for (int iter = 0; iter < 60; iter++)
        {
            var (seen, _) = Flood(m, f, sx, sy);
            bool missing = false;
            var cands = new List<(int, int)>();
            int x0 = r?.X ?? 0, y0 = r?.Y ?? 0, x1 = r != null ? r.X + r.W : m.W, y1 = r != null ? r.Y + r.H : m.H;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int k = m.Idx(x, y);
                    byte t = m.Floors[f].T[k];
                    if (t == Tile.Floor && !seen[k]) missing = true;
                    if (t != Tile.Wall) continue;
                    if (r != null && (x == r.X || y == r.Y || x == r.X + r.W - 1 || y == r.Y + r.H - 1)) continue;
                    foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                    {
                        int ax = x - dx, ay = y - dy, bx = x + dx, by = y + dy;
                        if (m.Get(f, ax, ay) != Tile.Floor || m.Get(f, bx, by) != Tile.Floor) continue;
                        if (seen[m.Idx(ax, ay)] != seen[m.Idx(bx, by)]) cands.Add((x, y));
                    }
                }
            if (!missing || cands.Count == 0) return;
            var (cx, cy) = rng.Pick(cands);
            AddDoor(m, f, cx, cy, rng);
        }
    }

    private static void RegisterRooms(GameMap m, List<Room> rooms, Func<Room, string> tagFn, Func<Room, byte> matFn, int bId)
    {
        foreach (var r in rooms)
        {
            r.Id = m.Rooms.Count; r.Building = bId;
            r.Tag = tagFn(r);
            m.Rooms.Add(r);
            byte mat = matFn(r);
            for (int y = r.Y; y < r.Y + r.H; y++)
                for (int x = r.X; x < r.X + r.W; x++)
                {
                    int k = m.Idx(x, y);
                    if (m.Floors[r.F].T[k] == Tile.Floor) { m.Floors[r.F].Room[k] = (short)r.Id; m.Floors[r.F].Mat[k] = mat; }
                }
        }
    }

    private static void ExteriorOpenings(GameMap m, int f, Rect b, Rng rng, int doors, double windows, double lockChance, bool ground)
    {
        var edge = new List<(int x, int y, int dx, int dy)>();
        for (int x = b.X + 1; x < b.X + b.W - 1; x++) { edge.Add((x, b.Y, 0, -1)); edge.Add((x, b.Y + b.H - 1, 0, 1)); }
        for (int y = b.Y + 1; y < b.Y + b.H - 1; y++) { edge.Add((b.X, y, -1, 0)); edge.Add((b.X + b.W - 1, y, 1, 0)); }
        bool OutsideOk(byte t) => t == Tile.Ground || t == Tile.Void;
        if (ground && doors > 0)
        {
            var sides = new List<(int, int)> { (0, 1), (0, -1), (1, 0), (-1, 0) };
            rng.Shuffle(sides);
            int placed = 0;
            foreach (var (sx, sy) in sides)
            {
                if (placed >= doors) break;
                var c = edge.Where(e => e.dx == sx && e.dy == sy && m.Get(f, e.x - e.dx, e.y - e.dy) == Tile.Floor && OutsideOk(m.Get(f, e.x + e.dx, e.y + e.dy)) && m.Get(f, e.x, e.y) == Tile.Wall).ToList();
                if (c.Count == 0) continue;
                var pick = c[(int)Math.Floor(c.Count * rng.Range(0.25, 0.75))];
                AddDoor(m, f, pick.x, pick.y, rng, locked: rng.Chance(lockChance), barricaded: rng.Chance(0.08), exterior: true);
                placed++;
            }
        }
        if (windows > 0)
        {
            foreach (var (x, y, dx, dy) in edge)
            {
                if (m.Get(f, x, y) != Tile.Wall) continue;
                if (m.Get(f, x - dx, y - dy) != Tile.Floor) continue;
                if (!OutsideOk(m.Get(f, x + dx, y + dy))) continue;
                bool near = false;
                for (int k = -1; k <= 1; k++)
                {
                    int nx = x + (dy != 0 ? k : 0), ny = y + (dx != 0 ? k : 0);
                    byte t = m.Get(f, nx, ny);
                    if (t == Tile.Door || t == Tile.Window) near = true;
                }
                if (!near && rng.Chance(windows)) m.Set(f, x, y, Tile.Window);
            }
        }
    }

    private static (int, int) LinkStairs(GameMap m, int fLo, int fHi, Rect b, Rng rng, (int, int)? force = null)
    {
        (int x, int y) pos;
        if (force.HasValue) pos = force.Value;
        else
        {
            var cands = new List<(int, int)>();
            for (int y = b.Y + 2; y < b.Y + b.H - 2; y++)
                for (int x = b.X + 2; x < b.X + b.W - 3; x++)
                {
                    if (m.Get(fLo, x, y) != Tile.Floor || m.Get(fLo, x + 1, y) != Tile.Floor) continue;
                    bool bad = false;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 2; dx++)
                        {
                            byte t = m.Get(fLo, x + dx, y + dy);
                            if (t == Tile.Door || t == Tile.Stairs || t == Tile.Window) bad = true;
                        }
                    if (!(m.Get(fLo, x - 1, y) == Tile.Floor || m.Get(fLo, x + 2, y) == Tile.Floor)) bad = true;
                    if (!bad) cands.Add((x, y));
                }
            pos = cands.Count > 0 ? rng.Pick(cands) : (b.X + 2, b.Y + 2);
        }
        var (px, py) = pos;
        foreach (var f in new[] { fLo, fHi })
        {
            foreach (var xx in new[] { px, px + 1 })
            {
                m.Set(f, xx, py, Tile.Stairs);
                m.Floors[f].Mat[m.Idx(xx, py)] = Mat.Concrete;
                m.Floors[f].Furn[m.Idx(xx, py)] = 0;
            }
            var nbrs = new[] { (px - 1, py), (px + 2, py), (px, py - 1), (px + 1, py - 1), (px, py + 1), (px + 1, py + 1) };
            if (!nbrs.Any(n => m.Get(f, n.Item1, n.Item2) == Tile.Floor))
                foreach (var (a, c) in new[] { (px - 1, py), (px + 2, py) })
                    if (a > b.X && a < b.X + b.W - 1) m.Set(f, a, c, Tile.Floor);
        }
        m.Stairs.Add(new Stair(px, py, fLo, fHi));
        m.Stairs.Add(new Stair(px + 1, py, fLo, fHi));
        return pos;
    }

    private static readonly Dictionary<string, byte[]> FurnSets = new()
    {
        ["living"] = new[] { Furn.Sofa, Furn.Sofa, Furn.Table, Furn.Cabinet, Furn.Plant, Furn.Chair },
        ["bedroom"] = new[] { Furn.Bed, Furn.Bed, Furn.Cabinet, Furn.Desk, Furn.Chair },
        ["kitchen"] = new[] { Furn.Counter, Furn.Counter, Furn.Counter, Furn.Table, Furn.Chair },
        ["office"] = new[] { Furn.Desk, Furn.Desk, Furn.Cabinet, Furn.Chair, Furn.Shelf, Furn.Plant },
        ["bath"] = new[] { Furn.Toilet, Furn.Counter },
        ["storage"] = new[] { Furn.Crate, Furn.Crate, Furn.Shelf, Furn.Barrel, Furn.Shelf },
        ["lab"] = new[] { Furn.Lab, Furn.Lab, Furn.Lab, Furn.Barrel, Furn.Counter, Furn.Crate },
        ["cell"] = new[] { Furn.Bunk, Furn.Toilet },
        ["guard"] = new[] { Furn.Desk, Furn.Locker, Furn.Locker, Furn.Chair },
        ["vault"] = new[] { Furn.Safe, Furn.Safe, Furn.Safe, Furn.Shelf },
        ["lobby"] = new[] { Furn.Plant, Furn.Sofa, Furn.Chair, Furn.Table },
        ["hotel"] = new[] { Furn.Bed, Furn.Bed, Furn.Cabinet, Furn.Chair, Furn.Desk },
        ["bunker"] = new[] { Furn.Crate, Furn.Locker, Furn.Desk, Furn.Shelf, Furn.Barrel },
        ["shop"] = new[] { Furn.Shelf, Furn.Shelf, Furn.Counter },
        ["barn"] = new[] { Furn.Hay, Furn.Hay, Furn.Crate, Furn.Barrel, Furn.Hay },
        ["hall"] = new[] { Furn.Crate, Furn.Barrel },
        ["room"] = new[] { Furn.Table, Furn.Chair, Furn.Cabinet },
    };

    private static void Furnish(GameMap m, Rng rng, Dictionary<int, (int, int)> refs)
    {
        foreach (var r in m.Rooms)
        {
            var set = FurnSets.GetValueOrDefault(r.Tag ?? "room", FurnSets["room"]);
            int n = Math.Max(1, (int)Math.Round(r.W * r.H / (r.Tag == "cell" ? 4.0 : 9.0)));
            var cands = new List<(int, int)>();
            for (int y = r.Y; y < r.Y + r.H; y++)
                for (int x = r.X; x < r.X + r.W; x++)
                {
                    if (m.Get(r.F, x, y) != Tile.Floor) continue;
                    bool wallAdj = false, bad = false;
                    foreach (var (dx, dy) in Orth)
                    {
                        byte t = m.Get(r.F, x + dx, y + dy);
                        if (t == Tile.Wall || t == Tile.Window) wallAdj = true;
                        if (t == Tile.Door || t == Tile.Stairs) bad = true;
                    }
                    foreach (var (dx, dy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                    {
                        byte t = m.Get(r.F, x + dx, y + dy);
                        if (t == Tile.Door || t == Tile.Stairs) bad = true;
                    }
                    if (!bad && (wallAdj || (r.W >= 5 && r.H >= 5 && rng.Chance(0.15)))) cands.Add((x, y));
                }
            rng.Shuffle(cands);
            int placed = 0;
            bool hasRef = refs.TryGetValue(r.F, out var rf);
            foreach (var (x, y) in cands)
            {
                if (placed >= n) break;
                int before = hasRef ? Flood(m, r.F, rf.Item1, rf.Item2).count : 0;
                m.Set(r.F, x, y, Tile.Furn);
                int after = hasRef ? Flood(m, r.F, rf.Item1, rf.Item2).count : 0;
                if (hasRef && after < before - 1) { m.Set(r.F, x, y, Tile.Floor); continue; }
                SetFurn(m, r.F, x, y, rng.Pick(set));
                placed++;
            }
        }
    }

    // ---------- building styles ----------
    private static List<Room> StyleRooms(GameMap m, int f, Rect b, Rng rng, int minS, int maxArea)
    {
        var rooms = new List<Room>(); var splits = new List<Split>();
        Bsp(m, f, b.X + 1, b.Y + 1, b.W - 2, b.H - 2, minS, rng, 0, rooms, splits, maxArea);
        DoorsForSplits(m, splits, rng);
        return rooms;
    }

    private static List<Room> StyleCorridor(GameMap m, int f, Rect b, Rng rng, int rw0, int rw1, string cellTag)
    {
        var rooms = new List<Room>();
        int ix = b.X + 1, iy = b.Y + 1, iw = b.W - 2, ih = b.H - 2;
        void MaybeOpen(Door d) { if (rng.Chance(0.2) && !d.Locked) d.State = DoorState.Open; }
        if (iw >= ih)
        {
            int cy = iy + ih / 2 - 1;
            for (int x = ix; x < ix + iw; x++) { m.Set(f, x, cy - 1, Tile.Wall); m.Set(f, x, cy + 2, Tile.Wall); }
            rooms.Add(new Room { F = f, X = ix, Y = cy, W = iw, H = 2, Tag = "corridor" });
            foreach (var (ry, rh, dy) in new[] { (iy, cy - 1 - iy, cy - 1), (cy + 3, iy + ih - (cy + 3), cy + 2) })
            {
                int x = ix;
                while (x < ix + iw)
                {
                    int w = rng.Int(rw0, rw1);
                    if (ix + iw - (x + w) < rw0) w = ix + iw - x;
                    if (rh > 0) rooms.Add(new Room { F = f, X = x, Y = ry, W = w, H = rh });
                    if (x + w < ix + iw) for (int yy = ry; yy < ry + rh; yy++) m.Set(f, x + w, yy, Tile.Wall);
                    int dx = x + rng.Int(0, Math.Max(0, w - 1));
                    if (rh > 0) MaybeOpen(AddDoor(m, f, dx, dy, rng, locked: rng.Chance(0.25)));
                    x += w + 1;
                }
            }
        }
        else
        {
            int cx = ix + iw / 2 - 1;
            for (int y = iy; y < iy + ih; y++) { m.Set(f, cx - 1, y, Tile.Wall); m.Set(f, cx + 2, y, Tile.Wall); }
            rooms.Add(new Room { F = f, X = cx, Y = iy, W = 2, H = ih, Tag = "corridor" });
            foreach (var (rx, rw, dx) in new[] { (ix, cx - 1 - ix, cx - 1), (cx + 3, ix + iw - (cx + 3), cx + 2) })
            {
                int y = iy;
                while (y < iy + ih)
                {
                    int h = rng.Int(rw0, rw1);
                    if (iy + ih - (y + h) < rw0) h = iy + ih - y;
                    if (rw > 0) rooms.Add(new Room { F = f, X = rx, Y = y, W = rw, H = h });
                    if (y + h < iy + ih) for (int xx = rx; xx < rx + rw; xx++) m.Set(f, xx, y + h, Tile.Wall);
                    int dy = y + rng.Int(0, Math.Max(0, h - 1));
                    if (rw > 0) MaybeOpen(AddDoor(m, f, dx, dy, rng, locked: rng.Chance(0.25)));
                    y += h + 1;
                }
            }
        }
        foreach (var r in rooms) r.Tag ??= cellTag;
        return rooms;
    }

    private static List<Room> StyleHall(GameMap m, int f, Rect b, Rng rng, int officeDepth, bool shelves, string hallTag)
    {
        var rooms = new List<Room>();
        int oy = b.Y + 1, oh = officeDepth;
        for (int x = b.X + 1; x < b.X + b.W - 1; x++) m.Set(f, x, oy + oh, Tile.Wall);
        var splits = new List<Split>(); var orooms = new List<Room>();
        Bsp(m, f, b.X + 1, oy, b.W - 2, oh, 3, rng, 0, orooms, splits, 24);
        DoorsForSplits(m, splits, rng);
        foreach (var r in orooms) { r.Tag = rng.Chance(0.6) ? "office" : "storage"; rooms.Add(r); }
        foreach (var r in orooms)
        {
            int dx = r.X + rng.Int(0, r.W - 1);
            if (m.Get(f, dx, oy + oh + 1) == Tile.Floor) AddDoor(m, f, dx, oy + oh, rng, locked: rng.Chance(0.15));
        }
        var hall = new Room { F = f, X = b.X + 1, Y = oy + oh + 1, W = b.W - 2, H = b.H - 2 - oh - 1, Tag = hallTag };
        rooms.Add(hall);
        if (shelves)
        {
            for (int y = hall.Y + 3; y < hall.Y + hall.H - 3; y += 4)
            {
                int x = hall.X + 3;
                while (x < hall.X + hall.W - 4)
                {
                    int len = rng.Int(4, 8);
                    if (x + len > hall.X + hall.W - 3) break;
                    if (rng.Chance(0.85)) for (int k = 0; k < len; k++) { m.Set(f, x + k, y, Tile.High); SetFurn(m, f, x + k, y, Furn.Rack); }
                    x += len + rng.Int(2, 3);
                }
            }
            for (int i = 0; i < hall.W * hall.H / 30; i++)
            {
                int x = rng.Int(hall.X + 1, hall.X + hall.W - 2), y = rng.Int(hall.Y + 1, hall.Y + hall.H - 2);
                if (m.Get(f, x, y) == Tile.Floor && m.Get(f, x, y - 1) != Tile.High && m.Get(f, x, y + 1) != Tile.High)
                { m.Set(f, x, y, Tile.Furn); SetFurn(m, f, x, y, rng.Pick(new[] { Furn.Crate, Furn.Crate, Furn.Barrel })); }
            }
        }
        return rooms;
    }

    private static List<Room> StyleBank(GameMap m, int f, Rect b, Rng rng)
    {
        var rooms = new List<Room>();
        int lobbyH = (int)Math.Floor((b.H - 2) * 0.48);
        int ly = b.Y + b.H - 1 - lobbyH;
        rooms.Add(new Room { F = f, X = b.X + 1, Y = ly, W = b.W - 2, H = lobbyH, Tag = "lobby" });
        int cy = ly, gap = b.X + 2 + rng.Int(0, 2);
        for (int x = b.X + 1; x < b.X + b.W - 1; x++)
        {
            if (x == gap || x == gap + 1) continue;
            m.Set(f, x, cy, Tile.Furn); SetFurn(m, f, x, cy, Furn.Counter);
        }
        int ty = cy - 2;
        for (int x = b.X + 1; x < b.X + b.W - 1; x++) m.Set(f, x, ty - 1, Tile.Wall);
        rooms.Add(new Room { F = f, X = b.X + 1, Y = ty, W = b.W - 2, H = 2, Tag = "office" });
        var splits = new List<Split>(); var br = new List<Room>();
        Bsp(m, f, b.X + 1, b.Y + 1, b.W - 2, ty - 1 - (b.Y + 1), 3, rng, 0, br, splits, 26);
        DoorsForSplits(m, splits, rng, 0.2);
        foreach (var r in br)
        {
            int dx = r.X + rng.Int(0, r.W - 1);
            if (r.Y + r.H == ty - 1 && m.Get(f, dx, ty) == Tile.Floor) AddDoor(m, f, dx, ty - 1, rng, locked: rng.Chance(0.2));
        }
        br.Sort((a, c) => c.W * c.H - a.W * a.H);
        if (br.Count > 0) br[0].Tag = "vault";
        foreach (var r in br) r.Tag ??= rng.Pick(new[] { "office", "office", "storage" });
        rooms.AddRange(br);
        return rooms;
    }

    private static List<Room> StyleShop(GameMap m, int f, Rect b, Rng rng)
    {
        var rooms = new List<Room>();
        int backD = Math.Max(3, (int)Math.Floor((b.H - 2) * 0.32));
        int wy = b.Y + 1 + backD;
        for (int x = b.X + 1; x < b.X + b.W - 1; x++) m.Set(f, x, wy, Tile.Wall);
        var front = new Room { F = f, X = b.X + 1, Y = wy + 1, W = b.W - 2, H = b.Y + b.H - 2 - wy, Tag = "shop" };
        rooms.Add(front);
        for (int y = front.Y + 2; y < front.Y + front.H - 2; y += 3)
            for (int x = front.X + 3; x < front.X + front.W - 4; x++) { m.Set(f, x, y, Tile.Furn); SetFurn(m, f, x, y, Furn.Shelf); }
        for (int y = front.Y + front.H - 3; y < front.Y + front.H; y++) { m.Set(f, front.X + 1, y, Tile.Furn); SetFurn(m, f, front.X + 1, y, Furn.Counter); }
        var splits = new List<Split>(); var br = new List<Room>();
        Bsp(m, f, b.X + 1, b.Y + 1, b.W - 2, backD, 3, rng, 0, br, splits, 20);
        DoorsForSplits(m, splits, rng);
        foreach (var r in br) { r.Tag = rng.Pick(new[] { "storage", "office", "bath" }); rooms.Add(r); }
        var r0 = br[0];
        AddDoor(m, f, r0.X + r0.W / 2, wy, rng);
        return rooms;
    }

    private sealed class BOpts
    {
        public int MinS = 3, MaxArea = 30, ExtDoors = 2, OfficeDepth = 5, RoomW0 = 4, RoomW1 = 6;
        public double? Windows;
        public double LockChance = 0.4;
        public bool NoExtDoors, DoubleStairs, Shelves = true;
        public string Name = "", CellTag = "hotel", OpenTag = "hall", HallTag = "hall";
        public byte FloorMat = Mat.Wood, CorridorMat = Mat.Carpet;
        public Func<Room, Rng, string> TagFn;
        public Func<Room, byte> MatFn;
    }

    private static List<Room> BuildingAt(GameMap m, Rect b, int[] floors, string style, Rng rng, BOpts o)
    {
        int bId = m.Buildings.Count;
        m.Buildings.Add(new Building { Id = bId, X = b.X, Y = b.Y, W = b.W, H = b.H, Floors = floors, Name = o.Name });
        var all = new List<Room>();
        for (int i = 0; i < floors.Length; i++)
        {
            int f = floors[i];
            FillRect(m, f, b.X, b.Y, b.W, b.H, Tile.Wall, Mat.Concrete);
            FillRect(m, f, b.X + 1, b.Y + 1, b.W - 2, b.H - 2, Tile.Floor, Mat.Wood);
            List<Room> rooms = style switch
            {
                "rooms" => StyleRooms(m, f, b, rng, o.MinS, o.MaxArea),
                "corridor" => StyleCorridor(m, f, b, rng, o.RoomW0, o.RoomW1, o.CellTag),
                "hall" => i == 0 ? StyleHall(m, f, b, rng, o.OfficeDepth, o.Shelves, o.HallTag) : StyleRooms(m, f, b, rng, 3, 30),
                "open" => new List<Room> { new Room { F = f, X = b.X + 1, Y = b.Y + 1, W = b.W - 2, H = b.H - 2, Tag = o.OpenTag } },
                "bank" => i == 0 ? StyleBank(m, f, b, rng) : StyleRooms(m, f, b, rng, 3, 28),
                "shop" => StyleShop(m, f, b, rng),
                _ => StyleRooms(m, f, b, rng, o.MinS, o.MaxArea),
            };
            all.AddRange(rooms);
        }
        for (int i = 0; i < floors.Length - 1; i++)
        {
            LinkStairs(m, floors[i], floors[i + 1], b, rng);
            if (o.DoubleStairs && b.W > 24) LinkStairs(m, floors[i], floors[i + 1], b, rng);
        }
        for (int i = 0; i < floors.Length; i++)
            ExteriorOpenings(m, floors[i], b, rng, i == 0 && !o.NoExtDoors ? o.ExtDoors : 0, o.Windows ?? (i == 0 ? 0.3 : 0.4), o.LockChance, i == 0);
        foreach (var f in floors)
        {
            int sx = -1, sy = -1;
            var st = m.Stairs.FirstOrDefault(s => (s.F0 == f || s.F1 == f) && s.X > b.X && s.X < b.X + b.W && s.Y > b.Y && s.Y < b.Y + b.H);
            if (st != null) { sx = st.X; sy = st.Y; }
            else
            {
                for (int y = b.Y + 1; y < b.Y + b.H - 1 && sx < 0; y++)
                    for (int x = b.X + 1; x < b.X + b.W - 1; x++)
                        if (m.Get(f, x, y) == Tile.Door || m.Get(f, x, y) == Tile.Floor) { sx = x; sy = y; break; }
            }
            if (sx >= 0) EnsureConnected(m, f, sx, sy, b, rng);
        }
        var tagFn = o.TagFn ?? ((r, rg) =>
        {
            if (r.Tag != null) return r.Tag;
            int a = r.W * r.H;
            if (a <= 9) return rg.Pick(new[] { "bath", "storage" });
            if (a >= 30) return rg.Pick(new[] { "living", "kitchen", "office" });
            return rg.Pick(new[] { "bedroom", "office", "kitchen", "living", "storage" });
        });
        var matFn = o.MatFn ?? (r => r.Tag switch
        {
            "bath" or "kitchen" or "lab" or "shop" => Mat.TileFloor,
            "storage" or "hall" or "cell" or "bunker" or "guard" => Mat.Concrete,
            "office" or "bedroom" or "hotel" => Mat.Carpet,
            "living" => Mat.Wood,
            "corridor" => o.CorridorMat,
            "vault" => Mat.Metal,
            "lobby" => Mat.Marble,
            "barn" => Mat.Dirt,
            _ => o.FloorMat,
        });
        RegisterRooms(m, all, r => tagFn(r, rng), matFn, bId);
        return all;
    }

    // ---------- outdoor helpers ----------
    private static void Road(GameMap m, int y, int h)
    {
        FillRect(m, 0, 0, y, m.W, h, Tile.Ground, Mat.Asphalt);
        FillRect(m, 0, 0, y - 1, m.W, 1, Tile.Ground, Mat.Sidewalk);
    }

    private static void ParkCar(GameMap m, int x, int y, bool horiz, Rng rng, string color = null)
    {
        int w = horiz ? 4 : 2, h = horiz ? 2 : 4;
        for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) if (m.Get(0, xx, yy) != Tile.Ground) return;
        for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) { m.Set(0, xx, yy, Tile.Furn); SetFurn(m, 0, xx, yy, Furn.Car); }
        m.Decor.Add(new Decor("car", x, y, w, h, color ?? rng.Pick(new[] { "#7a1f1f", "#1f3f7a", "#cccccc", "#222222", "#4a4a4a", "#6a5a2a", "#2f5a3a" })));
    }

    private static void Scatter(GameMap m, int n, byte furn, byte tile, Rng rng, Rect[] avoid, Rect area = null)
    {
        for (int i = 0; i < n * 4 && n > 0; i++)
        {
            int x = rng.Int(area?.X ?? 1, area != null ? area.X + area.W - 1 : m.W - 2), y = rng.Int(area?.Y ?? 1, area != null ? area.Y + area.H - 1 : m.H - 2);
            if (m.Get(0, x, y) != Tile.Ground) continue;
            if (avoid.Any(r => x >= r.X - 2 && x < r.X + r.W + 2 && y >= r.Y - 2 && y < r.Y + r.H + 2)) continue;
            m.Set(0, x, y, tile); SetFurn(m, 0, x, y, furn); n--;
        }
    }

    private static void Fence(GameMap m, Rect r, (int x, int y, int len, bool horiz)[] gates)
    {
        for (int x = r.X; x < r.X + r.W; x++) { m.Set(0, x, r.Y, Tile.Fence); m.Set(0, x, r.Y + r.H - 1, Tile.Fence); }
        for (int y = r.Y; y < r.Y + r.H; y++) { m.Set(0, r.X, y, Tile.Fence); m.Set(0, r.X + r.W - 1, y, Tile.Fence); }
        foreach (var g in gates) for (int k = 0; k < g.len; k++) m.Set(0, g.x + (g.horiz ? k : 0), g.y + (g.horiz ? 0 : k), Tile.Ground);
    }

    private static void AddSpawns(GameMap m, string sides = "swe")
    {
        string[] names = { "Alpha", "Bravo", "Charlie", "Delta" };
        int i = 0;
        foreach (char s in sides)
        {
            var (x, y, ang) = s switch
            {
                's' => (m.W / 2, m.H - 3, -Math.PI / 2),
                'n' => (m.W / 2, 2, Math.PI / 2),
                'w' => (2, m.H / 2, 0.0),
                _ => (m.W - 3, m.H / 2, Math.PI),
            };
            (int, int)? best = null; double bd = 1e9;
            for (int yy = 1; yy < m.H - 1; yy++)
                for (int xx = 1; xx < m.W - 1; xx++)
                {
                    if (m.Get(0, xx, yy) != Tile.Ground) continue;
                    double d = Math.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y));
                    if (d < bd) { bd = d; best = (xx, yy); }
                }
            if (best.HasValue && bd < 8) m.Spawns.Add(new Spawn(names[i++], best.Value.Item1, best.Value.Item2, ang, s));
            if (i >= 3) break;
        }
    }

    /// <summary>Generate a map of the given type. Same type + seed + options always produces the same layout.</summary>
    public static GameMap Generate(string type, int seed, int? floors = null, bool small = false)
    {
        var rng = new Rng(seed);
        GameMap m;
        switch (type)
        {
            case "house":
            case "house2":
            {
                int nf = floors ?? (type == "house2" ? 2 : rng.Chance(0.4) ? 2 : 1);
                int bw = rng.Int(18, 23), bh = rng.Int(13, 16);
                m = Make(bw + 14, bh + 15, nf, Mat.Grass);
                Road(m, m.H - 4, 4);
                var b = new Rect(7, 5, bw, bh);
                var o = new BOpts { MinS = 3, MaxArea = 28, ExtDoors = 2 };
                if (type == "house2") o.TagFn = (r, rg) => r.W * r.H <= 9 ? "bath" : rg.Chance(0.45) ? "lab" : rg.Pick(new[] { "living", "kitchen", "bedroom", "storage" });
                BuildingAt(m, b, Enumerable.Range(0, nf).ToArray(), "rooms", rng, o);
                FillRect(m, 0, b.X + 3, b.Y + b.H, 3, m.H - 4 - b.Y - b.H, Tile.Ground, Mat.Asphalt);
                FillRect(m, 0, b.X + b.W / 2, b.Y + b.H, 1, m.H - 5 - b.Y - b.H, Tile.Ground, Mat.Sidewalk);
                ParkCar(m, b.X + 3, b.Y + b.H + 2, false, rng);
                ParkCar(m, rng.Int(2, m.W - 8), m.H - 3, true, rng);
                Scatter(m, 6, Furn.Bush, Tile.Furn, rng, new[] { b });
                Scatter(m, 4, Furn.Tree, Tile.High, rng, new[] { b });
                AddSpawns(m);
                break;
            }
            case "warehouse":
            {
                int bw = small ? rng.Int(24, 28) : rng.Int(32, 38), bh = small ? rng.Int(18, 20) : rng.Int(22, 26);
                m = Make(bw + 16, bh + 14, 1, Mat.Asphalt);
                var b = new Rect(8, 6, bw, bh);
                BuildingAt(m, b, new[] { 0 }, "hall", rng, new BOpts { OfficeDepth = 5, ExtDoors = 3, Windows = 0.15 });
                for (int i = 0; i < 3; i++) ParkCar(m, b.X + 2 + i * 6, b.Y + b.H + 3, false, rng, "#dddddd");
                Scatter(m, 10, Furn.Crate, Tile.Furn, rng, new[] { b });
                Scatter(m, 5, Furn.Container, Tile.High, rng, new[] { b });
                AddSpawns(m);
                break;
            }
            case "compound":
            {
                m = Make(66, 50, 2, Mat.Grass, new[] { "Ground", "Upper" });
                var yard = new Rect(4, 4, m.W - 8, m.H - 8);
                FillRect(m, 0, yard.X, yard.Y, yard.W, yard.H, Tile.Ground, Mat.Gravel);
                Fence(m, yard, new[] { (m.W / 2 - 2, yard.Y + yard.H - 1, 4, true), (yard.X, m.H / 2 - 1, 3, false), (yard.X + yard.W - 1, 12, 3, false) });
                var main = new Rect(22, 8, 24, 16);
                BuildingAt(m, main, new[] { 0, 1 }, "rooms", rng, new BOpts { MinS = 3, MaxArea = 30, ExtDoors = 3, Name = "Main Building" });
                var shed1 = new Rect(8, 9, 10, 9); var shed2 = new Rect(48, 28, 12, 10); var gar = new Rect(10, 28, 16, 11);
                BuildingAt(m, shed1, new[] { 0 }, "rooms", rng, new BOpts { MinS = 3, MaxArea = 20, ExtDoors = 1, Name = "Guard House" });
                BuildingAt(m, shed2, new[] { 0 }, "rooms", rng, new BOpts { MinS = 3, MaxArea = 24, ExtDoors = 2, Name = "Workshop" });
                BuildingAt(m, gar, new[] { 0 }, "open", rng, new BOpts { ExtDoors = 2, OpenTag = "storage", Name = "Garage", Windows = 0.1 });
                var bs = new[] { main, shed1, shed2, gar };
                for (int i = 0; i < 4; i++) ParkCar(m, rng.Int(30, 44), rng.Int(28, 40), rng.Chance(0.5), rng, rng.Pick(new[] { "#dddddd", "#333333", "#5a5a2a" }));
                Scatter(m, 18, Furn.Crate, Tile.Furn, rng, bs, yard);
                Scatter(m, 8, Furn.Barrel, Tile.Furn, rng, bs, yard);
                Scatter(m, 4, Furn.Container, Tile.High, rng, bs, yard);
                Scatter(m, 14, Furn.Tree, Tile.High, rng, new[] { yard });
                AddSpawns(m);
                break;
            }
            case "bank":
            {
                int bw = rng.Int(28, 32), bh = rng.Int(19, 22);
                m = Make(bw + 14, bh + 15, 2, Mat.Sidewalk);
                Road(m, m.H - 5, 5);
                var b = new Rect(7, 5, bw, bh);
                BuildingAt(m, b, new[] { 0, 1 }, "bank", rng, new BOpts { ExtDoors = 2, Windows = 0.5, LockChance = 0.5 });
                var vault = m.Rooms.FirstOrDefault(r => r.Tag == "vault");
                if (vault != null)
                    foreach (var d in m.Doors)
                        if (d.F == 0 && Math.Abs(d.X - (vault.X + vault.W / 2.0)) <= vault.W / 2.0 + 1 && Math.Abs(d.Y - (vault.Y + vault.H / 2.0)) <= vault.H / 2.0 + 1)
                        { d.Locked = true; d.Heavy = true; d.State = DoorState.Closed; }
                for (int i = 0; i < 4; i++) ParkCar(m, 2 + i * 9, m.H - 4, true, rng);
                AddSpawns(m);
                break;
            }
            case "hangar":
            {
                int bw = rng.Int(40, 44), bh = rng.Int(24, 28);
                m = Make(bw + 18, bh + 18, 1, Mat.Asphalt);
                var b = new Rect(9, 6, bw, bh);
                BuildingAt(m, b, new[] { 0 }, "hall", rng, new BOpts { OfficeDepth = 6, ExtDoors = 3, Windows = 0.1 });
                int gx = b.X + b.W / 2 - 3;
                for (int k = 0; k < 6; k++) m.Set(0, gx + k, b.Y + b.H - 1, Tile.Floor);
                for (int i = 0; i < 6; i++)
                {
                    int x = rng.Int(4, m.W - 8), y = rng.Int(b.Y + b.H + 3, m.H - 5);
                    if (m.Get(0, x, y) == Tile.Ground)
                        for (int k = 0; k < 3; k++) if (m.Get(0, x + k, y) == Tile.Ground) { m.Set(0, x + k, y, Tile.High); SetFurn(m, 0, x + k, y, Furn.Container); }
                }
                Scatter(m, 12, Furn.Crate, Tile.Furn, rng, new[] { b });
                AddSpawns(m);
                break;
            }
            case "hotel":
            {
                int nf = floors ?? 3;
                int bw = rng.Int(32, 36), bh = rng.Int(15, 17);
                m = Make(bw + 14, bh + 15, nf, Mat.Sidewalk, new[] { "Lobby", "2nd Floor", "3rd Floor" }.Take(nf).ToArray());
                Road(m, m.H - 5, 5);
                var b = new Rect(7, 5, bw, bh);
                BuildingAt(m, b, Enumerable.Range(0, nf).ToArray(), "corridor", rng, new BOpts { RoomW0 = 4, RoomW1 = 6, CellTag = "hotel", ExtDoors = 2, Windows = 0.45, DoubleStairs = true });
                for (int i = 0; i < 4; i++) ParkCar(m, 2 + i * 10, m.H - 4, true, rng);
                AddSpawns(m);
                break;
            }
            case "prison":
            {
                int bw = rng.Int(34, 38), bh = rng.Int(16, 18);
                m = Make(bw + 14, bh + 16, 2, Mat.Gravel, new[] { "Block A", "Block A — Upper" });
                var yard = new Rect(2, 2, m.W - 4, m.H - 4);
                Fence(m, yard, new[] { (m.W / 2 - 2, yard.Y + yard.H - 1, 4, true), (yard.X, m.H / 2, 2, false) });
                var b = new Rect(7, 6, bw, bh);
                BuildingAt(m, b, new[] { 0, 1 }, "corridor", rng, new BOpts { RoomW0 = 3, RoomW1 = 4, CellTag = "cell", ExtDoors = 2, Windows = 0.1, CorridorMat = Mat.Concrete, LockChance = 0.6 });
                foreach (var r in m.Rooms.Where(r => r.Tag == "cell").Take(2)) r.Tag = "guard";
                AddSpawns(m);
                break;
            }
            case "store":
            {
                int bw = rng.Int(15, 18), bh = rng.Int(12, 14);
                m = Make(bw + 16, bh + 14, 1, Mat.Sidewalk);
                Road(m, m.H - 5, 5);
                var b = new Rect(8, 4, bw, bh);
                BuildingAt(m, b, new[] { 0 }, "shop", rng, new BOpts { ExtDoors = 2, Windows = 0.6, LockChance = 0.2 });
                ParkCar(m, 3, m.H - 4, true, rng); ParkCar(m, m.W - 8, m.H - 4, true, rng);
                AddSpawns(m);
                break;
            }
            case "mansion":
            {
                int bw = rng.Int(30, 34), bh = rng.Int(20, 23);
                m = Make(bw + 22, bh + 22, 2, Mat.Grass);
                var yard = new Rect(3, 3, m.W - 6, m.H - 6);
                Fence(m, yard, new[] { (m.W / 2 - 2, yard.Y + yard.H - 1, 4, true), (yard.X, m.H / 2, 3, false), (yard.X + yard.W - 1, m.H / 2, 3, false) });
                var b = new Rect(11, 8, bw, bh);
                BuildingAt(m, b, new[] { 0, 1 }, "rooms", rng, new BOpts { MinS = 4, MaxArea = 42, ExtDoors = 3, Windows = 0.5, FloorMat = Mat.Marble, DoubleStairs = true });
                FillRect(m, 0, b.X + 4, b.Y + b.H + 3, 10, 4, Tile.Water, Mat.TileFloor);
                FillRect(m, 0, m.W / 2 - 1, b.Y + b.H, 3, m.H - b.Y - b.H - 3, Tile.Ground, Mat.Gravel);
                ParkCar(m, m.W / 2 + 4, b.Y + b.H + 4, false, rng, "#111111"); ParkCar(m, m.W / 2 + 7, b.Y + b.H + 4, false, rng, "#dddddd");
                Scatter(m, 14, Furn.Tree, Tile.High, rng, new[] { b }, yard);
                Scatter(m, 14, Furn.Bush, Tile.Furn, rng, new[] { b }, yard);
                AddSpawns(m);
                break;
            }
            case "shipyard":
            {
                m = Make(62, 44, 1, Mat.Asphalt);
                FillRect(m, 0, 0, 0, m.W, 5, Tile.Water, Mat.Concrete);
                FillRect(m, 0, 0, 5, m.W, 1, Tile.Ground, Mat.Concrete);
                var office = new Rect(44, 28, 14, 10);
                BuildingAt(m, office, new[] { 0 }, "rooms", rng, new BOpts { MinS = 3, MaxArea = 20, ExtDoors = 2, Windows = 0.4, Name = "Dock Office" });
                string[] cols = { "#8a3b2b", "#2b5a8a", "#3b7a3b", "#9a7a2a", "#5a5a5a", "#6a2a6a" };
                for (int cy = 8; cy < 38; cy += 5)
                    for (int cx = 4; cx < 40; cx += 9)
                    {
                        if (rng.Chance(0.25)) continue;
                        string c = rng.Pick(cols); int len = rng.Int(5, 7);
                        for (int k = 0; k < len; k++) for (int j = 0; j < 2; j++)
                                if (m.Get(0, cx + k, cy + j) == Tile.Ground) { m.Set(0, cx + k, cy + j, Tile.High); SetFurn(m, 0, cx + k, cy + j, Furn.Container); }
                        m.Decor.Add(new Decor("container", cx, cy, len, 2, c));
                    }
                Scatter(m, 14, Furn.Crate, Tile.Furn, rng, new[] { office });
                Scatter(m, 6, Furn.Barrel, Tile.Furn, rng, new[] { office });
                AddSpawns(m);
                break;
            }
            case "rural":
            {
                m = Make(64, 48, 2, Mat.Grass, new[] { "Ground", "Upper" });
                FillRect(m, 0, 30, 0, 3, m.H, Tile.Ground, Mat.Dirt);
                var house = new Rect(12, 8, 16, 12); var barn = new Rect(38, 7, 17, 13); var shed = new Rect(40, 30, 8, 7);
                BuildingAt(m, house, new[] { 0, 1 }, "rooms", rng, new BOpts { MinS = 3, MaxArea = 22, ExtDoors = 2, Name = "Farmhouse" });
                BuildingAt(m, barn, new[] { 0 }, "open", rng, new BOpts { ExtDoors = 2, OpenTag = "barn", Name = "Barn", Windows = 0.05 });
                BuildingAt(m, shed, new[] { 0 }, "rooms", rng, new BOpts { MinS = 3, MaxArea = 20, ExtDoors = 1, Name = "Shed" });
                for (int i = 0; i < 6; i++)
                {
                    int x = rng.Int(barn.X + 2, barn.X + barn.W - 3), y = rng.Int(barn.Y + 2, barn.Y + barn.H - 3);
                    if (m.Get(0, x, y) == Tile.Floor) { m.Set(0, x, y, Tile.Furn); SetFurn(m, 0, x, y, Furn.Hay); }
                }
                ParkCar(m, 32, 24, false, rng, "#5a3a2a"); ParkCar(m, 22, 24, true, rng, "#2a3a5a");
                Scatter(m, 40, Furn.Tree, Tile.High, rng, new[] { house, barn, shed });
                Scatter(m, 20, Furn.Bush, Tile.Furn, rng, new[] { house, barn, shed });
                AddSpawns(m);
                break;
            }
            case "bunker":
            {
                m = Make(54, 42, 2, Mat.Dirt, new[] { "Surface", "Bunker B1" });
                FillRect(m, 0, 0, 0, m.W, m.H, Tile.Ground, Mat.Grass);
                FillRect(m, 1, 0, 0, m.W, m.H, Tile.Wall, Mat.Rock);
                var cabin = new Rect(22, 14, 10, 8);
                BuildingAt(m, cabin, new[] { 0 }, "rooms", rng, new BOpts { MinS = 3, MaxArea = 30, ExtDoors = 2, Name = "Hunting Cabin" });
                var bk = new Rect(6, 5, 42, 32);
                BuildingAt(m, bk, new[] { 1 }, "rooms", rng, new BOpts
                {
                    MinS = 4, MaxArea = 36, NoExtDoors = true, Windows = 0, FloorMat = Mat.Concrete, Name = "Bunker",
                    TagFn = (r, rg) => r.W * r.H > 36 ? "bunker" : rg.Pick(new[] { "bunker", "office", "storage", "guard" }),
                });
                (int, int)? pos = null;
                for (int y = cabin.Y + 2; y < cabin.Y + cabin.H - 2 && pos == null; y++)
                    for (int x = cabin.X + 1; x < cabin.X + cabin.W - 3; x++)
                        if (m.Get(0, x, y) == Tile.Floor && m.Get(0, x + 1, y) == Tile.Floor) { pos = (x, y); break; }
                var p = LinkStairs(m, 0, 1, cabin, rng, pos);
                EnsureConnected(m, 1, p.Item1, p.Item2, bk, rng);
                Scatter(m, 60, Furn.Tree, Tile.High, rng, new[] { cabin });
                Scatter(m, 25, Furn.Bush, Tile.Furn, rng, new[] { cabin });
                AddSpawns(m);
                break;
            }
            case "training":
            {
                m = Make(34, 26, 1, Mat.Concrete);
                var b = new Rect(6, 4, 22, 15);
                BuildingAt(m, b, new[] { 0 }, "rooms", rng, new BOpts { MinS = 3, MaxArea = 22, ExtDoors = 2, LockChance = 0.2, FloorMat = Mat.Concrete, MatFn = _ => Mat.Concrete, TagFn = (_, _) => "room" });
                AddSpawns(m);
                break;
            }
            default:
                return Generate("house", seed, floors, small);
        }
        var refs = new Dictionary<int, (int, int)>();
        for (int f = 0; f < m.Floors.Length; f++)
        {
            var st = m.Stairs.FirstOrDefault(s => s.F0 == f || s.F1 == f);
            if (f == 0 && m.Spawns.Count > 0) refs[0] = (m.Spawns[0].X, m.Spawns[0].Y);
            else if (st != null) refs[f] = (st.X, st.Y);
        }
        Furnish(m, rng, refs);
        foreach (var d in m.Doors) { d.State0 = d.State; d.Anim = d.State == DoorState.Closed ? 0 : 1; }
        m.Type = type; m.Seed = seed;
        return m;
    }
}
