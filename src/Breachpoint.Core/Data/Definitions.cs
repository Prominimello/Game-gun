namespace Breachpoint.Core.Data;

public sealed record Caliber(string Id, string Name, double Dmg, double Pen, double Cost, int Pellets = 1);

public sealed record WeaponCategory(string Id, string Name, double Reload, string[] Slots);

/// <summary>Drawing parameters for the procedural side-view weapon renderer.</summary>
public sealed record WeaponGeo(double Recv, double Barrel, string Stock, string MagT, double Hand,
    bool Rail = false, bool Bulky = false, bool Ak = false, bool Wood = false, bool Bolt = false, bool Pistol = false);

public sealed record WeaponDef(string Id, string Name, string Cat, string Cal, string Mode, int Rpm, int Mag, double Spread,
    double Recoil, double Handling, double Weight, double Rel, int Price, int Tier, WeaponGeo Geo);

/// <summary>Stat modifiers an attachment applies to a weapon.</summary>
public sealed class Mods
{
    public double Weight, Handling, Recoil, Accuracy, AimSpeed, Ergonomics, Reliability, Suppression, Visibility, Light, Range, Damage, MagMult;
}

public sealed record AttachmentDef(string Id, string Slot, string Name, string[] Cats, int Price, int Tier, Mods Mods, string Desc);

public sealed record FinishDef(string Id, string Name, string Color, string[] Camo = null);
public sealed record AmmoDef(string Id, string Name, double Dmg, double Pen, double Cost, int Price, string Desc);
public sealed record ArmorDef(string Id, string Name, int Prot, double Speed, double Stam, int Price);
public sealed record HelmetDef(string Id, string Name, int Prot, int Price, double Speed = 1);
public sealed record ItemDef(string Id, string Name, string Kind, int Price, string Desc, string Short);
public sealed record VehicleDef(string Id, string Name, int Seats, int Price, double Len, double Wid, string Color, bool Armored);
public sealed record UniformDef(string Id, string Name, string Shirt, string Pants, string Carrier, string Helmet, string Glove, string Patch, bool Camo = false);
public sealed record StatDef(string Id, string Name, string Desc);
public sealed record PersonalityDef(string Id, string Name, string Desc, double Stress, double React, double Acc, double Speed, double Dis = 0, bool Aura = false);
public sealed record RoleDef(string Id, string Name, string Desc);
public sealed record PerkDef(string Id, string Name, string Desc);
public sealed record OrgDef(string Id, string Name, string Short, int Tier, string Color, string Desc, string Boss);

public sealed record MissionTypeDef(string Id, string Name, string Map, string[] Obj, (int, int) Enemies, (int, int) Hostages, (int, int) Civilians,
    (int, int) Evidence, (int, int) Contraband, string Brief, bool Hvt = false);

public sealed class CampaignMissionDef
{
    public string Id, Org, Name, Type, Intel;
    public int Idx, Seed, Enemies, Hostages, Civilians, Evidence, Contraband, Reward, IntelReq;
    public int? Floors;
    public bool Small, Hvt, Lieutenant, Night, Rain, Takedown;
}

public sealed record EvidenceType(string Id, string Name, string Icon, int Intel);
public sealed record ContrabandType(string Id, string Name, string Icon, int Value);

public sealed record DifficultyDef(string Id, string Name, double DmgTaken, double EnemyAcc, double EnemyReact, bool ShowEnemies, double Cost, double Pay, string Death, double ExecTime, string Desc);

public sealed record EnemyDef(string Id, string Name, double Acc, double React, int Morale, double Aggr, bool Radio, double Surrender, string[] Weapons, int Armor);
