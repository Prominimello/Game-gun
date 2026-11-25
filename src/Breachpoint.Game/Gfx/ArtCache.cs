using System.Text.Json;
using Breachpoint.Core.Data;
using Breachpoint.Core.Sim;
using Breachpoint.Core.State;
using Microsoft.Xna.Framework.Graphics;

namespace Breachpoint.Gfx;

/// <summary>Paints procedural art on demand and caches the resulting GPU textures.</summary>
public sealed class ArtCache
{
    private readonly GraphicsDevice _gd;
    private readonly Dictionary<string, Texture2D> _cache = new();
    private readonly Queue<string> _order = new();
    private const int MaxEntries = 400;

    public ArtCache(GraphicsDevice gd) => _gd = gd;

    private Texture2D Get(string key, Func<Texture2D> make)
    {
        if (_cache.TryGetValue(key, out var t) && !t.IsDisposed) return t;
        t = make();
        _cache[key] = t;
        _order.Enqueue(key);
        while (_order.Count > MaxEntries)
        {
            var k = _order.Dequeue();
            if (_cache.Remove(k, out var old)) old.Dispose();
        }
        return t;
    }

    private Texture2D Supersampled(int w, int h, Action<Canvas> paint)
    {
        var c = new Canvas(w * 2, h * 2);
        paint(c);
        return c.Half().ToTexture(_gd);
    }

    public Texture2D Portrait(Operator op, int w, int h, bool gear = true)
    {
        string key = $"p|{op.Id}|{w}x{h}|{gear}|{op.Gender}|{op.Loadout.Helmet}|{JsonSerializer.Serialize(op.Look)}";
        return Get(key, () => Supersampled(w, h, c => Art.Portrait(c, op, gear)));
    }

    public Texture2D Weapon(WeaponInstance w, int pw, int ph, float scale, float yaw = 0, bool wear = true)
    {
        string att = string.Join(",", w.Att.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value));
        string key = $"w|{w.Base}|{att}|{w.Finish}|{w.Camo[0]}{w.Camo[1]}|{(int)(w.Condition / 10)}|{pw}x{ph}|{scale:0.00}|{yaw:0.00}|{wear}|{w.Uid}";
        return Get(key, () => Supersampled(pw, ph, c => Art.Weapon(c, w, pw, ph, scale * 2, yaw, wear)));
    }

    /// <summary>Uncached weapon render (for the live-rotating gunsmith view).</summary>
    public Texture2D WeaponFresh(WeaponInstance w, int pw, int ph, float scale, float yaw)
    {
        var c = new Canvas(pw, ph);
        Art.Weapon(c, w, pw / 2f, ph / 2f, scale, yaw);
        return c.ToTexture(_gd);
    }

    public const int SpriteSize = 96;     // pixels; unit sprite drawn at 0.5 scale → 48 world units
    public Texture2D Unit(UnitLook look, string pose, string weaponCat, bool shield, bool suppressed, bool isOp, bool armor)
    {
        string key = $"u|{look.Key}|{pose}|{weaponCat}|{shield}|{suppressed}|{isOp}|{armor}";
        return Get(key, () =>
        {
            var c = new Canvas(SpriteSize, SpriteSize);
            Art.UnitSprite(c, look, pose, weaponCat, shield, suppressed, isOp, armor, 2f);
            return c.ToTexture(_gd);
        });
    }

    public Texture2D Vehicle(VehicleDef def, VehicleInstance inst)
    {
        string key = $"v|{def.Id}|{inst?.Paint}|{inst?.Lights}|{string.Join(",", inst?.Accessories ?? new())}";
        return Get(key, () =>
        {
            int w = (int)(def.Len * 32 + 24), h = (int)(def.Wid * 32 + 24);
            var c = new Canvas(w, h);
            Art.Vehicle(c, def, inst, w / 2f, h / 2f, 0);
            return c.ToTexture(_gd);
        });
    }

    public Texture2D ItemIcon(string type) => Get("i|" + type, () =>
    {
        var c = new Canvas(64, 64);
        c.Scale(2, 2);
        Art.Item(c, type, 16, 16);
        return c.ToTexture(_gd);
    });
}
