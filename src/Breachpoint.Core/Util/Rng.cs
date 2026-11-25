namespace Breachpoint.Core.Util;

/// <summary>Small, fast, deterministic random generator (mulberry32). Same seed = same sequence on every platform.</summary>
public sealed class Rng
{
    private uint _a;

    public Rng(uint seed) => _a = seed == 0 ? 1u : seed;
    public Rng(int seed) : this(unchecked((uint)seed)) { }

    public static Rng FromTime() => new((uint)(DateTime.UtcNow.Ticks & 0xFFFFFFFF) ^ (uint)Environment.TickCount);

    /// <summary>Uniform double in [0, 1).</summary>
    public double Next()
    {
        unchecked
        {
            _a += 0x6D2B79F5;
            uint t = _a;
            t = (t ^ (t >> 15)) * (1 | t);
            t ^= t + (t ^ (t >> 7)) * (61 | t);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }

    public int Int(int lo, int hi) => lo + (int)Math.Floor(Next() * (hi - lo + 1));
    public bool Chance(double p) => Next() < p;
    public double Range(double lo, double hi) => lo + Next() * (hi - lo);

    /// <summary>Approximately normal, mean 0, std-dev ~1.</summary>
    public double Gauss()
    {
        double s = 0;
        for (int i = 0; i < 4; i++) s += Next();
        return (s - 2) / 0.577;
    }

    public T Pick<T>(IReadOnlyList<T> list) => list[(int)Math.Floor(Next() * list.Count)];

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = (int)Math.Floor(Next() * (i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public T Weighted<T>(IReadOnlyList<T> items, Func<T, double> weight)
    {
        double total = 0;
        foreach (var it in items) total += weight(it);
        double r = Next() * total;
        foreach (var it in items)
        {
            r -= weight(it);
            if (r <= 0) return it;
        }
        return items[^1];
    }
}
