using Breachpoint.Core.Map;
using Breachpoint.Core.Sim;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using Microsoft.Xna.Framework;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

/// <summary>Plays back a recorded operation: overview or any operator's body camera.</summary>
public sealed class ReplayScreen : Screen
{
    private readonly Mission _m;
    private readonly Screen _back;
    private MapRenderer _r;
    private readonly Camera _cam = new();
    private List<Unit> _ghosts;
    private double _t, _real;
    private bool _playing = true;
    private double _speed = 1;
    private int _follow = -1, _floor, _evIdx;
    private Dictionary<int, DoorState> _finalDoors;
    private Vector2? _drag;

    public ReplayScreen(BreachpointGame g, Mission m, Screen back) : base(g) { _m = m; _back = back; }

    public override void Enter()
    {
        _r = new MapRenderer(G, _m.Map);
        _ghosts = _m.Units.Select(u => u.Clone()).ToList();
        foreach (var gh in _ghosts) { if (gh.Ai != null) { gh.Ai = new AiState { State = "idle" }; } }
        _finalDoors = _m.Map.Doors.ToDictionary(d => d.Id, d => d.State);
        var vp = G.GraphicsDevice.Viewport;
        _cam.X = _m.Map.W * 16; _cam.Y = _m.Map.H * 16;
        _cam.Zoom = Math.Min(vp.Width / (_m.Map.W * 32f), vp.Height / (_m.Map.H * 32f)) * 1.1f;
    }

    public override void Leave()
    {
        foreach (var d in _m.Map.Doors) d.State = _finalDoors[d.Id];
        _r.Dispose();
    }

    public override bool Back() { G.Switch(_back); return true; }

    private void Seek(double t)
    {
        _t = Math.Clamp(t, 0, _m.T);
        _r.ClearEffects();
        _evIdx = _m.Events.FindIndex(e => e.T >= _t);
        if (_evIdx < 0) _evIdx = _m.Events.Count;
    }

