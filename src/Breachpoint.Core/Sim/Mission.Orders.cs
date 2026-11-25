using Breachpoint.Core.Data;
using Breachpoint.Core.Map;
using Breachpoint.Core.Util;

namespace Breachpoint.Core.Sim;

public sealed partial class Mission
{
    // ================= Public order API (used by the UI) =================
    private static void ClearOrders(Unit u) { u.Orders.Clear(); u.Path = null; u.Act = null; u.Follow = null; u.Hold = false; }

    private static void Give(Unit u, Order o, bool append)
    {
        if (!CanAct(u)) return;
        if (!append) ClearOrders(u);
        u.Orders.Add(o);
    }

    public void OrderMove(IEnumerable<Unit> units, int tx, int ty, int f, bool append)
    {
        var list = units.Where(CanAct).ToList();
        var spots = list.Count > 1 ? GroupSpots(tx, ty, f, list.Count) : new List<(int, int)> { (tx, ty) };
        for (int i = 0; i < list.Count; i++) Give(list[i], new Order { Kind = "move", X = spots[i].Item1, Y = spots[i].Item2, F = f, Pace = list[i].Pace }, append);
        if (list.Count > 0 && !append && Phase == "live") Voice(list[0], "moving");
    }

    private List<(int, int)> GroupSpots(int tx, int ty, int f, int n)
    {
        var o = new List<(int, int)> { (tx, ty) };
        for (int r = 1; r < 5 && o.Count < n; r++)
            for (int dy = -r; dy <= r && o.Count < n; dy++)
                for (int dx = -r; dx <= r && o.Count < n; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    int x = tx + dx, y = ty + dy; byte t = TileAt(f, x, y);
                    if ((t == Tile.Floor || t == Tile.Ground || t == Tile.Stairs) && Los(f, Ctr(tx), Ctr(ty), Ctr(x), Ctr(y), true)) o.Add((x, y));
                }
        while (o.Count < n) o.Add((tx, ty));
        return o;
    }

    private sealed record Sides((int, int) Near, (int, int) Far, char Axis);

    private (int, int, char)[] DoorSides(Door d)
    {
        bool horiz = Walkable(d.F, d.X - 1, d.Y, true) || Walkable(d.F, d.X + 1, d.Y, true);
        bool vert = Walkable(d.F, d.X, d.Y - 1, true) || Walkable(d.F, d.X, d.Y + 1, true);
        if (horiz && (!vert || TileAt(d.F, d.X, d.Y - 1) == Tile.Wall)) return new[] { (d.X - 1, d.Y, 'x'), (d.X + 1, d.Y, 'x') };
        return new[] { (d.X, d.Y - 1, 'y'), (d.X, d.Y + 1, 'y') };
    }

    private (int f, int x, int y) PlanPos(Unit u)
    {
        for (int i = u.Orders.Count - 1; i >= 0; i--) if (u.Orders[i].Kind == "move") return (u.Orders[i].F, u.Orders[i].X, u.Orders[i].Y);
        return (u.Floor, ToTile(u.X), ToTile(u.Y));
    }

    private Sides NearSide(Unit u, Door d, bool useQueue)
    {
        var s = DoorSides(d);
        var (pf, px, py) = useQueue ? PlanPos(u) : (u.Floor, ToTile(u.X), ToTile(u.Y));
        int Len((int, int, char) side)
        {
            var p = FindPath(pf, px, py, d.F, side.Item1, side.Item2, true);
            if (p == null) return 1_000_000;
            bool through = p.Any(n => n.X == d.X && n.Y == d.Y && n.F == d.F);
            return p.Count + (through ? 100 : 0);
        }
        return Len(s[0]) <= Len(s[1]) ? new Sides((s[0].Item1, s[0].Item2), (s[1].Item1, s[1].Item2), s[0].Item3) : new Sides((s[1].Item1, s[1].Item2), (s[0].Item1, s[0].Item2), s[0].Item3);
    }

    private List<(int, int)> StackTiles(Door d, Sides side, int n)
    {
        var (nx, ny) = side.Near;
        int dirX = nx - d.X, dirY = ny - d.Y;
        var perp = side.Axis == 'x' ? new[] { (0, -1), (0, 1) } : new[] { (-1, 0), (1, 0) };
        var cands = new[]
        {
            (nx + perp[0].Item1, ny + perp[0].Item2), (nx + perp[1].Item1, ny + perp[1].Item2),
            (nx + dirX + perp[0].Item1, ny + dirY + perp[0].Item2), (nx + dirX + perp[1].Item1, ny + dirY + perp[1].Item2),
            (nx + dirX, ny + dirY), (nx + dirX * 2, ny + dirY * 2), (nx + dirX * 2 + perp[0].Item1, ny + dirY * 2 + perp[0].Item2),
        };
        var o = cands.Where(c => Walkable(d.F, c.Item1, c.Item2, false) && TileAt(d.F, c.Item1, c.Item2) != Tile.Door).ToList();
        while (o.Count < n) o.Add((nx + dirX * o.Count, ny + dirY * o.Count));
        return o;
    }

