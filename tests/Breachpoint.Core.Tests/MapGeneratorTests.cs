using Breachpoint.Core.Map;
using Xunit;

namespace Breachpoint.Core.Tests;

public class MapGeneratorTests
{
    public static IEnumerable<object[]> MapTypes() =>
        new[] { "house", "house2", "warehouse", "compound", "bank", "hangar", "hotel", "prison", "store", "mansion", "shipyard", "rural", "bunker", "training" }
            .Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(MapTypes))]
    public void Generates_map_with_spawns_rooms_and_doors(string type)
    {
        var m = MapGenerator.Generate(type, 1234);
        Assert.True(m.Spawns.Count >= 1);
        Assert.NotEmpty(m.Rooms);
        Assert.NotEmpty(m.Doors);
    }

    [Theory]
    [MemberData(nameof(MapTypes))]
    public void Same_seed_gives_same_layout(string type)
    {
        var a = MapGenerator.Generate(type, 99);
        var b = MapGenerator.Generate(type, 99);
        Assert.Equal(a.Floors[0].T, b.Floors[0].T);
        Assert.Equal(a.Doors.Count, b.Doors.Count);
    }

    [Theory]
    [MemberData(nameof(MapTypes))]
    public void Every_room_is_reachable_from_the_first_spawn(string type)
    {
        var m = MapGenerator.Generate(type, 4242);
        var (seen, _) = MapGenerator.Flood(m, 0, m.Spawns[0].X, m.Spawns[0].Y);
        foreach (var r in m.Rooms.Where(r => r.F == 0))
        {
            bool any = false;
            for (int y = r.Y; y < r.Y + r.H && !any; y++)
                for (int x = r.X; x < r.X + r.W && !any; x++)
                    if (m.Get(0, x, y) == Tile.Floor && seen[m.Idx(x, y)]) any = true;
            Assert.True(any, $"room {r.Id} ({r.Tag}) on ground floor of {type} unreachable");
        }
    }
}
