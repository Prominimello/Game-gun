using Breachpoint.Core.Data;
using Breachpoint.Core.Map;
using Breachpoint.Core.Sim;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;
using Breachpoint.Gfx;
using Breachpoint.UI;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

/// <summary>Mission briefing: blueprint, intel, squad selection, quick loadouts, entry points, deploy.</summary>
public sealed class BriefScreen : Screen
{
    private readonly MissionConfig _cfg;
    private GameMap _map;
    private MapRenderer _renderer;
    private int _floor;
    private readonly List<string> _squad = new();
    private readonly Dictionary<string, int> _entries = new();
    private float _rightH = 800;
    private Profile Pf => G.Profile;

    private static readonly (string label, string[] util)[] UtilPresets =
    {
        ("2× Flashbang", new[] { "flash", "flash" }), ("2× Flash + Smoke", new[] { "flash", "flash", "smoke" }), ("Flash + Charge", new[] { "flash", "charge" }),
        ("2× Charge", new[] { "charge", "charge" }), ("4× Flashbang", new[] { "flash", "flash", "flash", "flash" }), ("Smoke + Gas", new[] { "smoke", "gas" }),
        ("Flash + Diversion", new[] { "flash", "stinger" }), ("None", Array.Empty<string>()),
    };

    public BriefScreen(BreachpointGame g, MissionConfig cfg) : base(g) { _cfg = cfg; }

    public override void Enter()
    {
        _map = MapGenerator.Generate(_cfg.MapType, _cfg.Seed, _cfg.Floors, _cfg.Small);
        _renderer = new MapRenderer(G, _map);
        foreach (var op in Pf.Operators.Where(o => o.Available()).OrderBy(o => o.Fatigue).Take(Math.Min(Pf.Seats(), 4))) { _squad.Add(op.Id); _entries[op.Id] = 0; }
    }

    public override void Leave() => _renderer?.Dispose();

    public override bool Back() { G.Switch(new HqScreen(G)); return true; }