    private List<(int, int)> RoomPoints(Door d, Sides side, int n)
    {
        var (fx, fy) = side.Far; int f = d.F;
        int rid = Map.Floors[f].Room[Map.Idx(fx, fy)];
        var tiles = new List<(int, int)>();
        if (rid >= 0)
        {
            var r = Map.Rooms[rid];
            for (int y = r.Y; y < r.Y + r.H; y++) for (int x = r.X; x < r.X + r.W; x++) if (Walkable(f, x, y, false) && TileAt(f, x, y) != Tile.Door) tiles.Add((x, y));
        }
        else
        {
            for (int y = fy - 4; y <= fy + 4; y++) for (int x = fx - 4; x <= fx + 4; x++)
                    if (Walkable(f, x, y, false) && TileAt(f, x, y) != Tile.Door && Los(f, Ctr(fx), Ctr(fy), Ctr(x), Ctr(y), true)) tiles.Add((x, y));
        }
        var o = new List<(int, int)>();
        if (tiles.Count == 0) { for (int i = 0; i < n; i++) o.Add(side.Far); return o; }
        int dirX = fx - d.X, dirY = fy - d.Y;
        var scored = tiles.Select(t => (t, lx: (t.Item1 - fx) * dirX + (t.Item2 - fy) * dirY, ly: (t.Item1 - fx) * (side.Axis == 'x' ? 0 : 1) + (t.Item2 - fy) * (side.Axis == 'x' ? 1 : 0))).ToList();
        Func<(int lx, int ly), double>[] fns =
        {
            s => -Math.Abs(s.ly) * 2 - Math.Abs(s.lx - 2), s => -s.ly + s.lx * 0.7 - Math.Abs(s.lx - 3), s => s.ly + s.lx * 0.7 - Math.Abs(s.lx - 3),
            s => s.lx - Math.Abs(s.ly) * 0.5, s => -s.ly * 2 + s.lx, s => s.ly * 2 + s.lx, s => s.lx, s => -Math.Abs(s.ly),
        };
        for (int i = 0; i < n; i++)
        {
            (int, int)? best = null; double bs = -1e9;
            foreach (var s in scored)
            {
                if (o.Contains(s.t)) continue;
                double v = fns[i % fns.Length]((s.lx, s.ly));
                if (v > bs) { bs = v; best = s.t; }
            }
            if (best.HasValue) o.Add(best.Value);
        }
        while (o.Count < n) o.Add(side.Far);
        return o;
    }

    private static bool HasUtil(Unit u, string id) => u.Util.Contains(id);

    /// <summary>Breach/clear a door with the given units. action: openclear, flashclear, kickclear, charge, shotgun, ram, pick, cam, open, close, stack.</summary>
    public bool OrderDoor(IEnumerable<Unit> units, Door d, string action, bool append)
    {
        var list = units.Where(CanAct).OrderBy(u => MathUtil.Dist(u.X, u.Y, Ctr(d.X), Ctr(d.Y))).ToList();
        if (list.Count == 0) return false;
        Func<Unit, bool> need = action switch
        {
            "flashclear" => u => HasUtil(u, "flash"),
            "charge" => u => HasUtil(u, "charge"),
            "pick" => u => u.Tool == "lockpick",
            "cam" => u => u.Tool == "doorcam",
            "ram" => u => u.Tool == "ram",
            "shotgun" => u => u.Weapons.Any(w => w.St.Cat == "sg"),
            _ => null,
        };
        if (need != null)
        {
            int idx = list.FindIndex(u => need(u));
            if (idx < 0) { AddLog("Selected operators lack the required equipment.", "#d9534f"); Snd("error", 0, 0); return false; }
            if (idx > 0) { var x = list[idx]; list.RemoveAt(idx); list.Insert(0, x); }
        }
        var leader = list[0];
        var side = NearSide(leader, d, append);
        var stack = StackTiles(d, side, list.Count);
        bool clear = action is "openclear" or "flashclear" or "kickclear" or "charge" or "shotgun" or "ram";
        var pts = clear ? RoomPoints(d, side, list.Count) : null;
        string prim = action switch
        {
            "openclear" or "flashclear" => "entry", "kickclear" => "kick", "charge" => "charge", "shotgun" => "shotgun", "ram" => "ram",
            "pick" => "pick", "cam" => "cam", "open" => "open", "close" => "close", "kick" => "kick", _ => null,
        };
        for (int i = 0; i < list.Count; i++)
        {
            var u = list[i];
            if (!append) ClearOrders(u); else { u.Hold = false; u.Follow = null; }
            if (i == 0)
            {
                u.Orders.Add(new Order { Kind = "move", X = side.Near.Item1, Y = side.Near.Item2, F = d.F, Pace = "walk" });
                if (prim != null) u.Orders.Add(new Order { Kind = "door", DoorId = d.Id, Action = prim });
                if (action == "flashclear")
                {
                    double fx = Ctr(side.Far.Item1) + (side.Far.Item1 - d.X) * TS * 1.6, fy = Ctr(side.Far.Item2) + (side.Far.Item2 - d.Y) * TS * 1.6;
                    u.Orders.Add(new Order { Kind = "throw", Grenade = "flash", WX = fx, WY = fy, F = d.F });
                    u.Orders.Add(new Order { Kind = "wait", Dur = 1.25 });
                }
                if (action == "charge") u.Orders.Add(new Order { Kind = "wait", Dur = 0.4 });
                if (clear) u.Orders.Add(new Order { Kind = "move", X = pts[0].Item1, Y = pts[0].Item2, F = d.F, Pace = "walk", Clearing = true });
            }
            else
            {
                u.Orders.Add(new Order { Kind = "move", X = stack[i - 1].Item1, Y = stack[i - 1].Item2, F = d.F, Pace = "walk" });
                if (clear)
                {
                    u.Orders.Add(new Order { Kind = "waitdoor", DoorId = d.Id, Leader = leader, Delay = (action == "flashclear" ? 1.4 : 0.2) + (i - 1) * 0.35 });
                    u.Orders.Add(new Order { Kind = "move", X = pts[i].Item1, Y = pts[i].Item2, F = d.F, Pace = "walk", Clearing = true });
                }
            }
        }
        if (Phase == "live") Voice(leader, "moving");
        return true;
    }

    public bool OrderThrow(Unit u, string g, double wx, double wy, int f, bool append)
    {
        if (!CanAct(u)) return false;
        if (!HasUtil(u, g)) { AddLog($"{u.Name} has no {GameData.Item[g].Name}.", "#d9534f"); return false; }
        Give(u, new Order { Kind = "throw", Grenade = g, WX = wx, WY = wy, F = f }, append);
        return true;
    }

    public void OrderInteract(IEnumerable<Unit> units, string targetId, double tx, double ty, int tf, string kind, bool append)
    {
        var list = units.Where(CanAct).ToList();
        if (list.Count == 0) return;
        Unit best = list[0]; double bd = 1e18;
        foreach (var u in list)
        {
            double d = MathUtil.Dist(u.X, u.Y, tx, ty) + (u.Floor != tf ? 2000 : 0);
            if (kind == "heal" && u.Tool == "medkit") { best = u; break; }
            if (d < bd) { bd = d; best = u; }
        }
        Give(best, new Order { Kind = "interact", TargetId = targetId, InteractKind = kind }, append);
    }

