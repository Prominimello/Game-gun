namespace Breachpoint.Core.State;

/// <summary>A specific owned firearm with its attachments and cosmetics.</summary>
public sealed class WeaponInstance
{
    public string Uid { get; set; }
    public string Base { get; set; }
    public Dictionary<string, string> Att { get; set; } = new();
    public string Finish { get; set; } = "black";
    public string[] Camo { get; set; } = { "#3a3a3a", "#777777" };
    public string Engraving { get; set; } = "";
    public string Serial { get; set; } = "";
    public double Condition { get; set; } = 100;
    public int RoundsFired { get; set; }
}

public sealed class Look
{
    public int Face { get; set; }
    public int Skin { get; set; }
    public int Hair { get; set; }
    public int HairColor { get; set; }
    public int Facial { get; set; }
    public int Build { get; set; }
    public string Uniform { get; set; } = "swat";
    public string Shirt { get; set; }
    public string Pants { get; set; }
    public string Carrier { get; set; }
    public bool Helmet { get; set; } = true;
    public bool Balaclava { get; set; }
    public int EyePro { get; set; }
    public int Headset { get; set; }
    public int Gloves { get; set; }
    public int Boots { get; set; }
    public int Kneepads { get; set; }
    public int Watch { get; set; }
    public int Patch { get; set; }
    public string UnitId { get; set; } = "TF-1";
}

public sealed class Loadout
{
    public string Primary { get; set; }
    public string Secondary { get; set; }
    public string Armor { get; set; } = "light";
    public string Helmet { get; set; } = "bump";
    public string Ammo { get; set; } = "fmj";
    public List<string> Util { get; set; } = new() { "flash", "flash" };
    public string Tool { get; set; }
}

public sealed class Operator
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Callsign { get; set; }
    public string Gender { get; set; } = "m";
    public Look Look { get; set; } = new();
    public Dictionary<string, int> Stats { get; set; } = new();
    public string Personality { get; set; } = "calm";
    public string Role { get; set; } = "assault";
    public int Xp { get; set; }
    public int Level { get; set; } = 1;
    public int Points { get; set; }
    public List<string> Perks { get; set; } = new();
    public int PerkPicks { get; set; }
    public double Morale { get; set; } = 70;
    public double Fatigue { get; set; }
    public int Injury { get; set; }
    public string InjuryDesc { get; set; } = "";
    public string Status { get; set; } = "active";
    public int Missions { get; set; }
    public int Kills { get; set; }
    public int Arrests { get; set; }
    public int Rescues { get; set; }
    public int Shots { get; set; }
    public int Hits { get; set; }
    public Dictionary<string, int> Bonds { get; set; } = new();
    public string Bio { get; set; } = "";
    public int Age { get; set; }
    public string Color { get; set; } = "#4fc3f7";
    public Loadout Loadout { get; set; } = new();
    public int DiedDay { get; set; }
    public string DiedMission { get; set; }

    public int Stat(string id) => Stats.TryGetValue(id, out var v) ? v : 40;
}

public sealed class VehicleInstance
{
    public string Id { get; set; } = "suv";
    public string Uid { get; set; }
    public string Paint { get; set; } = "#20252b";
    public string Markings { get; set; } = "TF-1";
    public string Lights { get; set; } = "hidden";
    public List<string> Accessories { get; set; } = new();
}

public sealed class CampaignState
{
    public Dictionary<string, string> Done { get; set; } = new();
    public Dictionary<string, int> Intel { get; set; } = new() { ["ldn"] = 0, ["bwl"] = 0, ["syn"] = 0, ["mer"] = 0 };
    public Dictionary<string, bool> HvtCaptured { get; set; } = new();
    public bool Complete { get; set; }
}

public sealed class EvidenceRecord
{
    public string Name { get; set; }
    public string Org { get; set; }
    public string Mission { get; set; }
    public int Day { get; set; }
}

public sealed class CareerStats
{
    public int Missions { get; set; }
    public int Success { get; set; }
    public int Arrests { get; set; }
    public int Kills { get; set; }
    public int Rescued { get; set; }
    public int Lost { get; set; }
    public int Seized { get; set; }
}

/// <summary>The whole persistent save game.</summary>
public sealed class Profile
{
    public int Version { get; set; } = 1;
    public string UnitName { get; set; } = "Task Force Breachpoint";
    public string Difficulty { get; set; } = "standard";
    public bool Ironman { get; set; }
    public int Day { get; set; } = 1;
    public long Money { get; set; } = 25000;
    public List<Operator> Operators { get; set; } = new();
    public List<Operator> Fallen { get; set; } = new();
    public List<WeaponInstance> Weapons { get; set; } = new();
    public Dictionary<string, int> Stock { get; set; } = new();
    public List<string> ArmorOwned { get; set; } = new() { "none", "light" };
    public List<string> HelmetsOwned { get; set; } = new() { "none", "bump" };
    public List<string> AmmoOwned { get; set; } = new() { "fmj" };
    public List<VehicleInstance> Vehicles { get; set; } = new();
    public string ActiveVehicle { get; set; }
    public CampaignState Campaign { get; set; } = new();
    public List<EvidenceRecord> Evidence { get; set; } = new();
    public int TrainingLevel { get; set; } = 1;
    public int MedicalLevel { get; set; } = 1;
    public CareerStats Stats { get; set; } = new();
    public List<MissionConfig> OpsBoard { get; set; }
    public int OpsDay { get; set; }
    public int WeeklyDone { get; set; } = -1;

    public int StockOf(string id) => Stock.TryGetValue(id, out var v) ? v : 0;
    public void AddStock(string id, int n) => Stock[id] = Math.Max(0, StockOf(id) + n);
}

/// <summary>Everything needed to generate and run one operation.</summary>
public sealed class MissionConfig
{
    public string Id { get; set; }
    public string CampaignId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string MapType { get; set; }
    public string Org { get; set; }
    public string OrgName { get; set; }
    public int Seed { get; set; }
    public int? Floors { get; set; }
    public bool Small { get; set; }
    public int Enemies { get; set; }
    public int Hostages { get; set; }
    public int Civilians { get; set; }
    public int Evidence { get; set; }
    public int Contraband { get; set; }
    public bool Hvt { get; set; }
    public string HvtName { get; set; }
    public bool Lieutenant { get; set; }
    public bool Night { get; set; }
    public bool Rain { get; set; }
    public int Reward { get; set; }
    public double ParTime { get; set; } = 240;
    public string Brief { get; set; }
    public string Intel { get; set; }
    public string IntelNote { get; set; }
    public int Tier { get; set; } = 1;
    public bool Takedown { get; set; }
    public bool Sandbox { get; set; }
    public bool Training { get; set; }
    public string DiffOverride { get; set; }
    public int? Weekly { get; set; }
}
