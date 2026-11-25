using Breachpoint.Core.Data;
using Breachpoint.Core.Util;

namespace Breachpoint.Core.Sim;

public sealed partial class Mission
{
    // ================= Enemy AI =================
    private void EnterCombat(Unit e, Unit tgt, bool surprised)
    {
        var ai = e.Ai;
        if (ai.Dummy) return;
        if (!e.Armed)
        {
            if (ai.State != "flee" && ai.State != "surrender") { if (R.Next() < 0.6) Surrender(e); else StartFlee(e); }
            return;
        }
        bool wasCalm = ai.State is "idle" or "hide";
        ai.State = "combat"; ai.LostT = 0;
        if (tgt != null) ai.LastKnown = new Pos(tgt.X, tgt.Y, tgt.Floor);
        e.Target = null; e.AimT = 0;
        if (ai.Radio && !ai.Radioed) { ai.Radioed = true; ai.RadioT = 1.5; }
        if (e.Hvt && R.Next() < 0.75) StartFlee(e);
        if (ai.Guard != null && ai.ExecT < 0) ai.ExecT = Diff.ExecTime + R.Next() * 4;
        if (wasCalm && surprised) e.ReactBonus = 0.35;
    }

    private void AlertEnemy(Unit e, Pos pos, string via)
    {
        var ai = e.Ai;
        if (ai.Dummy || !e.Alive || e.Incap || e.Restrained || ai.State is "combat" or "surrender" or "flee") return;
        ai.Alerted = true;
        if (ai.Guard != null && ai.ExecT < 0 && via != "step") ai.ExecT = Diff.ExecTime + R.Next() * 4;
        if (e.Hvt) { if (via != "step") StartFlee(e); return; }
        if (!e.Armed) { if (R.Next() < 0.5) StartFlee(e); else { ai.State = "hide"; e.Stance = "crouch"; } return; }
        if (ai.Style == "rusher" || via == "diversion" || (via == "step" && R.Next() < 0.5)) { ai.State = "investigate"; ai.Investigate = pos; e.Path = null; ai.InvT = 0; ai.InvArrived = false; }
        else if (ai.Style == "hider") { ai.State = "hide"; e.Stance = "crouch"; ai.FaceTo = Math.Atan2(pos.Y - e.Y, pos.X - e.X); }
        else { ai.State = "alert"; ai.FaceTo = pos.F == e.Floor ? Math.Atan2(pos.Y - e.Y, pos.X - e.X) : e.Angle; }
    }

    private void Surrender(Unit e)
    {
        var ai = e.Ai;
        ai.State = "surrender"; e.Surrendered = true; e.Target = null; e.Path = null; e.Stance = "stand"; ai.SurrT = 0;
        if (e.Armed) { e.Armed = false; DropWeapon(e.X + R.Range(-12, 12), e.Y + R.Range(-12, 12), e.Floor); }
        AddLog($"{e.Name} is surrendering.", "#5cb85c");
    }

    private void StartFlee(Unit e)
    {
        var ai = e.Ai; ai.State = "flee"; e.Path = null; e.Stance = "stand";
        (int, int)? best = null; double bs = -1;
        var ops = Ops.ToList();
        for (int i = 0; i < 24 && _exits.Count > 0; i++)
        {
            var ex = _exits[(int)Math.Floor(R.Next() * _exits.Count)];
            double md = 1e9;
            foreach (var o in ops) md = Math.Min(md, MathUtil.Dist(Ctr(ex.x), Ctr(ex.y), o.X, o.Y));
            if (md > bs) { bs = md; best = ex; }
        }
        ai.Exit = best;
    }