    public override void Draw()
    {
        if (_renderer == null) return; // deployed; mission screen takes over next frame
        P.Begin();
        Ui.Fill(new RectangleF(0, 0, Ui.W, Ui.H), C.Bg);
        float rw = Math.Min(560, Ui.W * 0.44f), lw = Ui.W - rw;
        // ---- left: blueprint + intel ----
        float mapH = Ui.H * 0.58f;
        Ui.Fill(new RectangleF(0, 0, lw, mapH), C.Hex("#080b0e"));
        P.End();
        _renderer.DrawPreview(P, _floor, 0, 0, lw * Ui.S, mapH * Ui.S, Ui);
        P.Begin();
        float fx = 10;
        for (int i = 0; i < _map.Floors.Length; i++)
        {
            float bw = Ui.TextW(_map.Labels[i], 12) + 24;
            if (Ui.Button(new RectangleF(fx, 10, bw, 30), _map.Labels[i], i == _floor ? BtnStyle.On : BtnStyle.Normal, true, 12)) _floor = i;
            fx += bw + 6;
        }
        Ui.Line(0, mapH, lw, mapH, C.Line);
        var info = new RectangleF(0, mapH + 1, lw, Ui.H - mapH - 1);
        float off = Ui.BeginScroll("brief_info", info, 360);
        float y = info.Y + 12 + off, x = 16, w = lw - 32;
        var diff = CampaignData.Difficulty[_cfg.DiffOverride ?? Pf.Difficulty];
        Ui.Header(_cfg.Name, x, y, 17); y += 26;
        Ui.Label($"{(CampaignData.MissionTypes.TryGetValue(_cfg.Type, out var mt) ? mt.Name : "")} · {_cfg.OrgName} · Threat tier {_cfg.Tier}", x, y, 11, C.Dim); y += 22;
        y += Ui.Wrap(_cfg.Brief, x, y, w, 12) + 4;
        y += Ui.Wrap(_cfg.Intel, x, y, w, 12, C.Accent) + 4;
        if (_cfg.IntelNote != null) y += Ui.Wrap(_cfg.IntelNote, x, y, w, 12, C.Red) + 4;
        bool exact = _cfg.Training || diff.ShowEnemies || (_cfg.Org != null && Pf.Campaign.Intel.GetValueOrDefault(_cfg.Org) >= 2);
        string hostiles = _cfg.Training ? $"{_cfg.Enemies} targets" : exact ? $"{_cfg.Enemies} suspects" : Pf.Difficulty == "milsim" ? "Unknown" : $"{Math.Max(1, _cfg.Enemies - 2)}–{_cfg.Enemies + 2} suspects (estimated)";
        var kv = new (string, string)[]
        {
            ("Hostiles", hostiles), ("Hostages", _cfg.Hostages > 0 ? _cfg.Hostages.ToString() : "None reported"), ("Civilians", _cfg.Civilians > 0 ? "Possible" : "None expected"),
            ("High-value target", _cfg.Hvt ? _cfg.HvtName + " — capture alive" : "—"), ("Conditions", (_cfg.Night ? "Night" : "Day") + (_cfg.Rain ? ", rain" : ", clear")),
            ("Building", $"{_map.Floors.Length} level(s), {_map.Doors.Count} doors, {_map.Doors.Count(d => d.Exterior)} exterior entrances"),
            ("Payment", MathUtil.Money(_cfg.Reward)), ("Difficulty", diff.Name),
        };
        foreach (var (k, v) in kv) { Ui.Label(k, x, y, 11, C.Dim); Ui.Label(v, x + 150, y, 11); y += 19; }
        Ui.EndScroll();

        // ---- right: squad ----
        var right = new RectangleF(lw, 0, rw, Ui.H - 60);
        Ui.Line(lw, 0, lw, Ui.H, C.Line);
        float roff = Ui.BeginScroll("brief_squad", right, _rightH);
        x = lw + 12; w = rw - 24; y = 12 + roff;
        int seats = Pf.Seats();
        Ui.Header("Squad Selection", x, y);
        Ui.Label($"{_squad.Count}/{seats} seats ({GameData.Vehicle[Pf.Vehicle().Id].Name})", x + w, y + 3, 11, C.Dim, FontKind.Regular, Align.Right);
        y += 30;
        foreach (var op in Pf.Operators)
        {
            bool inSquad = _squad.Contains(op.Id), ok = op.Available();
            float h = inSquad ? 196 : 62;
            var r = new RectangleF(x, y, w, h);
            Ui.Fill(r, inSquad ? C.Hex("#16202a") : C.Panel2 * (ok ? 1 : 0.5f));
            Ui.Border(r, inSquad ? C.Accent : C.Line);
            var tex = G.Art.Portrait(op, (int)(40 * Ui.S), (int)(48 * Ui.S));
            P.Image(tex, (x + 8) * Ui.S, (y + 7) * Ui.S, 40 * Ui.S, 48 * Ui.S, ok ? null : Microsoft.Xna.Framework.Color.White * 0.4f);
            var head = new RectangleF(x, y, w, 60);
            Ui.Label(op.Callsign, x + 56, y + 9, 13, ok ? C.Text : C.Dim, FontKind.Bold);
            Ui.Label($"{GameData.Role[op.Role].Name} · Lv{op.Level}{(op.Fatigue > 60 ? " · FATIGUED" : "")}{(!ok ? $" · INJURED {op.Injury}d" : "")}", x + 56, y + 29, 10, ok ? C.Dim : C.Red);
            if (!inSquad) Ui.Label(Summary(op), x + 56, y + 44, 10, C.Dim);
            var box = new RectangleF(x + w - 34, y + 18, 22, 22);
            Ui.Fill(box, C.Hex("#0e141a")); Ui.Border(box, inSquad ? C.Accent : C.ButtonBorder);
            if (inSquad) Ui.Fill(new RectangleF(box.X + 5, box.Y + 5, 12, 12), C.Accent);
            if (ok && Ui.Clicked(head))
            {
                if (inSquad) _squad.Remove(op.Id);
                else if (_squad.Count < seats) { _squad.Add(op.Id); _entries.TryAdd(op.Id, 0); }
                else G.Toast("Vehicle is full — buy a bigger vehicle in the Garage.");
                G.Audio.Ui();
            }
            if (inSquad) MiniLoadout(op, x + 8, y + 64, w - 16);
            y += h + 6;
        }
        // supplies
        var need = new Dictionary<string, int>();
        foreach (var id in _squad) foreach (var u in Pf.OpById(id).Loadout.Util) need[u] = need.GetValueOrDefault(u) + 1;
        var tools = new Dictionary<string, int>();
        foreach (var id in _squad) { var t = Pf.OpById(id).Loadout.Tool; if (t != null) tools[t] = tools.GetValueOrDefault(t) + 1; }
        var shortList = need.Where(k => Pf.StockOf(k.Key) < k.Value).Select(k => k.Key).Concat(tools.Where(k => Pf.StockOf(k.Key) < k.Value).Select(k => k.Key)).ToList();
        float sh = 70 + (shortList.Count > 0 ? 20 : 0);
        Ui.Panel(new RectangleF(x, y, w, sh));
        Ui.Label("SUPPLIES", x + 10, y + 8, 11, C.Hex("#9fb0bf"), FontKind.Bold);
        string supply = need.Count == 0 ? "No consumables assigned." : string.Join(" · ", need.Select(k => $"{GameData.Item[k.Key].Name} {k.Value}/{Pf.StockOf(k.Key)}"));
        Ui.Wrap(supply, x + 10, y + 28, w - 20, 10);
        if (shortList.Count > 0) Ui.Wrap("Shortage: " + string.Join(", ", shortList.Select(k => GameData.Item[k].Name)) + " — excess removed at deployment.", x + 10, y + sh - 22, w - 20, 10, C.Red);
        y += sh + 10;
        Ui.EndScroll();
        _rightH = y - roff + 10;
        // bottom bar
        float by = Ui.H - 54;
        Ui.Fill(new RectangleF(lw, by - 6, rw, 60), C.Bg);
        if (Ui.Button(new RectangleF(lw + 12, by, 130, 42), "◀ Back to HQ")) G.Switch(new HqScreen(G));
        if (Ui.Button(new RectangleF(lw + 150, by, rw - 162, 42), $"DEPLOY {_squad.Count} OPERATOR{(_squad.Count == 1 ? "" : "S")} ▶", BtnStyle.Primary, _squad.Count > 0, 15)) Deploy();
    }

