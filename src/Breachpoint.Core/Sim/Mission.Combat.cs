using Breachpoint.Core.Map;
using Breachpoint.Core.Util;

namespace Breachpoint.Core.Sim;

public sealed partial class Mission
{
    public static bool IsThreat(Unit e)
    {
        if (e.Team != Team.Enemy) return false;
        if (e.Ai.Dummy) return e.Alive && !e.Dead;
        return e.Alive && !e.Dead && !e.Incap && !e.Restrained && e.Armed && e.Ai.State != "surrender";
    }

    private double OpSpread(Unit u, SimWeapon w)
    {
        double s = w.St.Spread * (1.55 - u.Stat("acc") / 100.0) - u.Pers.Acc * 0.03;
        if (u.Path != null) s *= u.Pace == "run" ? 2.2 : u.Pace == "slow" ? 1.05 : 1.45;
        if (u.Stance == "crouch") s *= 0.8;
        s *= 1 + u.Stress / 140;
        if (u.Hp < u.MaxHp * 0.5) s *= 1.3;
        if (u.Stun > 0) s *= 3;
        if (u.Gas > 0) s *= 1.15;
        s += u.RecoilHeat * (1 - w.St.Ergonomics / 220) * (u.HasPerk("steady") ? 0.75 : 1);
        return Math.Max(0.15, s);
    }

    private double EnemySpread(Unit e, SimWeapon w)
    {
        double s = w.St.Spread * (1 + (1 - e.Acc) * 2.2) / Diff.EnemyAcc;
        var tg = e.Target;
        if (tg != null)
        {
            if (tg.Path != null) s *= tg.Pace == "run" ? 1.45 : 1.2;
            if (tg.Stance == "crouch") s *= 1.15;
            double d = MathUtil.Dist(tg.X, tg.Y, e.X, e.Y) / TS;
            if (d > 12) s *= 1 + (d - 12) * 0.04;
        }
        if (e.Path != null) s *= 1.6;
        if (e.Gas > 0) s *= 3;
        if (e.Stun > 0) s *= 4;
        if (e.Hp < 50) s *= 1.3;
        return s + e.RecoilHeat * 1.2;
    }

    private bool IsIndoor(Unit u) => TileAt(u.Floor, ToTile(u.X), ToTile(u.Y)) != Tile.Ground;

    private bool Fire(Unit u, double tx, double ty)
    {
        var w = u.W;
        if (w == null || u.ReloadT > 0 || u.JamT > 0 || u.FireCd > 0) return false;
        if (w.Mag <= 0) { StartReload(u); return false; }
        if (R.Next() < (1 - w.St.Reliability / 100) * 0.015)
        {
            u.JamT = 1.6; Snd("dryfire", u.X, u.Y);
            if (u.Team == Team.Op) Voice(u, "jam");
            return false;
        }
        w.Mag--; w.Fired++; u.Shots++;
        double baseAng = Math.Atan2(ty - u.Y, tx - u.X);
        double spread = (u.Team == Team.Op ? OpSpread(u, w) : EnemySpread(u, w)) * Math.PI / 180;
        var ammo = u.Ammo ?? Data.GameData.Ammo[0];
        double dmg = w.St.Dmg * ammo.Dmg, pen = w.St.Pen * ammo.Pen;
        for (int p = 0; p < w.St.Pellets; p++)
        {
            double a = baseAng + R.Gauss() * spread * 0.5 + (w.St.Pellets > 1 ? R.Gauss() * 0.045 : 0);
            TraceShot(u, a, dmg, pen);
        }
        u.RecoilHeat = Math.Min(8, u.RecoilHeat + w.St.Recoil / 100 * (w.St.Mode == "auto" ? 1.2 : 0.8));
        u.FireCd = 60.0 / w.St.Rpm;
        double quiet = ammo.Id == "sub" && w.St.Suppressed ? 0.6 : 1;
        double loud = w.St.Suppressed ? 6 * quiet : w.St.Cal is "7.62" or ".338" or "12g" ? 32 : 26;
        MakeNoise(u.X, u.Y, u.Floor, loud, "gunshot", u);
        if (u.Team == Team.Enemy)
            foreach (var o in Units)
            {
                if (o.Team != Team.Op || !CanAct(o) || o.Floor != u.Floor || o.Target != null) continue;
                if (MathUtil.Dist(o.X, o.Y, u.X, u.Y) < 18 * TS && Los(u.Floor, o.X, o.Y, u.X, u.Y, true)) { o.AlertAng = Math.Atan2(u.Y - o.Y, u.X - o.X); o.AlertT = T; }
            }
        AddFx(new FxEvent { Type = "muzzle", X = u.X + Math.Cos(baseAng) * 20, Y = u.Y + Math.Sin(baseAng) * 20, F = u.Floor, Ang = baseAng, Sup = w.St.Suppressed, Team = u.Team });
        if (w.St.Mode != "pump" && w.St.Mode != "bolt") AddFx(new FxEvent { Type = "shell", X = u.X, Y = u.Y, F = u.Floor, Ang = baseAng + Math.PI / 2 });
        Sounds.Add(new SoundEvent { Name = w.St.Suppressed ? "shotSup" : "shot", X = u.X, Y = u.Y, Cal = w.St.Cal, Flag = IsIndoor(u) });
        if (w.St.Mode is "pump" or "bolt") Snd("pump", u.X, u.Y);
        else if (R.Next() < 0.5) Snd("shell", u.X, u.Y, w.St.Cal);
        if (u.Team == Team.Op) _shotsOp++;
        return true;
    }