    private double SurrenderChance(Unit e)
    {
        var E = CampaignData.Enemies.GetValueOrDefault(e.Kind, CampaignData.Enemies["guard"]);
        double p = E.Surrender;
        if (e.Stun > 0) p += 0.45;
        if (e.Gas > 0) p += 0.3;
        if (e.Hp < 60) p += 0.2;
        int seeing = Ops.Count(o => CanAct(o) && o.Floor == e.Floor && MathUtil.Dist(o.X, o.Y, e.X, e.Y) < 10 * TS && Los(e.Floor, e.X, e.Y, o.X, o.Y));
        p += 0.08 * Math.Max(0, seeing - 1);
        var sh = e.Ai.Shouter;
        if (sh != null && sh.HasPerk("negotiator")) p += 0.25;
        if (sh != null && sh.Op != null) p += (sh.Stat("lea") - 50) / 400.0;
        p *= 1.3 - MathUtil.Clamp(e.Ai.Morale, 0, 100) / 100;
        if (!e.Armed) p += 0.35;
        if (DifficultyId == "veteran") p -= 0.05;
        if (DifficultyId == "milsim") p -= 0.1;
        return MathUtil.Clamp(p, 0.02, 0.95);
    }

    private void Decide(Unit e)
    {
        if (e.Ai.State == "surrender" || !e.Alive) return;
        if (R.Next() < SurrenderChance(e)) Surrender(e);
        else if (e.Armed) { EnterCombat(e, e.Ai.Shouter, true); e.ReactBonus = 0.2; }
        else StartFlee(e);
    }

    private void RadioAlert(Unit src)
    {
        if (!src.Alive || src.Dead || src.Restrained || src.Stun > 0) return;
        int n = 0;
        foreach (var e in Units)
        {
            if (e.Team != Team.Enemy || e == src || !e.Ai.Radio) continue;
            if (e.Ai.State is "combat" or "surrender" or "flee" or "restrained") continue;
            AlertEnemy(e, src.Ai.LastKnown ?? new Pos(src.X, src.Y, src.Floor), "radio"); n++;
        }
        if (n > 0) AddLog("Intercepted suspect radio traffic — they know you're here.", "#d9534f");
    }

