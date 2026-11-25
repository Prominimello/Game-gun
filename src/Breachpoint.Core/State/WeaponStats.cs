using Breachpoint.Core.Data;
using Breachpoint.Core.Util;

namespace Breachpoint.Core.State;

/// <summary>Derived statistics for a weapon instance (base weapon + attachments + condition).</summary>
public sealed class WeaponStats
{
    public string Name, Cat, Cal, Mode;
    public int Rpm, Mag, Pellets;
    public double Spread, Weight, Handling, Recoil, Accuracy, AimSpeed, Ergonomics, Reliability, Suppression, Visibility, Range, Dmg, Pen, Reload, Noise;
    public bool Light, Suppressed;

    public static WeaponStats Of(WeaponInstance w)
    {
        var b = GameData.Weapon[w.Base];
        var m = new Mods();
        foreach (var id in w.Att.Values)
        {
            if (!GameData.Attachment.TryGetValue(id, out var a)) continue;
            var x = a.Mods;
            m.Weight += x.Weight; m.Handling += x.Handling; m.Recoil += x.Recoil; m.Accuracy += x.Accuracy; m.AimSpeed += x.AimSpeed;
            m.Ergonomics += x.Ergonomics; m.Reliability += x.Reliability; m.Suppression += x.Suppression; m.Visibility += x.Visibility;
            m.Light += x.Light; m.Range += x.Range; m.Damage += x.Damage; m.MagMult += x.MagMult;
        }
        var cal = GameData.Calibers[b.Cal];
        double weight = Math.Max(0.5, b.Weight + m.Weight);
        double handling = MathUtil.Clamp(b.Handling + m.Handling - (weight - b.Weight) * 6, 5, 100);
        double ergonomics = MathUtil.Clamp(50 + m.Ergonomics + (b.Cat == "hg" ? 20 : 0) + (b.Cat == "smg" ? 10 : 0), 5, 100);
        double condF = 0.6 + 0.4 * (w.Condition / 100);
        double suppression = MathUtil.Clamp(m.Suppression, -30, 90);
        return new WeaponStats
        {
            Name = b.Name, Cat = b.Cat, Cal = b.Cal, Mode = b.Mode, Rpm = b.Rpm,
            Mag = (int)Math.Round(b.Mag * (1 + m.MagMult)),
            Spread = b.Spread * (1 - m.Accuracy / 100 * 0.9),
            Weight = weight, Handling = handling,
            Recoil = MathUtil.Clamp(b.Recoil + m.Recoil, 5, 100),
            Accuracy = MathUtil.Clamp(100 - b.Spread * 12 + m.Accuracy, 5, 100),
            AimSpeed = MathUtil.Clamp(handling * 0.8 + m.AimSpeed + 10, 5, 100),
            Ergonomics = ergonomics,
            Reliability = MathUtil.Clamp((b.Rel + m.Reliability) * condF, 5, 100),
            Suppression = suppression,
            Visibility = MathUtil.Clamp(50 + m.Visibility, 0, 100),
            Light = m.Light > 0, Range = m.Range,
            Dmg = cal.Dmg * (1 + m.Damage / 100), Pen = cal.Pen, Pellets = cal.Pellets,
            Suppressed = suppression > 50, Noise = 1 - suppression / 100,
            Reload = GameData.Categories[b.Cat].Reload * (1.25 - ergonomics / 200),
        };
    }

    public static int Value(WeaponInstance w)
    {
        int v = GameData.Weapon[w.Base].Price;
        foreach (var id in w.Att.Values) if (GameData.Attachment.TryGetValue(id, out var a)) v += a.Price;
        return v;
    }
}
