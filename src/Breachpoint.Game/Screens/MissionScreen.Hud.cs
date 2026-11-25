using Breachpoint.Core.Data;
using Breachpoint.Core.Map;
using Breachpoint.Core.Sim;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using Microsoft.Xna.Framework;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

public sealed partial class MissionScreen
{
    public override void Draw()
    {
        var view = new View
        {
            Map = M.Map, Floor = _floor, Cam = _cam, Units = M.Units, Items = M.Items, Grenades = M.Grenades, Smokes = M.Smokes, Gases = M.Gases, Heard = M.Heard,
            CamVision = M.CamSources.Where(c => c.F == _floor).ToList(), Night = M.Night, Rain = M.Rain, ShowHints = M.Diff.ShowEnemies, ShowSpawns = M.Phase == "plan",
            GameTime = M.T, RealTime = _real, VehicleTex = _vehicle?.Tex, VehiclePos = _vehicle?.Pos ?? Vector2.Zero, VehicleAng = _vehicle?.Ang ?? 0, Overlay = Overlay,
        };
        foreach (var u in Ops.Where(u => u.Floor == _floor && Mission.CanAct(u))) view.Vision.Add((new Vector2((float)u.X, (float)u.Y), M.VisionPoly(u)));
        _r.Shake = Math.Max(_r.Shake, 0);
        _r.Draw(P, view);
        Hud();
    }