    private void EnemyThink(Unit e, double dt)
    {
        var ai = e.Ai;
        if (!e.Alive || e.Dead || e.Restrained || e.Incap || ai.Dummy) return;
        e.FireCd -= dt; e.RecoilHeat = Math.Max(0, e.RecoilHeat - dt * 3);
        if (e.ReloadT > 0) { e.ReloadT -= dt; if (e.ReloadT <= 0) FinishReload(e); }
        if (e.JamT > 0) e.JamT -= dt;
        if (e.Stun > 0)
        {
            e.Stun -= dt; e.Status = "stunned";
            if (ai.DecideT > 0) { ai.DecideT -= dt; if (ai.DecideT <= 0) Decide(e); }
            return;
        }
        if (ai.RadioT > 0) { ai.RadioT -= dt; if (ai.RadioT <= 0) RadioAlert(e); }
        if (ai.State == "surrender")
        {
            ai.SurrT += dt;
            bool watched = Ops.Any(o => CanAct(o) && o.Floor == e.Floor && MathUtil.Dist(o.X, o.Y, e.X, e.Y) < 12 * TS && Los(e.Floor, o.X, o.Y, e.X, e.Y, true));
            ai.Unwatched = watched ? 0 : ai.Unwatched + dt;
            if (ai.Unwatched > 14 && R.Next() < dt * 0.15) { e.Surrendered = false; StartFlee(e); AddLog($"{e.Name} is fleeing!", "#e2a33a"); }
            return;
        }
        if (ai.DecideT > 0) { ai.DecideT -= dt; if (ai.DecideT <= 0) { Decide(e); if (ai.State == "surrender") return; } }
        if (ai.Guard != null && ai.ExecT >= 0)
        {
            ai.ExecT -= dt;
            if (ai.ExecT <= 0)
            {
                var h = (ai.GuardList ?? new List<Unit> { ai.Guard }).FirstOrDefault(x => x.Alive && !x.Dead && x.Ai.State != "secured" && x.Floor == e.Floor && Los(e.Floor, e.X, e.Y, x.X, x.Y, true));
                if (h != null && e.Armed)
                {
                    double rem = TurnTo(e, Math.Atan2(h.Y - e.Y, h.X - e.X), dt, 8);
                    if (rem < 0.1 && e.FireCd <= 0) { Fire(e, h.X, h.Y); if (!ai.ExecLogged) { ai.ExecLogged = true; AddLog($"{e.Name} is executing a hostage!", "#d9534f"); } }
                    return;
                }
                ai.ExecT = 4;
            }
        }
        Unit tgt = null; double td = 1e18;
        foreach (var o in Units)
        {
            if (o.Team != Team.Op || !CanAct(o) || o.Floor != e.Floor || !CanSee(e, o)) continue;
            double d = MathUtil.Dist(o.X, o.Y, e.X, e.Y);
            if (d < td) { td = d; tgt = o; }
        }
        if (ai.State == "flee")
        {
            if (tgt != null && e.Armed && td < 6 * TS && ai.Style != "hider" && R.Next() < 0.4 * dt) EnterCombat(e, tgt, false);
            else { FleeTick(e, dt); return; }
        }
        if (tgt != null)
        {
            if (ai.State != "combat")
            {
                bool calm = ai.State == "idle";
                double thr = (ai.State == "hide" ? 0.12 : calm ? 0.35 : 0.15) + (td / TS) * (calm ? 0.06 : 0.03) * (Night ? 1.4 : 1) * (tgt.Stance == "crouch" ? 1.3 : 1);
                ai.NoticeT += dt;
                TurnTo(e, Math.Atan2(tgt.Y - e.Y, tgt.X - e.X), dt, calm ? 2.5 : 5);
                if (ai.NoticeT < thr) { e.Status = "noticing"; return; }
                EnterCombat(e, tgt, calm || ai.State == "hide");
            }
            if (ai.State != "combat") return;
            ai.LastKnown = new Pos(tgt.X, tgt.Y, tgt.Floor); ai.LostT = 0;
            if (e.Target != tgt) { e.Target = tgt; e.AimT = 0; e.ReactT = (e.React * Diff.EnemyReact + e.ReactBonus) * (0.8 + R.Next() * 0.5); e.ReactBonus = 0; }
            ai.AimingAt = tgt;
            double rem = TurnTo(e, Math.Atan2(tgt.Y - e.Y, tgt.X - e.X), dt, 6);
            e.AimT += dt;
            if (ai.Style == "rusher" && td > 4 * TS && e.Path == null && R.Next() < dt * 0.6) SetPath(e, tgt.Floor, ToTile(tgt.X), ToTile(tgt.Y));
            if (e.Path != null && td < 4 * TS) e.Path = null;
            if (e.Path != null) FollowPath(e, dt);
            if (e.AimT >= e.ReactT && rem < 0.15 && e.Armed)
            {
                var w = e.W;
                if (w.Mag <= 0) { if (w.Reserve > 0) StartReload(e); else { e.Armed = false; ai.State = "alert"; } }
                else if (e.BurstPause > 0) e.BurstPause -= dt;
                else if (Fire(e, tgt.X, tgt.Y))
                {
                    e.Burst++;
                    int mx = w.St.Mode == "auto" ? 2 + (int)Math.Floor(R.Next() * 3) : 1;
                    if (e.Burst >= mx) { e.Burst = 0; e.BurstPause = w.St.Mode == "auto" ? 0.35 + R.Next() * 0.4 : 0.25 + R.Next() * 0.45; }
                }
            }
            ai.CheckT -= dt;
            if (ai.CheckT <= 0)
            {
                ai.CheckT = 1.6;
                if ((e.Hp < 70 || ai.Morale < 30 || e.Gas > 0) && R.Next() < SurrenderChance(e) * 0.35) { Surrender(e); return; }
                if (ai.Morale < 15 && R.Next() < 0.3) { StartFlee(e); return; }
            }
            e.Status = "combat";
            return;
        }
        ai.AimingAt = null; e.Target = null; ai.NoticeT = Math.Max(0, ai.NoticeT - dt * 0.5);
        if (ai.State == "combat")
        {
            ai.LostT += dt;
            var lk = ai.LastKnown;
            if (ai.Style == "rusher" && lk.HasValue && ai.LostT > 0.8)
            {
                if (e.Path == null) SetPath(e, lk.Value.F, ToTile(lk.Value.X), ToTile(lk.Value.Y));
                if (e.Path != null && FollowPath(e, dt) != "moving") e.Path = null;
            }
            else if (lk.HasValue && lk.Value.F == e.Floor) TurnTo(e, Math.Atan2(lk.Value.Y - e.Y, lk.Value.X - e.X), dt, 4);
            if (ai.LostT > 10) { ai.State = "alert"; e.Path = null; }
            return;
        }
        if (ai.State == "investigate")
        {
            var p = ai.Investigate.Value;
            if (e.Path == null && !ai.InvArrived && !SetPath(e, p.F, ToTile(p.X), ToTile(p.Y))) { ai.State = "alert"; return; }
            if (e.Path != null)
            {
                if (FollowPath(e, dt) != "moving") { e.Path = null; ai.InvArrived = true; }
                if (e.Path != null && e.MoveAng.HasValue && e.Act == null) TurnTo(e, e.MoveAng.Value, dt, 6);
            }
            else
            {
                ai.InvT += dt; e.Angle += dt * 1.2 * Math.Sin(ai.InvT * 1.3);
                if (ai.InvT > 5) { ai.State = "alert"; ai.InvArrived = false; }
            }
            return;
        }
        if (ai.State is "alert" or "hide")
        {
            if (ai.FaceTo.HasValue) TurnTo(e, ai.FaceTo.Value, dt, 4);
            return;
        }
        if (ai.Patrol != null && ai.Patrol.Count > 1)
        {
            if (e.Path == null)
            {
                ai.WaitT -= dt;
                if (ai.WaitT <= 0) { ai.Pi = (ai.Pi + 1) % ai.Patrol.Count; var pt = ai.Patrol[ai.Pi]; SetPath(e, e.Floor, pt.x, pt.y); ai.WaitT = 2 + R.Next() * 4; }
                else e.Angle += dt * 0.3 * Math.Sin(T + e.X);
            }
            else
            {
                if (FollowPath(e, dt) != "moving") e.Path = null;
                if (e.MoveAng.HasValue) TurnTo(e, e.MoveAng.Value, dt, 5);
            }
        }
        else
        {
            ai.WaitT -= dt;
            if (ai.WaitT <= 0) { ai.WaitT = 3 + R.Next() * 5; ai.FaceTo = e.Angle + R.Range(-1.5, 1.5); }
            if (ai.FaceTo.HasValue) TurnTo(e, ai.FaceTo.Value, dt, 2);
        }
    }

