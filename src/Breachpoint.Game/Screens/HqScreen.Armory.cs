using Breachpoint.Core.Data;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

public sealed partial class HqScreen
{
    private int _armTab;
    private WeaponInstance _selW;
    private float _yaw = 0.15f, _wzoom = 1f;
    private Texture2D _inspectTex;
    private string _inspectKey;
    private string _storeCat = "ar";
    private float _dragYawX = float.NaN;

    private float Armory(float x, float y, float w)
    {
        float y0 = y;
        _armTab = Ui.Tabs(new RectangleF(x, y, w, 32), new[] { "Gunsmith", "Weapon Store", "Equipment", "Armor & Ammunition" }, _armTab);
        y += 44;
        y = _armTab switch { 0 => Gunsmith(x, y, w), 1 => Store(x, y, w), 2 => Equipment(x, y, w), _ => ArmorShop(x, y, w) };
        return y - y0;
    }

    private float Gunsmith(float x, float y, float w)
    {
        if (_selW == null || !Pf.Weapons.Contains(_selW)) _selW = Pf.Weapons.FirstOrDefault();
        float lw = Math.Min(290, w * 0.3f), ly = y;
        foreach (var wi in Pf.Weapons)
        {
            var b = GameData.Weapon[wi.Base]; var a = Pf.AssignedTo(wi.Uid);
            var r = new RectangleF(x, ly, lw, 50);
            Card(r, wi == _selW);
            var tex = G.Art.Weapon(wi, (int)(84 * Ui.S), (int)(34 * Ui.S), 0.42f * Ui.S, 0, false);
            P.Image(tex, (x + 4) * Ui.S, (ly + 8) * Ui.S, 84 * Ui.S, 34 * Ui.S);
            Ui.Label(b.Name + (wi.Engraving.Length > 0 ? $" \"{wi.Engraving}\"" : ""), x + 94, ly + 8, 12, C.Text, FontKind.Bold);
            Ui.Label($"{GameData.Categories[b.Cat].Name} · {wi.Serial}{(a != null ? " · " + a.Callsign : "")} · {wi.Condition:0}%", x + 94, ly + 28, 10, C.Dim);
            if (Ui.Clicked(r)) { _selW = wi; G.Audio.Ui(); }
            ly += 56;
        }
        float rx = x + lw + 12, rw = w - lw - 12;
        float ry = _selW != null ? GunDetail(_selW, rx, y, rw) : y;
        return Math.Max(ly, ry);
    }

