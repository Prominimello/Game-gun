using Breachpoint.Core.Data;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

public sealed partial class HqScreen
{
    private Operator _selOp;
    public static readonly string[] Palette =
    {
        "#111111", "#1c2430", "#2a2d33", "#3a3f45", "#5a5f66", "#8a8f96", "#d8d8d0", "#4f5a3a", "#6b6650", "#7d7356", "#9a8361", "#5a3b3b",
        "#7a2a2a", "#2d3a55", "#365a7a", "#2f3e31",
    };

    private float Barracks(float x, float y, float w)
    {
        float y0 = y;
        if (_selOp == null || !Pf.Operators.Contains(_selOp)) _selOp = Pf.Operators.FirstOrDefault();
        float lw = Math.Min(300, w * 0.3f);
        Ui.Header("Roster", x, y);
        if (Ui.Button(new RectangleF(x + lw - 96, y - 4, 96, 28), "+ Recruit", BtnStyle.Normal, Pf.Operators.Count < 12, 12)) RecruitDialog();
        float ly = y + 30;
        foreach (var op in Pf.Operators)
        {
            var r = new RectangleF(x, ly, lw, 64);
            Card(r, op == _selOp);
            Portrait(op, x + 6, ly + 6, 44, 52);
            Ui.Label(op.Callsign, x + 58, ly + 7, 13, C.Text, FontKind.Bold);
            Ui.Label(op.Name, x + 64 + Ui.TextW(op.Callsign, 13, FontKind.Bold), ly + 9, 11, C.Dim);
            Ui.Label($"{GameData.Role[op.Role].Name} · Lv {op.Level}{(op.Points > 0 ? $" · {op.Points} pts" : "")}{(op.PerkPicks > 0 ? " · perk!" : "")}", x + 58, ly + 26, 10, C.Dim);
            StatusTag(op, x + 58, ly + 42);
            if (Ui.Clicked(r)) { _selOp = op; G.Audio.Ui(); }
            ly += 70;
        }
        if (Pf.Fallen.Count > 0) { Ui.Label("In memoriam: " + string.Join(", ", Pf.Fallen.Select(f => f.Callsign)), x, ly + 4, 10, C.Dim); ly += 20; }
        float rx = x + lw + 12, rw = w - lw - 12;
        float ry = _selOp != null ? OpDetail(_selOp, rx, y, rw) : y + 20;
        return Math.Max(ly, ry) - y0;
    }

