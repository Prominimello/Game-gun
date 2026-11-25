using Breachpoint.Core.Data;
using Breachpoint.Core.State;
using Breachpoint.Core.Util;

namespace Breachpoint.Core.Sim;

public sealed class OpSummary
{
    public Operator Op;
    public int Xp, NewLevel, Kills, Arrests, Rescues;
    public bool Leveled;
    public string InjuryText;
}

public sealed class ResultSummary
{
    public List<(string label, double amount)> Lines = new();
    public List<OpSummary> OpLines = new();
    public List<string> Unlocks = new();
    public List<Operator> Deaths = new();
    public long Net;
}

/// <summary>Applies a finished mission to the persistent profile: money, XP, injuries, deaths, intel, campaign progress.</summary>
public static class Results
{
    public static ResultSummary Apply(Profile p, MissionConfig cfg, MissionResult r, string difficulty)
    {
        var diff = CampaignData.Difficulty.GetValueOrDefault(difficulty, p.Diff());
        var sum = new ResultSummary();
        int tierBefore = p.Tier();
        double pay = 0;
        if (!cfg.Training)
        {
            double basePay = cfg.Reward * diff.Pay * (r.Failed ? 0.15 : 0.5 + r.Total / 100.0) * (cfg.Sandbox ? 0.4 : 1);
            pay += basePay; sum.Lines.Add(("Operational funding", basePay));
            int conVal = r.Contraband.Sum(c => c.value);
            if (conVal > 0) { pay += conVal * 0.25; sum.Lines.Add(("Asset forfeiture share (25%)", conVal * 0.25)); }
            int intelV = r.Evidence.Sum(e => Math.Max(1, e.intel));
            if (intelV > 0) { pay += intelV * 350; sum.Lines.Add(("Intelligence bonus", intelV * 350)); }
            if (r.HvtCaptured == true) { pay += cfg.Reward * 0.25; sum.Lines.Add(("HVT captured alive", cfg.Reward * 0.25)); }
            if (r.LieutenantCaptured) { pay += 2500; sum.Lines.Add(("Lieutenant in custody", 2500)); }
        }
        double ammo = 0;
        foreach (var o in r.Ops)
        {
            var op = p.OpById(o.Id);
            var am = GameData.AmmoById.GetValueOrDefault(op?.Loadout.Ammo ?? "fmj", GameData.Ammo[0]);
            foreach (var f in o.Fired) ammo += f.fired * GameData.Calibers[f.cal].Cost * am.Cost * 1.1 * diff.Cost * (cfg.Training ? 0.3 : 1);
        }
        if (ammo > 0) sum.Lines.Add(("Ammunition expended", -ammo));
        pay -= ammo;
        p.Money += (long)Math.Round(pay);
        sum.Net = (long)Math.Round(pay);

        // return unused consumables, deplete medkits
        foreach (var o in r.Ops)
        {
            foreach (var u in o.Util) p.AddStock(u, 1);
            var op = p.OpById(o.Id);
            if (op != null && op.Loadout.Tool == "medkit" && o.ToolUses <= 0) { p.AddStock("medkit", -1); sum.Lines.Add(("Medical kit depleted", 0)); }
        }

        int rankXp = r.Rank switch { "S" => 160, "A" => 110, "B" => 60, "C" => 30, _ => 0 };
        var ids = r.Ops.Select(o => o.Id).ToList();
        var lost = new List<Operator>();
        foreach (var o in r.Ops)
        {
            var op = p.OpById(o.Id);
            if (op == null) continue;
            int xp = (cfg.Training ? 60 : 120) + o.Kills * 25 + o.Arrests * 60 + o.Rescues * 50 + o.Evidence * 25 + rankXp;
            if (r.Failed) xp = (int)Math.Round(xp * 0.4);
            op.Missions++; op.Kills += o.Kills; op.Arrests += o.Arrests; op.Rescues += o.Rescues; op.Shots += o.Shots; op.Hits += o.Hits;
            string injury = "";
            if (!cfg.Training)
            {
                if (o.Dead)
                {
                    bool dies = p.Ironman || diff.Death == "always" || (diff.Death == "chance" && Random.Shared.NextDouble() < 0.5);
                    if (dies) { op.Status = "kia"; injury = "KILLED IN ACTION"; lost.Add(op); }
                    else { op.Injury = 9; op.Status = "injured"; op.InjuryDesc = "Critical gunshot wounds"; injury = "Critically wounded — 9 days"; }
                }
                else if (o.Downed) { op.Injury = 5; op.Status = "injured"; op.InjuryDesc = "Gunshot wound, surgery"; injury = "Seriously wounded — 5 days"; }
                else if (o.Hp < o.MaxHp)
                {
                    double dmg = o.MaxHp - o.Hp;
                    op.Injury = Math.Max(1, (int)Math.Ceiling(dmg / 22));
                    op.Status = "injured"; op.InjuryDesc = dmg > 40 ? "Gunshot wound" : "Grazing wound / blunt trauma";
                    injury = $"Wounded — {op.Injury} day{(op.Injury > 1 ? "s" : "")}";
                }
            }
            var armor = GameData.Armor.GetValueOrDefault(op.Loadout.Armor ?? "light", GameData.Armors[1]);
            op.Fatigue = Math.Min(100, op.Fatigue + (cfg.Training ? 12 : 22) * armor.Stam * (1.3 - op.Stat("end") / 200.0));
            op.Morale = MathUtil.Clamp(op.Morale + (r.Failed ? -10 : r.Rank == "S" ? 10 : r.Rank == "A" ? 6 : 3), 0, 100);
            foreach (var other in ids) if (other != op.Id) op.Bonds[other] = op.Bonds.GetValueOrDefault(other) + (r.Failed ? 1 : 2);
            bool leveled = ProfileService.AddXp(op, xp);
            sum.OpLines.Add(new OpSummary { Op = op, Xp = xp, Leveled = leveled, NewLevel = op.Level, InjuryText = injury, Kills = o.Kills, Arrests = o.Arrests, Rescues = o.Rescues });
        }
        foreach (var op in lost)
        {
            op.DiedDay = p.Day; op.DiedMission = cfg.Name;
            p.Fallen.Add(op); p.Operators.Remove(op);
            foreach (var other in p.Operators)
            {
                if (other.Bonds.TryGetValue(op.Id, out var b)) other.Morale = Math.Max(0, other.Morale - 10 - b * 2);
                other.Morale = Math.Max(0, other.Morale - 8);
            }
            sum.Deaths.Add(op);
            p.Stats.Lost++;
        }
        foreach (var o in r.Ops)
            foreach (var f in o.Fired)
            {
                var w = p.WeaponByUid(f.uid);
                if (w == null) continue;
                w.RoundsFired += f.fired;
                w.Condition = Math.Max(25, w.Condition - f.fired * 0.05 - (cfg.Training ? 0 : 0.5));
            }
        if (!cfg.Training)
        {
            foreach (var e in r.Evidence) p.Evidence.Add(new EvidenceRecord { Name = e.name, Org = cfg.Org, Mission = cfg.Name, Day = p.Day });
            p.Stats.Seized += r.Contraband.Sum(c => c.value);
            if (cfg.Org != null)
            {
                int intel = r.Evidence.Sum(e => Math.Max(1, e.intel)) + (r.LieutenantCaptured ? 2 : 0) + (r.HvtCaptured == true ? 2 : 0);
                p.Campaign.Intel[cfg.Org] = p.Campaign.Intel.GetValueOrDefault(cfg.Org) + intel;
                if (intel > 0) sum.Lines.Add(($"Intel gathered on {CampaignData.Org[cfg.Org].Short}: +{intel}", 0));
            }
            if (cfg.CampaignId != null && !r.Failed)
            {
                string order = "SABC";
                if (!p.Campaign.Done.TryGetValue(cfg.CampaignId, out var prev) || order.IndexOf(r.Rank) < order.IndexOf(prev)) p.Campaign.Done[cfg.CampaignId] = r.Rank;
                if (r.HvtCaptured == true && cfg.Org != null) p.Campaign.HvtCaptured[cfg.Org] = true;
                if (cfg.CampaignId == "c4_4") p.Campaign.Complete = true;
            }
            if (cfg.Weekly.HasValue && !r.Failed) p.WeeklyDone = cfg.Weekly.Value;
            p.Stats.Missions++;
            if (!r.Failed) p.Stats.Success++;
            p.Stats.Arrests += r.Arrested; p.Stats.Kills += r.Killed; p.Stats.Rescued += r.HostSaved;
            p.AdvanceDay(1);
            p.OpsBoard = null;
        }
        int tierAfter = p.Tier();
        if (tierAfter > tierBefore) sum.Unlocks.Add($"Supplier tier {tierAfter} unlocked: new weapons, attachments and equipment are available in the Armory.");
        return sum;
    }
}