    private float GunDetail(WeaponInstance wi, float x, float y, float w)
    {
        var b = GameData.Weapon[wi.Base];
        float ih = Math.Min(260, w * 0.36f);
        var view = new RectangleF(x, y, w, ih + 60);
        Ui.Panel(view);
        Ui.Label(b.Name.ToUpperInvariant(), x + 12, y + 10, 15, C.Accent, FontKind.Bold);
        Ui.Label("Drag to rotate · slider to zoom", x + w - 12, y + 12, 11, C.Dim, FontKind.Regular, Align.Right);
        var img = new RectangleF(x + 10, y + 34, w - 20, ih - 10);
        P.Gradient(img.X * Ui.S, img.Y * Ui.S, img.Width * Ui.S, img.Height * Ui.S, C.Hex("#1d2833"), C.Hex("#0a0e12"), false);
        for (float gx = img.X; gx < img.Right; gx += 20) Ui.Line(gx, img.Y, gx, img.Bottom, Color.White * 0.03f);
        for (float gy = img.Y; gy < img.Bottom; gy += 20) Ui.Line(img.X, gy, img.Right, gy, Color.White * 0.03f);
        // rotate by dragging
        if (Ui.Enabled && G.Input.Down && img.Contains(G.Input.PressPos / Ui.S))
        {
            if (float.IsNaN(_dragYawX)) _dragYawX = Ui.Mouse.X;
            _yaw += (Ui.Mouse.X - _dragYawX) * 0.012f; _dragYawX = Ui.Mouse.X;
        }
        else _dragYawX = float.NaN;
        int pw = (int)(img.Width * Ui.S), ph = (int)(img.Height * Ui.S);
        float scale = Math.Min(pw / 230f, ph / 90f) * _wzoom;
        string key = $"{wi.Uid}|{string.Join(",", wi.Att.Values)}|{wi.Finish}|{wi.Camo[0]}{wi.Camo[1]}|{wi.Engraving}|{wi.Condition:0}|{pw}x{ph}|{scale:0.00}|{_yaw:0.00}";
        if (key != _inspectKey) { _inspectTex?.Dispose(); _inspectTex = G.Art.WeaponFresh(wi, pw, ph, scale, _yaw); _inspectKey = key; }
        P.Image(_inspectTex, img.X * Ui.S, img.Y * Ui.S, pw, ph);
        Ui.Label($"{b.Name} · {wi.Serial}", img.X + 8, img.Y + 6, 11, C.Dim, FontKind.Mono);
        if (wi.Engraving.Length > 0) Ui.Label($"Engraved: \"{wi.Engraving}\"", img.X + 8, img.Bottom - 20, 11, C.Hex("#e6dcbe"));
        Ui.Label("Rotate", x + 12, y + ih + 32, 12);
        float ny = Ui.Slider(new RectangleF(x + 70, y + ih + 28, 200, 24), (float)((MathUtil.AngNorm(_yaw) + Math.PI) / (2 * Math.PI)));
        _yaw = (float)(ny * 2 * Math.PI - Math.PI);
        Ui.Label("Zoom", x + 290, y + ih + 32, 12);
        _wzoom = 0.6f + Ui.Slider(new RectangleF(x + 340, y + ih + 28, 160, 24), (_wzoom - 0.6f) / 1.6f) * 1.6f;
        y += ih + 70;
        bool two = w > 720;
        float cw = two ? (w - 10) / 2 : w;
        // attachments
        var slots = GameData.Categories[b.Cat].Slots;
        float ah = 40 + slots.Length * 44;
        Ui.Panel(new RectangleF(x, y, cw, ah));
        Ui.Label("ATTACHMENTS", x + 12, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float sy = y + 34;
        int tier = Pf.Tier();
        foreach (var slot in slots)
        {
            var opts = new List<AttachmentDef> { null };
            opts.AddRange(GameData.Attachments.Where(a => a.Slot == slot && a.Cats.Contains(b.Cat)));
            wi.Att.TryGetValue(slot, out var curId);
            int cur = opts.FindIndex(a => a?.Id == curId);
            Ui.Label(GameData.SlotNames[slot], x + 12, sy + 6, 11, C.Dim);
            string label = cur <= 0 ? (slot == "optic" ? "Iron sights" : "Standard") : opts[cur].Name + " ✔";
            int d = Ui.Cycler(new RectangleF(x + 96, sy, cw - 108, 26), label, true, 11);
            if (d != 0)
            {
                // step to next option; locked options are skipped
                int ni = Math.Max(0, cur);
                for (int k = 0; k < opts.Count; k++) { ni = Gui.Wrap(ni + d, opts.Count); if (opts[ni] == null || opts[ni].Tier <= tier) break; }
                var na = opts[ni];
                if (na == null) { wi.Att.Remove(slot); G.Audio.Equip(); Changed(); }
                else if (G.Spend(na.Price, $"{na.Name} installed")) { wi.Att[slot] = na.Id; G.Audio.Equip(); Changed(); }
            }
            var desc = cur > 0 ? opts[cur].Desc : $"{opts.Count - 1} options · next costs shown in store";
            int locked = opts.Count(a => a != null && a.Tier > tier);
            Ui.Label(desc + (locked > 0 ? $" ({locked} locked until tier up)" : ""), x + 96, sy + 28, 9, C.Dim);
            sy += 44;
        }
        // stats
        float sx = two ? x + cw + 10 : x, sty = two ? y : y + ah + 10;
        var cur2 = WeaponStats.Of(wi); var stock = WeaponStats.Of(new WeaponInstance { Base = wi.Base, Condition = wi.Condition });
        var rows = new (string n, double v, double b2, double max, bool lower, bool kg)[]
        {
            ("Weight", cur2.Weight, stock.Weight, 8, true, true), ("Handling", cur2.Handling, stock.Handling, 100, false, false), ("Recoil", cur2.Recoil, stock.Recoil, 100, true, false),
            ("Accuracy", cur2.Accuracy, stock.Accuracy, 100, false, false), ("Ergonomics", cur2.Ergonomics, stock.Ergonomics, 100, false, false), ("Aim Speed", cur2.AimSpeed, stock.AimSpeed, 100, false, false),
            ("Reliability", cur2.Reliability, stock.Reliability, 100, false, false), ("Suppression", Math.Max(0, cur2.Suppression), Math.Max(0, stock.Suppression), 100, false, false), ("Visibility", cur2.Visibility, stock.Visibility, 100, true, false),
        };
        float sh = 44 + rows.Length * 22 + 130;
        Ui.Panel(new RectangleF(sx, sty, cw, sh));
        Ui.Label("STATISTICS (VS. STOCK)", sx + 12, sty + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float ry = sty + 36;
        foreach (var (n, v, bv, max, lower, kg) in rows)
        {
            Ui.Label(n, sx + 12, ry, 11);
            var bar = new RectangleF(sx + 100, ry + 3, cw - 170, 9);
            Ui.Fill(bar, C.Hex("#0a0e12"));
            float lo = (float)(Math.Min(v, bv) / max), diff = (float)(Math.Abs(v - bv) / max);
            Ui.Fill(new RectangleF(bar.X, bar.Y, bar.Width * Math.Min(1, lo), bar.Height), C.Accent2);
            bool better = lower ? v < bv : v > bv, worse = lower ? v > bv : v < bv;
            if (diff > 0.001f) Ui.Fill(new RectangleF(bar.X + bar.Width * lo, bar.Y, bar.Width * Math.Min(diff, 1 - lo), bar.Height), better ? C.Green : C.Red);
            Ui.Border(bar, C.Hex("#2a3542"));
            Ui.Label(kg ? $"{v:0.00}" : $"{v:0}", sx + cw - 12, ry, 11, better ? C.Green : worse ? C.Red : C.Text, FontKind.Mono, Align.Right);
            ry += 22;
        }
        var cal = GameData.Calibers[cur2.Cal];
        string[] info = { $"Caliber {cal.Name}", $"Capacity {cur2.Mag} rds · {cur2.Rpm} rpm ({cur2.Mode})", $"Damage {cur2.Dmg:0} × {cur2.Pellets} · Pen {cur2.Pen:0}", $"Reload {cur2.Reload:0.00}s · Weapon light {(cur2.Light ? "yes" : "no")}" };
        foreach (var s in info) { Ui.Label(s, sx + 12, ry + 6, 11, C.Dim); ry += 20; }
        y = Math.Max(y + ah, sty + sh) + 10;
        // finish & markings
        var fr = new RectangleF(x, y, w, 196);
        Ui.Panel(fr);
        Ui.Label("FINISH, MARKINGS & CONDITION", x + 12, y + 10, 12, C.Hex("#9fb0bf"), FontKind.Bold);
        float fy = y + 36;
        Ui.Label("Finish", x + 12, fy + 6, 11, C.Dim);
        int fi = GameData.Finishes.FindIndex(f => f.Id == wi.Finish);
        int fd = Ui.Cycler(new RectangleF(x + 110, fy, 260, 28), GameData.Finishes[Math.Max(0, fi)].Name);
        if (fd != 0) { wi.Finish = GameData.Finishes[Gui.Wrap(fi + fd, GameData.Finishes.Count)].Id; Changed(); }
        if (wi.Finish == "custom")
        {
            Ui.Label("Camo", x + 390, fy + 6, 11, C.Dim);
            int c0 = Ui.Swatches(new RectangleF(x + 430, fy + 4, 200, 14), Palette, Array.IndexOf(Palette, wi.Camo[0]));
            int c1 = Ui.Swatches(new RectangleF(x + 430, fy + 22, 200, 14), Palette, Array.IndexOf(Palette, wi.Camo[1]));
            if (c0 >= 0 && Palette[c0] != wi.Camo[0]) { wi.Camo[0] = Palette[c0]; Changed(); }
            if (c1 >= 0 && Palette[c1] != wi.Camo[1]) { wi.Camo[1] = Palette[c1]; Changed(); }
        }
        fy += 42;
        Ui.Label("Engraving", x + 12, fy + 6, 11, C.Dim);
        Ui.TextField("eng_" + wi.Uid, new RectangleF(x + 110, fy, 260, 28), wi.Engraving, v => { wi.Engraving = v; Changed(); }, 12, 22, "Engraving");
        Ui.Label($"Serial {wi.Serial}", x + 390, fy + 6, 11, C.Dim, FontKind.Mono);
        fy += 40;
        Ui.Label("Condition", x + 12, fy + 4, 11, C.Dim);
        Ui.Bar(new RectangleF(x + 110, fy + 6, 160, 9), (float)wi.Condition / 100, wi.Condition > 60 ? C.Green : C.Accent);
        Ui.Label($"{wi.Condition:0}% · {wi.RoundsFired} rounds fired", x + 280, fy + 3, 11);
        int maint = (int)Math.Round((100 - wi.Condition) * b.Price / 200 / 10) * 10;
        if (maint > 0 && Ui.Button(new RectangleF(x + w - 200, fy - 4, 188, 28), $"Maintain {MathUtil.Money(maint)}", BtnStyle.Normal, true, 11) && G.Spend(maint, "Weapon serviced")) { wi.Condition = 100; Changed(); }
        fy += 36;
        var assigned = Pf.AssignedTo(wi.Uid);
        int value = WeaponStats.Value(wi);
        Ui.Label($"Assigned to: {(assigned != null ? assigned.Callsign : "— unassigned —")} · Value {MathUtil.Money(value)}", x + 12, fy + 6, 11);
        if (Ui.Button(new RectangleF(x + w - 400, fy, 180, 28), $"Sell ({MathUtil.Money(value * 0.4)})", BtnStyle.Danger, assigned == null, 11))
        { Pf.Money += (long)(value * 0.4); Pf.Weapons.Remove(wi); _selW = null; G.Audio.Cash(); Changed(); }
        if (Ui.Button(new RectangleF(x + w - 212, fy, 200, 28), $"Buy copy ({MathUtil.Money(value)})", BtnStyle.Normal, true, 11) && G.Spend(value, "Duplicate build purchased"))
        {
            var copy = ProfileService.NewWeapon(wi.Base);
            copy.Att = new(wi.Att); copy.Finish = wi.Finish; copy.Camo = wi.Camo.ToArray(); copy.Engraving = wi.Engraving;
            Pf.Weapons.Add(copy); Changed();
        }
        return y + 206;
    }

    private float Store(float x, float y, float w)
    {
        int ci = Array.IndexOf(GameData.CategoryOrder, _storeCat);
        ci = Ui.Tabs(new RectangleF(x, y, w, 30), GameData.CategoryOrder.Select(c => GameData.Categories[c].Name).ToArray(), ci, 11);
        _storeCat = GameData.CategoryOrder[ci];
        y += 42;
        int cols = w > 900 ? 3 : 2;
        float cw = (w - (cols - 1) * 10) / cols;
        var list = GameData.Weapons.Where(wd => wd.Cat == _storeCat).ToList();
        int tier = Pf.Tier();
        for (int i = 0; i < list.Count; i++)
        {
            var wd = list[i];
            float cx = x + (i % cols) * (cw + 10), cy = y + (i / cols) * 296;
            bool locked = wd.Tier > tier;
            var r = new RectangleF(cx, cy, cw, 286);
            Ui.Panel(r, locked ? C.Panel * 0.6f : C.Panel);
            int owned = Pf.Weapons.Count(q => q.Base == wd.Id);
            Ui.Label(wd.Name, cx + 12, cy + 10, 14, locked ? C.Dim : C.Text, FontKind.Bold);
            Ui.Label($"{GameData.Calibers[wd.Cal].Name} · {owned} owned", cx + cw - 12, cy + 13, 10, C.Dim, FontKind.Regular, Align.Right);
            var inst = new WeaponInstance { Uid = "store_" + wd.Id, Base = wd.Id };
            var tex = G.Art.Weapon(inst, (int)((cw - 24) * Ui.S), (int)(90 * Ui.S), Math.Min((cw - 24) / 240f, 0.9f) * Ui.S, 0, false);
            P.Image(tex, (cx + 12) * Ui.S, (cy + 34) * Ui.S, (cw - 24) * Ui.S, 90 * Ui.S, locked ? Color.White * 0.5f : null);
            var st = WeaponStats.Of(inst);
            string[] lines = { $"Capacity {wd.Mag} · {wd.Rpm} rpm {wd.Mode}", $"Accuracy {st.Accuracy:0} · Handling {st.Handling:0}", $"Recoil {st.Recoil:0} · Reliability {st.Reliability:0}", $"Weight {st.Weight:0.0} kg" };
            float ly = cy + 132;
            foreach (var l in lines) { Ui.Label(l, cx + 12, ly, 11, C.Dim); ly += 19; }
            if (locked) Ui.Wrap($"Requires supplier license — Tier {wd.Tier}. Dismantle more organizations.", cx + 12, cy + 222, cw - 24, 11, C.Red);
            else if (Ui.Button(new RectangleF(cx + 12, cy + 236, cw - 24, 36), $"Buy — {MathUtil.Money(wd.Price)}", BtnStyle.Primary) && G.Spend(wd.Price, $"{wd.Name} purchased"))
            { var n = ProfileService.NewWeapon(wd.Id); Pf.Weapons.Add(n); _selW = n; Changed(); }
        }
        return y + ((list.Count + cols - 1) / cols) * 296;
    }

    private float Equipment(float x, float y, float w)
    {
        Ui.Panel(new RectangleF(x, y, w, 70 + GameData.Items.Count * 50));
        Ui.Header("Tactical Equipment", x + 12, y + 10);
        Ui.Label("Consumables are drawn from stock when you deploy; unused ones return afterwards. Tools are assigned to one operator each.", x + 12, y + 36, 11, C.Dim);
        float ry = y + 64;
        foreach (var it in GameData.Items)
        {
            int price = (int)Math.Round(it.Price * Pf.Diff().Cost);
            Ui.Line(x + 12, ry - 4, x + w - 12, ry - 4, C.Line);
            Ui.Label(it.Name, x + 12, ry + 4, 13, C.Text, FontKind.Bold);
            Ui.Label(it.Kind == "util" ? "Consumable" : "Tool (reusable)", x + 12, ry + 24, 10, C.Dim);
            Ui.Wrap(it.Desc, x + 210, ry + 6, w - 560, 11, C.Dim);
            Ui.Label($"In stock: {Pf.StockOf(it.Id)}", x + w - 330, ry + 12, 12, C.Text, FontKind.Mono);
            Ui.Label(MathUtil.Money(price), x + w - 200, ry + 12, 12, C.Accent, FontKind.Mono);
            if (Ui.Button(new RectangleF(x + w - 120, ry + 6, 50, 30), "+1", BtnStyle.Normal, true, 12) && G.Spend(price)) { Pf.AddStock(it.Id, 1); Changed(); }
            if (it.Kind == "util" && Ui.Button(new RectangleF(x + w - 64, ry + 6, 50, 30), "+5", BtnStyle.Normal, true, 12) && G.Spend(price * 5)) { Pf.AddStock(it.Id, 5); Changed(); }
            ry += 50;
        }
        return ry + 10;
    }

    private float ArmorShop(float x, float y, float w)
    {
        float Section(string title, IEnumerable<(string id, string name, string info, int price)> items, List<string> owned, float yy)
        {
            var list = items.ToList();
            Ui.Panel(new RectangleF(x, yy, w, 44 + list.Count * 40));
            Ui.Label(title.ToUpperInvariant(), x + 12, yy + 12, 12, C.Hex("#9fb0bf"), FontKind.Bold);
            float ry = yy + 40;
            foreach (var (id, name, info, price) in list)
            {
                bool has = owned.Contains(id);
                Ui.Label(name, x + 12, ry + 8, 12, C.Text, FontKind.Bold);
                Ui.Label(info, x + 230, ry + 9, 11, C.Dim);
                Ui.Label(has ? "OWNED" : MathUtil.Money(price), x + w - 200, ry + 9, 12, has ? C.Green : C.Accent, FontKind.Mono);
                if (!has && Ui.Button(new RectangleF(x + w - 110, ry + 2, 96, 30), "Purchase", BtnStyle.Primary, true, 11) && G.Spend(price, $"{name} unlocked for the unit")) { owned.Add(id); Changed(); }
                ry += 40;
            }
            return ry + 10;
        }
        y = Section("Body armor (unit-wide licence)", GameData.Armors.Select(a => (a.Id, a.Name, $"Protection {a.Prot} · speed ×{a.Speed} · stamina ×{a.Stam}", a.Price)), Pf.ArmorOwned, y);
        y = Section("Helmets (unit-wide)", GameData.Helmets.Select(a => (a.Id, a.Name, $"Protection {a.Prot}", a.Price)), Pf.HelmetsOwned, y);
        y = Section("Ammunition types", GameData.Ammo.Select(a => (a.Id, a.Name, $"{a.Desc} Dmg ×{a.Dmg}, pen ×{a.Pen}, cost ×{a.Cost}", a.Price)), Pf.AmmoOwned, y);
        return y;
    }
}