    private float OpDetail(Operator op, float x, float y, float w)
    {
        float y0 = y;
        // header
        var hr = new RectangleF(x, y, w, 200);
        Ui.Panel(hr);
        Portrait(op, x + 10, y + 10, 150, 180);
        float tx = x + 172;
        Ui.TextField("cs_" + op.Id, new RectangleF(tx, y + 10, 170, 32), op.Callsign, v => { if (v.Length > 0) { op.Callsign = v; Changed(); } }, 16, 14, "Callsign");
        Ui.TextField("nm_" + op.Id, new RectangleF(tx + 178, y + 10, Math.Min(230, w - 360), 32), op.Name, v => { if (v.Length > 0) { op.Name = v; Changed(); } }, 13, 30, "Full name");
        StatusTag(op, x + w - 100, y + 16);
        Ui.Label("Role", tx, y + 56, 12, C.Dim);
        int ri = GameData.Roles.FindIndex(r => r.Id == op.Role);
        int d = Ui.Cycler(new RectangleF(tx + 40, y + 50, 200, 28), GameData.Role[op.Role].Name);
        if (d != 0) { op.Role = GameData.Roles[Gui.Wrap(ri + d, GameData.Roles.Count)].Id; Changed(); }
        var pers = GameData.Personality[op.Personality];
        float tw = Tag(pers.Name, tx + 250, y + 54, C.Accent);
        Ui.Label(pers.Desc, tx + 258 + tw, y + 56, 11, C.Dim);
        Ui.Wrap($"{op.Bio} Age {op.Age}.", tx, y + 88, w - 190, 11, C.Dim);
        int nextXp = ProfileService.XpFor(op.Level + 1), curXp = ProfileService.XpFor(op.Level);
        Ui.Label($"Level {op.Level}", tx, y + 114, 12);
        Ui.Bar(new RectangleF(tx + 64, y + 117, 200, 8), (float)(op.Xp - curXp) / Math.Max(1, nextXp - curXp), C.Accent2);
        Ui.Label($"{op.Xp}/{nextXp} XP", tx + 272, y + 114, 11, C.Dim);
        Ui.Label($"Morale {op.Morale:0} · Fatigue {op.Fatigue:0}", tx, y + 134, 11);
        Ui.Label($"Service: {op.Missions} ops · {op.Kills} kills · {op.Arrests} arrests · {op.Rescues} rescues · {(op.Shots > 0 ? op.Hits * 100 / op.Shots : 0)}% accuracy", tx, y + 152, 11, C.Dim);
        string partners = string.Join(", ", op.Bonds.OrderByDescending(b => b.Value).Take(3).Select(b => Pf.OpById(b.Key)).Where(o => o != null).Select(o => $"{o.Callsign} ({op.Bonds[o.Id]})"));
        Ui.Label("Trusted partners: " + (partners.Length > 0 ? partners : "—"), tx, y + 170, 11, C.Dim);
        if (Ui.Button(new RectangleF(x + w - 100, y + 160, 88, 30), "Dismiss", BtnStyle.Danger, Pf.Operators.Count > 1, 11))
            Dialogs.Confirm(G, "Dismiss operator?", $"{op.Callsign} will leave the unit permanently.", "Dismiss", () => { op.Loadout.Primary = op.Loadout.Secondary = null; Pf.Operators.Remove(op); _selOp = null; Changed(); });
        y += 210;
        bool twoCol = w > 760;
        float colW = twoCol ? (w - 10) / 2 : w;
        // attributes
        float ay = y;
        var ar = new RectangleF(x, ay, colW, 300);
        Ui.Panel(ar);
        Ui.Label(op.Points > 0 ? $"ATTRIBUTES — {op.Points} POINTS TO SPEND" : "ATTRIBUTES", x + 12, ay + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float sy = ay + 34;
        foreach (var s in GameData.Stats)
        {
            int v = op.Stat(s.Id);
            Ui.Label(s.Name, x + 12, sy, 12);
            Ui.Bar(new RectangleF(x + 110, sy + 3, colW - 190, 9), v / 100f, C.Accent2);
            Ui.Label(v.ToString(), x + colW - 72, sy, 12, C.Text, FontKind.Mono);
            if (op.Points > 0 && v < 99 && Ui.Button(new RectangleF(x + colW - 40, sy - 3, 28, 22), "+", BtnStyle.Normal, true, 12)) { op.Points--; op.Stats[s.Id] = v + 1; Changed(); }
            sy += 24;
        }
        Ui.Label("ABILITIES", x + 12, sy + 6, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        sy += 28;
        if (op.Perks.Count == 0) Ui.Label("None yet — unlock one every 3 levels.", x + 12, sy, 11, C.Dim);
        float px = x + 12;
        foreach (var pk in op.Perks) { px += Tag(GameData.Perk[pk].Name, px, sy, C.Accent) + 6; }
        sy += 26;
        if (op.PerkPicks > 0)
        {
            Ui.Label("Choose an ability:", x + 12, sy, 11, C.Accent); sy += 20;
            px = x + 12;
            foreach (var pk in GameData.Perks.Where(p => !op.Perks.Contains(p.Id)))
            {
                float bw = Ui.TextW(pk.Name, 11) + 16;
                if (px + bw > x + colW - 10) { px = x + 12; sy += 30; }
                if (Ui.Button(new RectangleF(px, sy, bw, 26), pk.Name, BtnStyle.Normal, true, 11)) { op.Perks.Add(pk.Id); op.PerkPicks--; G.Toast($"{pk.Name}: {pk.Desc}"); Changed(); }
                px += bw + 6;
            }
            sy += 34;
        }
        float attrBottom = Math.Max(ay + 300, sy + 8);
        // loadout
        float lx = twoCol ? x + colW + 10 : x, ly = twoCol ? y : attrBottom + 10;
        float lb = Loadout(op, lx, ly, colW);
        y = Math.Max(attrBottom, lb) + 10;
        y = Appearance(op, x, y, w);
        return y;
    }

    private float Loadout(Operator op, float x, float y, float w)
    {
        var L = op.Loadout;
        var r = new RectangleF(x, y, w, 420);
        Ui.Panel(r);
        Ui.Label("LOADOUT", x + 12, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float ry = y + 34, lw = 92, cw = w - lw - 24;
        string WName(WeaponInstance wi)
        {
            if (wi == null) return "— none —";
            var a = Pf.AssignedTo(wi.Uid);
            return $"{GameData.Weapon[wi.Base].Name}{(wi.Engraving.Length > 0 ? $" \"{wi.Engraving}\"" : "")}{(a != null && a != op ? $" ({a.Callsign})" : "")}";
        }
        void WeaponRow(string label, bool hg, Func<string> get, Action<string> set)
        {
            Ui.Label(label, x + 12, ry + 6, 12, C.Dim);
            var opts = new List<WeaponInstance> { null };
            opts.AddRange(Pf.Weapons.Where(wi => (GameData.Weapon[wi.Base].Cat == "hg") == hg));
            int cur = opts.FindIndex(wi => wi?.Uid == get());
            int dd = Ui.Cycler(new RectangleF(x + lw, ry, cw, 28), WName(cur >= 0 ? opts[cur] : null));
            if (dd != 0)
            {
                var nw = opts[Gui.Wrap(Math.Max(0, cur) + dd, opts.Count)];
                if (nw != null) { var other = Pf.AssignedTo(nw.Uid); if (other != null && other != op) { if (other.Loadout.Primary == nw.Uid) other.Loadout.Primary = null; if (other.Loadout.Secondary == nw.Uid) other.Loadout.Secondary = null; } }
                set(nw?.Uid); G.Audio.Equip(); Changed();
            }
            ry += 34;
        }
        WeaponRow("Primary", false, () => L.Primary, v => L.Primary = v);
        WeaponRow("Secondary", true, () => L.Secondary, v => L.Secondary = v);
        void ListRow<T>(string label, List<T> items, Func<T, string> id, Func<T, string> name, string cur, Action<string> set)
        {
            Ui.Label(label, x + 12, ry + 6, 12, C.Dim);
            int i = Math.Max(0, items.FindIndex(it => id(it) == cur));
            int dd = Ui.Cycler(new RectangleF(x + lw, ry, cw, 28), items.Count > 0 ? name(items[i]) : "—");
            if (dd != 0 && items.Count > 0) { set(id(items[Gui.Wrap(i + dd, items.Count)])); G.Audio.Equip(); Changed(); }
            ry += 34;
        }
        ListRow("Armor", GameData.Armors.Where(a => Pf.ArmorOwned.Contains(a.Id)).ToList(), a => a.Id, a => $"{a.Name} (prot {a.Prot}, ×{a.Speed})", L.Armor, v => L.Armor = v);
        ListRow("Helmet", GameData.Helmets.Where(a => Pf.HelmetsOwned.Contains(a.Id)).ToList(), a => a.Id, a => $"{a.Name} (prot {a.Prot})", L.Helmet, v => L.Helmet = v);
        ListRow("Ammo", GameData.Ammo.Where(a => Pf.AmmoOwned.Contains(a.Id)).ToList(), a => a.Id, a => a.Name, L.Ammo, v => L.Ammo = v);
        var tools = new List<ItemDef> { null }; tools.AddRange(GameData.Items.Where(i => i.Kind == "tool" && Pf.StockOf(i.Id) > 0));
        ListRow("Tool", tools, t => t?.Id, t => t == null ? "— none —" : $"{t.Name} ({Pf.StockOf(t.Id)} owned)", L.Tool, v => L.Tool = v);
        Ui.Label("Utility", x + 12, ry + 6, 12, C.Dim);
        var utils = new List<string> { null }; utils.AddRange(GameData.Items.Where(i => i.Kind == "util").Select(i => i.Id));
        float uw = (cw - 6) / 2;
        for (int s = 0; s < 4; s++)
        {
            string cur = s < L.Util.Count ? L.Util[s] : null;
            int i = utils.IndexOf(cur);
            var ur = new RectangleF(x + lw + (s % 2) * (uw + 6), ry + (s / 2) * 32, uw, 28);
            int dd = Ui.Cycler(ur, cur == null ? "— empty —" : $"{GameData.Item[cur].Name} ({Pf.StockOf(cur)})", true, 11);
            if (dd != 0)
            {
                var nv = utils[Gui.Wrap(i + dd, utils.Count)];
                var arr = L.Util.ToList();
                while (arr.Count <= s) arr.Add(null);
                arr[s] = nv;
                L.Util = arr.Where(u => u != null).Take(4).ToList();
                G.Audio.Equip(); Changed();
            }
        }
        ry += 68;
        var pw = Pf.WeaponByUid(L.Primary);
        if (pw != null)
        {
            var tex = G.Art.Weapon(pw, (int)((w - 24) * Ui.S), (int)(70 * Ui.S), 1.45f * Ui.S, 0.1f);
            P.Image(tex, (x + 12) * Ui.S, ry * Ui.S, (w - 24) * Ui.S, 70 * Ui.S);
        }
        if (L.Tool == "shield") Ui.Label("Shield operators fight with their secondary (handgun).", x + 12, ry + 70, 10, C.Dim);
        return Math.Max(y + 420, ry + 86);
    }

    private float Appearance(Operator op, float x, float y, float w)
    {
        var L = op.Look;
        float y0 = y;
        var uc = Art.Uniform(L);
        int cols = w > 900 ? 3 : 2;
        float cw = (w - 24 - (cols - 1) * 12) / cols;
        int rows = 0;
        var items = new List<Action<float, float>>();
        void Cyc(string label, string[] opts, Func<int> get, Action<int> set) => items.Add((cx, cy) =>
        {
            Ui.Label(label, cx, cy + 6, 11, C.Dim);
            int dd = Ui.Cycler(new RectangleF(cx + 104, cy, cw - 104, 28), opts[Math.Clamp(get(), 0, opts.Length - 1)], true, 11);
            if (dd != 0) { set(Gui.Wrap(get() + dd, opts.Length)); Changed(); }
        });
        void Col(string label, Func<string> get, string def, Action<string> set) => items.Add((cx, cy) =>
        {
            Ui.Label(label, cx, cy + 6, 11, C.Dim);
            int cur = Array.IndexOf(Palette, get());
            float sw = Math.Min(16, (cw - 104 - 30) / Palette.Length - 2);
            int n = Ui.Swatches(new RectangleF(cx + 104, cy + 6, cw - 134, sw), Palette, cur);
            if (n != cur && n >= 0) { set(Palette[n]); Changed(); }
            if (get() != null && Ui.Button(new RectangleF(cx + cw - 26, cy + 2, 26, 24), "↺", BtnStyle.Ghost, true, 11)) { set(null); Changed(); }
        });
        void Tog(string label, Func<bool> get, Action<bool> set) => items.Add((cx, cy) =>
        {
            bool v = Ui.Toggle(new RectangleF(cx, cy, cw, 28), label, get(), 12);
            if (v != get()) { set(v); Changed(); }
        });
        Cyc("Gender", new[] { "Male", "Female" }, () => op.Gender == "f" ? 1 : 0, v => op.Gender = v == 1 ? "f" : "m");
        Cyc("Face", GameData.Faces, () => L.Face, v => L.Face = v);
        Cyc("Skin tone", GameData.Skin.Select((_, i) => $"Tone {i + 1}").ToArray(), () => L.Skin, v => L.Skin = v);
        Cyc("Hairstyle", GameData.Hair, () => L.Hair, v => L.Hair = v);
        Cyc("Hair color", GameData.HairColors.Select((_, i) => $"Color {i + 1}").ToArray(), () => L.HairColor, v => L.HairColor = v);
        Cyc("Facial hair", GameData.Facial, () => L.Facial, v => L.Facial = v);
        Cyc("Build", GameData.Builds, () => L.Build, v => L.Build = v);
        Cyc("Uniform", GameData.Uniforms.Select(u => u.Name).ToArray(), () => GameData.Uniforms.FindIndex(u => u.Id == L.Uniform), v => { L.Uniform = GameData.Uniforms[v].Id; L.Shirt = L.Pants = L.Carrier = null; });
        Cyc("Eye protection", GameData.EyePro, () => L.EyePro, v => L.EyePro = v);
        Cyc("Headset", GameData.Headset, () => L.Headset, v => L.Headset = v);
        Cyc("Gloves", GameData.Gloves, () => L.Gloves, v => L.Gloves = v);
        Cyc("Boots", GameData.Boots, () => L.Boots, v => L.Boots = v);
        Cyc("Kneepads", GameData.Kneepads, () => L.Kneepads, v => L.Kneepads = v);
        Cyc("Watch", GameData.Watch, () => L.Watch, v => L.Watch = v);
        Cyc("Patch", GameData.Patches, () => L.Patch, v => L.Patch = v);
        Col("Shirt color", () => L.Shirt, uc.shirt, v => L.Shirt = v);
        Col("Pants color", () => L.Pants, uc.pants, v => L.Pants = v);
        Col("Plate carrier", () => L.Carrier, uc.carrier, v => L.Carrier = v);
        Tog("Wear helmet", () => L.Helmet, v => L.Helmet = v);
        Tog("Balaclava", () => L.Balaclava, v => L.Balaclava = v);
        items.Add((cx, cy) =>
        {
            Ui.Label("Unit ID", cx, cy + 6, 11, C.Dim);
            Ui.TextField("uid_" + op.Id, new RectangleF(cx + 104, cy, cw - 104, 28), L.UnitId, v => { L.UnitId = v; Changed(); }, 12, 8, "Unit ID");
        });
        rows = (items.Count + cols - 1) / cols;
        float h = 44 + rows * 36 + 46;
        Ui.Panel(new RectangleF(x, y, w, h));
        Ui.Label("APPEARANCE & UNIFORM", x + 12, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        for (int i = 0; i < items.Count; i++) items[i](x + 12 + (i % cols) * (cw + 12), y + 38 + (i / cols) * 36);
        float by = y + 44 + rows * 36;
        Ui.Label("Apply this uniform to the whole roster:", x + 12, by + 8, 11, C.Dim);
        if (Ui.Button(new RectangleF(x + 270, by + 2, 90, 28), "Apply", BtnStyle.Normal, true, 11))
        {
            foreach (var o in Pf.Operators) { o.Look.Uniform = L.Uniform; o.Look.Shirt = L.Shirt; o.Look.Pants = L.Pants; o.Look.Carrier = L.Carrier; o.Look.UnitId = L.UnitId; }
            G.Toast("Uniform applied to all operators."); Changed();
        }
        return y + h;
    }

    private void RecruitDialog()
    {
        var rng = new Rng((uint)(Pf.Day * 31 + Pf.Operators.Count * 7));
        double q = 0.9 + Pf.Tier() * 0.15;
        var cands = Enumerable.Range(0, 3).Select(i =>
        {
            var o = ProfileService.GenOperator(rng, quality: q + (i == 2 ? 0.4 : 0));
            for (int k = 0; k < 20 && Pf.Operators.Any(x => x.Callsign == o.Callsign); k++) o.Callsign = rng.Pick(GameData.Callsigns);
            return (op: o, cost: (int)Math.Round((3000 + o.Stats.Values.Sum() * 12) / 100.0) * 100);
        }).ToList();
        G.Modal(() =>
        {
            var r = Dialogs.Box(Ui, 980, 620, "Recruitment");
            Ui.Label($"Roster {Pf.Operators.Count}/12. Candidates refresh daily.", r.X + 20, r.Y + 46, 12, C.Dim);
            float cw = (r.Width - 60) / 3;
            for (int i = 0; i < cands.Count; i++)
            {
                var (op, cost) = cands[i];
                float cx = r.X + 20 + i * (cw + 10), cy = r.Y + 72;
                Ui.Panel(new RectangleF(cx, cy, cw, r.Height - 140), C.Panel2);
                Portrait(op, cx + 10, cy + 10, 110, 130);
                Ui.Label($"{op.Callsign}", cx + 130, cy + 12, 14, C.Text, FontKind.Bold);
                Ui.Label(op.Name, cx + 130, cy + 32, 11, C.Dim);
                Ui.Label(GameData.Role[op.Role].Name, cx + 130, cy + 52, 11);
                Ui.Label(GameData.Personality[op.Personality].Name, cx + 130, cy + 70, 11, C.Accent);
                Ui.Wrap(op.Bio, cx + 10, cy + 148, cw - 20, 10, C.Dim);
                float sy = cy + 190;
                foreach (var s in GameData.Stats)
                {
                    Ui.Label(s.Name, cx + 10, sy, 11);
                    Ui.Bar(new RectangleF(cx + 96, sy + 3, cw - 140, 8), op.Stat(s.Id) / 100f, C.Accent2);
                    Ui.Label(op.Stat(s.Id).ToString(), cx + cw - 34, sy, 11, C.Text, FontKind.Mono);
                    sy += 21;
                }
                if (Ui.Button(new RectangleF(cx + 10, cy + r.Height - 190, cw - 20, 36), $"Hire — {MathUtil.Money(cost)}", BtnStyle.Primary, Pf.Operators.Count < 12) && G.Spend(cost, $"Hired {op.Callsign}"))
                {
                    op.Color = GameData.OpColors[Pf.Operators.Count % GameData.OpColors.Length];
                    op.Loadout.Util = new() { "flash" };
                    Pf.Operators.Add(op); _selOp = op; Changed();
                    return false;
                }
            }
            return !Ui.Button(new RectangleF(r.Right - 120, r.Bottom - 50, 100, 36), "Close");
        });
    }
}
