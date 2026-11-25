using Breachpoint.Core.Data;
using Breachpoint.Core.Map;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;

namespace Breachpoint.Core.Sim;

public sealed class SquadMember
{
    public Operator Op;
    public int Entry;
}

/// <summary>
/// A running tactical operation: units, orders, AI, ballistics, grenades, objectives and replay recording.
/// Pure logic — rendering and audio consume the Fx / Sounds / Voices queues.
/// </summary>
public sealed partial class Mission
{
    public const int TS = GameMap.TS;

    public readonly MissionConfig Cfg;
    public readonly GameMap Map;
    public readonly Profile Profile;
    public readonly DifficultyDef Diff;
    public readonly string DifficultyId;
    public readonly int Tier;
    public readonly bool Night, Rain;
    private readonly Rng _rng;
    private readonly Rng R = Rng.FromTime();

    public double T;
    public bool Paused = true, Ended, CanComplete, ReplayMode;
    public string Phase = "plan";
    public double Speed = 1;
    public string Formation = "stack";
    public readonly List<Unit> Units = new();
    public readonly List<MissionItem> Items = new();
    public readonly List<Grenade> Grenades = new();
    public readonly List<Cloud> Smokes = new();
    public readonly List<Cloud> Gases = new();
    public readonly List<CamSource> CamSources = new();
    public readonly List<Noise> Noises = new();
    public readonly List<HeardMarker> Heard = new();
    public readonly List<FxEvent> Fx = new();
    public readonly List<SoundEvent> Sounds = new();
    public readonly List<VoiceEvent> Voices = new();
    public readonly List<LogEntry> Log = new();
    public readonly List<Frame> Frames = new();
    public readonly List<SimEvent> Events = new();
    public List<Objective> Objectives = new();
    public MissionResult Result;
    public Unit HvtUnit;
    private List<(int x, int y)> _exits;
    private double _recT, _objT, _lastClear = -10;
    private bool _allClearAnnounced, _failNotified;
    private int _shotsOp, _hitsOp, _civKilledByOp, _roe, _flashes, _smokes, _charges;

    public Mission(MissionConfig cfg, GameMap map, IReadOnlyList<SquadMember> squad, Profile profile, string difficulty, int placeSeed)
    {
        Cfg = cfg; Map = map; Profile = profile;
        DifficultyId = difficulty;
        Diff = CampaignData.Difficulty.GetValueOrDefault(difficulty, CampaignData.Difficulty["standard"]);
        Tier = Math.Max(1, cfg.Tier);
        Night = cfg.Night; Rain = cfg.Rain;
        _rng = new Rng(placeSeed);
        foreach (var d in Map.Doors) d.State0 = d.State;
        for (int i = 0; i < squad.Count; i++)
        {
            var s = squad[i];
            var sp = Map.Spawns.Count > s.Entry ? Map.Spawns[s.Entry] : Map.Spawns[0];
            var slots = SpawnSlots(sp, squad.Count + 4);
            int k = 0;
            for (int j = 0; j < i; j++) if (squad[j].Entry == s.Entry) k++;
            var (x, y) = k < slots.Count ? slots[k] : (sp.X, sp.Y);
            var u = MakeOperator(s.Op, x, y, 0, sp.Ang);
            u.Entry = sp.Name;
            Units.Add(u);
        }
        Populate(_rng);
        _exits = ComputeExits();
        Objectives = ComputeObjectives();
        AddLog($"Operation {cfg.Name} — planning phase. Issue orders, then EXECUTE.", "#e2a33a");
    }

    // ---------------- map queries ----------------
    public byte TileAt(int f, int x, int y) => Map.Get(f, x, y);
    public Door DoorAt(int f, int x, int y) => Map.DoorAt(f, x, y);
    public static int ToTile(double v) => (int)Math.Floor(v / TS);
    public static double Ctr(int v) => (v + 0.5) * TS;

    public bool BlocksVision(int f, int x, int y)
    {
        byte t = TileAt(f, x, y);
        if (t == Tile.Wall || t == Tile.High) return true;
        if (t == Tile.Door) { var d = DoorAt(f, x, y); return d != null && d.State == DoorState.Closed; }
        return false;
    }

