using System.Text.Json;
using Breachpoint.Core.Data;
using Breachpoint.Core.Util;

namespace Breachpoint.Core.State;

/// <summary>Creates, saves, loads and queries the player profile.</summary>
public static class ProfileService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false, IncludeFields = false };

    public static WeaponInstance NewWeapon(string baseId) => new()
    {
        Uid = MathUtil.Uid("w"), Base = baseId, Serial = "TF-" + Random.Shared.Next(10000, 99999),
    };

    public static Operator GenOperator(Rng rng, string role = null, double quality = 1, string gender = null)
    {
        gender ??= rng.Chance(0.3) ? "f" : "m";
        var first = rng.Pick(gender == "f" ? GameData.NamesF : GameData.NamesM);
        var last = rng.Pick(GameData.Surnames);
        var stats = new Dictionary<string, int>();
        foreach (var s in GameData.Stats)
            stats[s.Id] = (int)Math.Round(MathUtil.Clamp(rng.Int(30, 60) + (quality - 1) * 12 + rng.Gauss() * 6, 15, 85));
        role ??= rng.Pick(new[] { "assault", "assault", "point", "medic", "tech", "marksman" });
        void Atleast(string k, int v) { if (stats[k] < v) stats[k] = v; }
        if (role == "medic") Atleast("med", 60 + rng.Int(0, 15));
        if (role == "tech") Atleast("tec", 60 + rng.Int(0, 15));
        if (role == "marksman") Atleast("acc", 58 + rng.Int(0, 15));
        if (role == "leader") Atleast("lea", 60 + rng.Int(0, 15));
        if (role == "point") Atleast("awa", 55 + rng.Int(0, 15));
        return new Operator
        {
            Id = MathUtil.Uid("op"), Name = $"{first} {last}", Callsign = rng.Pick(GameData.Callsigns), Gender = gender,
            Look = new Look
            {
                Face = rng.Int(0, GameData.Faces.Length - 1), Skin = rng.Int(0, GameData.Skin.Length - 1),
                Hair = gender == "f" ? rng.Pick(new[] { 1, 5, 6, 7, 9, 10 }) : rng.Pick(new[] { 0, 1, 2, 3, 4, 8, 9 }),
                HairColor = rng.Int(0, GameData.HairColors.Length - 1), Facial = gender == "f" ? 0 : rng.Pick(new[] { 0, 0, 1, 1, 2, 3, 4, 5 }),
                Build = rng.Int(0, 3), Balaclava = rng.Chance(0.25), EyePro = rng.Int(0, 3), Headset = rng.Int(1, 2),
                Kneepads = rng.Int(0, 2), Watch = rng.Int(0, 2), Patch = rng.Int(0, 6),
            },
            Stats = stats, Personality = rng.Pick(GameData.Personalities).Id, Role = role,
            Bio = $"{first} {last} is a {rng.Pick(GameData.Backgrounds)} from {rng.Pick(GameData.Hometowns)}.",
            Age = rng.Int(24, 44),
        };
    }

    public static Profile NewGame(string difficulty = "standard", bool ironman = false, string unitName = "Task Force Breachpoint")
    {
        var rng = Rng.FromTime();
        var p = new Profile
        {
            UnitName = unitName, Difficulty = difficulty, Ironman = ironman,
            Stock = new() { ["flash"] = 10, ["smoke"] = 4, ["gas"] = 0, ["stinger"] = 2, ["charge"] = 3, ["medkit"] = 2, ["lockpick"] = 1, ["doorcam"] = 0, ["shield"] = 0, ["ram"] = 1 },
        };
        var v = new VehicleInstance { Id = "suv", Uid = MathUtil.Uid("v") };
        p.Vehicles.Add(v); p.ActiveVehicle = v.Uid;
        var roles = new[] { "leader", "point", "assault", "assault", "medic", "tech" };
        var used = new HashSet<string>();
        for (int i = 0; i < roles.Length; i++)
        {
            var op = GenOperator(rng, roles[i]);
            while (used.Contains(op.Callsign)) op.Callsign = rng.Pick(GameData.Callsigns);
            used.Add(op.Callsign);
            op.Color = GameData.OpColors[i % GameData.OpColors.Length];
            p.Operators.Add(op);
        }
        void Add(string b, int n, Dictionary<string, string> att = null) { for (int i = 0; i < n; i++) { var w = NewWeapon(b); if (att != null) w.Att = new(att); p.Weapons.Add(w); } }
        Add("m4a1", 3, new() { ["optic"] = "rds", ["side"] = "light" });
        Add("mp5", 2, new() { ["side"] = "light" });
        Add("r870", 1);
        Add("g17", 6);
        var prim = p.Weapons.Where(w => GameData.Weapon[w.Base].Cat != "hg").ToList();
        var sec = p.Weapons.Where(w => GameData.Weapon[w.Base].Cat == "hg").ToList();
        for (int i = 0; i < p.Operators.Count; i++)
        {
            var op = p.Operators[i];
            op.Loadout.Primary = i < prim.Count ? prim[i].Uid : null;
            op.Loadout.Secondary = i < sec.Count ? sec[i].Uid : null;
            op.Loadout.Tool = op.Role switch { "medic" => "medkit", "tech" => "lockpick", "point" => "ram", _ => null };
        }
        return p;
    }

    public static void Save(Profile p, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(p, Json));
        File.Move(tmp, path, true);
    }

    public static Profile Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), Json) : null; }
        catch { return null; }
    }

    public static WeaponInstance WeaponByUid(this Profile p, string uid) => uid == null ? null : p.Weapons.FirstOrDefault(w => w.Uid == uid);
    public static Operator OpById(this Profile p, string id) => p.Operators.FirstOrDefault(o => o.Id == id);
    public static Operator AssignedTo(this Profile p, string uid) => p.Operators.FirstOrDefault(o => o.Loadout.Primary == uid || o.Loadout.Secondary == uid);
    public static DifficultyDef Diff(this Profile p) => CampaignData.Difficulty[p.Difficulty];
    public static VehicleInstance Vehicle(this Profile p) => p.Vehicles.FirstOrDefault(v => v.Uid == p.ActiveVehicle) ?? p.Vehicles[0];
    public static int Seats(this Profile p) => GameData.Vehicle[p.Vehicle().Id].Seats;

    /// <summary>Supplier tier: 1 + number of organizations taken down.</summary>
    public static int Tier(this Profile p)
    {
        int t = 1;
        foreach (var o in CampaignData.Orgs)
        {
            var tk = CampaignData.Campaign.First(m => m.Org == o.Id && m.Takedown);
            if (p.Campaign.Done.ContainsKey(tk.Id)) t++;
        }
        return Math.Min(4, t);
    }

    public static bool CampaignAvailable(this Profile p, CampaignMissionDef m)
    {
        if (p.Campaign.Done.ContainsKey(m.Id)) return true;
        int orgIdx = CampaignData.Orgs.FindIndex(o => o.Id == m.Org);
        if (orgIdx > 0)
        {
            var prevTk = CampaignData.Campaign.First(x => x.Org == CampaignData.Orgs[orgIdx - 1].Id && x.Takedown);
            if (!p.Campaign.Done.ContainsKey(prevTk.Id)) return false;
        }
        if (m.Idx > 0)
        {
            var prev = CampaignData.Campaign.First(x => x.Org == m.Org && x.Idx == m.Idx - 1);
            if (!p.Campaign.Done.ContainsKey(prev.Id)) return false;
        }
        return true;
    }

    public static int XpFor(int lvl) => (int)Math.Round(250 * (lvl - 1) * (1 + (lvl - 1) * 0.35));

    public static bool AddXp(Operator op, int xp)
    {
        op.Xp += xp;
        bool leveled = false;
        while (op.Xp >= XpFor(op.Level + 1))
        {
            op.Level++; op.Points += 3; leveled = true;
            if (op.Level % 3 == 0) op.PerkPicks++;
        }
        return leveled;
    }

    public static int MaxHp(Operator op) => 100 + (op.Perks.Contains("tough") ? 15 : 0);

    public static void AdvanceDay(this Profile p, int days = 1)
    {
        p.Day += days;
        foreach (var op in p.Operators)
        {
            if (op.Injury > 0)
            {
                op.Injury = Math.Max(0, op.Injury - days);
                if (op.Injury == 0) { op.Status = "active"; op.InjuryDesc = ""; }
            }
            op.Fatigue = Math.Max(0, op.Fatigue - 18 * days);
            if (op.Morale < 60) op.Morale = Math.Min(100, op.Morale + 3 * days);
        }
    }

    public static bool Available(this Operator op) => op.Injury <= 0 && op.Status != "kia";
}