    private bool AtMapEdge(Unit u) => u.Floor == 0 && (ToTile(u.X) <= 1 || ToTile(u.Y) <= 1 || ToTile(u.X) >= Map.W - 2 || ToTile(u.Y) >= Map.H - 2);

    private void FleeTick(Unit e, double dt)
    {
        var ai = e.Ai;
        if (!ai.Exit.HasValue) StartFlee(e);
        if (!ai.Exit.HasValue) return;
        if (e.Path == null && !SetPath(e, 0, ai.Exit.Value.x, ai.Exit.Value.y)) { ai.State = "hide"; e.Stance = "crouch"; return; }
        string r = FollowPath(e, dt);
        if (e.MoveAng.HasValue) TurnTo(e, e.MoveAng.Value, dt, 8);
        if (r != "moving") e.Path = null;
        if (AtMapEdge(e)) { e.Escaped = true; e.Alive = false; AddLog($"{e.Name} ESCAPED the perimeter.", "#d9534f"); }
        e.Status = "fleeing";
    }

    // ================= Civilians =================
    private void CivPanic(Unit c)
    {
        if (c.Ai.Dummy || c.HostageBound || c.Ai.State is "secured" or "panic" or "flee" or "cower" || !c.Alive) return;
        if (R.Next() < 0.5) Snd("scream", c.X, c.Y);
        if (R.Next() < 0.55) { c.Ai.State = "cower"; c.Stance = "crouch"; c.Path = null; }
        else
        {
            c.Ai.State = "flee";
            (int, int)? best = null; double bd = 1e18;
            for (int i = 0; i < 20 && _exits.Count > 0; i++)
            {
                var ex = _exits[(int)Math.Floor(R.Next() * _exits.Count)];
                double d = MathUtil.Dist(Ctr(ex.x), Ctr(ex.y), c.X, c.Y);
                if (d < bd) { bd = d; best = ex; }
            }
            c.Ai.Exit = best; c.Path = null;
        }
    }

