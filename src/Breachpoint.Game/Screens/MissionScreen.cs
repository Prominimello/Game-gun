using Breachpoint.Core.Data;
using Breachpoint.Core.Map;
using Breachpoint.Core.Sim;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Breachpoint.Screens;

/// <summary>The tactical operation: camera, input, orders, HUD and the simulation loop.</summary>
public sealed partial class MissionScreen : Screen
{
    private const int TS = GameMap.TS;
    public readonly Mission M;
    private readonly MapRenderer _r;
    private readonly Action<Mission> _onEnd;
    private readonly Camera _cam = new();
    private List<Unit> _sel = new();
    private int _floor;
    private string _mode;               // grenade being aimed: flash, smoke, gas, stinger
    private bool _lockCam, _help, _append, _multi, _cmdOpen, _objOpen, _ended;
    private string _banner = "PLANNING PHASE";
    private double _bannerT = 3, _visT, _real;
    private Vector2 _mouseW;
    private readonly List<RectangleF> _hud = new();
    private (Door door, Vector2 screen)? _doorMenu;
    private Texture2D2 _vehicle;

    // pointer / gesture state
    private bool _dragBox, _panning, _longFired;
    private Vector2 _panWorld;
    private (float d, float ang, float zoom, float rot, Vector2 mid)? _pinch;

    private sealed class Texture2D2 { public Microsoft.Xna.Framework.Graphics.Texture2D Tex; public Vector2 Pos; public float Ang; }

    public MissionScreen(BreachpointGame g, Mission m, MapRenderer renderer, Action<Mission> onEnd) : base(g)
    {
        M = m; _onEnd = onEnd;
        _r = renderer ?? new MapRenderer(g, m.Map);
        _r.ClearEffects();
    }

    private IEnumerable<Unit> Ops => M.Ops;
    private List<Unit> Sel => _sel.Where(Mission.CanAct).ToList();

    public override void Enter()
    {
        var ops = Ops.ToList();
        _sel = ops.ToList();
        float ox = (float)ops.Average(u => u.X), oy = (float)ops.Average(u => u.Y);
        var b0 = M.Map.Buildings.FirstOrDefault();
        float bx = b0 != null ? (b0.X + b0.W / 2f) * TS : M.Map.W * TS / 2f, by = b0 != null ? (b0.Y + b0.H / 2f) * TS : M.Map.H * TS / 2f;
        _cam.X = ox * 0.6f + bx * 0.4f; _cam.Y = oy * 0.6f + by * 0.4f;
        _cam.Zoom = G.Platform.IsMobile ? 1.0f : 1.0f;
        var sp = M.Map.Spawns.FirstOrDefault(s => s.Name == ops[0].Entry) ?? M.Map.Spawns[0];
        if (G.Profile != null)
        {
            var v = G.Profile.Vehicle(); var def = GameData.Vehicle[v.Id];
            float ang = (float)sp.Ang;
            _vehicle = new Texture2D2
            {
                Tex = G.Art.Vehicle(def, v),
                Pos = new Vector2(Math.Clamp((sp.X + 0.5f) * TS - MathF.Cos(ang) * TS * 2.2f, TS * 2, M.Map.W * TS - TS * 2), Math.Clamp((sp.Y + 0.5f) * TS - MathF.Sin(ang) * TS * 2.2f, TS * 2, M.Map.H * TS - TS * 2)),
                Ang = ang + MathF.PI / 2,
            };
        }
        string amb = M.Map.Type == "bunker" ? "bunker" : M.Map.Type == "rural" ? "rural" : "city";
        G.Audio.Ambient(amb, M.Rain ? "rain" : null);
        G.Audio.MusicTrack("mission");
        M.RefreshVisibility();
    }

    public override void Leave() { _r.Dispose(); G.Platform.StopSpeaking(); }

