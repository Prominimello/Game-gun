using System.Diagnostics;

namespace Breachpoint.Desktop;

/// <summary>Desktop (Windows / Linux / macOS) platform services.</summary>
internal sealed class DesktopPlatform : DefaultPlatform
{
    private readonly bool _canSpeak;
    private Process _speech;

    public DesktopPlatform()
    {
        // Optional voice callouts: uses the OS speech engine if one is available.
        _canSpeak = OperatingSystem.IsWindows() || (OperatingSystem.IsLinux() && File.Exists("/usr/bin/spd-say")) || OperatingSystem.IsMacOS();
    }

    public override void Speak(string text, float pitch, float rate, bool priority, float volume)
    {
        if (!_canSpeak) return;
        try
        {
            if (_speech != null && !_speech.HasExited) { if (!priority) return; _speech.Kill(); }
            string safe = new string(text.Where(ch => char.IsLetterOrDigit(ch) || " ,.!?'-".Contains(ch)).ToArray());
            ProcessStartInfo psi;
            if (OperatingSystem.IsWindows())
            {
                int vol = (int)Math.Clamp(volume * 100, 0, 100);
                psi = new ProcessStartInfo("powershell", $"-NoProfile -WindowStyle Hidden -Command \"Add-Type -AssemblyName System.Speech; $s=New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.Volume={vol}; $s.Rate=2; $s.Speak('{safe}')\"");
            }
            else if (OperatingSystem.IsMacOS()) psi = new ProcessStartInfo("say", $"-r 220 \"{safe}\"");
            else psi = new ProcessStartInfo("spd-say", $"-r 30 -p {(int)((pitch - 1) * 60)} \"{safe}\"");
            psi.CreateNoWindow = true; psi.UseShellExecute = false;
            _speech = Process.Start(psi);
        }
        catch { }
    }

    public override void StopSpeaking() { try { if (_speech != null && !_speech.HasExited) _speech.Kill(); } catch { } }
}

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var game = new BreachpointGame(new DesktopPlatform(), args);
        game.Run();
    }
}