    private void CivThink(Unit c, double dt)
    {
        if (!c.Alive || c.Dead || c.Ai.Dummy) return;
        if (c.Stun > 0) { c.Stun -= dt; return; }
        var ai = c.Ai;
        if (ai.State == "flee")
        {
            if (!ai.Exit.HasValue) { ai.State = "cower"; return; }
            if (c.Path == null && !SetPath(c, 0, ai.Exit.Value.x, ai.Exit.Value.y)) { ai.State = "cower"; c.Stance = "crouch"; return; }
            string r = FollowPath(c, dt);
            if (c.MoveAng.HasValue) TurnTo(c, c.MoveAng.Value, dt, 8);
            if (r != "moving") c.Path = null;
            if (AtMapEdge(c)) { c.Evacuated = true; c.Alive = false; AddLog("A civilian fled the area.", "#9fb0bf"); }
            return;
        }
        if (ai.State == "idle" && !c.HostageBound)
        {
            ai.WaitT -= dt;
            if (ai.WaitT <= 0) { ai.WaitT = 3 + R.Next() * 5; c.Angle += R.Range(-1.5, 1.5); }
        }
    }

    private void ProcessNoises()
    {
        foreach (var n in Noises)
            foreach (var u in Units)
            {
                if (u.Team == Team.Op || !u.Alive || u.Dead || u == n.Src) continue;
                if ((n.Kind is "step" or "door") && (n.Src == null || n.Src.Team != Team.Op)) continue;
                bool sameF = u.Floor == n.F;
                double d = MathUtil.Dist(u.X, u.Y, n.X, n.Y);
                double r = n.R * (sameF ? 1 : 0.35);
                if (d > r) continue;
                if (sameF && !Los(u.Floor, u.X, u.Y, n.X, n.Y, true)) r *= 0.6;
                if (d > r) continue;
                if (u.Team == Team.Enemy)
                {
                    if (n.Kind == "step" && u.Ai.State != "idle") continue;
                    AlertEnemy(u, new Pos(n.X, n.Y, n.F), n.Kind);
                }
                else if (n.Kind is "gunshot" or "explosion" or "breach" && u.Ai.State == "idle") CivPanic(u);
            }
        Noises.Clear();
    }