    public override void Update(double dt)
    {
        _real += dt;
        if (_playing) { _t += dt * _speed; if (_t >= _m.T) { _t = _m.T; _playing = false; } }
        var F = _m.Frames;
        if (F.Count > 0)
        {
            int lo = 0, hi = F.Count - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (F[mid].T <= _t) lo = mid; else hi = mid - 1; }
            var a = F[lo]; var b = F[Math.Min(F.Count - 1, lo + 1)];
            double k = b.T > a.T ? Math.Clamp((_t - a.T) / (b.T - a.T), 0, 1) : 0;
            for (int i = 0; i < _ghosts.Count && i * 6 + 5 < a.U.Length; i++)
            {
                var g = _ghosts[i];
                int ai = i * 6;
                bool sameF = a.U[ai + 2] == b.U[ai + 2];
                g.X = sameF ? MathUtil.Lerp(a.U[ai], b.U[ai], k) : a.U[ai];
                g.Y = sameF ? MathUtil.Lerp(a.U[ai + 1], b.U[ai + 1], k) : a.U[ai + 1];
                g.Floor = a.U[ai + 2];
                g.Angle = a.U[ai + 3] / 100.0 + MathUtil.AngDiff(a.U[ai + 3] / 100.0, b.U[ai + 3] / 100.0) * k;
                int s = a.U[ai + 4];
                g.Dead = (s & 1) != 0; g.Downed = (s & 2) != 0; g.Incap = (s & 4) != 0; g.Restrained = (s & 8) != 0; g.Surrendered = (s & 16) != 0;
                g.Stun = (s & 32) != 0 ? 1 : 0; g.Escaped = (s & 64) != 0; g.Evacuated = false; g.Stance = (s & 128) != 0 ? "crouch" : "stand";
                g.Armed = (s & 256) == 0; g.HostageBound = (s & 512) != 0; g.Kneel = (s & 1024) != 0; g.Hp = a.U[ai + 5];
                g.Alive = !g.Dead && !g.Escaped;
                if (g.Ai != null) g.Ai.State = g.Surrendered ? "surrender" : "idle";
            }
        }
        foreach (var d in _m.Map.Doors) d.State = d.State0;
        foreach (var e in _m.Events) { if (e.T > _t) break; if (e.Type == "door") _m.Map.Doors[e.Id].State = Enum.Parse<DoorState>(e.DoorState); }
        while (_evIdx < _m.Events.Count && _m.Events[_evIdx].T <= _t)
        {
            var e = _m.Events[_evIdx++];
            if (e.Type == "shot")
            {
                _r.Spawn(new FxEvent { Type = "tracer", X = e.X0, Y = e.Y0, X1 = e.X1, Y1 = e.Y1, F = e.F, Team = e.Team });
                _r.Spawn(new FxEvent { Type = "muzzle", X = e.X0, Y = e.Y0, F = e.F, Ang = Math.Atan2(e.Y1 - e.Y0, e.X1 - e.X0) });
                if (_playing && _speed <= 1) G.Audio.Play("shot_rifle_in", e.X0, e.Y0, 0.6f);
            }
            if (e.Type == "boom") _r.Spawn(new FxEvent { Type = e.G == "charge" ? "explosion" : "flash", X = e.X0, Y = e.Y0, F = e.F, Big = true, R = 50 });
        }
        _r.UpdateParticles(_playing ? (float)(dt * _speed) : 0);
        var inp = G.Input;
        if (_follow < 0 && inp.Down && !G.ModalOpen && inp.Pointer.Y < G.GraphicsDevice.Viewport.Height - 70 * Ui.S)
        {
            if (_drag.HasValue) { var d = (inp.Pointer - _drag.Value) / _cam.Zoom; float c = MathF.Cos(-_cam.Rot), s = MathF.Sin(-_cam.Rot); _cam.X -= d.X * c - d.Y * s; _cam.Y -= d.X * s + d.Y * c; }
            _drag = inp.Pointer;
        }
        else _drag = null;
        if (inp.Wheel != 0) _cam.Zoom = Math.Clamp(_cam.Zoom * (inp.Wheel > 0 ? 1.12f : 0.89f), 0.2f, 3f);
        if (inp.Touches.Count >= 2)
        {
            var a = inp.Touches[0].Pos; var b = inp.Touches[1].Pos;
            float dd = Vector2.Distance(a, b);
            if (_pinchD > 0) _cam.Zoom = Math.Clamp(_pinchZ * dd / _pinchD, 0.2f, 3f); else { _pinchD = dd; _pinchZ = _cam.Zoom; }
        }
        else _pinchD = 0;
        if (inp.KeyPressed(Microsoft.Xna.Framework.Input.Keys.Space)) _playing = !_playing;
    }
    private float _pinchD, _pinchZ;

    public override void Draw()
    {
        var view = new View { Map = _m.Map, Cam = _cam, Units = _ghosts, RealTime = _real, GameTime = _t };
        if (_follow >= 0)
        {
            var g = _ghosts[_follow];
            _cam.X = (float)g.X; _cam.Y = (float)g.Y; _cam.Rot = (float)(-g.Angle - Math.PI / 2); _cam.Zoom = 1.4f * Ui.S; _floor = g.Floor;
            if (!g.Dead && !g.Downed) view.Vision.Add((new Vector2((float)g.X, (float)g.Y), _m.VisionPoly(g)));
            foreach (var o in _ghosts) o.Visible = o == g || (!g.Dead && _m.CanSee(g, o));
            view.Night = _m.Night;
        }
        else { view.ShowAll = true; view.Items = _m.Items.Select(i => new MissionItem { Id = i.Id, Kind = i.Kind, Type = i.Type, X = i.X, Y = i.Y, F = i.F, Seen = true }).ToList(); _cam.Rot = 0; }
        view.Floor = _floor;
        _r.Draw(P, view);
        P.Begin();
        string title = _follow >= 0 ? $"BODY CAM — {_ghosts[_follow].Name.ToUpperInvariant()}  {MathUtil.Time(_t)}" : $"MISSION REPLAY  {MathUtil.Time(_t)}";
        Ui.Label(title, 16, 14, 15, C.Text, FontKind.Mono);
        if (_follow >= 0)
        {
            Ui.Border(new RectangleF(10, 10, Ui.W - 20, Ui.H - 90), Color.White * 0.2f, 2);
            if (Math.Floor(_real * 2) % 2 == 0) P.Circle((Ui.W - 30) * Ui.S, 30 * Ui.S, 7 * Ui.S, C.Red);
        }
        float ly = 40;
        foreach (var l in _m.Log.Where(l => l.T <= _t && _t - l.T < 3).TakeLast(3)) { Ui.Label(l.Text, 16, ly, 12, C.Hex(l.Color), FontKind.Mono); ly += 18; }
        // control bar
        var bar = new RectangleF(0, Ui.H - 62, Ui.W, 62);
        Ui.Fill(bar, C.Hex("#0a0e12") * 0.92f); Ui.Line(0, bar.Y, Ui.W, bar.Y, C.Line);
        float x = 12, y = bar.Y + 12;
        Ui.Label("● REC", x, y + 8, 13, C.Red, FontKind.Bold); x += 70;
        if (Ui.Button(new RectangleF(x, y, 60, 38), _playing ? "❚❚" : "▶")) _playing = !_playing; x += 66;
        if (Ui.Button(new RectangleF(x, y, 54, 38), $"×{_speed}")) _speed = _speed >= 4 ? 0.5 : _speed * 2; x += 60;
        float sw = Math.Max(160, Ui.W - x - 640);
        float nv = Ui.Slider(new RectangleF(x, y + 7, sw, 24), (float)(_t / Math.Max(0.01, _m.T)));
        if (Math.Abs(nv - _t / Math.Max(0.01, _m.T)) > 0.002 && G.Input.Down) Seek(nv * _m.T);
        x += sw + 10;
        Ui.Label($"{MathUtil.Time(_t)} / {MathUtil.Time(_m.T)}", x, y + 9, 12, C.Text, FontKind.Mono); x += 110;
        var ops = _ghosts.Select((g, i) => (g, i)).Where(p => p.g.Team == Team.Op).ToList();
        var camNames = new List<string> { "Overview" }; camNames.AddRange(ops.Select(p => "Body cam — " + p.g.Name));
        int ci = _follow < 0 ? 0 : ops.FindIndex(p => p.i == _follow) + 1;
        int d = Ui.Cycler(new RectangleF(x, y, 230, 38), camNames[ci], true, 11);
        if (d != 0) { ci = Gui.Wrap(ci + d, camNames.Count); _follow = ci == 0 ? -1 : ops[ci - 1].i; _r.ClearEffects(); if (_follow < 0) Enter2(); }
        x += 236;
        if (_follow < 0 && _m.Map.Floors.Length > 1)
        {
            int fd = Ui.Cycler(new RectangleF(x, y, 150, 38), _m.Map.Labels[_floor], true, 11);
            if (fd != 0) _floor = Gui.Wrap(_floor + fd, _m.Map.Floors.Length);
        }
        if (Ui.Button(new RectangleF(Ui.W - 140, y, 128, 38), "Exit Replay", BtnStyle.Primary)) G.Switch(_back);
    }

    public void TestBodyCam() { _t = _m.T * 0.4; Seek(_t); _follow = _ghosts.FindIndex(g => g.Team == Team.Op); }

    private void Enter2()
    {
        var vp = G.GraphicsDevice.Viewport;
        _cam.X = _m.Map.W * 16; _cam.Y = _m.Map.H * 16; _cam.Rot = 0;
        _cam.Zoom = Math.Min(vp.Width / (_m.Map.W * 32f), vp.Height / (_m.Map.H * 32f)) * 1.1f;
    }
}
