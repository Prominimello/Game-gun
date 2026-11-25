using Breachpoint.Audio;
using Breachpoint.Core.State;
using Breachpoint.Gfx;
using Breachpoint.Screens;
using Breachpoint.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Breachpoint;

/// <summary>Base class for all screens (title, HQ, briefing, mission, debrief, replay).</summary>
public abstract class Screen
{
    protected readonly BreachpointGame G;
    protected Screen(BreachpointGame g) => G = g;
    protected Gui Ui => G.Gui;
    protected Painter P => G.Painter;
    public virtual void Enter() { }
    public virtual void Leave() { }
    public virtual void Update(double dt) { }
    public abstract void Draw();
    /// <summary>Android back button / Escape. Return true if handled.</summary>
    public virtual bool Back() => false;
}

public sealed class Settings
{
    public float Master { get; set; } = 0.8f;
    public float Sfx { get; set; } = 0.9f;
    public float Ambient { get; set; } = 0.55f;
    public float Music { get; set; } = 0.35f;
    public bool Voice { get; set; } = true;
}

/// <summary>The MonoGame host: owns graphics, input, audio, the save profile and the active screen.</summary>
public sealed class BreachpointGame : Game
{
    public readonly GraphicsDeviceManager Graphics;
    public readonly IPlatform Platform;
    public Painter Painter { get; private set; }
    public Input Input { get; } = new();
    public Gui Gui { get; private set; }
    public SoundSystem Audio { get; private set; }
    public ArtCache Art { get; private set; }
    public Profile Profile;
    public Settings Settings = new();
    public Screen Current { get; private set; }
    private Screen _pending;
    private Func<bool> _modal;          // returns false to close
    private readonly List<(string text, double t)> _toasts = new();
    public double Time;
    public readonly Autotest Autotest;

    public string SavePath => Path.Combine(Platform.DataDirectory, "profile.json");
    private string SettingsPath => Path.Combine(Platform.DataDirectory, "settings.json");

    public BreachpointGame(IPlatform platform, string[] args = null)
    {
        Platform = platform ?? new DefaultPlatform();
        Graphics = new GraphicsDeviceManager(this)
        {
            PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = true,
            PreferMultiSampling = false,
        };
        if (Platform.IsMobile)
        {
            Graphics.IsFullScreen = true;
            Graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
        }
        else
        {
            Graphics.PreferredBackBufferWidth = 1600;
            Graphics.PreferredBackBufferHeight = 900;
            Window.AllowUserResizing = true;
        }
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        Window.Title = "BREACHPOINT: TASK FORCE";
        Autotest = Autotest.FromArgs(this, args);
    }

    protected override void Initialize()
    {
        base.Initialize();
        TouchPanel.EnabledGestures = GestureType.None;
        Window.TextInput += (_, e) => Input.TextChars.Add(e.Character);
        Window.ClientSizeChanged += (_, _) => { };
    }

    protected override void LoadContent()
    {
        Painter = new Painter(GraphicsDevice);
        Gui = new Gui(Painter, Input, Platform);
        Art = new ArtCache(GraphicsDevice);
        LoadSettings();
        Audio = new SoundSystem(this);
        Gui.Click = () => Audio.Ui();
        Profile = ProfileService.Load(SavePath);
        Switch(new TitleScreen(this));
        if (Autotest.Active) Autotest.BuildScript();
    }

    public void LoadSettings()
    {
        try { if (File.Exists(SettingsPath)) Settings = System.Text.Json.JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings(); }
        catch { Settings = new Settings(); }
    }

    public void SaveSettings()
    {
        try { Directory.CreateDirectory(Platform.DataDirectory); File.WriteAllText(SettingsPath, System.Text.Json.JsonSerializer.Serialize(Settings)); } catch { }
        Audio?.ApplyVolumes();
    }

    public void SaveProfile()
    {
        if (Profile == null || Autotest.Active) return;
        try { ProfileService.Save(Profile, SavePath); } catch (Exception e) { Toast("Save failed: " + e.Message); }
    }

