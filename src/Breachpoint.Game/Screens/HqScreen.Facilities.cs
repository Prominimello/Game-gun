using Breachpoint.Core.Data;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

public sealed partial class HqScreen
{
    private static readonly (string stat, string name)[] Courses =
    {
        ("acc", "Marksmanship"), ("awa", "Threat Recog."), ("mob", "Agility"), ("end", "Endurance PT"), ("dis", "ROE & Discipline"), ("med", "Tactical Medicine"), ("tec", "Breaching & Tech"), ("lea", "Leadership"),
    };

    private float Training(float x, float y, float w)
    {
        int lvl = Pf.TrainingLevel;
        Ui.Header($"Training Facility — Level {lvl}", x, y); y += 30;
        Ui.Label("Each course costs money, adds fatigue and raises one attribute. Injured or exhausted operators can't train.", x, y, 11, C.Dim); y += 24;
        float nameW = 120, cellW = (w - nameW - 70) / Courses.Length;
        Ui.Fill(new RectangleF(x, y, w, 28), C.Panel);
        for (int i = 0; i < Courses.Length; i++) Ui.Label(Courses[i].name, x + nameW + i * cellW + cellW / 2, y + 8, 10, C.Dim, FontKind.Regular, Align.Center);
        Ui.Label("Fatigue", x + w - 35, y + 8, 10, C.Dim, FontKind.Regular, Align.Center);
        y += 30;
        foreach (var op in Pf.Operators)
        {
            Ui.Fill(new RectangleF(x, y, w, 52), C.Panel2);
            Ui.Label(op.Callsign, x + 8, y + 8, 12, C.Text, FontKind.Bold);
            Ui.Label(GameData.Role[op.Role].Name, x + 8, y + 28, 10, C.Dim);
            for (int i = 0; i < Courses.Length; i++)
            {
                var (stat, cname) = Courses[i];
                int v = op.Stat(stat);
                int cost = (int)Math.Round((800 + v * 25) / (1 + (lvl - 1) * 0.2) / 50) * 50;
                bool can = op.Injury <= 0 && op.Fatigue < 80 && v < 95;
                float cx = x + nameW + i * cellW;
                Ui.Label(v.ToString(), cx + cellW / 2, y + 5, 12, C.Text, FontKind.Mono, Align.Center);
                if (Ui.Button(new RectangleF(cx + 3, y + 24, cellW - 6, 24), MathUtil.Money(cost), BtnStyle.Normal, can, 10) && G.Spend(cost))
                {
                    int gain = 2 + lvl + Random.Shared.Next(3);
                    op.Stats[stat] = Math.Min(99, v + gain); op.Fatigue += 20; ProfileService.AddXp(op, 40);
                    G.Toast($"{op.Callsign}: {GameData.Stats.First(s => s.Id == stat).Name} +{gain}"); Changed();
                }
            }
            Ui.Label($"{op.Fatigue:0}", x + w - 35, y + 18, 12, op.Fatigue > 60 ? C.Accent : C.Text, FontKind.Mono, Align.Center);
            y += 56;
        }
        y += 10;
        float hw = (w - 10) / 2;
        Ui.Panel(new RectangleF(x, y, hw, 150));
        Ui.Label("KILL HOUSE EXERCISE", x + 12, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        Ui.Wrap("Live squad drill with training rounds against paper targets and no-shoot targets. Builds room-clearing coordination and experience. No injuries.", x + 12, y + 34, hw - 24, 11, C.Dim);
        if (Ui.Button(new RectangleF(x + 12, y + 100, 180, 36), "Start Exercise", BtnStyle.Primary)) G.Switch(new BriefScreen(G, MissionFactory.Training()));
        Ui.Panel(new RectangleF(x + hw + 10, y, hw, 150));
        Ui.Label("FACILITY UPGRADE", x + hw + 22, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        Ui.Wrap("Higher levels make courses cheaper and more effective.", x + hw + 22, y + 34, hw - 24, 11, C.Dim);
        if (lvl < 3) { if (Ui.Button(new RectangleF(x + hw + 22, y + 100, 280, 36), $"Upgrade to Level {lvl + 1} — {MathUtil.Money(lvl * 20000)}") && G.Spend(lvl * 20000, "Training facility upgraded")) { Pf.TrainingLevel++; Changed(); } }
        else Ui.Label("Maximum level", x + hw + 22, y + 108, 13, C.Green);
        return y + 160;
    }

    private float Medical(float x, float y, float w)
    {
        int lvl = Pf.MedicalLevel;
        Ui.Header($"Medical Bay — Level {lvl}", x, y); y += 32;
        foreach (var op in Pf.Operators)
        {
            Ui.Fill(new RectangleF(x, y, w, 60), C.Panel2); Ui.Border(new RectangleF(x, y, w, 60), C.Line);
            Portrait(op, x + 6, y + 6, 40, 48);
            Ui.Label($"{op.Callsign}", x + 56, y + 8, 13, C.Text, FontKind.Bold);
            Ui.Label(op.Name, x + 56, y + 28, 10, C.Dim);
            StatusTag(op, x + 220, y + 8);
            Ui.Label(op.Injury > 0 ? $"{op.InjuryDesc} — {op.Injury} days" : "No injuries", x + 220, y + 34, 11, op.Injury > 0 ? C.Red : C.Dim);
            Ui.Label("Fatigue", x + 520, y + 10, 10, C.Dim);
            Ui.Bar(new RectangleF(x + 520, y + 28, 120, 8), (float)op.Fatigue / 100, C.Accent);
            Ui.Label($"Morale {op.Morale:0}", x + 660, y + 22, 11);
            int exp = (int)Math.Round(1500 / (1 + (lvl - 1) * 0.25));
            if (op.Injury > 0)
            {
                if (Ui.Button(new RectangleF(x + w - 230, y + 14, 218, 32), $"Expedite 1 day — {MathUtil.Money(exp)}", BtnStyle.Normal, true, 11) && G.Spend(exp, "Treatment expedited"))
                { op.Injury--; if (op.Injury <= 0) { op.Injury = 0; op.Status = "active"; op.InjuryDesc = ""; } Changed(); }
            }
            else if (op.Fatigue > 0 && Ui.Button(new RectangleF(x + w - 230, y + 14, 218, 32), "Physio −30 fatigue ($400)", BtnStyle.Normal, true, 11) && G.Spend(400, "Physio session"))
            { op.Fatigue = Math.Max(0, op.Fatigue - 30); Changed(); }
            y += 66;
        }
        y += 8;
        float hw = (w - 10) / 2;
        Ui.Panel(new RectangleF(x, y, hw, 120));
        Ui.Label("REST", x + 12, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        Ui.Wrap("Advance one day: injuries heal by a day, fatigue drops, morale recovers.", x + 12, y + 34, hw - 24, 11, C.Dim);
        if (Ui.Button(new RectangleF(x + 12, y + 74, 160, 34), "Rest one day")) { Pf.AdvanceDay(1); Pf.OpsBoard = null; Changed(); }
        Ui.Panel(new RectangleF(x + hw + 10, y, hw, 120));
        Ui.Label("UPGRADE", x + hw + 22, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        if (lvl < 3) { if (Ui.Button(new RectangleF(x + hw + 22, y + 74, 280, 34), $"Upgrade to Level {lvl + 1} — {MathUtil.Money(lvl * 18000)}") && G.Spend(lvl * 18000, "Medical bay upgraded")) { Pf.MedicalLevel++; Changed(); } }
        else Ui.Label("Maximum level", x + hw + 22, y + 80, 13, C.Green);
        y += 130;
        if (Pf.Fallen.Count > 0)
        {
            Ui.Panel(new RectangleF(x, y, w, 44 + Pf.Fallen.Count * 58));
            Ui.Label("MEMORIAL WALL", x + 12, y + 12, 12, C.Hex("#9fb0bf"), FontKind.Bold);
            float my = y + 40;
            foreach (var f in Pf.Fallen)
            {
                Portrait(f, x + 12, my, 40, 48);
                Ui.Label($"{f.Name} \"{f.Callsign}\"", x + 62, my + 6, 13, C.Text, FontKind.Bold);
                Ui.Label($"{f.Missions} operations · fell on day {f.DiedDay} — {f.DiedMission}", x + 62, my + 26, 11, C.Dim);
                my += 58;
            }
            y = my + 10;
        }
        return y;
    }

    private float Garage(float x, float y, float w)
    {
        Ui.Header("Garage", x, y); y += 28;
        Ui.Label("The active vehicle sets how many operators you can deploy.", x, y, 11, C.Dim); y += 24;
        float cw = (w - 10) / 2;
        for (int i = 0; i < Pf.Vehicles.Count; i++)
        {
            var v = Pf.Vehicles[i]; var def = GameData.Vehicle[v.Id];
            float cx = x + (i % 2) * (cw + 10), cy = y + (i / 2) * 290;
            bool active = Pf.ActiveVehicle == v.Uid;
            Ui.Panel(new RectangleF(cx, cy, cw, 280), C.Panel, active ? C.Accent : C.Line);
            Ui.Label(def.Name, cx + 12, cy + 10, 14, C.Text, FontKind.Bold);
            Tag(active ? "ACTIVE" : $"{def.Seats} seats", cx + cw - 90, cy + 10, active ? C.Accent : C.Dim);
            var tex = G.Art.Vehicle(def, v);
            float s = Math.Min((cw - 40) / tex.Width, 90f / tex.Height);
            Ui.Fill(new RectangleF(cx + 12, cy + 36, cw - 24, 100), C.Hex("#2c2e31"));
            P.Sprite(tex, (cx + cw / 2) * Ui.S, (cy + 86) * Ui.S, 0, s * Ui.S);
            float fy = cy + 146;
            Ui.Label($"Seats {def.Seats}{(def.Armored ? " · armored" : "")}", cx + 12, fy, 11, C.Dim);
            Ui.Label("Paint", cx + 12, fy + 24, 11, C.Dim);
            int pi = Ui.Swatches(new RectangleF(cx + 90, fy + 22, cw - 110, 16), Palette.Concat(new[] { "#e7e9ec", "#20252b" }).ToArray(), Array.IndexOf(Palette.Concat(new[] { "#e7e9ec", "#20252b" }).ToArray(), v.Paint));
            var pal = Palette.Concat(new[] { "#e7e9ec", "#20252b" }).ToArray();
            if (pi >= 0 && pal[pi] != v.Paint) { v.Paint = pal[pi]; Changed(); }
            Ui.Label("Markings", cx + 12, fy + 54, 11, C.Dim);
            Ui.TextField("mk_" + v.Uid, new RectangleF(cx + 90, fy + 48, 150, 28), v.Markings, t => { v.Markings = t; Changed(); }, 12, 10, "Unit markings");
            Ui.Label("Lights", cx + 260, fy + 54, 11, C.Dim);
            string[] lights = { "none", "hidden", "full" }; string[] lnames = { "None", "Hidden grille", "Full light bar" };
            int li = Array.IndexOf(lights, v.Lights);
            int d = Ui.Cycler(new RectangleF(cx + 306, fy + 48, cw - 318, 28), lnames[Math.Max(0, li)], true, 11);
            if (d != 0) { v.Lights = lights[Gui.Wrap(li + d, 3)]; Changed(); }
            bool pb = Ui.Toggle(new RectangleF(cx + 12, fy + 86, 130, 26), "Push bar", v.Accessories.Contains("pushbar"));
            bool ld = Ui.Toggle(new RectangleF(cx + 150, fy + 86, 130, 26), "Ladder rack", v.Accessories.Contains("ladder"));
            if (pb != v.Accessories.Contains("pushbar")) { if (pb) v.Accessories.Add("pushbar"); else v.Accessories.Remove("pushbar"); Changed(); }
            if (ld != v.Accessories.Contains("ladder")) { if (ld) v.Accessories.Add("ladder"); else v.Accessories.Remove("ladder"); Changed(); }
            if (!active && Ui.Button(new RectangleF(cx + cw - 200, fy + 84, 188, 30), "Set as deployment vehicle", BtnStyle.Normal, true, 11)) { Pf.ActiveVehicle = v.Uid; Changed(); }
        }
        y += ((Pf.Vehicles.Count + 1) / 2) * 290 + 6;
        var dealer = GameData.Vehicles.Where(d => !Pf.Vehicles.Any(v => v.Id == d.Id)).ToList();
        if (dealer.Count > 0)
        {
            Ui.Panel(new RectangleF(x, y, w, 44 + dealer.Count * 44));
            Ui.Label("VEHICLE DEALER", x + 12, y + 12, 12, C.Hex("#9fb0bf"), FontKind.Bold);
            float dy = y + 40;
            foreach (var def in dealer)
            {
                Ui.Label(def.Name, x + 12, dy + 8, 13, C.Text, FontKind.Bold);
                Ui.Label($"{def.Seats} seats{(def.Armored ? ", armored" : "")}", x + 260, dy + 9, 11, C.Dim);
                if (Ui.Button(new RectangleF(x + w - 160, dy + 2, 146, 32), MathUtil.Money(def.Price), BtnStyle.Primary, true, 12) && G.Spend(def.Price, $"{def.Name} purchased"))
                { Pf.Vehicles.Add(new VehicleInstance { Id = def.Id, Uid = MathUtil.Uid("v"), Paint = def.Color }); Changed(); }
                dy += 44;
            }
            y = dy + 10;
        }
        return y;
    }

    private float Evidence(float x, float y, float w)
    {
        Ui.Header("Evidence Room — Case Files", x, y); y += 32;
        float cw = (w - 10) / 2;
        float rowMax = y;
        for (int i = 0; i < CampaignData.Orgs.Count; i++)
        {
            var org = CampaignData.Orgs[i];
            float cx = x + (i % 2) * (cw + 10);
            if (i % 2 == 0 && i > 0) y = rowMax + 10;
            float cy = y;
            var items = Pf.Evidence.Where(e => e.Org == org.Id).Reverse().ToList();
            var tk = CampaignData.Campaign.First(m => m.Org == org.Id && m.Takedown);
            int intel = Pf.Campaign.Intel.GetValueOrDefault(org.Id);
            float h = 170 + Math.Min(8, Math.Max(1, items.Count)) * 18;
            Ui.Panel(new RectangleF(cx, cy, cw, h));
            Ui.Label(org.Name.ToUpperInvariant(), cx + 12, cy + 10, 12, C.Hex(org.Color), FontKind.Bold);
            Tag($"INTEL {intel}/{tk.IntelReq}", cx + cw - 100, cy + 8, C.Dim);
            Ui.Bar(new RectangleF(cx + 12, cy + 34, cw - 24, 6), Math.Min(1, intel / (float)tk.IntelReq), C.Hex(org.Color));
            var reveals = new (int n, string t)[] { (1, "Network size estimated. Front businesses identified."), (2, "Lieutenants identified by name."), (3, $"Principal: {org.Boss}. Known locations mapped."), (tk.IntelReq, "Takedown warrant approved — full floor plans and guard rotations.") };
            float ry = cy + 48;
            foreach (var (n, t) in reveals) { Ui.Label(intel >= n ? "▸ " + t : $"▸ [REDACTED — need {n} intel]", cx + 12, ry, 11, intel >= n ? C.Text : C.Hex("#4a5560")); ry += 20; }
            ry += 6;
            if (items.Count == 0) Ui.Label("No evidence collected yet.", cx + 12, ry, 10, C.Dim);
            foreach (var e in items.Take(8)) { Ui.Label($"Day {e.Day} · {e.Mission}: {e.Name}", cx + 12, ry, 10, C.Dim); ry += 18; }
            rowMax = Math.Max(rowMax, cy + h);
        }
        y = rowMax + 10;
        var other = Pf.Evidence.Count(e => e.Org == null);
        Ui.Panel(new RectangleF(x, y, w, 60));
        Ui.Label("OTHER OPERATIONS", x + 12, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        Ui.Label($"{other} items logged from standalone operations. Total contraband seized: {MathUtil.Money(Pf.Stats.Seized)} street value.", x + 12, y + 34, 11, C.Dim);
        return y + 70;
    }
}
