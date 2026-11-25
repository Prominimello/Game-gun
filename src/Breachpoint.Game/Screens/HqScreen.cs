using Breachpoint.Core.Data;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using Microsoft.Xna.Framework;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

/// <summary>Headquarters: Command Center, Barracks, Armory, Training, Medical Bay, Garage, Evidence Room.</summary>
public sealed partial class HqScreen : Screen
{
    public static readonly (string id, string name, string sub)[] Tabs =
    {
        ("command", "Command Center", "Missions & intel"), ("barracks", "Barracks", "Operators"), ("armory", "Armory", "Gunsmith & gear"),
        ("training", "Training", "Skills & drills"), ("medical", "Medical Bay", "Injuries & rest"), ("garage", "Garage", "Vehicles"), ("evidence", "Evidence Room", "Case files"),
    };
    public string Tab = "command";
    private readonly Dictionary<string, float> _contentH = new();
    private Profile Pf => G.Profile;

    public HqScreen(BreachpointGame g, string tab = null) : base(g) { if (tab != null) Tab = tab; }

    public override void Enter()
    {
        G.Audio.Ambient("hq");
        G.Audio.MusicTrack("hq");
    }

    public override bool Back() { G.SaveProfile(); G.Switch(new TitleScreen(G)); return true; }

    private void Changed() => G.SaveProfile();

    public override void Draw()
    {
        P.Begin();
        Ui.Fill(new RectangleF(0, 0, Ui.W, Ui.H), C.Bg);
        // top bar
        var top = new RectangleF(0, 0, Ui.W, 44);
        Ui.Fill(top, C.Hex("#0f141a")); Ui.Line(0, 44, Ui.W, 44, C.Line);
        Ui.Label("BREACHPOINT", 14, 13, 15, C.Hex("#e9eef2"), FontKind.Bold);
        Ui.Label("HQ", 14 + Ui.TextW("BREACHPOINT", 15, FontKind.Bold) + 6, 13, 15, C.Accent, FontKind.Bold);
        float sx = 190;
        void Stat(string k, string v, Color? vc = null)
        {
            Ui.Label(k, sx, 15, 12, C.Dim, FontKind.Mono); sx += Ui.TextW(k, 12, FontKind.Mono) + 5;
            Ui.Label(v, sx, 15, 12, vc ?? C.Accent, FontKind.Mono); sx += Ui.TextW(v, 12, FontKind.Mono) + 18;
        }
        Stat("Funds", MathUtil.Money(Pf.Money)); Stat("Day", Pf.Day.ToString()); Stat("Tier", Pf.Tier().ToString()); Stat("Roster", Pf.Operators.Count.ToString());
        Stat(Pf.Diff().Name, Pf.Ironman ? "IRONMAN" : "", C.Red);
        if (Ui.Button(new RectangleF(Ui.W - 220, 6, 100, 32), "Settings", BtnStyle.Ghost)) Dialogs.Settings(G);
        if (Ui.Button(new RectangleF(Ui.W - 112, 6, 104, 32), "Main Menu", BtnStyle.Ghost)) { G.SaveProfile(); G.Switch(new TitleScreen(G)); }
        // nav
        float navW = 176;
        Ui.Fill(new RectangleF(0, 45, navW, Ui.H - 45), C.Hex("#0e1318"));
        float ny = 54;
        foreach (var (id, name, sub) in Tabs)
        {
            var r = new RectangleF(0, ny, navW, 50);
            bool on = Tab == id;
            if (on) { Ui.Fill(r, C.Hex("#16202a")); Ui.Fill(new RectangleF(0, ny, 3, 50), C.Accent); }
            else if (Ui.Hover(r)) Ui.Fill(r, C.Hex("#131a22"));
            Ui.Label(name.ToUpperInvariant(), 16, ny + 9, 12, on ? C.Accent : C.Text, FontKind.Bold);
            Ui.Label(sub, 16, ny + 28, 10, C.Dim);
            if (Ui.Clicked(r)) { Tab = id; G.Audio.Ui(); Ui.ResetScroll("hq"); }
            ny += 52;
        }
        // content
        var area = new RectangleF(navW + 1, 45, Ui.W - navW - 1, Ui.H - 45);
        float ch = _contentH.GetValueOrDefault(Tab, area.Height);
        float off = Ui.BeginScroll("hq", area, ch);
        float x0 = area.X + 14, y0 = area.Y + 12 + off, w = area.Width - 28;
        float used = Tab switch
        {
            "command" => Command(x0, y0, w),
            "barracks" => Barracks(x0, y0, w),
            "armory" => Armory(x0, y0, w),
            "training" => Training(x0, y0, w),
            "medical" => Medical(x0, y0, w),
            "garage" => Garage(x0, y0, w),
            _ => Evidence(x0, y0, w),
        };
        Ui.EndScroll();
        _contentH[Tab] = used + 30;
    }