    public bool HasSave => File.Exists(SavePath);

    /// <summary>Called when the app is backgrounded (Android): pause any live mission and save.</summary>
    public void OnSuspend()
    {
        if (Current is MissionScreen ms && ms.M.Phase == "live" && !ms.M.Ended) ms.M.Paused = true;
        SaveProfile();
        SaveSettings();
        Platform.StopSpeaking();
    }

    public void Switch(Screen s) => _pending = s;

    public void Modal(Func<bool> draw) => _modal = draw;
    public void CloseModal() => _modal = null;
    public bool ModalOpen => _modal != null;

    public void Toast(string text) { _toasts.Add((text, Time)); if (_toasts.Count > 4) _toasts.RemoveAt(0); }

    protected override void Update(GameTime gameTime)
    {
        Time = gameTime.TotalGameTime.TotalSeconds;
        if (_pending != null)
        {
            Current?.Leave();
            Current = _pending; _pending = null;
            Current.Enter();
        }
        Input.Update(gameTime, IsActive || Platform.IsMobile);
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed && !_backHeld)
        {
            _backHeld = true;
            if (_modal != null) _modal = null;
            else if (Current == null || !Current.Back()) { if (Platform.IsMobile) Platform.Quit(); }
        }
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Released) _backHeld = false;
        double dt = Math.Min(0.05, gameTime.ElapsedGameTime.TotalSeconds);
        Audio?.Update();
        Autotest?.Update(dt);
        base.Update(gameTime);
    }
    private bool _backHeld;
    private double _lastErrorToast = -10;

    protected override void Draw(GameTime gameTime)
    {
        var vp = GraphicsDevice.Viewport;
        GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Colors.Bg, 1, 0);
        Gui.BeginFrame(vp.Width, vp.Height);
        double dt = Math.Min(0.05, gameTime.ElapsedGameTime.TotalSeconds);
        if (Current != null)
        {
            Gui.Enabled = _modal == null;
            try
            {
                Current.Update(dt);
                Current.Draw();
            }
            catch (Exception e)
            {
                // Keep the game alive; report the problem instead of crashing (important on phones).
                Console.Error.WriteLine(e);
                Autotest.Errors.Add(e.ToString());
                if ((Time - _lastErrorToast) > 5) { _lastErrorToast = Time; Toast("Error: " + e.Message); }
                try { Painter.SetClip(null); } catch { }
            }
            Painter.End();
        }
        if (_modal != null)
        {
            Gui.Enabled = true;
            Painter.Begin();
            Gui.Fill(new RectangleF(0, 0, Gui.W, Gui.H), Color.Black * 0.65f);
            var keep = _modal();
            if (!keep) _modal = null;
            Painter.End();
        }
        DrawToasts();
        Painter.End();
        Autotest?.AfterDraw();
        base.Draw(gameTime);
    }

    private void DrawToasts()
    {
        _toasts.RemoveAll(t => Time - t.t > 3.2);
        Painter.Begin();
        float y = 60;
        foreach (var (text, t) in _toasts)
        {
            float a = (float)Math.Clamp(Math.Min((Time - t) * 6, (3.2 - (Time - t)) * 2), 0, 1);
            float w = Gui.TextW(text, 13) + 32;
            var r = new RectangleF(Gui.W / 2 - w / 2, y, w, 30);
            Gui.Fill(r, Colors.Hex("#16202a") * a); Gui.Border(r, Colors.Accent * a);
            Gui.Label(text, Gui.W / 2, y + 7, 13, Colors.Text * a, FontKind.Regular, Align.Center);
            y += 36;
        }
    }

    /// <summary>Spend money with feedback. Returns false (and complains) if unaffordable.</summary>
    public bool Spend(long amount, string what = null)
    {
        if (Profile.Money < amount) { Toast("Insufficient funds."); Audio.Error(); return false; }
        Profile.Money -= amount;
        Audio.Cash();
        if (what != null) Toast($"{what} — {Core.Util.MathUtil.Money(amount)}");
        return true;
    }
}