    public void OrderHold(IEnumerable<Unit> units) { foreach (var u in units) if (CanAct(u)) { ClearOrders(u); u.Hold = true; } }
    public void OrderFollow(IEnumerable<Unit> units, Unit leader) { foreach (var u in units) if (CanAct(u) && u != leader) { ClearOrders(u); u.Follow = leader; } }
    public void OrderFace(IEnumerable<Unit> units, double wx, double wy) { foreach (var u in units) if (CanAct(u)) { u.WatchAngle = Math.Atan2(wy - u.Y, wx - u.X); if (u.Orders.Count == 0) u.Angle = u.WatchAngle.Value; } }
    public void OrderGoWait(IEnumerable<Unit> units) { foreach (var u in units) Give(u, new Order { Kind = "go" }, true); }
    public void OrderCancel(IEnumerable<Unit> units) { foreach (var u in units) if (CanAct(u)) ClearOrders(u); }
    public void SetPace(IEnumerable<Unit> units, string p) { foreach (var u in units) { u.Pace = p; foreach (var o in u.Orders) if (o.Kind == "move") o.Pace = p; } }

    public void ToggleCrouch(IEnumerable<Unit> units)
    {
        var l = units.ToList();
        bool c = l.Any(u => u.Stance != "crouch");
        foreach (var u in l) u.Stance = c ? "crouch" : "stand";
    }

    public int Go()
    {
        int n = 0;
        foreach (var u in Ops) if (u.Orders.Count > 0 && u.Orders[0].Kind == "go") { u.Orders.RemoveAt(0); n++; }
        if (n > 0) { AddLog("GO GO GO!", "#e2a33a"); Say("Go, go, go!"); }
        return n;
    }