    // ---------------- shared helpers ----------------
    private float Card(RectangleF r, bool selected = false, Color? topColor = null, bool locked = false)
    {
        Ui.Fill(r, locked ? C.Panel2 * 0.5f : C.Panel2);
        Ui.Border(r, selected ? C.Accent : C.Line);
        if (topColor.HasValue) Ui.Fill(new RectangleF(r.X, r.Y, r.Width, 3), topColor.Value);
        return r.Y;
    }

    private void Portrait(Operator op, float x, float y, float w, float h)
    {
        var tex = G.Art.Portrait(op, (int)(w * Ui.S), (int)(h * Ui.S));
        P.Image(tex, x * Ui.S, y * Ui.S, w * Ui.S, h * Ui.S);
        Ui.Border(new RectangleF(x, y, w, h), C.Hex("#222c36"));
    }

    private void StatusTag(Operator op, float x, float y)
    {
        if (op.Injury > 0) Tag($"INJURED {op.Injury}d", x, y, C.Red);
        else if (op.Fatigue > 60) Tag("FATIGUED", x, y, C.Accent);
        else Tag("READY", x, y, C.Green);
    }

    private float Tag(string text, float x, float y, Color c, float size = 10)
    {
        float w = Ui.TextW(text, size) + 12;
        Ui.Border(new RectangleF(x, y, w, size + 8), c);
        Ui.Label(text, x + 6, y + 3, size, c);
        return w;
    }

