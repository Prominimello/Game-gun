using Breachpoint.Core.Data;
using Breachpoint.Core.Map;
using Breachpoint.Core.Sim;
using Breachpoint.Core.State;
using Xunit;
using Xunit.Abstractions;

namespace Breachpoint.Core.Tests;

public class SimulationTests
{
    private readonly ITestOutputHelper _out;
    public SimulationTests(ITestOutputHelper o) => _out = o;

    /// <summary>Simple bot: repeatedly breach the nearest unvisited door, then hunt remaining suspects.</summary>
    internal static MissionResult RunBot(MissionConfig cfg, Profile p, int squadSize, int seed, int maxSteps = 6000)
    {
        var map = MapGenerator.Generate(cfg.MapType, cfg.Seed, cfg.Floors, cfg.Small);
        var squad = p.Operators.Take(squadSize).Select((op, i) => new SquadMember { Op = op, Entry = i % map.Spawns.Count }).ToList();
        var m = new Mission(cfg, map, squad, p, "standard", seed) { ReplayMode = true };
        m.Start();
        var tried = new HashSet<int>();
        string[] actions = { "flashclear", "kickclear", "openclear", "charge" };
        for (int step = 0; step < maxSteps && !m.Ended; step++)
        {
            if (step % 20 == 0)
            {
                var ops = m.Ops.Where(Mission.CanAct).ToList();
                if (ops.Count > 0 && ops.All(u => u.Orders.Count == 0 && u.Follow == null))
                {
                    var L = ops[0];
                    var door = m.Map.Doors.Where(d => d.State == DoorState.Closed && !tried.Contains(d.Id))
                        .OrderBy(d => Math.Abs(d.X * 32 - L.X) + Math.Abs(d.Y * 32 - L.Y) + (d.F != L.Floor ? 3000 : 0)).FirstOrDefault();
                    if (door != null) { tried.Add(door.Id); m.OrderDoor(ops, door, actions[step % 4], false); }
                    else
                    {
                        var e = m.Units.FirstOrDefault(u => u.Team == Team.Enemy && u.Alive && !u.Restrained && !u.Incap);
                        if (e != null) m.OrderMove(ops, Mission.ToTile(e.X), Mission.ToTile(e.Y), e.Floor, false);
                    }
                }
            }
            m.Update(0.05);
            m.Fx.Clear(); m.Sounds.Clear(); m.Voices.Clear();
        }
        return m.Finish(true);
    }

    public static IEnumerable<object[]> Types() => CampaignData.MissionTypes.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_mission_type_simulates_without_errors(string type)
    {
        var p = ProfileService.NewGame();
        var T = CampaignData.MissionTypes[type];
        for (int seed = 1; seed <= 2; seed++)
        {
            var cfg = new MissionConfig
            {
                Name = type, Type = type, MapType = T.Map, Seed = seed * 977, Enemies = T.Enemies.Item2, Hostages = T.Hostages.Item2, Civilians = T.Civilians.Item2,
                Evidence = T.Evidence.Item2, Contraband = T.Contraband.Item2, Hvt = T.Hvt || seed == 2, HvtName = "X", Lieutenant = seed == 1, Night = seed == 2,
                Rain = seed == 1, Tier = seed + 1, ParTime = 200, Reward = 10000,
            };
            var r = RunBot(cfg, p, 4, seed * 31);
            _out.WriteLine($"{type}#{seed}: rank={r.Rank} {r.Total} arrested={r.Arrested} killed={r.Killed} escaped={r.Escaped} hostages={r.HostSaved}/{r.HostDead} t={r.Time:0}");
            Assert.NotNull(r.Rank);
        }
    }

    [Fact]
    public void First_campaign_mission_is_winnable_by_bot()
    {
        var p = ProfileService.NewGame();
        var cfg = MissionFactory.FromCampaign(p, CampaignData.Campaign[0]);
        int wins = 0;
        for (int i = 0; i < 4; i++) if (!RunBot(cfg, p, 4, 100 + i, 8000).Failed) wins++;
        _out.WriteLine($"wins {wins}/4");
        Assert.True(wins >= 2);
    }

    [Fact]
    public void Results_update_profile()
    {
        var p = ProfileService.NewGame();
        var cfg = MissionFactory.FromCampaign(p, CampaignData.Campaign[0]);
        var r = RunBot(cfg, p, 4, 7, 8000);
        int day = p.Day;
        var sum = Results.Apply(p, cfg, r, p.Difficulty);
        Assert.Equal(day + 1, p.Day);
        Assert.NotEmpty(sum.OpLines);
        Assert.All(sum.OpLines, l => Assert.True(l.Xp > 0));
    }

    [Fact]
    public void Profile_roundtrips_through_json()
    {
        var p = ProfileService.NewGame("veteran", true, "Test Unit");
        var path = Path.Combine(Path.GetTempPath(), $"bp_test_{Guid.NewGuid():N}.json");
        ProfileService.Save(p, path);
        var q = ProfileService.Load(path);
        File.Delete(path);
        Assert.Equal("Test Unit", q.UnitName);
        Assert.Equal(p.Operators.Count, q.Operators.Count);
        Assert.Equal(p.Weapons[0].Att["optic"], q.Weapons[0].Att["optic"]);
        Assert.True(q.Ironman);
    }

    [Fact]
    public void Attachments_change_weapon_stats()
    {
        var w = ProfileService.NewWeapon("m4a1");
        var before = WeaponStats.Of(w);
        w.Att["muzzle"] = "supp";
        var after = WeaponStats.Of(w);
        Assert.True(after.Suppressed);
        Assert.True(after.Weight > before.Weight);
        Assert.False(before.Suppressed);
    }
}