    // ================= world overlay =================
    private void Overlay(Painter p, Matrix m)
    {
        float t = (float)_real;
        foreach (var u in Ops)
        {
            if (u.Floor != _floor && u.Orders.Count == 0) continue;
            bool sel = _sel.Contains(u);
            float alpha = sel ? 0.95f : 0.4f, lw = sel ? 2 : 1.3f;
            var col = C.Hex(u.Color) * alpha;
            float px = (float)u.X, py = (float)u.Y; int pf = u.Floor;
            if (u.Path != null && u.Floor == _floor)
            {
                float lx = px, ly = py;
                for (int i = u.Pi; i < u.Path.Count; i++)
                {
                    var n = u.Path[i]; if (n.F != _floor) break;
                    float nx = (n.X + 0.5f) * TS, ny = (n.Y + 0.5f) * TS;
                    p.DashedLine(lx, ly, nx, ny, lw, col, 2, 4); lx = nx; ly = ny;
                }
            }
            int idx = 0;
            foreach (var o in u.Orders)
            {
                idx++;
                switch (o.Kind)
                {
                    case "move":
                    {
                        float x = (o.X + 0.5f) * TS, y = (o.Y + 0.5f) * TS;
                        if (o.F == _floor)
                        {
                            if (pf == _floor && !(idx == 1 && u.Path != null)) p.Line(px, py, x, y, lw, col);
                            p.Circle(x, y, 5, col);
                            if (o.Pace == "run") p.Circle(x + 7, y - 5, 2, col);
                        }
                        px = x; py = y; pf = o.F;
                        break;
                    }
                    case "door":
                    {
                        var d = M.Map.Doors[o.DoorId]; if (d.F != _floor) break;
                        float x = (d.X + 0.5f) * TS, y = (d.Y + 0.5f) * TS;
                        p.Circle(x, y, 10, Color.Black * alpha); p.Ring(x, y, 10, 2, col);
                        _overlayLabels.Add((o.Action switch { "entry" or "open" => "O", "kick" => "K", "ram" => "R", "shotgun" => "S", "charge" => "C4", "pick" => "L", "cam" => "◉", "close" => "X", _ => "D" }, new Vector2(x, y), col, 10));
                        break;
                    }
                    case "throw":
                    {
                        if (o.F != _floor) break;
                        float tx = (float)o.WX, ty = (float)o.WY;
                        Vector2 prev = new(px, py);
                        for (int i = 1; i <= 12; i++)
                        {
                            float k = i / 12f;
                            var q = new Vector2(MathHelper.Lerp(px, tx, k), MathHelper.Lerp(py, ty, k) - MathF.Sin(k * MathF.PI) * 30);
                            if (i % 2 == 1) p.Line(prev.X, prev.Y, q.X, q.Y, lw, col);
                            prev = q;
                        }
                        p.Ring(tx, ty, 6, lw, col);
                        _overlayLabels.Add((o.Grenade switch { "flash" => "FLASH", "smoke" => "SMOKE", "gas" => "GAS", _ => "DIV" }, new Vector2(tx, ty - 12), col, 9));
                        break;
                    }
                    case "go":
                    case "waitdoor":
                        if (pf == _floor) _overlayLabels.Add((o.Kind == "go" ? "GO?" : "STACK", new Vector2(px, py - 12), o.Kind == "go" ? C.Accent : C.Blue, 10));
                        break;
                    case "interact":
                    {
                        Vector2? tp = null; int tf = -1;
                        if (o.InteractKind == "collect") { var it = M.Items.FirstOrDefault(i => i.Id == o.TargetId); if (it != null) { tp = new Vector2((float)it.X, (float)it.Y); tf = it.F; } }
                        else { var tu = M.Units.FirstOrDefault(x => x.Id == o.TargetId); if (tu != null) { tp = new Vector2((float)tu.X, (float)tu.Y); tf = tu.Floor; } }
                        if (tp.HasValue && tf == _floor) { p.DashedLine(px, py, tp.Value.X, tp.Value.Y, lw, col, 1, 3); _overlayLabels.Add((o.InteractKind.ToUpperInvariant(), tp.Value + new Vector2(0, -16), col, 9)); }
                        break;
                    }
                }
            }
            if (u.Floor != _floor) continue;
            float ux = (float)u.X, uy = (float)u.Y;
            if (sel) p.Ring(ux, uy, 15, 1.5f, Color.White * (0.6f + 0.3f * MathF.Sin(t * 5)));
            if (sel && u.WatchAngle.HasValue) p.Line(ux, uy, ux + MathF.Cos((float)u.WatchAngle) * 40, uy + MathF.Sin((float)u.WatchAngle) * 40, 1.5f, C.Hex(u.Color));
            if (u.Act != null && u.Act.Dur > 0) p.Ring(ux, uy, 19, 3, C.Accent, -MathF.PI / 2, -MathF.PI / 2 + Math.Min(1, (float)(u.Act.T / u.Act.Dur)) * MathF.PI * 2);
            var io = u.Orders.FirstOrDefault();
            if (io != null && io.Kind == "interact" && io.El > 0) p.Ring(ux, uy, 19, 3, C.Green, -MathF.PI / 2, -MathF.PI / 2 + Math.Min(1, (float)(io.El / io.Dur)) * MathF.PI * 2);
            if (u.ReloadT > 0) _overlayLabels.Add(("RELOAD", new Vector2(ux, uy + 26), C.Accent, 9));
        }
        int tx0 = Mission.ToTile(_mouseW.X), ty0 = Mission.ToTile(_mouseW.Y);
        if (!G.Input.TouchActive && M.TileAt(_floor, tx0, ty0) == Tile.Door) p.Rect(tx0 * TS, ty0 * TS, TS, TS, C.Accent, 2);
        if (_mode != null)
        {
            var u = Sel.Where(o => o.Util.Contains(_mode) && o.Floor == _floor).OrderBy(o => Vector2.Distance(new Vector2((float)o.X, (float)o.Y), _mouseW)).FirstOrDefault();
            if (u != null)
            {
                p.DashedRing((float)u.X, (float)u.Y, (u.HasPerk("grenadier") ? 13 : 10) * TS, 1.5f, C.Accent, 40);
                p.Line((float)u.X, (float)u.Y, _mouseW.X, _mouseW.Y, 1.5f, C.Accent);
                float rr = _mode switch { "flash" => 6, "smoke" => 3.3f, "gas" => 3.6f, _ => 2 } * TS;
                p.Ring(_mouseW.X, _mouseW.Y, rr, 1.5f, Color.White * 0.5f);
            }
        }
    }
    private readonly List<(string text, Vector2 pos, Color col, float size)> _overlayLabels = new();

    // ================= HUD =================
    private void HudRect(RectangleF r) => _hud.Add(r);