    public bool Walkable(int f, int x, int y, bool op)
    {
        byte t = TileAt(f, x, y);
        if (t == Tile.Ground || t == Tile.Floor || t == Tile.Stairs || t == Tile.BrokenWindow) return true;
        if (t == Tile.Door) { var d = DoorAt(f, x, y); return !(d.Barricaded && d.State == DoorState.Closed); }
        if (t == Tile.Window) return op;
        return false;
    }

    private bool SmokeBlocks(int f, double x0, double y0, double x1, double y1)
    {
        foreach (var s in Smokes)
        {
            if (s.F != f || s.R < 8) continue;
            double dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
            if (l2 == 0) l2 = 1;
            double t = MathUtil.Clamp(((s.X - x0) * dx + (s.Y - y0) * dy) / l2, 0, 1);
            double px = x0 + dx * t - s.X, py = y0 + dy * t - s.Y;
            if (px * px + py * py < (s.R * 0.85) * (s.R * 0.85)) return true;
        }
        return false;
    }

    public bool Los(int f, double x0, double y0, double x1, double y1, bool ignoreSmoke = false)
    {
        double d = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        int n = (int)Math.Ceiling(d / 8);
        int lx = int.MinValue, ly = int.MinValue;
        for (int i = 1; i < n; i++)
        {
            int tx = ToTile(x0 + (x1 - x0) * i / n), ty = ToTile(y0 + (y1 - y0) * i / n);
            if (tx == lx && ty == ly) continue;
            lx = tx; ly = ty;
            if (BlocksVision(f, tx, ty)) return false;
        }
        return ignoreSmoke || !SmokeBlocks(f, x0, y0, x1, y1);
    }