    // ================= Command Center =================
    private float Command(float x, float y, float w)
    {
        float y0 = y;
        Ui.Header("Campaign — Criminal Organizations", x, y); y += 28;
        int cols = w > 900 ? 4 : 2;
        float cw = (w - (cols - 1) * 10) / cols;
        float colY = y, maxY = y;
        for (int oi = 0; oi < CampaignData.Orgs.Count; oi++)
        {
            var org = CampaignData.Orgs[oi];
            float cx = x + (oi % cols) * (cw + 10);
            if (oi % cols == 0 && oi > 0) colY = maxY + 12;
            float cy = colY;
            var tk = CampaignData.Campaign.First(m => m.Org == org.Id && m.Takedown);
            int intel = Pf.Campaign.Intel.GetValueOrDefault(org.Id);
            var head = new RectangleF(cx, cy, cw, 96);
            Card(head, false, C.Hex(org.Color));
            Ui.Label(org.Name, cx + 10, cy + 10, 13, C.Text, FontKind.Bold);
            Ui.Wrap(org.Desc, cx + 10, cy + 30, cw - 20, 10, C.Dim);
            Ui.Label($"Intel: {intel}/{tk.IntelReq}", cx + 10, cy + 76, 11, intel >= tk.IntelReq ? C.Green : C.Accent);
            if (Pf.Campaign.HvtCaptured.GetValueOrDefault(org.Id)) Tag("BOSS IN CUSTODY", cx + cw - 120, cy + 74, C.Green, 9);
            cy += 104;
            foreach (var m in CampaignData.Campaign.Where(m => m.Org == org.Id))
            {
                bool avail = Pf.CampaignAvailable(m);
                Pf.Campaign.Done.TryGetValue(m.Id, out var done);
                var r = new RectangleF(cx, cy, cw, 112);
                Card(r, false, null, !avail);
                if (done != null) Ui.Fill(new RectangleF(cx, cy, 3, r.Height), C.Green);
                var tc = avail ? C.Text : C.Dim;
                Ui.Label($"{m.Idx + 1}. {m.Name}".ToUpperInvariant(), cx + 10, cy + 8, 12, tc, FontKind.Bold);
                Ui.Label(CampaignData.MissionTypes[m.Type].Name + (m.Night ? " · Night" : "") + (m.Takedown ? " · TAKEDOWN" : ""), cx + 10, cy + 27, 10, C.Dim);
                string blurb = avail ? m.Intel : "Locked — complete the previous operation.";
                if (blurb.Length > 110) blurb = blurb[..108] + "…";
                Ui.Wrap(blurb, cx + 10, cy + 44, cw - 20, 10, avail ? C.Text * 0.85f : C.Dim);
                Ui.Label(MathUtil.Money(m.Reward), cx + 10, cy + 92, 12, avail ? C.Accent : C.Dim, FontKind.Mono);
                if (done != null) Ui.Label(done, r.Right - 12, cy + 6, 22, C.Accent, FontKind.Bold, Align.Right);
                if (avail && Ui.Clicked(r)) { G.Audio.Ui(); G.Switch(new BriefScreen(G, MissionFactory.FromCampaign(Pf, m))); }
                cy += 118;
            }
            maxY = Math.Max(maxY, cy);
        }
        y = maxY + 6;
        if (Pf.Campaign.Complete)
        {
            var r = new RectangleF(x, y, w, 60); Ui.Panel(r, C.Panel, C.Green);
            Ui.Label("CAMPAIGN COMPLETE", x + 12, y + 10, 15, C.Green, FontKind.Bold);
            Ui.Label("The Meridian Network is dismantled. Generated operations, HVT contracts and weekly challenges continue below.", x + 12, y + 34, 11);
            y += 70;
        }
        Ui.Header("Operations Board", x, y + 8); y += 34;
        Ui.Label("Standalone operations with randomized layouts and placements. Refreshes daily.", x, y, 11, C.Dim); y += 22;
        var board = MissionFactory.OpsBoard(Pf).ToList();
        board.Add(MissionFactory.Weekly(Pf));
        int oc = w > 900 ? 4 : 2;
        float ow = (w - (oc - 1) * 10) / oc;
        for (int i = 0; i < board.Count; i++)
        {
            var m = board[i];
            float cx = x + (i % oc) * (ow + 10), cy = y + (i / oc) * 122;
            bool weekly = m.Weekly.HasValue, doneW = weekly && Pf.WeeklyDone == m.Weekly;
            var r = new RectangleF(cx, cy, ow, 114);
            Card(r, false, weekly ? C.Hex("#a45bc8") : null, doneW);
            Ui.Label((m.Name + (weekly ? " ★" : "")).ToUpperInvariant(), cx + 10, cy + 10, 12, C.Text, FontKind.Bold);
            Ui.Label($"{CampaignData.MissionTypes[m.Type].Name} · Tier {m.Tier}{(m.Night ? " · Night" : "")}{(m.Rain ? " · Rain" : "")}", cx + 10, cy + 30, 10, C.Dim);
            Ui.Wrap($"{m.OrgName}. ~{m.Enemies} suspects{(m.Hostages > 0 ? $", {m.Hostages} hostages" : "")}{(m.Hvt ? ", HVT present" : "")}.", cx + 10, cy + 48, ow - 20, 10);
            Ui.Label(doneW ? "COMPLETED THIS WEEK" : MathUtil.Money(m.Reward), cx + 10, cy + 92, 12, C.Accent, FontKind.Mono);
            if (!doneW && Ui.Clicked(r)) { G.Audio.Ui(); G.Switch(new BriefScreen(G, m)); }
        }
        y += ((board.Count + oc - 1) / oc) * 122 + 6;
        float bx = x;
        if (Ui.Button(new RectangleF(bx, y, 250, 38), "Sandbox — Build Custom Mission")) SandboxDialog();
        bx += 260;
        if (Ui.Button(new RectangleF(bx, y, 180, 38), "Kill House Exercise")) G.Switch(new BriefScreen(G, MissionFactory.Training()));
        bx += 190;
        if (Ui.Button(new RectangleF(bx, y, 220, 38), "Stand Down (advance 1 day)")) { Pf.AdvanceDay(1); Pf.OpsBoard = null; G.Toast("A day passes. Operators rest."); Changed(); }
        y += 50;
        return y - y0;
    }

