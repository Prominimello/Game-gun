using Breachpoint.Core.Sim;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

/// <summary>After-action report: rank, score breakdown, finances, operators, evidence, timeline.</summary>
public sealed class DebriefScreen : Screen
{
    private readonly MissionConfig _cfg;
    private readonly Mission _m;
    private readonly ResultSummary _sum;
    private float _h = 900;
    private bool _stamped;

    public DebriefScreen(BreachpointGame g, MissionConfig cfg, Mission m, ResultSummary sum) : base(g) { _cfg = cfg; _m = m; _sum = sum; }

    public override void Enter()
    {
        G.Audio.MusicTrack("debrief");
        G.Audio.Ambient("hq");
    }

    public Mission TestMission => _m;

    public override bool Back() { G.Switch(new HqScreen(G)); return true; }

    public override void Draw()
    {
        if (!_stamped && G.Time > 0) { _stamped = true; G.Audio.Play("rankStamp"); }
        var r = _m.Result;
        P.Begin();
        P.Gradient(0, 0, Ui.W * Ui.S, Ui.H * Ui.S, C.Hex("#16202a"), C.Bg, false);
        float off = Ui.BeginScroll("debrief", new RectangleF(0, 0, Ui.W, Ui.H), _h);
        float x = 30, y = 20 + off, w = Ui.W - 60;
        if (r.Failed) Ui.Label("MISSION FAILED", x, y, 46, C.Red, FontKind.Bold);
        else Ui.Label(r.Rank, x, y - 16, 110, C.Accent, FontKind.Bold);
        float hx = x + (r.Failed ? 420 : 120);
        Ui.Header(_cfg.Name, hx, y + 8, 18);
        Ui.Label($"{MathUtil.Time(r.Time)} · {r.Arrested} arrested · {r.Killed} killed · {r.Escaped} escaped · {r.HostSaved} hostages rescued", hx, y + 36, 12, C.Dim);
        if (r.Reason != null) Ui.Label(r.Reason, hx, y + 56, 12, C.Red);
        y += 110;
        bool two = w > 900;
        float cw = two ? (w - 14) / 2 : w;
        // score
        float sh = 40 + r.Score.Count * 36;
        Ui.Panel(new RectangleF(x, y, cw, sh));
        Ui.Label($"EVALUATION — {r.Total} / 100", x + 12, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float sy = y + 36;
        foreach (var s in r.Score)
        {
            Ui.Label(s.Name, x + 12, sy, 12);
            float bw = cw - 260;
            Ui.Bar(new RectangleF(x + 180, sy + 3, bw, 8), Math.Max(0, (float)s.Pts / s.Max), s.Pts >= s.Max * 0.8 ? C.Green : s.Pts > 0 ? C.Accent : C.Red);
            Ui.Label(s.Detail, x + 180, sy + 14, 9, C.Dim);
            Ui.Label($"{s.Pts}/{s.Max}", x + cw - 12, sy, 12, C.Text, FontKind.Mono, Align.Right);
            sy += 36;
        }
        // finances
        float fy = y + sh + 10;
        float fh = 50 + _sum.Lines.Count * 20 + 30;
        Ui.Panel(new RectangleF(x, fy, cw, fh));
        Ui.Label("FINANCES", x + 12, fy + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float ly = fy + 36;
        foreach (var (label, amount) in _sum.Lines)
        {
            Ui.Label(label, x + 12, ly, 11);
            if (amount != 0) Ui.Label(MathUtil.Money(amount), x + cw - 12, ly, 11, amount < 0 ? C.Red : C.Accent, FontKind.Mono, Align.Right);
            ly += 20;
        }
        Ui.Line(x + 12, ly + 2, x + cw - 12, ly + 2, C.Line);
        Ui.Label("Net", x + 12, ly + 8, 13, C.Text, FontKind.Bold);
        Ui.Label(MathUtil.Money(_sum.Net), x + cw - 12, ly + 8, 13, C.Accent, FontKind.Mono, Align.Right);
        float leftBottom = fy + fh;
        // operators
        float rx = two ? x + cw + 14 : x, ry = two ? y : leftBottom + 10;
        float oh = 40 + _sum.OpLines.Count * 56 + _sum.Deaths.Count * 22;
        Ui.Panel(new RectangleF(rx, ry, cw, oh));
        Ui.Label("OPERATORS", rx + 12, ry + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float oy = ry + 36;
        foreach (var l in _sum.OpLines)
        {
            var tex = G.Art.Portrait(l.Op, (int)(40 * Ui.S), (int)(48 * Ui.S));
            P.Image(tex, (rx + 12) * Ui.S, oy * Ui.S, 40 * Ui.S, 48 * Ui.S);
            Ui.Label(l.Op.Callsign, rx + 60, oy + 4, 13, C.Text, FontKind.Bold);
            Ui.Label($"+{l.Xp} XP{(l.Leveled ? $" — LEVEL UP ({l.NewLevel})!" : "")}", rx + 66 + Ui.TextW(l.Op.Callsign, 13, FontKind.Bold), oy + 6, 11, l.Leveled ? C.Accent : C.Dim);
            Ui.Label($"{l.Kills} kills · {l.Arrests} arrests · {l.Rescues} rescues", rx + 60, oy + 24, 11, C.Dim);
            if (!string.IsNullOrEmpty(l.InjuryText)) Ui.Label(l.InjuryText, rx + cw - 12, oy + 24, 11, C.Red, FontKind.Regular, Align.Right);
            oy += 56;
        }
        foreach (var d in _sum.Deaths) { Ui.Label($"✝ {d.Name} \"{d.Callsign}\" — killed in action. {d.Missions} operations served.", rx + 12, oy, 12, C.Red, FontKind.Bold); oy += 22; }
        // evidence
        float ey = ry + oh + 10;
        var evLines = r.Evidence.Select(e => "▸ " + e.name).Concat(r.Contraband.Select(c => $"▸ {c.name} ({MathUtil.Money(c.value)})")).ToList();
        float eh = 50 + Math.Max(1, evLines.Count) * 18 + (r.HvtName != null ? 22 : 0) + _sum.Unlocks.Count * 22;
        Ui.Panel(new RectangleF(rx, ey, cw, eh));
        Ui.Label("EVIDENCE & SEIZURES", rx + 12, ey + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float ely = ey + 36;
        if (evLines.Count == 0) { Ui.Label("No evidence recovered.", rx + 12, ely, 11, C.Dim); ely += 18; }
        foreach (var l in evLines) { Ui.Label(l, rx + 12, ely, 11); ely += 18; }
        if (r.HvtName != null) { Ui.Label($"HVT {r.HvtName}: {(r.HvtCaptured == true ? "CAPTURED" : "NOT CAPTURED")}", rx + 12, ely + 4, 12, r.HvtCaptured == true ? C.Green : C.Red, FontKind.Bold); ely += 22; }
        foreach (var u in _sum.Unlocks) { Ui.Wrap("★ " + u, rx + 12, ely + 4, cw - 24, 11, C.Green); ely += 22; }
        // timeline
        float ty = Math.Max(leftBottom, ey + eh) + 10;
        float th = 40 + Math.Min(_m.Log.Count, 60) * 16;
        Ui.Panel(new RectangleF(x, ty, w, th));
        Ui.Label("MISSION TIMELINE", x + 12, ty + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float tly = ty + 34;
        foreach (var l in _m.Log.TakeLast(60)) { Ui.Label($"{MathUtil.Time(l.T)}  {l.Text}", x + 12, tly, 10, C.Hex(l.Color), FontKind.Mono); tly += 16; }
        float by = ty + th + 14;
        if (Ui.Button(new RectangleF(x, by, 240, 42), "▶ Watch Replay / Body Cams")) G.Switch(new ReplayScreen(G, _m, this));
        if (Ui.Button(new RectangleF(x + 250, by, 180, 42), "Return to HQ", BtnStyle.Primary, true, 14)) G.Switch(new HqScreen(G));
        Ui.EndScroll();
        _h = by - off + 70;
    }
}
