using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Breachpoint;

/// <summary>
/// Scripted self-test: run with "--autotest &lt;dir&gt;" to walk through every screen, play part of a mission,
/// and save screenshots. Used for CI smoke tests and verifying rendering on headless machines.
/// </summary>
public sealed class Autotest
{
    private readonly BreachpointGame _g;
    private readonly string _dir;
    private readonly Queue<(int frames, string shot, Action act)> _steps = new();
    private int _wait;
    private string _pendingShot;
    public bool Active => _dir != null;
    public readonly List<string> Errors = new();

    private Autotest(BreachpointGame g, string dir) { _g = g; _dir = dir; }

    public static Autotest FromArgs(BreachpointGame g, string[] args)
    {
        if (args == null) return new Autotest(g, null);
        int i = Array.IndexOf(args, "--autotest");
        if (i < 0 || i + 1 >= args.Length) return new Autotest(g, null);
        var t = new Autotest(g, args[i + 1]);
        Directory.CreateDirectory(t._dir);
        return t;
    }

    /// <summary>The scripted tour. Extended as screens are added.</summary>
    public void BuildScript()
    {
        Step(40, null, "01_title");
        Step(30, () => { _g.Profile = Core.State.ProfileService.NewGame(); _g.Switch(new Screens.HqScreen(_g)); }, "02_hq_command");
        foreach (var (id, i) in Screens.HqScreen.Tabs.Select((t, i) => (t.id, i)).Skip(1))
            Step(30, () => ((Screens.HqScreen)_g.Current).Tab = id, $"03_hq_{i}_{id}");
        Step(20, () => { var h = (Screens.HqScreen)_g.Current; h.Tab = "armory"; }, null);
        Step(30, () => Screens.Dialogs.NewCampaign(_g), "04_newgame");
        Step(5, () => _g.CloseModal(), null);
        Step(40, () => _g.Switch(new Screens.BriefScreen(_g, Core.State.MissionFactory.FromCampaign(_g.Profile, Core.Data.CampaignData.Campaign[1]))), "05_brief");
        Step(20, () => { var b = (Screens.BriefScreen)_g.Current; b.TestDeploy(); }, null);
        Step(30, null, "06_plan");
        Step(5, () => ((Screens.MissionScreen)_g.Current).TestBreachNearestDoor("flashclear"), null);
        Step(20, null, "07_plan_orders");
        Step(400, () => ((Screens.MissionScreen)_g.Current).TestExecute(), "08_live");
        Step(400, null, "09_live_later");
        Step(10, () => { ((Screens.MissionScreen)_g.Current).TestZoom(0.5f); }, "09b_zoomed");
        Step(10, () => { Gfx.MapRenderer.DebugNoVision = true; }, "09c_novision");
        Step(5, () => { Gfx.MapRenderer.DebugNoVision = false; }, null);
        Step(30, () => { if (_g.Current is Screens.MissionScreen ms) ms.TestComplete(); }, null);
        Step(40, null, "10_debrief");
        Step(60, () => _g.Switch(new Screens.ReplayScreen(_g, ((Screens.DebriefScreen)_g.Current).TestMission, _g.Current)), "11_replay");
        Step(60, () => ((Screens.ReplayScreen)_g.Current).TestBodyCam(), "12_bodycam");
        if (ExtraSteps != null) ExtraSteps(this);
    }
    public static Action<Autotest> ExtraSteps;

    public void Step(int frames, Action act, string shot = null) => _steps.Enqueue((frames, shot, act));

    public void Update(double dt)
    {
        if (!Active) return;
        if (_wait > 0) { _wait--; return; }
        if (_steps.Count == 0) { File.WriteAllText(Path.Combine(_dir, "result.txt"), Errors.Count == 0 ? "OK" : string.Join("\n", Errors)); _g.Exit(); return; }
        var (frames, shot, act) = _steps.Dequeue();
        try { act?.Invoke(); }
        catch (Exception e) { Errors.Add($"{shot ?? "step"}: {e}"); Console.Error.WriteLine(e); }
        _wait = frames;
        _pendingShot = shot;
    }

    public void AfterDraw()
    {
        if (!Active || _pendingShot == null || _wait > 1) return;
        var gd = _g.GraphicsDevice;
        int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
        var data = new Color[w * h];
        gd.GetBackBufferData(data);
        for (int i = 0; i < data.Length; i++) data[i].A = 255;
        using var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        using var fs = File.Create(Path.Combine(_dir, _pendingShot + ".png"));
        tex.SaveAsPng(fs, w, h);
        Console.WriteLine("screenshot " + _pendingShot);
        _pendingShot = null;
    }
}