    private void StartReload(Unit u)
    {
        var w = u.W;
        if (u.ReloadT > 0 || w == null) return;
        if (w.Reserve <= 0)
        {
            int alt = -1;
            for (int i = 0; i < u.Weapons.Count; i++)
                if (i != u.Wi && (u.Weapons[i].Mag > 0 || u.Weapons[i].Reserve > 0) && !(u.Shield && u.Weapons[i].St.Cat != "hg")) { alt = i; break; }
            if (alt >= 0) { u.Wi = alt; u.ReloadT = 0.6; u.ReloadKind = "switch"; }
            return;
        }
        int need = w.St.Mag - w.Mag;
        if (need <= 0) return;
        u.ReloadT = w.St.Cat == "sg" ? Math.Min(need, w.Reserve) * 0.45 + 0.3 : w.St.Reload * (w.Mag == 0 ? 1.15 : 1);
        u.ReloadKind = "reload";
        Snd("reload", u.X, u.Y);
        if (u.Team == Team.Op && (u.Target != null || R.Next() < 0.4)) Voice(u, "reload");
    }

    private static void FinishReload(Unit u)
    {
        var w = u.W;
        if (u.ReloadKind == "reload" && w != null)
        {
            int take = Math.Min(w.St.Mag - w.Mag, w.Reserve);
            w.Mag += take; w.Reserve -= take;
        }
        u.ReloadKind = null;
    }

    private void TraceShot(Unit sh, double ang, double dmg, double pen)
    {
        int f = sh.Floor;
        double dx = Math.Cos(ang), dy = Math.Sin(ang), sx = sh.X + dx * 8, sy = sh.Y + dy * 8, range = 45 * TS;
        double wallT = range; string kind = "none";
        for (double t = 0; t < range; t += 4)
        {
            double x = sx + dx * t, y = sy + dy * t;
            int tx = ToTile(x), ty = ToTile(y);
            byte tile = TileAt(f, tx, ty);
            if (tile == Tile.Wall || tile == Tile.High) { wallT = t; kind = tile == Tile.High ? "metal" : "wall"; break; }
            if (tile == Tile.Door && DoorAt(f, tx, ty).State == DoorState.Closed) { wallT = t; kind = "door"; break; }
            if (tile == Tile.Window) BreakWindow(f, tx, ty);
            if (tile == Tile.Furn && t > 26 && R.Next() < 0.035) { wallT = t; kind = "furn"; break; }
        }
        Unit best = null; double bestT = wallT;
        foreach (var u in Units)
        {
            if (u == sh || u.Floor != f || !u.Alive || u.Incap || u.Downed) continue;
            double ox = u.X - sx, oy = u.Y - sy, tp = ox * dx + oy * dy;
            if (tp < 0 || tp > bestT + 10) continue;
            double pd = Math.Abs(ox * dy - oy * dx);
            double r = (u.Stance == "crouch" || u.Kneel) ? 7 : 9;
            if (pd < r && tp < bestT) { best = u; bestT = tp; }
            else if (pd < 42 && u.Team != sh.Team) NearMiss(u, sh);
        }
        double ex = sx + dx * bestT, ey = sy + dy * bestT;
        AddFx(new FxEvent { Type = "tracer", X = sx, Y = sy, X1 = ex, Y1 = ey, F = f, Team = sh.Team });
        Events.Add(new SimEvent { T = T, Type = "shot", F = f, X0 = (int)sx, Y0 = (int)sy, X1 = (int)ex, Y1 = (int)ey, Team = sh.Team });
        if (best != null)
        {
            if (best.Shield && Math.Abs(MathUtil.AngDiff(best.Angle, ang + Math.PI)) < 0.95 && R.Next() < 0.9)
            {
                AddFx(new FxEvent { Type = "impact", X = ex, Y = ey, F = f, Kind = "metal" }); Snd("ricochet", ex, ey); best.Stress += 3;
                return;
            }
            ApplyDamage(best, dmg, pen, sh, ang);
        }
        else if (kind != "none")
        {
            AddFx(new FxEvent { Type = "impact", X = ex, Y = ey, F = f, Kind = kind, Ang = ang });
            if (R.Next() < 0.5) Snd("impact", ex, ey, flag: kind == "metal");
        }
    }

