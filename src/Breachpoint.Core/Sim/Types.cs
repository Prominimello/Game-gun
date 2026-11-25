using Breachpoint.Core.Data;
using Breachpoint.Core.State;

namespace Breachpoint.Core.Sim;

public enum Team { Op, Enemy, Civ }

public struct Pos
{
    public double X, Y, T;
    public int F;
    public Pos(double x, double y, int f, double t = 0) { X = x; Y = y; F = f; T = t; }
}

public sealed class SimWeapon
{
    public WeaponInstance Inst;
    public WeaponStats St;
    public int Mag, Reserve, Fired;
}

/// <summary>Colors used to draw a unit sprite.</summary>
public sealed class UnitLook
{
    public string Shirt = "#666666", Pants = "#333333", Carrier, Helmet, Skin = "#c99a70", Hair = "#222222", Glove;
    public bool Camo, Balaclava;
    public int Build;
    public string Key => $"{Shirt}{Pants}{Carrier}{Helmet}{Skin}{Hair}{Glove}{Camo}{Balaclava}{Build}";
}

public sealed class AiState
{
    public string State = "idle", Style = "holder";
    public Pos Post;
    public double Morale;
    public bool Radio, Radioed, Alerted, InvArrived, Dummy, ExecLogged;
    public List<(int x, int y)> Patrol;
    public int Pi;
    public double WaitT, DecideT, Shouted = -1, LostT, ExecT = -1, InvT, SurrT, Unwatched, CheckT, RadioT, NoticeT, ToldT = -100;
    public Unit Shouter, Guard, AimingAt;
    public List<Unit> GuardList;
    public Pos? LastKnown, Investigate;
    public double? FaceTo;
    public (int x, int y)? Exit;
}

public sealed class Order
{
    public string Kind;           // move, door, throw, wait, go, waitdoor, interact
    public int X, Y, F;           // tile target (move)
    public string Pace;
    public int DoorId = -1;
    public string Action;         // door action
    public string Grenade;
    public double WX, WY;         // world target (throw)
    public double Dur, El, T0, Delay;
    public bool Started, Clearing, Pathed, Auto;
    public Unit Leader;
    public string TargetId;
    public string InteractKind;   // arrest, secure, collect, heal
}

public sealed class TimedAct
{
    public string Type;
    public double T, Dur;
    public int DoorId = -1, Tx, Ty, Tries;
}

public sealed class PathNode
{
    public int F, X, Y;
    public PathNode(int f, int x, int y) { F = f; X = x; Y = y; }
}

public sealed class Unit
{
    public string Id, Name, FullName, Kind, Status = "", Color = "#ffffff", Entry;
    public Team Team;
    public Operator Op;
    public double X, Y, Angle, Hp = 100, MaxHp = 100;
    public int Floor;
    public bool Alive = true, Dead, Downed, Incap, Restrained, Surrendered, Escaped, Evacuated, Stable, Shield, Armed = true, HostageBound, Kneel, Hvt, Lieutenant;
    public bool Visible, SeenNow, WasSeen, Spotted, Hold;
    public List<Order> Orders = new();
    public List<PathNode> Path;
    public int Pi;
    public TimedAct Act;
    public Unit Target;
    public double AimT, ReactT = 0.5, FireCd, BurstPause, RecoilHeat, ReloadT, JamT, Stun, Gas, Stress, Bleed, StepT, ScanT, FollowT, ReactBonus;
    public int Burst;
    public string ReloadKind;
    public string Stance = "stand", Pace = "walk";
    public double SpeedMult = 1, Armor, Helmet, Acc = 0.5, React = 0.7, Pitch = 1;
    public List<SimWeapon> Weapons = new();
    public int Wi;
    public List<string> Util = new();
    public string Tool;
    public int ToolUses;
    public UnitLook Look = new();
    public Dictionary<string, int> Stats = new();
    public List<string> Perks = new();
    public PersonalityDef Pers;
    public AmmoDef Ammo;
    public bool Female;
    public Unit Follow;
    public double? WatchAngle, ScanAng, MoveAng, AlertAng;
    public double AlertT = -100;
    public Pos? LastSeen;
    public AiState Ai;
    public int RoomId = -1;
    public int Kills, Hits, Shots, Arrests, Rescues, EvidenceFound;
    public double DamageTaken;