    private string Summary(Operator op)
    {
        var L = op.Loadout;
        var pw = Pf.WeaponByUid(L.Primary); var sw = Pf.WeaponByUid(L.Secondary);
        return $"{(pw != null ? GameData.Weapon[pw.Base].Name : "—")} / {(sw != null ? GameData.Weapon[sw.Base].Name : "—")} · {GameData.Armor[L.Armor].Name}";
    }

    private void MiniLoadout(Operator op, float x, float y, float w)
    {
        var L = op.Loadout;
        float cw = (w - 8) / 2;
        void Row(int col, int row, string label, string value, Action<int> change)
        {
            float cx = x + col * (cw + 8), cy = y + row * 42;
            Ui.Label(label, cx, cy, 9, C.Dim);
            int d = Ui.Cycler(new RectangleF(cx, cy + 12, cw, 26), value, true, 10);
            if (d != 0) { change(d); G.Audio.Equip(); G.SaveProfile(); }
        }
        Operator Assigned(string uid) => Pf.Operators.FirstOrDefault(o => o != op && (o.Loadout.Primary == uid || o.Loadout.Secondary == uid));
        void Weapon(int col, int row, string label, bool hg, Func<string> get, Action<string> set)
        {
            var opts = new List<WeaponInstance> { null }; opts.AddRange(Pf.Weapons.Where(wi => (GameData.Weapon[wi.Base].Cat == "hg") == hg));
            int cur = Math.Max(0, opts.FindIndex(wi => wi?.Uid == get()));
            var c = opts[cur];
            var a = c != null ? Assigned(c.Uid) : null;
            Row(col, row, label, c == null ? "—" : GameData.Weapon[c.Base].Name + (a != null ? $" ({a.Callsign})" : ""), d =>
            {
                var n = opts[Gui.Wrap(cur + d, opts.Count)];
                if (n != null) { var o = Assigned(n.Uid); if (o != null) { if (o.Loadout.Primary == n.Uid) o.Loadout.Primary = null; if (o.Loadout.Secondary == n.Uid) o.Loadout.Secondary = null; } }
                set(n?.Uid);
            });
        }
        Weapon(0, 0, "PRIMARY", false, () => L.Primary, v => L.Primary = v);
        Weapon(1, 0, "SECONDARY", true, () => L.Secondary, v => L.Secondary = v);
        var armors = GameData.Armors.Where(a => Pf.ArmorOwned.Contains(a.Id)).ToList();
        int ai = Math.Max(0, armors.FindIndex(a => a.Id == L.Armor));
        Row(0, 1, "ARMOR", armors[ai].Name, d => L.Armor = armors[Gui.Wrap(ai + d, armors.Count)].Id);
        var tools = new List<ItemDef> { null }; tools.AddRange(GameData.Items.Where(i => i.Kind == "tool" && Pf.StockOf(i.Id) > 0));
        int ti = Math.Max(0, tools.FindIndex(t => t?.Id == L.Tool));
        Row(1, 1, "TOOL", tools[ti]?.Name ?? "—", d => L.Tool = tools[Gui.Wrap(ti + d, tools.Count)]?.Id);
        int ui = Array.FindIndex(UtilPresets, p => p.util.SequenceEqual(L.Util));
        string ulabel = ui >= 0 ? UtilPresets[ui].label : string.Join(", ", L.Util.Select(u => GameData.Item[u].Short));
        Row(0, 2, "UTILITY", ulabel.Length == 0 ? "None" : ulabel, d => L.Util = UtilPresets[Gui.Wrap((ui < 0 ? -1 : ui) + d, UtilPresets.Length)].util.ToList());
        int e = _entries.GetValueOrDefault(op.Id);
        Row(1, 2, "ENTRY POINT", "Entry " + _map.Spawns[Math.Min(e, _map.Spawns.Count - 1)].Name, d => _entries[op.Id] = Gui.Wrap(e + d, _map.Spawns.Count));
    }