    // ================= Objectives =================
    public List<Objective> ComputeObjectives()
    {
        var o = new List<Objective>();
        var enemies = Units.Where(u => u.Team == Team.Enemy).ToList();
        var hostages = Units.Where(u => u.Kind == "hostage").ToList();
        var civs = Units.Where(u => u.Kind == "civilian").ToList();
        var ev = Items.Where(i => i.Kind == "evidence").ToList();
        var con = Items.Where(i => i.Kind == "contraband").ToList();
        if (Cfg.Training)
        {
            int hit = enemies.Count(e => e.Dead);
            o.Add(new Objective { Id = "neutralize", Text = $"Hit all targets ({hit}/{enemies.Count})", Done = hit == enemies.Count, Req = true });
            o.Add(new Objective { Id = "noshoot", Text = $"Avoid no-shoot targets ({_civKilledByOp} hit)", Done = _civKilledByOp == 0, Fail = _civKilledByOp > 0 });
            return o;
        }
        int active = enemies.Count(e => e.Alive && !e.Restrained && !e.Escaped && !e.Dead && !e.Incap);
        o.Add(new Objective { Id = "neutralize", Text = $"Neutralize suspects ({enemies.Count - active}/{enemies.Count})", Done = active == 0, Req = true });
        if (hostages.Count > 0)
        {
            int saved = hostages.Count(h => h.Alive && h.Ai.State == "secured"), dead = hostages.Count(h => h.Dead);
            o.Add(new Objective { Id = "rescue", Text = $"Rescue hostages ({saved}/{hostages.Count}{(dead > 0 ? $", {dead} KIA" : "")})", Done = saved + dead == hostages.Count && saved > 0, Req = true, Fail = dead > hostages.Count / 2.0 });
        }
        if (HvtUnit != null) o.Add(new Objective { Id = "capture", Text = $"Capture {HvtUnit.Name} alive", Done = HvtUnit.Restrained && !HvtUnit.Dead, Fail = HvtUnit.Dead || HvtUnit.Escaped });
        if (ev.Count > 0) o.Add(new Objective { Id = "evidence", Text = $"Recover evidence ({ev.Count(i => i.Collected)}/{ev.Count})", Done = ev.All(i => i.Collected) });
        if (con.Count > 0) o.Add(new Objective { Id = "contraband", Text = $"Seize contraband ({con.Count(i => i.Collected)}/{con.Count})", Done = con.All(i => i.Collected) });
        if (civs.Count > 0)
        {
            int s = civs.Count(c => (c.Alive && c.Ai.State == "secured") || c.Evacuated);
            o.Add(new Objective { Id = "civs", Text = $"Secure civilians ({s}/{civs.Count})", Done = s == civs.Count(c => !c.Dead), Fail = civs.Any(c => c.Dead) });
        }
        int arrestable = enemies.Count(e => e.Alive && (e.Ai.State == "surrender" || e.Incap) && !e.Restrained);
        if (arrestable > 0) o.Add(new Objective { Id = "restrain", Text = $"Restrain surrendered/incapacitated suspects ({arrestable})" });
        return o;
    }

    private void CheckEnd(double dt)
    {
        _objT -= dt;
        if (_objT > 0) return;
        _objT = 0.5;
        if (!Ops.Any(CanAct)) { Finish(false, "All operators down."); return; }
        Objectives = ComputeObjectives();
        bool reqDone = Objectives.Where(o => o.Req).All(o => o.Done);
        if (Objectives.Any(o => o.Req && o.Fail) && !_failNotified) { _failNotified = true; AddLog("MISSION FAILURE CONDITION: too many hostages lost.", "#d9534f"); }
        if (reqDone && !_allClearAnnounced)
        {
            _allClearAnnounced = true; CanComplete = true;
            AddLog("Area secure. Complete remaining objectives or end the operation.", "#5cb85c");
            Say(CampaignData.Voice["allclear"][0]);
        }
    }

    // ================= Main update =================
    public void Update(double dt)
    {
        if (Ended || Paused) return;
        dt = Math.Min(dt, 0.05) * Speed;
        T += dt;
        foreach (var u in Units) u.SeenNow = false;
        for (int i = 0; i < Units.Count; i++)
        {
            var u = Units[i];
            if (u.Team == Team.Op) OpThink(u, dt);
            else if (u.Team == Team.Enemy) EnemyThink(u, dt);
            else CivThink(u, dt);
        }
        foreach (var u in Units)
        {
            if (u.Team == Team.Op) { u.Visible = true; continue; }
            bool vis = u.SeenNow;
            if (!vis) foreach (var c in CamSources) if (c.F == u.Floor && MathUtil.Dist(u.X, u.Y, c.X, c.Y) < 8 * TS && Los(c.F, c.X, c.Y, u.X, u.Y)) vis = true;
            u.Visible = vis || (u.Dead && u.WasSeen) || u.Restrained;
            if (vis) { u.WasSeen = true; u.LastSeen = new Pos(u.X, u.Y, u.Floor, T); }
        }
        foreach (var c in CamSources) c.T -= dt;
        CamSources.RemoveAll(c => c.T <= 0);
        ProcessNoises();
        UpdateGrenades(dt);
        UpdateGas(dt);
        Separate();
        Heard.RemoveAll(h => T - h.T > 2.5);
        _recT -= dt;
        if (_recT <= 0) { _recT = 0.15; Record(); }
        CheckEnd(dt);
    }