    public override bool Back()
    {
        if (_doorMenu != null) { _doorMenu = null; return true; }
        if (_mode != null) { _mode = null; return true; }
        if (M.Phase == "live") { M.Paused = !M.Paused; _banner = M.Paused ? "PAUSED" : null; _bannerT = M.Paused ? 999 : 0; }
        return true;
    }

    // ================= coordinate helpers =================
    private Matrix WorldM => _r.WorldMatrix(_cam, G.GraphicsDevice.Viewport.Width, G.GraphicsDevice.Viewport.Height);
    private Vector2 ScreenToWorld(Vector2 px) => Vector2.Transform(px, Matrix.Invert(WorldM));
    private Vector2 WorldToScreen(float x, float y) => Vector2.Transform(new Vector2(x, y), WorldM);
    private bool OverHud(Vector2 px) => _hud.Any(r => r.Contains(px / Ui.S));

    // ================= update =================
    public override void Update(double dt)
    {
        _real += dt;
        var inp = G.Input;
        if (!G.ModalOpen && Ui.FocusId == null) { HandleKeys(inp, (float)dt); HandlePointer(inp); }
        _cam.Rot += (_cam.TargetRot - _cam.Rot) * Math.Min(1, (float)dt * 8);
        var prevFloor = _sel.Count > 0 ? _sel[0].Floor : -1;
        M.Update(dt);
        if (_sel.Count > 0 && prevFloor == _floor && _sel[0].Floor != prevFloor) _floor = _sel[0].Floor;
        if (_lockCam && _sel.Count > 0) { _cam.X += ((float)_sel[0].X - _cam.X) * Math.Min(1, (float)dt * 6); _cam.Y += ((float)_sel[0].Y - _cam.Y) * Math.Min(1, (float)dt * 6); _floor = _sel[0].Floor; }
        _cam.X = Math.Clamp(_cam.X, 0, M.Map.W * TS); _cam.Y = Math.Clamp(_cam.Y, 0, M.Map.H * TS);
        foreach (var e in M.Fx) _r.Spawn(e);
        M.Fx.Clear();
        var a = G.Audio;
        a.LX = _cam.X; a.LY = _cam.Y; a.LRange = G.GraphicsDevice.Viewport.Width / 2f / _cam.Zoom; a.LRot = _cam.Rot;
        foreach (var s in M.Sounds) a.Play(s);
        M.Sounds.Clear();
        foreach (var v in M.Voices) a.Voice(v.Text, v.Pitch, v.Prio);
        M.Voices.Clear();
        _r.UpdateParticles(M.Paused ? 0 : (float)(dt * M.Speed));
        if (M.Paused) { _visT -= dt; if (_visT <= 0) { _visT = 0.25; M.RefreshVisibility(); } }
        _bannerT -= dt;
        if (M.Ended && !_ended) { _ended = true; G.Platform.StopSpeaking(); _onEnd(M); }
    }

