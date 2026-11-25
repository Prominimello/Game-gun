using System.Globalization;

namespace Breachpoint.Core.Util;

public static class MathUtil
{
    public const double Tau = Math.PI * 2;

    public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
    public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static double Dist(double ax, double ay, double bx, double by) => Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));

    public static double AngNorm(double a)
    {
        while (a > Math.PI) a -= Tau;
        while (a < -Math.PI) a += Tau;
        return a;
    }

    /// <summary>Signed shortest angle from a to b.</summary>
    public static double AngDiff(double a, double b) => AngNorm(b - a);

    public static string Money(double n) =>
        (n < 0 ? "-$" : "$") + Math.Abs(Math.Round(n)).ToString("#,0", CultureInfo.InvariantCulture);

    public static string Time(double s)
    {
        int t = Math.Max(0, (int)Math.Floor(s));
        return $"{t / 60:00}:{t % 60:00}";
    }

    /// <summary>FNV-1a string hash (stable across runs).</summary>
    public static uint Hash(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }
    }

    private static int _uid = Environment.TickCount & 0xFFFF;
    public static string Uid(string prefix) => $"{prefix}_{Interlocked.Increment(ref _uid):x}{Random.Shared.Next(1296):x}";
}