    private void SandboxDialog()
    {
        var types = CampaignData.MissionTypes.Keys.ToList();
        int type = types.IndexOf("warehouse"), floors = 2, enemies = 6, tier = 2, hostages = 1, civs = 1, ev = 2, con = 1, diff = Array.IndexOf(CampaignData.DifficultyOrder, Pf.Difficulty);
        bool hvt = false, night = false, rain = false;
        int seed = Random.Shared.Next(1, 99999);
        G.Modal(() =>
        {
            var r = Dialogs.Box(Ui, 640, 600, "Sandbox Mission Builder");
            float y = r.Y + 56, lx = r.X + 20, cx = r.X + 230, cw = 300;
            void Row(string label, string value, Action<int> change)
            {
                Ui.Label(label, lx, y + 7, 13, C.Dim);
                int d = Ui.Cycler(new RectangleF(cx, y, cw, 30), value);
                if (d != 0) change(d);
                y += 38;
            }
            Row("Scenario / map", CampaignData.MissionTypes[types[type]].Name, d => type = Gui.Wrap(type + d, types.Count));
            Row("Floors (houses/hotel)", floors.ToString(), d => floors = Math.Clamp(floors + d, 1, 3));
            Row("Enemies", enemies.ToString(), d => enemies = Math.Clamp(enemies + d, 1, 24));
            Row("Enemy gear tier", $"Tier {tier} — {new[] { "", "Street", "Organized", "Professional", "Elite" }[tier]}", d => tier = Math.Clamp(tier + d, 1, 4));
            Row("Hostages", hostages.ToString(), d => hostages = Math.Clamp(hostages + d, 0, 8));
            Row("Civilians", civs.ToString(), d => civs = Math.Clamp(civs + d, 0, 8));
            Row("Evidence", ev.ToString(), d => ev = Math.Clamp(ev + d, 0, 8));
            Row("Contraband", con.ToString(), d => con = Math.Clamp(con + d, 0, 8));
            Row("Difficulty", CampaignData.Difficulty[CampaignData.DifficultyOrder[diff]].Name, d => diff = Gui.Wrap(diff + d, 4));
            hvt = Ui.Toggle(new RectangleF(lx, y, 180, 28), "High-value target", hvt);
            night = Ui.Toggle(new RectangleF(lx + 190, y, 120, 28), "Night", night);
            rain = Ui.Toggle(new RectangleF(lx + 300, y, 120, 28), "Rain", rain);
            y += 40;
            Ui.Label($"Seed {seed}", lx, y + 6, 12, C.Dim);
            if (Ui.Button(new RectangleF(lx + 110, y, 100, 30), "Reroll")) seed = Random.Shared.Next(1, 99999);
            if (Ui.Button(new RectangleF(r.X + 20, r.Bottom - 54, 220, 38), "Continue to Briefing", BtnStyle.Primary))
            {
                var m = MissionFactory.Sandbox(types[type], floors, enemies, tier, hostages, civs, ev, con, hvt, night, rain, CampaignData.DifficultyOrder[diff], seed);
                G.Switch(new BriefScreen(G, m));
                return false;
            }
            return !Ui.Button(new RectangleF(r.X + 250, r.Bottom - 54, 110, 38), "Cancel");
        });
    }
}