    private void HandleKeys(Input inp, float dt)
    {
        float pan = 600 * dt / _cam.Zoom;
        float mx = 0, my = 0;
        if (inp.Key(Keys.W) || inp.Key(Keys.Up)) my -= pan;
        if (inp.Key(Keys.S) || inp.Key(Keys.Down)) my += pan;
        if ((inp.Key(Keys.A) && !inp.Ctrl) || inp.Key(Keys.Left)) mx -= pan;
        if (inp.Key(Keys.D) || inp.Key(Keys.Right)) mx += pan;
        if (mx != 0 || my != 0) { float c = MathF.Cos(-_cam.Rot), s = MathF.Sin(-_cam.Rot); _cam.X += mx * c - my * s; _cam.Y += mx * s + my * c; }
        var s0 = Sel;
        if (inp.KeyPressed(Keys.Space)) TogglePause();
        for (int i = 0; i < 8; i++)
            if (inp.KeyPressed(Keys.D1 + i))
            {
                var ops = Ops.ToList(); if (i >= ops.Count) break;
                var u = ops[i];
                if (inp.Shift) { if (_sel.Contains(u)) _sel.Remove(u); else _sel.Add(u); }
                else { if (_sel.Count == 1 && _sel[0] == u) { _cam.X = (float)u.X; _cam.Y = (float)u.Y; } _sel = new() { u }; }
                _floor = u.Floor; G.Audio.Ui();
            }
        if (inp.KeyPressed(Keys.OemTilde) || (inp.Ctrl && inp.KeyPressed(Keys.A))) _sel = Ops.Where(Mission.CanAct).ToList();
        if (inp.KeyPressed(Keys.H)) { M.OrderHold(s0); G.Audio.Ui(); }
        if (inp.KeyPressed(Keys.F)) FollowMe();
        if (inp.KeyPressed(Keys.R)) Regroup();
        if (inp.KeyPressed(Keys.V)) { M.OrderCover(s0); G.Audio.Ui(); }
        if (inp.KeyPressed(Keys.C)) { M.ToggleCrouch(s0); G.Audio.Ui(); }
        if (inp.KeyPressed(Keys.O)) { M.OrderFace(s0, _mouseW.X, _mouseW.Y); G.Audio.Ui(); }
        if (inp.KeyPressed(Keys.G)) SetMode("flash");
        if (inp.KeyPressed(Keys.T)) SetMode("smoke");
        if (inp.KeyPressed(Keys.Y)) SetMode("gas");
        if (inp.KeyPressed(Keys.U)) SetMode("stinger");
        if (inp.KeyPressed(Keys.B)) { M.OrderGoWait(s0); M.AddLog("GO-code wait added.", "#e2a33a"); G.Audio.Ui(); }
        if (inp.KeyPressed(Keys.Enter)) { if (M.Phase == "plan") Execute(); else M.Go(); }
        if (inp.KeyPressed(Keys.X) || inp.KeyPressed(Keys.Delete)) { M.OrderCancel(s0); G.Audio.Ui(); }
        if (inp.KeyPressed(Keys.Z)) CyclePace();
        if (inp.KeyPressed(Keys.N)) { M.Formation = M.Formation == "wedge" ? "stack" : "wedge"; M.AddLog("Formation: " + M.Formation.ToUpperInvariant()); }
        if (inp.KeyPressed(Keys.Tab) || inp.KeyPressed(Keys.PageUp)) _floor = (_floor + 1) % M.Map.Floors.Length;
        if (inp.KeyPressed(Keys.PageDown)) _floor = (_floor + M.Map.Floors.Length - 1) % M.Map.Floors.Length;
        if (inp.KeyPressed(Keys.Q)) _cam.TargetRot -= MathF.PI / 4;
        if (inp.KeyPressed(Keys.E)) _cam.TargetRot += MathF.PI / 4;
        if (inp.KeyPressed(Keys.L)) { _lockCam = !_lockCam; M.AddLog("Camera lock " + (_lockCam ? "ON" : "OFF")); }
        if (inp.KeyPressed(Keys.Escape)) { _mode = null; _doorMenu = null; }
        if (inp.KeyPressed(Keys.F1) || inp.KeyPressed(Keys.OemQuestion)) _help = !_help;
        if (inp.KeyPressed(Keys.OemOpenBrackets)) SetSpeed(Math.Max(0.25, M.Speed / 2));
        if (inp.KeyPressed(Keys.OemCloseBrackets)) SetSpeed(Math.Min(2, M.Speed * 2));
    }