    public void OrderCover(IEnumerable<Unit> units)
    {
        foreach (var u in units)
        {
            if (!CanAct(u)) continue;
            int tx0 = ToTile(u.X), ty0 = ToTile(u.Y);
            (int, int)? best = null; double bs = -1e9;
            var threats = Units.Where(e => e.Team == Team.Enemy && e.LastSeen.HasValue && e.Alive && !e.Restrained && e.Floor == u.Floor).ToList();
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int x = tx0 + dx, y = ty0 + dy;
                    if (!Walkable(u.Floor, x, y, false) || TileAt(u.Floor, x, y) == Tile.Door) continue;
                    int cover = 0;
                    foreach (var (ax, ay) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        byte t = TileAt(u.Floor, x + ax, y + ay);
                        if (t == Tile.Furn) cover += 2;
                        if (t == Tile.Wall || t == Tile.High) cover += 1;
                    }
                    int exposed = threats.Count(e => Los(u.Floor, Ctr(x), Ctr(y), e.LastSeen.Value.X, e.LastSeen.Value.Y, true));
                    double s = cover * 2 - exposed * 3 - Math.Sqrt(dx * dx + dy * dy) * 0.6;
                    if (s > bs) { bs = s; best = (x, y); }
                }
            ClearOrders(u);
            if (best.HasValue) u.Orders.Add(new Order { Kind = "move", X = best.Value.Item1, Y = best.Value.Item2, F = u.Floor, Pace = "run" });
            u.Stance = "crouch";
        }
    }

    public void Start()
    {
        Phase = "live"; Paused = false;
        AddLog("EXECUTE. All teams go.", "#e2a33a");
        Say("All teams, execute!");
    }

    // ================= Operator AI / order execution =================
    private void OpThink(Unit u, double dt)
    {
        if (u.Dead) return;
        if (u.Downed)
        {
            u.Status = u.Stable ? "DOWN (stable)" : $"DOWN — bleeding {Math.Ceiling(u.Bleed)}s";
            if (!u.Stable)
            {
                u.Bleed -= dt;
                if (u.Bleed <= 0) { u.Dead = true; u.Alive = false; AddLog($"{u.Name} has died.", "#d9534f"); Say("We lost him."); }
            }
            return;
        }
        if (u.Stun > 0) u.Stun -= dt;
        if (u.JamT > 0) u.JamT -= dt;
        if (u.ReloadT > 0) { u.ReloadT -= dt; if (u.ReloadT <= 0) FinishReload(u); }
        u.FireCd -= dt; u.RecoilHeat = Math.Max(0, u.RecoilHeat - dt * 4);
        bool aura = Ops.Any(o => o != u && CanAct(o) && (o.Role == "leader" || o.Pers.Aura) && o.Floor == u.Floor && MathUtil.Dist(o.X, o.Y, u.X, u.Y) < 7 * TS);
        u.Stress = Math.Max(0, u.Stress - dt * (u.Target != null ? 0.5 : 3) * (aura ? 1.8 : 1) * (1 + u.Stat("lea") / 200.0));

        Unit best = null; double bd = 1e18;
        foreach (var e in Units)
        {
            if (e.Team == Team.Op || !e.Alive || e.Dead || e.Floor != u.Floor) continue;
            if (!CanSee(u, e)) continue;
            e.SeenNow = true;
            if (e.Team == Team.Enemy && !e.Spotted)
            {
                e.Spotted = true;
                if (e.Armed && !e.Ai.Dummy && e.Ai.State != "surrender") { Voice(u, "contact"); u.Stress = Math.Min(100, u.Stress + 6 * u.Pers.Stress); }
            }
            if (e.Team == Team.Civ && !e.HostageBound && e.Ai.State != "secured" && e.Ai.State != "cower" && !e.Ai.Dummy && MathUtil.Dist(e.X, e.Y, u.X, u.Y) < 9 * TS && T - e.Ai.ToldT > 4)
            {
                e.Ai.ToldT = T;
                if (R.Next() < 0.85) { e.Ai.State = "cower"; e.Stance = "crouch"; e.Path = null; } else CivPanic(e);
            }
            if (e.Team != Team.Enemy) continue;
            double d = MathUtil.Dist(e.X, e.Y, u.X, u.Y);
            if (!e.Ai.Dummy && e.Alive && !e.Incap && !e.Restrained && e.Ai.State != "surrender" && d < 12 * TS)
            {
                bool canShout = e.Ai.Shouted < 0 || (e.Stun > 0 && T - e.Ai.Shouted > 2.5) || (!e.Armed && T - e.Ai.Shouted > 3);
                if (canShout && (e.Ai.State != "combat" || e.Stun > 0 || !e.Armed || e.Ai.AimingAt != u))
                {
                    e.Ai.Shouted = T; e.Ai.DecideT = 0.35 + R.Next() * 0.45; e.Ai.Shouter = u;
                    Voice(u, "comply");
                }
            }
            if (IsThreat(e) && d < bd) { bd = d; best = e; }
        }
        if (best != null)
        {
            var w = u.W;
            if (u.Target != best)
            {
                u.Target = best; u.AimT = 0;
                double quick = u.HasPerk("quickdraw") ? 0.8 : 1;
                u.ReactT = Math.Max(0.12, (0.5 - u.Stat("awa") * 0.0035 + u.Pers.React + u.Stress * 0.002) * quick * (1.15 - w.St.AimSpeed / 250) * (u.Role == "point" ? 0.9 : 1));
            }
            double ang = Math.Atan2(best.Y - u.Y, best.X - u.X);
            double rem = TurnTo(u, ang, dt, 6 + w.St.Handling / 12);
            u.AimT += dt;
            bool holdForComply = best.Ai.DecideT > 0 && best.Ai.State != "combat";
            bool aggressive = best.Ai.State == "combat" || best.Ai.Dummy || (best.Stun <= 0 && best.Ai.AimingAt != null);
            if (u.AimT >= u.ReactT && rem < 0.12 && !holdForComply && (aggressive || best.Ai.DecideT <= 0) && u.Stun <= 0 && LineClear(u, best))
            {
                if (w.Mag <= 0) StartReload(u);
                else if (u.BurstPause > 0) u.BurstPause -= dt;
                else if (Fire(u, best.X, best.Y))
                {
                    double dd = MathUtil.Dist(best.X, best.Y, u.X, u.Y) / TS;
                    if (w.St.Mode == "auto") { u.Burst++; if (u.Burst >= (dd < 5 ? 3 : dd < 12 ? 2 : 1)) { u.Burst = 0; u.BurstPause = dd < 5 ? 0.12 : 0.28; } }
                    else if (w.St.Mode == "semi") u.FireCd = Math.Max(u.FireCd, 0.16 + dd * 0.01);
                }
            }
            u.Status = u.ReloadT > 0 ? "Reloading" : "Engaging";
        }
        else
        {
            if (u.Target != null) { u.Target = null; u.Burst = 0; }
            var w = u.W;
            if (w != null && u.ReloadT <= 0 && w.Mag < w.St.Mag * 0.35 && w.Reserve > 0 && u.Act == null) StartReload(u);
        }
        if (u.Stun > 0 && best == null) u.Status = "Stunned";
        if (u.Orders.Count > 0)
        {
            var o = u.Orders[0];
            string r = ExecOrder(u, o, dt, best != null);
            if (r == "done") { if (u.Orders.Count > 0 && u.Orders[0] == o) u.Orders.RemoveAt(0); u.Path = null; }
            else if (r == "fail") { u.Orders.Clear(); u.Path = null; u.Act = null; }
        }
        else if (u.Follow != null) FollowTick(u, dt, best);
        else
        {
            u.Status = u.ReloadT > 0 ? "Reloading" : u.Hold ? "Holding" : best != null ? "Engaging" : "Idle";
            if (best == null)
            {
                if (u.WatchAngle.HasValue) TurnTo(u, u.WatchAngle.Value, dt, 5);
                else IdleScan(u, dt);
                if (!u.Hold) AutoInteract(u);
            }
        }
        if (best == null && T - u.AlertT < 2.5 && u.AlertAng.HasValue) { TurnTo(u, u.AlertAng.Value, dt, 11); return; }
        if (best == null && u.Path != null && u.MoveAng.HasValue && u.Act == null) TurnTo(u, u.MoveAng.Value, dt, 8);
    }

    private void IdleScan(Unit u, double dt)
    {
        if (u.ScanT < T)
        {
            u.ScanT = T + 2 + R.Next() * 2;
            Door best = null; double bd = 1e9;
            foreach (var d in Map.Doors)
            {
                if (d.F != u.Floor || d.State == DoorState.Closed) continue;
                double dd = MathUtil.Dist(Ctr(d.X), Ctr(d.Y), u.X, u.Y);
                if (dd < 7 * TS && dd > TS && dd < bd && Los(u.Floor, u.X, u.Y, Ctr(d.X), Ctr(d.Y), true)) { bd = dd; best = d; }
            }
            u.ScanAng = best != null ? Math.Atan2(Ctr(best.Y) - u.Y, Ctr(best.X) - u.X) : null;
        }
        if (u.ScanAng.HasValue) TurnTo(u, u.ScanAng.Value, dt, 4);
    }

    private bool LineClear(Unit u, Unit target)
    {
        double dx = target.X - u.X, dy = target.Y - u.Y, d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 1) return true;
        foreach (var o in Units)
        {
            if (o == u || o == target || o.Floor != u.Floor || o.Dead || !o.Alive) continue;
            if (o.Team == Team.Enemy && !o.Restrained && o.Ai.State != "surrender") continue;
            if (o.Downed || o.Incap) continue;
            double ox = o.X - u.X, oy = o.Y - u.Y, tp = (ox * dx + oy * dy) / d;
            if (tp < 0 || tp > d) continue;
            if (Math.Abs(ox * dy - oy * dx) / d < 12) return false;
        }
        return true;
    }

    private void AutoInteract(Unit u)
    {
        if (u.Act != null) return;
        foreach (var e in Units)
        {
            if (e.Floor != u.Floor || e.Dead || !e.Alive) continue;
            if (MathUtil.Dist(e.X, e.Y, u.X, u.Y) > 3.2 * TS) continue;
            if (e.Team == Team.Enemy && !e.Restrained && (e.Ai.State == "surrender" || e.Incap) && Los(u.Floor, u.X, u.Y, e.X, e.Y, true))
            { u.Orders.Add(new Order { Kind = "interact", TargetId = e.Id, InteractKind = "arrest", Auto = true }); return; }
            if (e.Team == Team.Civ && !e.Ai.Dummy && e.Ai.State != "secured" && (e.HostageBound || e.Ai.State == "cower") && Los(u.Floor, u.X, u.Y, e.X, e.Y, true))
            { u.Orders.Add(new Order { Kind = "interact", TargetId = e.Id, InteractKind = "secure", Auto = true }); return; }
        }
        foreach (var it in Items)
        {
            if (it.Collected || it.F != u.Floor || (it.Kind != "evidence" && it.Kind != "contraband")) continue;
            if (MathUtil.Dist(it.X, it.Y, u.X, u.Y) < 1.6 * TS && Los(u.Floor, u.X, u.Y, it.X, it.Y, true))
            { u.Orders.Add(new Order { Kind = "interact", TargetId = it.Id, InteractKind = "collect", Auto = true }); return; }
        }
    }

    private void FollowTick(Unit u, double dt, Unit threat)
    {
        var L = u.Follow;
        if (L == null || !CanAct(L)) { u.Follow = null; return; }
        var followers = Ops.Where(o => o.Follow == L).ToList();
        int i = followers.IndexOf(u) + 1;
        double back = L.Angle + Math.PI, ox, oy;
        if (Formation == "stack") { ox = Math.Cos(back) * i * 1.1 * TS; oy = Math.Sin(back) * i * 1.1 * TS; }
        else
        {
            int side = i % 2 == 1 ? 1 : -1, row = (i + 1) / 2;
            ox = Math.Cos(back) * row * TS + Math.Cos(back + Math.PI / 2) * side * row * TS;
            oy = Math.Sin(back) * row * TS + Math.Sin(back + Math.PI / 2) * side * row * TS;
        }
        int tx = ToTile(L.X + ox), ty = ToTile(L.Y + oy);
        if (!Walkable(L.Floor, tx, ty, false) || !Los(L.Floor, L.X, L.Y, Ctr(tx), Ctr(ty), true)) { tx = ToTile(L.X); ty = ToTile(L.Y); }
        bool far = MathUtil.Dist(Ctr(tx), Ctr(ty), u.X, u.Y) > TS * 0.9 || u.Floor != L.Floor;
        u.FollowT -= dt;
        if (far && (u.FollowT <= 0 || u.Path == null)) { u.FollowT = 0.5; SetPath(u, L.Floor, tx, ty); }
        if (u.Path != null) { u.Pace = L.Pace; FollowPath(u, dt); }
        else if (threat == null) TurnTo(u, L.Angle + (i % 2 == 1 ? 0.6 : -0.6), dt, 4);
        u.Status = "Following " + L.Name;
    }

    private string ExecOrder(Unit u, Order o, double dt, bool engaged)
    {
        switch (o.Kind)
        {
            case "move":
            {
                if (!o.Started) { o.Started = true; if (!SetPath(u, o.F, o.X, o.Y)) { AddLog($"{u.Name}: no route.", "#d9534f"); return "fail"; } }
                u.Pace = o.Pace ?? u.Pace;
                u.Status = o.Clearing ? "Clearing" : u.Pace == "run" ? "Running" : u.Pace == "slow" ? "Moving (slow)" : "Moving";
                string r = FollowPath(u, dt);
                if (r == "blocked") { AddLog($"{u.Name}: path blocked.", "#e2a33a"); return "fail"; }
                if (r == "done") { if (o.Clearing) MaybeRoomClear(u); return "done"; }
                return "busy";
            }
            case "door":
            {
                var d = Map.Doors[o.DoorId];
                if (engaged && o.Action != "charge") { u.Status = "Engaging"; return "busy"; }
                TurnTo(u, Math.Atan2(Ctr(d.Y) - u.Y, Ctr(d.X) - u.X), dt, 8);
                string r = DoDoorAction(u, d, o.Action, dt);
                u.Status = o.Action switch
                {
                    "entry" => "Opening", "open" => "Opening door", "kick" => "Kicking door", "ram" => "Ramming", "shotgun" => "Shotgun breach",
                    "charge" => "Setting charge", "pick" => "Picking lock", "cam" => "Door camera", "close" => "Closing door", _ => "Door",
                };
                return r;
            }
            case "throw":
            {
                if (o.F != u.Floor) return "done";
                double rem = TurnTo(u, Math.Atan2(o.WY - u.Y, o.WX - u.X), dt, 10);
                o.T0 += dt;
                u.Status = "Throwing";
                if (rem < 0.2 && o.T0 > 0.35) { if (!ThrowGrenade(u, o.Grenade, o.WX, o.WY)) AddLog($"{u.Name}: none left.", "#d9534f"); return "done"; }
                return "busy";
            }
            case "wait": o.El += dt; u.Status = "Waiting"; return o.El >= o.Dur ? "done" : "busy";
            case "go": u.Status = "WAITING FOR GO"; return "busy";
            case "waitdoor":
            {
                var d = Map.Doors[o.DoorId];
                u.Status = "Stacked up";
                if (d.State != DoorState.Closed) { o.El += dt; return o.El >= o.Delay ? "done" : "busy"; }
                var L = o.Leader;
                if (L == null || !CanAct(L) || !L.Orders.Any(x => x.Kind == "door" && x.DoorId == d.Id)) return "fail";
                return "busy";
            }
            case "interact": return ExecInteract(u, o, dt, engaged);
        }
        return "done";
    }

    private void MaybeRoomClear(Unit u)
    {
        int rid = Map.Floors[u.Floor].Room[Map.Idx(ToTile(u.X), ToTile(u.Y))];
        if (rid < 0) return;
        var r = Map.Rooms[rid];
        bool threats = Units.Any(e => e.Team == Team.Enemy && e.Floor == u.Floor && IsThreat(e) && ToTile(e.X) >= r.X && ToTile(e.X) < r.X + r.W && ToTile(e.Y) >= r.Y && ToTile(e.Y) < r.Y + r.H);
        if (!threats && T - _lastClear > 3) { _lastClear = T; Voice(u, "clear"); }
    }

    private string ExecInteract(Unit u, Order o, double dt, bool engaged)
    {
        MissionItem item = null; Unit tgt = null;
        if (o.InteractKind == "collect") item = Items.FirstOrDefault(i => i.Id == o.TargetId);
        else tgt = Units.FirstOrDefault(x => x.Id == o.TargetId);
        if (item == null && tgt == null) return "done";
        double tx = item?.X ?? tgt.X, ty = item?.Y ?? tgt.Y; int tf = item?.F ?? tgt.Floor;
        switch (o.InteractKind)
        {
            case "arrest":
                if (tgt.Dead || !tgt.Alive || tgt.Restrained || !(tgt.Ai.State == "surrender" || tgt.Incap || tgt.Stun > 0))
                    return tgt.Ai.State == "combat" && tgt.Stun <= 0 && !tgt.Dead ? "fail" : "done";
                break;
            case "secure": if (tgt.Dead || !tgt.Alive || tgt.Ai.State == "secured") return "done"; break;
            case "heal": if (tgt.Dead || (!tgt.Downed && tgt.Hp >= tgt.MaxHp * 0.9) || (tgt.Downed && tgt.Stable && u.Tool != "medkit")) return "done"; break;
            case "collect": if (item.Collected) return "done"; break;
        }
        double d = MathUtil.Dist(tx, ty, u.X, u.Y);
        if (tf != u.Floor || d > 1.35 * TS)
        {
            if (!o.Pathed || (u.Path == null && u.Act == null)) { o.Pathed = true; if (!SetPath(u, tf, ToTile(tx), ToTile(ty))) return "fail"; }
            string r = FollowPath(u, dt); u.Status = "Moving";
            if (r == "blocked") return "fail";
            if (r == "done" && (tf != u.Floor || MathUtil.Dist(tx, ty, u.X, u.Y) > 1.35 * TS)) o.Pathed = false;
            return "busy";
        }
        u.Path = null;
        if (engaged) { u.Status = "Engaging"; return "busy"; }
        TurnTo(u, Math.Atan2(ty - u.Y, tx - u.X), dt, 8);
        if (o.Dur <= 0)
        {
            o.Dur = o.InteractKind switch
            {
                "arrest" => 1.5, "secure" => 1.2, "collect" => 1.6,
                _ => (u.HasPerk("medic") ? 2 : 4) * (1.3 - u.Stat("med") / 200.0) * (u.Role == "medic" ? 0.75 : 1),
            };
            if (o.InteractKind is "arrest" or "secure") Snd("cuffs", tx, ty);
        }
        o.El += dt;
        u.Status = o.InteractKind switch { "arrest" => "Restraining", "secure" => "Securing civilian", "collect" => "Bagging evidence", _ => "Treating " + tgt?.Name };
        if (o.El < o.Dur) return "busy";
        switch (o.InteractKind)
        {
            case "arrest":
                tgt.Restrained = true; tgt.Ai.State = "restrained"; tgt.Surrendered = true; tgt.Stun = 0; tgt.Path = null;
                if (tgt.Armed) { tgt.Armed = false; DropWeapon(tgt.X + 8, tgt.Y + 6, tgt.Floor); }
                u.Arrests++; Voice(u, "arrest"); AddLog($"{tgt.Name} restrained by {u.Name}.", "#5cb85c");
                if (tgt.Hvt) { AddLog($"HVT {tgt.Name} CAPTURED ALIVE.", "#5cb85c"); Say("Primary target in custody!"); }
                break;
            case "secure":
                tgt.Ai.State = "secured"; tgt.Path = null; tgt.Stance = "crouch"; tgt.Kneel = true; tgt.Restrained = true; tgt.HostageBound = false;
                u.Rescues++; Voice(u, "secure"); AddLog($"{tgt.Name} secured by {u.Name}.", "#5cb85c");
                break;
            case "collect":
                item.Collected = true; u.EvidenceFound++; Voice(u, "evidence"); AddLog($"{u.Name} recovered {item.Name}.", "#4fc3f7"); Snd("equip", tx, ty);
                break;
            case "heal":
                bool kit = u.Tool == "medkit" && u.ToolUses > 0;
                if (tgt.Downed)
                {
                    if (kit) { u.ToolUses--; tgt.Downed = false; tgt.Hp = 35 + u.Stat("med") / 5.0; tgt.Stable = false; AddLog($"{u.Name} revived {tgt.Name}.", "#5cb85c"); }
                    else { tgt.Stable = true; AddLog($"{u.Name} stabilized {tgt.Name}.", "#5cb85c"); }
                }
                else if (kit) { u.ToolUses--; tgt.Hp = Math.Min(tgt.MaxHp, tgt.Hp + 30 + u.Stat("med") / 4.0); AddLog($"{u.Name} treated {tgt.Name}.", "#5cb85c"); }
                break;
        }
        return "done";
    }

    private void DropWeapon(double x, double y, int f) =>
        Items.Add(new MissionItem { Id = MathUtil.Uid("drop"), Kind = "drop", Type = "weapon", X = x, Y = y, F = f });

    // ================= Doors =================
    private void SetDoor(Door d, DoorState s)
    {
        d.State = s;
        Events.Add(new SimEvent { T = T, Type = "door", Id = d.Id, DoorState = s.ToString() });
    }

    private void DoorBlast(Door d, double power)
    {
        double dx = Ctr(d.X), dy = Ctr(d.Y);
        foreach (var u in Units)
        {
            if (!u.Alive || u.Dead || u.Floor != d.F) continue;
            double dd = MathUtil.Dist(u.X, u.Y, dx, dy);
            if (dd < 3 * TS * power)
            {
                u.Stun = Math.Max(u.Stun, (u.Team == Team.Op ? 0.6 : 3.5) * power * (1 - dd / (3 * TS * power) + 0.3));
                if (dd < 1.3 * TS && power >= 1)
                {
                    u.Hp -= 30;
                    if (u.Hp <= 0) { if (u.Team == Team.Op) DownOp(u); else { u.Dead = true; u.Alive = false; } }
                }
                if (u.Team == Team.Enemy) u.Ai.Morale -= 15;
            }
        }
    }

    private string DoDoorAction(Unit u, Door d, string a, double dt)
    {
        var act = u.Act;
        double cx = Ctr(d.X), cy = Ctr(d.Y);
        if (act == null)
        {
            double brk = u.HasPerk("breacher") ? 0.6 : 1, dur = 0.4;
            if (a == "entry")
            {
                if (d.State != DoorState.Closed) return "done";
                if (d.Barricaded) { AddLog($"{u.Name}: Door is barricaded — need a breaching charge.", "#d9534f"); Voice(u, "locked"); d.KnownLocked = true; return "fail"; }
                if (d.Locked && !d.KnownLocked) { d.KnownLocked = true; Voice(u, "locked"); u.Act = new TimedAct { Type = "tryopen", Dur = 0.5 }; return "busy"; }
                a = d.Locked ? (u.Tool == "ram" ? "ram" : u.Weapons.Any(w => w.St.Cat == "sg") ? "shotgun" : "kick") : "open";
            }
            switch (a)
            {
                case "open":
                    if (d.State != DoorState.Closed) return "done";
                    if (d.Barricaded) { d.KnownLocked = true; AddLog($"{u.Name}: barricaded!", "#d9534f"); return "fail"; }
                    if (d.Locked) { d.KnownLocked = true; Voice(u, "locked"); return "fail"; }
                    dur = 0.4; break;
                case "close": if (d.State != DoorState.Open) return "done"; dur = 0.4; break;
                case "kick": if (d.State == DoorState.Broken) return "done"; if (d.Barricaded) { AddLog("Barricaded — kick won't work.", "#d9534f"); return "fail"; } dur = 1.0 * brk; break;
                case "ram": if (d.State == DoorState.Broken) return "done"; if (d.Barricaded) { AddLog("Barricaded — need a charge.", "#d9534f"); return "fail"; } dur = 1.3 * brk; break;
                case "shotgun":
                {
                    if (d.State == DoorState.Broken) return "done";
                    if (d.Barricaded) return "fail";
                    dur = 0.9 * brk;
                    int sg = u.Weapons.FindIndex(w => w.St.Cat == "sg"); if (sg >= 0) u.Wi = sg;
                    break;
                }
                case "charge":
                    if (d.State == DoorState.Broken) return "done";
                    if (!HasUtil(u, "charge")) { AddLog($"{u.Name}: no breaching charge.", "#d9534f"); return "fail"; }
                    dur = (2.8 - u.Stat("tec") / 100.0) * brk; Voice(u, "breach"); break;
                case "pick":
                    if (!d.Locked || d.State != DoorState.Closed) return "done";
                    if (u.Tool != "lockpick") return "fail";
                    dur = Math.Max(2, 7 - u.Stat("tec") / 18.0) * (u.HasPerk("locksmith") ? 0.5 : 1); break;
                case "cam": if (d.State != DoorState.Closed) return "done"; if (u.Tool != "doorcam") return "fail"; dur = 1.4; break;
            }
            u.Act = new TimedAct { Type = a, Dur = dur, DoorId = d.Id };
            if (a is "kick" or "ram") Voice(u, "breach");
            return "busy";
        }
        double prevT = act.T;
        act.T += dt;
        if (act.Type == "pick" && Math.Floor(act.T * 3) != Math.Floor(prevT * 3)) Snd("pick", u.X, u.Y);
        if (act.T < act.Dur) return "busy";
        u.Act = null;
        switch (act.Type)
        {
            case "tryopen": return "busy";
            case "open": SetDoor(d, DoorState.Open); Snd("doorOpen", cx, cy); MakeNoise(cx, cy, d.F, 3, "door", u); return "done";
            case "close": SetDoor(d, DoorState.Closed); Snd("doorClose", cx, cy); return "done";
            case "kick":
            case "ram":
            {
                double p = !d.Locked ? 1 : act.Type == "ram" ? 0.9 : (u.HasPerk("breacher") ? 0.85 : 0.45);
                bool ok = R.Next() < p;
                Snd("kick", cx, cy, flag: ok);
                MakeNoise(cx, cy, d.F, 16, "breach");
                if (ok) { SetDoor(d, DoorState.Broken); d.Locked = false; DoorBlast(d, 0.35); AddFx(new FxEvent { Type = "splinters", X = cx, Y = cy, F = d.F }); return "done"; }
                u.Act = new TimedAct { Type = act.Type, Dur = act.Type == "ram" ? 1.1 : 0.9, DoorId = d.Id };
                return "busy";
            }
            case "shotgun":
            {
                var w = u.W;
                if (w != null && w.Mag > 0) w.Mag--;
                Snd("shot", cx, cy, "12g");
                MakeNoise(cx, cy, d.F, 26, "gunshot");
                SetDoor(d, DoorState.Broken); d.Locked = false; DoorBlast(d, 0.4);
                AddFx(new FxEvent { Type = "splinters", X = cx, Y = cy, F = d.F });
                AddFx(new FxEvent { Type = "muzzle", X = cx, Y = cy, F = d.F, Ang = u.Angle });
                return "done";
            }
            case "charge":
                u.Util.Remove("charge"); _charges++;
                Snd("explosion", cx, cy);
                MakeNoise(cx, cy, d.F, 36, "explosion");
                SetDoor(d, DoorState.Broken); d.Locked = false; d.Barricaded = false;
                AddFx(new FxEvent { Type = "explosion", X = cx, Y = cy, F = d.F, R = 50 });
                AddFx(new FxEvent { Type = "splinters", X = cx, Y = cy, F = d.F });
                Events.Add(new SimEvent { T = T, Type = "boom", G = "charge", X0 = (int)cx, Y0 = (int)cy, F = d.F });
                DoorBlast(d, 1);
                return "done";
            case "pick":
                d.Locked = false; d.KnownLocked = false; AddLog($"{u.Name}: lock picked.", "#5cb85c");
                SetDoor(d, DoorState.Open); Snd("doorOpen", cx, cy); return "done";
            case "cam":
            {
                var side = NearSide(u, d, false);
                var (fx, fy) = side.Far;
                CamSources.Add(new CamSource { X = Ctr(fx), Y = Ctr(fy), F = d.F, Angle = Math.Atan2(fy - d.Y, fx - d.X), T = 3.5 });
                AddLog($"{u.Name}: inspecting under the door…", "#4fc3f7");
                return "done";
            }
        }
        return "done";
    }

    // ================= Movement =================
    private double MoveSpeed(Unit u)
    {
        double s = u.Team == Team.Op ? (u.Pace == "run" ? 150 : u.Pace == "slow" ? 55 : 88) * u.SpeedMult
            : u.Team == Team.Enemy ? (u.Ai.State == "flee" || (u.Ai.State == "combat" && u.Ai.Style == "rusher") ? 125 : 70)
            : (u.Ai.State == "flee" ? 120 : 60);
        if (u.Stance == "crouch") s *= 0.55;
        if (u.Shield) s *= 0.8;
        if (u.Hp < u.MaxHp * 0.5) s *= 0.8;
        if (u.Stun > 0) s *= 0.3;
        if (u.Gas > 0 && u.Team != Team.Op) s *= 0.6;
        if (u.Team == Team.Op && u.Target != null && u.Pace != "run") s *= 0.75;
        return s;
    }

    private bool SetPath(Unit u, int f, int x, int y)
    {
        u.Path = FindPath(u.Floor, ToTile(u.X), ToTile(u.Y), f, x, y, u.Team == Team.Op);
        u.Pi = 0;
        return u.Path != null;
    }

    /// <summary>Returns done | moving | blocked.</summary>
    private string FollowPath(Unit u, double dt)
    {
        if (u.Path == null) return "done";
        if (u.Act != null)
        {
            string r = ActOnTile(u, dt);
            if (r != "done") return r == "fail" ? "blocked" : "moving";
        }
        if (u.Pi >= u.Path.Count) { u.Path = null; return "done"; }
        var n = u.Path[u.Pi];
        if (n.F != u.Floor) { u.Floor = n.F; u.Pi++; u.Act = new TimedAct { Type = "stairs", Dur = 0.5 }; return "moving"; }
        byte t = TileAt(n.F, n.X, n.Y);
        if (t == Tile.Door)
        {
            var d = DoorAt(n.F, n.X, n.Y);
            if (d.State == DoorState.Closed)
            {
                if (d.Barricaded) { d.KnownLocked = true; if (u.Team == Team.Op) AddLog($"{u.Name}: door barricaded.", "#d9534f"); return "blocked"; }
                if (d.Locked && u.Team == Team.Op) { d.KnownLocked = true; Voice(u, "locked"); return "blocked"; }
                u.Act = new TimedAct { Type = "open", Dur = u.Team == Team.Op ? 0.45 : 0.6, DoorId = d.Id };
                return "moving";
            }
        }
        if (t == Tile.Window) { u.Act = new TimedAct { Type = "vault", Dur = 1.0, Tx = n.X, Ty = n.Y }; return "moving"; }
        double gx = Ctr(n.X), gy = Ctr(n.Y), dx = gx - u.X, dy = gy - u.Y, dist = Math.Sqrt(dx * dx + dy * dy);
        double sp = MoveSpeed(u) * dt;
        if (dist <= sp) { u.X = gx; u.Y = gy; u.Pi++; }
        else { u.X += dx / dist * sp; u.Y += dy / dist * sp; }
        u.MoveAng = Math.Atan2(dy, dx);
        u.StepT -= dt;
        if (u.StepT <= 0)
        {
            u.StepT = u.Pace == "run" ? 0.3 : u.Pace == "slow" ? 0.65 : 0.45;
            double r = u.Team == Team.Op ? (u.Pace == "run" ? 5 : u.Pace == "slow" ? 1 : 2.2) : 2;
            MakeNoise(u.X, u.Y, u.Floor, r, "step", u);
            if ((u.Team == Team.Op || u.Visible) && R.Next() < 0.8) Snd(u.Pace == "run" ? "stepRun" : "step", u.X, u.Y);
        }
        return "moving";
    }

    private string ActOnTile(Unit u, double dt)
    {
        var a = u.Act; a.T += dt;
        if (a.T < a.Dur) return "busy";
        u.Act = null;
        if (a.Type == "open")
        {
            var d = Map.Doors[a.DoorId];
            if (d.State == DoorState.Closed) { SetDoor(d, DoorState.Open); Snd("doorOpen", Ctr(d.X), Ctr(d.Y)); MakeNoise(Ctr(d.X), Ctr(d.Y), d.F, 3, "door", u); }
        }
        if (a.Type == "vault" && TileAt(u.Floor, a.Tx, a.Ty) == Tile.Window)
        {
            BreakWindow(u.Floor, a.Tx, a.Ty);
            MakeNoise(Ctr(a.Tx), Ctr(a.Ty), u.Floor, 12);
        }
        return "done";
    }

    private void BreakWindow(int f, int tx, int ty)
    {
        Map.Floors[f].T[Map.Idx(tx, ty)] = Tile.BrokenWindow;
        Map.Version++;
        Events.Add(new SimEvent { T = T, Type = "tile", F = f, X0 = tx, Y0 = ty });
        AddFx(new FxEvent { Type = "glass", X = Ctr(tx), Y = Ctr(ty), F = f });
        Snd("glass", Ctr(tx), Ctr(ty));
    }

    private static double TurnTo(Unit u, double ang, double dt, double rate = 9)
    {
        double d = MathUtil.AngDiff(u.Angle, ang), s = rate * dt;
        u.Angle = Math.Abs(d) <= s ? ang : MathUtil.AngNorm(u.Angle + Math.Sign(d) * s);
        return Math.Abs(MathUtil.AngDiff(u.Angle, ang));
    }

    private void Separate()
    {
        var list = Units.Where(u => u.Alive && !u.Dead && !u.Downed && !u.Incap).ToList();
        for (int i = 0; i < list.Count; i++)
            for (int j = i + 1; j < list.Count; j++)
            {
                var a = list[i]; var b = list[j];
                if (a.Floor != b.Floor) continue;
                double dx = b.X - a.X, dy = b.Y - a.Y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 0.01 && d < 15)
                {
                    double push = (15 - d) * 0.25, nx = dx / d * push, ny = dy / d * push;
                    double am = a.Path != null || a.Team != Team.Op ? 1 : 0.4, bm = b.Path != null || b.Team != Team.Op ? 1 : 0.4;
                    if (!a.HostageBound && Walkable(a.Floor, ToTile(a.X - nx), ToTile(a.Y - ny), false)) { a.X -= nx * am; a.Y -= ny * am; }
                    if (!b.HostageBound && Walkable(b.Floor, ToTile(b.X + nx), ToTile(b.Y + ny), false)) { b.X += nx * bm; b.Y += ny * bm; }
                }
            }
    }
}