    private void Record()
    {
        var arr = new int[Units.Count * 6];
        for (int i = 0; i < Units.Count; i++)
        {
            var u = Units[i];
            arr[i * 6] = (int)u.X; arr[i * 6 + 1] = (int)u.Y; arr[i * 6 + 2] = u.Floor; arr[i * 6 + 3] = (int)Math.Round(u.Angle * 100);
            arr[i * 6 + 4] = StateBits(u); arr[i * 6 + 5] = Math.Max(0, (int)u.Hp);
        }
        Frames.Add(new Frame { T = T, U = arr });
    }

    public static int StateBits(Unit u) =>
        (u.Dead ? 1 : 0) | (u.Downed ? 2 : 0) | (u.Incap ? 4 : 0) | (u.Restrained ? 8 : 0) | (u.Surrendered || (u.Ai != null && u.Ai.State == "surrender") ? 16 : 0)
        | (u.Stun > 0 ? 32 : 0) | (u.Escaped || u.Evacuated ? 64 : 0) | (u.Stance == "crouch" ? 128 : 0) | (!u.Armed ? 256 : 0) | (u.HostageBound ? 512 : 0) | (u.Kneel ? 1024 : 0);

    // ================= Evaluation =================
    public MissionResult Finish(bool success, string reason = null)
    {
        if (Ended) return Result;
        Ended = true; Paused = true; Record();
        var enemies = Units.Where(u => u.Team == Team.Enemy).ToList();
        var hostages = Units.Where(u => u.Kind == "hostage").ToList();
        var civs = Units.Where(u => u.Kind == "civilian").ToList();
        var ops = Ops.ToList();
        var ev = Items.Where(i => i.Kind == "evidence").ToList();
        var con = Items.Where(i => i.Kind == "contraband").ToList();
        int arrested = enemies.Count(e => e.Restrained && !e.Dead), killed = enemies.Count(e => e.Dead), escaped = enemies.Count(e => e.Escaped);
        int active = enemies.Count(e => e.Alive && !e.Restrained && !e.Escaped && !e.Dead && !e.Incap);
        int hostSaved = hostages.Count(h => h.Alive && h.Ai.State == "secured"), hostDead = hostages.Count(h => h.Dead), civDead = civs.Count(c => c.Dead);
        int opsDown = ops.Count(o => o.Downed || o.Dead), opsDead = ops.Count(o => o.Dead), opsInjured = ops.Count(o => !o.Dead && o.Hp < o.MaxHp);
        int evFound = ev.Count(i => i.Collected), conFound = con.Count(i => i.Collected);
        bool allDown = ops.All(o => !CanAct(o));
        bool reqOk = active == 0 && (hostages.Count == 0 || hostSaved > 0);
        bool failed = allDown || !reqOk || hostDead > hostages.Count / 2.0;
        if (Cfg.Training) failed = allDown;
        var S = new List<ScoreLine>();
        void Add(string n, double pts, int max, string detail) => S.Add(new ScoreLine { Name = n, Pts = (int)Math.Round(pts), Max = max, Detail = detail });
        Add("Mission completion", failed ? 0 : 25, 25, failed ? reason ?? "Objectives incomplete" : "Primary objectives complete");
        Add("Hostages rescued", hostages.Count > 0 ? 15.0 * hostSaved / hostages.Count : 15, 15, hostages.Count > 0 ? $"{hostSaved}/{hostages.Count} rescued" : "No hostages");
        Add("Suspects apprehended", enemies.Count > 0 ? 15.0 * (arrested + killed * 0.45) / enemies.Count - escaped * 3 : 15, 15, $"{arrested} arrested, {killed} killed, {escaped} escaped");
        Add("Evidence recovered", ev.Count + con.Count > 0 ? 15.0 * (evFound + conFound) / (ev.Count + con.Count) : 15, 15, $"{evFound}/{ev.Count} evidence, {conFound}/{con.Count} contraband");
        Add("Civilian safety", Math.Max(-20, 10 - civDead * 5 - hostDead * 4 - _civKilledByOp * 10 - _roe * 6), 10, $"{civDead + hostDead} civilian casualties{(_civKilledByOp > 0 ? $", {_civKilledByOp} by friendly fire" : "")}{(_roe > 0 ? $", {_roe} ROE violations" : "")}");
        Add("Operator injuries", Math.Max(-15, 10 - opsInjured * 2 - opsDown * 4 - opsDead * 8), 10, $"{opsInjured} injured, {opsDown} downed, {opsDead} KIA");
        double acc = _shotsOp > 0 ? (double)_hitsOp / _shotsOp : 1;
        Add("Ammunition expenditure", MathUtil.Clamp(5 * Math.Min(1, acc * 2.2), 0, 5), 5, $"{_shotsOp} rounds, {acc * 100:0}% hits");
        Add("Equipment losses", opsDead > 0 ? 0 : 2, 2, opsDead > 0 ? "Operator equipment lost" : "No losses");
        Add("Time taken", MathUtil.Clamp(3 * (1.6 - T / Cfg.ParTime), 0, 3), 3, $"{MathUtil.Time(T)} (par {MathUtil.Time(Cfg.ParTime)})");
        int total = S.Sum(s => s.Pts);
        string rank = failed ? "F" : total >= 90 ? "S" : total >= 76 ? "A" : total >= 60 ? "B" : "C";
        if (!failed && HvtUnit != null && (HvtUnit.Dead || HvtUnit.Escaped) && rank is "S" or "A") rank = "B";
        Result = new MissionResult
        {
            Failed = failed, Rank = rank, Total = total, Score = S, Time = T, Reason = reason, Arrested = arrested, Killed = killed, Escaped = escaped,
            HostSaved = hostSaved, HostDead = hostDead, CivDead = civDead, EvFound = evFound, ConFound = conFound,
            Evidence = ev.Where(i => i.Collected).Select(i => (i.Name, i.Intel)).ToList(),
            Contraband = con.Where(i => i.Collected).Select(i => (i.Name, i.Value)).ToList(),
            HvtCaptured = HvtUnit != null ? HvtUnit.Restrained && !HvtUnit.Dead : null, HvtName = HvtUnit?.Name,
            LieutenantCaptured = enemies.Any(e => e.Lieutenant && e.Restrained && !e.Dead),
            Roe = _roe, CivKilledByOp = _civKilledByOp,
            UtilUsed = new() { ["flash"] = _flashes, ["smoke"] = _smokes, ["charge"] = _charges },
            Ops = ops.Select(o => new OpResult
            {
                Id = o.Op.Id, Name = o.Name, Hp = o.Hp, MaxHp = o.MaxHp, Downed = o.Downed, Dead = o.Dead, Stable = o.Stable, Kills = o.Kills, Arrests = o.Arrests,
                Rescues = o.Rescues, Shots = o.Shots, Hits = o.Hits, Evidence = o.EvidenceFound, Util = o.Util.ToList(), ToolUses = o.ToolUses,
                Fired = o.Weapons.Select(w => (w.Inst.Uid, w.Fired, w.St.Cal)).ToList(),
            }).ToList(),
        };
        return Result;
    }
}