    public void TestDeploy() => Deploy();

    private void Deploy()
    {
        var squadOps = _squad.Select(id => Pf.OpById(id)).Where(o => o != null).ToList();
        var stockLeft = new Dictionary<string, int>(Pf.Stock);
        var toolUse = new Dictionary<string, int>();
        var saved = new List<(Operator op, List<string> util, string tool)>();
        foreach (var op in squadOps)
        {
            saved.Add((op, op.Loadout.Util.ToList(), op.Loadout.Tool));
            var util = new List<string>();
            foreach (var u in op.Loadout.Util) if (stockLeft.GetValueOrDefault(u) > 0) { stockLeft[u]--; util.Add(u); }
            string tool = op.Loadout.Tool;
            if (tool != null) { toolUse[tool] = toolUse.GetValueOrDefault(tool) + 1; if (toolUse[tool] > Pf.StockOf(tool)) tool = null; }
            op.Loadout.Util = util; op.Loadout.Tool = tool;
        }
        foreach (var k in Pf.Stock.Keys.ToList()) if (GameData.Item.TryGetValue(k, out var it) && it.Kind == "util") Pf.Stock[k] = stockLeft.GetValueOrDefault(k);
        var squad = squadOps.Select(op => new SquadMember { Op = op, Entry = Math.Min(_entries.GetValueOrDefault(op.Id), _map.Spawns.Count - 1) }).ToList();
        string diff = _cfg.DiffOverride ?? Pf.Difficulty;
        var mission = new Mission(_cfg, _map, squad, Pf, diff, Random.Shared.Next() ^ _cfg.Seed);
        foreach (var (op, util, tool) in saved) { op.Loadout.Util = util; op.Loadout.Tool = tool; }
        G.SaveProfile();
        G.Switch(new MissionScreen(G, mission, _renderer, m =>
        {
            var summary = Results.Apply(Pf, _cfg, m.Result, diff);
            G.SaveProfile();
            G.Switch(new DebriefScreen(G, _cfg, m, summary));
        }));
        _renderer = null; // ownership passes to the mission screen
    }
}