    private void NearMiss(Unit u, Unit sh)
    {
        if (u.Team == Team.Op)
        {
            if (u.Target == null) { u.AlertAng = Math.Atan2(sh.Y - u.Y, sh.X - u.X); u.AlertT = T; }
            u.Stress = Math.Min(100, u.Stress + 4 * u.Pers.Stress * (u.HasPerk("iron") ? 0.5 : 1));
        }
        if (u.Team == Team.Enemy)
        {
            u.Ai.Morale -= 1.5;
            if (u.Ai.State != "combat" && u.Ai.State != "surrender" && !u.Ai.Dummy) EnterCombat(u, sh, true);
        }
        if (u.Team == Team.Civ && u.Ai.State == "idle") CivPanic(u);
    }

    private void ApplyDamage(Unit u, double dmg, double pen, Unit sh, double ang)
    {
        if (u.Ai != null && u.Ai.Dummy)
        {
            u.Hp = 0; u.Alive = false; u.Dead = true;
            AddFx(new FxEvent { Type = "impact", X = u.X, Y = u.Y, F = u.Floor, Kind = "wood" });
            if (sh != null && sh.Team == Team.Op)
            {
                sh.Hits++; _hitsOp++;
                if (u.Team == Team.Civ) { _civKilledByOp++; AddLog($"{sh.Name} hit a no-shoot target!", "#d9534f"); } else sh.Kills++;
            }
            return;
        }
        double roll = R.Next();
        string loc = roll < 0.12 ? "head" : roll < 0.7 ? "torso" : "limb";
        bool blocked = false;
        if (loc == "torso" && u.Armor > 0)
        {
            double stop = MathUtil.Clamp(0.2 + (u.Armor - pen) / 70, 0.03, 0.95);
            if (R.Next() < stop) { dmg *= 0.12; blocked = true; } else dmg *= 1 - u.Armor / 300;
        }
        else if (loc == "head")
        {
            if (u.Helmet > 0 && R.Next() < MathUtil.Clamp(0.1 + (u.Helmet - pen) / 80, 0.02, 0.9)) { dmg *= 0.25; blocked = true; }
            else dmg *= 2.4;
        }
        else if (loc == "limb") dmg *= 0.55;
        if (u.Team == Team.Op) dmg *= Diff.DmgTaken;
        u.Hp -= dmg;
        if (sh != null && sh.Team == Team.Op && u.Team != Team.Op) { sh.Hits++; _hitsOp++; }
        AddFx(new FxEvent { Type = blocked ? "impact" : "blood", X = u.X, Y = u.Y, F = u.Floor, Ang = ang, Kind = blocked ? "metal" : "flesh" });
        if (blocked) Snd("ricochet", u.X, u.Y);
        else { Snd("flesh", u.X, u.Y); if (u.Hp > 0 && R.Next() < 0.6) Snd("grunt", u.X, u.Y, flag: u.Female); }
        if (u.Team == Team.Op)
        {
            if (sh != null && u.Target == null) { u.AlertAng = Math.Atan2(sh.Y - u.Y, sh.X - u.X); u.AlertT = T; }
            u.DamageTaken += dmg;
            u.Stress = Math.Min(100, u.Stress + 18 * u.Pers.Stress * (u.HasPerk("iron") ? 0.5 : 1));
            if (u.Hp <= 0) DownOp(u);
            else if (!blocked && R.Next() < 0.6) Voice(u, "hurt");
            return;
        }
        if (u.Team == Team.Enemy)
        {
            u.Ai.Morale -= blocked ? 6 : 20;
            if (u.Ai.State != "combat" && u.Ai.State != "surrender" && u.Hp > 0 && sh != null) EnterCombat(u, sh, true);
            if (u.Hp <= 0)
            {
                if (u.Ai.State == "surrender" && sh != null && sh.Team == Team.Op) { _roe++; AddLog($"ROE VIOLATION: {sh.Name} shot a surrendering suspect.", "#d9534f"); }
                if (u.Hp > -18 && loc != "head" && R.Next() < 0.55) { u.Incap = true; u.Alive = true; AddLog($"{u.Name} incapacitated.", "#e2a33a"); }
                else { u.Dead = true; u.Alive = false; }
                u.Target = null; u.Path = null;
                if (u.Armed) { u.Armed = false; DropWeapon(u.X + R.Range(-10, 10), u.Y + R.Range(-10, 10), u.Floor); }
                if (sh != null && sh.Team == Team.Op) { sh.Kills++; if (R.Next() < 0.7) Voice(sh, "down"); }
                Snd("fall", u.X, u.Y);
                AddFx(new FxEvent { Type = "pool", X = u.X, Y = u.Y, F = u.Floor });
                foreach (var o in Units)
                    if (o.Team == Team.Enemy && o.Alive && o != u && o.Floor == u.Floor && MathUtil.Dist(o.X, o.Y, u.X, u.Y) < 8 * TS) o.Ai.Morale -= 12;
                if (u.Hvt) AddLog($"HVT {u.Name} {(u.Dead ? "KILLED" : "down")}.", "#d9534f");
            }
            return;
        }
        if (u.Hp <= 0)
        {
            u.Dead = true; u.Alive = false;
            AddFx(new FxEvent { Type = "pool", X = u.X, Y = u.Y, F = u.Floor }); Snd("fall", u.X, u.Y);
            if (sh != null && sh.Team == Team.Op) { _civKilledByOp++; AddLog($"{u.Name} killed by friendly fire!", "#d9534f"); }
            else AddLog($"{u.Name} killed!", "#d9534f");
            if (u.Kind == "hostage") Say("Hostage down!");
        }
        else CivPanic(u);
    }