    private void Hud()
    {
        _hud.Clear();
        P.Begin();
        var wm = WorldM;
        foreach (var (text, pos, col, size) in _overlayLabels)
        {
            var sp = Vector2.Transform(pos, wm);
            P.Text(text, sp.X, sp.Y - size * Ui.S * 0.6f, size * Ui.S, col, FontKind.Bold, Align.Center);
        }
        _overlayLabels.Clear();
        bool touchUi = G.Platform.IsMobile || G.Input.TouchActive;
        float W = Ui.W, H = Ui.H;
        // drag box
        if (_dragBox && !G.Input.TouchActive && Vector2.Distance(G.Input.Pointer, G.Input.PressPos) > 6)
        {
            var a = G.Input.PressPos; var b = G.Input.Pointer;
            float x0 = Math.Min(a.X, b.X), y0 = Math.Min(a.Y, b.Y);
            P.FillRect(x0, y0, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y), C.Accent * 0.08f);
            P.Rect(x0, y0, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y), C.Accent, 1);
        }
        // ---- top bar ----
        float tbW = Math.Min(W - 20, 720), tx = W / 2 - tbW / 2;
        var top = new RectangleF(tx, 6, tbW, 42); HudRect(top);
        Ui.Fill(top, C.Hex("#0a0e12") * 0.85f); Ui.Border(top, C.Line);
        string phase = M.Phase == "plan" ? "PLANNING" : M.Paused ? "PAUSED" : "LIVE";
        Ui.Label(phase, tx + 10, 18, 13, M.Phase == "plan" ? C.Blue : M.Paused ? C.Accent : C.Green, FontKind.Bold);
        Ui.Label(MathUtil.Time(M.T), tx + 100, 16, 16, C.Hex("#e9eef2"), FontKind.Mono);
        float bx = tx + 170;
        bool waiting = Ops.Any(u => u.Orders.Count > 0 && u.Orders[0].Kind == "go");
        if (M.Phase == "plan") { if (Ui.Button(new RectangleF(bx, 11, 120, 32), "EXECUTE ▶", BtnStyle.Primary, true, 12)) Execute(); bx += 126; }
        else { if (Ui.Button(new RectangleF(bx, 11, 90, 32), M.Paused ? "Resume ▶" : "Pause ❚❚", BtnStyle.Normal, true, 12)) TogglePause(); bx += 96; }
        if (Ui.Button(new RectangleF(bx, 11, 60, 32), "GO", waiting ? BtnStyle.Primary : BtnStyle.Normal, true, 12)) M.Go();
        bx += 66;
        if (Ui.Button(new RectangleF(bx, 11, 48, 32), $"×{M.Speed}", BtnStyle.Normal, true, 11)) SetSpeed(M.Speed >= 2 ? 0.5 : M.Speed * 2);
        bx += 54;
        if (Ui.Button(new RectangleF(bx, 11, 116, 32), "Complete Op", M.CanComplete ? BtnStyle.Primary : BtnStyle.Normal, true, 11)) EndMission(true);
        bx += 122;
        if (Ui.Button(new RectangleF(bx, 11, 70, 32), "Abort", BtnStyle.Danger, true, 11))
            Dialogs.Confirm(G, "Abort operation?", "The squad extracts immediately. Remaining objectives count as failed.", "Abort & Extract", () => EndMission(false, "Operation aborted"));
        bx += 76;
        if (bx + 34 < tx + tbW && Ui.Button(new RectangleF(bx, 11, 34, 32), "?", BtnStyle.Ghost, true, 12)) _help = !_help;
        // ---- floors ----
        float fy = 6, fxx = 8;
        for (int i = 0; i < M.Map.Floors.Length; i++)
        {
            int n = Ops.Count(u => u.Floor == i && !u.Dead);
            string lab = M.Map.Labels[i] + (n > 0 ? $" ({n})" : "");
            float fw = Ui.TextW(lab, 11) + 18;
            var fr = new RectangleF(fxx, fy, fw, 30); HudRect(fr);
            if (Ui.Button(fr, lab, i == _floor ? BtnStyle.On : BtnStyle.Normal, true, 11)) _floor = i;
            fxx += fw + 4;
        }
        // ---- objectives ----
        var objs = M.Objectives;
        bool showObj = !touchUi || _objOpen;
        float objW = 290;
        if (touchUi)
        {
            var ob = new RectangleF(W - 110, 54, 102, 30); HudRect(ob);
            if (Ui.Button(ob, _objOpen ? "Objectives ▴" : "Objectives ▾", _objOpen ? BtnStyle.On : BtnStyle.Normal, true, 11)) _objOpen = !_objOpen;
        }
        if (showObj)
        {
            float oh = 30 + objs.Count * 18 + (M.Night ? 18 : 0) + (M.Rain ? 18 : 0);
            var orc = touchUi ? new RectangleF(W - objW - 8, 88, objW, oh) : new RectangleF(8, 42, objW, oh);
            HudRect(orc);
            Ui.Fill(orc, C.Hex("#0a0e12") * 0.85f); Ui.Border(orc, C.Line);
            Ui.Label(M.Cfg.Name.ToUpperInvariant(), orc.X + 8, orc.Y + 7, 12, C.Hex("#9fb0bf"), FontKind.Bold);
            float oy = orc.Y + 28;
            foreach (var o in objs)
            {
                var c = o.Done ? C.Green : o.Fail ? C.Red : o.Req ? C.Text : C.Dim;
                Ui.Label($"{(o.Done ? "✔" : o.Fail ? "✖" : "☐")} {o.Text}{(o.Req ? "" : " (opt)")}", orc.X + 8, oy, 11, c);
                oy += 18;
            }
            if (M.Night) { Ui.Label("Night operation", orc.X + 8, oy, 11, C.Dim); oy += 18; }
            if (M.Rain) Ui.Label("Rain — reduced hearing", orc.X + 8, oy, 11, C.Dim);
        }
        // ---- radio log ----
        if (!(touchUi && (_cmdOpen || _objOpen)))
        {
            float lw = touchUi ? 300 : 340, lh = touchUi ? 92 : 190;
            var lr = touchUi ? new RectangleF(W - lw - 8, H - 96 - lh, lw, lh) : new RectangleF(W - lw - 8, 54, lw, lh);
            Ui.Fill(lr, C.Hex("#0a0e12") * 0.7f); Ui.Border(lr, C.Line);
            float ly = lr.Bottom - 16;
            for (int i = M.Log.Count - 1; i >= 0 && ly > lr.Y + 2; i--)
            {
                var l = M.Log[i];
                string s = $"{MathUtil.Time(l.T)} {l.Text}";
                while (s.Length > 10 && Ui.TextW(s, 10, FontKind.Mono) > lw - 12) s = s[..^4] + "…";
                Ui.Label(s, lr.X + 6, ly, 10, C.Hex(l.Color), FontKind.Mono);
                ly -= 15;
            }
        }
        // ---- operator cards ----
        var ops = Ops.ToList();
        float cw = touchUi ? 128 : 172, chh = touchUi ? 78 : 86, cx = 8, cy = H - chh - 8;
        for (int i = 0; i < ops.Count; i++)
        {
            var u = ops[i];
            var r = new RectangleF(cx, cy, cw, chh); HudRect(r);
            bool sel = _sel.Contains(u);
            Ui.Fill(r, C.Hex("#0c1117") * 0.92f);
            Ui.Border(r, u.Downed || u.Dead ? C.Red : sel ? C.Accent : C.Line, sel ? 2 : 1);
            Ui.Fill(new RectangleF(cx + 6, cy + 8, 8, 8), C.Hex(u.Color));
            Ui.Label(u.Name, cx + 18, cy + 4, 12, C.Text, FontKind.Bold);
            if (!touchUi) Ui.Label(GameData.Role[u.Role].Name, cx + 24 + Ui.TextW(u.Name, 12, FontKind.Bold), cy + 6, 10, C.Dim);
            Ui.Label((i + 1).ToString(), r.Right - 12, cy + 4, 10, C.Dim, FontKind.Mono);
            float hp = (float)Math.Max(0, u.Hp / u.MaxHp);
            Ui.Bar(new RectangleF(cx + 6, cy + 22, cw - 12, 5), hp, hp > 0.6f ? C.Green : hp > 0.3f ? C.Accent : C.Red);
            var w = u.W;
            Ui.Label($"{w.St.Name} {w.Mag}/{w.Reserve} {(u.Pace == "run" ? "»" : u.Pace == "slow" ? "·" : "›")}{(u.Stance == "crouch" ? " ⤓" : "")}", cx + 6, cy + 31, 10, C.Text, FontKind.Mono);
            var util = string.Join(" ", u.Util.GroupBy(x => x).Select(gp => $"{GameData.Item[gp.Key].Short}×{gp.Count()}"));
            if (!touchUi || chh > 70) Ui.Label((util.Length > 0 ? util : "—") + (u.Tool != null ? " · " + GameData.Item[u.Tool].Short + (u.Tool == "medkit" ? $"({u.ToolUses})" : "") : ""), cx + 6, cy + 46, 9, C.Dim, FontKind.Mono);
            string st = u.Dead ? "KIA" : u.Status + (u.Orders.Count > 1 ? $" [+{u.Orders.Count - 1}]" : "") + (u.Stress > 50 ? " · stressed" : "");
            if (st.Length > 26) st = st[..25] + "…";
            Ui.Label(st, cx + 6, cy + chh - 18, 10, u.Status == "WAITING FOR GO" ? C.Accent : C.Dim);
            if (Ui.Clicked(r))
            {
                if (_multi || G.Input.Shift) { if (_sel.Contains(u)) _sel.Remove(u); else _sel.Add(u); }
                else { if (_sel.Count == 1 && _sel[0] == u) { _cam.X = (float)u.X; _cam.Y = (float)u.Y; } _sel = new() { u }; }
                _floor = u.Floor; G.Audio.Ui();
            }
            cx += cw + 5;
        }
        // ---- command panel ----
        var cmds = new (string name, string key, Action act)[]
        {
            ("Hold", "H", () => M.OrderHold(Sel)), ("Follow", "F", FollowMe), ("Regroup", "R", Regroup), ("Cover", "V", () => M.OrderCover(Sel)),
            ("Crouch", "C", () => M.ToggleCrouch(Sel)), ("Observe", "O", () => M.OrderFace(Sel, _mouseW.X, _mouseW.Y)), ("Pace", "Z", CyclePace), ("Cancel", "X", () => M.OrderCancel(Sel)),
            ("Flash", "G", () => SetMode("flash")), ("Smoke", "T", () => SetMode("smoke")), ("Gas", "Y", () => SetMode("gas")), ("Divert", "U", () => SetMode("stinger")),
            ("Wait GO", "B", () => M.OrderGoWait(Sel)), ("GO", "⏎", () => M.Go()), ("Formation", "N", () => M.Formation = M.Formation == "wedge" ? "stack" : "wedge"),
            ("Floor", "Tab", () => _floor = (_floor + 1) % M.Map.Floors.Length),
        };
        if (!touchUi || _cmdOpen)
        {
            float bw = touchUi ? 86 : 82, bh = touchUi ? 38 : 28;
            float pw = bw * 4 + 5 * 4, ph = bh * 4 + 5 * 4;
            var pr = new RectangleF(W - pw - 8, H - ph - (touchUi ? 96 : 8), pw, ph); HudRect(pr);
            Ui.Fill(pr, C.Hex("#0a0e12") * 0.88f); Ui.Border(pr, C.Line);
            for (int i = 0; i < cmds.Length; i++)
            {
                var (name, key, act) = cmds[i];
                var br = new RectangleF(pr.X + 4 + (i % 4) * (bw + 5), pr.Y + 4 + (i / 4) * (bh + 5), bw, bh);
                bool on = _mode != null && ((name == "Flash" && _mode == "flash") || (name == "Smoke" && _mode == "smoke") || (name == "Gas" && _mode == "gas") || (name == "Divert" && _mode == "stinger"));
                if (Ui.Button(br, touchUi ? name : $"{name} {key}", on ? BtnStyle.On : BtnStyle.Normal, true, touchUi ? 12 : 10)) act();
            }
        }
        // ---- touch toolbar ----
        if (touchUi)
        {
            var items = new (string label, bool on, Action act)[]
            {
                ("All", false, () => _sel = Ops.Where(Mission.CanAct).ToList()), ("Multi", _multi, () => _multi = !_multi), ("+Waypoint", _append, () => _append = !_append),
                ("Cam Lock", _lockCam, () => _lockCam = !_lockCam), (_cmdOpen ? "Orders ▾" : "Orders ▴", _cmdOpen, () => _cmdOpen = !_cmdOpen),
            };
            float tby = H - chh - 8 - 44, tbx = 8;
            foreach (var (label, on, act) in items)
            {
                float bw = Ui.TextW(label, 12) + 22;
                var br = new RectangleF(tbx, tby, bw, 36); HudRect(br);
                if (Ui.Button(br, label, on ? BtnStyle.On : BtnStyle.Normal, true, 12)) act();
                tbx += bw + 4;
            }
        }
        // ---- door menu ----
        if (_doorMenu.HasValue) DoorMenu();
        // ---- banners ----
        if (_banner != null && _bannerT > 0)
        {
            float a = (float)Math.Min(1, _bannerT);
            Ui.Label(_banner, W / 2, H * 0.2f, 26, C.Accent * a, FontKind.Bold, Align.Center);
            if (M.Phase == "plan") Ui.Label(touchUi ? "Time is frozen. Tap operators, then tap where to go or a door to breach. Press EXECUTE when ready." : "Time is frozen. Plan routes, breaches and GO-codes, then press EXECUTE (Enter).", W / 2, H * 0.2f + 36, 12, C.Text * 0.85f, FontKind.Regular, Align.Center);
        }
        if (M.CanComplete && M.Phase == "live") Ui.Label("AREA SECURE — press \"Complete Op\" when ready", W / 2, 56, 14, C.Green, FontKind.Bold, Align.Center);
        if (_mode != null) Ui.Label($"{(touchUi ? "Tap" : "Click")} to throw {GameData.Item[_mode].Name} · {(touchUi ? "Back" : "Esc / right-click")} to cancel", W / 2, H * 0.28f, 13, C.Accent, FontKind.Bold, Align.Center);
        if (_help) Help(touchUi);
    }

    private void DoorMenu()
    {
        var (d, screen) = _doorMenu.Value;
        var s = Sel;
        bool Has(Func<Unit, bool> f) => s.Any(f);
        var items = new List<(string id, string label)>();
        string title = (d.Exterior ? "Exterior Door" : "Door") + (d.KnownLocked ? (d.Barricaded ? " (BARRICADED)" : d.Locked ? " (LOCKED)" : "") : "");
        if (d.State == DoorState.Closed)
        {
            items.Add(("openclear", "Open & Clear"));
            if (Has(u => u.Util.Contains("flash"))) items.Add(("flashclear", "Flashbang & Clear"));
            items.Add(("kickclear", "Kick & Clear"));
            if (Has(u => u.Util.Contains("charge"))) items.Add(("charge", "Breaching Charge & Clear"));
            if (Has(u => u.Weapons.Any(w => w.St.Cat == "sg"))) items.Add(("shotgun", "Shotgun Breach & Clear"));
            if (Has(u => u.Tool == "ram")) items.Add(("ram", "Ram & Clear"));
            if (Has(u => u.Tool == "lockpick")) items.Add(("pick", "Pick Lock (quiet)"));
            if (Has(u => u.Tool == "doorcam")) items.Add(("cam", "Inspect (Door Camera)"));
            items.Add(("open", "Open Only"));
        }
        else
        {
            items.Add(("openclear", "Clear Room"));
            if (Has(u => u.Util.Contains("flash"))) items.Add(("flashclear", "Flashbang & Clear"));
            if (d.State == DoorState.Open) items.Add(("close", "Close Door"));
        }
        items.Add(("stack", "Stack Up"));
        bool touchUi = G.Platform.IsMobile || G.Input.TouchActive;
        float bh = touchUi ? 38 : 28, w = 220;
        float x = Math.Min(screen.X / Ui.S, Ui.W - w - 8), y = Math.Min(screen.Y / Ui.S, Ui.H - (items.Count + 1) * bh - 12);
        var r = new RectangleF(x, y, w, (items.Count + 1) * bh + 4); HudRect(r);
        Ui.Fill(r, C.Panel); Ui.Border(r, C.Accent);
        Ui.Label(title.ToUpperInvariant(), x + 10, y + bh / 2 - 7, 11, C.Accent, FontKind.Bold);
        for (int i = 0; i < items.Count; i++)
        {
            var (id, label) = items[i];
            if (Ui.Button(new RectangleF(x + 2, y + bh * (i + 1), w - 4, bh - 2), label, BtnStyle.Ghost, true, touchUi ? 13 : 12))
            {
                M.OrderDoor(s, d, id, _menuAppend || _append || G.Input.Shift);
                _doorMenu = null;
                return;
            }
        }
    }

    private void Help(bool touchUi)
    {
        var r = new RectangleF(8, 140, 380, touchUi ? 250 : 330); HudRect(r);
        Ui.Fill(r, C.Hex("#0a0e12") * 0.92f); Ui.Border(r, C.Line);
        string text = touchUi
            ? "TOUCH\nTap operator: select (Multi to add, All = squad)\nTap ground / door / suspect / item: give order\nLong-press or +Waypoint: queue waypoint\nDrag: pan · Pinch: zoom · Twist: rotate\nOrders: command panel (grenades, hold, GO…)\nBack button: pause / close menus"
            : "CONTROLS\nLMB select / drag-box · 1–8 select operator (twice = centre)\nRMB move · Shift+RMB waypoint · RMB-drag move + facing\nRMB door: breach menu · RMB suspect/civ/item/teammate: act\nSpace pause · [ ] time scale · Enter GO / Execute\nG/T/Y/U flash/smoke/gas/diversion, then click\nB wait-for-GO · H hold · F follow · R regroup · V cover\nC crouch · O face cursor · Z pace · N formation · X cancel\nWASD/arrows pan · wheel zoom · MMB pan · Q/E rotate\nTab floor · L camera lock · F1 help";
        Ui.Wrap(text, r.X + 10, r.Y + 10, r.Width - 20, 11);
    }
}
