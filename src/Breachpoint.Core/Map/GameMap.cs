namespace Breachpoint.Core.Map;

public static class Tile
{
    public const byte Ground = 0, Floor = 1, Wall = 2, Window = 3, Door = 4, Furn = 5, High = 6, Stairs = 7, BrokenWindow = 8, Fence = 9, Void = 10, Water = 11;
}

public static class Mat
{
    public const byte Grass = 0, Asphalt = 1, Dirt = 2, Gravel = 3, Sidewalk = 4, Wood = 10, TileFloor = 11, Carpet = 12, Concrete = 13, Marble = 14, Metal = 15, Rock = 16;
}

public static class Furn
{
    public const byte None = 0, Sofa = 1, Table = 2, Bed = 3, Desk = 4, Shelf = 5, Counter = 6, Crate = 7, Cabinet = 8, Lab = 9, Barrel = 10, Toilet = 11,
        Car = 12, Tree = 13, Rack = 14, Container = 15, Hay = 16, Chair = 17, Plant = 18, Safe = 19, Bunk = 20, Locker = 21, Bush = 22;
}

public enum DoorState { Closed, Open, Broken }

public sealed class Door
{
    public int Id, F, X, Y;
    public DoorState State = DoorState.Closed, State0;
    public bool Locked, Barricaded, Exterior, Heavy, KnownLocked;
    public int Hinge = 1;
    /// <summary>Animation 0 (closed) .. 1 (open). Presentation only.</summary>
    public float Anim;
}

public sealed class Room
{
    public int Id, F, X, Y, W, H, Building;
    public string Tag;
}

public sealed record Spawn(string Name, int X, int Y, double Ang, char Side);
public sealed record Stair(int X, int Y, int F0, int F1);
public sealed record Decor(string Type, int X, int Y, int W, int H, string Color);

public sealed class Building
{
    public int Id, X, Y, W, H;
    public int[] Floors;
    public string Name = "";
}

public sealed class FloorLayer
{
    public byte[] T, Mat, Furn;
    public short[] Room, Door;
}

/// <summary>A tile map with one or more floors.</summary>
public sealed class GameMap
{
    public const int TS = 32;
    public int W, H, Seed;
    public string Type;
    public FloorLayer[] Floors;
    public List<Door> Doors = new();
    public List<Room> Rooms = new();
    public List<Spawn> Spawns = new();
    public List<Stair> Stairs = new();
    public List<Decor> Decor = new();
    public List<Building> Buildings = new();
    public string[] Labels;

    /// <summary>Incremented whenever tiles change (e.g. a window breaks) so renderers can refresh caches.</summary>
    public int Version;

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
    public int Idx(int x, int y) => y * W + x;
    public byte Get(int f, int x, int y) => InBounds(x, y) ? Floors[f].T[y * W + x] : Tile.Wall;
    public void Set(int f, int x, int y, byte v) { if (InBounds(x, y)) Floors[f].T[y * W + x] = v; }
    public Door DoorAt(int f, int x, int y)
    {
        if (!InBounds(x, y)) return null;
        int id = Floors[f].Door[y * W + x];
        return id >= 0 ? Doors[id] : null;
    }

    public static bool Passable(byte t) => t == Tile.Ground || t == Tile.Floor || t == Tile.Door || t == Tile.Stairs || t == Tile.BrokenWindow;
}