    // ================= pointer (mouse + touch) =================
    private void HandlePointer(Input inp)
    {
        var px = inp.Pointer;
        _mouseW = ScreenToWorld(px);
        // two-finger gestures
        var touches = inp.Touches.Where(t => t.State != TouchLocationState.Released && t.State != TouchLocationState.Invalid).ToList();
        if (touches.Count >= 2)
        {
            var a = touches[0].Pos; var b = touches[1].Pos;
            float d = Vector2.Distance(a, b), ang = MathF.Atan2(b.Y - a.Y, b.X - a.X);
            var mid = (a + b) / 2;
            if (_pinch == null) _pinch = (d, ang, _cam.Zoom, _cam.TargetRot, ScreenToWorld(mid));
            else
            {
                var p = _pinch.Value;
                _cam.Zoom = Math.Clamp(p.zoom * d / Math.Max(10, p.d), 0.35f, 3f);
                float dr = (float)MathUtil.AngNorm(ang - p.ang);
                if (Math.Abs(dr) > 0.15f) { _cam.TargetRot = p.rot + dr; _cam.Rot = _cam.TargetRot; }
                var w = ScreenToWorld(mid);
                _cam.X += p.mid.X - w.X; _cam.Y += p.mid.Y - w.Y;
            }
            _dragBox = false; _panning = false; _longFired = true;
            return;
        }
        if (_pinch != null && touches.Count < 2) { _pinch = null; if (touches.Count == 0) { _longFired = false; return; } }
        // wheel zoom (desktop)
        if (inp.Wheel != 0 && !OverHud(px))
        {
            var before = ScreenToWorld(px);
            _cam.Zoom = Math.Clamp(_cam.Zoom * (inp.Wheel > 0 ? 1.12f : 1 / 1.12f), 0.35f, 3f);
            var after = ScreenToWorld(px);
            _cam.X += before.X - after.X; _cam.Y += before.Y - after.Y;
        }
        if (inp.Pressed && OverHud(px)) return;
        bool startedOnHud = OverHud(inp.PressPos);
        // middle-mouse pan
        if (inp.MiddleDown) { if (!_panning) { _panning = true; _panWorld = ScreenToWorld(px); } var w = ScreenToWorld(px); _cam.X += _panWorld.X - w.X; _cam.Y += _panWorld.Y - w.Y; return; }
        if (inp.TouchActive)
        {
            if (inp.Pressed) { _longFired = false; _panning = false; _panWorld = ScreenToWorld(px); _doorMenu = null; }
            if (inp.Down && !startedOnHud)
            {
                if (!_panning && Vector2.Distance(px, inp.PressPos) > 14 * Ui.S) _panning = true;
                if (_panning) { var w = ScreenToWorld(px); _cam.X += _panWorld.X - w.X; _cam.Y += _panWorld.Y - w.Y; }
                else if (!_longFired && G.Time - inp.PressTime > 0.48) { _longFired = true; G.Platform.Vibrate(25); TapAction(px, true); }
            }
            if (inp.Released && !startedOnHud && !_panning && !_longFired && !inp.Consumed) TapAction(px, false);
            if (inp.Released) _panning = false;
            return;
        }
        // mouse
        _panning = false;
        if (inp.Pressed && !startedOnHud)
        {
            _doorMenu = null;
            if (_mode != null) { DoThrow(_mouseW, inp.Shift); if (!inp.Shift) _mode = null; return; }
            _dragBox = true;
        }
        if (inp.Released && _dragBox)
        {
            _dragBox = false;
            if (Vector2.Distance(px, inp.PressPos) < 6) SelectAt(_mouseW, inp.Shift);
            else
            {
                float x0 = Math.Min(px.X, inp.PressPos.X), x1 = Math.Max(px.X, inp.PressPos.X), y0 = Math.Min(px.Y, inp.PressPos.Y), y1 = Math.Max(px.Y, inp.PressPos.Y);
                var box = Ops.Where(o => o.Floor == _floor && Mission.CanAct(o)).Where(o => { var s = WorldToScreen((float)o.X, (float)o.Y); return s.X >= x0 && s.X <= x1 && s.Y >= y0 && s.Y <= y1; }).ToList();
                if (box.Count > 0) { _sel = inp.Shift ? _sel.Union(box).ToList() : box; G.Audio.Ui(); }
            }
        }
        if (inp.RightPressed && !OverHud(px)) { _rightStart = px; _rightWorld = _mouseW; if (_mode != null) { _mode = null; _rightStart = null; } }
        if (inp.RightReleased && _rightStart.HasValue)
        {
            var start = _rightStart.Value; _rightStart = null;
            if (Vector2.Distance(px, start) > 18) { var w = _rightWorld; RightClick(w, inp.Shift, start, inp.Ctrl || inp.Alt, MathF.Atan2(_mouseW.Y - w.Y, _mouseW.X - w.X)); }
            else RightClick(_rightWorld, inp.Shift, start, inp.Ctrl || inp.Alt, null);
        }
    }
    private Vector2? _rightStart;
    private Vector2 _rightWorld;

