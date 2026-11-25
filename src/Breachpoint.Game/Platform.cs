namespace Breachpoint;

/// <summary>Services that differ between desktop and Android.</summary>
public interface IPlatform
{
    bool IsMobile { get; }
    string DataDirectory { get; }
    /// <summary>Text-to-speech for operator callouts. May be a no-op.</summary>
    void Speak(string text, float pitch, float rate, bool priority, float volume);
    void StopSpeaking();
    void Vibrate(int milliseconds);
    /// <summary>Ask the user for a line of text (on-screen keyboard on mobile). Returns null if cancelled or unsupported.</summary>
    Task<string> PromptText(string title, string current);
    void Quit();
}

/// <summary>Fallback platform used when nothing else is provided.</summary>
public class DefaultPlatform : IPlatform
{
    public virtual bool IsMobile => false;
    public virtual string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Breachpoint");
    public virtual void Speak(string text, float pitch, float rate, bool priority, float volume) { }
    public virtual void StopSpeaking() { }
    public virtual void Vibrate(int milliseconds) { }
    public virtual Task<string> PromptText(string title, string current) => Task.FromResult<string>(null);
    public virtual void Quit() { }
}