    /// <summary>Shallow copy (used by the replay viewer for "ghost" units).</summary>
    public Unit Clone() => (Unit)MemberwiseClone();

    public SimWeapon W => Weapons.Count > 0 ? Weapons[Math.Min(Wi, Weapons.Count - 1)] : null;
    public string WeaponCat => Armed && !Surrendered && !Restrained && !HostageBound && W != null ? W.St.Cat : null;
    public int Stat(string k) => Stats.TryGetValue(k, out var v) ? v : 40;
    public bool HasPerk(string p) => Perks.Contains(p);
    public string Role => Op?.Role ?? "";
}

public sealed class MissionItem
{
    public string Id, Kind, Type, Name, EvType;
    public double X, Y;
    public int F, Intel, Value;
    public bool Collected, Seen;
}

public sealed class Grenade
{
    public string Type;
    public double X, Y, Sx, Sy, Tx, Ty, T, Travel, Fuse, Z;
    public int F;
    public Unit Owner;
    public bool Bounced, Done;
}

public sealed class Cloud
{
    public double X, Y, R, MaxR, T;
    public int F;
}

public sealed class CamSource
{
    public double X, Y, Angle, T;
    public int F;
}

public sealed class Noise
{
    public double X, Y, R;
    public int F;
    public string Kind;
    public Unit Src;
}

public sealed class HeardMarker
{
    public double X, Y, T;
    public int F;
}

/// <summary>Visual effect request for the renderer.</summary>
public sealed class FxEvent
{
    public string Type, Kind;
    public double X, Y, X1, Y1, Ang, R;
    public int F;
    public bool Sup, Big;
    public Team Team;
}

/// <summary>Sound request for the audio system.</summary>
public sealed class SoundEvent
{
    public string Name, Cal;
    public double X, Y;
    public bool Flag;
}

public sealed class VoiceEvent
{
    public string Text;
    public double Pitch;
    public bool Prio;
}

public sealed class LogEntry
{
    public double T;
    public string Text, Color;
}

/// <summary>Recorded snapshot for replays.</summary>
public sealed class Frame
{
    public double T;
    public int[] U; // 6 ints per unit: x, y, floor, angle*100, state bits, hp
}

public sealed class SimEvent
{
    public double T;
    public string Type, G;
    public int F, Id, X0, Y0, X1, Y1;
    public Team Team;
    public string DoorState;
}

public sealed class Objective
{
    public string Id, Text;
    public bool Done, Req, Fail;
}

public sealed class ScoreLine
{
    public string Name, Detail;
    public int Pts, Max;
}

public sealed class OpResult
{
    public string Id, Name;
    public double Hp, MaxHp;
    public bool Downed, Dead, Stable;
    public int Kills, Arrests, Rescues, Shots, Hits, Evidence, ToolUses;
    public List<string> Util = new();
    public List<(string uid, int fired, string cal)> Fired = new();
}

public sealed class MissionResult
{
    public bool Failed;
    public string Rank, Reason, HvtName;
    public int Total, Arrested, Killed, Escaped, HostSaved, HostDead, CivDead, EvFound, ConFound, Roe, CivKilledByOp;
    public double Time;
    public bool? HvtCaptured;
    public bool LieutenantCaptured;
    public List<ScoreLine> Score = new();
    public List<(string name, int intel)> Evidence = new();
    public List<(string name, int value)> Contraband = new();
    public List<OpResult> Ops = new();
    public Dictionary<string, int> UtilUsed = new();
}
