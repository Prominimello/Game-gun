using Breachpoint.Core.Data;
using Breachpoint.Core.Util;

namespace Breachpoint.Core.State;

/// <summary>Builds mission configurations for campaign, operations board, weekly challenge, training and sandbox.</summary>
public static class MissionFactory
{
    public static MissionConfig FromCampaign(Profile p, CampaignMissionDef m)
    {
        var org = CampaignData.Org[m.Org];
        var T = CampaignData.MissionTypes[m.Type];
        int enemies = m.Enemies;
        string note = null;
        int orgIdx = CampaignData.Orgs.FindIndex(o => o.Id == m.Org);
        if (m.Takedown && p.Campaign.Intel[m.Org] < m.IntelReq)
        {
            enemies += 2;
            note = $"LIMITED INTEL: only {p.Campaign.Intel[m.Org]}/{m.IntelReq} intel gathered on {org.Short}. Expect more resistance.";
        }
        if (m.Idx == 0 && orgIdx > 0 && p.Campaign.HvtCaptured.GetValueOrDefault(CampaignData.Orgs[orgIdx - 1].Id))
        {
            enemies = Math.Max(2, enemies - 1);
            note = $"Interrogation of {CampaignData.Orgs[orgIdx - 1].Boss} gave us an edge: suspect count reduced.";
        }
        return new MissionConfig
        {
            Id = m.Id, CampaignId = m.Id, Name = m.Name, Type = m.Type, MapType = T.Map, Org = m.Org, OrgName = org.Name, Seed = m.Seed,
            Floors = m.Floors, Small = m.Small, Enemies = enemies, Hostages = m.Hostages, Civilians = m.Civilians, Evidence = m.Evidence,
            Contraband = m.Contraband, Hvt = m.Hvt, HvtName = m.Hvt ? org.Boss : null, Lieutenant = m.Lieutenant, Night = m.Night, Rain = m.Rain,
            Reward = m.Reward, ParTime = 120 + enemies * 22, Brief = T.Brief, Intel = m.Intel, IntelNote = note, Tier = org.Tier, Takedown = m.Takedown,
        };
    }

    public static MissionConfig GenOperation(Profile p, Rng rng, string type = null, int? tier = null, bool hvt = false, string name = null, int? weekly = null)
    {
        var keys = CampaignData.MissionTypes.Keys.Where(k => k != "mansion" || p.Tier() >= 2).ToList();
        type ??= rng.Pick(keys);
        var T = CampaignData.MissionTypes[type];
        int t = tier ?? Math.Max(1, Math.Min(4, p.Tier() + (rng.Chance(0.25) ? 1 : 0) - (rng.Chance(0.25) ? 1 : 0)));
        double sc = 0.85 + t * 0.15;
        int enemies = (int)Math.Round(rng.Int(T.Enemies.Item1, T.Enemies.Item2) * sc);
        int hostages = rng.Int(T.Hostages.Item1, T.Hostages.Item2);
        var eligible = CampaignData.Orgs.Where(o => o.Tier <= t).ToList();
        string orgName = p.Campaign.Complete || rng.Chance(0.4) ? $"{rng.Pick(CampaignData.GenOrgA)} {rng.Pick(CampaignData.GenOrgB)}" : rng.Pick(eligible).Name;
        bool isHvt = hvt || T.Hvt;
        int reward = (int)Math.Round((5000 + enemies * 1300 + hostages * 1500 + (isHvt ? 8000 : 0)) * (0.8 + t * 0.3) / 100) * 100;
        return new MissionConfig
        {
            Id = "op_" + rng.Int(1, 1_000_000_000), Name = name ?? T.Name, Type = type, MapType = T.Map, OrgName = orgName, Seed = rng.Int(1, 1_000_000_000),
            Small = rng.Chance(0.5), Enemies = enemies, Hostages = hostages, Civilians = rng.Int(T.Civilians.Item1, T.Civilians.Item2),
            Evidence = rng.Int(T.Evidence.Item1, T.Evidence.Item2), Contraband = rng.Int(T.Contraband.Item1, T.Contraband.Item2),
            Hvt = isHvt, HvtName = isHvt ? $"{rng.Pick(GameData.SuspectFirst)} \"{rng.Pick(GameData.SuspectNick)}\" {rng.Pick(GameData.Surnames)}" : null,
            Lieutenant = rng.Chance(0.3), Night = rng.Chance(0.3), Rain = rng.Chance(0.2), Reward = reward, ParTime = 120 + enemies * 22,
            Brief = T.Brief, Intel = $"Suspects linked to {orgName}. Threat tier {t}.", Tier = t, Weekly = weekly,
        };
    }

    public static List<MissionConfig> OpsBoard(Profile p)
    {
        if (p.OpsBoard == null || p.OpsDay != p.Day)
        {
            var rng = new Rng((uint)(p.Day * 7919 + 13));
            p.OpsBoard = new() { GenOperation(p, rng), GenOperation(p, rng), GenOperation(p, rng) };
            if (p.Campaign.Done.Count >= 4) p.OpsBoard.Add(GenOperation(p, rng, "mansion", hvt: true, name: "HVT Contract"));
            p.OpsDay = p.Day;
        }
        return p.OpsBoard;
    }

    public static MissionConfig Weekly(Profile p)
    {
        int wk = (p.Day - 1) / 7;
        var rng = new Rng((uint)(wk * 104729 + 7));
        var m = GenOperation(p, rng, tier: Math.Min(4, p.Tier() + 1), name: "Weekly Challenge", weekly: wk);
        m.Reward = (int)Math.Round(m.Reward * 1.8);
        m.Night = true;
        return m;
    }

    public static MissionConfig Training() => new()
    {
        Id = "train", Name = "Kill House Exercise", Type = "warrant", MapType = "training", OrgName = "Training", Seed = Random.Shared.Next(1, 99999),
        Enemies = 8, Civilians = 4, Reward = 0, ParTime = 120, Tier = 1, Training = true, Intel = "Facility exercise.",
        Brief = "Clear the kill house. Hit all targets, avoid the no-shoot targets. Training rounds — no injuries.",
    };

    public static MissionConfig Sandbox(string type, int floors, int enemies, int tier, int hostages, int civilians, int evidence, int contraband,
        bool hvt, bool night, bool rain, string diff, int seed)
    {
        var T = CampaignData.MissionTypes[type];
        return new MissionConfig
        {
            Id = "sb", Name = "Sandbox: " + T.Name, Type = type, MapType = T.Map, OrgName = "Sandbox", Seed = seed, Floors = floors, Enemies = enemies,
            Hostages = hostages, Civilians = civilians, Evidence = evidence, Contraband = contraband, Hvt = hvt, HvtName = "Sandbox Target",
            Night = night, Rain = rain, Reward = 2000 + enemies * 400, ParTime = 120 + enemies * 22, Brief = T.Brief, Intel = "Custom scenario.",
            Tier = tier, Sandbox = true, DiffOverride = diff,
        };
    }
}