    private void DownOp(Unit u)
    {
        u.Downed = true; u.Hp = 0; u.Path = null; u.Orders.Clear(); u.Act = null; u.Target = null; u.Follow = null;
        u.Bleed = 60 * (u.HasPerk("tough") ? 2 : 1);
        AddLog($"{u.Name} is DOWN!", "#d9534f"); Voice(u, "mandown", true);
        AddFx(new FxEvent { Type = "pool", X = u.X, Y = u.Y, F = u.Floor }); Snd("fall", u.X, u.Y);
        foreach (var o in Ops) if (o != u && CanAct(o)) o.Stress = Math.Min(100, o.Stress + 22 * o.Pers.Stress);
    }

    // ================= Grenades =================
    private bool ThrowGrenade(Unit u, string g, double tx, double ty)
    {
        if (!u.Util.Remove(g)) return false;
        double maxR = (u.HasPerk("grenadier") ? 13 : 10) * TS;
        double dx = tx - u.X, dy = ty - u.Y, d = Math.Sqrt(dx * dx + dy * dy);
        if (d > maxR) { tx = u.X + dx / d * maxR; ty = u.Y + dy / d * maxR; d = maxR; }
        int n = (int)Math.Ceiling(d / 6); double lx = u.X, ly = u.Y;
        for (int k = 1; k <= n; k++)
        {
            double x = u.X + (tx - u.X) * k / n, y = u.Y + (ty - u.Y) * k / n;
            if (BlocksVision(u.Floor, ToTile(x), ToTile(y))) { tx = lx; ty = ly; break; }
            lx = x; ly = y;
        }
        Grenades.Add(new Grenade
        {
            Type = g, X = u.X, Y = u.Y, F = u.Floor, Sx = u.X, Sy = u.Y, Tx = tx, Ty = ty, Owner = u,
            Travel = Math.Max(0.25, MathUtil.Dist(u.X, u.Y, tx, ty) / 420), Fuse = g == "flash" ? 1.5 : g == "stinger" ? 1.4 : 1.2,
        });
        Snd("pin", u.X, u.Y);
        if (g == "flash") { Voice(u, "flash"); _flashes++; }
        if (g == "smoke") _smokes++;
        return true;
    }

