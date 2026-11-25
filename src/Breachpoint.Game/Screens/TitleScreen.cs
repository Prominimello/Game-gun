using Breachpoint.Core.Map;
using Breachpoint.Core.State;
using Breachpoint.Gfx;
using Breachpoint.UI;
using Microsoft.Xna.Framework;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

public sealed class TitleScreen : Screen
{
    private MapRenderer _bg;
    private GameMap _map;
    private double _t;

    public TitleScreen(BreachpointGame g) : base(g) { }

    public override void Enter()
    {
        _map = MapGenerator.Generate("compound", 4404);
        _bg = new MapRenderer(G, _map);
        G.Audio.Ambient("city");
        G.Audio.MusicTrack("hq");
    }

    public override void Leave() => _bg?.Dispose();

    public override void Update(double dt) => _t += dt;

    public override bool Back() { G.Platform.Quit(); G.Exit(); return true; }

    public override void Draw()
    {
        var cam = new Camera { X = _map.W * 16 + MathF.Cos((float)_t * 0.05f) * 300, Y = _map.H * 16 + MathF.Sin((float)_t * 0.04f) * 200, Zoom = 0.9f * Ui.S, Rot = (float)_t * 0.02f };
        _bg.Draw(P, new View { Map = _map, Floor = 0, Cam = cam, ShowAll = true, Night = true, RealTime = _t });
        P.Begin();
        // left-side darkening gradient
        P.Gradient(0, 0, Ui.W * Ui.S * 0.6f, Ui.H * Ui.S, C.Bg * 0.95f, C.Bg * 0.45f, true);
        P.Gradient(Ui.W * Ui.S * 0.6f, 0, Ui.W * Ui.S * 0.4f + 2, Ui.H * Ui.S, C.Bg * 0.45f, C.Bg * 0.2f, true);
        // radar
        float rcx = Ui.W * 0.75f, rcy = Ui.H * 0.5f, rr = Math.Min(Ui.W, Ui.H) * 0.3f;
        for (int i = 1; i <= 3; i++) P.Ring(rcx * Ui.S, rcy * Ui.S, rr * i / 3 * Ui.S, 1, C.Accent * 0.25f);
        float a0 = (float)_t * 1.2f;
        for (int i = 0; i < 20; i++) P.Line(rcx * Ui.S, rcy * Ui.S, (rcx + MathF.Cos(a0 - i * 0.02f) * rr) * Ui.S, (rcy + MathF.Sin(a0 - i * 0.02f) * rr) * Ui.S, 2 * Ui.S, C.Accent * (0.3f * (1 - i / 20f)));

        float x = Math.Max(30, Ui.W * 0.07f), y = Ui.H * 0.14f;
        Ui.Label("BREACHPOINT", x, y, 54, C.Hex("#e9eef2"), FontKind.Bold);
        Ui.Label("T A S K   F O R C E", x + 4, y + 70, 20, C.Accent, FontKind.Bold);
        Ui.Label("TACTICAL INTERVENTION UNIT — AUTHORIZED OPERATIONS", x + 4, y + 104, 11, C.Dim);
        y += 140;
        float bw = 300, bh = 44;
        if (G.Profile != null)
        {
            if (Ui.Button(new RectangleF(x, y, bw, bh), $"CONTINUE — DAY {G.Profile.Day}", BtnStyle.Menu, true, 14)) G.Switch(new HqScreen(G));
            y += bh + 8;
        }
        if (Ui.Button(new RectangleF(x, y, bw, bh), "NEW CAMPAIGN", BtnStyle.Menu, true, 14)) Dialogs.NewCampaign(G);
        y += bh + 8;
        if (Ui.Button(new RectangleF(x, y, bw, bh), "OPERATIONS & SANDBOX", BtnStyle.Menu, true, 14))
        {
            G.Profile ??= ProfileService.NewGame();
            G.SaveProfile();
            G.Switch(new HqScreen(G));
        }
        y += bh + 8;
        if (Ui.Button(new RectangleF(x, y, bw, bh), "SETTINGS", BtnStyle.Menu, true, 14)) Dialogs.Settings(G);
        y += bh + 8;
        if (Ui.Button(new RectangleF(x, y, bw, bh), "HOW TO PLAY", BtnStyle.Menu, true, 14)) Dialogs.HowTo(G);
        y += bh + 8;
        if (!G.Platform.IsMobile && Ui.Button(new RectangleF(x, y, bw, bh), "QUIT", BtnStyle.Menu, true, 14)) G.Exit();
        Ui.Label("All visuals procedurally drawn · All audio synthesized · Built with C# and MonoGame", x, Ui.H - 30, 10, C.Hex("#4d5a66"));
    }
}