    private void SelectAt(Vector2 w, bool add)
    {
        var u = Ops.FirstOrDefault(o => o.Floor == _floor && Vector2.Distance(new Vector2((float)o.X, (float)o.Y), w) < 16);
        if (u == null) return;
        if (add) { if (_sel.Contains(u)) _sel.Remove(u); else _sel.Add(u); } else _sel = new() { u };
        G.Audio.Ui();
    }

    private void TapAction(Vector2 px, bool longPress)
    {
        var w = ScreenToWorld(px);
        if (_mode != null) { DoThrow(w, _append || longPress); if (!_append) _mode = null; return; }
        var u = Ops.FirstOrDefault(o => o.Floor == _floor && Vector2.Distance(new Vector2((float)o.X, (float)o.Y), w) < 22);
        if (u != null && !longPress)
        {
            if (_multi) { if (_sel.Contains(u)) _sel.Remove(u); else _sel.Add(u); } else _sel = new() { u };
            G.Audio.Ui(); return;
        }
        if (Sel.Count == 0) return;
        RightClick(w, _append || longPress, px, false, null);
    }

    private void RightClick(Vector2 w, bool append, Vector2 screen, bool face, float? faceAng)
    {
        var s = Sel; if (s.Count == 0) return;
        int tx = Mission.ToTile(w.X), ty = Mission.ToTile(w.Y), f = _floor;
        if (face) { M.OrderFace(s, w.X, w.Y); G.Audio.Ui(); return; }
        var tgt = M.Units.FirstOrDefault(u => u.Floor == f && !u.Dead && (u.Visible || u.Team == Team.Op) && Vector2.Distance(new Vector2((float)u.X, (float)u.Y), w) < 18);
        if (tgt != null)
        {
            if (tgt.Team == Team.Enemy && tgt.Alive && !tgt.Restrained && (tgt.Ai.State == "surrender" || tgt.Incap || tgt.Stun > 0)) { M.OrderInteract(s, tgt.Id, tgt.X, tgt.Y, tgt.Floor, "arrest", append); G.Audio.Ui(); return; }
            if (tgt.Team == Team.Civ && tgt.Alive && tgt.Ai.State != "secured") { M.OrderInteract(s, tgt.Id, tgt.X, tgt.Y, tgt.Floor, "secure", append); G.Audio.Ui(); return; }
            if (tgt.Team == Team.Op && ((tgt.Downed && !tgt.Dead) || tgt.Hp < tgt.MaxHp * 0.9) && !_sel.Contains(tgt)) { M.OrderInteract(s, tgt.Id, tgt.X, tgt.Y, tgt.Floor, "heal", append); G.Audio.Ui(); return; }
        }
        var it = M.Items.FirstOrDefault(i => i.F == f && !i.Collected && i.Seen && (i.Kind == "evidence" || i.Kind == "contraband") && Vector2.Distance(new Vector2((float)i.X, (float)i.Y), w) < 18);
        if (it != null) { M.OrderInteract(s, it.Id, it.X, it.Y, it.F, "collect", append); G.Audio.Ui(); return; }
        if (M.TileAt(f, tx, ty) == Tile.Door) { _doorMenu = (M.DoorAt(f, tx, ty), screen); _menuAppend = append; G.Audio.Ui(); return; }
        if (!M.Walkable(f, tx, ty, true)) { G.Audio.Error(); return; }
        M.OrderMove(s, tx, ty, f, append);
        if (faceAng.HasValue) foreach (var u in s) u.WatchAngle = faceAng;
        G.Audio.Ui();
    }
    private bool _menuAppend;

