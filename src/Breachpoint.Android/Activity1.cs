using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Speech.Tts;
using Android.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Breachpoint.Android;

/// <summary>Android platform services: storage, native text-to-speech, vibration, on-screen keyboard.</summary>
internal sealed class AndroidPlatform : Java.Lang.Object, IPlatform, TextToSpeech.IOnInitListener
{
    private readonly Activity _activity;
    private readonly TextToSpeech _tts;
    private bool _ttsReady;

    public AndroidPlatform(Activity activity)
    {
        _activity = activity;
        _tts = new TextToSpeech(activity, this);
    }

    public bool IsMobile => true;
    public string DataDirectory => _activity.FilesDir!.AbsolutePath;

    public void OnInit(OperationResult status)
    {
        if (status == OperationResult.Success) { _tts.SetLanguage(Java.Util.Locale.Us); _ttsReady = true; }
    }

    public void Speak(string text, float pitch, float rate, bool priority, float volume)
    {
        if (!_ttsReady || string.IsNullOrEmpty(text)) return;
        if (!priority && _tts.IsSpeaking) return;
        _tts.SetPitch(Math.Clamp(pitch, 0.5f, 2f));
        _tts.SetSpeechRate(Math.Clamp(rate, 0.5f, 2f));
        var p = new Bundle();
        p.PutFloat(TextToSpeech.Engine.KeyParamVolume, Math.Clamp(volume, 0f, 1f));
        _tts.Speak(text, priority ? QueueMode.Flush : QueueMode.Add, p, "bp");
    }

    public void StopSpeaking() { if (_ttsReady) _tts.Stop(); }

    public void Vibrate(int milliseconds)
    {
        try
        {
            var v = (Vibrator)_activity.GetSystemService(Context.VibratorService)!;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O) v.Vibrate(VibrationEffect.CreateOneShot(milliseconds, VibrationEffect.DefaultAmplitude));
            else
#pragma warning disable CA1422
                v.Vibrate(milliseconds);
#pragma warning restore CA1422
        }
        catch { }
    }

    public Task<string> PromptText(string title, string current) => KeyboardInput.Show(title, "", current ?? "");

    public void Quit() => _activity.MoveTaskToBack(true);

    public void Shutdown() { _tts.Stop(); _tts.Shutdown(); }
}

[Activity(
    Label = "Breachpoint",
    MainLauncher = true,
    Icon = "@mipmap/icon",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode)]
public class Activity1 : AndroidGameActivity
{
    private BreachpointGame _game;
    private AndroidPlatform _platform;
    private global::Android.Views.View _view;

    protected override void OnCreate(Bundle bundle)
    {
        base.OnCreate(bundle);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P && Window != null)
            Window.Attributes!.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
        _platform = new AndroidPlatform(this);
        _game = new BreachpointGame(_platform);
        _view = (global::Android.Views.View)_game.Services.GetService(typeof(global::Android.Views.View));
        SetContentView(_view);
        HideSystemUi();
        _game.Run();
    }

    private void HideSystemUi()
    {
#pragma warning disable CA1422, CS0618
        if (Window?.DecorView != null)
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(SystemUiFlags.ImmersiveSticky | SystemUiFlags.Fullscreen | SystemUiFlags.HideNavigation
                | SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutFullscreen);
#pragma warning restore CA1422, CS0618
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) HideSystemUi();
    }

    protected override void OnPause()
    {
        // Pause a live mission and save when the app goes to the background.
        try { _game?.OnSuspend(); } catch { }
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        _platform?.Shutdown();
        base.OnDestroy();
    }
}