    private void UpdateGrenades(double dt)
    {
        foreach (var gr in Grenades)
        {
            gr.T += dt;
            double k = Math.Min(1, gr.T / gr.Travel);
            gr.X = gr.Sx + (gr.Tx - gr.Sx) * k; gr.Y = gr.Sy + (gr.Ty - gr.Sy) * k;
            gr.Z = Math.Sin(k * Math.PI) * 18;
            if (k >= 1 && !gr.Bounced) { gr.Bounced = true; Snd("bounce", gr.X, gr.Y); }
            if (gr.T >= gr.Fuse) { Detonate(gr); gr.Done = true; }
        }
        Grenades.RemoveAll(g => g.Done);
        foreach (var s in Smokes) { s.T -= dt; s.R = s.T > 2 ? Math.Min(s.MaxR, s.R + dt * s.MaxR / 1.8) : s.MaxR * Math.Max(0, s.T / 2); }
        Smokes.RemoveAll(s => s.T <= 0);
        foreach (var g in Gases) g.T -= dt;
        Gases.RemoveAll(g => g.T <= 0);
    }

    private void Detonate(Grenade gr)
    {
        var owner = gr.Owner;
        Events.Add(new SimEvent { T = T, Type = "boom", G = gr.Type, X0 = (int)gr.X, Y0 = (int)gr.Y, F = gr.F });
        if (gr.Type is "flash" or "stinger")
        {
            Snd("flashbang", gr.X, gr.Y);
            AddFx(new FxEvent { Type = "flash", X = gr.X, Y = gr.Y, F = gr.F, Big = gr.Type == "flash" });
            MakeNoise(gr.X, gr.Y, gr.F, gr.Type == "flash" ? 30 : 34, gr.Type == "stinger" ? "diversion" : "explosion");
            double R0 = (gr.Type == "flash" ? (owner != null && owner.HasPerk("grenadier") ? 7.5 : 6) : 2.2) * TS;
            foreach (var u in Units)
            {
                if (!u.Alive || u.Dead || u.Floor != gr.F) continue;
                double d = MathUtil.Dist(u.X, u.Y, gr.X, gr.Y);
                if (d > R0 || !Los(gr.F, gr.X, gr.Y, u.X, u.Y, true)) continue;
                double facing = Math.Abs(MathUtil.AngDiff(u.Angle, Math.Atan2(gr.Y - u.Y, gr.X - u.X))) < 1.2 ? 1 : 0.55;
                double s = (1.5 + 5 * (1 - d / R0)) * facing;
                if (u.Team == Team.Op) s *= 0.35;
                u.Stun = Math.Max(u.Stun, s);
                if (u.Team == Team.Enemy) { u.Ai.Morale -= 18; if (u.Ai.State == "idle") u.Ai.State = "alert"; u.AimT = 0; }
                if (u.Team == Team.Civ && !u.HostageBound) { u.Ai.State = "cower"; u.Stance = "crouch"; u.Path = null; }
            }
        }
        else if (gr.Type == "smoke")
        {
            Snd("smoke", gr.X, gr.Y);
            Smokes.Add(new Cloud { X = gr.X, Y = gr.Y, F = gr.F, R = 4, MaxR = 3.3 * TS, T = 26 });
            Events.Add(new SimEvent { T = T, Type = "smoke", X0 = (int)gr.X, Y0 = (int)gr.Y, F = gr.F });
        }
        else if (gr.Type == "gas")
        {
            Snd("smoke", gr.X, gr.Y);
            Gases.Add(new Cloud { X = gr.X, Y = gr.Y, F = gr.F, R = 3.6 * TS, T = 22 });
        }
    }

    private void UpdateGas(double dt)
    {
        foreach (var u in Units)
        {
            if (!u.Alive || u.Dead) continue;
            bool inGas = Gases.Any(g => g.F == u.Floor && MathUtil.Dist(u.X, u.Y, g.X, g.Y) < g.R * Math.Min(1, (22 - g.T) / 2 + 0.3));
            if (inGas)
            {
                if (u.Gas <= 0 && u.Team != Team.Op) Snd("cough", u.X, u.Y);
                u.Gas = u.Team == Team.Op ? 0.5 : 4;
                if (u.Team == Team.Enemy) u.Ai.Morale -= 8 * dt;
            }
            else u.Gas = Math.Max(0, u.Gas - dt);
        }
    }
}