    private void SetMode(string g)
    {
        var s = Sel; if (s.Count == 0) return;
        if (!s.Any(u => u.Util.Contains(g))) { M.AddLog($"No {GameData.Item[g].Name} carried by the selected operators.", "#d9534f"); G.Audio.Error(); return; }
        _mode = g; G.Audio.Ui();
    }

    private void DoThrow(Vector2 w, bool append)
    {
        var s = Sel.Where(u => u.Util.Contains(_mode)).OrderBy(u => Vector2.Distance(new Vector2((float)u.X, (float)u.Y), w)).ToList();
        if (s.Count == 0) return;
        M.OrderThrow(s[0], _mode, w.X, w.Y, _floor, append || M.Phase == "plan");
        G.Audio.Ui();
    }

    private void FollowMe()
    {
        var s = Sel;
        if (s.Count < 2) return;
        M.OrderFollow(s.Skip(1), s[0]);
        M.AddLog($"{string.Join(", ", s.Skip(1).Select(u => u.Name))} following {s[0].Name}.");
        G.Audio.Ui();
    }

    private void Regroup()
    {
        var all = Sel.Count > 1 ? Sel : Ops.Where(Mission.CanAct).ToList();
        var leader = all.FirstOrDefault(u => u.Role == "leader") ?? all.FirstOrDefault();
        if (leader == null) return;
        M.OrderFollow(all.Where(u => u != leader), leader);
        M.AddLog($"Regroup on {leader.Name}.", "#e2a33a"); G.Audio.Ui();
    }

    private void CyclePace()
    {
        var s = Sel; if (s.Count == 0) return;
        string[] order = { "slow", "walk", "run" };
        string p = order[(Array.IndexOf(order, s[0].Pace) + 1) % 3];
        M.SetPace(s, p); M.AddLog("Pace: " + p.ToUpperInvariant()); G.Audio.Ui();
    }

    private void SetSpeed(double v) { M.Speed = v; M.AddLog($"Time scale ×{v}"); }

    private void TogglePause()
    {
        if (M.Phase == "plan") { Execute(); return; }
        M.Paused = !M.Paused; _banner = M.Paused ? "PAUSED" : null; _bannerT = M.Paused ? 999 : 0; G.Audio.Ui();
    }

    private void Execute() { M.Start(); _banner = "EXECUTE"; _bannerT = 1.5; G.Audio.Confirm(); }

    private void EndMission(bool success, string reason = null)
    {
        if (!M.Ended) M.Finish(success, reason);
    }

    /// <summary>Used by the autotest to drive the screen.</summary>
    public void TestBreachNearestDoor(string action)
    {
        var ops = Ops.Where(Mission.CanAct).ToList();
        var L = ops[0];
        var d = M.Map.Doors.Where(x => x.Exterior && x.F == 0).OrderBy(x => Math.Abs(x.X * TS - L.X) + Math.Abs(x.Y * TS - L.Y)).First();
        M.OrderDoor(ops, d, action, false);
        _cam.X = d.X * TS; _cam.Y = d.Y * TS; _cam.Zoom = 1.3f;
    }
    public void TestExecute() => Execute();
    public void TestZoom(float z) { _cam.Zoom = z; _cam.X = M.Map.W * 16; _cam.Y = M.Map.H * 16; }
    public void TestComplete() => EndMission(true);
}