    // ---------------- pathfinding (multi-floor A*) ----------------
    public List<PathNode> FindPath(int f0, int x0, int y0, int f1, int x1, int y1, bool op = true, int maxIter = 30000)
    {
        int W = Map.W, H = Map.H, N = W * H * Map.Floors.Length;
        if (!Walkable(f1, x1, y1, op))
        {
            var alt = NearestWalkable(f1, x1, y1, op, 3);
            if (alt == null) return null;
            (x1, y1) = alt.Value;
        }
        var g = new float[N]; Array.Fill(g, 1e9f);
        var from = new int[N]; Array.Fill(from, -1);
        var closed = new bool[N];
        int start = f0 * W * H + y0 * W + x0, goal = f1 * W * H + y1 * W + x1;
        var pq = new PriorityQueue<int, float>();
        float Hf(int f, int x, int y) { int dx = Math.Abs(x - x1), dy = Math.Abs(y - y1); return Math.Max(dx, dy) + 0.414f * Math.Min(dx, dy) + Math.Abs(f - f1) * 4; }
        g[start] = 0; pq.Enqueue(start, Hf(f0, x0, y0));
        int it = 0;
        while (pq.Count > 0 && it++ < maxIter)
        {
            int n = pq.Dequeue();
            if (closed[n]) continue;
            closed[n] = true;
            if (n == goal) break;
            int f = n / (W * H), rem = n % (W * H), y = rem / W, x = rem % W;
            byte tHere = TileAt(f, x, y);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (!Walkable(f, nx, ny, op)) continue;
                    byte tn = TileAt(f, nx, ny);
                    float c = 1;
                    if (dx != 0 && dy != 0)
                    {
                        if (!Walkable(f, x + dx, y, op) || !Walkable(f, x, y + dy, op)) continue;
                        byte ta = TileAt(f, x + dx, y), tb = TileAt(f, x, y + dy);
                        if (tn == Tile.Door || tHere == Tile.Door || ta == Tile.Door || tb == Tile.Door || tn == Tile.Window || tHere == Tile.Window) continue;
                        c = 1.414f;
                    }
                    if (tn == Tile.Window) c += 7;
                    if (tn == Tile.Door) { c += 0.3f; var d = DoorAt(f, nx, ny); if (op && d.State == DoorState.Closed && d.KnownLocked) c += 6; }
                    int nn = f * W * H + ny * W + nx;
                    if (closed[nn]) continue;
                    float ng = g[n] + c;
                    if (ng < g[nn]) { g[nn] = ng; from[nn] = n; pq.Enqueue(nn, ng + Hf(f, nx, ny)); }
                }
            if (tHere == Tile.Stairs)
                foreach (int of in new[] { f - 1, f + 1 })
                {
                    if (of < 0 || of >= Map.Floors.Length || TileAt(of, x, y) != Tile.Stairs) continue;
                    int nn = of * W * H + y * W + x;
                    if (closed[nn]) continue;
                    float ng = g[n] + 3;
                    if (ng < g[nn]) { g[nn] = ng; from[nn] = n; pq.Enqueue(nn, ng + Hf(of, x, y)); }
                }
        }
        if (from[goal] < 0 && goal != start) return null;
        var path = new List<PathNode>();
        int cur = goal;
        while (cur != start && cur >= 0)
        {
            int f = cur / (W * H), rem = cur % (W * H);
            path.Add(new PathNode(f, rem % W, rem / W));
            cur = from[cur];
        }
        path.Reverse();
        return path;
    }

    private (int, int)? NearestWalkable(int f, int x, int y, bool op, int rad)
    {
        for (int r = 1; r <= rad; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    byte t = TileAt(f, x + dx, y + dy);
                    if (Walkable(f, x + dx, y + dy, op) && t != Tile.Door && t != Tile.Window) return (x + dx, y + dy);
                }
        return null;
    }

    // ---------------- logging / events ----------------
    public void AddLog(string text, string color = "#c9d3dc")
    {
        Log.Add(new LogEntry { T = T, Text = text, Color = color });
        if (Log.Count > 120) Log.RemoveAt(0);
    }

    private void Voice(Unit u, string cat, bool prio = false)
    {
        if (!CampaignData.Voice.TryGetValue(cat, out var lines)) return;
        string line = lines[(int)Math.Floor(R.Next() * lines.Length)];
        if (u != null) AddLog($"{u.Name}: \"{line}\"", u.Color);
        if (!ReplayMode) Voices.Add(new VoiceEvent { Text = line, Pitch = u?.Pitch ?? 1, Prio = prio });
    }

    private void Say(string text, bool prio = true) { if (!ReplayMode) Voices.Add(new VoiceEvent { Text = text, Pitch = 1, Prio = prio }); }

    private void Snd(string name, double x, double y, string cal = null, bool flag = false)
    {
        if (!ReplayMode) Sounds.Add(new SoundEvent { Name = name, X = x, Y = y, Cal = cal, Flag = flag });
    }

    private void AddFx(FxEvent e) => Fx.Add(e);

    private void MakeNoise(double x, double y, int f, double rTiles, string kind = "noise", Unit src = null)
    {
        Noises.Add(new Noise { X = x, Y = y, F = f, R = rTiles * TS * (Rain ? 0.8 : 1), Kind = kind, Src = src });
        if (rTiles >= 10) Heard.Add(new HeardMarker { X = x, Y = y, F = f, T = T });
    }

    // ---------------- unit construction ----------------
    private static UnitLook LookFromOp(Operator op)
    {
        var L = op.Look;
        var u = GameData.Uniform.GetValueOrDefault(L.Uniform, GameData.Uniforms[0]);
        bool helmetOn = op.Loadout.Helmet != "none" && L.Helmet && u.Helmet != null;
        return new UnitLook
        {
            Shirt = L.Shirt ?? u.Shirt, Pants = L.Pants ?? u.Pants, Carrier = L.Carrier ?? u.Carrier, Helmet = helmetOn ? u.Helmet : null,
            Skin = GameData.Skin[MathUtil.Clamp(L.Skin, 0, GameData.Skin.Length - 1)], Hair = GameData.HairColors[MathUtil.Clamp(L.HairColor, 0, GameData.HairColors.Length - 1)],
            Glove = u.Glove, Camo = u.Camo, Balaclava = L.Balaclava, Build = L.Build,
        };
    }

    private static SimWeapon MkWeapon(WeaponInstance inst)
    {
        var st = WeaponStats.Of(inst);
        int reserveMags = st.Cat switch { "hg" => 3, "sg" => 0, "sr" => 4, _ => 5 };
        return new SimWeapon { Inst = inst, St = st, Mag = st.Mag, Reserve = st.Cat == "sg" ? 28 : st.Mag * reserveMags };
    }

    private Unit MakeOperator(Operator op, int x, int y, int f, double ang)
    {
        var L = op.Loadout;
        var arm = GameData.Armor.GetValueOrDefault(L.Armor ?? "light", GameData.Armors[1]);
        var hel = GameData.Helmet.GetValueOrDefault(L.Helmet ?? "none", GameData.Helmets[0]);
        var ws = new List<SimWeapon>();
        var prim = Profile?.WeaponByUid(L.Primary); var sec = Profile?.WeaponByUid(L.Secondary);
        if (prim != null) ws.Add(MkWeapon(prim));
        if (sec != null) ws.Add(MkWeapon(sec));
        if (ws.Count == 0) ws.Add(MkWeapon(ProfileService.NewWeapon("g17")));
        bool shield = L.Tool == "shield";
        var pers = GameData.Personality.GetValueOrDefault(op.Personality, GameData.Personalities[0]);
        double maxHp = ProfileService.MaxHp(op);
        var u = new Unit
        {
            Id = MathUtil.Uid("u"), Team = Team.Op, Kind = "operator", Name = op.Callsign, FullName = op.Name, Op = op, X = Ctr(x), Y = Ctr(y), Floor = f, Angle = ang,
            Hp = maxHp, MaxHp = maxHp, Armor = arm.Prot, Helmet = hel.Prot,
            SpeedMult = arm.Speed * hel.Speed * pers.Speed * (op.Perks.Contains("runner") ? 1.1 : 1) * (0.85 + op.Stat("mob") / 333.0),
            Weapons = ws, Shield = shield, Util = L.Util.ToList(), Tool = L.Tool, ToolUses = L.Tool == "medkit" ? 3 : 99,
            Color = op.Color, Look = LookFromOp(op), Stats = op.Stats, Perks = op.Perks, Pers = pers, Ammo = GameData.AmmoById.GetValueOrDefault(L.Ammo ?? "fmj", GameData.Ammo[0]),
            Pitch = 0.75 + (MathUtil.Hash(op.Id) % 50) / 100.0 + (op.Gender == "f" ? 0.35 : 0), Female = op.Gender == "f",
        };
        if (shield) { int hi = ws.FindIndex(w => w.St.Cat == "hg"); if (hi >= 0) u.Wi = hi; else u.Shield = false; }
        return u;
    }

    private static UnitLook EnemyLook(string kind, Rng rng)
    {
        string[] shirts = kind == "pro" ? new[] { "#1e2126", "#2a2f26", "#24282e" } : kind == "inmate" ? new[] { "#d86a1f", "#cf6a24" }
            : new[] { "#7a3030", "#30507a", "#555555", "#2d4a2d", "#6a5a3a", "#8a8a8a", "#3a2a4a", "#b0a080", "#222222" };
        return new UnitLook
        {
            Shirt = rng.Pick(shirts), Pants = rng.Pick(new[] { "#2d3a55", "#333333", "#4a4030", "#222222" }), Skin = rng.Pick(GameData.Skin), Hair = rng.Pick(GameData.HairColors),
            Helmet = kind == "pro" && rng.Chance(0.5) ? "#2a2d30" : null, Balaclava = (kind == "crim" || kind == "pro") && rng.Chance(0.4),
        };
    }

    private Unit MakeEnemy(string kind, int x, int y, int f, Rng rng, string name = null, string style = null, bool armed = false, Unit guard = null, bool dummy = false)
    {
        var E = CampaignData.Enemies[kind];
        string wid = rng.Pick(E.Weapons);
        if (armed && wid == "none") wid = "g17";
        var ws = new List<SimWeapon>();
        if (wid != "none")
        {
            var inst = new WeaponInstance { Uid = "e", Base = wid, Condition = 70 + rng.Int(0, 30) };
            string cat = GameData.Weapon[wid].Cat;
            if (kind == "pro" || (kind == "crim" && rng.Chance(0.4))) inst.Att = new() { ["optic"] = cat == "hg" ? "rmr" : "holo", ["side"] = "light" };
            if (kind == "pro" && rng.Chance(0.3) && cat != "hg") inst.Att["muzzle"] = "supp";
            ws.Add(MkWeapon(inst));
        }
        int tierArmor = E.Armor + (Tier >= 3 && kind != "associate" && kind != "hvt" ? 15 : 0);
        var e = new Unit
        {
            Id = MathUtil.Uid("u"), Team = Team.Enemy, Kind = kind, Name = name ?? E.Name, X = Ctr(x), Y = Ctr(y), Floor = f, Angle = rng.Range(-Math.PI, Math.PI),
            Weapons = ws, Armed = wid != "none", Armor = tierArmor, Helmet = kind == "pro" ? 40 : 0, Look = EnemyLook(kind, rng), Acc = E.Acc, React = E.React,
            Pers = GameData.Personalities[0],
        };
        e.Ai = new AiState
        {
            Post = new Pos(Ctr(x), Ctr(y), f),
            Style = style ?? rng.Weighted(new[] { "rusher", "holder", "hider" }, s => s == "rusher" ? E.Aggr : s == "holder" ? 0.6 : (1 - E.Aggr) * 0.7),
            Morale = E.Morale + rng.Int(-10, 10), Radio = E.Radio, WaitT = rng.Range(1, 4), Guard = guard, Dummy = dummy,
        };
        return e;
    }

    private Unit MakeCiv(string kind, int x, int y, int f, Rng rng)
    {
        var c = new Unit
        {
            Id = MathUtil.Uid("u"), Team = Team.Civ, Kind = kind, Name = kind == "hostage" ? "Hostage" : "Civilian", X = Ctr(x), Y = Ctr(y), Floor = f, Angle = rng.Range(-3, 3), Armed = false,
            Look = new UnitLook
            {
                Shirt = rng.Pick(new[] { "#c8c0a0", "#4a7ab0", "#b04a4a", "#e0e0e0", "#5a8a5a", "#d0a040", "#8a5ab0", "#2a3a4a" }),
                Pants = rng.Pick(new[] { "#2d3a55", "#555555", "#8a7a5a" }), Skin = rng.Pick(GameData.Skin), Hair = rng.Pick(GameData.HairColors),
            },
            Ai = new AiState { WaitT = rng.Range(2, 6) }, Pers = GameData.Personalities[0],
        };
        if (kind == "hostage") { c.HostageBound = true; c.Kneel = true; }
        if (Cfg.Type == "prison" && kind == "hostage") { c.Name = "Corrections Officer"; c.Look.Shirt = "#3a4a6a"; }
        return c;
    }

    private List<(int, int)> SpawnSlots(Spawn sp, int n)
    {
        var outp = new List<(int, int)>();
        for (int r = 0; r < 6 && outp.Count < n; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    int x = sp.X + dx, y = sp.Y + dy;
                    if (TileAt(0, x, y) == Tile.Ground && !outp.Contains((x, y))) outp.Add((x, y));
                }
        return outp;
    }

    private List<(int, int)> ComputeExits()
    {
        var ex = new List<(int, int)>();
        for (int x = 1; x < Map.W - 1; x++) foreach (int y in new[] { 1, Map.H - 2 }) if (TileAt(0, x, y) == Tile.Ground) ex.Add((x, y));
        for (int y = 1; y < Map.H - 1; y++) foreach (int x in new[] { 1, Map.W - 2 }) if (TileAt(0, x, y) == Tile.Ground) ex.Add((x, y));
        return ex;
    }

    private int[] DistField()
    {
        int W = Map.W, H = Map.H, N = W * H * Map.Floors.Length;
        var dist = new int[N]; Array.Fill(dist, -1);
        var q = new Queue<int>();
        foreach (var s in Map.Spawns) { int n = s.Y * W + s.X; dist[n] = 0; q.Enqueue(n); }
        while (q.Count > 0)
        {
            int n = q.Dequeue();
            int f = n / (W * H), rem = n % (W * H), y = rem / W, x = rem % W;
            var nb = new List<(int, int, int)> { (f, x + 1, y), (f, x - 1, y), (f, x, y + 1), (f, x, y - 1) };
            if (TileAt(f, x, y) == Tile.Stairs)
            {
                if (f > 0 && TileAt(f - 1, x, y) == Tile.Stairs) nb.Add((f - 1, x, y));
                if (f + 1 < Map.Floors.Length && TileAt(f + 1, x, y) == Tile.Stairs) nb.Add((f + 1, x, y));
            }
            foreach (var (nf, nx, ny) in nb)
            {
                if (!Walkable(nf, nx, ny, false)) continue;
                int nn = nf * W * H + ny * W + nx;
                if (dist[nn] >= 0) continue;
                dist[nn] = dist[n] + 1; q.Enqueue(nn);
            }
        }
        return dist;
    }

    private void Populate(Rng rng)
    {
        int W = Map.W, H = Map.H;
        var dist = DistField();
        var occupied = new HashSet<(int, int, int)>();
        List<(int, int)> RoomTiles(Room r)
        {
            var o = new List<(int, int)>();
            for (int y = r.Y; y < r.Y + r.H; y++)
                for (int x = r.X; x < r.X + r.W; x++)
                    if (TileAt(r.F, x, y) == Tile.Floor && !occupied.Contains((r.F, x, y)) && dist[r.F * W * H + y * W + x] > 6) o.Add((x, y));
            return o;
        }
        var rooms = Map.Rooms.Where(r => r.Tag != "corridor" || r.W * r.H > 20).Select(r =>
        {
            double sum = 0; int n = 0;
            for (int y = r.Y; y < r.Y + r.H; y++) for (int x = r.X; x < r.X + r.W; x++) { int d = dist[r.F * W * H + y * W + x]; if (d >= 0) { sum += d; n++; } }
            return (r, depth: n > 0 ? sum / n : 0, ok: n > 0);
        }).Where(o => o.ok).OrderByDescending(o => o.depth).ToList();
        if (rooms.Count == 0) return;
        (int, int)? PickTile(Room room, (int, int)? near = null)
        {
            var tiles = RoomTiles(room);
            if (tiles.Count == 0) return null;
            var t = rng.Pick(tiles);
            if (near.HasValue)
            {
                var (nx, ny) = near.Value;
                tiles.Sort((a, b) => Math.Sqrt((a.Item1 - nx) * (a.Item1 - nx) + (a.Item2 - ny) * (a.Item2 - ny)).CompareTo(Math.Sqrt((b.Item1 - nx) * (b.Item1 - nx) + (b.Item2 - ny) * (b.Item2 - ny))));
                t = tiles[Math.Min(tiles.Count - 1, rng.Int(1, 3))];
            }
            occupied.Add((room.F, t.Item1, t.Item2));
            return t;
        }
        var deep = rooms.Take(Math.Max(1, (int)Math.Ceiling(rooms.Count * 0.4))).ToList();
        string EnemyKind()
        {
            if (Cfg.Type == "prison") return "inmate";
            string[] pool = Tier <= 1 ? new[] { "associate", "associate", "guard", "guard" } : Tier == 2 ? new[] { "associate", "guard", "guard", "crim" }
                : Tier == 3 ? new[] { "guard", "crim", "crim", "pro" } : new[] { "crim", "pro", "pro", "crim" };
            return rng.Pick(pool);
        }
        string NameSuspect() => $"{rng.Pick(GameData.SuspectFirst)} \"{rng.Pick(GameData.SuspectNick)}\" {rng.Pick(GameData.Surnames)}";
        int left = Cfg.Enemies;
        if (Cfg.Training)
        {
            for (int i = 0; i < Cfg.Enemies; i++) { var ro = rng.Pick(rooms).r; var t = PickTile(ro); if (t == null) continue; var e = MakeEnemy("guard", t.Value.Item1, t.Value.Item2, ro.F, rng, "Target", armed: true, dummy: true); Units.Add(e); }
            for (int i = 0; i < Cfg.Civilians; i++) { var ro = rng.Pick(rooms).r; var t = PickTile(ro); if (t == null) continue; var c = MakeCiv("civilian", t.Value.Item1, t.Value.Item2, ro.F, rng); c.Ai.Dummy = true; c.Name = "No-Shoot Target"; Units.Add(c); }
            return;
        }
        var hostRooms = new List<Room>();
        for (int i = 0; i < Cfg.Hostages; i++)
        {
            var hr = hostRooms.Count > 0 && rng.Chance(0.55) ? rng.Pick(hostRooms) : rng.Pick(deep).r;
            if (!hostRooms.Contains(hr)) hostRooms.Add(hr);
            var t = PickTile(hr); if (t == null) continue;
            var h = MakeCiv("hostage", t.Value.Item1, t.Value.Item2, hr.F, rng); h.RoomId = hr.Id;
            Units.Add(h);
        }
        foreach (var hr in hostRooms)
        {
            if (left <= 0) break;
            var hs = Units.Where(u => u.Kind == "hostage" && u.RoomId == hr.Id).ToList();
            if (hs.Count == 0) continue;
            var t = PickTile(hr, (ToTile(hs[0].X), ToTile(hs[0].Y))); if (t == null) continue;
            var k = EnemyKind(); if (k == "associate") k = "guard";
            var e = MakeEnemy(k, t.Value.Item1, t.Value.Item2, hr.F, rng, style: "holder", armed: true, guard: hs[0]);
            e.Ai.GuardList = hs;
            e.Angle = Math.Atan2(hs[0].Y - e.Y, hs[0].X - e.X) + Math.PI;
            Units.Add(e); left--;
        }
        if (Cfg.Hvt)
        {
            var hr = deep[0].r;
            var t = PickTile(hr);
            if (t != null) { var e = MakeEnemy("hvt", t.Value.Item1, t.Value.Item2, hr.F, rng, Cfg.HvtName ?? NameSuspect(), "hider", true); e.Hvt = true; Units.Add(e); HvtUnit = e; }
            if (left > 0) { var t2 = PickTile(hr); if (t2 != null) { Units.Add(MakeEnemy(Tier >= 3 ? "pro" : "guard", t2.Value.Item1, t2.Value.Item2, hr.F, rng, style: "holder", armed: true)); left--; } }
        }
        if (Cfg.Lieutenant && left > 0)
        {
            var hr = rng.Pick(rooms.Take(Math.Max(1, (int)Math.Ceiling(rooms.Count * 0.7))).ToList()).r;
            var t = PickTile(hr);
            if (t != null) { var e = MakeEnemy("lieutenant", t.Value.Item1, t.Value.Item2, hr.F, rng, NameSuspect(), armed: true); e.Lieutenant = true; Units.Add(e); left--; }
        }
        var outdoor = new List<(int, int)>();
        for (int y = 2; y < H - 2; y++) for (int x = 2; x < W - 2; x++) if (TileAt(0, x, y) == Tile.Ground && dist[y * W + x] > 16) outdoor.Add((x, y));
        bool hasYard = Map.Type is "compound" or "rural" or "shipyard" or "hangar" or "mansion" or "bunker" or "prison";
        while (left > 0)
        {
            string kind = EnemyKind();
            if (hasYard && rng.Next() < 0.3 && outdoor.Count > 0)
            {
                var (x, y) = rng.Pick(outdoor);
                var e = MakeEnemy(kind, x, y, 0, rng, style: rng.Chance(0.5) ? "rusher" : "holder");
                e.Ai.Patrol = new List<(int, int)> { (x, y), rng.Pick(outdoor), rng.Pick(outdoor) }.Where(p => Math.Sqrt((p.Item1 - x) * (p.Item1 - x) + (p.Item2 - y) * (p.Item2 - y)) < 25).ToList();
                Units.Add(e);
            }
            else
            {
                var ro = rng.Chance(0.6) ? rng.Pick(deep) : rng.Pick(rooms);
                var t = PickTile(ro.r);
                if (t == null) { left--; continue; }
                var e = MakeEnemy(kind, t.Value.Item1, t.Value.Item2, ro.r.F, rng);
                if (rng.Chance(0.25))
                {
                    var other = rng.Pick(rooms).r;
                    if (other.F == ro.r.F) { var t2 = RoomTiles(other); if (t2.Count > 0) e.Ai.Patrol = new() { t.Value, rng.Pick(t2) }; }
                }
                if (e.Ai.Style == "hider" && rng.Chance(0.5)) { e.Ai.State = "hide"; e.Stance = "crouch"; }
                Units.Add(e);
            }
            left--;
        }
        for (int i = 0; i < Cfg.Civilians; i++) { var ro = rng.Pick(rooms); var t = PickTile(ro.r); if (t == null) continue; Units.Add(MakeCiv("civilian", t.Value.Item1, t.Value.Item2, ro.r.F, rng)); }
        var evRooms = rooms.Where(o => o.r.Tag is "office" or "bedroom" or "lab" or "storage" or "vault" or "living" or "bunker" or "guard" or "hotel").ToList();
        var conRooms = rooms.Where(o => o.r.Tag is "lab" or "storage" or "vault" or "hall" or "barn" or "bunker").ToList();
        void PlaceItem(List<(Room r, double depth, bool ok)> list, Func<MissionItem> mk)
        {
            var ro = list.Count > 0 ? rng.Pick(list) : rng.Pick(rooms);
            var t = PickTile(ro.r);
            if (t == null) return;
            var it = mk(); it.Id = MathUtil.Uid("it"); it.X = Ctr(t.Value.Item1); it.Y = Ctr(t.Value.Item2); it.F = ro.r.F;
            Items.Add(it);
        }
        for (int i = 0; i < Cfg.Evidence; i++) PlaceItem(evRooms, () => { var e = rng.Pick(CampaignData.EvidenceTypes); return new MissionItem { Kind = "evidence", Type = e.Icon, Name = e.Name, Intel = e.Intel, EvType = e.Id }; });
        for (int i = 0; i < Cfg.Contraband; i++) PlaceItem(conRooms, () => { var c = rng.Pick(CampaignData.ContrabandTypes); return new MissionItem { Kind = "contraband", Type = c.Icon, Name = c.Name, Value = c.Value }; });
    }

    // ---------------- vision ----------------
    public double VisRange(Unit u)
    {
        double r = 22;
        var w = u.W;
        if (Night && !(Map.Type == "bunker" && u.Floor == 1)) r = w != null && w.St.Light ? 12 : 6;
        if (u.Team == Team.Op)
        {
            if (w != null && !Night) r += w.St.Range * 0.5;
            if (u.HasPerk("eagle")) r += 3;
            if (u.Role == "marksman" && u.Path == null) r += 4;
        }
        if (u.Team == Team.Enemy && Night) r = 8;
        if (u.Gas > 0) r *= 0.5;
        return r * TS;
    }

    public double FovHalf(Unit u) => u.Team == Team.Op ? (u.W != null && u.W.St.Range > 4 ? 0.85 : 1.05) : 0.95;

    public bool CanSee(Unit a, Unit b)
    {
        if (a.Floor != b.Floor) return false;
        double dx = b.X - a.X, dy = b.Y - a.Y, d = Math.Sqrt(dx * dx + dy * dy);
        double range = VisRange(a);
        if (b.Team == Team.Op && Night && b.W != null && b.W.St.Light) range += 6 * TS;
        if (b.Ai != null && b.Ai.State == "hide") range = Math.Min(range, (a.HasPerk("eagle") ? 8 : 4.5) * TS);
        if (d > range) return false;
        if (d > 1.7 * TS && Math.Abs(MathUtil.AngDiff(a.Angle, Math.Atan2(dy, dx))) > FovHalf(a)) return false;
        return Los(a.Floor, a.X, a.Y, b.X, b.Y);
    }

    /// <summary>Vision polygon (flat x,y pairs) for fog-of-war rendering.</summary>
    public List<float> VisionPoly(Unit u, int rays = 72)
    {
        int f = u.Floor;
        double R = VisRange(u), half = FovHalf(u), near = 1.7 * TS;
        var pts = new List<float>((rays + 30) * 2);
        void Cast(double a, double maxR)
        {
            double dx = Math.Cos(a), dy = Math.Sin(a), d = 0;
            for (; d < maxR; d += 5)
            {
                if (BlocksVision(f, ToTile(u.X + dx * d), ToTile(u.Y + dy * d))) { d += 2; break; }
            }
            d = Math.Min(d, maxR);
            foreach (var s in Smokes)
            {
                if (s.F != f || s.R < 8) continue;
                double ox = s.X - u.X, oy = s.Y - u.Y, tp = ox * dx + oy * dy;
                if (tp < 0 || tp > d) continue;
                double pd = Math.Abs(ox * dy - oy * dx);
                if (pd < s.R * 0.85) { double back = Math.Sqrt(Math.Max(0, (s.R * 0.85) * (s.R * 0.85) - pd * pd)); d = Math.Max(0, Math.Min(d, tp - back + s.R * 0.3)); }
            }
            pts.Add((float)(u.X + dx * d)); pts.Add((float)(u.Y + dy * d));
        }
        for (int i = 0; i <= rays; i++) Cast(u.Angle - half + 2 * half * i / rays, R);
        const int back = 24;
        for (int i = 1; i < back; i++) Cast(u.Angle + half + (Math.PI * 2 - 2 * half) * i / back, near);
        return pts;
    }

    public IEnumerable<Unit> Ops => Units.Where(u => u.Team == Team.Op);
    public static bool CanAct(Unit u) => u.Alive && !u.Downed && !u.Dead;

    public void RefreshVisibility()
    {
        foreach (var e in Units)
        {
            if (e.Team == Team.Op) { e.Visible = true; continue; }
            bool vis = Units.Any(o => o.Team == Team.Op && CanAct(o) && CanSee(o, e));
            e.Visible = vis || (e.Dead && e.WasSeen) || e.Restrained;
            if (vis) { e.WasSeen = true; e.LastSeen = new Pos(e.X, e.Y, e.Floor, T); }
        }
    }
}
